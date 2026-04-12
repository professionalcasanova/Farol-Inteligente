using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Farol.Api.Modules.Auth;
using Farol.Api.Modules.Categories;
using Farol.Domain.Categories;
using Farol.Infrastructure.Persistence;
using Farol.Infrastructure.Seeding;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Tests.Api;

public sealed class CategoriesEndpointsTests : IClassFixture<FarolApiFactory>
{
    private readonly FarolApiFactory _factory;

    public CategoriesEndpointsTests(FarolApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCategories_ShouldRequireAuthentication()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCategories_ShouldReturnSystemAndUserOwnedCategories()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
            var userId = dbContext.Users.Single(user => user.Email == "maria@email.com").Id;

            dbContext.Categories.Add(Category.CreateSystem("Moradia", CategoryType.Expense));
            dbContext.Categories.Add(Category.CreateUserOwned(userId, "Pets", CategoryType.Expense));
            dbContext.Categories.Add(Category.CreateUserOwned(Guid.NewGuid(), "Outra Pessoa", CategoryType.Income));
            await dbContext.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var categories = await client.GetFromJsonAsync<List<CategoryResponse>>("/api/categories");

        Assert.NotNull(categories);
        Assert.Equal(2, categories.Count);
        Assert.Contains(categories, category => category.Name == "Moradia" && category.IsSystem);
        Assert.Contains(categories, category => category.Name == "Pets" && !category.IsSystem);
    }

    [Fact]
    public async Task GetCategories_ShouldExposeExpandedBrazilianSystemTaxonomyWithoutPixCategory()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

            foreach (var (type, name) in CategorySeed.SystemCategories)
            {
                dbContext.Categories.Add(Category.CreateSystem(name, type));
            }

            await dbContext.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var categories = await client.GetFromJsonAsync<List<CategoryResponse>>("/api/categories");

        Assert.NotNull(categories);
        Assert.Contains(categories, category => category.Name == "Mercado" && category.IsSystem);
        Assert.Contains(categories, category => category.Name == "Contas e serviços" && category.IsSystem);
        Assert.Contains(categories, category => category.Name == "Benefícios" && category.IsSystem);
        Assert.Contains(categories, category => category.Name == "Rendimento" && category.IsSystem);
        Assert.DoesNotContain(categories, category => string.Equals(category.Name, "PIX", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Name = "Usuario Teste",
            Email = email,
            Password = "123456"
        });

        response.EnsureSuccessStatusCode();

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(authResponse);

        return authResponse.AccessToken;
    }
}
