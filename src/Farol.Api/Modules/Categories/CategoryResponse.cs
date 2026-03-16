using Farol.Domain.Categories;

namespace Farol.Api.Modules.Categories;

public sealed class CategoryResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required CategoryType Type { get; init; }
    public required bool IsSystem { get; init; }
}
