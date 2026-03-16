using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farol.Infrastructure.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(transaction => transaction.Id);

        builder.HasIndex(transaction => new { transaction.UserId, transaction.OccurredOn });

        builder.HasIndex(transaction => new { transaction.UserId, transaction.CategoryId, transaction.OccurredOn });

        builder.HasIndex(transaction => new { transaction.FinancialAccountId, transaction.OccurredOn });

        builder.Property(transaction => transaction.UserId)
            .IsRequired();

        builder.Property(transaction => transaction.FinancialAccountId)
            .IsRequired();

        builder.Property(transaction => transaction.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(transaction => transaction.Amount)
            .IsRequired()
            .HasPrecision(14, 2);

        builder.Property(transaction => transaction.Description)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(transaction => transaction.OccurredOn)
            .IsRequired();

        builder.Property(transaction => transaction.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(transaction => transaction.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<FinancialAccount>()
            .WithMany()
            .HasForeignKey(transaction => transaction.FinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(transaction => transaction.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
