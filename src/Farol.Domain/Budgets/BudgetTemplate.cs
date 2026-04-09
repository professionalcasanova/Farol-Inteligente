namespace Farol.Domain.Budgets;

public sealed class BudgetTemplate
{
    private BudgetTemplate()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public BudgetTemplate(Guid userId)
    {
        Id = Guid.NewGuid();
        UserId = EnsureUserId(userId);
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static Guid EnsureUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Budget template must belong to a user.", nameof(userId));
        }

        return userId;
    }
}
