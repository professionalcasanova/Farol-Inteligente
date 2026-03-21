using Farol.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Farol.Api.Modules.Insights;

[ApiController]
[Authorize]
[Route("api/insights")]
public sealed class InsightsController(MonthlyInsightsService monthlyInsightsService) : ControllerBase
{
    [HttpGet("free-money")]
    public async Task<ActionResult<FreeMoneyResponse>> GetFreeMoney(
        [FromQuery] FreeMoneyRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = TryGetUserIdAndPeriodStart(request, out var userId, out var periodStart);

        if (validationResult is not null)
        {
            return validationResult;
        }

        var snapshot = await monthlyInsightsService.GetMonthlySnapshotAsync(
            userId,
            periodStart,
            cancellationToken);

        return Ok(monthlyInsightsService.BuildFreeMoneyResponse(
            snapshot,
            request.Month,
            request.Year));
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<AlertsResponse>> GetAlerts(
        [FromQuery] FreeMoneyRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = TryGetUserIdAndPeriodStart(request, out var userId, out var periodStart);

        if (validationResult is not null)
        {
            return validationResult;
        }

        var snapshot = await monthlyInsightsService.GetMonthlySnapshotAsync(
            userId,
            periodStart,
            cancellationToken);

        return Ok(monthlyInsightsService.BuildAlertsResponse(snapshot));
    }

    [HttpGet("month-health")]
    public async Task<ActionResult<MonthHealthResponse>> GetMonthHealth(
        [FromQuery] FreeMoneyRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = TryGetUserIdAndPeriodStart(request, out var userId, out var periodStart);

        if (validationResult is not null)
        {
            return validationResult;
        }

        var snapshot = await monthlyInsightsService.GetMonthlySnapshotAsync(
            userId,
            periodStart,
            cancellationToken);

        return Ok(monthlyInsightsService.BuildMonthHealthResponse(snapshot));
    }

    private ActionResult? TryGetUserIdAndPeriodStart(
        FreeMoneyRequest request,
        out Guid userId,
        out DateOnly periodStart)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out userId))
        {
            periodStart = default;
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        try
        {
            periodStart = new DateOnly(request.Year, request.Month, 1);
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            periodStart = default;
            return BadRequest(new ErrorResponse("Month and year are invalid."));
        }
    }
}
