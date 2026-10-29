using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Proc.Api.Health;

/// <summary>
/// Ghi kết quả health check dưới dạng JSON gọn: trạng thái chung, phiên bản và từng mục kiểm tra.
/// </summary>
public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Ghi báo cáo health check ra response.
    /// </summary>
    /// <param name="context">HTTP context của request.</param>
    /// <param name="report">Kết quả các health check.</param>
    /// <param name="version">Số phiên bản ứng dụng (từ Directory.Build.props).</param>
    /// <returns>Task hoàn tất khi đã ghi xong.</returns>
    public static Task WriteAsync(HttpContext context, HealthReport report, string version)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var body = new HealthResponse(
            report.Status.ToString(),
            version,
            report.Entries.Select(e => new HealthEntryResponse(
                e.Key,
                e.Value.Status.ToString(),
                Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                // Không trả chi tiết exception ra ngoài để tránh lộ thông tin kết nối.
                e.Value.Exception is null ? e.Value.Description : "Không kết nối được.")).ToList());

        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}

/// <summary>Kết quả health check tổng hợp.</summary>
/// <param name="Status">Healthy, Degraded hoặc Unhealthy.</param>
/// <param name="Version">Số phiên bản ứng dụng.</param>
/// <param name="Checks">Danh sách từng mục kiểm tra.</param>
public sealed record HealthResponse(string Status, string Version, IReadOnlyList<HealthEntryResponse> Checks);

/// <summary>Kết quả một mục health check.</summary>
/// <param name="Name">Tên mục kiểm tra (ví dụ "database").</param>
/// <param name="Status">Trạng thái của mục.</param>
/// <param name="DurationMs">Thời gian kiểm tra, mili giây.</param>
/// <param name="Description">Mô tả thêm, nếu có.</param>
public sealed record HealthEntryResponse(string Name, string Status, double DurationMs, string? Description);
