using Farol.Domain.Ledger;
using Farol.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farol.Infrastructure.Persistence.Configurations;

public sealed class FinancialAccountConfiguration : IEntityTypeConfiguration<FinancialAccount>
{
    public void Configure(EntityTypeBuilder<FinancialAccount> builder)
    {
        builder.ToTable("financial_accounts");

        builder.HasKey(account => account.Id);

        builder.HasIndex(account => new { account.UserId, account.Name });

        builder.Property(account => account.UserId)
            .IsRequired();

        builder.Property(account => account.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(account => account.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(account => account.IsActive)
            .IsRequired();

        builder.Property(account => account.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(account => account.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
