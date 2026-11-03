using Microsoft.AspNetCore.Diagnostics;

namespace UniFlow.Api.Errors;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService,
        IHostEnvironment environment, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _environment = environment;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, code) = ErrorMapping.Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Request failed with {Code}.", code);
        else
            _logger.LogWarning("Request rejected with {Code}: {Message}", code, exception.Message);

        httpContext.Response.StatusCode = status;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = status,
                // Exception text only while developing - it is technical and in English
                Detail = _environment.IsDevelopment() ? exception.Message : null,
                Extensions = { ["code"] = code }
            }
        });
    }
}