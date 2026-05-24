using System.Text.RegularExpressions;

namespace Farol.Domain.Budgets;

internal static class SensitiveBudgetTextValidator
{
    private static readonly Regex EmailPattern = new(
        @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CpfPattern = new(
        @"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b",
        RegexOptions.Compiled);

    private static readonly Regex CnpjPattern = new(
        @"\b\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}\b",
        RegexOptions.Compiled);

    private static readonly Regex LongNumberPattern = new(
        @"\b\d{8,}\b",
        RegexOptions.Compiled);

    private static readonly string[] SensitiveTerms =
    [
        "senha",
        "password",
        "token",
        "cpf",
        "cnpj",
        "cartao",
        "cartão"
    ];

    public static void EnsureSafe(string value, string parameterName)
    {
        if (EmailPattern.IsMatch(value) ||
            CpfPattern.IsMatch(value) ||
            CnpjPattern.IsMatch(value) ||
            LongNumberPattern.IsMatch(value) ||
            SensitiveTerms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Community budget fields cannot contain sensitive personal or credential data.", parameterName);
        }
    }
}
