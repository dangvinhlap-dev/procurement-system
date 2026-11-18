using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Notifications;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Documents;

/// <summary>
/// Chữ ký người dùng theo phiên bản (lát IN1 – design.md mục 20.4). Chính chủ tự tải / ngừng dùng; người có quyền
/// <see cref="Permissions.System.UserSignature"/> làm thay (bắt buộc lý do, người được làm thay nhận thông báo).
/// </summary>
public sealed class UserSignatureService : IUserSignatureService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    public UserSignatureService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<UserSignatureDto> GetAsync(Guid? userId, CancellationToken cancellationToken) =>
        await ReadAsync(Target(userId), cancellationToken);

    /// <inheritdoc />
    public async Task<byte[]> GetImageAsync(Guid? userId, int? versionNo, CancellationToken cancellationToken)
    {
        var target = Target(userId);
        var query = _db.UserSignatures.AsNoTracking().Where(s => s.UserId == target);
        query = versionNo is { } v ? query.Where(s => s.VersionNo == v) : query.Where(s => s.ValidTo == null);
        return await query.Select(s => s.Image).SingleOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException(versionNo is null ? "Chưa có chữ ký đang dùng." : $"Không có chữ ký phiên bản {versionNo}.");
    }

    /// <inheritdoc />
    public async Task<UserSignatureDto> UploadAsync(Guid? userId, byte[] image, string? reason, CancellationToken cancellationToken)
    {
        var target = Target(userId);
        var actor = _scope.UserId!.Value;
        await EnsureUserAsync(target, cancellationToken);
        var now = _clock.UtcNow;
        var versions = await _db.UserSignatures.Where(s => s.UserId == target).ToListAsync(cancellationToken);
        var signature = UserSignature.Create(target, versions.Count == 0 ? 1 : versions.Max(s => s.VersionNo) + 1, image, actor, now, reason);
        // Thay chữ ký: bản cũ hết hiệu lực đúng lúc bản mới bắt đầu – phiếu duyệt trước lúc này vẫn in chữ ký cũ (người dùng chốt 18/11/2026).
        versions.SingleOrDefault(s => s.ValidTo is null)?.Retire(now, actor, null, replaced: true);
        _db.UserSignatures.Add(signature);
        if (target != actor) Notify(target, "Chữ ký của bạn đã được cập nhật", $"Người quản trị đã tải chữ ký mới cho bạn. Lý do: {signature.Reason}");
        await SaveAsync(cancellationToken);
        return await ReadAsync(target, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserSignatureDto> RetireAsync(Guid? userId, string? reason, CancellationToken cancellationToken)
    {
        var target = Target(userId);
        var actor = _scope.UserId!.Value;
        var current = await _db.UserSignatures.SingleOrDefaultAsync(s => s.UserId == target && s.ValidTo == null, cancellationToken)
                      ?? throw new BusinessRuleException("Chưa có chữ ký đang dùng.");
        current.Retire(_clock.UtcNow, actor, reason, replaced: false);
        if (target != actor) Notify(target, "Chữ ký của bạn đã ngừng dùng", $"Người quản trị đã ngừng dùng chữ ký của bạn. Lý do: {current.RetireReason}");
        await SaveAsync(cancellationToken);
        return await ReadAsync(target, cancellationToken);
    }

    /// <summary>Người dùng đích; người khác thì phải có quyền quản trị chữ ký.</summary>
    private Guid Target(Guid? userId)
    {
        var me = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        if (userId is null || userId == me) return me;
        _scope.EnsurePermission(Permissions.System.UserSignature);
        return userId.Value;
    }

    private async Task EnsureUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken)) throw new NotFoundException("Không tìm thấy người dùng.");
    }

    /// <summary>Báo người được làm thay (tại công ty đang làm việc của người quản trị) – mở màn hình Chữ ký của tôi (CKY).</summary>
    private void Notify(Guid userId, string title, string message) =>
        _db.Notifications.Add(Notification.Create(
            _scope.RequireCompany(), userId, NotificationTypes.SignatureChanged, title, message, "CKY", null, _clock.UtcNow));

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Hai lần tải cùng lúc đụng chỉ mục "một chữ ký đang dùng" / số phiên bản – người sau tải lại.
            throw new BusinessRuleException("Chữ ký vừa được người khác thay đổi, hãy tải lại màn hình rồi làm lại.");
        }
    }

    private async Task<UserSignatureDto> ReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking().Where(u => u.Id == userId)
                       .Select(u => new { u.Id, u.UserName, u.FullName, u.Honorific })
                       .SingleOrDefaultAsync(cancellationToken)
                   ?? throw new NotFoundException("Không tìm thấy người dùng.");
        var versions = await _db.UserSignatures.AsNoTracking().Where(s => s.UserId == userId).OrderByDescending(s => s.VersionNo)
            .Select(s => new { s.VersionNo, s.ValidFrom, s.ValidTo, s.UploadedBy, s.ByAdmin, s.Reason, s.RetiredBy, s.RetireReason, s.Width, s.Height })
            .ToListAsync(cancellationToken);
        var peopleIds = versions.Select(v => v.UploadedBy).Concat(versions.Select(v => v.RetiredBy).OfType<Guid>()).Distinct().ToList();
        var people = await _db.Users.AsNoTracking().Where(u => peopleIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
        return new UserSignatureDto(
            user.Id, user.UserName ?? "", user.FullName, user.Honorific, user.Id == _scope.UserId,
            versions.FirstOrDefault(v => v.ValidTo is null)?.VersionNo,
            versions.Select(v => new SignatureVersionDto(
                v.VersionNo, v.ValidFrom, v.ValidTo, people.GetValueOrDefault(v.UploadedBy), v.ByAdmin, v.Reason,
                v.RetiredBy is { } r ? people.GetValueOrDefault(r) : null, v.RetireReason, v.Width, v.Height)).ToList());
    }
}
