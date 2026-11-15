using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Domain.Common;
using Proc.Domain.Operations;
using Proc.Infrastructure.Identity;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Operations;

/// <summary>
/// Xác nhận việc nguy hiểm theo Q6 (design.md mục 21.1): nhập lại mật khẩu của chính người đang đăng nhập + gõ đúng chữ xác nhận (lý do do từng
/// việc kiểm tra – ví dụ <see cref="MaintenanceRun.Queue"/>). Mục đích: phiên đăng nhập bị bỏ quên trên máy khác, hoặc bấm nhầm, không đủ để
/// ngắt phiên / phục hồi / cài PROD. Sai mật khẩu đủ số lần trong số phút của cấu hình truy cập CFG (mặc định 5 lần, 10 phút) thì phải chờ mới xác
/// nhận lại được – như đăng nhập (người dùng chốt 15/11/2026; lát QT7 lấy số từ CFG), nhưng <b>không khóa tài khoản</b>: đăng nhập vẫn bình thường.
/// </summary>
public sealed class DangerousActionGuard : IDangerousActionGuard
{
    private readonly UserManager<AppUser> _users;
    private readonly ICurrentUser _currentUser;
    private readonly IOperationLog _operationLog;
    private readonly ProcDbContext _db;
    private readonly IClock _clock;
    private readonly IAccessPolicyProvider _policy;

    /// <summary>Tạo chốt.</summary>
    /// <param name="users">Kiểm tra mật khẩu (ASP.NET Identity).</param>
    /// <param name="currentUser">Người đang đăng nhập.</param>
    /// <param name="operationLog">Nhật ký vận hành (ghi lần sai mật khẩu – cũng là nơi đếm lần sai).</param>
    /// <param name="db">CSDL (đọc nhật ký vận hành).</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="policy">Cấu hình truy cập: số lần sai, số phút chờ (lát QT7).</param>
    public DangerousActionGuard(
        UserManager<AppUser> users, ICurrentUser currentUser, IOperationLog operationLog, ProcDbContext db, IClock clock,
        IAccessPolicyProvider policy)
    {
        _policy = policy;
        _users = users;
        _currentUser = currentUser;
        _operationLog = operationLog;
        _db = db;
        _clock = clock;
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

        var userId = _currentUser.UserId;
        await EnsureNotWaitingAsync(userId, cancellationToken);

        var user = userId is { } id ? await _users.FindByIdAsync(id.ToString()) : null;
        // CheckPasswordAsync không tăng đếm sai của Identity: sai khi xác nhận không chặn đăng nhập (không khóa tài khoản). Lần sai đếm bằng nhật
        // ký vận hành DangerConfirmFailed – cũng để thấy ai thử nhiều lần.
        if (user is null || !await _users.CheckPasswordAsync(user, password))
        {
            await _operationLog.WriteAsync(
                new OperationLogEntry(OperationKinds.DangerConfirmFailed, action, null, OperationResult.Failed, "Mật khẩu xác nhận không đúng."),
                cancellationToken);
            throw new BusinessRuleException("Mật khẩu xác nhận không đúng.");
        }
    }

    /// <summary>
    /// Đủ <c>MaxFailedLogins</c> lần sai trong <c>LockoutMinutes</c> phút gần nhất (cấu hình truy cập) thì chặn đến khi lần sai cũ nhất trong số
    /// đó quá số phút đó. Ví dụ 5 lần, 10 phút: sai lúc 10:00, 10:01, 10:02, 10:03, 10:04 → chờ đến 10:10 (lần 10:00 hết hạn, còn 4 lần sai).
    /// </summary>
    private async Task EnsureNotWaitingAsync(Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is null) return;
        var policy = await _policy.GetAsync(cancellationToken);
        var maxFailedAttempts = policy.MaxFailedLogins;
        var waitDuration = TimeSpan.FromMinutes(policy.LockoutMinutes);
        var now = _clock.UtcNow;
        var since = now - waitDuration;
        var failures = await _db.OperationLogs.AsNoTracking()
            .Where(l => l.UserId == userId && l.Kind == OperationKinds.DangerConfirmFailed && l.OccurredAt > since)
            .OrderByDescending(l => l.OccurredAt)
            .Select(l => l.OccurredAt)
            .Take(maxFailedAttempts)
            .ToListAsync(cancellationToken);
        if (failures.Count < maxFailedAttempts) return;

        var until = TimeZoneInfo.ConvertTime(failures[^1] + waitDuration, VietnamTime.Zone);
        throw new BusinessRuleException(
            $"Nhập sai mật khẩu xác nhận {maxFailedAttempts} lần – chờ {policy.LockoutMinutes} phút, thử lại sau " +
            $"{until.ToString("HH:mm", CultureInfo.InvariantCulture)}.");
    }
}
