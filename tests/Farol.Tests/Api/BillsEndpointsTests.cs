using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Farol.Api.Modules.Auth;
using Farol.Api.Modules.Bills;
using Farol.Domain.Bills;
using Farol.Domain.Ledger;
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
        var dueOn = _factory.Today.AddDays(7);

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
    public async Task PostBills_ShouldCreateRecurringBillSeriesAndFirstOccurrence()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var dueOn = new DateOnly(2026, 3, 10);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/bills", new CreateBillRequest
        {
            Description = "Internet",
            Amount = 99.90m,
            DueOn = dueOn,
            Recurrence = new CreateRecurringBillRequest
            {
                Kind = BillSeries.RecurringKind,
                Frequency = BillSeries.MonthlyFrequency,
                EndMode = BillSeries.OpenEndedEndMode
            }
        });

        response.EnsureSuccessStatusCode();

        var bill = await response.Content.ReadFromJsonAsync<BillResponse>();

        Assert.NotNull(bill);
        Assert.Equal(dueOn, bill.DueOn);
        Assert.NotNull(bill.BillSeriesId);
        Assert.Equal(BillSeries.RecurringKind, bill.SeriesKind);
        Assert.Equal(1, bill.OccurrenceNumber);
        Assert.Null(bill.TotalOccurrences);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        Assert.Single(dbContext.BillSeries);
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
        var accountId = await SeedAccountAsync("maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/bills/{billId}/pay")
        {
            Content = JsonContent.Create(new PayBillRequest
            {
                FinancialAccountId = accountId
            })
        };
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var bill = await response.Content.ReadFromJsonAsync<BillResponse>();

        Assert.NotNull(bill);
        Assert.True(bill.IsPaid);
        Assert.NotNull(bill.PaidAtUtc);
        Assert.Equal("paid", bill.Status);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var paymentTransaction = Assert.Single(dbContext.Transactions);

        Assert.Equal(accountId, paymentTransaction.FinancialAccountId);
        Assert.Equal(TransactionType.Expense, paymentTransaction.Type);
        Assert.Equal(99.90m, paymentTransaction.Amount);
        Assert.Equal("Pagamento da conta: Internet", paymentTransaction.Description);
        Assert.Equal(paymentTransaction.Id, dbContext.Bills.Single(item => item.Id == billId).PaidTransactionId);
    }

    [Fact]
    public async Task PatchUnpay_ShouldClearPaidAtUtc()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var dueOn = _factory.Today.AddDays(2);
        var billId = await SeedBillAsync(
            "maria@email.com",
            "Internet",
            99.90m,
            dueOn,
            isPaid: true,
            withPaymentTransaction: true);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/bills/{billId}/unpay");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var bill = await response.Content.ReadFromJsonAsync<BillResponse>();

        Assert.NotNull(bill);
        Assert.False(bill.IsPaid);
        Assert.Null(bill.PaidAtUtc);
        Assert.Equal("pending", bill.Status);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        Assert.Empty(dbContext.Transactions);
        Assert.Null(dbContext.Bills.Single(item => item.Id == billId).PaidTransactionId);
    }

    [Fact]
    public async Task PatchPay_ShouldNotAllowAnotherUsersBill()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        await RegisterAndGetTokenAsync(client, "joao@email.com");
        var joaoBillId = await SeedBillAsync("joao@email.com", "Aluguel", 1200m, new DateOnly(2026, 3, 10));
        var mariaAccountId = await SeedAccountAsync("maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/bills/{joaoBillId}/pay")
        {
            Content = JsonContent.Create(new PayBillRequest
            {
                FinancialAccountId = mariaAccountId
            })
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("Bill was not found.", error.Message);
    }

    [Fact]
    public async Task PatchPay_NonexistentBill_ReturnsNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var accountId = await SeedAccountAsync("maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/bills/{Guid.NewGuid()}/pay")
        {
            Content = JsonContent.Create(new PayBillRequest
            {
                FinancialAccountId = accountId
            })
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("Bill was not found.", error.Message);
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

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("Bill was not found.", error.Message);
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

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("Bill was not found.", error.Message);
    }

    [Fact]
    public async Task GetBills_ShouldFilterByStatus()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = _factory.Today;

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

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Contains("Month", error.Message);
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

    [Fact]
    public async Task GetBills_ShouldExpandRecurringSeriesForRequestedMonth()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.PostAsJsonAsync("/api/bills", new CreateBillRequest
        {
            Description = "Academia",
            Amount = 120m,
            DueOn = new DateOnly(2026, 3, 10),
            Recurrence = new CreateRecurringBillRequest
            {
                Kind = BillSeries.RecurringKind,
                Frequency = BillSeries.MonthlyFrequency,
                EndMode = BillSeries.OccurrenceCountEndMode,
                OccurrenceCount = 3
            }
        });

        createResponse.EnsureSuccessStatusCode();
        var firstBill = await createResponse.Content.ReadFromJsonAsync<BillResponse>();

        var aprilBills = await client.GetFromJsonAsync<List<BillResponse>>("/api/bills?month=4&year=2026");
        var mayBills = await client.GetFromJsonAsync<List<BillResponse>>("/api/bills?month=5&year=2026");
        var juneBills = await client.GetFromJsonAsync<List<BillResponse>>("/api/bills?month=6&year=2026");

        Assert.NotNull(firstBill);
        Assert.NotNull(aprilBills);
        Assert.NotNull(mayBills);
        Assert.NotNull(juneBills);
        Assert.Single(aprilBills);
        Assert.Single(mayBills);
        Assert.Empty(juneBills);
        Assert.Equal(firstBill.BillSeriesId, aprilBills[0].BillSeriesId);
        Assert.Equal(2, aprilBills[0].OccurrenceNumber);
        Assert.Equal(3, aprilBills[0].TotalOccurrences);
        Assert.Equal(new DateOnly(2026, 4, 10), aprilBills[0].DueOn);
        Assert.Equal(3, mayBills[0].OccurrenceNumber);
        Assert.Equal(3, mayBills[0].TotalOccurrences);
    }

    [Fact]
    public async Task GetBills_RepeatingTheSameMonthLoad_ShouldNotDuplicateRecurringOccurrence()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.PostAsJsonAsync("/api/bills", new CreateBillRequest
        {
            Description = "Internet",
            Amount = 140m,
            DueOn = new DateOnly(2026, 3, 15),
            Recurrence = new CreateRecurringBillRequest
            {
                Kind = BillSeries.RecurringKind,
                Frequency = BillSeries.MonthlyFrequency,
                EndMode = BillSeries.OpenEndedEndMode
            }
        });

        createResponse.EnsureSuccessStatusCode();

        var firstAprilLoad = await client.GetFromJsonAsync<List<BillResponse>>("/api/bills?month=4&year=2026");
        var secondAprilLoad = await client.GetFromJsonAsync<List<BillResponse>>("/api/bills?month=4&year=2026");

        Assert.NotNull(firstAprilLoad);
        Assert.NotNull(secondAprilLoad);
        Assert.Single(firstAprilLoad);
        Assert.Single(secondAprilLoad);
        Assert.Equal(firstAprilLoad[0].Id, secondAprilLoad[0].Id);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        Assert.Single(dbContext.Bills.Where(item => item.BillSeriesId.HasValue && item.DueOn == new DateOnly(2026, 4, 15)));
    }

    [Fact]
    public async Task PatchPay_ShouldMarkOnlyOneRecurringOccurrenceAsPaid()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var accountId = await SeedAccountAsync("maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.PostAsJsonAsync("/api/bills", new CreateBillRequest
        {
            Description = "Curso",
            Amount = 180m,
            DueOn = new DateOnly(2026, 3, 5),
            Recurrence = new CreateRecurringBillRequest
            {
                Kind = BillSeries.RecurringKind,
                Frequency = BillSeries.MonthlyFrequency,
                EndMode = BillSeries.UntilDateEndMode,
                UntilDate = new DateOnly(2026, 5, 5)
            }
        });

        createResponse.EnsureSuccessStatusCode();
        var firstBill = await createResponse.Content.ReadFromJsonAsync<BillResponse>();

        using (var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/bills/{firstBill!.Id}/pay")
        {
            Content = JsonContent.Create(new PayBillRequest
            {
                FinancialAccountId = accountId
            })
        })
        {
            var payResponse = await client.SendAsync(request);
            payResponse.EnsureSuccessStatusCode();
        }

        var aprilBills = await client.GetFromJsonAsync<List<BillResponse>>("/api/bills?month=4&year=2026");

        Assert.NotNull(aprilBills);
        Assert.Single(aprilBills);
        Assert.False(aprilBills[0].IsPaid);
        Assert.Equal("pending", aprilBills[0].Status);
        Assert.Equal(2, aprilBills[0].OccurrenceNumber);
    }

    [Fact]
    public async Task PostBills_ShouldCreateInstallmentSeriesWithProgress()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/bills", new CreateBillRequest
        {
            Description = "Notebook",
            Amount = 320m,
            DueOn = new DateOnly(2026, 3, 8),
            Recurrence = new CreateRecurringBillRequest
            {
                Kind = BillSeries.InstallmentKind,
                Frequency = BillSeries.MonthlyFrequency,
                EndMode = BillSeries.OccurrenceCountEndMode,
                OccurrenceCount = 12
            }
        });

        response.EnsureSuccessStatusCode();

        var bill = await response.Content.ReadFromJsonAsync<BillResponse>();

        Assert.NotNull(bill);
        Assert.Equal(BillSeries.InstallmentKind, bill.SeriesKind);
        Assert.Equal(1, bill.OccurrenceNumber);
        Assert.Equal(12, bill.TotalOccurrences);
    }

    [Fact]
    public async Task PostBills_ShouldRejectInstallmentWithSingleOccurrence()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/bills", new CreateBillRequest
        {
            Description = "Curso",
            Amount = 180m,
            DueOn = new DateOnly(2026, 3, 5),
            Recurrence = new CreateRecurringBillRequest
            {
                Kind = BillSeries.InstallmentKind,
                Frequency = BillSeries.MonthlyFrequency,
                EndMode = BillSeries.OccurrenceCountEndMode,
                OccurrenceCount = 1
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("Installment count must be at least 2.", error.Message);
    }

    [Fact]
    public async Task PutBills_ShouldUpdateOwnedSingleBill()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var billId = await SeedBillAsync("maria@email.com", "Internet", 99.90m, new DateOnly(2026, 3, 25));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PutAsJsonAsync($"/api/bills/{billId}", new UpdateBillRequest
        {
            Description = "Internet fibra",
            Amount = 119.90m,
            DueOn = new DateOnly(2026, 3, 28)
        });

        response.EnsureSuccessStatusCode();

        var bill = await response.Content.ReadFromJsonAsync<BillResponse>();

        Assert.NotNull(bill);
        Assert.Equal("Internet fibra", bill.Description);
        Assert.Equal(119.90m, bill.Amount);
        Assert.Equal(new DateOnly(2026, 3, 28), bill.DueOn);
    }

    [Fact]
    public async Task PutBills_ShouldUpdateOnlySelectedRecurringOccurrence()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.PostAsJsonAsync("/api/bills", new CreateBillRequest
        {
            Description = "Academia",
            Amount = 120m,
            DueOn = new DateOnly(2026, 3, 10),
            Recurrence = new CreateRecurringBillRequest
            {
                Kind = BillSeries.RecurringKind,
                Frequency = BillSeries.MonthlyFrequency,
                EndMode = BillSeries.OccurrenceCountEndMode,
                OccurrenceCount = 3
            }
        });

        createResponse.EnsureSuccessStatusCode();
        var firstBill = await createResponse.Content.ReadFromJsonAsync<BillResponse>();

        var updateResponse = await client.PutAsJsonAsync($"/api/bills/{firstBill!.Id}", new UpdateBillRequest
        {
            Description = "Academia premium",
            Amount = 150m,
            DueOn = new DateOnly(2026, 3, 12)
        });

        updateResponse.EnsureSuccessStatusCode();

        var updatedBill = await updateResponse.Content.ReadFromJsonAsync<BillResponse>();
        var aprilBills = await client.GetFromJsonAsync<List<BillResponse>>("/api/bills?month=4&year=2026");

        Assert.NotNull(updatedBill);
        Assert.Equal("Academia premium", updatedBill.Description);
        Assert.Equal(150m, updatedBill.Amount);
        Assert.Equal(new DateOnly(2026, 3, 12), updatedBill.DueOn);

        Assert.NotNull(aprilBills);
        Assert.Single(aprilBills);
        Assert.Equal("Academia", aprilBills[0].Description);
        Assert.Equal(120m, aprilBills[0].Amount);
        Assert.Equal(new DateOnly(2026, 4, 10), aprilBills[0].DueOn);
    }

    [Fact]
    public async Task DeleteBills_ShouldRemoveOwnedSingleBill()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var billId = await SeedBillAsync("maria@email.com", "Internet", 99.90m, new DateOnly(2026, 3, 25));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.DeleteAsync($"/api/bills/{billId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        Assert.DoesNotContain(dbContext.Bills, bill => bill.Id == billId);
    }

    [Fact]
    public async Task DeleteBills_WithSeriesScope_ShouldEndRemainingRecurringSeries()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.PostAsJsonAsync("/api/bills", new CreateBillRequest
        {
            Description = "Curso",
            Amount = 180m,
            DueOn = new DateOnly(2026, 3, 5),
            Recurrence = new CreateRecurringBillRequest
            {
                Kind = BillSeries.RecurringKind,
                Frequency = BillSeries.MonthlyFrequency,
                EndMode = BillSeries.UntilDateEndMode,
                UntilDate = new DateOnly(2026, 5, 5)
            }
        });

        createResponse.EnsureSuccessStatusCode();
        var firstBill = await createResponse.Content.ReadFromJsonAsync<BillResponse>();

        var deleteResponse = await client.DeleteAsync($"/api/bills/{firstBill!.Id}?scope=series");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var aprilBills = await client.GetFromJsonAsync<List<BillResponse>>("/api/bills?month=4&year=2026");
        var mayBills = await client.GetFromJsonAsync<List<BillResponse>>("/api/bills?month=5&year=2026");

        Assert.NotNull(aprilBills);
        Assert.NotNull(mayBills);
        Assert.Empty(aprilBills);
        Assert.Empty(mayBills);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var series = dbContext.BillSeries.Single();
        Assert.False(series.IsActive);
    }

    private async Task<Guid> SeedBillAsync(
        string email,
        string description,
        decimal amount,
        DateOnly dueOn,
        bool isPaid = false,
        bool withPaymentTransaction = false)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var userId = dbContext.Users.Single(user => user.Email == email).Id;
        var bill = new Bill(userId, description, amount, dueOn);

        if (isPaid)
        {
            if (withPaymentTransaction)
            {
                var account = new FinancialAccount(userId, $"Conta {email}", FinancialAccountType.BankAccount);
                var paymentTransaction = new Transaction(
                    account,
                    TransactionType.Expense,
                    amount,
                    $"Pagamento da conta: {description}",
                    dueOn);

                dbContext.FinancialAccounts.Add(account);
                dbContext.Transactions.Add(paymentTransaction);
                bill.MarkAsPaid(new DateTimeOffset(2026, 3, 17, 12, 0, 0, TimeSpan.Zero), paymentTransaction.Id);
            }
            else
            {
                bill.MarkAsPaid(new DateTimeOffset(2026, 3, 17, 12, 0, 0, TimeSpan.Zero));
            }
        }

        dbContext.Bills.Add(bill);
        await dbContext.SaveChangesAsync();

        return bill.Id;
    }

    private async Task<Guid> SeedAccountAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var userId = dbContext.Users.Single(user => user.Email == email).Id;
        var account = new FinancialAccount(userId, $"Conta {email}", FinancialAccountType.BankAccount);

        dbContext.FinancialAccounts.Add(account);
        await dbContext.SaveChangesAsync();

        return account.Id;
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
