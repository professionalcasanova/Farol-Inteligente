using System.Data;
using Farol.Domain.Categories;
using Farol.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Infrastructure.Seeding;

public static class DatabaseSeeder
{
    public static async Task SeedSystemCategoriesAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return;
        }

        if (!await CategoriesTableExistsAsync(dbContext, cancellationToken))
        {
            return;
        }

        var existingCategories = await dbContext.Categories
            .AsNoTracking()
            .Where(category => category.IsSystem)
            .Select(category => new { category.Type, category.Name })
            .ToListAsync(cancellationToken);

        var existingKeys = existingCategories
            .Select(category => BuildKey(category.Type, category.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (type, name) in CategorySeed.SystemCategories)
        {
            if (existingKeys.Contains(BuildKey(type, name)))
            {
                continue;
            }

            dbContext.Categories.Add(Category.CreateSystem(name, type));
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<bool> CategoriesTableExistsAsync(
        FarolDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var mustCloseConnection = connection.State != ConnectionState.Open;

        if (mustCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'categories'
                );
                """;

            var result = await command.ExecuteScalarAsync(cancellationToken);

            return result is bool exists && exists;
        }
        finally
        {
            if (mustCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string BuildKey(CategoryType type, string name)
    {
        return $"{(int)type}:{name}";
    }
}
