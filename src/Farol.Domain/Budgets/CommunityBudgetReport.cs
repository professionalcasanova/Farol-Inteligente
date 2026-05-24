namespace Farol.Domain.Budgets;

public sealed class CommunityBudgetReport
{
    private const int DescriptionMaxLength = 500;

    private CommunityBudgetReport()
    {
    }

    public Guid Id { get; private set; }
    public Guid CommunityBudgetId { get; private set; }
    public Guid ReporterUserId { get; private set; }
    public CommunityBudgetReportReason Reason { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public CommunityBudgetReport(
        Guid communityBudgetId,
        Guid reporterUserId,
        CommunityBudgetReportReason reason,
        string? description)
    {
        Id = Guid.NewGuid();
        CommunityBudgetId = EnsureId(communityBudgetId, nameof(communityBudgetId), "Community budget report must belong to a budget.");
        ReporterUserId = EnsureId(reporterUserId, nameof(reporterUserId), "Community budget report must belong to a reporter.");
        Reason = reason;
        Description = EnsureOptionalDescription(description);
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static Guid EnsureId(Guid value, string parameterName, string message)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(message, parameterName);
        }

        return value;
    }

    private static string? EnsureOptionalDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > DescriptionMaxLength)
        {
            throw new ArgumentException($"Community budget report description cannot exceed {DescriptionMaxLength} characters.", nameof(value));
        }

        SensitiveBudgetTextValidator.EnsureSafe(normalized, nameof(value));

        return normalized;
    }
}
