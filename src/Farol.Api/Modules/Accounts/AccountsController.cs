using Farol.Api.Common;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Accounts;

[ApiController]
[Authorize]
[Route("api/accounts")]
public sealed class AccountsController(FarolDbContext dbContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<AccountResponse>> Create(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ErrorResponse("Name is required."));
        }

        FinancialAccount account;

        try
        {
            account = new FinancialAccount(userId, request.Name, request.Type);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ErrorResponse(exception.Message));
        }

        dbContext.FinancialAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(account));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountResponse>>> List(CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        var accounts = await dbContext.FinancialAccounts
            .AsNoTracking()
            .Where(account => account.UserId == userId)
            .OrderBy(account => account.CreatedAtUtc)
            .Select(account => new AccountResponse
            {
                Id = account.Id,
                Name = account.Name,
                Type = account.Type,
                IsActive = account.IsActive,
                CreatedAtUtc = account.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(accounts);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AccountResponse>> Update(
        Guid id,
        UpdateAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ErrorResponse("Name is required."));
        }

        var account = await dbContext.FinancialAccounts
            .SingleOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId, cancellationToken);

        if (account is null)
        {
            return NotFound(new ErrorResponse("Financial account was not found."));
        }

        try
        {
            account.UpdateDetails(request.Name, request.Type);

            if (request.IsActive)
            {
                account.Activate();
            }
            else
            {
                account.Deactivate();
            }
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ErrorResponse(exception.Message));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(account));
    }
    private static AccountResponse ToResponse(FinancialAccount account)
    {
        return new AccountResponse
        {
            Id = account.Id,
            Name = account.Name,
            Type = account.Type,
            IsActive = account.IsActive,
            CreatedAtUtc = account.CreatedAtUtc
        };
    }
}
