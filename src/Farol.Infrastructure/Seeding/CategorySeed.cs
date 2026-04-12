using Farol.Domain.Categories;

namespace Farol.Infrastructure.Seeding;

public static class CategorySeed
{
    public static IReadOnlyList<(CategoryType Type, string Name)> SystemCategories { get; } =
    [
        (CategoryType.Expense, "Moradia"),
        (CategoryType.Expense, "Alimentacao"),
        (CategoryType.Expense, "Contas e servicos"),
        (CategoryType.Expense, "Mercado"),
        (CategoryType.Expense, "Restaurantes"),
        (CategoryType.Expense, "Transporte"),
        (CategoryType.Expense, "Mobilidade"),
        (CategoryType.Expense, "Saude"),
        (CategoryType.Expense, "Educacao"),
        (CategoryType.Expense, "Lazer"),
        (CategoryType.Expense, "Compras"),
        (CategoryType.Expense, "Cuidados pessoais"),
        (CategoryType.Expense, "Familia e filhos"),
        (CategoryType.Expense, "Pets"),
        (CategoryType.Expense, "Impostos e taxas"),
        (CategoryType.Expense, "Viagem"),
        (CategoryType.Expense, "Presentes e doacoes"),
        (CategoryType.Expense, "Assinaturas"),
        (CategoryType.Expense, "Outros"),
        (CategoryType.Expense, "Sem categoria"),
        (CategoryType.Income, "Salario"),
        (CategoryType.Income, "Adiantamento"),
        (CategoryType.Income, "Beneficios"),
        (CategoryType.Income, "Freelance"),
        (CategoryType.Income, "Comissao"),
        (CategoryType.Income, "Reembolso"),
        (CategoryType.Income, "Venda"),
        (CategoryType.Income, "Aluguel recebido"),
        (CategoryType.Income, "Restituicao"),
        (CategoryType.Income, "Rendimento"),
        (CategoryType.Income, "Transferencia recebida"),
        (CategoryType.Income, "Presente recebido"),
        (CategoryType.Income, "Outros"),
        (CategoryType.Income, "Sem categoria")
    ];
}
