using Farol.Domain.Categories;

namespace Farol.Infrastructure.Seeding;

public static class CategorySeed
{
    public static IReadOnlyList<(CategoryType Type, string Name)> SystemCategories { get; } =
    [
        (CategoryType.Expense, "Moradia"),
        (CategoryType.Expense, "Alimentação"),
        (CategoryType.Expense, "Transporte"),
        (CategoryType.Expense, "Saúde"),
        (CategoryType.Expense, "Educação"),
        (CategoryType.Expense, "Lazer"),
        (CategoryType.Expense, "Assinaturas"),
        (CategoryType.Expense, "Outros"),
        (CategoryType.Expense, "Sem categoria"),
        (CategoryType.Income, "Salário"),
        (CategoryType.Income, "Freelance"),
        (CategoryType.Income, "Transferência recebida"),
        (CategoryType.Income, "Outros"),
        (CategoryType.Income, "Sem categoria")
    ];
}
