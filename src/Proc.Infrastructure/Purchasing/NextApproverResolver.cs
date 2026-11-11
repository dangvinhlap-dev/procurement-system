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
/// thông báo chờ duyệt – dùng chung một chỗ để ba nơi không lệch nhau. Từ lát O2 gồm cả người được ủy quyền duyệt thay.
/// </summary>
internal sealed class NextApproverResolver
{
    private readonly ProcDbContext _db;
    private readonly IClock _clock;
    private readonly NotificationPublisher _publisher;
    private readonly DelegationLookup _delegations;

    /// <summary>Tạo bộ tìm người duyệt.</summary>
    public NextApproverResolver(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _clock = clock;
        _publisher = new NotificationPublisher(db, scope, clock);
        _delegations = new DelegationLookup(db, clock);
    }

    /// <summary>Một bước đang chờ cần tìm người duyệt.</summary>
    /// <param name="Key">Khóa trả kết quả (thường là Id bước duyệt).</param>
    /// <param name="DocType">Loại phiếu duyệt (quyết định quyền duyệt).</param>
    /// <param name="CompanyId">Công ty duyệt bước này.</param>
    /// <param name="RoleId">Vai trò bắt buộc của cấp; null = ai có quyền duyệt.</param>
    /// <param name="IsPreparer">Người lập (và người tương đương – RFQ: người chọn thầu) không tự duyệt.</param>
    /// <param name="SupplierIds">NCC của phiếu để loại người có kê khai xung đột lợi ích.</param>
    /// <param name="OnlyUsers">
    /// Bước người duyệt động (lát O1): chỉ người này (Trưởng phòng ban / GĐ khối hiện hành); rỗng = chưa khai báo, không ai duyệt được;
    /// null = bước theo vai trò.
    /// </param>
    /// <param name="HasActed">
    /// Người đã duyệt một cấp trong lượt này (tự duyệt hoặc được duyệt thay) – không duyệt thêm cấp khác (lát R3); null = chưa ai duyệt.
    /// </param>
    /// <param name="Permission">Quyền cần có để duyệt bước; null = quyền duyệt loại phiếu. Bước Thủ kho: quyền nhập tồn (lát Y3c).</param>
    /// <param name="WarehouseId">Bước Thủ kho nơi nhận: người duyệt phải có phạm vi kho chứa kho này; null = không xét phạm vi kho.</param>
    /// <param name="AllowDelegation">Có xét ủy quyền duyệt thay không (bước Thủ kho không – thủ kho khác cùng kho làm thay được).</param>
    public sealed record PendingStep(
        Guid Key, string DocType, Guid CompanyId, Guid? RoleId, Func<Guid, bool> IsPreparer, IReadOnlyCollection<Guid> SupplierIds,
        IReadOnlyCollection<Guid>? OnlyUsers = null, Func<Guid, bool>? HasActed = null, string? Permission = null, Guid? WarehouseId = null,
        bool AllowDelegation = true)
    {
        /// <summary>
        /// Bước đang chờ từ bước duyệt đã lưu: lấy quyền, nơi nhận, cho phép ủy quyền theo loại người duyệt của bước (lát Y3c).
        /// </summary>
        /// <param name="step">Bước duyệt.</param>
        /// <param name="isPreparer">Người lập (không tự duyệt).</param>
        /// <param name="supplierIds">NCC của phiếu.</param>
        /// <param name="onlyUsers">Người duyệt động (Trưởng phòng ban / GĐ khối) hoặc null.</param>
        /// <param name="hasActed">Người đã duyệt một cấp trong lượt.</param>
        /// <returns>Bước đang chờ.</returns>
        public static PendingStep Of(
            ApprovalStep step, Func<Guid, bool> isPreparer, IReadOnlyCollection<Guid> supplierIds, IReadOnlyCollection<Guid>? onlyUsers,
            Func<Guid, bool>? hasActed) =>
            new(step.Id, step.DocType, step.CompanyId, step.RoleId, isPreparer, supplierIds, onlyUsers, hasActed,
                ApprovalEngine.StepPermission(step), step.Kind == ApproverKind.Storekeeper ? step.WarehouseId : null,
                step.Kind != ApproverKind.Storekeeper);
    }

    /// <summary>Một người duyệt được bước.</summary>
    /// <param name="UserId">Người duyệt.</param>
    /// <param name="OnBehalfOf">Người ủy quyền khi duyệt thay (lát O2); null = tự giữ cấp này.</param>
    public sealed record Candidate(Guid UserId, Guid? OnBehalfOf);

    /// <summary>Id những người duyệt được từng bước (kể cả người được ủy quyền duyệt thay).</summary>
    /// <param name="steps">Các bước đang chờ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo <see cref="PendingStep.Key"/>; bước không ai duyệt được thì danh sách rỗng.</returns>
    public async Task<Dictionary<Guid, List<Guid>>> CandidateIdsAsync(IReadOnlyCollection<PendingStep> steps, CancellationToken cancellationToken) =>
        (await CandidatesAsync(steps, cancellationToken)).ToDictionary(kv => kv.Key, kv => kv.Value.Select(c => c.UserId).Distinct().ToList());

