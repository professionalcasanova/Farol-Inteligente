using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Farol.Api.Modules.Auth;
using Farol.Api.Modules.Bills;
using Farol.Domain.Bills;
using Farol.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Tests.Api;

public sealed class BillsEndpointsTests : IClassFixture<FarolApiFactory>
{
    private readonly FarolApiFactory _factory;

    public BillsEndpointsTests(FarolApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetBills_ShouldRequireAuthentication()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/bills");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostBills_ShouldCreateBillForAuthenticatedUser()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var dueOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/bills", new CreateBillRequest
        {
            Description = "Internet",
            Amount = 99.90m,
            DueOn = dueOn
        });

        response.EnsureSuccessStatusCode();

        var bill = await response.Content.ReadFromJsonAsync<BillResponse>();

        Assert.NotNull(bill);
        Assert.Equal("Internet", bill.Description);
        Assert.Equal(99.90m, bill.Amount);
        Assert.Equal(dueOn, bill.DueOn);
        Assert.False(bill.IsPaid);
        Assert.Equal("pending", bill.Status);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        Assert.Single(dbContext.Bills);
    }

    [Fact]
    public async Task GetBills_ShouldReturnOnlyBillsFromAuthenticatedUser()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        await SeedBillAsync("maria@email.com", "Internet", 99.90m, new DateOnly(2026, 3, 25));

        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");
        await SeedBillAsync("joao@email.com", "Aluguel", 1200m, new DateOnly(2026, 3, 10));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);

        var bills = await client.GetFromJsonAsync<List<BillResponse>>("/api/bills");

        Assert.NotNull(bills);
        Assert.Single(bills);
        Assert.Equal("Internet", bills[0].Description);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);
        var otherBills = await client.GetFromJsonAsync<List<BillResponse>>("/api/bills");

        Assert.NotNull(otherBills);
        Assert.Single(otherBills);
        Assert.Equal("Aluguel", otherBills[0].Description);
    }

    [Fact]
    public async Task PatchPay_ShouldMarkOwnedBillAsPaid()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var billId = await SeedBillAsync("maria@email.com", "Internet", 99.90m, new DateOnly(2026, 3, 25));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/bills/{billId}/pay");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var bill = await response.Content.ReadFromJsonAsync<BillResponse>();

        Assert.NotNull(bill);
        Assert.True(bill.IsPaid);
        Assert.NotNull(bill.PaidAtUtc);
        Assert.Equal("paid", bill.Status);
    }

    [Fact]
    public async Task PatchUnpay_ShouldClearPaidAtUtc()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var dueOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
        var billId = await SeedBillAsync(
            "maria@email.com",
            "Internet",
            99.90m,
            dueOn,
            isPaid: true);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/bills/{billId}/unpay");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var bill = await response.Content.ReadFromJsonAsync<BillResponse>();

        Assert.NotNull(bill);
        Assert.False(bill.IsPaid);
        Assert.Null(bill.PaidAtUtc);
        Assert.Equal("pending", bill.Status);
    }

    [Fact]
    public async Task PatchPay_ShouldNotAllowAnotherUsersBill()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        await RegisterAndGetTokenAsync(client, "joao@email.com");
        var joaoBillId = await SeedBillAsync("joao@email.com", "Aluguel", 1200m, new DateOnly(2026, 3, 10));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/bills/{joaoBillId}/pay");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchPay_NonexistentBill_ReturnsNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/bills/{Guid.NewGuid()}/pay");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchUnpay_NonexistentBill_ReturnsNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/bills/{Guid.NewGuid()}/unpay");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchUnpay_BillFromAnotherUser_ReturnsNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        await RegisterAndGetTokenAsync(client, "joao@email.com");
        var joaoBillId = await SeedBillAsync("joao@email.com", "Aluguel", 1200m, new DateOnly(2026, 3, 10), isPaid: true);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/bills/{joaoBillId}/unpay");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBills_ShouldFilterByStatus()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await SeedBillAsync("maria@email.com", "Internet", 99.90m, today.AddDays(-1));
        await SeedBillAsync("maria@email.com", "Streaming", 39.90m, today.AddDays(2));
        await SeedBillAsync("maria@email.com", "Aluguel", 1200m, today.AddDays(-3), isPaid: true);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var bills = await client.GetFromJsonAsync<List<BillResponse>>("/api/bills?status=overdue");

        Assert.NotNull(bills);
        Assert.Single(bills);
        Assert.Equal("Internet", bills[0].Description);
        Assert.Equal("overdue", bills[0].Status);
    }

    [Fact]
    public async Task GetBills_InvalidStatus_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("/api/bills?status=invalid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("Bill status is invalid. Use pending, paid or overdue.", error.Message);
    }

    [Fact]
    public async Task GetBills_MonthWithoutYear_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("/api/bills?month=3");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("Month and year must be provided together.", error.Message);
    }

    [Fact]
    public async Task GetBills_YearWithoutMonth_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("/api/bills?year=2026");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("Month and year must be provided together.", error.Message);
    }

    [Fact]
    public async Task GetBills_InvalidMonthOrYear_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("/api/bills?month=13&year=2026");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBills_ShouldFilterByMonthAndYear()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        await SeedBillAsync("maria@email.com", "Internet", 99.90m, new DateOnly(2026, 3, 25));
        await SeedBillAsync("maria@email.com", "Streaming", 39.90m, new DateOnly(2026, 4, 5));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var bills = await client.GetFromJsonAsync<List<BillResponse>>("/api/bills?month=3&year=2026");

        Assert.NotNull(bills);
        Assert.Single(bills);
        Assert.Equal("Internet", bills[0].Description);
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

    private sealed class ErrorResponse
    {
        public string? Message { get; init; }
    }
}
