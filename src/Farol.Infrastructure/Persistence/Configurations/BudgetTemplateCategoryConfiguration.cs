using Farol.Domain.Budgets;
using Farol.Domain.Categories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farol.Infrastructure.Persistence.Configurations;

public sealed class BudgetTemplateCategoryConfiguration : IEntityTypeConfiguration<BudgetTemplateCategory>
{
    public void Configure(EntityTypeBuilder<BudgetTemplateCategory> builder)
    {
        builder.ToTable("budget_template_categories");

        builder.HasKey(item => item.Id);

        builder.HasIndex(item => new { item.BudgetTemplateId, item.CategoryId })
            .IsUnique();

        builder.Property(item => item.BudgetTemplateId)
            .IsRequired();

        builder.Property(item => item.CategoryId)
            .IsRequired();

        builder.Property(item => item.PlannedAmount)
            .IsRequired()
            .HasPrecision(14, 2);

        builder.HasOne<BudgetTemplate>()
            .WithMany()
            .HasForeignKey(item => item.BudgetTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(item => item.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
