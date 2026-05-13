using Farol.Api.Common;
using Farol.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Imports;

[ApiController]
[Authorize]
[Route("api/imports/transactions")]
public sealed class ImportsController(
    FarolDbContext dbContext,
    TransactionCsvImportProcessor importProcessor) : ControllerBase
{
    private const long MaxCsvFileBytes = 2 * 1024 * 1024;
    private const long MaxCsvRequestBytes = MaxCsvFileBytes + (128 * 1024);

    private static readonly HashSet<string> AllowedCsvContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/csv",
        "application/csv",
        "application/vnd.ms-excel",
        "application/octet-stream"
    };

    [HttpPost("csv")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxCsvRequestBytes)]
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

        if (request.File.Length > MaxCsvFileBytes)
        {
            return BadRequest(new ErrorResponse("O arquivo CSV excede o tamanho maximo permitido de 2 MB."));
        }

        if (!IsCsvFile(request.File))
        {
            return BadRequest(new ErrorResponse("Envie um arquivo CSV valido."));
        }

        var financialAccount = await dbContext.FinancialAccounts
            .SingleOrDefaultAsync(
                account => account.Id == request.FinancialAccountId && account.UserId == userId,
                cancellationToken);

        if (financialAccount is null)
        {
            return NotFound(new ErrorResponse("Financial account was not found."));
        }

        try
        {
            return Ok(await importProcessor.ProcessAsync(
                userId,
                financialAccount,
                request.File,
                cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ErrorResponse(exception.Message));
        }
    }

    private static bool IsCsvFile(IFormFile file)
    {
        var hasCsvExtension = string.Equals(
            Path.GetExtension(file.FileName),
            ".csv",
            StringComparison.OrdinalIgnoreCase);

        if (!hasCsvExtension)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(file.ContentType) ||
               AllowedCsvContentTypes.Contains(file.ContentType);
    }
}
