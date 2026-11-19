using Microsoft.AspNetCore.Diagnostics;
using NArchitecture.Core.CrossCuttingConcerns.Exception.Types;

namespace Okul.WebApi.Errors;

/// <summary>
/// Maps exceptions to RFC 9457 ProblemDetails (docs/conventions.md, Error handling). Messages are
/// Turkish; stack traces and internals never leave the server.
/// </summary>
internal sealed class OkulExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<OkulExceptionHandler> _logger;

    public OkulExceptionHandler(IProblemDetailsService problemDetails, ILogger<OkulExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException e => (StatusCodes.Status404NotFound, "Bulunamadı", e.Message),
            BusinessException e => (StatusCodes.Status400BadRequest, "İş kuralı ihlali", e.Message),
            ValidationException e => (StatusCodes.Status400BadRequest, "Doğrulama hatası", e.Message),
            AuthorizationException => (StatusCodes.Status403Forbidden, "Yetkisiz işlem", "Bu işlem için yetkiniz yok."),
            _ => (StatusCodes.Status500InternalServerError, "Sunucu hatası", "Beklenmeyen bir hata oluştu."),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception, trace {TraceId}", httpContext.TraceIdentifier);
        }
        else
        {
            _logger.LogInformation("Expected failure {Status}: {Title}", status, title);
        }

        httpContext.Response.StatusCode = status;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = status, Title = title, Detail = detail },
        });
    }
}
