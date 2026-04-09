using Farol.Domain.Bills;
using Farol.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farol.Infrastructure.Persistence.Configurations;

public sealed class BillSeriesConfiguration : IEntityTypeConfiguration<BillSeries>
{
    public void Configure(EntityTypeBuilder<BillSeries> builder)
    {
        builder.ToTable("bill_series");

        builder.HasKey(series => series.Id);

        builder.HasIndex(series => new { series.UserId, series.IsActive, series.FirstDueOn });

        builder.Property(series => series.UserId)
            .IsRequired();

        builder.Property(series => series.Description)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(series => series.Amount)
            .IsRequired()
            .HasPrecision(14, 2);

        builder.Property(series => series.FirstDueOn)
            .IsRequired();

        builder.Property(series => series.Kind)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(series => series.Frequency)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(series => series.EndMode)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(series => series.UntilDate);

        builder.Property(series => series.OccurrenceCount);

        builder.Property(series => series.IsActive)
            .IsRequired();

        builder.Property(series => series.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(series => series.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
