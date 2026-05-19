using System.Text.Json;
using inz.DocumentExceptions;
using inz.UserExceptions;

namespace inz.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Wystąpił nieobsłużony wyjątek.");

            await HandleExceptionAsync(context, exception);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = exception switch
        {
            // Walidacja
            ArgumentNullException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidDocumentException => StatusCodes.Status400BadRequest,

            // Nie znaleziono
            DocumentMetadataNotFoundException => StatusCodes.Status404NotFound,
            DocumentNotFoundException => StatusCodes.Status404NotFound,
            UserNotFoundException => StatusCodes.Status404NotFound,
            OrganizationNotFoundException => StatusCodes.Status404NotFound,
            RoleNotFoundException => StatusCodes.Status404NotFound,

            // Konflikt stanu
            DocumentUnavailableException => StatusCodes.Status409Conflict,
            DocumentWrongStatusException => StatusCodes.Status409Conflict,
            UserAlreadyExistsException => StatusCodes.Status409Conflict,
            RoleAlreadyAssignedException => StatusCodes.Status409Conflict,
            UserInactiveException => StatusCodes.Status409Conflict,
            EmailAlreadyRegisteredException => StatusCodes.Status409Conflict,

            // Autentykacja / autoryzacja
            InvalidCredentialsException => StatusCodes.Status401Unauthorized,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            InsufficientPermissionsException => StatusCodes.Status403Forbidden,

            // Błędy przetwarzania
            DocumentProcessingFailureException => StatusCodes.Status500InternalServerError,
            DocumentRetrievalFailureException => StatusCodes.Status500InternalServerError,
            DocumentDeletionFailureException => StatusCodes.Status500InternalServerError,

            _ => StatusCodes.Status500InternalServerError
        };

        var response = new
        {
            status = statusCode,
            error = exception.GetType().Name,
            message = exception.Message
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(response);

        await context.Response.WriteAsync(json);
    }
}
