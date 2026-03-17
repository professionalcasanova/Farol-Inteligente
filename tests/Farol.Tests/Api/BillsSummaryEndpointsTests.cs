using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Farol.Api.Modules.Auth;
using Farol.Api.Modules.Dashboard;
using Farol.Domain.Bills;
using Farol.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Tests.Api;

public sealed class BillsSummaryEndpointsTests : IClassFixture<FarolApiFactory>
{
    private readonly FarolApiFactory _factory;

    public BillsSummaryEndpointsTests(FarolApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetBillsSummary_ShouldRequireAuthentication()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/dashboard/bills-summary?month=3&year=2026");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBillsSummary_ShouldCalculatePendingOverdueAndPaid()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var month = today.Month;
        var year = today.Year;

        await SeedBillAsync("maria@email.com", "Internet", 100m, today.AddDays(2));
        await SeedBillAsync("maria@email.com", "Celular", 80m, today.AddDays(5));
        await SeedBillAsync("maria@email.com", "Energia", 50m, today.AddDays(-3));
        await SeedBillAsync("maria@email.com", "Aluguel", 900m, today.AddDays(-6), isPaid: true);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var summary = await client.GetFromJsonAsync<BillsSummaryResponse>(
            $"/api/dashboard/bills-summary?month={month}&year={year}");

        Assert.NotNull(summary);
        Assert.Equal(180m, summary.TotalPending);
        Assert.Equal(50m, summary.TotalOverdue);
        Assert.Equal(900m, summary.TotalPaid);
        Assert.Equal(2, summary.CountPending);
        Assert.Equal(1, summary.CountOverdue);
        Assert.Equal(1, summary.CountPaid);
    }

    [Fact]
    public async Task GetBillsSummary_ShouldReturnUpcomingPendingOrderedByDueOn()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var month = today.Month;
        var year = today.Year;

        await SeedBillAsync("maria@email.com", "Bill 3", 30m, today.AddDays(5));
        await SeedBillAsync("maria@email.com", "Bill 1", 10m, today.AddDays(1));
        await SeedBillAsync("maria@email.com", "Bill 2", 20m, today.AddDays(3));
        await SeedBillAsync("maria@email.com", "Bill 4", 40m, today.AddDays(7));
        await SeedBillAsync("maria@email.com", "Bill 5", 50m, today.AddDays(9));
        await SeedBillAsync("maria@email.com", "Bill 6", 60m, today.AddDays(11));
        await SeedBillAsync("maria@email.com", "Bill paid", 70m, today.AddDays(2), isPaid: true);
        await SeedBillAsync("maria@email.com", "Bill overdue", 80m, today.AddDays(-2));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var summary = await client.GetFromJsonAsync<BillsSummaryResponse>(
            $"/api/dashboard/bills-summary?month={month}&year={year}");

        Assert.NotNull(summary);
        Assert.Equal(5, summary.Upcoming.Count);
        Assert.Equal(["Bill 1", "Bill 2", "Bill 3", "Bill 4", "Bill 5"], summary.Upcoming.Select(item => item.Description).ToArray());
        Assert.All(summary.Upcoming, item => Assert.Equal("pending", item.Status));
    }

    [Fact]
    public async Task GetBillsSummary_ShouldBeIsolatedByAuthenticatedUser()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        await RegisterAndGetTokenAsync(client, "joao@email.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var month = today.Month;
        var year = today.Year;

        await SeedBillAsync("maria@email.com", "Internet", 100m, today.AddDays(2));
        await SeedBillAsync("joao@email.com", "Aluguel", 1200m, today.AddDays(2));
        await SeedBillAsync("joao@email.com", "Luz", 200m, today.AddDays(-1));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);

        var summary = await client.GetFromJsonAsync<BillsSummaryResponse>(
            $"/api/dashboard/bills-summary?month={month}&year={year}");

        Assert.NotNull(summary);
        Assert.Equal(100m, summary.TotalPending);
        Assert.Equal(0m, summary.TotalOverdue);
        Assert.Equal(0m, summary.TotalPaid);
        Assert.Single(summary.Upcoming);
        Assert.Equal("Internet", summary.Upcoming[0].Description);
    }

    private async Task<Guid> SeedBillAsync(
        string email,
        string description,
        decimal amount,
        DateOnly dueOn,
        bool isPaid = false)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var userId = dbContext.Users.Single(user => user.Email == email).Id;
        var bill = new Bill(userId, description, amount, dueOn);

        if (isPaid)
        {
            bill.MarkAsPaid(new DateTimeOffset(2026, 3, 17, 12, 0, 0, TimeSpan.Zero));
        }

        dbContext.Bills.Add(bill);
        await dbContext.SaveChangesAsync();

        return bill.Id;
    }

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Name = "Usuario Teste",
            Email = email,
            Password = "123456"
        });

        response.EnsureSuccessStatusCode();

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(authResponse);

        return authResponse.AccessToken;
    }
}
