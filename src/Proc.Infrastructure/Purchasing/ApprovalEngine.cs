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

    /// <summary>Tạo cơ chế duyệt.</summary>
    public ApprovalEngine(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
        _notifications = new NotificationPublisher(db, scope, clock);
        _approvers = new NextApproverResolver(db, scope, clock);
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
        var steps = ApprovalPlan.Levels(rules, document.ApprovalAmount)
            .Select(l => ApprovalStep.Create(document.CompanyId, document.DocType, document.Id, round, l.Level, l.RoleId))
            .ToList();
        await StartAsync(document, steps, cancellationToken);
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
        var steps = ApprovalPlan.Levels(await BaseRulesAsync(document, ruleType, cancellationToken), document.ApprovalAmount)
            .Select(l => ApprovalStep.Create(document.CompanyId, document.DocType, document.Id, round, l.Level, l.RoleId))
            .ToList();
        foreach (var (type, stage) in extraStages.OrderBy(x => x.Stage))
        {
            var rules = await _db.ApprovalRules.AsNoTracking().Where(r => r.DocType == type).ToListAsync(cancellationToken);
            var levels = ApprovalPlan.ExceptionLevels(rules, document.ApprovalAmount);
            if (skipPlannedRoleTypes?.Contains(type) == true)
                levels = ApprovalPlan.WithoutPlannedRoles(levels, steps.Select(s => s.RoleId).ToList());
            steps.AddRange(levels.Select(l => ApprovalStep.Create(document.CompanyId, document.DocType, document.Id, round, l.Level, l.RoleId, stage)));

        }

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
    public async Task<string?> ApproveBlockerAsync(IApprovalSubject document, ApprovalStep? step, CancellationToken cancellationToken)
    {
        if (!document.IsAwaitingApproval) return $"Phiếu {document.No} không ở trạng thái chờ duyệt.";
        if (step is null) return $"Phiếu {document.No} chưa đến lượt công ty bạn đang làm việc duyệt.";
        var userId = _scope.UserId;
        if (userId is null) return "Chưa đăng nhập.";
        // Tách người lập và người duyệt – nguyên tắc kiểm soát nội bộ.
        if (document.IsPreparedBy(userId.Value)) return "Người lập không tự duyệt phiếu của mình.";
        if (!_scope.HasPermission(ApprovePermission(document.DocType))) return "Bạn không có quyền duyệt loại phiếu này.";
        if (await ConflictBlockerAsync(userId.Value, await SupplierIdsAsync(document, cancellationToken), cancellationToken) is { } conflict)
            return conflict;
        if (step.RoleId is { } roleId
            && !await _db.UserCompanyRoles.AnyAsync(u => u.UserId == userId && u.CompanyId == step.CompanyId && u.RoleId == roleId, cancellationToken))
        {
            var role = await _db.Roles.Where(r => r.Id == roleId).Select(r => r.Name).SingleOrDefaultAsync(cancellationToken);
            return $"Cấp {step.Level} do vai trò {role} duyệt – bạn không có vai trò này tại công ty.";
        }

        // Mỗi cấp một người khác nhau trong cùng lượt duyệt: một người có quyền nhiều cấp mà duyệt liên tiếp thì duyệt nhiều cấp mất tác
        // dụng (lát R3, design.md mục 18.26). Gửi lại sau khi bị từ chối là lượt mới – người đó lại duyệt được một cấp.
        var approvedLevel = (await RoundStepsAsync(document, cancellationToken))
            .Where(s => s.Status == ApprovalStepStatus.Approved && s.ActedBy == userId)
            .OrderBy(s => s.Stage).ThenBy(s => s.Level).FirstOrDefault();
        if (approvedLevel is not null)
            return $"Bạn đã duyệt cấp {approvedLevel.Level} của phiếu {document.No} trong lượt này – cấp sau phải do người khác duyệt.";

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
        if (await ApproveBlockerAsync(document, step, cancellationToken) is { } reason) throw new BusinessRuleException(reason);
        step!.Approve(_scope.UserId!.Value, _clock.UtcNow, comment);
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
        if (await ApproveBlockerAsync(document, step, cancellationToken) is { } reason) throw new BusinessRuleException(reason);
        step!.Reject(_scope.UserId!.Value, _clock.UtcNow, comment);
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
    private async Task NotifyApproversAsync(IApprovalSubject document, ApprovalStep step, CancellationToken cancellationToken)
    {
        // Người nhận = "người duyệt tiếp theo" (NextApproverResolver: quyền, vai trò, không phải người lập, không vướng xung đột lợi ích).
        var users = (await _approvers.CandidateIdsAsync([await PendingStepAsync(document, step, cancellationToken)], cancellationToken))[step.Id];
        _notifications.Add(step.CompanyId, users.Where(u => document.CreatedBy is not null || u != _scope.UserId), NotificationTypes.ApprovalPending,
            $"{Noun(document)} {document.No} chờ bạn duyệt",
            ApprovalTypes.Get(document.DocType).AmountLabel is null
                ? $"{Noun(document)} {document.No} đang chờ duyệt cấp {step.Level}."
                : $"{Noun(document)} {document.No} trị giá {document.ApprovalAmount.ToString("N0", VietnameseCulture)} đ đang chờ duyệt cấp {step.Level}.",
            document.DocType, document.Id);
    }

    /// <summary>
    /// Bước đang chờ kèm điều kiện loại người lập, người đã duyệt một cấp trong lượt này (lát R3) và NCC của phiếu, để tìm người duyệt.
    /// Bước của lượt đọc có theo dõi nên thấy cả bước vừa duyệt trong request mà chưa lưu.
    /// </summary>
    private async Task<NextApproverResolver.PendingStep> PendingStepAsync(IApprovalSubject document, ApprovalStep step, CancellationToken cancellationToken)
    {
        var approvedBy = ApprovedBy(await RoundStepsAsync(document, cancellationToken));
        return new(step.Id, document.DocType, step.CompanyId, step.RoleId, u => document.IsPreparedBy(u) || approvedBy.Contains(u),
            await SupplierIdsAsync(document, cancellationToken));
    }

    /// <summary>Người đã duyệt một cấp trong các bước cho trước (cùng lượt) – không duyệt thêm cấp khác (lát R3, design.md mục 18.26).</summary>
    /// <param name="steps">Bước của một lượt duyệt.</param>
    /// <returns>Id người đã duyệt.</returns>
    internal static HashSet<Guid> ApprovedBy(IEnumerable<ApprovalStep> steps) =>
        steps.Where(s => s.Status == ApprovalStepStatus.Approved && s.ActedBy is not null).Select(s => s.ActedBy!.Value).ToHashSet();

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
        PurchaseContractAmendment amendment => await _db.PurchaseContracts.AsNoTracking()
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
        var userIds = steps.Select(s => s.ActedBy).Concat(steps.Select(s => s.SubmittedBy)).OfType<Guid>().Distinct().ToList();
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

        return steps.Select(s => new ApprovalStepDto(
                s.Round, s.Level, s.RoleId is { } r ? roles.GetValueOrDefault(r) : null, s.Status.ToString(),
                s.ActedBy is { } u ? users.GetValueOrDefault(u) : null, s.ActedAt, s.Comment, companies.GetValueOrDefault(s.CompanyId), s.Stage,
                activeIds.Contains(s.Id) ? next.GetValueOrDefault(s.Id) : null,
                !activeIds.Contains(s.Id) ? next.GetValueOrDefault(s.Id) : null,
                s.SubmittedAt, s.SubmittedBy is { } sb ? users.GetValueOrDefault(sb) : null))
            .ToList();
    }
}
