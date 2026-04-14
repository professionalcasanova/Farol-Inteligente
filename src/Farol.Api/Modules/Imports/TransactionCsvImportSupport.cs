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

internal sealed record TransactionCsvLayout(
    char Delimiter,
    int HeaderColumnCount,
    int OccurredOnIndex,
    int DescriptionIndex,
    int AmountIndex,
    int TypeIndex,
    int? CategoryNameIndex);

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
    private const string InvalidHeaderMessage =
        "O cabecalho do CSV e invalido. Informe colunas para data, descricao, valor e tipo. Categoria e opcional.";

    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd",
        "dd/MM/yyyy",
        "dd-MM-yyyy",
        "yyyy/MM/dd"
    ];

    private static readonly Dictionary<string, string> HeaderAliases = new(StringComparer.Ordinal)
    {
        ["occurredon"] = "occurredOn",
        ["date"] = "occurredOn",
        ["data"] = "occurredOn",
        ["transactiondate"] = "occurredOn",
        ["postedon"] = "occurredOn",
        ["dataocorrencia"] = "occurredOn",
        ["description"] = "description",
        ["descricao"] = "description",
        ["details"] = "description",
        ["detail"] = "description",
        ["detalhe"] = "description",
        ["historico"] = "description",
        ["memo"] = "description",
        ["amount"] = "amount",
        ["valor"] = "amount",
        ["value"] = "amount",
        ["total"] = "amount",
        ["type"] = "type",
        ["tipo"] = "type",
        ["transactiontype"] = "type",
        ["nature"] = "type",
        ["natureza"] = "type",
        ["categoryname"] = "categoryName",
        ["category"] = "categoryName",
        ["categoria"] = "categoryName",
        ["categorianome"] = "categoryName"
    };

    public static bool TryParseHeader(
        string headerLine,
        out TransactionCsvLayout layout,
        out string error)
    {
        layout = null!;
        error = InvalidHeaderMessage;

        var delimiter = DetectDelimiter(headerLine);
        var headerColumns = ParseColumns(headerLine, delimiter, out _);

        if (headerColumns is null || headerColumns.Count < 4)
        {
            return false;
        }

        var canonicalIndexes = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var index = 0; index < headerColumns.Count; index++)
        {
            var normalizedColumn = NormalizeToken(headerColumns[index]);

            if (string.IsNullOrWhiteSpace(normalizedColumn))
            {
                continue;
            }

            if (!HeaderAliases.TryGetValue(normalizedColumn, out var canonicalName))
            {
                continue;
            }

            if (!canonicalIndexes.TryAdd(canonicalName, index))
            {
                error = "O cabecalho do CSV contem colunas duplicadas para o mesmo campo.";
                return false;
            }
        }

        if (!canonicalIndexes.TryGetValue("occurredOn", out var occurredOnIndex) ||
            !canonicalIndexes.TryGetValue("description", out var descriptionIndex) ||
            !canonicalIndexes.TryGetValue("amount", out var amountIndex) ||
            !canonicalIndexes.TryGetValue("type", out var typeIndex))
        {
            return false;
        }

        canonicalIndexes.TryGetValue("categoryName", out var categoryNameIndex);

        layout = new TransactionCsvLayout(
            delimiter,
            headerColumns.Count,
            occurredOnIndex,
            descriptionIndex,
            amountIndex,
            typeIndex,
            canonicalIndexes.ContainsKey("categoryName") ? categoryNameIndex : null);

        error = string.Empty;
        return true;
    }

    public static bool TryParseRow(
        string line,
        TransactionCsvLayout layout,
        int rowNumber,
        out ParsedTransactionCsvRow row,
        out string error)
    {
        row = null!;
        error = string.Empty;

        var columns = ParseColumns(line, layout.Delimiter, out error);

        if (columns is null)
        {
            return false;
        }

        if (columns.Count > layout.HeaderColumnCount)
        {
            error = "A linha contem mais colunas do que o cabecalho informado.";
            return false;
        }

        var occurredOnValue = GetColumnValue(columns, layout.OccurredOnIndex);
        var descriptionValue = GetColumnValue(columns, layout.DescriptionIndex);
        var amountValue = GetColumnValue(columns, layout.AmountIndex);
        var typeValue = GetColumnValue(columns, layout.TypeIndex);
        var categoryNameValue = layout.CategoryNameIndex.HasValue
            ? GetColumnValue(columns, layout.CategoryNameIndex.Value)
            : string.Empty;

        if (!TryParseOccurredOn(occurredOnValue, out var occurredOn))
        {
            error = "A data da transacao esta invalida.";
            return false;
        }

        var description = descriptionValue.Trim();

        if (string.IsNullOrWhiteSpace(description))
        {
            error = "A descricao e obrigatoria.";
            return false;
        }

        if (!TryParseAmount(amountValue, out var amount))
        {
            error = "O valor em amount esta invalido.";
            return false;
        }

        if (!TryParseTransactionType(typeValue, out var type))
        {
            error = "O tipo de transacao esta invalido.";
            return false;
        }

        row = new ParsedTransactionCsvRow(
            occurredOn,
            description,
            amount,
            type,
            categoryNameValue.Trim());

        return true;
    }

    private static string GetColumnValue(IReadOnlyList<string> columns, int index)
    {
        return index >= 0 && index < columns.Count ? columns[index] : string.Empty;
    }

    private static bool TryParseOccurredOn(string value, out DateOnly occurredOn)
    {
        return DateOnly.TryParseExact(
                   value.Trim(),
                   DateFormats,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out occurredOn)
               || DateOnly.TryParseExact(
                   value.Trim(),
                   DateFormats,
                   new CultureInfo("pt-BR"),
                   DateTimeStyles.None,
                   out occurredOn);
    }

    private static bool TryParseAmount(string value, out decimal amount)
    {
        var trimmed = value.Trim();
        var ptBrCulture = new CultureInfo("pt-BR");
        var lastComma = trimmed.LastIndexOf(',');
        var lastDot = trimmed.LastIndexOf('.');

        if (lastComma >= 0 && (lastDot < 0 || lastComma > lastDot))
        {
            if (decimal.TryParse(
                    trimmed,
                    NumberStyles.Number,
                    ptBrCulture,
                    out amount))
            {
                return true;
            }
        }

        if (decimal.TryParse(
                trimmed,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out amount))
        {
            return true;
        }

        if (decimal.TryParse(
                trimmed,
                NumberStyles.Number,
                ptBrCulture,
                out amount))
        {
            return true;
        }

        var normalized = trimmed.Replace(" ", string.Empty, StringComparison.Ordinal);
        lastComma = normalized.LastIndexOf(',');
        lastDot = normalized.LastIndexOf('.');

        if (lastComma >= 0 && lastDot >= 0)
        {
            normalized = lastComma > lastDot
                ? normalized.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.')
                : normalized.Replace(",", string.Empty, StringComparison.Ordinal);
        }
        else if (lastComma >= 0)
        {
            normalized = normalized.Replace(',', '.');
        }

        return decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out amount);
    }

    private static List<string>? ParseColumns(string line, char delimiter, out string error)
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

            if (character == delimiter && !isInsideQuotes)
            {
                columns.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        if (isInsideQuotes)
        {
            error = "O CSV contem aspas abertas sem fechamento.";
            return null;
        }

        columns.Add(current.ToString());
        return columns;
    }

    private static char DetectDelimiter(string line)
    {
        var commas = CountDelimiterOccurrences(line, ',');
        var semicolons = CountDelimiterOccurrences(line, ';');

        return semicolons > commas ? ';' : ',';
    }

    private static int CountDelimiterOccurrences(string line, char delimiter)
    {
        var count = 0;
        var isInsideQuotes = false;

        foreach (var character in line)
        {
            if (character == '"')
            {
                isInsideQuotes = !isInsideQuotes;
                continue;
            }

            if (character == delimiter && !isInsideQuotes)
            {
                count++;
            }
        }

        return count;
    }

    private static string NormalizeToken(string value)
    {
        return TransactionImportCategoryResolver.NormalizeText(value);
    }

    private static bool TryParseTransactionType(string value, out TransactionType type)
    {
        switch (NormalizeToken(value))
        {
            case "income":
            case "receita":
            case "entrada":
            case "credit":
            case "credito":
                type = TransactionType.Income;
                return true;
            case "expense":
            case "despesa":
            case "saida":
            case "debit":
            case "debito":
                type = TransactionType.Expense;
                return true;
            default:
                type = default;
                return false;
        }
    }
}
