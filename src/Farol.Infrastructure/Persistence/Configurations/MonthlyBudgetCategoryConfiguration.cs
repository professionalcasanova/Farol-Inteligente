using Farol.Domain.Budgets;
using Farol.Domain.Categories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farol.Infrastructure.Persistence.Configurations;

public sealed class MonthlyBudgetCategoryConfiguration : IEntityTypeConfiguration<MonthlyBudgetCategory>
{
    public void Configure(EntityTypeBuilder<MonthlyBudgetCategory> builder)
    {
        builder.ToTable("monthly_budget_categories");

        builder.HasKey(item => item.Id);

        builder.HasIndex(item => new { item.MonthlyBudgetId, item.CategoryId })
            .IsUnique();

        builder.Property(item => item.MonthlyBudgetId)
            .IsRequired();

        builder.Property(item => item.CategoryId)
            .IsRequired();

        builder.Property(item => item.PlannedAmount)
            .IsRequired()
            .HasPrecision(14, 2);

        builder.HasOne<MonthlyBudget>()
            .WithMany()
            .HasForeignKey(item => item.MonthlyBudgetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(item => item.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
