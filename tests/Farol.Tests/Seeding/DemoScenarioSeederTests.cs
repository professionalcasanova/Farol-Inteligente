using Farol.Infrastructure.Auth;
using Farol.Infrastructure.Persistence;
using Farol.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Tests.Seeding;

public sealed class DemoScenarioSeederTests
{
    [Fact]
    public async Task SeedDemoScenarios_ShouldCreateScenarioUsersAndData()
    {
        await using var services = BuildServices();
        await SeedSystemCategoriesAsync(services);

        await DemoScenarioSeeder.SeedDemoScenariosAsync(services);

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<PasswordService>();

        Assert.Equal(DemoScenarioSeeder.Scenarios.Count, await dbContext.Users.CountAsync());

        var healthyUser = await dbContext.Users.SingleAsync(user => user.Email == "saudavel@farol.local");
        Assert.True(passwordService.VerifyPassword(healthyUser, DemoScenarioSeeder.DemoPassword));

        var lowDataUser = await dbContext.Users.SingleAsync(user => user.Email == "poucosdados@farol.local");
        Assert.True(await dbContext.FinancialAccounts.AnyAsync(account => account.UserId == lowDataUser.Id));
        Assert.False(await dbContext.Transactions.AnyAsync(transaction => transaction.UserId == lowDataUser.Id));
        Assert.False(await dbContext.Bills.AnyAsync(bill => bill.UserId == lowDataUser.Id));

        var overdueUser = await dbContext.Users.SingleAsync(user => user.Email == "vencido@farol.local");
        Assert.True(await dbContext.Bills.AnyAsync(bill => bill.UserId == overdueUser.Id && !bill.IsPaid));
    }

    [Fact]
    public async Task SeedDemoScenarios_ShouldBeIdempotent()
    {
        await using var services = BuildServices();
        await SeedSystemCategoriesAsync(services);

        await DemoScenarioSeeder.SeedDemoScenariosAsync(services);
        await DemoScenarioSeeder.SeedDemoScenariosAsync(services);

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

        Assert.Equal(DemoScenarioSeeder.Scenarios.Count, await dbContext.Users.CountAsync());
        Assert.Equal(DemoScenarioSeeder.Scenarios.Count, await dbContext.FinancialAccounts.CountAsync());
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        var databaseName = $"FarolDemoSeedTests-{Guid.NewGuid()}";
        services.AddDbContext<FarolDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddScoped<PasswordService>();
        return services.BuildServiceProvider();
    }

    private static async Task SeedSystemCategoriesAsync(ServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

        foreach (var (type, name) in CategorySeed.SystemCategories)
        {
            dbContext.Categories.Add(Farol.Domain.Categories.Category.CreateSystem(name, type));
        }

        await dbContext.SaveChangesAsync();
    }
}
