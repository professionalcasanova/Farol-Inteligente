using Farol.Api.Common;
using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Transactions;

[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed class TransactionsController(FarolDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransactionResponse>>> List(CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.UserId == userId)
            .OrderByDescending(transaction => transaction.OccurredOn)
            .ThenByDescending(transaction => transaction.CreatedAtUtc)
            .Select(transaction => ToResponse(transaction))
            .ToListAsync(cancellationToken);

        return Ok(transactions);
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create(
        CreateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        var account = await dbContext.FinancialAccounts
            .SingleOrDefaultAsync(candidate => candidate.Id == request.FinancialAccountId && candidate.UserId == userId, cancellationToken);

        if (account is null)
        {
            return NotFound(new ErrorResponse("Financial account was not found."));
        }

        var category = await FindVisibleCategoryAsync(userId, request.CategoryId, cancellationToken);

        if (request.CategoryId.HasValue && category is null)
        {
            return NotFound(new ErrorResponse("Category was not found."));
        }

        Transaction transaction;

        try
        {
            transaction = new Transaction(
                account,
                request.Type,
                request.Amount,
                request.Description,
                request.OccurredOn,
                category);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            ArgumentOutOfRangeException or
            InvalidOperationException)
        {
            return BadRequest(new ErrorResponse(exception.Message));
        }

        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(transaction));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TransactionResponse>> Update(
        Guid id,
        UpdateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        var transaction = await dbContext.Transactions
            .SingleOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId, cancellationToken);

        if (transaction is null)
        {
            return NotFound(new ErrorResponse("Transaction was not found."));
        }

        var account = await dbContext.FinancialAccounts
            .SingleOrDefaultAsync(candidate => candidate.Id == request.FinancialAccountId && candidate.UserId == userId, cancellationToken);

        if (account is null)
        {
            return NotFound(new ErrorResponse("Financial account was not found."));
        }

        var category = await FindVisibleCategoryAsync(userId, request.CategoryId, cancellationToken);

        if (request.CategoryId.HasValue && category is null)
        {
            return NotFound(new ErrorResponse("Category was not found."));
        }

        try
        {
            if (!request.CategoryId.HasValue || request.Type != transaction.Type)
            {
                transaction.RemoveCategory();
            }

            transaction.Update(
                account,
                request.Type,
                request.Amount,
                request.Description,
                request.OccurredOn);

            if (category is not null)
            {
                transaction.AssignCategory(category);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            ArgumentOutOfRangeException or
            InvalidOperationException)
        {
            return BadRequest(new ErrorResponse(exception.Message));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(transaction));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        var transaction = await dbContext.Transactions
            .SingleOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId, cancellationToken);

        if (transaction is null)
        {
            return NotFound(new ErrorResponse("Transaction was not found."));
        }

        dbContext.Transactions.Remove(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<Category?> FindVisibleCategoryAsync(
        Guid userId,
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return null;
        }

        return await dbContext.Categories
            .SingleOrDefaultAsync(
                category => category.Id == categoryId.Value && (category.IsSystem || category.UserId == userId),
                cancellationToken);
    }

    private static TransactionResponse ToResponse(Transaction transaction)
    {
        return new TransactionResponse
        {
            Id = transaction.Id,
            FinancialAccountId = transaction.FinancialAccountId,
            CategoryId = transaction.CategoryId,
            Type = transaction.Type,
            Amount = transaction.Amount,
            Description = transaction.Description,
            OccurredOn = transaction.OccurredOn,
            CreatedAtUtc = transaction.CreatedAtUtc
        };
    }
}
