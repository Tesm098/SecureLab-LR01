
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace SecureLab.Api.Scaffolding;

// ЛР №6. Спільна валідація CSRF для змінювальних маршрутів.
public sealed class Lab06AntiforgeryFilter(
    IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(
                context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(
                title: "CSRF token is missing or invalid.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}
