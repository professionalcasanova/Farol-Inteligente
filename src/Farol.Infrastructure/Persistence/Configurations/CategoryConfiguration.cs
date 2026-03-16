using Farol.Domain.Categories;
using Farol.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farol.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(category => category.Id);

        builder.HasIndex(category => new { category.UserId, category.Type, category.Name });

        builder.Property(category => category.UserId)
            .IsRequired(false);

        builder.Property(category => category.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(category => category.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(category => category.IsSystem)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(category => category.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
