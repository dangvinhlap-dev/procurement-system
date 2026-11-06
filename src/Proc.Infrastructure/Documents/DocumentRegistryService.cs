using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Domain.Common;
using Proc.Domain.Control;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;
using Proc.Domain.Sourcing;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Purchasing;

namespace Proc.Infrastructure.Documents;

/// <summary>
/// Tra cứu mọi loại chứng từ của công ty đang làm việc (design.md mục 18.22). Mỗi loại một truy vấn riêng, chỉ chạy khi người dùng có
/// quyền xem loại đó; mọi truy vấn đi qua global query filter (công ty, kho) nên không thấy chứng từ ngoài phạm vi.
/// </summary>
public sealed class DocumentRegistryService : IDocumentRegistryService
{
    /// <summary>Số dòng tối đa mỗi lần tra cứu.</summary>
    public const int Limit = 5000;

    /// <summary>Khoảng ngày dài nhất (ngày) – tránh quét cả CSDL.</summary>
    public const int MaxDays = 366;

    /// <summary>Loại phiếu kho (một bảng InventoryDocument).</summary>
    private static readonly string[] InventoryTypes =
    [
        DocumentTypes.GoodsReceipt, DocumentTypes.ReturnToVendor, DocumentTypes.Transfer, DocumentTypes.IntercompanySale,
        DocumentTypes.IntercompanyReceipt, DocumentTypes.Adjustment,
    ];

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly NextApproverResolver _approvers;

