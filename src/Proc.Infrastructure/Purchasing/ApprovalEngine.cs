using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Notifications;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Notifications;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Cơ chế duyệt nhiều cấp dùng chung (design.md mục 6.2, 18.6) cho mọi loại trong <see cref="ApprovalTypes"/> – PR, PO, bảng giá,
/// kết quả chọn thầu, hồ sơ NCC, hợp đồng: gửi duyệt sinh các bước theo quy trình của công ty và giá trị phiếu; duyệt lần lượt cấp
/// thấp → cao; từ chối thì đối tượng về nháp. Không tự lưu – dịch vụ gọi lưu cùng thay đổi của đối tượng trong một lần SaveChanges.
/// </summary>
internal sealed class ApprovalEngine
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;
    private readonly NotificationPublisher _notifications;
    private readonly NextApproverResolver _approvers;
    private readonly DynamicApprovers _dynamic;
    private readonly DelegationLookup _delegations;

    /// <summary>
    /// Ủy quyền đang tác dụng cho người dùng hiện tại, theo công ty – nạp một lần mỗi request vì Việc cần làm kiểm tra nhiều phiếu liên tiếp.
    /// </summary>
    private readonly Dictionary<Guid, List<DelegationLookup.Active>> _myDelegations = [];

    /// <summary>Tạo cơ chế duyệt.</summary>
    public ApprovalEngine(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
        _notifications = new NotificationPublisher(db, scope, clock);
        _approvers = new NextApproverResolver(db, scope, clock);
        _dynamic = new DynamicApprovers(db);
        _delegations = new DelegationLookup(db, clock);
    }

    /// <summary>Quyền duyệt theo loại phiếu.</summary>
    /// <exception cref="BusinessRuleException">Khi loại không có duyệt.</exception>
    public static string ApprovePermission(string docType) => ApprovalTypes.Get(docType).ApprovePermission;

    /// <summary>
    /// Gửi duyệt: phiếu sang Chờ duyệt, sinh các bước của lượt mới theo quy trình của công ty phiếu (design.md mục 6.2). Dưới mọi
    /// ngưỡng đã cấu hình thì phiếu được duyệt ngay.
    /// </summary>
    /// <param name="document">Đối tượng duyệt.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <param name="ruleType">Bộ cấp duyệt thay thế (ví dụ POK – PO khẩn cấp, design.md mục 18.11); chưa cấu hình thì dùng cấp của loại phiếu.</param>
    public async Task SubmitAsync(IApprovalSubject document, CancellationToken cancellationToken, string? ruleType = null)
    {
        var round = document.Submit();
        var rules = await BaseRulesAsync(document, ruleType, cancellationToken);
        var steps = Steps(document, rules, ApprovalPlan.Levels(rules, document.ApprovalAmount), round, stage: 1).ToList();
        await EnsureDynamicApproversAsync(document, steps, cancellationToken);
        await StartAsync(document, steps, cancellationToken);
    }

    /// <summary>
    /// Sinh bước của các cấp đã chọn tại công ty của phiếu, mang theo loại người duyệt của cấp (lát O1). Cấp người duyệt động ghi bộ phận của
    /// phiếu; phiếu không có bộ phận thì không gửi được (không ai làm Trưởng phòng ban / GĐ khối của phiếu).
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi có cấp người duyệt động mà phiếu chưa có bộ phận.</exception>
    private static IEnumerable<ApprovalStep> Steps(
        IApprovalSubject document, IReadOnlyList<ApprovalRule> rules, IEnumerable<(int Level, Guid? RoleId)> levels, int round, int stage)
    {
        foreach (var (level, roleId) in levels)
        {
            var kind = rules.FirstOrDefault(r => r.Level == level)?.Kind ?? ApproverKind.Role;
            if (kind != ApproverKind.Role && document.ApprovalDepartmentId is null)
                throw new BusinessRuleException(
                    $"Cấp {level} của quy trình duyệt do {DynamicApprovers.KindLabel(kind)} duyệt – hãy chọn bộ phận cho phiếu {document.No} rồi gửi lại.");
            yield return ApprovalStep.Create(
                document.CompanyId, document.DocType, document.Id, round, level, roleId, stage, kind, kind == ApproverKind.Role ? null : document.ApprovalDepartmentId);
        }
    }

    /// <summary>
    /// Chặn gửi duyệt khi cấp người duyệt động chưa có người duyệt được (lát O1, design.md mục 19.3 – "phòng ban chưa khai trưởng / GĐ khối thì
    /// chặn gửi kèm thông báo rõ"): bộ phận chưa khai Trưởng phòng ban, chưa gán khối, khối chưa khai GĐ khối, tài khoản đã ngừng dùng, chưa có
    /// quyền duyệt loại phiếu tại công ty; người đó là người lập (không tự duyệt); hoặc một người giữ hai cấp động (mỗi người một cấp – lát R3).
    /// Kiểm tra trước khi lưu nên phiếu vẫn ở nháp.
    /// </summary>
    /// <exception cref="BusinessRuleException">Lý do cụ thể, chỉ chỗ cần sửa.</exception>
    private async Task EnsureDynamicApproversAsync(IApprovalSubject document, IReadOnlyList<ApprovalStep> steps, CancellationToken cancellationToken)
    {
        var resolved = await _dynamic.ResolveAsync(steps, cancellationToken);
        if (resolved.Count == 0) return;
        var approvers = await _notifications.UsersWithPermissionAsync(document.CompanyId, ApprovePermission(document.DocType), null, null, cancellationToken);
        var seen = new Dictionary<Guid, string>();
        foreach (var step in steps.Where(DynamicApprovers.IsDynamic).OrderBy(s => s.Stage).ThenBy(s => s.Level))
        {
            var r = resolved[step.Id];
            if (r.UserId is not { } userId) throw new BusinessRuleException($"Không gửi duyệt được – cấp {step.Level} ({r.Label}): {r.Problem}");
            // Người lập chính là Trưởng phòng ban / GĐ khối: gửi được khi người đó đang ủy quyền cho người khác duyệt thay (lát O2) – người
            // duyệt thật là người được ủy quyền, vẫn tách khỏi người lập.
            if (document.IsPreparedBy(userId) && !await HasUsableDelegateAsync(document, step, userId, cancellationToken))
                throw new BusinessRuleException(
                    $"Cấp {step.Level} do {r.Label} ({r.UserName}) duyệt nhưng đó là người lập phiếu – người lập không tự duyệt. Nhờ người khác lập phiếu, đổi {r.Label} hoặc lập ủy quyền duyệt (Việc hằng ngày → Ủy quyền duyệt).");
            if (!approvers.Contains(userId))
                throw new BusinessRuleException(
                    $"{r.Label} ({r.UserName}) chưa có quyền duyệt {Noun(document).ToLowerInvariant()} tại công ty – hãy phân quyền trước khi gửi duyệt.");
            if (seen.TryGetValue(userId, out var other))
                throw new BusinessRuleException(
                    $"{r.UserName} vừa là {other} vừa là {r.Label} – mỗi cấp phải do một người khác duyệt; sửa danh mục bộ phận/khối hoặc quy trình duyệt.");
            seen[userId] = r.Label;
        }
    }

    /// <summary>
    /// Người giữ cấp đang có ủy quyền (hôm nay, đúng loại phiếu, tại công ty của bước) cho một người khác có quyền duyệt loại phiếu và không
    /// phải người lập – dùng khi người giữ cấp động lại là người lập phiếu (lát O2).
    /// </summary>
    private async Task<bool> HasUsableDelegateAsync(IApprovalSubject document, ApprovalStep step, Guid holderId, CancellationToken cancellationToken)
    {
        var delegates = (await _delegations.ActiveAsync([step.CompanyId], [holderId], null, cancellationToken))
            .Where(d => d.Covers(step.CompanyId, step.DocType) && !document.IsPreparedBy(d.DelegateUserId))
            .Select(d => d.DelegateUserId).ToList();
        if (delegates.Count == 0) return false;
        var approvers = await _notifications.UsersWithPermissionAsync(step.CompanyId, ApprovePermission(document.DocType), null, null, cancellationToken);
        return delegates.Any(approvers.Contains);
    }

    /// <summary>
    /// Gửi duyệt có thêm chặng phụ sau chặng thường: chặng 1 các cấp thường của loại phiếu (có thể không có cấp nào nếu dưới mọi
    /// ngưỡng), rồi lần lượt từng chặng phụ theo bộ cấp của loại phụ tại cùng công ty – mỗi chặng phụ luôn ít nhất một cấp
    /// (<see cref="ApprovalPlan.ExceptionLevels"/>). Dùng cho PO lệch giá đã duyệt (POX, chặng 2 – design.md mục 18.10) và PR/PO vượt
    /// ngân sách (PRB/POB, chặng 3 – mục 18.24). Bước chặng phụ vẫn mang loại phiếu gốc nên người duyệt cần quyền duyệt loại phiếu đó.
    /// Không có chặng phụ nào thì như <see cref="SubmitAsync"/>.
    /// </summary>
    /// <param name="document">Đối tượng duyệt.</param>
    /// <param name="extraStages">Các chặng phụ: mã loại trong <see cref="ApprovalTypes"/> và số chặng (tăng dần).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <param name="ruleType">Bộ cấp duyệt thay thế cho chặng 1 (như <see cref="SubmitAsync"/>).</param>
    /// <param name="skipPlannedRoleTypes">Loại chặng phụ chỉ đòi "phải lên đúng vai trò này": bỏ cấp có vai trò đã có ở các chặng trước
    /// (<see cref="ApprovalPlan.WithoutPlannedRoles"/>) – PO vượt giá trần (POC, lát H3); chặng có thể không còn cấp nào.</param>
    public async Task SubmitWithStagesAsync(
        IApprovalSubject document, IReadOnlyList<(string Type, int Stage)> extraStages, CancellationToken cancellationToken, string? ruleType = null,
        IReadOnlyCollection<string>? skipPlannedRoleTypes = null)
    {
        var round = document.Submit();
        var baseRules = await BaseRulesAsync(document, ruleType, cancellationToken);
        var steps = Steps(document, baseRules, ApprovalPlan.Levels(baseRules, document.ApprovalAmount), round, stage: 1).ToList();
        foreach (var (type, stage) in extraStages.OrderBy(x => x.Stage))
        {
            var rules = await _db.ApprovalRules.AsNoTracking().Where(r => r.DocType == type).ToListAsync(cancellationToken);
            var levels = ApprovalPlan.ExceptionLevels(rules, document.ApprovalAmount);
            if (skipPlannedRoleTypes?.Contains(type) == true)
                levels = ApprovalPlan.WithoutPlannedRoles(levels, steps.Select(s => s.RoleId).ToList());
            steps.AddRange(Steps(document, rules, levels, round, stage));
        }

        await EnsureDynamicApproversAsync(document, steps, cancellationToken);
        await StartAsync(document, steps, cancellationToken);
    }

    /// <summary>
    /// Cấp duyệt chặng thường: bộ cấp thay thế nếu công ty đã cấu hình (ví dụ PO khẩn cấp – POK), không thì cấp của loại phiếu. Không cấu
    /// hình bộ thay thế thì không làm PO khẩn thoát duyệt – vẫn qua quy trình PO thường.
    /// </summary>
    private async Task<List<ApprovalRule>> BaseRulesAsync(IApprovalSubject document, string? ruleType, CancellationToken cancellationToken)
    {
        if (ruleType is not null)
        {
            var alternative = await _db.ApprovalRules.AsNoTracking().Where(r => r.DocType == ruleType).ToListAsync(cancellationToken);
            if (alternative.Count > 0) return alternative;
        }

        return await _db.ApprovalRules.AsNoTracking().Where(r => r.DocType == document.DocType).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gửi duyệt hai chặng (design.md mục 18.6 – duyệt NCC): chặng 1 mỗi công ty liên quan duyệt theo quy trình của công ty đó (các công
    /// ty song song), chặng 2 Tổng công ty duyệt lần cuối theo quy trình của Tổng công ty. Tổng công ty có trong danh sách liên quan thì
    /// chỉ duyệt ở chặng 2 (không duyệt hai lần). Công ty chưa cấu hình quy trình loại này → một cấp, ai có quyền duyệt tại đó cũng duyệt.
    /// </summary>
    /// <param name="document">Đối tượng duyệt.</param>
    /// <param name="relatedCompanyIds">Công ty liên quan (chặng 1), chốt lúc gửi.</param>
    /// <param name="headOfficeId">Tổng công ty (chặng 2).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    public async Task SubmitStagedAsync(IApprovalSubject document, IReadOnlyCollection<Guid> relatedCompanyIds, Guid headOfficeId, CancellationToken cancellationToken)
    {
        var round = document.Submit();
        var companies = relatedCompanyIds.Where(c => c != headOfficeId).Distinct().Append(headOfficeId).ToList();
        // Đọc quy trình duyệt của nhiều công ty cùng lúc: chỉ bỏ filter công ty, giới hạn đúng các công ty liên quan + loại phiếu.
        var rules = await _db.ApprovalRules.AsNoTracking().IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(r => r.DocType == document.DocType && companies.Contains(r.CompanyId))
            .ToListAsync(cancellationToken);
        var steps = companies
            .SelectMany(c => ApprovalPlan.Levels(rules.Where(r => r.CompanyId == c).ToList(), document.ApprovalAmount)
                .Select(l => ApprovalStep.Create(c, document.DocType, document.Id, round, l.Level, l.RoleId, c == headOfficeId ? 2 : 1)))
            .ToList();
        await StartAsync(document, steps, cancellationToken);
    }

    /// <summary>Bắt đầu quy trình duyệt: không có bước nào thì duyệt ngay, có thì báo người duyệt cấp đầu.</summary>
    private async Task StartAsync(IApprovalSubject document, List<ApprovalStep> steps, CancellationToken cancellationToken)
    {
        if (steps.Count == 0)
        {
            document.MarkApproved(_clock.UtcNow);
            return;
        }

        // Người và thời điểm gửi – lịch sử duyệt hiện "Lượt n: X gửi duyệt lúc …" (yêu cầu người dùng 03/11/2026).
        foreach (var step in steps) step.MarkSubmitted(_clock.UtcNow, _scope.UserId);
        _db.ApprovalSteps.AddRange(steps);
        foreach (var step in ApprovalProgress.Active(steps)) await NotifyApproversAsync(document, step, cancellationToken);
    }

    /// <summary>
    /// Các bước của lượt hiện tại, ở mọi công ty. Bỏ filter công ty vì duyệt nhiều công ty (mục 18.6) cần biết các công ty khác đã duyệt
    /// chưa; an toàn vì đối tượng đã được nạp qua phạm vi người dùng và chỉ lấy đúng bước của đối tượng đó.
    /// </summary>
    private Task<List<ApprovalStep>> RoundStepsAsync(IApprovalSubject document, CancellationToken cancellationToken) =>
        _db.ApprovalSteps.IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(s => s.DocType == document.DocType && s.DocumentId == document.Id && s.Round == document.ApprovalRound)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Bước đang đến lượt tại công ty người dùng đang làm việc (cấp thấp nhất còn chờ của công ty đó trong chặng đang chạy), hoặc null.
    /// Công ty nào duyệt do phân quyền: người duyệt làm việc ở công ty của bước và có quyền duyệt tại đó.
    /// </summary>
    public async Task<ApprovalStep?> CurrentStepAsync(IApprovalSubject document, CancellationToken cancellationToken)
    {
        var companyId = _scope.CompanyId;
        return ApprovalProgress.Active(await RoundStepsAsync(document, cancellationToken)).FirstOrDefault(s => s.CompanyId == companyId);
    }

    /// <summary>
    /// Lý do người dùng hiện tại chưa duyệt được phiếu (null = duyệt được): phiếu phải chờ duyệt và đến lượt công ty đang làm việc; người
    /// lập không tự duyệt; phải có quyền duyệt loại phiếu tại công ty; cấp có vai trò thì người duyệt phải được gán vai trò đó tại công ty.
    /// </summary>
    public async Task<string?> ApproveBlockerAsync(IApprovalSubject document, ApprovalStep? step, CancellationToken cancellationToken) =>
        (await ActorAsync(document, step, cancellationToken)).Blocker;

    /// <summary>
    /// Người dùng hiện tại có duyệt/từ chối được bước không, và duyệt với tư cách nào. Điều kiện của chính người thao tác: phiếu đang chờ và đến
    /// lượt công ty đang làm việc; không phải người lập; có quyền duyệt loại phiếu; không vướng xung đột lợi ích; chưa duyệt cấp khác trong lượt
    /// (lát R3). Rồi đến "vị trí" của cấp (đúng Trưởng phòng ban / GĐ khối của phiếu, có vai trò của cấp): tự giữ thì duyệt bằng tư cách của mình;
    /// không thì xét ủy quyền đang tác dụng (lát O2) – người ủy quyền phải giữ vị trí đó, có quyền duyệt tại công ty, không vướng xung đột, chưa
    /// duyệt cấp khác trong lượt (người ủy quyền được là người lập: người duyệt thật vẫn khác người lập).
    /// Ví dụ cấp 2 do Trưởng phòng ban PX1 (An) duyệt, An ủy quyền cho Bình 12–20/11: ngày 15/11 Bình duyệt được, bước ghi "Bình (thay An)".
    /// </summary>
    /// <returns>(lý do không duyệt được hoặc null, người ủy quyền khi duyệt thay).</returns>
    private async Task<(string? Blocker, Guid? OnBehalfOf)> ActorAsync(IApprovalSubject document, ApprovalStep? step, CancellationToken cancellationToken)
    {
        if (!document.IsAwaitingApproval) return ($"Phiếu {document.No} không ở trạng thái chờ duyệt.", null);
        if (step is null) return ($"Phiếu {document.No} chưa đến lượt công ty bạn đang làm việc duyệt.", null);
        if (_scope.UserId is not { } userId) return ("Chưa đăng nhập.", null);
        // Tách người lập và người duyệt – nguyên tắc kiểm soát nội bộ.
        if (document.IsPreparedBy(userId)) return ("Người lập không tự duyệt phiếu của mình.", null);
        if (!_scope.HasPermission(ApprovePermission(document.DocType))) return ("Bạn không có quyền duyệt loại phiếu này.", null);
        var supplierIds = await SupplierIdsAsync(document, cancellationToken);
        if (await ConflictBlockerAsync(userId, supplierIds, cancellationToken) is { } conflict) return (conflict, null);

        var own = await PositionBlockerAsync(step, userId, own: true, cancellationToken);

        // Mỗi cấp một người khác nhau trong cùng lượt duyệt: một người có quyền nhiều cấp mà duyệt liên tiếp thì duyệt nhiều cấp mất tác
        // dụng (lát R3, design.md mục 18.26). Gửi lại sau khi bị từ chối là lượt mới – người đó lại duyệt được một cấp. Duyệt thay (lát O2)
        // tính cho cả người duyệt lẫn người được duyệt thay. Không giữ vị trí thì báo lý do vị trí trước (như trước lát O2).
        var round = await RoundStepsAsync(document, cancellationToken);
        if (ActedLevel(round, userId) is { } level)
            return (own ?? $"Bạn đã duyệt cấp {level.Level} của phiếu {document.No} trong lượt này – cấp sau phải do người khác duyệt.", null);
        if (own is null) return (null, null);

        // Ủy quyền duyệt (lát O2): mượn vị trí của người ủy quyền.
        if (!_myDelegations.TryGetValue(step.CompanyId, out var mine))
            _myDelegations[step.CompanyId] = mine = await _delegations.ActiveAsync([step.CompanyId], null, userId, cancellationToken);
        var delegators = mine.Where(d => d.Covers(step.CompanyId, step.DocType) && ActedLevel(round, d.DelegatorUserId) is null)
            .Select(d => d.DelegatorUserId).Distinct().ToList();
        if (delegators.Count == 0) return (own, null);
        var approvers = await _notifications.UsersWithPermissionAsync(step.CompanyId, ApprovePermission(document.DocType), null, null, cancellationToken);
        foreach (var delegator in delegators.Where(approvers.Contains))
        {
            if (await PositionBlockerAsync(step, delegator, own: false, cancellationToken) is not null) continue;
            if (await ConflictBlockerAsync(delegator, supplierIds, cancellationToken) is not null) continue;
            return (null, delegator);
        }

        return (own, null);
    }

    /// <summary>Bước của lượt mà người này đã duyệt (tự duyệt hoặc được duyệt thay), cấp thấp nhất trước; null nếu chưa.</summary>
    private static ApprovalStep? ActedLevel(IEnumerable<ApprovalStep> round, Guid userId) =>
        round.Where(s => s.Status == ApprovalStepStatus.Approved && (s.ActedBy == userId || s.OnBehalfOf == userId))
            .OrderBy(s => s.Stage).ThenBy(s => s.Level).FirstOrDefault();

    /// <summary>
    /// Người này có giữ "vị trí" của cấp không: cấp người duyệt động (lát O1) – đúng Trưởng phòng ban / GĐ khối hiện hành của bộ phận trên
    /// phiếu; cấp có vai trò – được gán vai trò đó tại công ty của bước. Trả lý do (câu cho người đang thao tác khi <paramref name="own"/>) hoặc null.
    /// </summary>
    private async Task<string?> PositionBlockerAsync(ApprovalStep step, Guid userId, bool own, CancellationToken cancellationToken)
    {
        if (DynamicApprovers.IsDynamic(step))
        {
            var dynamic = (await _dynamic.ResolveAsync([step], cancellationToken))[step.Id];
            if (dynamic.UserId is null) return $"Cấp {step.Level} ({dynamic.Label}) chưa có người duyệt: {dynamic.Problem}";
            if (dynamic.UserId != userId) return $"Cấp {step.Level} do {dynamic.Label} ({dynamic.UserName}) duyệt.";
        }

        if (step.RoleId is { } roleId
            && !await _db.UserCompanyRoles.AnyAsync(u => u.UserId == userId && u.CompanyId == step.CompanyId && u.RoleId == roleId, cancellationToken))
        {
            var role = await _db.Roles.Where(r => r.Id == roleId).Select(r => r.Name).SingleOrDefaultAsync(cancellationToken);
            return own ? $"Cấp {step.Level} do vai trò {role} duyệt – bạn không có vai trò này tại công ty." : $"Cấp {step.Level} do vai trò {role} duyệt.";
        }

        return null;
    }

    /// <summary>
    /// Duyệt bước đến lượt của công ty đang làm việc; hết bước thì phiếu Đã duyệt; còn bước thì báo người duyệt các bước vừa đến lượt
    /// (cấp sau của công ty đó, hoặc chặng Tổng công ty khi mọi công ty liên quan đã xong).
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi người dùng không duyệt được (lý do cụ thể).</exception>
    public async Task ApproveAsync(IApprovalSubject document, string? comment, CancellationToken cancellationToken)
    {
        var steps = await RoundStepsAsync(document, cancellationToken);
        var before = ApprovalProgress.Active(steps);
        var step = before.FirstOrDefault(s => s.CompanyId == _scope.CompanyId);
        var (reason, onBehalfOf) = await ActorAsync(document, step, cancellationToken);
        if (reason is not null) throw new BusinessRuleException(reason);
        step!.Approve(_scope.UserId!.Value, _clock.UtcNow, comment, onBehalfOf);
        NotifyDelegator(document, step, onBehalfOf, "duyệt");
        if (steps.All(s => s.Status != ApprovalStepStatus.Pending))
        {
            document.MarkApproved(_clock.UtcNow);
            NotifyCreator(document, NotificationTypes.Approved, $"{Noun(document)} {document.No} đã được duyệt",
                $"{Noun(document)} {document.No} đã được duyệt xong, có thể xử lý bước tiếp theo.");
            return;
        }

        foreach (var next in ApprovalProgress.Active(steps).Where(s => !before.Contains(s)))
            await NotifyApproversAsync(document, next, cancellationToken);
    }

    /// <summary>
    /// Từ chối ở bước đến lượt: mọi bước còn chờ (ở mọi công ty) bỏ qua, phiếu về nháp kèm lý do (yêu cầu NCC: kết thúc ở Từ chối).
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi không duyệt được hoặc thiếu lý do.</exception>
    public async Task RejectAsync(IApprovalSubject document, string? comment, CancellationToken cancellationToken)
    {
        var step = await CurrentStepAsync(document, cancellationToken);
        var (reason, onBehalfOf) = await ActorAsync(document, step, cancellationToken);
        if (reason is not null) throw new BusinessRuleException(reason);
        step!.Reject(_scope.UserId!.Value, _clock.UtcNow, comment, onBehalfOf);
        NotifyDelegator(document, step, onBehalfOf, "từ chối");
        await SkipPendingAsync(document, cancellationToken);
        document.ReturnToDraft(comment);
        var after = document is ApprovableDocument ? " Phiếu đã về Nháp để sửa và gửi lại." : string.Empty;
        NotifyCreator(document, NotificationTypes.Rejected, $"{Noun(document)} {document.No} bị từ chối",
            $"{Noun(document)} {document.No} bị từ chối ở cấp {step.Level}: {comment}.{after}");
    }

    /// <summary>
    /// Báo người duyệt được cấp đang chờ (design.md mục 11.6): có quyền duyệt loại phiếu tại công ty; cấp có vai trò thì phải có vai trò
    /// đó – cùng điều kiện với <see cref="ApproveBlockerAsync"/>. Người lập không nhận (không tự duyệt được).
    /// </summary>
    private Task NotifyApproversAsync(IApprovalSubject document, ApprovalStep step, CancellationToken cancellationToken) =>
        NotifyCandidatesAsync(document, step, NotificationTypes.ApprovalPending, string.Empty, string.Empty, string.Empty, cancellationToken);

    /// <summary>
    /// Nhắc những người duyệt được một bước đã chờ quá 1 ngày làm việc (lát O2c, design.md mục 19.3) – cùng người nhận với thông báo chờ duyệt
    /// (tính lại lúc nhắc: ủy quyền mới, người mới được gán vai trò cũng nhận). Gọi từ dịch vụ nền nên không có người đang thao tác.
    /// Ví dụ: "Nhắc: Đề nghị mua hàng PR-0012 chờ bạn duyệt quá hạn" – "… đang chờ duyệt cấp 1 từ 09/11/2026 08:00, đã quá 1 ngày làm việc
    /// (nhắc lần 1)."
    /// </summary>
    /// <param name="document">Phiếu đang chờ duyệt.</param>
    /// <param name="step">Bước đang đến lượt.</param>
    /// <param name="waitingSince">Mốc bước bắt đầu đến lượt (<see cref="ApprovalOverdue.WaitingSince"/>).</param>
    /// <param name="sequence">Lần nhắc thứ mấy.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số người nhận (0 = không còn ai duyệt được bước).</returns>
    public Task<int> RemindOverdueAsync(
        IApprovalSubject document, ApprovalStep step, DateTimeOffset waitingSince, int sequence, CancellationToken cancellationToken)
    {
        var since = TimeZoneInfo.ConvertTime(waitingSince, VietnamTime.Zone).ToString("dd/MM/yyyy HH:mm", VietnameseCulture);
        return NotifyCandidatesAsync(document, step, NotificationTypes.ApprovalOverdue, "Nhắc: ", " quá hạn",
            $" từ {since}, đã quá 1 ngày làm việc (nhắc lần {sequence})", cancellationToken);
    }

    /// <summary>
    /// Gửi thông báo cho những người duyệt được bước. Người nhận = "người duyệt tiếp theo" (NextApproverResolver: quyền, vai trò, không phải người
    /// lập, không vướng xung đột lợi ích), gồm cả người được ủy quyền duyệt thay (lát O2) – câu báo ghi rõ duyệt thay ai.
    /// </summary>
    /// <param name="document">Phiếu.</param>
    /// <param name="step">Bước đang đến lượt.</param>
    /// <param name="type">Loại thông báo (chờ duyệt / nhắc quá hạn).</param>
    /// <param name="titlePrefix">Chữ trước tiêu đề ("Nhắc: ").</param>
    /// <param name="titleSuffix">Chữ sau tiêu đề (" quá hạn").</param>
    /// <param name="detail">Chữ chèn sau "đang chờ duyệt cấp n" trong nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số người nhận.</returns>
    private async Task<int> NotifyCandidatesAsync(
        IApprovalSubject document, ApprovalStep step, string type, string titlePrefix, string titleSuffix, string detail,
        CancellationToken cancellationToken)
    {
        var candidates = (await _approvers.CandidatesAsync([await PendingStepAsync(document, step, cancellationToken)], cancellationToken))[step.Id]
            .Where(c => document.CreatedBy is not null || c.UserId != _scope.UserId).ToList();
        var waiting = (ApprovalTypes.Get(document.DocType).AmountLabel is null
            ? $"{Noun(document)} {document.No} đang chờ duyệt cấp {step.Level}"
            : $"{Noun(document)} {document.No} trị giá {document.ApprovalAmount.ToString("N0", VietnameseCulture)} đ đang chờ duyệt cấp {step.Level}")
            + detail;
        _notifications.Add(step.CompanyId, candidates.Where(c => c.OnBehalfOf is null).Select(c => c.UserId), type,
            $"{titlePrefix}{Noun(document)} {document.No} chờ bạn duyệt{titleSuffix}", waiting + ".", document.DocType, document.Id);
        var delegated = candidates.Where(c => c.OnBehalfOf is not null).ToList();
        if (delegated.Count > 0)
        {
            var delegatorIds = delegated.Select(c => c.OnBehalfOf!.Value).Distinct().ToList();
            var names = await _db.Users.AsNoTracking().Where(u => delegatorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
            foreach (var c in delegated)
                _notifications.Add(step.CompanyId, [c.UserId], type,
                    $"{titlePrefix}{Noun(document)} {document.No} chờ bạn duyệt thay {names.GetValueOrDefault(c.OnBehalfOf!.Value)}{titleSuffix}",
                    $"{waiting} – bạn được {names.GetValueOrDefault(c.OnBehalfOf!.Value)} ủy quyền duyệt thay.", document.DocType, document.Id);
        }

        return candidates.Where(c => c.UserId != _scope.UserId).Select(c => c.UserId).Distinct().Count();
    }

    /// <summary>Báo người ủy quyền biết người được ủy quyền vừa duyệt/từ chối thay mình (lát O2).</summary>
    private void NotifyDelegator(IApprovalSubject document, ApprovalStep step, Guid? onBehalfOf, string action)
    {
        if (onBehalfOf is not { } delegator) return;
        _notifications.Add(step.CompanyId, [delegator], NotificationTypes.DelegatedAction,
            $"{Noun(document)} {document.No} đã được {action} thay bạn",
            $"Người được bạn ủy quyền đã {action} {Noun(document).ToLowerInvariant()} {document.No} ở cấp {step.Level} thay bạn.",
            document.DocType, document.Id);
    }

    /// <summary>
    /// Bước đang chờ kèm điều kiện loại người lập, người đã duyệt một cấp trong lượt này (lát R3) và NCC của phiếu, để tìm người duyệt.
    /// Bước của lượt đọc có theo dõi nên thấy cả bước vừa duyệt trong request mà chưa lưu.
    /// </summary>
    private async Task<NextApproverResolver.PendingStep> PendingStepAsync(IApprovalSubject document, ApprovalStep step, CancellationToken cancellationToken)
    {
        var approvedBy = ApprovedBy(await RoundStepsAsync(document, cancellationToken));
        return new(step.Id, document.DocType, step.CompanyId, step.RoleId, document.IsPreparedBy,
            await SupplierIdsAsync(document, cancellationToken), await OnlyUsersAsync(step, cancellationToken), approvedBy.Contains);
    }

    /// <summary>Người duyệt duy nhất của bước người duyệt động (rỗng khi chưa khai báo); null với bước theo vai trò.</summary>
    private async Task<IReadOnlyCollection<Guid>?> OnlyUsersAsync(ApprovalStep step, CancellationToken cancellationToken)
    {
        if (!DynamicApprovers.IsDynamic(step)) return null;
        return (await _dynamic.ResolveAsync([step], cancellationToken))[step.Id].UserId is { } id ? [id] : [];
    }

    /// <summary>
    /// Người đã duyệt một cấp trong các bước cho trước (cùng lượt) – không duyệt thêm cấp khác (lát R3, design.md mục 18.26). Gồm cả người
    /// được duyệt thay (lát O2).
    /// </summary>
    /// <param name="steps">Bước của một lượt duyệt.</param>
    /// <returns>Id người đã duyệt.</returns>
    internal static HashSet<Guid> ApprovedBy(IEnumerable<ApprovalStep> steps) =>
        steps.Where(s => s.Status == ApprovalStepStatus.Approved)
            .SelectMany(s => new[] { s.ActedBy, s.OnBehalfOf }).OfType<Guid>().ToHashSet();

    /// <summary>
    /// Lý do người dùng không được tham gia quyết định (duyệt, chấm thầu, chọn thầu) vì có kê khai xung đột lợi ích còn hiệu lực hôm nay
    /// với một trong các NCC (design.md mục 18.8 – đề xuất Ban CUVT N4), hoặc null. Kê khai dùng chung mọi công ty (gắn với con người).
    /// </summary>
    /// <param name="userId">Người dùng.</param>
    /// <param name="supplierIds">NCC của phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Câu báo nêu mã NCC, hoặc null khi không vướng.</returns>
    public async Task<string?> ConflictBlockerAsync(Guid userId, IReadOnlyCollection<Guid> supplierIds, CancellationToken cancellationToken)
    {
        if (supplierIds.Count == 0) return null;
        var today = _clock.TodayInVietnam();
        var codes = await _db.ConflictDeclarations.AsNoTracking()
            .Where(d => d.UserId == userId && supplierIds.Contains(d.SupplierId) && d.ValidFrom <= today && (d.ValidTo == null || d.ValidTo >= today))
            .Join(_db.Suppliers, d => d.SupplierId, s => s.Id, (_, s) => s.Code)
            .Distinct().OrderBy(c => c).ToListAsync(cancellationToken);
        return codes.Count == 0 ? null
            : $"Bạn đã kê khai quan hệ lợi ích với NCC {string.Join(", ", codes)} nên không được duyệt, chấm thầu hoặc chọn thầu liên quan NCC này.";
    }

    /// <summary>NCC của đối tượng duyệt; phụ lục hợp đồng lấy NCC của hợp đồng gốc.</summary>
    private async Task<IReadOnlyCollection<Guid>> SupplierIdsAsync(IApprovalSubject document, CancellationToken cancellationToken) => document switch
    {
        // Bỏ filter công ty: phụ lục đã nạp (qua phạm vi người dùng, hoặc bởi dịch vụ nhắc quá hạn chạy nền không có người dùng – lát O2c), chỉ
        // lấy NCC của đúng hợp đồng gốc.
        PurchaseContractAmendment amendment => await _db.PurchaseContracts.AsNoTracking().IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(c => c.Id == amendment.ContractId).Select(c => c.SupplierId).ToListAsync(cancellationToken),
        _ => document.RelatedSupplierIds,
    };

    /// <summary>Báo người lập phiếu kết quả duyệt.</summary>
    private void NotifyCreator(IApprovalSubject document, string type, string title, string message)
    {
        if (document.CreatedBy is { } creator)
            _notifications.Add(document.CompanyId, [creator], type, title, message, document.DocType, document.Id);
    }

    /// <summary>Tên loại phiếu trong câu thông báo.</summary>
    private static string Noun(IApprovalSubject document) => ApprovalTypes.Get(document.DocType).Name;

    /// <summary>Số tiền trong thông báo theo kiểu Việt Nam, ví dụ 2.000.000 (CLAUDE.md mục 5).</summary>
    private static readonly System.Globalization.CultureInfo VietnameseCulture = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Bỏ qua các bước còn chờ (phiếu bị từ chối hoặc hủy khi đang chờ duyệt).</summary>
    public async Task SkipPendingAsync(IApprovalSubject document, CancellationToken cancellationToken)
    {
        var pending = await _db.ApprovalSteps.IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(s => s.DocType == document.DocType && s.DocumentId == document.Id && s.Status == ApprovalStepStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var s in pending) s.Skip();
    }

    /// <summary>Lịch sử duyệt của phiếu, theo lượt rồi cấp.</summary>
    /// <param name="docType">Loại phiếu duyệt.</param>
    /// <param name="documentId">Id phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <param name="document">
    /// Phiếu (khi có): bước đang đến lượt kèm "người duyệt tiếp theo" (TC-4, design.md mục 18.22); null thì không tính.
    /// </param>
    /// <returns>Các bước.</returns>
    public async Task<IReadOnlyList<ApprovalStepDto>> HistoryAsync(
        string docType, Guid documentId, CancellationToken cancellationToken, IApprovalSubject? document = null)
    {
        // Lịch sử gồm bước của mọi công ty (duyệt nhiều công ty – mục 18.6); chỉ lấy bước của đúng phiếu người dùng đã xem được.
        var steps = await _db.ApprovalSteps.AsNoTracking().IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(s => s.DocType == docType && s.DocumentId == documentId)
            .OrderBy(s => s.Round).ThenBy(s => s.Stage).ThenBy(s => s.CompanyId).ThenBy(s => s.Level)
            .ToListAsync(cancellationToken);
        var roleIds = steps.Select(s => s.RoleId).OfType<Guid>().Distinct().ToList();
        var userIds = steps.Select(s => s.ActedBy).Concat(steps.Select(s => s.SubmittedBy)).Concat(steps.Select(s => s.OnBehalfOf))
            .OfType<Guid>().Distinct().ToList();
        var roles = await _db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
        var companyIds = steps.Select(s => s.CompanyId).Distinct().ToList();
        var companies = await _db.Companies.AsNoTracking().Where(c => companyIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);
        // Mọi bước còn chờ của lượt hiện tại đều tính người duyệt được (yêu cầu người dùng 03/11/2026: người duyệt cấp 1 thấy ai duyệt tiếp
        // sau mình): bước đang đến lượt → NextApprovers ("Chờ: …"), bước sau → UpcomingApprovers ("Sẽ duyệt: …"). Cùng điều kiện với
        // ApproveBlockerAsync (quyền, vai trò, không phải người lập, không vướng xung đột lợi ích) – danh sách có thể đổi khi phân quyền đổi.
        var next = new Dictionary<Guid, IReadOnlyList<string>>();
        var activeIds = new HashSet<Guid>();
        if (document is { IsAwaitingApproval: true })
        {
            var round = steps.Where(s => s.Round == document.ApprovalRound).ToList();
            activeIds = ApprovalProgress.Active(round).Select(s => s.Id).ToHashSet();
            var pendingSteps = new List<NextApproverResolver.PendingStep>();
            foreach (var step in round.Where(s => s.Status == ApprovalStepStatus.Pending)) pendingSteps.Add(await PendingStepAsync(document, step, cancellationToken));
            next = await _approvers.NamesAsync(pendingSteps, cancellationToken);
        }

        // Bước người duyệt động hiện nhãn theo bộ phận ("Trưởng phòng ban PX1", "GĐ khối HCNS") ở cột vai trò.
        var dynamicLabels = await _dynamic.ResolveAsync(steps, cancellationToken);
        return steps.Select(s => new ApprovalStepDto(
                s.Round, s.Level,
                dynamicLabels.TryGetValue(s.Id, out var dynamic) ? dynamic.Label : s.RoleId is { } r ? roles.GetValueOrDefault(r) : null,
                s.Status.ToString(),
                // Duyệt theo ủy quyền (lát O2): "Bình (thay An)".
                s.ActedBy is { } u
                    ? s.OnBehalfOf is { } o ? $"{users.GetValueOrDefault(u)} (thay {users.GetValueOrDefault(o)})" : users.GetValueOrDefault(u)
                    : null,
                s.ActedAt, s.Comment, companies.GetValueOrDefault(s.CompanyId), s.Stage,
                activeIds.Contains(s.Id) ? next.GetValueOrDefault(s.Id) : null,
                !activeIds.Contains(s.Id) ? next.GetValueOrDefault(s.Id) : null,
                s.SubmittedAt, s.SubmittedBy is { } sb ? users.GetValueOrDefault(sb) : null))
            .ToList();
    }
}
