using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using System.Text.Json;

public class CustomAuthorizationHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler DefaultHandler = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context,
                                  AuthorizationPolicy policy,
                                  PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var message = new
            {
                error = "Forbidden",
                message = "You do not have permission to access this endpoint."
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(message));
            return;
        }

        if (authorizeResult.Challenged)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";

            var message = new
            {
                error = "Unauthorized",
                message = "You must be logged in to access this endpoint."
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(message));
            return;
        }

        // Otherwise use default behavior
        await DefaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
