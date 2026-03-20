using Microsoft.AspNetCore.Mvc;

namespace Farol.Api.Common;

public static class ApiValidationErrorFactory
{
    public static BadRequestObjectResult Create(ActionContext context)
    {
        var message = context.ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? error.Exception?.Message
                : error.ErrorMessage)
            .FirstOrDefault(errorMessage => !string.IsNullOrWhiteSpace(errorMessage))
            ?? "Request payload is invalid.";

        return new BadRequestObjectResult(new ErrorResponse(message));
    }
}
