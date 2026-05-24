using Farol.Domain.Budgets;
using Farol.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farol.Infrastructure.Persistence.Configurations;

public sealed class CommunityBudgetConfiguration : IEntityTypeConfiguration<CommunityBudget>
{
    public void Configure(EntityTypeBuilder<CommunityBudget> builder)
    {
        builder.ToTable("community_budgets");

        builder.HasKey(budget => budget.Id);

        builder.HasIndex(budget => budget.OwnerUserId);

        builder.HasIndex(budget => new { budget.IsPublic, budget.CreatedAtUtc });

        builder.HasIndex(budget => new { budget.Status, budget.CreatedAtUtc });

        builder.Property(budget => budget.OwnerUserId)
            .IsRequired();

        builder.Property(budget => budget.Title)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(budget => budget.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(budget => budget.TargetProfile)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(budget => budget.MonthlyIncomeReference)
            .HasPrecision(14, 2);

        builder.Property(budget => budget.IsPublic)
            .IsRequired();

        builder.Property(budget => budget.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(budget => budget.CreatedAtUtc)
            .IsRequired();

        builder.Property(budget => budget.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(budget => budget.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
