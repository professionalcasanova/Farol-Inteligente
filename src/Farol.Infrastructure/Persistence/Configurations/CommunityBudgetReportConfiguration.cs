using Farol.Domain.Budgets;
using Farol.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farol.Infrastructure.Persistence.Configurations;

public sealed class CommunityBudgetReportConfiguration : IEntityTypeConfiguration<CommunityBudgetReport>
{
    public void Configure(EntityTypeBuilder<CommunityBudgetReport> builder)
    {
        builder.ToTable("community_budget_reports");

        builder.HasKey(report => report.Id);

        builder.HasIndex(report => report.CommunityBudgetId);

        builder.HasIndex(report => new { report.CommunityBudgetId, report.ReporterUserId })
            .IsUnique();

        builder.Property(report => report.CommunityBudgetId)
            .IsRequired();

        builder.Property(report => report.ReporterUserId)
            .IsRequired();

        builder.Property(report => report.Reason)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(report => report.Description)
            .HasMaxLength(500);

        builder.Property(report => report.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<CommunityBudget>()
            .WithMany()
            .HasForeignKey(report => report.CommunityBudgetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(report => report.ReporterUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
