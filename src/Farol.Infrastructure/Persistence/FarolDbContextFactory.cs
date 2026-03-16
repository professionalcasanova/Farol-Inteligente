using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Farol.Infrastructure.Persistence;

public sealed class FarolDbContextFactory : IDesignTimeDbContextFactory<FarolDbContext>
{
    public FarolDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(ResolveApiProjectPath())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .Build();

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection configuration is required.");

        var optionsBuilder = new DbContextOptionsBuilder<FarolDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new FarolDbContext(optionsBuilder.Options);
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
