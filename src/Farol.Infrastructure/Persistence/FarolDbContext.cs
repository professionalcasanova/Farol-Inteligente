using Farol.Domain.Bills;
using Farol.Domain.Budgets;
using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Farol.Infrastructure.Persistence;

public sealed class FarolDbContext(DbContextOptions<FarolDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillSeries> BillSeries => Set<BillSeries>();
    public DbSet<CommunityBudget> CommunityBudgets => Set<CommunityBudget>();
    public DbSet<CommunityBudgetItem> CommunityBudgetItems => Set<CommunityBudgetItem>();
    public DbSet<CommunityBudgetReport> CommunityBudgetReports => Set<CommunityBudgetReport>();
    public DbSet<BudgetTemplate> BudgetTemplates => Set<BudgetTemplate>();
    public DbSet<BudgetTemplateCategory> BudgetTemplateCategories => Set<BudgetTemplateCategory>();
    public DbSet<MonthlyBudget> MonthlyBudgets => Set<MonthlyBudget>();
    public DbSet<MonthlyBudgetCategory> MonthlyBudgetCategories => Set<MonthlyBudgetCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FarolDbContext).Assembly);
    }
}
