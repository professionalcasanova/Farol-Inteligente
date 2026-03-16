using Farol.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farol.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.HasIndex(user => user.Email)
            .IsUnique();

        builder.Property(user => user.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(user => user.Email)
            .IsRequired()
            .HasMaxLength(254);

        builder.Property(user => user.PasswordHash)
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc)
            .IsRequired();
    }
}
