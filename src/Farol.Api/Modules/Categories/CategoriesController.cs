using Farol.Api.Common;
using Farol.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Categories;

[ApiController]
[Authorize]
[Route("api/categories")]
public sealed class CategoriesController(FarolDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> List(CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var categories = await dbContext.Categories
            .AsNoTracking()
            .Where(category => category.IsSystem || category.UserId == userId)
            .OrderBy(category => category.Type)
            .ThenBy(category => category.Name)
            .Select(category => new CategoryResponse
            {
                Id = category.Id,
                Name = category.Name,
                Type = category.Type,
                IsSystem = category.IsSystem
            })
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }
}
