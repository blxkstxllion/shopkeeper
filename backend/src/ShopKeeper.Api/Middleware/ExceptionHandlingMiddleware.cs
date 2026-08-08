namespace ShopKeeper.Api.Middleware;

using System.Net;
using System.Text.Json;
using FluentValidation;
using ShopKeeper.Application.Common.Exceptions;

/// <summary>
/// Translates exceptions into the API's standard error envelope so clients (and the
/// frontend's error boundary) never see raw stack traces or EF/Npgsql messages -
/// see section 34 of the product spec ("never display raw backend errors").
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, errors) = exception switch
        {
            ValidationException vex => (HttpStatusCode.BadRequest, "One or more fields are invalid.",
                vex.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),
            NotFoundException nfe => (HttpStatusCode.NotFound, nfe.Message, null),
            ForbiddenAccessException fae => (HttpStatusCode.Forbidden, fae.Message, null),
            AuthenticationException aue => (HttpStatusCode.Unauthorized, aue.Message, null),
            ConflictException ce => (HttpStatusCode.Conflict, ce.Message, null),
            _ => (HttpStatusCode.InternalServerError, "Something went wrong on our end. Please try again.", null),
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = new { title, status = (int)statusCode, errors };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
