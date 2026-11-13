using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Portal;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;
using Proc.Domain.Sourcing;
using Proc.Infrastructure.Documents;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Hủy dây chuyền (lát Z4b, Z4c – design.md mục 20.2 D3, 20.2a, E1–E4). Chuỗi dựng từ phiếu được chọn đi xuống các phiếu tạo ra từ nó còn
/// hiệu lực, rồi hủy theo thứ tự ngược (phiếu cuối trước) trong một giao dịch có khóa dòng:
/// <list type="bullet">
/// <item>PR → bảng tổng hợp 2A/2B chứa dòng của PR → các PO có dòng lấy từ PR hoặc từ dòng của các bảng đó → phiếu xác nhận nhận hàng (XNH)
/// của các PO (lát Y5c).</item>
/// <item>Bảng tổng hợp (BTH) → các PO có dòng lấy từ dòng của bảng → XNH (lát Y5c). RFQ chưa hủy hỏi giá dòng của bảng thì chặn – hủy RFQ
/// trước (RFQ có chuỗi riêng: PO, hợp đồng, bảng giá).</item>
/// <item>PO → XNH. XNH → chính nó.</item>
/// <item>Đặt hàng gộp (DHG) đã tạo PO → các PO ở mọi công ty → XNH (E1).</item>
/// <item>RFQ → các PO tạo từ RFQ → XNH; hợp đồng, bảng giá tạo từ RFQ chỉ hủy kèm khi còn Nháp, đã duyệt/hiệu lực thì chặn (E2, E3).</item>
/// </list>
/// Người dùng chốt 11/11/2026: chuỗi đã có XNH <b>đã xác nhận</b> (hàng đã về) thì <b>chặn hủy</b> – chỉ điều chỉnh SL thực nhận hoặc đóng
/// phần còn lại; XNH nháp vẫn hủy theo chuỗi.
/// </summary>
internal sealed class CancelChainService : ICancelChainService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;
    private readonly ApprovalEngine _approvals;
    private readonly IPortalEmailService _portalEmails;

    /// <summary>Tạo service (ApprovalEngine tự tạo như các service phiếu khác – không đăng ký DI riêng).</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="portalEmails">Thư báo NCC khi RFQ đã gửi (lát P3b-3) / PO đã duyệt (lát P4a) bị hủy theo dây chuyền.</param>
    public CancelChainService(ProcDbContext db, ICurrentUserScope scope, IClock clock, IPortalEmailService portalEmails)
    {
        _portalEmails = portalEmails;
        _db = db;
        _scope = scope;
        _clock = clock;
        _approvals = new ApprovalEngine(db, scope, clock);
    }

    /// <summary>Chuỗi đã nạp (entity đang theo dõi). Thứ tự hủy: XNH → PO → hợp đồng → bảng giá → bảng tổng hợp → phiếu gốc.</summary>
    private sealed class Chain
    {
        public List<GoodsReceiptConfirmation> Receipts { get; } = [];

        /// <summary>Đề nghị thanh toán (lát Y8) – cuối chuỗi; chỉ khi chọn hủy chính nó (chuỗi có XNH đã xác nhận đã bị chặn).</summary>
        public PaymentRequest? Payment { get; set; }
        public List<PurchaseOrder> Orders { get; } = [];
        public List<PurchaseContract> Contracts { get; } = [];
        public List<SupplierPriceList> PriceLists { get; } = [];
        public List<PurchaseSummary> Summaries { get; } = [];
        public PurchaseRequest? Request { get; set; }
        public PooledOrder? Pooled { get; set; }
        public Rfq? Rfq { get; set; }
        public List<string> Warnings { get; } = [];

        /// <summary>Lý do chặn không phải quyền (phiếu sau đã duyệt/hiệu lực…).</summary>
        public List<string> Blocks { get; } = [];
    }

    /// <inheritdoc />
    public async Task<CancelChainDto> PreviewAsync(string docType, Guid id, CancellationToken cancellationToken) =>
        ToDto(await BuildAsync(docType, id, cancellationToken));

    /// <inheritdoc />
    public async Task<CancelChainDto> CancelAsync(string docType, Guid id, CancelChainRequest request, CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason)) throw new BusinessRuleException("Hãy nhập lý do hủy.");

        // Một giao dịch cho cả chuỗi (D3): một phiếu lỗi thì không hủy phiếu nào. Khóa phiếu gốc trước khi dựng chuỗi để phiếu sau lập cùng lúc
        // (lập PO từ PR, tạo PO từ DHG/RFQ, xác nhận nhận hàng) phải chờ – chuỗi dựng ra là đầy đủ.
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        await LockRootAsync(docType, id, cancellationToken);
        var chain = await BuildAsync(docType, id, cancellationToken);
        await RowLocks.LockAsync<PurchaseOrder>(_db, chain.Orders.Select(o => o.Id), cancellationToken);
        var preview = ToDto(chain);
        if (Blocking(chain) is { } block) throw new BusinessRuleException(block);
        if (preview.BlockedReason is { } denied) throw new ForbiddenAccessException(denied);

        // Chỉ còn XNH nháp, hợp đồng/bảng giá nháp trong chuỗi (phiếu đã xác nhận/duyệt bị chặn ở trên).
        if (chain.Payment is { } payment)
        {
            payment.Cancel(reason);
            await _approvals.SkipPendingAsync(payment, cancellationToken);
        }

        foreach (var receipt in chain.Receipts) receipt.Cancel(reason);
        foreach (var order in chain.Orders)
        {
            // PO đã duyệt thì NCC đã thấy trên Portal / đã nhận thư đơn hàng – báo hủy, không ghi lý do (lát P4a, P4-5).
            var wasIssued = order.Status == ApprovalDocumentStatus.Approved;
            order.Cancel(reason, inChain: true);
            await _approvals.SkipPendingAsync(order, cancellationToken);
            if (wasIssued) await _portalEmails.QueuePurchaseOrderAsync(order, SupplierPortalEmailKind.PurchaseOrderCancelled, null, cancellationToken);
        }

        foreach (var contract in chain.Contracts) contract.Cancel(reason);
        foreach (var list in chain.PriceLists) list.Cancel(reason);
        foreach (var summary in chain.Summaries)
        {
            summary.Cancel(reason, inChain: true);
            await _approvals.SkipPendingAsync(summary, cancellationToken);
        }

        if (chain.Request is { } pr)
        {
            pr.Cancel(reason, inChain: true);
            await _approvals.SkipPendingAsync(pr, cancellationToken);
        }

        chain.Pooled?.Cancel(_clock.TodayInVietnam(), reason, inChain: true);
        if (chain.Rfq is { } rfq)
        {
            var wasSent = rfq.Status != RfqStatus.Draft;
            rfq.Cancel(reason, inChain: true);
            await _approvals.SkipPendingAsync(rfq, cancellationToken);
            if (wasSent) await _portalEmails.QueueRfqAsync(rfq, SupplierPortalEmailKind.RfqCancelled, null, null, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return preview;
    }

    /// <summary>Khóa dòng phiếu được chọn trong giao dịch đang mở.</summary>
    private Task LockRootAsync(string docType, Guid id, CancellationToken cancellationToken) => Normalize(docType) switch
    {
        DocumentTypes.PurchaseRequest => RowLocks.LockAsync<PurchaseRequest>(_db, [id], cancellationToken),
        DocumentTypes.PurchaseOrder => RowLocks.LockAsync<PurchaseOrder>(_db, [id], cancellationToken),
        DocumentTypes.PurchaseSummary => RowLocks.LockAsync<PurchaseSummary>(_db, [id], cancellationToken),
        DocumentTypes.PaymentRequest => RowLocks.LockAsync<PaymentRequest>(_db, [id], cancellationToken),
        PooledType => RowLocks.LockAsync<PooledOrder>(_db, [id], cancellationToken),
        DocumentTypes.RequestForQuotation => RowLocks.LockAsync<Rfq>(_db, [id], cancellationToken),
        _ => Task.CompletedTask,
    };

    /// <summary>Mã loại phiếu đặt hàng gộp (mã màn hình DHG – design.md mục 18.25 G7).</summary>
    private const string PooledType = "DHG";

    /// <summary>Mã loại phiếu viết hoa, bỏ khoảng trắng.</summary>
    private static string Normalize(string docType) => (docType ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Dựng chuỗi từ phiếu được chọn đi xuống các phiếu sau còn hiệu lực.</summary>
    private async Task<Chain> BuildAsync(string docType, Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var chain = new Chain();
        switch (Normalize(docType))
        {
            case DocumentTypes.PurchaseRequest:
            {
                var pr = await _db.PurchaseRequests.Include(r => r.Lines).SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy đề nghị mua hàng.");
                if (pr.Status == ApprovalDocumentStatus.Cancelled) throw new BusinessRuleException($"Phiếu {pr.No} đã hủy.");
                var lineIds = pr.Lines.Select(l => l.Id).ToList();
                var orderIds = await _db.Set<PurchaseOrderLine>()
                    .Where(l => l.SourcePrLineId != null && lineIds.Contains(l.SourcePrLineId.Value)).Select(l => l.OrderId).Distinct()
                    .ToListAsync(cancellationToken);
                chain.Request = pr;
                // Bảng tổng hợp 2A/2B chứa dòng của PR (lát Y5c): hủy kèm cả bảng – kéo theo PO lấy từ mọi dòng của bảng (kể cả dòng PR khác).
                var summaryIds = await _db.Set<PurchaseSummaryLine>().Where(l => l.RequestId == pr.Id).Select(l => l.SummaryId).Distinct()
                    .ToListAsync(cancellationToken);
                orderIds = [.. orderIds.Union(await AddSummariesAsync(chain, summaryIds, cancellationToken))];
                var otherIds = chain.Summaries.SelectMany(s => s.Lines).Select(l => l.RequestId).Where(r => r != pr.Id).Distinct().ToList();
                var others = await _db.PurchaseRequests.Where(r => otherIds.Contains(r.Id)).Select(r => r.No).OrderBy(n => n).ToListAsync(cancellationToken);
                if (others.Count > 0)
                    chain.Warnings.Add($"Bảng tổng hợp trong chuỗi có dòng của đề nghị khác: {string.Join(", ", others)} – đề nghị không bị hủy, dòng của " +
                                       "chúng trở lại chưa qua bảng tổng hợp để lập bảng khác.");
                await AddOrdersAsync(chain, orderIds, allCompanies: false, cancellationToken);
                await WarnOtherRequestsAsync(chain, pr.Id, cancellationToken);
                break;
            }
            case DocumentTypes.PurchaseSummary:
            {
                var no = await _db.PurchaseSummaries.Where(s => s.Id == id).Select(s => new { s.No, s.Status }).SingleOrDefaultAsync(cancellationToken)
                         ?? throw new NotFoundException("Không tìm thấy bảng tổng hợp đề xuất mua.");
                if (no.Status == ApprovalDocumentStatus.Cancelled) throw new BusinessRuleException($"Phiếu {no.No} đã hủy.");
                await AddOrdersAsync(chain, await AddSummariesAsync(chain, [id], cancellationToken), allCompanies: false, cancellationToken);
                await WarnOtherRequestsAsync(chain, null, cancellationToken);
                break;
            }
            case DocumentTypes.PurchaseOrder:
            {
                await AddOrdersAsync(chain, [id], allCompanies: false, cancellationToken);
                if (chain.Orders.Count == 0)
                {
                    // Không có trong danh sách chưa hủy: hoặc không thấy, hoặc đã hủy.
                    var no = await _db.PurchaseOrders.Where(o => o.Id == id).Select(o => o.No).SingleOrDefaultAsync(cancellationToken)
                             ?? throw new NotFoundException("Không tìm thấy đơn mua hàng.");
                    throw new BusinessRuleException($"Phiếu {no} đã hủy.");
                }

                await WarnOtherRequestsAsync(chain, null, cancellationToken);
                break;
            }
            case DocumentTypes.GoodsReceiptConfirmation:
            {
                var receipt = await _db.GoodsReceiptConfirmations.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy phiếu xác nhận nhận hàng.");
                if (receipt.Status == ReceiptConfirmationStatus.Cancelled) throw new BusinessRuleException($"Phiếu {receipt.No} đã hủy.");
                chain.Receipts.Add(receipt);
                break;
            }
            case DocumentTypes.PaymentRequest:
            {
                // Đề nghị thanh toán (lát Y8) là phiếu cuối – chuỗi chỉ có chính nó; đã chi thì chặn (Blocking).
                var payment = await _db.PaymentRequests.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy đề nghị thanh toán.");
                if (payment.Status == ApprovalDocumentStatus.Cancelled) throw new BusinessRuleException($"Phiếu {payment.No} đã hủy.");
                chain.Payment = payment;
                break;
            }
            case PooledType:
            {
                var pooled = await _db.PooledOrders.Include(p => p.Companies).SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy phiếu đặt hàng gộp.");
                if (pooled.Status == PooledOrderStatus.Cancelled) throw new BusinessRuleException($"Phiếu {pooled.No} đã hủy.");
                chain.Pooled = pooled;
                // PO của từng công ty thành viên (E1): chỉ bỏ filter công ty để lấy đúng các PO phiếu đã tạo.
                await AddOrdersAsync(chain, pooled.Companies.Select(c => c.PurchaseOrderId).OfType<Guid>().ToList(), allCompanies: true, cancellationToken);
                break;
            }
            case DocumentTypes.RequestForQuotation:
            {
                var rfq = await _db.Rfqs.Include(r => r.Suppliers).SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy yêu cầu báo giá.");
                if (rfq.Status == RfqStatus.Cancelled) throw new BusinessRuleException($"RFQ {rfq.No} đã hủy.");
                chain.Rfq = rfq;
                await AddOrdersAsync(chain, rfq.Suppliers.Select(s => s.PurchaseOrderId).OfType<Guid>().ToList(), allCompanies: false, cancellationToken);
                await AddRfqContractsAndPriceListsAsync(chain, rfq, cancellationToken);
                break;
            }
            default:
                throw new BusinessRuleException(
                    $"Loại phiếu {docType} chưa hỗ trợ hủy dây chuyền (PR, bảng tổng hợp, PO, phiếu xác nhận nhận hàng, đề nghị thanh toán, đặt hàng gộp, RFQ).");
        }

        return chain;
    }

    /// <summary>
    /// Nạp các bảng tổng hợp chưa hủy vào chuỗi; RFQ chưa hủy hỏi giá dòng của bảng thì chặn (hủy RFQ trước – RFQ có chuỗi riêng).
    /// </summary>
    /// <returns>Id các PO có dòng lấy từ dòng đề nghị của các bảng.</returns>
    private async Task<List<Guid>> AddSummariesAsync(Chain chain, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return [];
        chain.Summaries.AddRange(await _db.PurchaseSummaries.Include(s => s.Lines)
            .Where(s => ids.Contains(s.Id) && s.Status != ApprovalDocumentStatus.Cancelled)
            .OrderByDescending(s => s.No).ToListAsync(cancellationToken));
        var prLineIds = chain.Summaries.SelectMany(s => s.Lines).Select(l => l.RequestLineId).Distinct().ToList();
        if (prLineIds.Count == 0) return [];
        var rfqs = await (
                from rl in _db.Set<RfqLine>()
                join r in _db.Rfqs on rl.RfqId equals r.Id
                where rl.SourcePrLineId != null && prLineIds.Contains(rl.SourcePrLineId.Value) && r.Status != RfqStatus.Cancelled
                select r.No)
            .Distinct().OrderBy(n => n).ToListAsync(cancellationToken);
        if (rfqs.Count > 0)
            chain.Blocks.Add($"RFQ {string.Join(", ", rfqs)} đang hỏi giá dòng của bảng tổng hợp {string.Join(", ", chain.Summaries.Select(s => s.No))} – hủy RFQ trước");
        return await _db.Set<PurchaseOrderLine>()
            .Where(l => l.SourcePrLineId != null && prLineIds.Contains(l.SourcePrLineId.Value)).Select(l => l.OrderId).Distinct()
            .ToListAsync(cancellationToken);
    }

    /// <summary>Nạp các PO chưa hủy (kèm dòng) và XNH chưa hủy của chúng vào chuỗi.</summary>
    /// <param name="chain">Chuỗi.</param>
    /// <param name="ids">Id PO.</param>
    /// <param name="allCompanies">PO ở nhiều công ty (đặt hàng gộp) – bỏ filter công ty, chỉ lấy đúng Id đã ghi trên phiếu gốc.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    private async Task AddOrdersAsync(Chain chain, IReadOnlyCollection<Guid> ids, bool allCompanies, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return;
        var orders = allCompanies ? _db.PurchaseOrders.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) : _db.PurchaseOrders;
        chain.Orders.AddRange(await orders.Include(o => o.Lines)
            .Where(o => ids.Contains(o.Id) && o.Status != ApprovalDocumentStatus.Cancelled)
            .OrderByDescending(o => o.No).ToListAsync(cancellationToken));
        var orderIds = chain.Orders.Select(o => o.Id).ToList();
        if (orderIds.Count == 0) return;
        var receipts = allCompanies ? _db.GoodsReceiptConfirmations.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) : _db.GoodsReceiptConfirmations;
        chain.Receipts.AddRange(await receipts
            .Where(c => orderIds.Contains(c.PurchaseOrderId) && c.Status != ReceiptConfirmationStatus.Cancelled)
            .OrderByDescending(c => c.No).ToListAsync(cancellationToken));
    }

    /// <summary>
    /// Hợp đồng, bảng giá tạo từ RFQ (E2): còn Nháp thì hủy kèm; đã gửi duyệt / hiệu lực thì chặn – hợp đồng hiệu lực dùng "Chấm dứt", bảng giá
    /// hiệu lực dùng "Ngừng áp dụng" (E3), vì PO đã có thể lấy giá theo chúng.
    /// </summary>
    private async Task AddRfqContractsAndPriceListsAsync(Chain chain, Rfq rfq, CancellationToken cancellationToken)
    {
        var contractIds = rfq.Suppliers.Select(s => s.ContractId).OfType<Guid>().ToList();
        foreach (var contract in await _db.PurchaseContracts.Where(c => contractIds.Contains(c.Id) && c.Status != ContractStatus.Cancelled)
                     .OrderByDescending(c => c.No).ToListAsync(cancellationToken))
        {
            if (contract.Status == ContractStatus.Draft) chain.Contracts.Add(contract);
            else chain.Blocks.Add($"hợp đồng {contract.No} tạo từ RFQ đã gửi duyệt / hiệu lực – xử lý riêng (Chấm dứt hợp đồng)");
        }

        var listIds = rfq.Suppliers.Select(s => s.PriceListId).OfType<Guid>().ToList();
        foreach (var list in await _db.SupplierPriceLists.IgnoreQueryFilters([ProcDbContext.CompanyFilter])
                     .Where(p => listIds.Contains(p.Id) && p.Status != PriceListStatus.Cancelled)
                     .OrderByDescending(p => p.No).ToListAsync(cancellationToken))
        {
            if (list.Status == PriceListStatus.Draft) chain.PriceLists.Add(list);
            else chain.Blocks.Add($"bảng giá {list.No} tạo từ RFQ đã gửi duyệt / hiệu lực – xử lý riêng (Ngừng áp dụng)");
        }
    }

    /// <summary>
    /// PO gộp dòng từ nhiều PR (D3): hủy PO thì hủy cả PO – PR nguồn ngoài PR đang hủy không bị hủy nhưng SL của chúng trở về "còn lại chưa
    /// đặt"; hộp thoại nêu rõ để người hủy biết.
    /// </summary>
    private async Task WarnOtherRequestsAsync(Chain chain, Guid? exceptRequestId, CancellationToken cancellationToken)
    {
        var prLineIds = chain.Orders.SelectMany(o => o.Lines).Select(l => l.SourcePrLineId).OfType<Guid>().Distinct().ToList();
        if (prLineIds.Count == 0) return;
        var others = await (
                from l in _db.Set<PurchaseRequestLine>()
                join r in _db.PurchaseRequests on l.RequestId equals r.Id
                where prLineIds.Contains(l.Id) && r.Id != exceptRequestId
                select r.No)
            .Distinct().OrderBy(n => n).ToListAsync(cancellationToken);
        if (others.Count > 0)
            chain.Warnings.Add((exceptRequestId is null ? $"PO có dòng lấy từ PR {string.Join(", ", others)}" : $"PO trong chuỗi có dòng lấy từ PR khác: {string.Join(", ", others)}")
                               + " – PR không bị hủy, SL đã đặt trở về \"còn lại chưa đặt\" để lập PO khác.");
    }

    /// <summary>Đổi chuỗi sang DTO kèm kiểm tra quyền từng phiếu (design.md mục 20.2a).</summary>
    private CancelChainDto ToDto(Chain chain)
    {
        var items = new List<CancelChainItemDto>();
        var denied = new List<string>();
        var orderNos = chain.Orders.ToDictionary(o => o.Id, o => o.No);
        var origin = chain.Request?.No ?? chain.Pooled?.No ?? chain.Rfq?.No;

        void Add(string docType, string typeName, Guid docId, string no, string status, bool isDraft, Guid? createdBy, string? note)
        {
            items.Add(new CancelChainItemDto(items.Count + 1, docType, typeName, docId, no, DocumentRegistryService.StatusName(docType, status), note));
            if (!DocumentCancelPolicy.CanCancel(_scope, isDraft, createdBy)) denied.Add(no);
        }

        if (chain.Payment is { } p0)
            Add(DocumentTypes.PaymentRequest, "Đề nghị thanh toán", p0.Id, p0.No, p0.Status.ToString(), p0.Status == ApprovalDocumentStatus.Draft,
                p0.CreatedBy, null);
        foreach (var c in chain.Receipts)
            Add(DocumentTypes.GoodsReceiptConfirmation, "Xác nhận nhận hàng", c.Id, c.No, c.Status.ToString(),
                c.Status == ReceiptConfirmationStatus.Draft, c.CreatedBy, $"Nhận hàng của {orderNos.GetValueOrDefault(c.PurchaseOrderId, "PO")}");
        foreach (var o in chain.Orders)
            Add(DocumentTypes.PurchaseOrder, "Đơn mua hàng", o.Id, o.No, o.Status.ToString(), o.Status == ApprovalDocumentStatus.Draft, o.CreatedBy,
                origin is null ? null : chain.Request is not null ? $"Có dòng lấy từ {origin}" : $"Tạo từ {origin}");
        foreach (var c in chain.Contracts)
            Add(DocumentTypes.Contract, "Hợp đồng nguyên tắc", c.Id, c.No, c.Status.ToString(), true, c.CreatedBy, $"Tạo từ {origin}");
        foreach (var p in chain.PriceLists)
            Add("PRL", "Bảng giá NCC", p.Id, p.No, p.Status.ToString(), true, p.CreatedBy, $"Tạo từ {origin}");
        foreach (var s in chain.Summaries)
            Add(DocumentTypes.PurchaseSummary, "Bảng tổng hợp đề xuất mua", s.Id, s.No, s.Status.ToString(), s.Status == ApprovalDocumentStatus.Draft,
                s.CreatedBy, chain.Request is { } source ? $"Có dòng của {source.No}" : null);
        if (chain.Request is { } request)
            Add(DocumentTypes.PurchaseRequest, "Đề nghị mua hàng", request.Id, request.No, request.Status.ToString(),
                request.Status == ApprovalDocumentStatus.Draft, request.CreatedBy, null);
        if (chain.Pooled is { } pooled)
            Add(PooledType, "Đặt hàng gộp", pooled.Id, pooled.No, pooled.Status.ToString(), pooled.Status == PooledOrderStatus.Draft, pooled.CreatedBy,
                chain.Orders.Count > 0 ? $"Đã tạo {chain.Orders.Count} PO ở các công ty" : null);
        if (chain.Rfq is { } rfq)
            Add(DocumentTypes.RequestForQuotation, "Yêu cầu báo giá", rfq.Id, rfq.No, rfq.Status.ToString(), rfq.Status == RfqStatus.Draft, rfq.CreatedBy, null);

        var blocked = Blocking(chain) ?? (denied.Count == 0
            ? null
            : $"Bạn không đủ quyền hủy {string.Join(", ", denied)}: phiếu đã gửi duyệt trở đi chỉ quản trị (quyền Hủy phiếu) hủy được; phiếu nháp " +
              "chỉ người lập (design.md mục 20.2a).");
        return new CancelChainDto(items, chain.Warnings, blocked);
    }

    /// <summary>
    /// Lý do chặn không phải quyền: XNH đã xác nhận (người dùng chốt 11/11/2026 – hàng đã về thì chỉ điều chỉnh), hợp đồng / bảng giá tạo từ
    /// RFQ đã duyệt (E2).
    /// </summary>
    /// <returns>Câu báo chặn; null khi không có.</returns>
    private static string? Blocking(Chain chain)
    {
        // Đã chi là tiền đã ra khỏi công ty – không hủy đề nghị (lát Y8).
        if (chain.Payment is { IsPaid: true } paid)
            return $"Đề nghị {paid.No} đã ghi Đã chi ngày {paid.PaidDate:dd/MM/yyyy} – không hủy được.";
        var confirmed = chain.Receipts.Where(c => c.Status == ReceiptConfirmationStatus.Confirmed).Select(c => c.No).ToList();
        if (confirmed.Count > 0)
            return $"Chuỗi đã có phiếu nhận hàng đã xác nhận {string.Join(", ", confirmed)} – không hủy được; dùng \"Điều chỉnh SL thực nhận\" trên " +
                   "phiếu nhận hàng hoặc \"Đóng phần còn lại\" trên PO.";
        return chain.Blocks.Count > 0 ? $"Không hủy được: {string.Join("; ", chain.Blocks)}." : null;
    }
}
