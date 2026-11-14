using Proc.Domain.Common;

namespace Proc.Domain.Operations;

/// <summary>
/// Tên môi trường cài đặt (design.md mục 21.3, quyết định E1): mỗi máy chủ cấu hình <c>App:Environment</c> là một trong ba giá trị này.
/// </summary>
public static class AppEnvironments
{
    /// <summary>Máy lập trình – dữ liệu mẫu, dải xám "DEV".</summary>
    public const string Dev = "Dev";

    /// <summary>Máy thử nghiệm (UAT) – bản sao dữ liệu thật, dải cam "MÔI TRƯỜNG TEST", email chuyển về hộp thư test.</summary>
    public const string Test = "Test";

    /// <summary>Máy chạy thật – không dải.</summary>
    public const string Prod = "Prod";

    /// <summary>
    /// Máy <b>chưa khai</b> <c>App:Environment</c> (người dùng chốt 14/11/2026): không gửi email nào, dải đỏ cảnh báo trên mọi màn hình, SYS đèn
    /// đỏ – tránh máy TEST quên cấu hình mà gửi thư thật tới người dùng, NCC. Không phải giá trị khai được trong cấu hình.
    /// </summary>
    public const string None = "None";

    /// <summary>Ba giá trị khai được trong cấu hình.</summary>
    public static readonly IReadOnlyList<string> All = [Dev, Test, Prod];

    /// <summary>
    /// Chuẩn hóa tên môi trường (không phân biệt hoa thường, bỏ khoảng trắng), ví dụ "test" → "Test".
    /// </summary>
    /// <param name="value">Giá trị trong cấu hình.</param>
    /// <returns>Tên chuẩn; null khi không phải Dev/Test/Prod.</returns>
    public static string? Normalize(string? value) =>
        All.FirstOrDefault(e => string.Equals(e, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Kết quả một thao tác vận hành.</summary>
public enum OperationResult
{
    /// <summary>Thành công.</summary>
    Succeeded = 0,

    /// <summary>Lỗi.</summary>
    Failed = 1,

    /// <summary>Chỉ ghi nhận sự kiện (khởi động ứng dụng…), không có thành công / lỗi.</summary>
    Info = 2,
}

/// <summary>
/// Một dòng nhật ký vận hành (module QT – design.md mục 21.2, nguyên tắc N3): ai làm việc vận hành gì, ở môi trường nào, lý do, kết quả.
/// <b>Chỉ thêm</b> – không có hàm sửa; CSDL chặn UPDATE / DELETE bằng trigger như AuditLog. Mỗi dòng còn được ghi ra file ngoài CSDL vì khôi phục
/// CSDL sẽ thay luôn nhật ký trong CSDL.
/// </summary>
public sealed class OperationLog
{
    /// <summary>Độ dài tối đa loại việc.</summary>
    public const int KindMaxLength = 50;

    /// <summary>Độ dài tối đa đối tượng, lý do.</summary>
    public const int TextMaxLength = 500;

    /// <summary>Độ dài tối đa chi tiết.</summary>
    public const int DetailMaxLength = 4000;

    private OperationLog()
    {
    }

    /// <summary>Id (uuid v7).</summary>
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    /// <summary>Thời điểm (UTC).</summary>
    public DateTimeOffset OccurredAt { get; private init; }

    /// <summary>Môi trường ghi dòng này (Dev / Test / Prod; None = máy chưa khai).</summary>
    public string Environment { get; private init; } = AppEnvironments.Prod;

    /// <summary>Người thao tác; null = hệ thống (khởi động, dịch vụ nền).</summary>
    public Guid? UserId { get; private init; }

    /// <summary>Tên đăng nhập lúc thao tác; null = hệ thống.</summary>
    public string? UserName { get; private init; }

    /// <summary>Loại việc, ví dụ AppStarted, BackupFull, Restore (mã tiếng Anh, giao diện dịch sang tiếng Việt).</summary>
    public string Kind { get; private init; } = string.Empty;

    /// <summary>Đối tượng của việc (tên bản sao lưu, phiên bản…).</summary>
    public string? Target { get; private init; }

    /// <summary>Lý do người làm ghi (bắt buộc với việc nguy hiểm – Q6).</summary>
    public string? Reason { get; private init; }

    /// <summary>Kết quả.</summary>
    public OperationResult Result { get; private init; }

    /// <summary>Chi tiết (thông báo lỗi, số liệu…).</summary>
    public string? Detail { get; private init; }

    /// <summary>
    /// Tạo một dòng nhật ký. Chữ dài quá giới hạn cột thì cắt bớt – nhật ký không được làm hỏng thao tác chính.
    /// </summary>
    /// <param name="occurredAt">Thời điểm (UTC).</param>
    /// <param name="environment">Môi trường (Dev / Test / Prod).</param>
    /// <param name="userId">Người thao tác; null = hệ thống.</param>
    /// <param name="userName">Tên đăng nhập.</param>
    /// <param name="kind">Loại việc (bắt buộc).</param>
    /// <param name="target">Đối tượng.</param>
    /// <param name="reason">Lý do.</param>
    /// <param name="result">Kết quả.</param>
    /// <param name="detail">Chi tiết.</param>
    /// <returns>Dòng nhật ký.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu loại việc hoặc môi trường không phải Dev/Test/Prod/None.</exception>
    public static OperationLog Create(
        DateTimeOffset occurredAt, string environment, Guid? userId, string? userName, string kind, string? target, string? reason,
        OperationResult result, string? detail)
    {
        var env = (environment == AppEnvironments.None ? AppEnvironments.None : AppEnvironments.Normalize(environment))
            ?? throw new BusinessRuleException($"Môi trường \"{environment}\" không hợp lệ – chỉ Dev, Test, Prod (hoặc None khi máy chưa khai).");
        if (string.IsNullOrWhiteSpace(kind)) throw new BusinessRuleException("Nhật ký vận hành phải có loại việc.");
        return new OperationLog
        {
            OccurredAt = occurredAt,
            Environment = env,
            UserId = userId,
            UserName = Cut(userName, 256),
            Kind = Cut(kind, KindMaxLength)!,
            Target = Cut(target, TextMaxLength),
            Reason = Cut(reason, TextMaxLength),
            Result = result,
            Detail = Cut(detail, DetailMaxLength),
        };
    }

    /// <summary>Bỏ khoảng trắng hai đầu, chữ rỗng thành null, dài quá thì cắt.</summary>
    private static string? Cut(string? value, int max) =>
        value?.Trim() is { Length: > 0 } v ? v[..Math.Min(v.Length, max)] : null;
}
