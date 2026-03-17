using Farol.Domain.Bills;
using Farol.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farol.Infrastructure.Persistence.Configurations;

public sealed class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.ToTable("bills");

        builder.HasKey(bill => bill.Id);

        builder.HasIndex(bill => new { bill.UserId, bill.DueOn });

        builder.HasIndex(bill => new { bill.UserId, bill.IsPaid, bill.DueOn });

        builder.Property(bill => bill.UserId)
            .IsRequired();

        builder.Property(bill => bill.Description)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(bill => bill.Amount)
            .IsRequired()
            .HasPrecision(14, 2);

        builder.Property(bill => bill.DueOn)
            .IsRequired();

        builder.Property(bill => bill.IsPaid)
            .IsRequired();

        builder.Property(bill => bill.PaidAtUtc);

        builder.Property(bill => bill.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(bill => bill.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
