using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Notifications;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Tìm "người duyệt tiếp theo" của các bước đang chờ (yêu cầu sau test 0.14.0 – TC-4, design.md mục 18.22): người dùng còn hoạt động có quyền
/// duyệt loại phiếu tại công ty của bước, có vai trò của cấp (nếu cấp gắn vai trò), không phải người lập và không vướng kê khai xung đột lợi
/// ích còn hiệu lực với NCC của phiếu. Cùng điều kiện với <see cref="ApprovalEngine.ApproveBlockerAsync"/> và với danh sách người nhận
/// thông báo chờ duyệt – dùng chung một chỗ để ba nơi không lệch nhau.
/// </summary>
internal sealed class NextApproverResolver
{
    private readonly ProcDbContext _db;
    private readonly IClock _clock;
    private readonly NotificationPublisher _publisher;

    /// <summary>Tạo bộ tìm người duyệt.</summary>
    public NextApproverResolver(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _clock = clock;
        _publisher = new NotificationPublisher(db, scope, clock);
    }

    /// <summary>Một bước đang chờ cần tìm người duyệt.</summary>
    /// <param name="Key">Khóa trả kết quả (thường là Id bước duyệt).</param>
    /// <param name="DocType">Loại phiếu duyệt (quyết định quyền duyệt).</param>
    /// <param name="CompanyId">Công ty duyệt bước này.</param>
    /// <param name="RoleId">Vai trò bắt buộc của cấp; null = ai có quyền duyệt.</param>
    /// <param name="IsPreparer">Người lập (và người tương đương – RFQ: người chọn thầu) không tự duyệt.</param>
    /// <param name="SupplierIds">NCC của phiếu để loại người có kê khai xung đột lợi ích.</param>
    public sealed record PendingStep(
        Guid Key, string DocType, Guid CompanyId, Guid? RoleId, Func<Guid, bool> IsPreparer, IReadOnlyCollection<Guid> SupplierIds);

    /// <summary>Id những người duyệt được từng bước.</summary>
    /// <param name="steps">Các bước đang chờ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo <see cref="PendingStep.Key"/>; bước không ai duyệt được thì danh sách rỗng.</returns>
    public async Task<Dictionary<Guid, List<Guid>>> CandidateIdsAsync(IReadOnlyCollection<PendingStep> steps, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, List<Guid>>();
        if (steps.Count == 0) return result;

        // Nhiều phiếu chung công ty – loại – vai trò (tra cứu chứng từ) chỉ hỏi CSDL một lần cho mỗi bộ.
        var byRule = new Dictionary<(Guid, string, Guid?), List<Guid>>();
        foreach (var step in steps)
        {
            var permission = ApprovalEngine.ApprovePermission(step.DocType);
            if (!byRule.ContainsKey((step.CompanyId, permission, step.RoleId)))
                byRule[(step.CompanyId, permission, step.RoleId)] =
                    await _publisher.UsersWithPermissionAsync(step.CompanyId, permission, step.RoleId, null, cancellationToken);
        }

        var userIds = byRule.Values.SelectMany(u => u).Distinct().ToList();
        var supplierIds = steps.SelectMany(s => s.SupplierIds).Distinct().ToList();
        var today = _clock.TodayInVietnam();
        // Kê khai xung đột lợi ích dùng chung mọi công ty (gắn với con người) – design.md mục 18.8.
        var conflicts = userIds.Count == 0 || supplierIds.Count == 0
            ? []
            : (await _db.ConflictDeclarations.AsNoTracking()
                .Where(d => userIds.Contains(d.UserId) && supplierIds.Contains(d.SupplierId) && d.ValidFrom <= today && (d.ValidTo == null || d.ValidTo >= today))
                .Select(d => new { d.UserId, d.SupplierId })
                .ToListAsync(cancellationToken))
                .Select(d => (d.UserId, d.SupplierId)).ToHashSet();

        foreach (var step in steps)
        {
            var candidates = byRule[(step.CompanyId, ApprovalEngine.ApprovePermission(step.DocType), step.RoleId)];
            result[step.Key] = candidates
                .Where(u => !step.IsPreparer(u) && !step.SupplierIds.Any(s => conflicts.Contains((u, s))))
                .ToList();
        }

        return result;
    }

    /// <summary>Họ tên những người duyệt được từng bước, theo thứ tự chữ cái.</summary>
    /// <param name="steps">Các bước đang chờ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo <see cref="PendingStep.Key"/>; bước không ai duyệt được thì danh sách rỗng (giao diện báo chưa có người đủ quyền).</returns>
    public async Task<Dictionary<Guid, IReadOnlyList<string>>> NamesAsync(IReadOnlyCollection<PendingStep> steps, CancellationToken cancellationToken)
    {
        var ids = await CandidateIdsAsync(steps, cancellationToken);
        var all = ids.Values.SelectMany(u => u).Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => all.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.UserName })
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(u.FullName) ? u.UserName ?? string.Empty : u.FullName, cancellationToken);
        return ids.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value.Select(u => names.GetValueOrDefault(u, string.Empty)).Order(StringComparer.CurrentCulture).ToList());
    }
}
