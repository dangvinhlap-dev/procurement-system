using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Organization;
using Proc.Domain.Notifications;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Notifications;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Cơ chế duyệt nhiều cấp dùng chung cho PR, PO (design.md mục 6.2, 7 của tổng quan – quy trình duyệt): gửi duyệt sinh các bước theo
/// quy trình của công ty và tổng giá trị phiếu; duyệt lần lượt cấp thấp → cao; từ chối thì phiếu về nháp. Không tự lưu – dịch vụ gọi
/// lưu cùng thay đổi của phiếu trong một lần SaveChanges.
/// </summary>
internal sealed class ApprovalEngine
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;
    private readonly NotificationPublisher _notifications;

    /// <summary>Tạo cơ chế duyệt.</summary>
    public ApprovalEngine(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
        _notifications = new NotificationPublisher(db, scope, clock);
    }

    /// <summary>Quyền duyệt theo loại phiếu.</summary>
    public static string ApprovePermission(string docType) =>
        docType == DocumentTypes.PurchaseRequest ? Permissions.Purchase.PrApprove : Permissions.Purchase.PoApprove;

    /// <summary>
    /// Gửi duyệt: phiếu sang Chờ duyệt, sinh các bước của lượt mới theo quy trình (design.md mục 6.2). Dưới mọi ngưỡng đã cấu
    /// hình thì phiếu được duyệt ngay.
    /// </summary>
    public async Task SubmitAsync(ApprovableDocument document, CancellationToken cancellationToken)
    {
        var round = document.Submit();
        var rules = await _db.ApprovalRules.AsNoTracking().Where(r => r.DocType == document.DocType).ToListAsync(cancellationToken);
        var levels = ApprovalPlan.Levels(rules, document.ApprovalAmount);
        if (levels.Count == 0)
        {
            document.MarkApproved(_clock.UtcNow);
            return;
        }

        var steps = levels.Select(l => ApprovalStep.Create(document.CompanyId, document.DocType, document.Id, round, l.Level, l.RoleId)).ToList();
        _db.ApprovalSteps.AddRange(steps);
        await NotifyApproversAsync(document, steps.MinBy(s => s.Level)!, cancellationToken);
    }

    /// <summary>Bước đang chờ của lượt hiện tại (cấp thấp nhất còn chờ), hoặc null.</summary>
    public Task<ApprovalStep?> CurrentStepAsync(ApprovableDocument document, CancellationToken cancellationToken) =>
        _db.ApprovalSteps
            .Where(s => s.DocType == document.DocType && s.DocumentId == document.Id && s.Round == document.ApprovalRound && s.Status == ApprovalStepStatus.Pending)
            .OrderBy(s => s.Level)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Lý do người dùng hiện tại chưa duyệt được phiếu (null = duyệt được): phiếu phải chờ duyệt; người lập không tự duyệt; phải có
    /// quyền duyệt loại phiếu; cấp có vai trò thì người duyệt phải được gán vai trò đó tại công ty.
    /// </summary>
    public async Task<string?> ApproveBlockerAsync(ApprovableDocument document, ApprovalStep? step, CancellationToken cancellationToken)
    {
        if (document.Status != ApprovalDocumentStatus.Submitted || step is null) return $"Phiếu {document.No} không ở trạng thái chờ duyệt.";
        var userId = _scope.UserId;
        if (userId is null) return "Chưa đăng nhập.";
        // Tách người lập và người duyệt – nguyên tắc kiểm soát nội bộ.
        if (document.CreatedBy == userId) return "Người lập không tự duyệt phiếu của mình.";
        if (!_scope.HasPermission(ApprovePermission(document.DocType))) return "Bạn không có quyền duyệt loại phiếu này.";
        if (step.RoleId is { } roleId
            && !await _db.UserCompanyRoles.AnyAsync(u => u.UserId == userId && u.CompanyId == document.CompanyId && u.RoleId == roleId, cancellationToken))
        {
            var role = await _db.Roles.Where(r => r.Id == roleId).Select(r => r.Name).SingleOrDefaultAsync(cancellationToken);
            return $"Cấp {step.Level} do vai trò {role} duyệt – bạn không có vai trò này tại công ty.";
        }

        return null;
    }

    /// <summary>Duyệt cấp đang chờ; hết cấp thì phiếu Đã duyệt.</summary>
    /// <exception cref="BusinessRuleException">Khi người dùng không duyệt được (lý do cụ thể).</exception>
    public async Task ApproveAsync(ApprovableDocument document, string? comment, CancellationToken cancellationToken)
    {
        var step = await CurrentStepAsync(document, cancellationToken);
        if (await ApproveBlockerAsync(document, step, cancellationToken) is { } reason) throw new BusinessRuleException(reason);
        step!.Approve(_scope.UserId!.Value, _clock.UtcNow, comment);
        var remaining = await _db.ApprovalSteps.CountAsync(
            s => s.DocType == document.DocType && s.DocumentId == document.Id && s.Round == document.ApprovalRound
                 && s.Status == ApprovalStepStatus.Pending && s.Id != step.Id,
            cancellationToken);
        if (remaining == 0)
        {
            document.MarkApproved(_clock.UtcNow);
            NotifyCreator(document, NotificationTypes.Approved, $"{Noun(document)} {document.No} đã được duyệt",
                $"{Noun(document)} {document.No} đã được duyệt xong, có thể xử lý bước tiếp theo.");
            return;
        }

        // Còn cấp sau: báo người duyệt cấp kế tiếp (cấp thấp nhất còn chờ, khác bước vừa duyệt).
        var next = await _db.ApprovalSteps
            .Where(s => s.DocType == document.DocType && s.DocumentId == document.Id && s.Round == document.ApprovalRound
                        && s.Status == ApprovalStepStatus.Pending && s.Id != step.Id)
            .OrderBy(s => s.Level)
            .FirstAsync(cancellationToken);
        await NotifyApproversAsync(document, next, cancellationToken);
    }

    /// <summary>Từ chối ở cấp đang chờ: các cấp sau bỏ qua, phiếu về nháp kèm lý do.</summary>
    /// <exception cref="BusinessRuleException">Khi không duyệt được hoặc thiếu lý do.</exception>
    public async Task RejectAsync(ApprovableDocument document, string? comment, CancellationToken cancellationToken)
    {
        var step = await CurrentStepAsync(document, cancellationToken);
        if (await ApproveBlockerAsync(document, step, cancellationToken) is { } reason) throw new BusinessRuleException(reason);
        step!.Reject(_scope.UserId!.Value, _clock.UtcNow, comment);
        await SkipPendingAsync(document, cancellationToken);
        document.ReturnToDraft(comment);
        NotifyCreator(document, NotificationTypes.Rejected, $"{Noun(document)} {document.No} bị từ chối",
            $"{Noun(document)} {document.No} bị từ chối ở cấp {step.Level}: {comment}. Phiếu đã về Nháp để sửa và gửi lại.");
    }

    /// <summary>
    /// Báo người duyệt được cấp đang chờ (design.md mục 11.6): có quyền duyệt loại phiếu tại công ty; cấp có vai trò thì phải có vai trò
    /// đó – cùng điều kiện với <see cref="ApproveBlockerAsync"/>. Người lập không nhận (không tự duyệt được).
    /// </summary>
    private async Task NotifyApproversAsync(ApprovableDocument document, ApprovalStep step, CancellationToken cancellationToken)
    {
        var users = await _notifications.UsersWithPermissionAsync(
            document.CompanyId, ApprovePermission(document.DocType), step.RoleId, null, cancellationToken);
        _notifications.Add(document.CompanyId, users.Where(u => u != document.CreatedBy), NotificationTypes.ApprovalPending,
            $"{Noun(document)} {document.No} chờ bạn duyệt",
            $"{Noun(document)} {document.No} trị giá {document.ApprovalAmount.ToString("N0", VietnameseCulture)} đ đang chờ duyệt cấp {step.Level}.",
            document.DocType, document.Id);
    }

    /// <summary>Báo người lập phiếu kết quả duyệt.</summary>
    private void NotifyCreator(ApprovableDocument document, string type, string title, string message)
    {
        if (document.CreatedBy is { } creator)
            _notifications.Add(document.CompanyId, [creator], type, title, message, document.DocType, document.Id);
    }

    /// <summary>Tên loại phiếu trong câu thông báo.</summary>
    private static string Noun(ApprovableDocument document) =>
        document.DocType == DocumentTypes.PurchaseRequest ? "Đề nghị mua hàng" : "Đơn mua hàng";

    /// <summary>Số tiền trong thông báo theo kiểu Việt Nam, ví dụ 2.000.000 (CLAUDE.md mục 5).</summary>
    private static readonly System.Globalization.CultureInfo VietnameseCulture = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Bỏ qua các bước còn chờ (phiếu bị từ chối hoặc hủy khi đang chờ duyệt).</summary>
    public async Task SkipPendingAsync(ApprovableDocument document, CancellationToken cancellationToken)
    {
        var pending = await _db.ApprovalSteps
            .Where(s => s.DocType == document.DocType && s.DocumentId == document.Id && s.Status == ApprovalStepStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var s in pending) s.Skip();
    }

    /// <summary>Lịch sử duyệt của phiếu, theo lượt rồi cấp.</summary>
    public async Task<IReadOnlyList<ApprovalStepDto>> HistoryAsync(string docType, Guid documentId, CancellationToken cancellationToken)
    {
        var steps = await _db.ApprovalSteps.AsNoTracking()
            .Where(s => s.DocType == docType && s.DocumentId == documentId)
            .OrderBy(s => s.Round).ThenBy(s => s.Level)
            .ToListAsync(cancellationToken);
        var roleIds = steps.Select(s => s.RoleId).OfType<Guid>().Distinct().ToList();
        var userIds = steps.Select(s => s.ActedBy).OfType<Guid>().Distinct().ToList();
        var roles = await _db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
        return steps.Select(s => new ApprovalStepDto(
                s.Round, s.Level, s.RoleId is { } r ? roles.GetValueOrDefault(r) : null, s.Status.ToString(),
                s.ActedBy is { } u ? users.GetValueOrDefault(u) : null, s.ActedAt, s.Comment))
            .ToList();
    }
}
