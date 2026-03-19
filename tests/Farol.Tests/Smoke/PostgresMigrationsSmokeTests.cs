using System.Data;
using System.Data.Common;
using Farol.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Farol.Tests.Smoke;

[Trait("Category", "PostgresSmoke")]
public sealed class PostgresMigrationsSmokeTests
{
    private const string EnableEnvironmentVariable = "FAROL_RUN_POSTGRES_SMOKE";

    [Fact]
    public async Task Migrate_ShouldCreateCriticalTablesInRealPostgresDatabase()
    {
        if (!ShouldRunSmokeTests())
        {
            return;
        }

        var baseConnectionString = ResolveBaseConnectionString();
        var databaseName = $"farol_smoke_{Guid.NewGuid():N}";
        var adminConnectionString = BuildConnectionString(baseConnectionString, "postgres");
        var testConnectionString = BuildConnectionString(baseConnectionString, databaseName);

        try
        {
            await CreateDatabaseAsync(adminConnectionString, databaseName);

            var options = new DbContextOptionsBuilder<FarolDbContext>()
                .UseNpgsql(testConnectionString)
                .Options;

            await using var dbContext = new FarolDbContext(options);
            await dbContext.Database.MigrateAsync();

            var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
            Assert.Contains("20260317220829_AddBills", appliedMigrations);

            var tables = await GetTableNamesAsync(dbContext.Database.GetDbConnection());

            Assert.Contains("__EFMigrationsHistory", tables);
            Assert.Contains("bills", tables);
            Assert.Contains("transactions", tables);
            Assert.Contains("categories", tables);
            Assert.Contains("users", tables);
            Assert.Contains("financial_accounts", tables);
        }
        finally
        {
            await DropDatabaseAsync(adminConnectionString, databaseName);
        }
    }

    private static bool ShouldRunSmokeTests()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(EnableEnvironmentVariable),
            "1",
            StringComparison.Ordinal);
    }

    private static string ResolveBaseConnectionString()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(ResolveApiProjectPath())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .Build();

        return Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection configuration is required.");
    }

    private static string BuildConnectionString(string connectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = databaseName,
            Pooling = false
        };

        return builder.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string adminConnectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);

        try
        {
            await connection.OpenAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Could not connect to local PostgreSQL. Start docker compose up -d before running the Postgres smoke tests.",
                exception);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"""CREATE DATABASE "{databaseName}";""";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<HashSet<string>> GetTableNamesAsync(DbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT tablename
            FROM pg_catalog.pg_tables
            WHERE schemaname = 'public';
            """;

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    private static async Task DropDatabaseAsync(string adminConnectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();

        await using (var terminateCommand = connection.CreateCommand())
        {
            terminateCommand.CommandText = """
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = @databaseName
                  AND pid <> pg_backend_pid();
                """;
            terminateCommand.Parameters.AddWithValue("databaseName", databaseName);
            await terminateCommand.ExecuteNonQueryAsync();
        }

        await using var dropCommand = connection.CreateCommand();
        dropCommand.CommandText = $"""DROP DATABASE IF EXISTS "{databaseName}";""";
        await dropCommand.ExecuteNonQueryAsync();
    }

    private static string ResolveApiProjectPath()
    {
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (currentDirectory is not null)
        {
            var candidate = Path.Combine(currentDirectory.FullName, "src", "Farol.Api");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Farol.Api project directory.");
    }
}
