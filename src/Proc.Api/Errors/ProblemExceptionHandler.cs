using Microsoft.AspNetCore.Diagnostics;
using Proc.Domain.Common;

namespace Proc.Api.Errors;

/// <summary>
/// Chuyển exception thành ProblemDetails: lỗi nghiệp vụ trả 400 kèm thông điệp tiếng Việt;
/// lỗi khác trả 500 với thông điệp chung (chi tiết chỉ ghi vào log, không lộ ra ngoài).
/// </summary>
public sealed class ProblemExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<ProblemExceptionHandler> _logger;

    /// <summary>
    /// Tạo bộ xử lý lỗi.
    /// </summary>
    /// <param name="problemDetails">Dịch vụ ghi ProblemDetails.</param>
    /// <param name="logger">Log kỹ thuật.</param>
    public ProblemExceptionHandler(IProblemDetailsService problemDetails, ILogger<ProblemExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception is BusinessRuleException
            ? (StatusCodes.Status400BadRequest, exception.Message)
            : (StatusCodes.Status500InternalServerError, "Có lỗi hệ thống. Hãy thử lại hoặc báo quản trị hệ thống.");

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Lỗi chưa xử lý khi gọi {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = status, Title = title },
        });
    }
}
