using Farol.Domain.Budgets;
using Farol.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farol.Infrastructure.Persistence.Configurations;

public sealed class MonthlyBudgetConfiguration : IEntityTypeConfiguration<MonthlyBudget>
{
    public void Configure(EntityTypeBuilder<MonthlyBudget> builder)
    {
        builder.ToTable("monthly_budgets");

        builder.HasKey(budget => budget.Id);

        builder.HasIndex(budget => new { budget.UserId, budget.Month, budget.Year })
            .IsUnique();

        builder.Property(budget => budget.UserId)
            .IsRequired();

        builder.Property(budget => budget.Month)
            .IsRequired();

        builder.Property(budget => budget.Year)
            .IsRequired();

        builder.Property(budget => budget.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(budget => budget.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