    /// <summary>
    /// Những người duyệt được từng bước. Người giữ cấp: có quyền duyệt + vai trò của cấp (hoặc đúng người duyệt động), không phải người lập, chưa
    /// duyệt cấp khác trong lượt, không vướng xung đột lợi ích. Người được ủy quyền (lát O2): có ủy quyền đang tác dụng của một người giữ cấp
    /// (người ủy quyền không vướng xung đột, chưa duyệt cấp khác – được là người lập), bản thân có quyền duyệt loại phiếu, không phải người lập,
    /// chưa duyệt cấp khác, không vướng xung đột. Một người vừa giữ cấp vừa được ủy quyền thì chỉ tính là người giữ cấp.
    /// </summary>
    /// <param name="steps">Các bước đang chờ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo <see cref="PendingStep.Key"/>.</returns>
    public async Task<Dictionary<Guid, List<Candidate>>> CandidatesAsync(IReadOnlyCollection<PendingStep> steps, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, List<Candidate>>();
        if (steps.Count == 0) return result;

        // Nhiều phiếu chung công ty – loại – vai trò (tra cứu chứng từ) chỉ hỏi CSDL một lần cho mỗi bộ; vai trò null = mọi người có quyền duyệt
        // (dùng cho người được ủy quyền). Bước Thủ kho nơi nhận (lát Y3c) thêm điều kiện phạm vi kho.
        var byRule = new Dictionary<(Guid, string, Guid?, Guid?), List<Guid>>();
        async Task<List<Guid>> UsersAsync(Guid companyId, string permission, Guid? roleId, Guid? warehouseId = null)
        {
            if (!byRule.TryGetValue((companyId, permission, roleId, warehouseId), out var users))
                byRule[(companyId, permission, roleId, warehouseId)] = users =
                    await _publisher.UsersWithPermissionAsync(companyId, permission, roleId, warehouseId, cancellationToken);
            return users;
        }

        var holders = new Dictionary<Guid, List<Guid>>();
        foreach (var step in steps)
        {
            var hasActed = step.HasActed ?? (_ => false);
            holders[step.Key] = (await UsersAsync(step.CompanyId, step.Permission ?? ApprovalEngine.ApprovePermission(step.DocType), step.RoleId, step.WarehouseId))
                .Where(u => (step.OnlyUsers is null || step.OnlyUsers.Contains(u)) && !hasActed(u))
                .ToList();
        }

        var delegations = await _delegations.ActiveAsync(
            steps.Select(s => s.CompanyId).Distinct().ToList(), holders.Values.SelectMany(u => u).Distinct().ToList(), null, cancellationToken);
        var userIds = holders.Values.SelectMany(u => u).Concat(delegations.Select(d => d.DelegateUserId)).Distinct().ToList();
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
            var hasActed = step.HasActed ?? (_ => false);
            bool Free(Guid u) => !step.SupplierIds.Any(s => conflicts.Contains((u, s)));
            var stepHolders = holders[step.Key].Where(Free).ToList();
            var candidates = stepHolders.Where(u => !step.IsPreparer(u)).Select(u => new Candidate(u, null)).ToList();
            var covering = step.AllowDelegation
                ? delegations.Where(d => d.Covers(step.CompanyId, step.DocType) && stepHolders.Contains(d.DelegatorUserId)).ToList()
                : [];
            if (covering.Count > 0)
            {
                // Người được ủy quyền vẫn phải có quyền duyệt loại phiếu (API duyệt đòi quyền này) – ủy quyền chỉ cho mượn "vị trí" của cấp.
                var approvers = await UsersAsync(step.CompanyId, ApprovalEngine.ApprovePermission(step.DocType), null);
                foreach (var d in covering)
                {
                    var u = d.DelegateUserId;
                    if (candidates.Any(c => c.UserId == u) || !approvers.Contains(u) || step.IsPreparer(u) || hasActed(u) || !Free(u)) continue;
                    candidates.Add(new Candidate(u, d.DelegatorUserId));
                }
            }

            result[step.Key] = candidates;
        }

        return result;
    }

    /// <summary>Họ tên những người duyệt được từng bước, theo thứ tự chữ cái; người được ủy quyền hiện "B (thay A)".</summary>
    /// <param name="steps">Các bước đang chờ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo <see cref="PendingStep.Key"/>; bước không ai duyệt được thì danh sách rỗng (giao diện báo chưa có người đủ quyền).</returns>
    public async Task<Dictionary<Guid, IReadOnlyList<string>>> NamesAsync(IReadOnlyCollection<PendingStep> steps, CancellationToken cancellationToken)
    {
        var candidates = await CandidatesAsync(steps, cancellationToken);
        var all = candidates.Values.SelectMany(c => c.Select(x => x.UserId).Concat(c.Select(x => x.OnBehalfOf).OfType<Guid>())).Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => all.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.UserName })
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(u.FullName) ? u.UserName ?? string.Empty : u.FullName, cancellationToken);
        return candidates.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value
                .GroupBy(c => c.UserId)
                .Select(g => g.First())
                .Select(c => c.OnBehalfOf is { } d
                    ? $"{names.GetValueOrDefault(c.UserId, string.Empty)} (thay {names.GetValueOrDefault(d, string.Empty)})"
                    : names.GetValueOrDefault(c.UserId, string.Empty))
                .Order(StringComparer.CurrentCulture).ToList());
    }
}
