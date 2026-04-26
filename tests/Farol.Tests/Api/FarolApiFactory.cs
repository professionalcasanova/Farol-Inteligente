using Farol.Api.Modules.Insights;
using Farol.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Farol.Tests.Api;

public sealed class FarolApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"FarolTests-{Guid.NewGuid()}";
    private readonly FakeFinancialIntelligenceClient _financialIntelligenceClient = new();
    private readonly TimeProvider _timeProvider = new FixedTimeProvider(
        new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));

    public FakeFinancialIntelligenceClient FinancialIntelligenceClient => _financialIntelligenceClient;
    public DateOnly Today => DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(FarolApiFactory).Assembly);

            services.RemoveAll<DbContextOptions<FarolDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<FarolDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<FarolDbContext>>();
            services.RemoveAll<IFinancialIntelligenceClient>();
            services.RemoveAll<TimeProvider>();

            services.AddDbContext<FarolDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            services.AddSingleton<IFinancialIntelligenceClient>(_financialIntelligenceClient);
            services.AddSingleton(_timeProvider);
        });
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        _financialIntelligenceClient.Reset();
    }
}
