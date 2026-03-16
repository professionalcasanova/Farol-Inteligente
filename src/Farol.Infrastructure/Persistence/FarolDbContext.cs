using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Farol.Infrastructure.Persistence;

public sealed class FarolDbContext(DbContextOptions<FarolDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FarolDbContext).Assembly);
    }
}
