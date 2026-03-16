using Farol.Domain.Ledger;

namespace Farol.Tests.Domain;

public sealed class FinancialAccountTests
{
    [Fact]
    public void Constructor_ShouldCreateActiveAccount()
    {
        var account = new FinancialAccount(Guid.NewGuid(), "Conta Corrente", FinancialAccountType.BankAccount);

        Assert.True(account.IsActive);
    }

    [Fact]
    public void UpdateDetails_ShouldTrimName()
    {
        var account = new FinancialAccount(Guid.NewGuid(), "Conta", FinancialAccountType.BankAccount);

        account.UpdateDetails("  Conta Principal  ", FinancialAccountType.BankAccount);

        Assert.Equal("Conta Principal", account.Name);
    }
}
