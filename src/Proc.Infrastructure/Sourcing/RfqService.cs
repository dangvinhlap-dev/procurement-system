using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Organization;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Application.Sourcing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Domain.Sourcing;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Purchasing;

namespace Proc.Infrastructure.Sourcing;

/// <summary>
/// RFQ – báo giá – so sánh – chọn thầu (design.md mục 8.4). RFQ có CompanyId nên tự lọc theo công ty đang làm việc. PO sinh từ kết quả
/// chọn thầu đi qua <see cref="IPurchaseOrderService"/> (module mua hàng) để giữ mọi kiểm tra của PO. Kết quả chọn thầu duyệt qua
/// <see cref="ApprovalEngine"/> theo quy trình loại RFQ của công ty lập (lát B3 – design.md mục 8.6, 18.6).
/// </summary>
public sealed class RfqService : IRfqService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IDocumentNumberService _numbers;
    private readonly IClock _clock;
    private readonly IPurchaseOrderService _orders;
    private readonly ISystemParameterService _parameters;
    private readonly IPriceListService _priceLists;
    private readonly ApprovalEngine _approvals;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="numbers">Đánh số chứng từ.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="orders">Dịch vụ đơn mua hàng.</param>
    /// <param name="parameters">Tham số công ty (số NCC tối thiểu khi gửi).</param>
    /// <param name="priceLists">Dịch vụ bảng giá NCC (sinh bảng giá từ kết quả thầu).</param>
    public RfqService(
        ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock, IPurchaseOrderService orders,
        ISystemParameterService parameters, IPriceListService priceLists)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
        _clock = clock;
        _orders = orders;
        _parameters = parameters;
        _priceLists = priceLists;
        _approvals = new ApprovalEngine(db, scope, clock);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RfqListItemDto>> ListAsync(string? status, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var rfqs = _db.Rfqs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var statuses = status.Split(',').Select(ParseStatus).ToList();
            rfqs = rfqs.Where(r => statuses.Contains(r.Status));
        }

        return await (
                from r in rfqs
                join u in _db.Users on r.CreatedBy equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby r.Date descending, r.No descending
                select new RfqListItemDto(
                    r.Id, r.No, r.Date, r.Title, r.Deadline, r.Lines.Count, r.Suppliers.Count, r.Suppliers.Count(s => s.HasQuoted), r.Status.ToString(),
                    u == null ? null : u.FullName))
            .Take(500)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RfqDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var rfq = await Query().AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy RFQ.");
        return await ToDtoAsync(rfq, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RfqDto> CreateAsync(SaveRfqRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);
        await CheckSuppliersAsync(request.SupplierIds, cancellationToken);
        await CheckSourcingAsync(request.SourcingId, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.RequestForQuotation, request.Date, cancellationToken);
        var rfq = Rfq.Create(companyId, no, ToHeader(request));
        rfq.SetLines(lines);
        rfq.SetSuppliers(request.SupplierIds);
        _db.Rfqs.Add(rfq);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(rfq.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RfqDto> UpdateAsync(Guid id, SaveRfqRequest request, CancellationToken cancellationToken)
    {
        var rfq = await LoadAsync(id, cancellationToken);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);
        await CheckSuppliersAsync(request.SupplierIds, cancellationToken);
        await CheckSourcingAsync(request.SourcingId, cancellationToken);
        rfq.Update(ToHeader(request));
        rfq.SetLines(lines);
        rfq.SetSuppliers(request.SupplierIds);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RfqDto> SendAsync(Guid id, SendRfqRequest request, CancellationToken cancellationToken)
    {
        var rfq = await LoadAsync(id, cancellationToken);
        // Kiểm tra lại lúc gửi: NCC có thể vào danh sách đen từ lúc soạn nháp.
        await CheckSuppliersAsync(rfq.Suppliers.Select(s => s.SupplierId).ToList(), cancellationToken);
        var minSuppliers = await _parameters.RfqMinSuppliersAsync(cancellationToken);
        var reason = request.Reason;
        if (rfq.Suppliers.Count < minSuppliers && string.IsNullOrWhiteSpace(reason))
            reason = await SoleSourceReasonAsync(rfq, cancellationToken);
        rfq.Send(_clock.UtcNow, minSuppliers, reason);
        await TrackSourcingAsync(rfq, rfq.Suppliers.Select(s => s.SupplierId), CandidateStatus.RfqSent, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Giải trình tự động khi mời thiếu NCC (design.md mục 18.9): mọi dòng RFQ đều có phiếu NCC độc quyền còn tác dụng hôm nay (của công ty
    /// lập RFQ) với một NCC được mời thì phiếu đó thay cho giải trình, ví dụ "NCC độc quyền đã duyệt: NDQ-TCT-2611-0001". Thiếu một dòng
    /// thì trả null – người dùng phải tự ghi giải trình như cũ.
    /// </summary>
    private async Task<string?> SoleSourceReasonAsync(Rfq rfq, CancellationToken cancellationToken)
    {
        var today = _clock.TodayInVietnam();
        var supplierIds = rfq.Suppliers.Select(s => s.SupplierId).ToList();
        var itemIds = rfq.Lines.Select(l => l.ItemId).Distinct().ToList();
        // Điều kiện "còn tác dụng" giống SoleSourceApproval.IsEffectiveOn, viết lại để EF dịch sang SQL.
        var approvals = await _db.SoleSourceApprovals.AsNoTracking()
            .Where(x => x.CompanyId == rfq.CompanyId && x.Status == SoleSourceStatus.Approved && x.ValidFrom <= today && x.ReviewDate >= today
                && supplierIds.Contains(x.SupplierId) && itemIds.Contains(x.ItemId))
            .Select(x => new { x.ItemId, x.No })
            .ToListAsync(cancellationToken);
        if (itemIds.Any(i => approvals.All(a => a.ItemId != i))) return null;
        var text = "NCC độc quyền đã duyệt: " + string.Join(", ", approvals.Select(a => a.No).Distinct().Order());
        // Cột FewSuppliersReason tối đa 500 ký tự.
        return text.Length > 500 ? text[..497] + "..." : text;
    }

    /// <inheritdoc />
    public async Task<RfqDto> SaveQuotationAsync(Guid id, Guid supplierId, SaveQuotationRequest request, CancellationToken cancellationToken)
    {
        var rfq = await LoadAsync(id, cancellationToken);
        await RequireNoConflictAsync(rfq, cancellationToken);
        rfq.SaveQuotation(
            supplierId,
            new QuotationHeader(
                request.QuoteDate, request.ValidUntil, request.PaymentTerms, request.QualityScore, request.DeliveryScore, request.PaymentScore,
                request.WarrantyScore, request.Note),
            request.Lines.Select(l => new QuotationLineData(l.RfqLineId, l.UnitPrice, l.DiscountPct, l.VatCode, l.LeadTimeDays, l.Note)).ToList());
        await TrackSourcingAsync(rfq, [supplierId], CandidateStatus.Quoted, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RfqDto> AwardAsync(Guid id, AwardRfqRequest request, CancellationToken cancellationToken)
    {
        var rfq = await LoadAsync(id, cancellationToken);
        var userId = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        await RequireNoConflictAsync(rfq, cancellationToken);
        rfq.Award(request.Lines.Select(l => new AwardLineData(l.RfqLineId, l.SupplierId, l.Reason)).ToList(), userId, _clock.TodayInVietnam());
        await CheckAwardedSuppliersAsync(rfq, cancellationToken);
        // Lát B3 (M4): chọn thầu xong là gửi hội đồng duyệt ngay – không có trạng thái "đã chọn nhưng chưa gửi".
        await _approvals.SubmitAsync(rfq, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Chặn người có kê khai xung đột lợi ích với một NCC được mời nhập báo giá – chấm điểm và chọn thầu (design.md mục 18.8).
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi người dùng vướng kê khai còn hiệu lực.</exception>
    private async Task RequireNoConflictAsync(Rfq rfq, CancellationToken cancellationToken)
    {
        if (_scope.UserId is { } userId && await _approvals.ConflictBlockerAsync(userId, rfq.RelatedSupplierIds, cancellationToken) is { } reason)
            throw new BusinessRuleException(reason);
    }

    /// <inheritdoc />
    public async Task<RfqDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var rfq = await LoadAsync(id, cancellationToken);
        // Duyệt cấp cuối là chốt NCC trúng thầu: không để NCC vừa vào danh sách đen trúng thầu (design.md mục 5.8).
        await CheckAwardedSuppliersAsync(rfq, cancellationToken);
        await _approvals.ApproveAsync(rfq, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RfqDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var rfq = await LoadAsync(id, cancellationToken);
        await _approvals.RejectAsync(rfq, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RfqDto> RecallAsync(Guid id, CancellationToken cancellationToken)
    {
        var rfq = await LoadAsync(id, cancellationToken);
        await _approvals.SkipPendingAsync(rfq, cancellationToken);
        rfq.ReturnToDraft(null);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CreatedOrderDto>> CreateOrdersAsync(Guid id, CancellationToken cancellationToken)
    {
        var rfq = await LoadAsync(id, cancellationToken);
        if (rfq.Status != RfqStatus.Awarded) throw new BusinessRuleException($"RFQ {rfq.No} chưa chọn thầu.");
        var warehouseId = await _db.Warehouses.AsNoTracking().Where(w => w.Type == WarehouseType.Main && w.IsActive).Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("Không tìm thấy kho tổng trong phạm vi của bạn để làm kho nhận.");
        var itemIds = rfq.Lines.Select(l => l.ItemId).Distinct().ToList();
        var vatByItem = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.VatCode, cancellationToken);
        var codes = await _db.Suppliers.AsNoTracking().Where(s => rfq.Suppliers.Select(x => x.SupplierId).Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Code, cancellationToken);
        var today = _clock.TodayInVietnam();
        var created = new List<CreatedOrderDto>();
        // Mỗi NCC một PO, lưu lần lượt (mỗi PO một giao dịch riêng): lỗi ở NCC sau không xóa PO đã tạo; bấm lại chỉ tạo phần còn thiếu.
        foreach (var supplier in rfq.Suppliers.Where(s => s.PurchaseOrderId is null && rfq.Lines.Any(l => l.AwardedSupplierId == s.SupplierId)))
        {
            var lines = rfq.Lines.Where(l => l.AwardedSupplierId == supplier.SupplierId).OrderBy(l => l.LineNo).Select(l =>
            {
                var quote = supplier.QuotationLines.Single(q => q.RfqLineId == l.Id);
                return new SavePurchaseOrderLineRequest(
                    null, l.ItemId, l.UomId, l.Qty, quote.UnitPrice, quote.DiscountPct, quote.VatCode ?? vatByItem.GetValueOrDefault(l.ItemId),
                    quote.LeadTimeDays is { } days ? today.AddDays(days) : null, null, l.SourcePrLineId, SourceRfqLineId: l.Id);
            }).ToList();
            var order = await _orders.CreateAsync(
                new SavePurchaseOrderRequest(today, supplier.SupplierId, warehouseId, null, supplier.PaymentTerms, null, null, 0, $"Từ kết quả chọn thầu {rfq.No}", lines),
                cancellationToken);
            rfq.LinkPurchaseOrder(supplier.SupplierId, order.Id);
            await _db.SaveChangesAsync(cancellationToken);
            created.Add(new CreatedOrderDto(supplier.SupplierId, codes.GetValueOrDefault(supplier.SupplierId) ?? "", order.Id, order.No));
        }

        if (created.Count == 0) throw new BusinessRuleException("Mọi NCC trúng thầu đã có đơn mua hàng.");
        return created;
    }

    /// <inheritdoc />
    public async Task<RfqDto> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var rfq = await LoadAsync(id, cancellationToken);
        rfq.Cancel();
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RfqDto> SaveDossierAsync(Guid id, SaveRfqDossierRequest request, CancellationToken cancellationToken)
    {
        var rfq = await LoadAsync(id, cancellationToken);
        rfq.SetDossier(request.Items.Select(i => new RfqDossierData((i.Code ?? string.Empty).Trim().ToUpperInvariant(), i.IsDone, i.Note)).ToList());
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CreatedPriceListDto>> CreatePriceListsAsync(Guid id, CancellationToken cancellationToken)
    {
        var rfq = await LoadAsync(id, cancellationToken);
        if (rfq.Status != RfqStatus.Awarded) throw new BusinessRuleException($"RFQ {rfq.No} chưa được duyệt kết quả chọn thầu.");
        var itemIds = rfq.Lines.Select(l => l.ItemId).Distinct().ToList();
        var vatByItem = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.VatCode, cancellationToken);
        var codes = await _db.Suppliers.AsNoTracking().Where(s => rfq.Suppliers.Select(x => x.SupplierId).Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Code, cancellationToken);
        var today = _clock.TodayInVietnam();
        var created = new List<CreatedPriceListDto>();
        // Mỗi NCC một bảng giá nháp, lưu lần lượt như "Tạo PO": lỗi ở NCC sau không xóa bảng đã tạo; bấm lại chỉ tạo phần còn thiếu.
        foreach (var supplier in rfq.Suppliers.Where(s => s.PriceListId is null && rfq.Lines.Any(l => l.AwardedSupplierId == s.SupplierId)))
        {
            // Hai dòng RFQ cùng mặt hàng × ĐVT thành một bậc giá (bậc từ SL 0): lấy giá sau CK thấp hơn – bảng giá không cho trùng bậc.
            var lines = rfq.Lines.Where(l => l.AwardedSupplierId == supplier.SupplierId).OrderBy(l => l.LineNo)
                .Select(l => (Line: l, Quote: supplier.QuotationLines.Single(q => q.RfqLineId == l.Id)))
                .GroupBy(x => (x.Line.ItemId, x.Line.UomId))
                .Select(g => g.OrderBy(x => x.Quote.UnitPrice * (100m - x.Quote.DiscountPct)).First())
                .Select(x => new SavePriceListLineRequest(
                    null, x.Line.ItemId, x.Line.UomId, 0, x.Quote.UnitPrice, x.Quote.DiscountPct, x.Quote.VatCode ?? vatByItem.GetValueOrDefault(x.Line.ItemId),
                    $"Giá trúng thầu {rfq.No}"))
                .ToList();
            // Hiệu lực tới ngày hết hiệu lực của báo giá; báo giá đã hết hạn thì để trống và ghi chú để người lập bảng giá xem lại.
            var validTo = supplier.ValidUntil is { } until && until >= today ? until : (DateOnly?)null;
            var note = supplier.ValidUntil is { } expired && expired < today
                ? $"Từ kết quả chọn thầu {rfq.No}. Báo giá hết hiệu lực ngày {expired:dd/MM/yyyy} – kiểm tra lại hiệu lực."
                : $"Từ kết quả chọn thầu {rfq.No}.";
            var title = $"Giá trúng thầu {rfq.No} – {rfq.Title}";
            var list = await _priceLists.CreateAsync(
                new SavePriceListRequest(supplier.SupplierId, title.Length > 200 ? title[..200] : title, false, today, validTo, null, note, lines),
                cancellationToken);
            rfq.LinkPriceList(supplier.SupplierId, list.Id);
            await _db.SaveChangesAsync(cancellationToken);
            created.Add(new CreatedPriceListDto(supplier.SupplierId, codes.GetValueOrDefault(supplier.SupplierId) ?? "", list.Id, list.No));
        }

        if (created.Count == 0) throw new BusinessRuleException("Mọi NCC trúng thầu đã có bảng giá.");
        return created;
    }

    /// <summary>Đọc trạng thái từ chuỗi.</summary>
    /// <exception cref="BusinessRuleException">Khi trạng thái không hợp lệ.</exception>
    internal static RfqStatus ParseStatus(string value) =>
        Enum.TryParse<RfqStatus>(value.Trim(), true, out var status) && Enum.IsDefined(status)
            ? status
            : throw new BusinessRuleException("Trạng thái phải là Draft, Sent, AwardPending, Awarded hoặc Cancelled.");

    private IQueryable<Rfq> Query() =>
        _db.Rfqs.Include(r => r.Lines).Include(r => r.Suppliers).ThenInclude(s => s.QuotationLines).Include(r => r.DossierItems).AsSplitQuery();

    private async Task<Rfq> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var rfq = await Query().SingleOrDefaultAsync(r => r.Id == id, cancellationToken) ?? throw new NotFoundException("Không tìm thấy RFQ.");
        var ids = rfq.Lines.Select(l => l.ItemId).Distinct().ToList();
        if (await _db.Items.CountAsync(i => ids.Contains(i.Id), cancellationToken) < ids.Count)
            throw new ForbiddenAccessException("RFQ có mặt hàng ngoài phạm vi nhóm hàng của bạn nên bạn không thao tác được.");
        return rfq;
    }

    /// <summary>
    /// Dòng lấy từ PR: PR đã duyệt của công ty, cùng mặt hàng (SL đặt thật kiểm tra khi sinh PO – RFQ chỉ hỏi giá).
    /// </summary>
    private async Task<List<(RfqLineData, Item)>> ResolveLinesAsync(IReadOnlyList<SaveRfqLineRequest> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Include(i => i.Conversions).Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        var prLineIds = lines.Select(l => l.SourcePrLineId).OfType<Guid>().Distinct().ToList();
        var sources = await (
                from l in _db.Set<PurchaseRequestLine>().AsNoTracking()
                join r in _db.PurchaseRequests on l.RequestId equals r.Id
                where prLineIds.Contains(l.Id)
                select new { l.Id, l.ItemId, r.No, r.Status })
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        return lines.Select((l, index) =>
        {
            var item = items.GetValueOrDefault(l.ItemId)
                ?? throw new NotFoundException($"Dòng {index + 1}: không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).");
            if (l.SourcePrLineId is { } prLineId)
            {
                var source = sources.GetValueOrDefault(prLineId) ?? throw new NotFoundException($"Dòng {index + 1}: không tìm thấy dòng đề nghị mua hàng nguồn.");
                if (source.Status != ApprovalDocumentStatus.Approved) throw new BusinessRuleException($"Dòng {index + 1}: đề nghị {source.No} chưa được duyệt (hoặc đã hủy).");
                if (source.ItemId != l.ItemId) throw new BusinessRuleException($"Dòng {index + 1}: mặt hàng khác mặt hàng của đề nghị {source.No}.");
            }

            return (new RfqLineData(l.Id, l.ItemId, l.UomId, l.Qty, l.SourcePrLineId, l.Note), item);
        }).ToList();
    }

    /// <summary>NCC phải có trong danh mục và không ở danh sách đen (design.md mục 5.8); NCC Tiềm năng vẫn mời được.</summary>
    private async Task CheckSuppliersAsync(IReadOnlyList<Guid> supplierIds, CancellationToken cancellationToken)
    {
        var found = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).Select(s => new { s.Id, s.Code, s.Status }).ToListAsync(cancellationToken);
        if (found.Count < supplierIds.Distinct().Count()) throw new NotFoundException("Không tìm thấy nhà cung cấp.");
        if (found.FirstOrDefault(s => s.Status == SupplierStatus.Blacklisted) is { } black)
            throw new BusinessRuleException($"NCC {black.Code} đang ở danh sách đen – không mời báo giá được.");
    }

    /// <summary>NCC trúng thầu không được ở danh sách đen (kiểm tra lúc chọn và lúc duyệt).</summary>
    private Task CheckAwardedSuppliersAsync(Rfq rfq, CancellationToken cancellationToken) =>
        CheckSuppliersAsync(rfq.Lines.Select(l => l.AwardedSupplierId).OfType<Guid>().Distinct().ToList(), cancellationToken);

    private async Task CheckSourcingAsync(Guid? sourcingId, CancellationToken cancellationToken)
    {
        if (sourcingId is { } id && !await _db.SourcingRequests.AnyAsync(s => s.Id == id, cancellationToken))
            throw new NotFoundException("Không tìm thấy phiếu tìm nguồn.");
    }

    /// <summary>Cập nhật tiến độ NCC ứng viên của phiếu tìm nguồn liên quan (design.md mục 8.3).</summary>
    private async Task TrackSourcingAsync(Rfq rfq, IEnumerable<Guid> supplierIds, CandidateStatus status, CancellationToken cancellationToken)
    {
        if (rfq.SourcingId is not { } sourcingId) return;
        var sourcing = await _db.SourcingRequests.Include(s => s.Candidates).SingleOrDefaultAsync(s => s.Id == sourcingId, cancellationToken);
        if (sourcing is null) return;
        foreach (var supplierId in supplierIds) sourcing.TrackRfq(supplierId, status);
    }

    private async Task<RfqDto> ToDtoAsync(Rfq rfq, CancellationToken cancellationToken)
    {
        var itemIds = rfq.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).Select(i => new { i.Id, i.Code, i.Name }).ToDictionaryAsync(i => i.Id, cancellationToken);
        var uomIds = rfq.Lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var supplierIds = rfq.Suppliers.Select(s => s.SupplierId).ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.Code, s.Name, s.Status }, cancellationToken);
        var prLineIds = rfq.Lines.Select(l => l.SourcePrLineId).OfType<Guid>().ToList();
        var prNos = await (
                from l in _db.Set<PurchaseRequestLine>().AsNoTracking()
                join r in _db.PurchaseRequests on l.RequestId equals r.Id
                where prLineIds.Contains(l.Id)
                select new { l.Id, r.No })
            .ToDictionaryAsync(x => x.Id, x => x.No, cancellationToken);
        var orderIds = rfq.Suppliers.Select(s => s.PurchaseOrderId).OfType<Guid>().ToList();
        var orderNos = await _db.PurchaseOrders.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.No, cancellationToken);
        var priceListIds = rfq.Suppliers.Select(s => s.PriceListId).OfType<Guid>().ToList();
        var priceListNos = await _db.SupplierPriceLists.AsNoTracking().Where(p => priceListIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.No, cancellationToken);
        var sourcingNo = rfq.SourcingId is { } sid
            ? await _db.SourcingRequests.AsNoTracking().Where(s => s.Id == sid).Select(s => s.No).SingleOrDefaultAsync(cancellationToken)
            : null;
        var userIds = new[] { rfq.CreatedBy, rfq.AwardedBy }.OfType<Guid>().Distinct().ToList();
        var userNames = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        // Giá quy đổi thấp nhất của từng dòng trên mọi báo giá – để đánh dấu và chấm điểm giá.
        var factor = rfq.Lines.ToDictionary(l => l.Id, l => l.UomFactor);
        var lowest = rfq.Suppliers.SelectMany(s => s.QuotationLines)
            .GroupBy(q => q.RfqLineId)
            .ToDictionary(g => g.Key, g => g.Min(q => q.BaseNetPrice(factor[q.RfqLineId])));
        var weights = rfq.Weights;
        var supplierDtos = rfq.Suppliers.Select(s =>
            {
                var info = suppliers.GetValueOrDefault(s.SupplierId);
                var quoteLines = s.QuotationLines.Select(q =>
                {
                    var basePrice = q.BaseNetPrice(factor[q.RfqLineId]);
                    return new QuotationLineDto(q.RfqLineId, q.UnitPrice, q.DiscountPct, q.VatCode, q.LeadTimeDays, q.Note, basePrice, basePrice == lowest[q.RfqLineId]);
                }).ToList();
                var priceScore = QuotationScoring.PriceScore(quoteLines.Select(q => (lowest[q.RfqLineId], q.BaseNetPrice)).ToList());
                var total = s.HasQuoted
                    ? QuotationScoring.Total(weights, priceScore, s.QualityScore, s.DeliveryScore, s.PaymentScore, s.WarrantyScore)
                    : 0m;
                // Giá trị trúng thầu = Σ SL × đơn giá × (100 − %CK)/100 của các dòng NCC trúng, làm tròn 2 số mỗi dòng.
                var awarded = rfq.Lines.Where(l => l.AwardedSupplierId == s.SupplierId).Sum(l =>
                {
                    var q = s.QuotationLines.Single(x => x.RfqLineId == l.Id);
                    return Math.Round(l.Qty * q.UnitPrice * (100m - q.DiscountPct) / 100m, 2, MidpointRounding.AwayFromZero);
                });
                return new RfqSupplierDto(
                    s.SupplierId, info?.Code ?? "", info?.Name ?? "", info?.Status.ToString() ?? "", s.HasQuoted, s.QuoteDate, s.ValidUntil, s.PaymentTerms,
                    s.QualityScore, s.DeliveryScore, s.PaymentScore, s.WarrantyScore, s.Note, priceScore, total, quoteLines.Count, awarded,
                    s.PurchaseOrderId, s.PurchaseOrderId is { } po ? orderNos.GetValueOrDefault(po) : null, quoteLines,
                    s.PriceListId, s.PriceListId is { } pl ? priceListNos.GetValueOrDefault(pl) : null);
            })
            .OrderBy(s => s.SupplierCode)
            .ToList();
        var lines = rfq.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var item = items.GetValueOrDefault(l.ItemId);
            return new RfqLineDto(
                l.Id, l.LineNo, l.ItemId, item?.Code ?? "(ngoài phạm vi)", item?.Name ?? "", l.UomId, uoms.GetValueOrDefault(l.UomId) ?? "", l.UomFactor, l.Qty,
                l.SourcePrLineId, l.SourcePrLineId is { } p ? prNos.GetValueOrDefault(p) : null, l.Note, l.AwardedSupplierId, l.AwardReason);
        }).ToList();

        string? readOnly = items.Count < itemIds.Count ? "RFQ có mặt hàng ngoài phạm vi nhóm hàng của bạn nên chỉ xem được."
            : !_scope.HasPermission(Permissions.Sourcing.RfqManage) ? "Bạn không có quyền lập, sửa yêu cầu báo giá." : null;
        var conflict = _scope.UserId is { } me && rfq.Status == RfqStatus.Sent
            ? await _approvals.ConflictBlockerAsync(me, rfq.RelatedSupplierIds, cancellationToken) : null;
        var canAward = rfq.Status == RfqStatus.Sent && items.Count == itemIds.Count && _scope.HasPermission(Permissions.Sourcing.RfqAward)
                       && rfq.Suppliers.Any(s => s.HasQuoted) && conflict is null;
        // Người duyệt có thể chỉ có quyền xem + duyệt (không có quyền sửa RFQ) nên nút Duyệt tính riêng, không theo readOnly.
        var canApprove = rfq.IsAwaitingApproval && items.Count == itemIds.Count
            && await _approvals.ApproveBlockerAsync(rfq, await _approvals.CurrentStepAsync(rfq, cancellationToken), cancellationToken) is null;
        var actions = new RfqActions(
            rfq.Status == RfqStatus.Draft && readOnly is null,
            rfq.Status == RfqStatus.Draft && readOnly is null,
            rfq.Status == RfqStatus.Sent && readOnly is null && conflict is null,
            canAward,
            rfq.Status == RfqStatus.Awarded && readOnly is null && _scope.HasPermission(Permissions.Purchase.PoCreate)
                && rfq.Suppliers.Any(s => s.PurchaseOrderId is null && rfq.Lines.Any(l => l.AwardedSupplierId == s.SupplierId)),
            rfq.Status is RfqStatus.Draft or RfqStatus.Sent && readOnly is null,
            readOnly,
            canApprove,
            rfq.IsAwaitingApproval && items.Count == itemIds.Count && _scope.HasPermission(Permissions.Sourcing.RfqAward),
            rfq.Status is RfqStatus.Draft or RfqStatus.Sent && readOnly is null,
            rfq.Status == RfqStatus.Awarded && items.Count == itemIds.Count && _scope.HasPermission(Permissions.Pricing.PriceListManage)
                && rfq.Suppliers.Any(s => s.PriceListId is null && rfq.Lines.Any(l => l.AwardedSupplierId == s.SupplierId)),
            conflict);
        var approvals = await _approvals.HistoryAsync(rfq.DocType, rfq.Id, cancellationToken, rfq);
        return new RfqDto(
            rfq.Id, rfq.No, rfq.Date, rfq.Title, rfq.Deadline, rfq.SourcingId, sourcingNo,
            new RfqWeightsDto(weights.Price, weights.Quality, weights.Delivery, weights.Payment, weights.Warranty), rfq.Note, rfq.Status.ToString(),
            rfq.CreatedAt, rfq.CreatedBy is { } c ? userNames.GetValueOrDefault(c) : null, rfq.SentAt, rfq.AwardedAt,
            await _parameters.RfqMinSuppliersAsync(cancellationToken), lines, supplierDtos, actions, rfq.FewSuppliersReason, rfq.ApprovalAmount,
            rfq.AwardedBy is { } a ? userNames.GetValueOrDefault(a) : null, rfq.RejectReason, approvals,
            rfq.Dossier.Select(d => new RfqDossierItemDto(d.Code, d.Name, d.IsDone, d.Note, d.IsAutomatic)).ToList());
    }

    private static RfqHeader ToHeader(SaveRfqRequest r) =>
        new(r.Date, r.Title, r.Deadline, r.SourcingId, new RfqWeights(r.Weights.Price, r.Weights.Quality, r.Weights.Delivery, r.Weights.Payment, r.Weights.Warranty), r.Note);
}
