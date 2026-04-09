using Farol.Domain.Budgets;
using Farol.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farol.Infrastructure.Persistence.Configurations;

public sealed class BudgetTemplateConfiguration : IEntityTypeConfiguration<BudgetTemplate>
{
    public void Configure(EntityTypeBuilder<BudgetTemplate> builder)
    {
        builder.ToTable("budget_templates");

        builder.HasKey(template => template.Id);

        builder.HasIndex(template => template.UserId)
            .IsUnique();

        builder.Property(template => template.UserId)
            .IsRequired();

        builder.Property(template => template.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(template => template.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
