using Farol.Domain.Budgets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farol.Infrastructure.Persistence.Configurations;

public sealed class CommunityBudgetItemConfiguration : IEntityTypeConfiguration<CommunityBudgetItem>
{
    public void Configure(EntityTypeBuilder<CommunityBudgetItem> builder)
    {
        builder.ToTable("community_budget_items");

        builder.HasKey(item => item.Id);

        builder.HasIndex(item => new { item.CommunityBudgetId, item.SortOrder });

        builder.Property(item => item.CommunityBudgetId)
            .IsRequired();

        builder.Property(item => item.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(item => item.CategoryName)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(item => item.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(item => item.AllocationType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(item => item.Amount)
            .HasPrecision(14, 2);

        builder.Property(item => item.Percentage)
            .HasPrecision(5, 2);

        builder.Property(item => item.Notes)
            .HasMaxLength(500);

        builder.Property(item => item.SortOrder)
            .IsRequired();

        builder.HasOne<CommunityBudget>()
            .WithMany()
            .HasForeignKey(item => item.CommunityBudgetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
