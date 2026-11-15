using Microsoft.AspNetCore.Diagnostics;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Domain.Common;
using Proc.Infrastructure.Persistence;

namespace Proc.Api.Errors;

/// <summary>
/// Chuyển exception thành ProblemDetails: lỗi nghiệp vụ trả 400, không đủ quyền/ngoài phạm vi trả 403, không tìm thấy trả 404,
/// trùng khóa duy nhất trong CSDL trả 409, đều kèm thông điệp tiếng Việt; lỗi khác trả 500 với thông điệp chung kèm <b>mã theo dõi</b> (chi tiết chỉ
/// ghi vào nhật ký lỗi ERR và log kỹ thuật, không lộ ra ngoài – lát QT2).
/// </summary>
public sealed class ProblemExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly IErrorRecorder _errors;
    private readonly ILogger<ProblemExceptionHandler> _logger;

    /// <summary>
    /// Tạo bộ xử lý lỗi.
    /// </summary>
    /// <param name="problemDetails">Dịch vụ ghi ProblemDetails.</param>
    /// <param name="errors">Nhật ký lỗi (màn hình ERR).</param>
    /// <param name="logger">Log kỹ thuật.</param>
    public ProblemExceptionHandler(IProblemDetailsService problemDetails, IErrorRecorder errors, ILogger<ProblemExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _errors = errors;
        _logger = logger;
    }

    /// <summary>Câu báo lỗi 500 cho người dùng – mã theo dõi để báo quản trị, trùng mã trên màn hình Nhật ký lỗi.</summary>
    /// <param name="trackingCode">Mã theo dõi.</param>
    /// <returns>Câu báo lỗi.</returns>
    public static string ServerErrorMessage(string trackingCode) =>
        $"Có lỗi hệ thống (mã theo dõi {trackingCode}). Hãy thử lại hoặc báo quản trị hệ thống kèm mã này.";

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            BusinessRuleException => (StatusCodes.Status400BadRequest, exception.Message),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, exception.Message),
            NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            // Sửa trên bản cũ, hoặc hai lệnh lưu cùng một dòng cùng lúc (lát R2 – design.md mục 18.26).
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, exception.Message),
            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, ConcurrencyConflictException.DefaultMessage),
            _ when DuplicateKeyMessages.Describe(exception) is { } duplicate => (StatusCodes.Status409Conflict, duplicate),
            _ => (StatusCodes.Status500InternalServerError, string.Empty),
        };

        string? trackingCode = null;
        if (status == StatusCodes.Status500InternalServerError)
        {
            // Ghi nhật ký lỗi trước (không ném lỗi); không dùng token của request – client ngắt kết nối vẫn phải ghi được lỗi.
            var scope = httpContext.RequestServices.GetService<ICurrentUserScope>();
            trackingCode = await _errors.RecordServerAsync(new ServerErrorReport(exception, httpContext.Request.Path + httpContext.Request.QueryString,
                httpContext.Request.Method, httpContext.Request.Headers.UserAgent.ToString(), SafeCompanyId(scope)), CancellationToken.None);
            _logger.LogError(exception, "Lỗi chưa xử lý {TrackingCode} khi gọi {Path}", trackingCode, httpContext.Request.Path);
            title = ServerErrorMessage(trackingCode);
        }

        httpContext.Response.StatusCode = status;
        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = status, Title = title },
        };
        if (trackingCode is not null) context.ProblemDetails.Extensions["trackingCode"] = trackingCode;
        return await _problemDetails.TryWriteAsync(context);
    }

    /// <summary>Công ty đang làm việc của request; phạm vi chưa nạp được (lỗi xảy ra trước middleware phạm vi) thì null.</summary>
    private static Guid? SafeCompanyId(ICurrentUserScope? scope)
    {
        try
        {
            return scope?.CompanyId;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
