using Farol.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farol.Infrastructure.Persistence.Configurations;

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

        builder.HasKey(token => token.Id);

        builder.HasIndex(token => token.Token)
            .IsUnique();

        builder.HasIndex(token => new { token.UserId, token.Used, token.ExpiresAtUtc });

        builder.Property(token => token.Token)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(token => token.ExpiresAtUtc)
            .IsRequired();

        builder.Property(token => token.Used)
            .IsRequired();

        builder.Property(token => token.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
