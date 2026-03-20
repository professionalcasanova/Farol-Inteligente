using System.Text;
using Farol.Api.Common;
using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Imports;

[ApiController]
[Authorize]
[Route("api/imports/transactions")]
public sealed class ImportsController(FarolDbContext dbContext) : ControllerBase
{
    [HttpPost("csv")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImportTransactionsCsvResponse>> ImportCsv(
        [FromForm] ImportTransactionsCsvRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new ErrorResponse("CSV file is required."));
        }

        var financialAccount = await dbContext.FinancialAccounts
            .SingleOrDefaultAsync(
                account => account.Id == request.FinancialAccountId && account.UserId == userId,
                cancellationToken);

        if (financialAccount is null)
        {
            return NotFound(new ErrorResponse("Financial account was not found."));
        }

        using var reader = new StreamReader(
            request.File.OpenReadStream(),
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

        var headerLine = await reader.ReadLineAsync(cancellationToken);

        if (headerLine is null)
        {
            return BadRequest(new ErrorResponse("CSV file is empty."));
        }

        if (!TransactionCsvParser.IsExpectedHeader(headerLine))
        {
            return BadRequest(new ErrorResponse("CSV header is invalid. Expected: occurredOn,description,amount,type,categoryName."));
        }

        var visibleCategories = await dbContext.Categories
            .AsNoTracking()
            .Where(category => category.IsSystem || category.UserId == userId)
            .OrderByDescending(category => category.IsSystem)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);

        var categoryResolver = new TransactionImportCategoryResolver(visibleCategories);
        var errors = new List<ImportTransactionsCsvErrorResponse>();
        var importedRows = 0;
        var totalRows = 0;
        var rowNumber = 1;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            rowNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                totalRows++;
                errors.Add(new ImportTransactionsCsvErrorResponse
                {
                    RowNumber = rowNumber,
                    Message = $"Row {rowNumber} is empty."
                });
                continue;
            }

            totalRows++;

            if (!TransactionCsvParser.TryParseRow(line, rowNumber, out var parsedRow, out var error))
            {
                errors.Add(new ImportTransactionsCsvErrorResponse
                {
                    RowNumber = rowNumber,
                    Message = error
                });
                continue;
            }

            Category? category;

            if (!string.IsNullOrWhiteSpace(parsedRow.CategoryName))
            {
                if (!categoryResolver.TryResolveExplicit(parsedRow.CategoryName, out category))
                {
                    errors.Add(new ImportTransactionsCsvErrorResponse
                    {
                        RowNumber = rowNumber,
                        Message = $"Row {rowNumber} references an unknown category."
                    });
                    continue;
                }
            }
            else
            {
                category = categoryResolver.ResolveByRule(parsedRow.Description, parsedRow.Type);
            }

            if (category is not null && !category.CanBeAssignedTo(parsedRow.Type))
            {
                errors.Add(new ImportTransactionsCsvErrorResponse
                {
                    RowNumber = rowNumber,
                    Message = $"Row {rowNumber} contains a category incompatible with the transaction type."
                });
                continue;
            }

            try
            {
                dbContext.Transactions.Add(new Transaction(
                    financialAccount,
                    parsedRow.Type,
                    parsedRow.Amount,
                    parsedRow.Description,
                    parsedRow.OccurredOn,
                    category));

                importedRows++;
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                ArgumentOutOfRangeException or
                InvalidOperationException)
            {
                errors.Add(new ImportTransactionsCsvErrorResponse
                {
                    RowNumber = rowNumber,
                    Message = exception.Message
                });
            }
        }

        if (totalRows == 0)
        {
            return BadRequest(new ErrorResponse("CSV file must contain at least one data row."));
        }

        if (importedRows > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new ImportTransactionsCsvResponse
        {
            TotalRows = totalRows,
            ImportedRows = importedRows,
            SkippedRows = totalRows - importedRows,
            Errors = errors
        });
    }
}
