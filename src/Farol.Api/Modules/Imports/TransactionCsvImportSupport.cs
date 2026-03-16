using System.Globalization;
using System.Text;
using Farol.Domain.Categories;
using Farol.Domain.Ledger;

namespace Farol.Api.Modules.Imports;

internal sealed record ParsedTransactionCsvRow(
    DateOnly OccurredOn,
    string Description,
    decimal Amount,
    TransactionType Type,
    string CategoryName);

internal sealed class TransactionImportCategoryResolver
{
    private static readonly (TransactionType Type, string Keyword, string CategoryName)[] Rules =
    [
        (TransactionType.Expense, "uber", "Transporte"),
        (TransactionType.Expense, "99", "Transporte"),
        (TransactionType.Expense, "combustivel", "Transporte"),
        (TransactionType.Expense, "mercado", "Alimentacao"),
        (TransactionType.Expense, "ifood", "Alimentacao"),
        (TransactionType.Expense, "restaurante", "Alimentacao"),
        (TransactionType.Expense, "netflix", "Assinaturas"),
        (TransactionType.Expense, "spotify", "Assinaturas"),
        (TransactionType.Expense, "farmacia", "Saude"),
        (TransactionType.Income, "salario", "Salario"),
        (TransactionType.Income, "pagamento", "Salario")
    ];

    private readonly IReadOnlyDictionary<string, List<Category>> _categoriesByName;

    public TransactionImportCategoryResolver(IEnumerable<Category> categories)
    {
        _categoriesByName = categories
            .GroupBy(category => NormalizeText(category.Name))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(category => category.IsSystem)
                    .ThenBy(category => category.Name)
                    .ToList());
    }

    public bool TryResolveExplicit(string categoryName, out Category? category)
    {
        category = null;

        var normalizedName = NormalizeText(categoryName);

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return false;
        }

        if (!_categoriesByName.TryGetValue(normalizedName, out var matches))
        {
            return false;
        }

        category = matches[0];
        return true;
    }

    public Category? ResolveByRule(string description, TransactionType type)
    {
        var normalizedDescription = NormalizeText(description);

        foreach (var (ruleType, keyword, categoryName) in Rules)
        {
            if (ruleType != type)
            {
                continue;
            }

            if (!normalizedDescription.Contains(NormalizeText(keyword), StringComparison.Ordinal))
            {
                continue;
            }

            if (TryResolveExplicit(categoryName, out var category))
            {
                return category;
            }
        }

        return null;
    }

    public static string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();

        normalized = normalized
            .Replace("\u00c3\u00a1", "a", StringComparison.Ordinal)
            .Replace("\u00c3\u00a0", "a", StringComparison.Ordinal)
            .Replace("\u00c3\u00a2", "a", StringComparison.Ordinal)
            .Replace("\u00c3\u00a3", "a", StringComparison.Ordinal)
            .Replace("\u00c3\u00a9", "e", StringComparison.Ordinal)
            .Replace("\u00c3\u00aa", "e", StringComparison.Ordinal)
            .Replace("\u00c3\u00ad", "i", StringComparison.Ordinal)
            .Replace("\u00c3\u00b3", "o", StringComparison.Ordinal)
            .Replace("\u00c3\u00b4", "o", StringComparison.Ordinal)
            .Replace("\u00c3\u00b5", "o", StringComparison.Ordinal)
            .Replace("\u00c3\u00ba", "u", StringComparison.Ordinal)
            .Replace("\u00c3\u00a7", "c", StringComparison.Ordinal);

        var builder = new StringBuilder();

        foreach (var character in normalized.Normalize(NormalizationForm.FormD))
        {
            if (char.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}

internal static class TransactionCsvParser
{
    private static readonly string[] ExpectedHeader =
    [
        "occurredOn",
        "description",
        "amount",
        "type",
        "categoryName"
    ];

    public static bool IsExpectedHeader(string headerLine)
    {
        var headerColumns = ParseColumns(headerLine, out _);

        if (headerColumns is null || headerColumns.Count != ExpectedHeader.Length)
        {
            return false;
        }

        for (var index = 0; index < ExpectedHeader.Length; index++)
        {
            if (!string.Equals(headerColumns[index].Trim(), ExpectedHeader[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryParseRow(
        string line,
        int rowNumber,
        out ParsedTransactionCsvRow row,
        out string error)
    {
        row = null!;
        error = string.Empty;

        var columns = ParseColumns(line, out error);

        if (columns is null)
        {
            return false;
        }

        if (columns.Count != ExpectedHeader.Length)
        {
            error = $"Row {rowNumber} must contain exactly {ExpectedHeader.Length} columns.";
            return false;
        }

        if (!DateOnly.TryParseExact(
                columns[0].Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var occurredOn))
        {
            error = $"Row {rowNumber} has an invalid occurredOn value.";
            return false;
        }

        var description = columns[1].Trim();

        if (string.IsNullOrWhiteSpace(description))
        {
            error = $"Row {rowNumber} must provide a description.";
            return false;
        }

        if (!decimal.TryParse(
                columns[2].Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            error = $"Row {rowNumber} has an invalid amount value.";
            return false;
        }

        if (!TryParseTransactionType(columns[3].Trim(), out var type))
        {
            error = $"Row {rowNumber} has an invalid transaction type.";
            return false;
        }

        row = new ParsedTransactionCsvRow(
            occurredOn,
            description,
            amount,
            type,
            columns[4].Trim());

        return true;
    }

    private static List<string>? ParseColumns(string line, out string error)
    {
        error = string.Empty;
        var columns = new List<string>();
        var current = new StringBuilder();
        var isInsideQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (character == '"')
            {
                if (isInsideQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                isInsideQuotes = !isInsideQuotes;
                continue;
            }

            if (character == ',' && !isInsideQuotes)
            {
                columns.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        if (isInsideQuotes)
        {
            error = "CSV contains an unterminated quoted value.";
            return null;
        }

        columns.Add(current.ToString());
        return columns;
    }

    private static bool TryParseTransactionType(string value, out TransactionType type)
    {
        if (string.Equals(value, "Income", StringComparison.OrdinalIgnoreCase))
        {
            type = TransactionType.Income;
            return true;
        }

        if (string.Equals(value, "Expense", StringComparison.OrdinalIgnoreCase))
        {
            type = TransactionType.Expense;
            return true;
        }

        type = default;
        return false;
    }
}
