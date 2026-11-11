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
/// Bảng tổng hợp đề xuất mua 2A/2B (lát Y5b, design.md mục 19.3 bước 2). Phiếu thuộc công ty đang làm việc (filter công ty giữ nguyên); dòng lấy
/// từ đề nghị mua <b>đã duyệt</b> còn cần mua của cùng bộ phận (2A: cùng công trình); mỗi dòng đề nghị chỉ ở một bảng chưa hủy – kiểm tra dưới
/// khóa các đề nghị liên quan để hai người lập cùng lúc không lấy trùng. Duyệt qua <see cref="ApprovalEngine"/> theo bộ cấp BTH.
/// </summary>
public sealed class PurchaseSummaryService : IPurchaseSummaryService
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
    public PurchaseSummaryService(ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
        _approvals = new ApprovalEngine(db, scope, clock);
        _budgets = new BudgetUsage(db);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PurchaseSummaryListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var rows = await (
                from s in _db.PurchaseSummaries.AsNoTracking()
                join u in _db.Users on s.CreatedBy equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby s.Date descending, s.No descending
                select new
                {
                    s.Id, s.No, s.Date, s.Form, s.Status, s.TotalAmount, LineCount = s.Lines.Count,
                    Department = _db.Departments.Where(d => d.Id == s.DepartmentId).Select(d => d.Name).FirstOrDefault(),
                    Project = _db.ConstructionProjects.Where(p => p.Id == s.ConstructionProjectId).Select(p => p.Code).FirstOrDefault(),
                    CreatedBy = u == null ? null : u.FullName,
                })
            .Take(ListLimit)
            .ToListAsync(cancellationToken);
        return rows.Select(r => new PurchaseSummaryListItemDto(
                r.Id, r.No, r.Date, r.Form.ToString(), r.Status.ToString(), r.Department, r.Project, r.LineCount, r.TotalAmount, r.CreatedBy))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<PurchaseSummaryDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var summary = await _db.PurchaseSummaries.AsNoTracking().Include(s => s.Lines).SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
                      ?? throw new NotFoundException("Không tìm thấy bảng tổng hợp đề xuất mua.");
        return await ToDtoAsync(summary, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PurchaseSummaryCandidateDto>> CandidatesAsync(PurchaseSummaryCandidateQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        _scope.RequireCompany();
        PurchaseSummaryForm? form = string.IsNullOrWhiteSpace(query.Form) ? null : ParseForm(query.Form);
        var requests = _db.PurchaseRequests.AsNoTracking().Where(r => r.Status == ApprovalDocumentStatus.Approved);
        if (form == PurchaseSummaryForm.Construction) requests = requests.Where(r => r.Category == PurchaseCategory.Construction);
        if (form == PurchaseSummaryForm.Goods) requests = requests.Where(r => r.Category != PurchaseCategory.Construction);
        if (query.DepartmentId is { } departmentId) requests = requests.Where(r => r.DepartmentId == departmentId);
        if (query.ConstructionProjectId is { } projectId) requests = requests.Where(r => r.ConstructionProjectId == projectId);
        var taken = TakenLineIds(query.SummaryId);
        // Mặt hàng đi qua filter nhóm hàng của _db.Items – dòng ngoài phạm vi nhóm hàng của người dùng không hiện.
        var rows = await (
                from l in _db.Set<PurchaseRequestLine>().AsNoTracking()
                join r in requests on l.RequestId equals r.Id
                join i in _db.Items on l.ItemId equals i.Id
                join u in _db.Uoms on l.UomId equals u.Id
                where !taken.Contains(l.Id)
                orderby r.Date, r.No, l.LineNo
                select new
                {
                    l.Id, RequestId = r.Id, r.No, r.Date, r.Category, r.DepartmentId, r.ConstructionProjectId,
                    Department = _db.Departments.Where(d => d.Id == r.DepartmentId).Select(d => d.Name).FirstOrDefault(),
                    Project = _db.ConstructionProjects.Where(p => p.Id == r.ConstructionProjectId).Select(p => p.Code).FirstOrDefault(),
                    l.ItemId, ItemCode = i.Code, ItemName = i.Name, l.UomId, UomCode = u.Code, l.BaseQty, l.UomFactor, l.EstPrice,
                    NeededDate = l.NeededDate ?? r.NeededDate,
                })
            .ToListAsync(cancellationToken);
        var ordered = await OrderedBaseByLineAsync(rows.Select(r => r.Id).ToList(), cancellationToken);
        return rows
            .Select(r => new { r, Open = OpenQty(r.BaseQty, ordered.GetValueOrDefault(r.Id), r.UomFactor) })
            .Where(x => x.Open > 0)
            .Select(x => new PurchaseSummaryCandidateDto(
                x.r.Id, x.r.RequestId, x.r.No, x.r.Date, x.r.Category.ToString(), x.r.DepartmentId, x.r.Department, x.r.ConstructionProjectId,
                x.r.Project, x.r.ItemId, x.r.ItemCode, x.r.ItemName, x.r.UomId, x.r.UomCode, x.Open, x.r.EstPrice,
                decimal.Round(x.Open * x.r.EstPrice, 2, MidpointRounding.AwayFromZero), x.r.NeededDate))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<PurchaseSummaryDto> CreateAsync(SavePurchaseSummaryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var companyId = _scope.RequireCompany();
        var header = ToHeader(request);
        await CheckHeaderAsync(header, null, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var lines = await ResolveLinesAsync(request.Lines, null, new HashSet<(Guid, Guid)>(), cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.PurchaseSummary, request.Date, cancellationToken);
        var summary = PurchaseSummary.Create(companyId, no, header);
        summary.SetLines(lines);
        _db.PurchaseSummaries.Add(summary);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(summary.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseSummaryDto> UpdateAsync(Guid id, SavePurchaseSummaryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var summary = await LoadAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(summary, cancellationToken);
        var header = ToHeader(request);
        await CheckHeaderAsync(header, summary, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var lines = await ResolveLinesAsync(request.Lines, summary.Id, CurrentUnits(summary), cancellationToken);
        summary.Update(header);
        summary.SetLines(lines);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseSummaryDto> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        var summary = await LoadAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(summary, cancellationToken);
        await CheckHeaderAsync(summary.HeaderData(), summary, cancellationToken);
        // Công trình phải còn Đang thực hiện lúc gửi (như đề nghị loại A – lát Y3a), kể cả bảng nháp lập trước khi công trình đổi trạng thái.
        if (summary.ConstructionProjectId is { } projectId)
        {
            var project = await _db.ConstructionProjects.AsNoTracking().Where(p => p.Id == projectId)
                .Select(p => new { p.Code, p.Status }).SingleAsync(cancellationToken);
            if (project.Status != ConstructionProjectStatus.InProgress)
                throw new BusinessRuleException($"Công trình {project.Code} không còn ở trạng thái Đang thực hiện – không gửi duyệt bảng tổng hợp được.");
        }

        // Kiểm tra lại dòng lúc gửi dưới khóa đề nghị: đề nghị có thể đã bị hủy / đã lên PO từ lúc lưu nháp – SL chụp lại theo SL còn cần mua.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var data = summary.Lines.OrderBy(l => l.LineNo)
            .Select(l => new SavePurchaseSummaryLineRequest(l.RequestLineId, l.PurchasingUnitId, l.Note)).ToList();
        summary.SetLines(await ResolveLinesAsync(data, summary.Id, CurrentUnits(summary), cancellationToken));
        await _approvals.SubmitAsync(summary, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseSummaryDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var summary = await LoadAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(summary, cancellationToken);
        await _approvals.ApproveAsync(summary, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseSummaryDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var summary = await LoadAsync(id, cancellationToken);
        await _approvals.RejectAsync(summary, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseSummaryDto> RecallAsync(Guid id, CancellationToken cancellationToken)
    {
        var summary = await LoadAsync(id, cancellationToken);
        // Như đề nghị mua (yêu cầu người dùng 11/11/2026): phiếu đã gửi chỉ quản trị hủy được, nên người lập rút về nháp để sửa hoặc tự hủy.
        if (!CanRecall(summary))
            throw new ForbiddenAccessException($"Chỉ người lập (hoặc quản trị) rút được bảng tổng hợp {summary.No} về nháp.");
        await _approvals.SkipPendingAsync(summary, cancellationToken);
        summary.ReturnToDraft(null);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseSummaryDto> CancelAsync(Guid id, CancelDocumentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var summary = await LoadAsync(id, cancellationToken);
        // Quyền hủy + lý do bắt buộc (design.md mục 20.2a): nháp – người lập; đã gửi duyệt trở đi – chỉ quản trị. Hủy xong các dòng đề nghị trở
        // lại chọn được vào bảng tổng hợp khác. Hủy dây chuyền (bảng → RFQ/PO lấy từ bảng) làm ở lát Y5c.
        var reason = DocumentCancelPolicy.Authorize(
            _scope, $"bảng tổng hợp {summary.No}", summary.Status == ApprovalDocumentStatus.Draft, summary.CreatedBy, request.Reason);
        summary.Cancel(reason);
        await _approvals.SkipPendingAsync(summary, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Số các bảng tổng hợp chưa hủy đang chứa dòng của các đề nghị – đề nghị trong bảng tổng hợp chưa hủy thì hủy bảng trước (lát Y5b; hủy dây
    /// chuyền đề nghị → bảng tổng hợp làm ở Y5c).
    /// </summary>
    /// <param name="db">DbContext (filter công ty giữ nguyên).</param>
    /// <param name="requestIds">Đề nghị mua.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số bảng, theo thứ tự.</returns>
    internal static async Task<List<string>> ActiveSummaryNosAsync(ProcDbContext db, IReadOnlyCollection<Guid> requestIds, CancellationToken cancellationToken) =>
        await (
                from l in db.Set<PurchaseSummaryLine>()
                join s in db.PurchaseSummaries on l.SummaryId equals s.Id
                where requestIds.Contains(l.RequestId) && s.Status != ApprovalDocumentStatus.Cancelled
                select s.No)
            .Distinct().OrderBy(no => no).ToListAsync(cancellationToken);

    /// <summary>
    /// Dòng đề nghị đã ở bảng tổng hợp chưa hủy (trừ bảng <paramref name="exceptSummaryId"/>) – mỗi dòng đề nghị chỉ thuộc một bảng (lát Y5b).
    /// Bỏ filter công ty không cần: dòng đề nghị và bảng cùng công ty (khóa ngoại kèm công ty).
    /// </summary>
    private IQueryable<Guid> TakenLineIds(Guid? exceptSummaryId) =>
        from l in _db.Set<PurchaseSummaryLine>()
        join s in _db.PurchaseSummaries on l.SummaryId equals s.Id
        where s.Status != ApprovalDocumentStatus.Cancelled && s.Id != exceptSummaryId
        select l.RequestLineId;

    /// <summary>
    /// Nạp thông tin dòng đề nghị cho các dòng gửi lên, dưới khóa các đề nghị liên quan (chống hai bảng lấy cùng dòng). Đơn vị mua phải thuộc công
    /// ty và còn dùng; đơn vị đã gán sẵn trên dòng của chính bảng này được giữ dù đã ngừng dùng (design.md mục 19.3, lát Y5a).
    /// </summary>
    /// <param name="lines">Dòng gửi lên.</param>
    /// <param name="summaryId">Bảng đang sửa (null khi lập mới).</param>
    /// <param name="keptUnits">Cặp (dòng đề nghị, đơn vị mua) đang có trên bảng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Dữ liệu dòng cho Domain.</returns>
    /// <exception cref="BusinessRuleException">Khi dòng đề nghị không có, chưa duyệt, đã ở bảng khác hoặc đơn vị mua không hợp lệ.</exception>
    private async Task<List<(PurchaseSummaryLineData, PurchaseSummaryLineSource)>> ResolveLinesAsync(
        IReadOnlyList<SavePurchaseSummaryLineRequest> lines, Guid? summaryId, IReadOnlySet<(Guid LineId, Guid UnitId)> keptUnits,
        CancellationToken cancellationToken)
    {
        var lineIds = lines.Select(l => l.RequestLineId).Distinct().ToList();
        var sources = await (
                from l in _db.Set<PurchaseRequestLine>().AsNoTracking()
                join r in _db.PurchaseRequests on l.RequestId equals r.Id
                where lineIds.Contains(l.Id)
                select new
                {
                    l.Id, l.RequestId, r.No, r.Status, r.Category, r.DepartmentId, r.ConstructionProjectId, l.ItemId, l.UomId, l.UomFactor,
                    l.BaseQty, l.EstPrice, NeededDate = l.NeededDate ?? r.NeededDate,
                })
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (sources.Count != lineIds.Count) throw new BusinessRuleException("Có dòng đề nghị không còn hoặc ngoài phạm vi công ty.");
        await RowLocks.LockAsync<PurchaseRequest>(_db, sources.Values.Select(s => s.RequestId).Distinct(), cancellationToken);
        // Trạng thái đọc lại sau khi khóa: đề nghị có thể vừa bị hủy / đóng bởi người khác.
        var requestIds = sources.Values.Select(s => s.RequestId).Distinct().ToList();
        var statuses = await _db.PurchaseRequests.AsNoTracking().Where(r => requestIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Status, cancellationToken);
        var itemIds = sources.Values.Select(s => s.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Code, cancellationToken);
        if (items.Count != itemIds.Count) throw new BusinessRuleException("Có dòng đề nghị chứa mặt hàng ngoài phạm vi nhóm hàng của bạn.");

        var taken = await (
                from l in _db.Set<PurchaseSummaryLine>().AsNoTracking()
                join s in _db.PurchaseSummaries on l.SummaryId equals s.Id
                where lineIds.Contains(l.RequestLineId) && s.Status != ApprovalDocumentStatus.Cancelled && s.Id != summaryId
                select new { l.RequestLineId, s.No })
            .ToListAsync(cancellationToken);
        if (taken.Count > 0)
        {
            var first = taken[0];
            var source = sources[first.RequestLineId];
            throw new BusinessRuleException(
                $"Dòng {items[source.ItemId]} của đề nghị {source.No} đã ở bảng tổng hợp {first.No} – mỗi dòng đề nghị chỉ thuộc một bảng tổng hợp.");
        }

        var unitIds = lines.Select(l => l.PurchasingUnitId).Distinct().ToList();
        var units = await _db.PurchasingUnits.AsNoTracking().Where(u => unitIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.Code, u.IsActive }, cancellationToken);
        var ordered = await OrderedBaseByLineAsync(lineIds, cancellationToken);
        var result = new List<(PurchaseSummaryLineData, PurchaseSummaryLineSource)>();
        foreach (var line in lines)
        {
            var s = sources[line.RequestLineId];
            var where = $"Dòng {items[s.ItemId]} của đề nghị {s.No}";
            if (statuses[s.RequestId] != ApprovalDocumentStatus.Approved)
                throw new BusinessRuleException($"{where}: đề nghị không ở trạng thái Đã duyệt (đã hủy, đã đóng hoặc chưa duyệt xong).");
            if (!units.TryGetValue(line.PurchasingUnitId, out var unit))
                throw new BusinessRuleException($"{where}: đơn vị mua không có ở công ty đang làm việc.");
            if (!unit.IsActive && !keptUnits.Contains((line.RequestLineId, line.PurchasingUnitId)))
                throw new BusinessRuleException($"{where}: đơn vị mua {unit.Code} đã ngừng dùng – hãy chọn đơn vị khác.");
            result.Add((
                new PurchaseSummaryLineData(line.RequestLineId, line.PurchasingUnitId, line.Note),
                new PurchaseSummaryLineSource(
                    s.Id, s.RequestId, s.No, s.Category, s.DepartmentId, s.ConstructionProjectId, s.ItemId, items[s.ItemId], s.UomId, s.UomFactor,
                    OpenQty(s.BaseQty, ordered.GetValueOrDefault(s.Id), s.UomFactor), s.EstPrice, s.NeededDate)));
        }

        return result;
    }

    /// <summary>Cặp (dòng đề nghị, đơn vị mua) đang có trên bảng – giữ được đơn vị đã ngừng dùng.</summary>
    private static HashSet<(Guid LineId, Guid UnitId)> CurrentUnits(PurchaseSummary summary) =>
        summary.Lines.Select(l => (l.RequestLineId, l.PurchasingUnitId)).ToHashSet();

    /// <summary>
    /// SL còn cần mua theo ĐVT dòng = (SL cơ sở − SL cơ sở đã lên PO chưa hủy) ÷ hệ số, làm tròn 4 số, không âm. Ví dụ dòng 10 thùng × 24 = 240
    /// cái, PO đã đặt 96 cái → còn (240 − 96) ÷ 24 = 6 thùng.
    /// </summary>
    private static decimal OpenQty(decimal baseQty, decimal orderedBase, decimal factor) =>
        factor <= 0 ? 0 : Math.Max(0, decimal.Round((baseQty - orderedBase) / factor, 4, MidpointRounding.AwayFromZero));

    /// <summary>SL đã lên PO chưa hủy (ĐVT cơ sở) theo dòng đề nghị – cùng cách tính với dòng PR còn mở (<see cref="PurchaseRequestService"/>).</summary>
    private async Task<Dictionary<Guid, decimal>> OrderedBaseByLineAsync(IReadOnlyList<Guid> prLineIds, CancellationToken cancellationToken) =>
        await (
                from ol in _db.Set<PurchaseOrderLine>().AsNoTracking()
                join o in _db.PurchaseOrders on ol.OrderId equals o.Id
                where ol.SourcePrLineId != null && prLineIds.Contains(ol.SourcePrLineId.Value) && o.Status != ApprovalDocumentStatus.Cancelled
                group ol by ol.SourcePrLineId!.Value into g
                select new { g.Key, Qty = g.Sum(x => x.BaseQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);

    /// <summary>
    /// Bộ phận, công trình của đầu phiếu thuộc công ty đang làm việc; chọn mới thì bộ phận phải còn dùng, công trình phải đang thực hiện (giữ giá
    /// trị cũ của bảng nháp dù đã đổi trạng thái – chặn lúc gửi).
    /// </summary>
    private async Task CheckHeaderAsync(PurchaseSummaryHeader header, PurchaseSummary? current, CancellationToken cancellationToken)
    {
        var department = await _db.Departments.AsNoTracking().Where(d => d.Id == header.DepartmentId)
                             .Select(d => new { d.Code, d.IsActive }).SingleOrDefaultAsync(cancellationToken)
                         ?? throw new BusinessRuleException("Bộ phận đề xuất không có ở công ty đang làm việc.");
        if (!department.IsActive && current?.DepartmentId != header.DepartmentId)
            throw new BusinessRuleException($"Bộ phận {department.Code} đã ngừng dùng.");
        if (header.ConstructionProjectId is not { } projectId) return;
        var project = await _db.ConstructionProjects.AsNoTracking().Where(p => p.Id == projectId)
                          .Select(p => new { p.Code, p.Status }).SingleOrDefaultAsync(cancellationToken)
                      ?? throw new BusinessRuleException("Công trình không có ở công ty đang làm việc.");
        if (project.Status != ConstructionProjectStatus.InProgress && current?.ConstructionProjectId != projectId)
            throw new BusinessRuleException($"Công trình {project.Code} không ở trạng thái Đang thực hiện.");
    }

    /// <summary>Mọi mặt hàng trên phiếu phải trong phạm vi nhóm hàng của người dùng mới sửa, gửi, duyệt được.</summary>
    private async Task EnsureItemsVisibleAsync(PurchaseSummary summary, CancellationToken cancellationToken)
    {
        var itemIds = summary.Lines.Select(l => l.ItemId).Distinct().ToList();
        var visible = await _db.Items.CountAsync(i => itemIds.Contains(i.Id), cancellationToken);
        if (visible != itemIds.Count)
            throw new ForbiddenAccessException($"Bảng tổng hợp {summary.No} có mặt hàng ngoài phạm vi nhóm hàng của bạn.");
    }

    /// <summary>Tải bảng kèm dòng (theo dõi thay đổi).</summary>
    /// <exception cref="NotFoundException">Khi không có ở công ty đang làm việc.</exception>
    private async Task<PurchaseSummary> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await _db.PurchaseSummaries.Include(s => s.Lines).SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
               ?? throw new NotFoundException("Không tìm thấy bảng tổng hợp đề xuất mua.");
    }

    /// <summary>Người dùng hiện tại rút được phiếu về nháp không: phiếu chờ duyệt, là người lập hoặc có quyền Hủy phiếu.</summary>
    private bool CanRecall(PurchaseSummary summary) =>
        summary.Status == ApprovalDocumentStatus.Submitted
        && (summary.CreatedBy == _scope.UserId || _scope.HasPermission(Permissions.System.DocumentCancel));

    /// <summary>
    /// Khung chi phí đầu phiếu (biểu mẫu 2A/2B). 2A: còn lại = dự toán − lũy kế PO đã duyệt − tổng đề xuất (chỉ trừ tổng đề xuất khi bảng còn nháp
    /// / chờ duyệt – đã duyệt thì PO lập từ bảng đã vào lũy kế, như đề nghị loại A). Ví dụ dự toán 1.000.000.000, lũy kế 700.000.000, đề xuất
    /// 350.000.000 → còn lại −50.000.000, vượt. 2B: ngân sách bộ phận tháng của ngày lập, lũy kế đã dùng (đã gồm các đề nghị của bảng), còn lại =
    /// ngân sách − lũy kế.
    /// </summary>
    private async Task<PurchaseSummaryFrameDto> FrameAsync(PurchaseSummary summary, CancellationToken cancellationToken)
    {
        if (summary.Form == PurchaseSummaryForm.Construction && summary.ConstructionProjectId is { } projectId)
        {
            var project = await _db.ConstructionProjects.AsNoTracking().Where(p => p.Id == projectId)
                .Select(p => new { p.Code, p.Name, p.EstimateAmount }).SingleOrDefaultAsync(cancellationToken);
            var spent = (await ConstructionSpending.ByProjectAsync(_db, [projectId], cancellationToken)).GetValueOrDefault(projectId).Net;
            var pending = summary.Status is ApprovalDocumentStatus.Draft or ApprovalDocumentStatus.Submitted ? summary.TotalAmount : 0m;
            var estimate = project?.EstimateAmount ?? 0m;
            var remaining = estimate - spent - pending;
            return new PurchaseSummaryFrameDto(estimate, spent, summary.TotalAmount, remaining, remaining < 0, project?.Code, project?.Name, null);
        }

        var month = new DateOnly(summary.Date.Year, summary.Date.Month, 1);
        var budget = await _db.DepartmentBudgets.AsNoTracking()
            .Where(b => b.DepartmentId == summary.DepartmentId && b.Year == month.Year && b.Month == month.Month)
            .Select(b => (decimal?)b.Amount).SingleOrDefaultAsync(cancellationToken);
        var used = (await _budgets.UsedAsync(month.Year, summary.DepartmentId, null, null, cancellationToken))
            .GetValueOrDefault((summary.DepartmentId, month.Month));
        var left = budget - used;
        return new PurchaseSummaryFrameDto(budget, used, summary.TotalAmount, left, left < 0, null, null, month);
    }

    /// <summary>Dựng DTO chi tiết.</summary>
    private async Task<PurchaseSummaryDto> ToDtoAsync(PurchaseSummary summary, CancellationToken cancellationToken)
    {
        var itemIds = summary.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => new { i.Code, i.Name }, cancellationToken);
        var uomIds = summary.Lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var unitIds = summary.Lines.Select(l => l.PurchasingUnitId).Distinct().ToList();
        var units = await _db.PurchasingUnits.AsNoTracking().Where(u => unitIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.Code, u.Name }, cancellationToken);
        var requestIds = summary.Lines.Select(l => l.RequestId).Distinct().ToList();
        var requests = await _db.PurchaseRequests.AsNoTracking().Where(r => requestIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => new { r.No, r.Category }, cancellationToken);
        var department = await _db.Departments.AsNoTracking().Where(d => d.Id == summary.DepartmentId).Select(d => d.Name)
            .SingleOrDefaultAsync(cancellationToken);
        var createdBy = summary.CreatedBy is { } c
            ? await _db.Users.AsNoTracking().Where(u => u.Id == c).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken)
            : null;

        var lines = summary.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var item = items.GetValueOrDefault(l.ItemId);
            var unit = units.GetValueOrDefault(l.PurchasingUnitId);
            var request = requests.GetValueOrDefault(l.RequestId);
            return new PurchaseSummaryLineDto(
                l.Id, l.LineNo, l.RequestId, request?.No ?? "", l.RequestLineId, request?.Category.ToString() ?? "", l.ItemId,
                item?.Code ?? "(ngoài phạm vi)", item?.Name ?? "", l.UomId, uoms.GetValueOrDefault(l.UomId) ?? "", l.Qty, l.EstPrice, l.Amount,
                l.NeededDate, l.PurchasingUnitId, unit?.Code ?? "", unit?.Name ?? "", l.Note);
        }).ToList();

        var itemsVisible = items.Count == itemIds.Count;
        var step = summary.Status == ApprovalDocumentStatus.Submitted ? await _approvals.CurrentStepAsync(summary, cancellationToken) : null;
        var canApprove = step is not null && itemsVisible && await _approvals.ApproveBlockerAsync(summary, step, cancellationToken) is null;
        var canManage = _scope.HasPermission(Permissions.Purchase.SummaryManage);
        string? readOnly = !itemsVisible ? "Bảng tổng hợp có mặt hàng ngoài phạm vi nhóm hàng của bạn nên chỉ xem được."
            : !canManage ? "Bạn không có quyền lập, sửa bảng tổng hợp đề xuất mua." : null;
        var draft = summary.Status == ApprovalDocumentStatus.Draft;
        var actions = new PurchaseDocumentActions(
            draft && readOnly is null,
            draft && readOnly is null,
            canApprove,
            // Hủy: nháp – người lập; đã gửi duyệt trở đi – chỉ quản trị (design.md mục 20.2a).
            summary.Status is not ApprovalDocumentStatus.Cancelled && readOnly is null
                && DocumentCancelPolicy.CanCancel(_scope, draft, summary.CreatedBy),
            readOnly,
            CanRecall: readOnly is null && CanRecall(summary));

        return new PurchaseSummaryDto(
            summary.Id, summary.No, summary.Date, summary.Form.ToString(), summary.Status.ToString(), summary.DepartmentId, department,
            summary.ConstructionProjectId, summary.Note, summary.TotalAmount, await FrameAsync(summary, cancellationToken), summary.RejectReason,
            summary.CancelReason, summary.CreatedAt, createdBy, summary.ApprovedAt, lines,
            await _approvals.HistoryAsync(DocumentTypes.PurchaseSummary, summary.Id, cancellationToken, summary), actions, summary.Version);
    }

    /// <summary>Đổi yêu cầu lưu thành đầu phiếu cho Domain.</summary>
    private static PurchaseSummaryHeader ToHeader(SavePurchaseSummaryRequest r) =>
        new(r.Date, ParseForm(r.Form), r.DepartmentId, r.ConstructionProjectId, r.Note);

    /// <summary>Đọc biểu mẫu: Construction/2A hoặc Goods/2B.</summary>
    /// <exception cref="BusinessRuleException">Khi không phải 2A/2B.</exception>
    private static PurchaseSummaryForm ParseForm(string? form) => form?.Trim().ToUpperInvariant() switch
    {
        "2A" or "CONSTRUCTION" => PurchaseSummaryForm.Construction,
        "2B" or "GOODS" => PurchaseSummaryForm.Goods,
        _ => throw new BusinessRuleException("Biểu mẫu phải là 2A (Construction) hoặc 2B (Goods)."),
    };
}
