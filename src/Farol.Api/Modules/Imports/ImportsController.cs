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

        if (request.File is null)
        {
            return BadRequest(new ErrorResponse("Selecione um arquivo CSV para importar."));
        }

        if (request.File.Length == 0)
        {
            return BadRequest(new ErrorResponse("O arquivo CSV esta vazio."));
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

        var lines = new List<string>();

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lines.Add(line);
        }

        if (lines.Count == 0)
        {
            return BadRequest(new ErrorResponse("O arquivo CSV esta vazio."));
        }

        TransactionCsvLayout layout = null!;
        var headerLineNumber = 0;
        var headerFound = false;

        for (var index = 0; index < lines.Count; index++)
        {
            var candidate = lines[index];

            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (!TransactionCsvParser.TryParseHeader(candidate, out layout, out _))
            {
                continue;
            }

            headerLineNumber = index + 1;
            headerFound = true;
            break;
        }

        if (!headerFound)
        {
            return BadRequest(new ErrorResponse(TransactionCsvParser.InvalidHeaderMessage));
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
        for (var index = headerLineNumber; index < lines.Count; index++)
        {
            var rowNumber = index + 1;
            var line = lines[index];

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            totalRows++;

            if (!TransactionCsvParser.TryParseRow(line, layout, rowNumber, out var parsedRow, out var error))
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
                        Message = "A categoria informada nao foi encontrada."
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
                    Message = "A categoria informada nao combina com o tipo da transacao."
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
            return Ok(new ImportTransactionsCsvResponse
            {
                TotalRows = 0,
                ImportedRows = 0,
                SkippedRows = 0,
                Errors = Array.Empty<ImportTransactionsCsvErrorResponse>()
            });
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
