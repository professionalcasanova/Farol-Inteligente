using Farol.Domain.Categories;
using System.Globalization;
using System.Text;

namespace Farol.Infrastructure.Seeding;

public static class CategorySeed
{
    public static IReadOnlyList<(CategoryType Type, string Name)> SystemCategories { get; } =
    [
        (CategoryType.Expense, "Moradia"),
        (CategoryType.Expense, "Alimentação"),
        (CategoryType.Expense, "Contas e serviços"),
        (CategoryType.Expense, "Mercado"),
        (CategoryType.Expense, "Restaurantes"),
        (CategoryType.Expense, "Transporte"),
        (CategoryType.Expense, "Mobilidade"),
        (CategoryType.Expense, "Saúde"),
        (CategoryType.Expense, "Educação"),
        (CategoryType.Expense, "Lazer"),
        (CategoryType.Expense, "Compras"),
        (CategoryType.Expense, "Cuidados pessoais"),
        (CategoryType.Expense, "Família e filhos"),
        (CategoryType.Expense, "Pets"),
        (CategoryType.Expense, "Impostos e taxas"),
        (CategoryType.Expense, "Viagem"),
        (CategoryType.Expense, "Presentes e doações"),
        (CategoryType.Expense, "Assinaturas"),
        (CategoryType.Expense, "Outros"),
        (CategoryType.Expense, "Sem categoria"),
        (CategoryType.Income, "Salário"),
        (CategoryType.Income, "Adiantamento"),
        (CategoryType.Income, "Benefícios"),
        (CategoryType.Income, "Freelance"),
        (CategoryType.Income, "Comissão"),
        (CategoryType.Income, "Reembolso"),
        (CategoryType.Income, "Venda"),
        (CategoryType.Income, "Aluguel recebido"),
        (CategoryType.Income, "Restituição"),
        (CategoryType.Income, "Rendimento"),
        (CategoryType.Income, "Transferência recebida"),
        (CategoryType.Income, "Presente recebido"),
        (CategoryType.Income, "Outros"),
        (CategoryType.Income, "Sem categoria")
    ];

    public static string? ResolveCanonicalName(CategoryType type, string name)
    {
        var normalizedCandidates = BuildNormalizedCandidates(name);

        foreach (var (candidateType, candidateName) in SystemCategories)
        {
            if (candidateType != type)
            {
                continue;
            }

            if (normalizedCandidates.Contains(NormalizeLookup(candidateName)))
            {
                return candidateName;
            }
        }

        return null;
    }

    private static string NormalizeLookup(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        foreach (var character in value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
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

    private static HashSet<string> BuildNormalizedCandidates(string value)
    {
        var candidates = new HashSet<string>(StringComparer.Ordinal)
        {
            NormalizeLookup(value)
        };

        var repairedValue = value;

        for (var attempt = 0; attempt < 4; attempt += 1)
        {
            repairedValue = TryRepairLegacyUtf8(repairedValue);
            candidates.Add(NormalizeLookup(repairedValue));
        }

        return candidates;
    }

    private static string TryRepairLegacyUtf8(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var bytes = new byte[value.Length];

        for (var index = 0; index < value.Length; index += 1)
        {
            if (!TryGetWindows1252Byte(value[index], out bytes[index]))
            {
                return value;
            }
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static bool TryGetWindows1252Byte(char value, out byte result)
    {
        if (value <= byte.MaxValue)
        {
            result = (byte)value;
            return true;
        }

        result = value switch
        {
            '\u20AC' => 0x80,
            '\u201A' => 0x82,
            '\u0192' => 0x83,
            '\u201E' => 0x84,
            '\u2026' => 0x85,
            '\u2020' => 0x86,
            '\u2021' => 0x87,
            '\u02C6' => 0x88,
            '\u2030' => 0x89,
            '\u0160' => 0x8A,
            '\u2039' => 0x8B,
            '\u0152' => 0x8C,
            '\u017D' => 0x8E,
            '\u2018' => 0x91,
            '\u2019' => 0x92,
            '\u201C' => 0x93,
            '\u201D' => 0x94,
            '\u2022' => 0x95,
            '\u2013' => 0x96,
            '\u2014' => 0x97,
            '\u02DC' => 0x98,
            '\u2122' => 0x99,
            '\u0161' => 0x9A,
            '\u203A' => 0x9B,
            '\u0153' => 0x9C,
            '\u017E' => 0x9E,
            '\u0178' => 0x9F,
            _ => 0
        };

        return result != 0;
    }
}
