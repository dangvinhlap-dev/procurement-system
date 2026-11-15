using Microsoft.AspNetCore.Identity;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Domain.Common;
using Proc.Domain.Operations;
using Proc.Infrastructure.Identity;

namespace Proc.Infrastructure.Operations;

/// <summary>
/// Xác nhận việc nguy hiểm theo Q6 (design.md mục 21.1): nhập lại mật khẩu của chính người đang đăng nhập + gõ đúng chữ xác nhận (lý do do từng
/// việc kiểm tra – ví dụ <see cref="MaintenanceRun.Queue"/>). Mục đích: phiên đăng nhập bị bỏ quên trên máy khác, hoặc bấm nhầm, không đủ để
/// ngắt phiên / phục hồi / cài PROD.
/// </summary>
public sealed class DangerousActionGuard : IDangerousActionGuard
{
    private readonly UserManager<AppUser> _users;
    private readonly ICurrentUser _currentUser;
    private readonly IOperationLog _operationLog;

    /// <summary>Tạo chốt.</summary>
    /// <param name="users">Kiểm tra mật khẩu (ASP.NET Identity).</param>
    /// <param name="currentUser">Người đang đăng nhập.</param>
    /// <param name="operationLog">Nhật ký vận hành (ghi lần sai mật khẩu).</param>
    public DangerousActionGuard(UserManager<AppUser> users, ICurrentUser currentUser, IOperationLog operationLog)
    {
        _users = users;
        _currentUser = currentUser;
        _operationLog = operationLog;
    }

    /// <inheritdoc />
    public async Task VerifyAsync(
        string action, string? password, string? confirmation, string expectedConfirmation, CancellationToken cancellationToken)
    {
        // Chữ xác nhận so không phân biệt hoa thường (gõ "ngắt phiên" vẫn được) nhưng phải đúng dấu; chuẩn hóa NFC vì bộ gõ tiếng Việt có thể gửi
        // dấu tổ hợp (ắ = a + ˘ + ´) – nhìn giống hệt nhưng khác mã.
        if (!string.Equals(confirmation?.Trim().Normalize(), expectedConfirmation.Normalize(), StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException($"Gõ đúng chữ \"{expectedConfirmation}\" để xác nhận.");
        if (string.IsNullOrEmpty(password)) throw new BusinessRuleException("Nhập lại mật khẩu của bạn để xác nhận.");

        var user = _currentUser.UserId is { } id ? await _users.FindByIdAsync(id.ToString()) : null;
        // Dùng CheckPasswordAsync (không tăng đếm sai của khóa đăng nhập): người đang đăng nhập gõ sai khi xác nhận không bị khóa tài khoản;
        // mỗi lần sai vẫn ghi nhật ký vận hành để thấy ai thử nhiều lần.
        if (user is null || !await _users.CheckPasswordAsync(user, password))
        {
            await _operationLog.WriteAsync(
                new OperationLogEntry(OperationKinds.DangerConfirmFailed, action, null, OperationResult.Failed, "Mật khẩu xác nhận không đúng."),
                cancellationToken);
            throw new BusinessRuleException("Mật khẩu xác nhận không đúng.");
        }
    }
}
