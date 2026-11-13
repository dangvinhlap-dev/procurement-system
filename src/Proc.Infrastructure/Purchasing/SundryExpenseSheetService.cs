using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Bảng kê chi phí tạp vụ 2C (lát Y11a, design.md mục 19.2 loại C). Phiếu thuộc công ty đang làm việc (filter công ty giữ nguyên); tổng bảng kê tính
/// vào định mức tháng (ngân sách bộ phận × tháng – mục 18.24) lúc lưu và lúc gửi; vượt thì thêm chặng TVB, không chặn (Q5). Duyệt qua
/// <see cref="ApprovalEngine"/> theo bộ cấp BKT.
/// </summary>
public sealed class SundryExpenseSheetService : ISundryExpenseSheetService
{
    /// <summary>Số phiếu tối đa trên danh sách.</summary>
    public const int ListLimit = 2000;

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IDocumentNumberService _numbers;
    private readonly ApprovalEngine _approvals;
    private readonly BudgetUsage _budgets;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="numbers">Đánh số chứng từ.</param>
    /// <param name="clock">Đồng hồ.</param>
    public SundryExpenseSheetService(ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
        _approvals = new ApprovalEngine(db, scope, clock);
        _budgets = new BudgetUsage(db);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SundryExpenseSheetListItemDto>> ListAsync(SundryExpenseSheetFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        EnsureCanView();
        var query = _db.SundryExpenseSheets.AsNoTracking();
        if (filter.From is { } from) query = query.Where(s => s.Date >= from);
        if (filter.To is { } to) query = query.Where(s => s.Date <= to);
        var rows = await (
                from s in query
                join d in _db.Departments on s.DepartmentId equals d.Id
                join u in _db.Users on s.CreatedBy equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby s.Date descending, s.No descending
                select new
                {
                    s.Id, s.No, s.Date, s.Status, d.Code, d.Name, LineCount = s.Lines.Count, s.TotalAmount, s.IsOverBudget,
                    CreatedBy = u == null ? null : u.FullName,
                })
            .Take(ListLimit)
            .ToListAsync(cancellationToken);
        var held = await HeldByAsync(rows.Select(r => r.Id).ToList(), cancellationToken);
        return rows.Select(r =>
            {
                var payment = held.GetValueOrDefault(r.Id);
                return new SundryExpenseSheetListItemDto(
                    r.Id, r.No, r.Date, r.Status.ToString(), r.Code, r.Name, r.LineCount, r.TotalAmount, r.IsOverBudget, payment.No, payment.Id,
                    r.CreatedBy);
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SundryExpenseSheetDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var sheet = await _db.SundryExpenseSheets.AsNoTracking().Include(s => s.Lines).SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy bảng kê chi phí tạp vụ.");
        return await ToDtoAsync(sheet, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SundryExpenseSheetDto> CreateAsync(SaveSundryExpenseSheetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var companyId = _scope.RequireCompany();
        _scope.EnsurePermission(Permissions.Purchase.SundryCreate);
        await EnsureDepartmentAsync(request.DepartmentId, null, cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.SundryExpenseSheet, request.Date, cancellationToken);
        var sheet = SundryExpenseSheet.Create(companyId, no, ToHeader(request));
        sheet.SetLines(ToLines(request));
        sheet.ApplyBudgetCheck(await _budgets.CheckAsync(sheet, cancellationToken));
        _db.SundryExpenseSheets.Add(sheet);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(sheet.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SundryExpenseSheetDto> UpdateAsync(Guid id, SaveSundryExpenseSheetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _scope.EnsurePermission(Permissions.Purchase.SundryCreate);
        var sheet = await LoadAsync(id, cancellationToken);
        await EnsureDepartmentAsync(request.DepartmentId, sheet.DepartmentId, cancellationToken);
        sheet.Update(ToHeader(request));
        sheet.SetLines(ToLines(request));
        sheet.ApplyBudgetCheck(await _budgets.CheckAsync(sheet, cancellationToken));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SundryExpenseSheetDto> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.Purchase.SundryCreate);
        // Dùng chung giao dịch của bên gọi khi có: xem trước chuỗi duyệt chạy thử lệnh gửi rồi hủy (như bảng tổng hợp – lát Y5c-2).
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        var sheet = await LoadAsync(id, cancellationToken);
        // Định mức tính lại lúc gửi (phiếu khác có thể đã dùng từ lúc lưu nháp); vượt thì thêm chặng TVB – không chặn gửi (Q5, lát Y11a).
        sheet.ApplyBudgetCheck(await _budgets.CheckAsync(sheet, cancellationToken));
        var stages = new List<(string Type, int Stage)>();
        if (sheet.IsOverBudget) stages.Add((ApprovalTypes.SundryOverBudget, ApprovalTypes.OverBudgetStage));
        // Phần vượt được xem trước khi trình lên cấp cao nhất – Chủ tịch (như đề nghị mua, người dùng yêu cầu 11/11/2026).
        await _approvals.SubmitWithStagesAsync(sheet, stages, cancellationToken, extraBeforeTopLevel: true);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SundryExpenseSheetDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sheet = await LoadAsync(id, cancellationToken);
        await _approvals.ApproveAsync(sheet, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SundryExpenseSheetDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sheet = await LoadAsync(id, cancellationToken);
        await _approvals.RejectAsync(sheet, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SundryExpenseSheetDto> RecallAsync(Guid id, CancellationToken cancellationToken)
    {
        var sheet = await LoadAsync(id, cancellationToken);
        if (!CanRecall(sheet))
            throw new ForbiddenAccessException($"Chỉ người lập (hoặc quản trị) rút được bảng kê {sheet.No} về nháp.");
        await _approvals.SkipPendingAsync(sheet, cancellationToken);
        sheet.ReturnToDraft(null);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SundryExpenseSheetDto> CancelAsync(Guid id, CancelDocumentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sheet = await LoadAsync(id, cancellationToken);
        // Bảng kê đã gộp vào đề nghị thanh toán 1C chưa hủy: hủy đề nghị trước (hủy dây chuyền từ phiếu cuối – design.md mục 20.2).
        var held = (await HeldByAsync([sheet.Id], cancellationToken)).GetValueOrDefault(sheet.Id);
        if (held.No is not null)
            throw new BusinessRuleException($"Bảng kê {sheet.No} đã ở đề nghị thanh toán {held.No} – hủy đề nghị trước (Hủy dây chuyền).");
        // Quyền hủy + lý do bắt buộc (design.md mục 20.2a): nháp – người lập; đã gửi duyệt trở đi – chỉ quản trị.
        var reason = DocumentCancelPolicy.Authorize(
            _scope, $"bảng kê chi phí tạp vụ {sheet.No}", sheet.Status == ApprovalDocumentStatus.Draft, sheet.CreatedBy, request.Reason);
        sheet.Cancel(reason);
        await _approvals.SkipPendingAsync(sheet, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Đề nghị thanh toán tổng hợp 1C chưa hủy đang giữ từng bảng kê (lát Y11b). Chưa có liên kết bảng kê – đề nghị thì trả rỗng.
    /// </summary>
    /// <param name="sheetIds">Các bảng kê.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Bảng kê → (Id, số) đề nghị.</returns>
    private static Task<Dictionary<Guid, (Guid? Id, string? No)>> HeldByAsync(IReadOnlyCollection<Guid> sheetIds, CancellationToken cancellationToken)
    {
        _ = sheetIds;
        _ = cancellationToken;
        return Task.FromResult(new Dictionary<Guid, (Guid? Id, string? No)>());
    }

    /// <summary>Bộ phận phải thuộc công ty đang làm việc; chọn bộ phận mới (khác bộ phận đang lưu) thì phải còn dùng.</summary>
    private async Task EnsureDepartmentAsync(Guid departmentId, Guid? currentId, CancellationToken cancellationToken)
    {
        var department = await _db.Departments.AsNoTracking().Where(d => d.Id == departmentId).Select(d => new { d.Code, d.IsActive })
                             .SingleOrDefaultAsync(cancellationToken)
                         ?? throw new BusinessRuleException("Bộ phận không có hoặc thuộc công ty khác.");
        if (!department.IsActive && departmentId != currentId)
            throw new BusinessRuleException($"Bộ phận {department.Code} đã ngừng dùng.");
    }

    /// <summary>Tải bảng kê kèm dòng (theo dõi thay đổi).</summary>
    private async Task<SundryExpenseSheet> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await _db.SundryExpenseSheets.Include(s => s.Lines).SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
               ?? throw new NotFoundException("Không tìm thấy bảng kê chi phí tạp vụ.");
    }

    private void EnsureCanView()
    {
        _scope.RequireCompany();
        if (!new[] { Permissions.Purchase.SundryView, Permissions.Purchase.SundryCreate, Permissions.Purchase.SundryApprove }.Any(_scope.HasPermission))
            throw new ForbiddenAccessException("Bạn không có quyền xem bảng kê chi phí tạp vụ.");
    }

    /// <summary>Người dùng hiện tại rút được phiếu về nháp không: phiếu chờ duyệt, là người lập hoặc có quyền Hủy phiếu.</summary>
    private bool CanRecall(SundryExpenseSheet sheet) =>
        sheet.Status == ApprovalDocumentStatus.Submitted
        && (sheet.CreatedBy == _scope.UserId || _scope.HasPermission(Permissions.System.DocumentCancel));

    /// <summary>Dựng DTO chi tiết.</summary>
    private async Task<SundryExpenseSheetDto> ToDtoAsync(SundryExpenseSheet sheet, CancellationToken cancellationToken)
    {
        var department = await _db.Departments.AsNoTracking().Where(d => d.Id == sheet.DepartmentId).Select(d => new { d.Code, d.Name })
            .SingleAsync(cancellationToken);
        var createdBy = sheet.CreatedBy is { } c
            ? await _db.Users.AsNoTracking().Where(u => u.Id == c).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken)
            : null;
        var held = (await HeldByAsync([sheet.Id], cancellationToken)).GetValueOrDefault(sheet.Id);
        var lines = sheet.Lines.OrderBy(l => l.LineNo)
            .Select(l => new SundryExpenseLineDto(l.Id, l.LineNo, l.ExpenseDate, l.Description, l.Vendor, l.ReceiptNo, l.Amount)).ToList();

        var step = sheet.Status == ApprovalDocumentStatus.Submitted ? await _approvals.CurrentStepAsync(sheet, cancellationToken) : null;
        var canApprove = step is not null && await _approvals.ApproveBlockerAsync(sheet, step, cancellationToken) is null;
        string? readOnly = !_scope.HasPermission(Permissions.Purchase.SundryCreate) ? "Bạn không có quyền lập, sửa bảng kê chi phí tạp vụ." : null;
        var draft = sheet.Status == ApprovalDocumentStatus.Draft;
        var actions = new PurchaseDocumentActions(
            draft && readOnly is null,
            draft && readOnly is null,
            canApprove,
            // Hủy: nháp – người lập; đã gửi duyệt trở đi – chỉ quản trị (design.md mục 20.2a); đã ở đề nghị 1C thì hủy đề nghị trước.
            sheet.Status is not ApprovalDocumentStatus.Cancelled && held.No is null
                && DocumentCancelPolicy.CanCancel(_scope, draft, sheet.CreatedBy),
            readOnly,
            CanRecall: CanRecall(sheet));

        return new SundryExpenseSheetDto(
            sheet.Id, sheet.No, sheet.Date, sheet.Status.ToString(), sheet.DepartmentId, department.Code, department.Name, sheet.TotalAmount,
            BudgetService.ToDto(sheet), sheet.Note, held.No, held.Id, sheet.RejectReason, sheet.CancelReason, sheet.CreatedAt, createdBy,
            sheet.ApprovedAt, lines, await _approvals.HistoryAsync(DocumentTypes.SundryExpenseSheet, sheet.Id, cancellationToken, sheet), actions,
            sheet.Version);
    }

    /// <summary>Đổi yêu cầu lưu thành đầu phiếu cho Domain.</summary>
    private static SundryExpenseSheetHeader ToHeader(SaveSundryExpenseSheetRequest r) => new(r.Date, r.DepartmentId, r.Note);

    /// <summary>Đổi dòng gửi lên thành dữ liệu cho Domain.</summary>
    private static List<SundryExpenseLineData> ToLines(SaveSundryExpenseSheetRequest r) =>
        (r.Lines ?? []).Select(l => new SundryExpenseLineData(l.ExpenseDate, l.Description, l.Vendor, l.ReceiptNo, l.Amount)).ToList();
}
