using Farol.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farol.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(token => token.Id);

        builder.HasIndex(token => token.Token)
            .IsUnique();

        builder.HasIndex(token => new { token.UserId, token.Revoked, token.ExpiresAtUtc });

        builder.Property(token => token.Token)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(token => token.ExpiresAtUtc)
            .IsRequired();

        builder.Property(token => token.Revoked)
            .IsRequired();

        builder.Property(token => token.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