    /// <summary>Khởi tạo dịch vụ.</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ (ngày hiệu lực kê khai xung đột lợi ích khi tìm người duyệt tiếp theo).</param>
    public DocumentRegistryService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _approvers = new NextApproverResolver(db, scope, clock);
    }

    /// <summary>Dòng thô đọc từ một bảng, trước khi ghép tên NCC/kho/người lập.</summary>
    private sealed record Raw(
        string DocType, Guid Id, string No, DateOnly Date, Guid? SupplierId, Guid? WarehouseId, Guid? CounterCompanyId,
        string? Description, decimal? Amount, string Status, DocumentProgress Progress, Guid? CreatedBy, DateTimeOffset CreatedAt);

    /// <summary>Loại chứng từ người dùng được xem (theo quyền xem của từng loại).</summary>
    private List<string> VisibleTypes()
    {
        var types = new List<string>();
        void Add(string permission, params string[] codes)
        {
            if (_scope.HasPermission(permission)) types.AddRange(codes);
        }

        Add(Permissions.Purchase.PrView, DocumentTypes.PurchaseRequest);
        Add(Permissions.Purchase.PoView, DocumentTypes.PurchaseOrder);
        Add(Permissions.Inventory.DocumentView, InventoryTypes);
        Add(Permissions.Pricing.PriceListView, DocumentTypes.PriceList);
        Add(Permissions.Sourcing.RfqView, DocumentTypes.Sourcing, DocumentTypes.RequestForQuotation);
        Add(Permissions.Purchase.ContractView, DocumentTypes.Contract, ApprovalTypes.ContractAmendment);
        Add(Permissions.MasterData.SupplierView, ApprovalTypes.Supplier);
        Add(Permissions.MasterData.SoleSourceView, DocumentTypes.SoleSource);
        Add(Permissions.Pricing.MonthlyApprovalView, DocumentTypes.MonthlySupplierApproval);
        Add(Permissions.MasterData.SupplierReviewView, DocumentTypes.SupplierReview);
        if (_scope.HasPermission(Permissions.Control.AnomalyView) || _scope.HasPermission(Permissions.Control.FindingReceive)
            || _scope.HasPermission(Permissions.Control.FindingManage))
            types.Add(DocumentTypes.ControlFinding);
        return types;
    }

    /// <inheritdoc />
    public async Task<DocumentRegistryResult> SearchAsync(DocumentRegistryQuery query, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        if (query.To < query.From) throw new BusinessRuleException("Đến ngày phải sau hoặc bằng từ ngày.");
        if (query.To.DayNumber - query.From.DayNumber >= MaxDays)
            throw new BusinessRuleException($"Khoảng ngày tối đa {MaxDays} ngày – hãy thu hẹp khoảng ngày.");

        var visible = VisibleTypes();
        var wanted = query.DocTypes.Count == 0 ? visible : visible.Where(t => query.DocTypes.Contains(t)).ToList();
        var userId = _scope.UserId;
        var created = query.Mine == "created";
        var acted = query.Mine == "acted";
        var from = query.From;
        var to = query.To;
        // Loại không có ngày chứng từ lọc theo thời điểm lập: 00:00 giờ Việt Nam của ngày đầu → 00:00 ngày sau ngày cuối.
        var offset = VietnamTime.Zone.GetUtcOffset(from.ToDateTime(TimeOnly.MinValue));
        var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime();
        var toUtc = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime();

        // Phiếu có quy trình duyệt: "tôi đã duyệt" = có bước duyệt/từ chối do tôi thực hiện.
        IQueryable<Guid> ActedIds(string docType) => _db.ApprovalSteps
            .Where(s => s.DocType == docType && s.ActedBy == userId
                        && (s.Status == ApprovalStepStatus.Approved || s.Status == ApprovalStepStatus.Rejected))
            .Select(s => s.DocumentId);

        var raws = new List<Raw>();
        var take = Limit + 1;

        if (wanted.Any(InventoryTypes.Contains))
        {
            var types = wanted.Where(InventoryTypes.Contains).ToList();
            var q = _db.InventoryDocuments.AsNoTracking().Where(d => types.Contains(d.DocType) && d.Date >= from && d.Date <= to);
            if (created) q = q.Where(d => d.CreatedBy == userId);
            // Phiếu kho không có quy trình duyệt: "tôi đã duyệt" hiểu là tôi ghi sổ.
            if (acted) q = q.Where(d => d.PostedBy == userId);
            var rows = await q.OrderByDescending(d => d.Date).Take(take)
                .Select(d => new { d.DocType, d.Id, d.No, d.Date, d.SupplierId, d.FromWarehouseId, d.ToWarehouseId, d.CounterCompanyId, d.Note,
                    d.TotalAmount, d.TotalVat, d.Status, d.CreatedBy, d.CreatedAt })
                .ToListAsync(cancellationToken);
            raws.AddRange(rows.Select(d => new Raw(
                d.DocType, d.Id, d.No, d.Date, d.SupplierId,
                // Kho hiện ở cột đối tác khi không có NCC: phiếu xuất lấy kho xuất, phiếu nhập lấy kho nhập.
                d.FromWarehouseId ?? d.ToWarehouseId, d.CounterCompanyId, d.Note, d.TotalAmount + d.TotalVat, d.Status.ToString(),
                d.Status switch
                {
                    DocumentStatus.Posted => DocumentProgress.Done,
                    DocumentStatus.Cancelled => DocumentProgress.Cancelled,
                    _ => DocumentProgress.Open,
                },
                d.CreatedBy, d.CreatedAt)));
        }

        if (wanted.Contains(DocumentTypes.PurchaseRequest))
        {
            var q = _db.PurchaseRequests.AsNoTracking().Where(d => d.Date >= from && d.Date <= to);
            if (created) q = q.Where(d => d.CreatedBy == userId);
            if (acted) { var actedIds = ActedIds(DocumentTypes.PurchaseRequest); q = q.Where(d => actedIds.Contains(d.Id)); }
            var rows = await q.OrderByDescending(d => d.Date).Take(take)
                .Select(d => new { d.Id, d.No, d.Date, d.Purpose, d.TotalAmount, d.Status, d.CreatedBy, d.CreatedAt })
                .ToListAsync(cancellationToken);
            raws.AddRange(rows.Select(d => new Raw(DocumentTypes.PurchaseRequest, d.Id, d.No, d.Date, null, null, null, d.Purpose,
                d.TotalAmount, d.Status.ToString(), ApprovalProgress(d.Status), d.CreatedBy, d.CreatedAt)));
        }

        if (wanted.Contains(DocumentTypes.PurchaseOrder))
        {
            var q = _db.PurchaseOrders.AsNoTracking().Where(d => d.Date >= from && d.Date <= to);
            if (created) q = q.Where(d => d.CreatedBy == userId);
            if (acted) { var actedIds = ActedIds(DocumentTypes.PurchaseOrder); q = q.Where(d => actedIds.Contains(d.Id)); }
            var rows = await q.OrderByDescending(d => d.Date).Take(take)
                .Select(d => new { d.Id, d.No, d.Date, d.SupplierId, d.Note, d.TotalAmount, d.TotalVat, d.Status, d.CreatedBy, d.CreatedAt })
                .ToListAsync(cancellationToken);
            raws.AddRange(rows.Select(d => new Raw(DocumentTypes.PurchaseOrder, d.Id, d.No, d.Date, d.SupplierId, null, null, d.Note,
                d.TotalAmount + d.TotalVat, d.Status.ToString(), ApprovalProgress(d.Status), d.CreatedBy, d.CreatedAt)));
        }

        if (wanted.Contains(DocumentTypes.PriceList))
        {
            // Bảng giá: ngày = hiệu lực từ.
            var q = _db.SupplierPriceLists.AsNoTracking().Where(d => d.ValidFrom >= from && d.ValidFrom <= to);
            if (created) q = q.Where(d => d.CreatedBy == userId);
            if (acted) { var actedIds = ActedIds(DocumentTypes.PriceList); q = q.Where(d => actedIds.Contains(d.Id)); }
            var rows = await q.OrderByDescending(d => d.ValidFrom).Take(take)
                .Select(d => new { d.Id, d.No, d.ValidFrom, d.SupplierId, d.Title, d.Status, d.CreatedBy, d.CreatedAt })
                .ToListAsync(cancellationToken);
            raws.AddRange(rows.Select(d => new Raw(DocumentTypes.PriceList, d.Id, d.No, d.ValidFrom, d.SupplierId, null, null, d.Title,
                null, d.Status.ToString(),
                d.Status is PriceListStatus.Active or PriceListStatus.Inactive ? DocumentProgress.Done : DocumentProgress.Open,
                d.CreatedBy, d.CreatedAt)));
        }

        if (wanted.Contains(DocumentTypes.Sourcing) && !acted)
        {
            var q = _db.SourcingRequests.AsNoTracking().Where(d => d.Date >= from && d.Date <= to);
            if (created) q = q.Where(d => d.CreatedBy == userId);
            var rows = await q.OrderByDescending(d => d.Date).Take(take)
                .Select(d => new { d.Id, d.No, d.Date, d.Description, d.Status, d.CreatedBy, d.CreatedAt })
                .ToListAsync(cancellationToken);
            raws.AddRange(rows.Select(d => new Raw(DocumentTypes.Sourcing, d.Id, d.No, d.Date, null, null, null, d.Description, null,
                d.Status.ToString(),
                d.Status switch
                {
                    SourcingStatus.Closed => DocumentProgress.Done,
                    SourcingStatus.Cancelled => DocumentProgress.Cancelled,
                    _ => DocumentProgress.Open,
                },
                d.CreatedBy, d.CreatedAt)));
        }

        if (wanted.Contains(DocumentTypes.RequestForQuotation))
        {
            var q = _db.Rfqs.AsNoTracking().Where(d => d.Date >= from && d.Date <= to);
            if (created) q = q.Where(d => d.CreatedBy == userId);
            if (acted) { var actedIds = ActedIds(DocumentTypes.RequestForQuotation); q = q.Where(d => actedIds.Contains(d.Id)); }
            var rows = await q.OrderByDescending(d => d.Date).Take(take)
                .Select(d => new { d.Id, d.No, d.Date, d.Title, d.Status, d.CreatedBy, d.CreatedAt })
                .ToListAsync(cancellationToken);
            raws.AddRange(rows.Select(d => new Raw(DocumentTypes.RequestForQuotation, d.Id, d.No, d.Date, null, null, null, d.Title, null,
                d.Status.ToString(),
                d.Status switch
                {
                    RfqStatus.Awarded => DocumentProgress.Done,
                    RfqStatus.Cancelled => DocumentProgress.Cancelled,
                    _ => DocumentProgress.Open,
                },
                d.CreatedBy, d.CreatedAt)));
        }

        if (wanted.Contains(DocumentTypes.Contract))
        {
            // Hợp đồng: ngày = ngày ký, chưa ký thì hiệu lực từ.
            var q = _db.PurchaseContracts.AsNoTracking().Where(d => (d.SignDate ?? d.ValidFrom) >= from && (d.SignDate ?? d.ValidFrom) <= to);
            if (created) q = q.Where(d => d.CreatedBy == userId);
            if (acted) { var actedIds = ActedIds(DocumentTypes.Contract); q = q.Where(d => actedIds.Contains(d.Id)); }
            var rows = await q.OrderByDescending(d => d.SignDate ?? d.ValidFrom).Take(take)
                .Select(d => new { d.Id, d.No, Date = d.SignDate ?? d.ValidFrom, d.SupplierId, d.Title, d.Status, d.CreatedBy, d.CreatedAt })
                .ToListAsync(cancellationToken);
            raws.AddRange(rows.Select(d => new Raw(DocumentTypes.Contract, d.Id, d.No, d.Date, d.SupplierId, null, null, d.Title, null,
                d.Status.ToString(),
                d.Status switch
                {
                    ContractStatus.Active or ContractStatus.Terminated => DocumentProgress.Done,
                    ContractStatus.Cancelled => DocumentProgress.Cancelled,
                    _ => DocumentProgress.Open,
                },
                d.CreatedBy, d.CreatedAt)));
        }

        if (wanted.Contains(ApprovalTypes.ContractAmendment))
        {
            var q = _db.PurchaseContractAmendments.AsNoTracking().Where(d => d.CreatedAt >= fromUtc && d.CreatedAt < toUtc);
            if (created) q = q.Where(d => d.CreatedBy == userId);
            if (acted) { var actedIds = ActedIds(ApprovalTypes.ContractAmendment); q = q.Where(d => actedIds.Contains(d.Id)); }
            var rows = await q.OrderByDescending(d => d.CreatedAt).Take(take)
                .Join(_db.PurchaseContracts, a => a.ContractId, c => c.Id,
                    (a, c) => new { a.Id, a.No, a.CreatedAt, c.SupplierId, ContractNo = c.No, a.ExpectedAmount, a.Status, a.CreatedBy })
                .ToListAsync(cancellationToken);
            raws.AddRange(rows.Select(d => new Raw(ApprovalTypes.ContractAmendment, d.Id, d.No, VietnamDate(d.CreatedAt), d.SupplierId, null,
                null, $"Phụ lục hợp đồng {d.ContractNo}", d.ExpectedAmount, d.Status.ToString(),
                d.Status switch
                {
                    ContractAmendmentStatus.Approved => DocumentProgress.Done,
                    ContractAmendmentStatus.Cancelled => DocumentProgress.Cancelled,
                    _ => DocumentProgress.Open,
                },
                d.CreatedBy, d.CreatedAt)));
        }

        if (wanted.Contains(ApprovalTypes.Supplier))
        {
            // Yêu cầu duyệt NCC không thuộc riêng công ty nào (danh mục NCC dùng chung): hiện yêu cầu lập tại công ty đang làm việc
            // hoặc có chặng duyệt tại công ty này (duyệt hai chặng – design.md mục 18.6).
            var steps = _db.ApprovalSteps.Where(s => s.DocType == ApprovalTypes.Supplier && s.CompanyId == companyId).Select(s => s.DocumentId);
            var q = _db.SupplierChangeRequests.AsNoTracking()
                .Where(d => (d.CompanyId == companyId || steps.Contains(d.Id)) && d.CreatedAt >= fromUtc && d.CreatedAt < toUtc);
            if (created) q = q.Where(d => d.CreatedBy == userId);
            if (acted) { var actedIds = ActedIds(ApprovalTypes.Supplier); q = q.Where(d => actedIds.Contains(d.Id)); }
            var rows = await q.OrderByDescending(d => d.CreatedAt).Take(take)
                .Select(d => new { d.Id, d.No, d.CreatedAt, d.SupplierId, d.ProposedSummary, d.Status, d.CreatedBy })
                .ToListAsync(cancellationToken);
            raws.AddRange(rows.Select(d => new Raw(ApprovalTypes.Supplier, d.Id, d.No, VietnamDate(d.CreatedAt), d.SupplierId, null, null,
                d.ProposedSummary, null, d.Status.ToString(),
                d.Status switch
                {
                    SupplierChangeRequestStatus.Approved => DocumentProgress.Done,
                    SupplierChangeRequestStatus.Rejected or SupplierChangeRequestStatus.Cancelled => DocumentProgress.Cancelled,
                    _ => DocumentProgress.Open,
                },
                d.CreatedBy, d.CreatedAt)));
        }

        if (wanted.Contains(DocumentTypes.SoleSource))
        {
            var q = _db.SoleSourceApprovals.AsNoTracking().Where(d => d.CreatedAt >= fromUtc && d.CreatedAt < toUtc);
            if (created) q = q.Where(d => d.CreatedBy == userId);
            if (acted) { var actedIds = ActedIds(DocumentTypes.SoleSource); q = q.Where(d => actedIds.Contains(d.Id)); }
            var rows = await q.OrderByDescending(d => d.CreatedAt).Take(take)
                .Select(d => new { d.Id, d.No, d.CreatedAt, d.SupplierId, d.Status, d.CreatedBy })
                .ToListAsync(cancellationToken);
            raws.AddRange(rows.Select(d => new Raw(DocumentTypes.SoleSource, d.Id, d.No, VietnamDate(d.CreatedAt), d.SupplierId, null, null,
                "Phê duyệt NCC độc quyền", null, d.Status.ToString(),
                d.Status is SoleSourceStatus.Approved or SoleSourceStatus.Ended ? DocumentProgress.Done : DocumentProgress.Open,
                d.CreatedBy, d.CreatedAt)));
        }

        if (wanted.Contains(DocumentTypes.MonthlySupplierApproval))
        {
            // Filter công ty giữ nguyên: sổ chứng từ là chứng từ của công ty đang làm việc – phiếu duyệt tháng hiện ở tổng công ty (nơi lập).
            var q = _db.MonthlySupplierApprovals.AsNoTracking().Where(d => d.CreatedAt >= fromUtc && d.CreatedAt < toUtc);
            if (created) q = q.Where(d => d.CreatedBy == userId);
            if (acted) { var actedIds = ActedIds(DocumentTypes.MonthlySupplierApproval); q = q.Where(d => actedIds.Contains(d.Id)); }
            var rows = await q.OrderByDescending(d => d.CreatedAt).Take(take)
                .Select(d => new { d.Id, d.No, d.CreatedAt, d.Period, d.Status, d.CreatedBy })
                .ToListAsync(cancellationToken);
            raws.AddRange(rows.Select(d => new Raw(DocumentTypes.MonthlySupplierApproval, d.Id, d.No, VietnamDate(d.CreatedAt), null, null, null,
                $"Duyệt NCC và giá tháng {d.Period:MM/yyyy}", null, d.Status.ToString(),
                d.Status switch
                {
                    MonthlyApprovalStatus.Approved or MonthlyApprovalStatus.Replaced => DocumentProgress.Done,
                    MonthlyApprovalStatus.Cancelled => DocumentProgress.Cancelled,
                    _ => DocumentProgress.Open,
                },
                d.CreatedBy, d.CreatedAt)));
        }

        if (wanted.Contains(DocumentTypes.SupplierReview) && !acted)
        {
            var q = _db.SupplierReviews.AsNoTracking().Where(d => d.Date >= from && d.Date <= to);
            if (created) q = q.Where(d => d.CreatedBy == userId);
            var rows = await q.OrderByDescending(d => d.Date).Take(take)
                .Select(d => new { d.Id, d.No, d.Date, d.SupplierId, d.Status, d.CreatedBy, d.CreatedAt })
                .ToListAsync(cancellationToken);
            raws.AddRange(rows.Select(d => new Raw(DocumentTypes.SupplierReview, d.Id, d.No, d.Date, d.SupplierId, null, null,
                "Đánh giá NCC định kỳ", null, d.Status.ToString(),
                d.Status switch
                {
                    SupplierReviewStatus.Completed => DocumentProgress.Done,
                    SupplierReviewStatus.Cancelled => DocumentProgress.Cancelled,
                    _ => DocumentProgress.Open,
                },
                d.CreatedBy, d.CreatedAt)));
        }

        if (wanted.Contains(DocumentTypes.ControlFinding) && !acted)
        {
            var q = _db.ControlFindings.AsNoTracking().Where(d => d.Date >= from && d.Date <= to);
            if (created) q = q.Where(d => d.CreatedBy == userId);
            var rows = await q.OrderByDescending(d => d.Date).Take(take)
                .Select(d => new { d.Id, d.No, d.Date, d.SupplierId, d.Title, d.Status, d.CreatedBy, d.CreatedAt })
                .ToListAsync(cancellationToken);
            raws.AddRange(rows.Select(d => new Raw(DocumentTypes.ControlFinding, d.Id, d.No, d.Date, d.SupplierId, null, null, d.Title,
                null, d.Status.ToString(), d.Status == FindingStatus.Concluded ? DocumentProgress.Done : DocumentProgress.Open,
                d.CreatedBy, d.CreatedAt)));
        }

        if (query.Progress is { } progress) raws = raws.Where(r => r.Progress == progress).ToList();
        var ordered = raws.OrderByDescending(r => r.Date).ThenByDescending(r => r.CreatedAt).ToList();
        var truncated = ordered.Count > Limit;
        if (truncated) ordered = ordered[..Limit];

        var supplierIds = ordered.Where(r => r.SupplierId is not null).Select(r => r.SupplierId!.Value).Distinct().ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => $"{s.Code} – {s.Name}", cancellationToken);
        var warehouseIds = ordered.Where(r => r.WarehouseId is not null).Select(r => r.WarehouseId!.Value).Distinct().ToList();
        // Tên kho vẫn qua lọc phạm vi kho (CLAUDE.md mục 4): kho ngoài phạm vi người xem thì để trống cột đối tác.
        var warehouses = await _db.Warehouses.AsNoTracking()
            .Where(w => warehouseIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => $"{w.Code} – {w.Name}", cancellationToken);
        var companyIds = ordered.Where(r => r.CounterCompanyId is not null).Select(r => r.CounterCompanyId!.Value).Distinct().ToList();
        var companies = await _db.Companies.AsNoTracking().Where(c => companyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);
        var userIds = ordered.Where(r => r.CreatedBy is not null).Select(r => r.CreatedBy!.Value).Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
        // Cấp đang chờ: bước Pending nhỏ nhất (theo chặng rồi cấp) – lượt bị từ chối đã chuyển các bước còn lại sang Skipped. Bước duyệt
        // qua filter công ty nên chỉ thấy bước của công ty đang làm việc.
        var openRows = ordered.Where(r => r.Progress == DocumentProgress.Open).ToDictionary(r => r.Id);
        var openIds = openRows.Keys.ToList();
        var pending = (await _db.ApprovalSteps.AsNoTracking()
                .Where(s => openIds.Contains(s.DocumentId) && s.Status == ApprovalStepStatus.Pending)
                .Select(s => new { s.DocumentId, s.Stage, s.Level })
                .ToListAsync(cancellationToken))
            .GroupBy(s => s.DocumentId)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Stage).ThenBy(s => s.Level).First().Level);
        var nextApprovers = await NextApproversAsync(openRows, cancellationToken);
        var rowsOut = ordered.Select(r =>
        {
            string? partner = r.SupplierId is { } sid && suppliers.TryGetValue(sid, out var s) ? s
                : r.WarehouseId is { } wid && warehouses.TryGetValue(wid, out var w) ? w : null;
            if (r.CounterCompanyId is { } cid && companies.TryGetValue(cid, out var cc)) partner = $"{partner} (đối ứng {cc})".TrimStart();
            return new DocumentRegistryRowDto(
                r.DocType, r.Id, r.No, r.Date, partner, r.Description, r.Amount, r.Status, StatusName(r.DocType, r.Status), r.Progress,
                r.CreatedBy is { } uid && users.TryGetValue(uid, out var name) ? name : null, r.CreatedAt,
                pending.TryGetValue(r.Id, out var level) ? level : null, nextApprovers.GetValueOrDefault(r.Id));
        }).ToList();

        return new DocumentRegistryResult(rowsOut, visible, truncated, Limit);
    }

    /// <summary>
    /// Người duyệt tiếp theo của các chứng từ đang chờ duyệt (TC-4, design.md mục 18.22) – cùng điều kiện với nút Duyệt và thông báo
    /// (<see cref="NextApproverResolver"/>). Bước đến lượt xét trên mọi công ty của phiếu (duyệt NCC hai chặng: chặng Tổng công ty chỉ tới
    /// lượt khi các công ty liên quan đã duyệt) – chỉ đọc bước của chứng từ người xem đã thấy được. Ở đây không tải cả phiếu nên người lập =
    /// người tạo, NCC = NCC của dòng; RFQ (người chọn thầu, mọi NCC được mời) xét đủ trong tab Lịch sử duyệt khi mở phiếu.
    /// </summary>
    /// <param name="openRows">Dòng chưa hoàn thành, theo Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Họ tên theo Id chứng từ; chỉ có ở chứng từ đang chờ duyệt.</returns>
    private async Task<Dictionary<Guid, IReadOnlyList<string>>> NextApproversAsync(
        IReadOnlyDictionary<Guid, Raw> openRows, CancellationToken cancellationToken)
    {
        var ids = openRows.Keys.ToList();
        var steps = await _db.ApprovalSteps.AsNoTracking().IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(s => ids.Contains(s.DocumentId) && s.Status == ApprovalStepStatus.Pending)
            .ToListAsync(cancellationToken);
        var active = steps
            .GroupBy(s => s.DocumentId)
            .SelectMany(g => Proc.Domain.Purchasing.ApprovalProgress.Active(g))
            .ToList();
        var names = await _approvers.NamesAsync(active.Select(step =>
        {
            var row = openRows[step.DocumentId];
            return new NextApproverResolver.PendingStep(step.Id, step.DocType, step.CompanyId, step.RoleId,
                u => u == row.CreatedBy, row.SupplierId is { } sid ? [sid] : []);
        }).ToList(), cancellationToken);
        return active.GroupBy(s => s.DocumentId).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<string>)g.SelectMany(s => names[s.Id]).Distinct().Order(StringComparer.CurrentCulture).ToList());
    }

    /// <summary>Tình trạng chung của PR/PO.</summary>
    private static DocumentProgress ApprovalProgress(ApprovalDocumentStatus status) => status switch
    {
        ApprovalDocumentStatus.Approved or ApprovalDocumentStatus.Closed => DocumentProgress.Done,
        ApprovalDocumentStatus.Cancelled => DocumentProgress.Cancelled,
        _ => DocumentProgress.Open,
    };

    /// <summary>Ngày theo giờ Việt Nam của một thời điểm lưu UTC.</summary>
    private static DateOnly VietnamDate(DateTimeOffset at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, VietnamTime.Zone).DateTime);

    /// <summary>
    /// Tên trạng thái tiếng Việt theo loại chứng từ (cùng tên với màn hình của từng loại).
    /// </summary>
    /// <param name="docType">Mã loại.</param>
    /// <param name="status">Mã trạng thái gốc.</param>
    /// <returns>Tên hiển thị; không biết thì trả mã gốc.</returns>
    public static string StatusName(string docType, string status) => (docType, status) switch
    {
        (_, "Draft") => "Nháp",
        (_, "Posted") => "Đã ghi sổ",
        (_, "Cancelled") when docType == ApprovalTypes.Supplier => "Đã rút",
        (_, "Cancelled") => "Đã hủy",
        (_, "Submitted") => "Chờ duyệt",
        (_, "Approved") => "Đã duyệt",
        (_, "Rejected") => "Bị từ chối",
        (_, "Closed") => "Đã đóng",
        (_, "Active") => "Hiệu lực",
        (_, "Inactive") => "Hết hiệu lực",
        (_, "Terminated") => "Chấm dứt",
        (_, "Ended") => "Đã kết thúc",
        (_, "Completed") => "Hoàn thành",
        (_, "Concluded") => "Đã kết luận",
        (_, "Open") when docType == DocumentTypes.ControlFinding => "Đang xử lý",
        (_, "Open") => "Đang tìm nguồn",
        (_, "Sent") => "Đang nhận báo giá",
        (_, "AwardPending") => "Chờ duyệt kết quả",
        (_, "Awarded") => "Đã chọn thầu",
        _ => status,
    };
}
