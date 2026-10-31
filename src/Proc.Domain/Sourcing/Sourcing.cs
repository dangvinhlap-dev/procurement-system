using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Pricing;

namespace Proc.Domain.Sourcing;

/// <summary>Trạng thái phiếu tìm nguồn.</summary>
public enum SourcingStatus
{
    /// <summary>Đang tìm.</summary>
    Open = 1,

    /// <summary>Đã xong (đã chọn được NCC hoặc dừng tìm).</summary>
    Closed = 2,

    /// <summary>Đã hủy.</summary>
    Cancelled = 3,
}

/// <summary>Trạng thái NCC ứng viên (design.md mục 8.3).</summary>
public enum CandidateStatus
{
    /// <summary>Đã liên hệ.</summary>
    Contacted = 1,

    /// <summary>Đã gửi RFQ.</summary>
    RfqSent = 2,

    /// <summary>Đã báo giá.</summary>
    Quoted = 3,

    /// <summary>Đạt.</summary>
    Passed = 4,

    /// <summary>Không đạt.</summary>
    Failed = 5,
}

/// <summary>Đầu phiếu tìm nguồn.</summary>
/// <param name="Date">Ngày lập.</param>
/// <param name="ItemId">Mặt hàng (null nếu chưa có mã – dùng mô tả).</param>
/// <param name="Description">Mô tả hàng cần tìm.</param>
/// <param name="Specification">Yêu cầu kỹ thuật.</param>
/// <param name="Qty">Số lượng dự kiến (theo ĐVT cơ sở nếu có mã hàng).</param>
/// <param name="Deadline">Hạn chót.</param>
/// <param name="Responsible">Người phụ trách.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record SourcingHeader(
    DateOnly Date, Guid? ItemId, string? Description, string? Specification, decimal? Qty, DateOnly? Deadline, string? Responsible, string? Note);

/// <summary>Dữ liệu một NCC ứng viên.</summary>
public sealed record SourcingCandidateData(Guid? Id, Guid SupplierId, CandidateStatus Status, string? Note);

/// <summary>
/// Danh sách tìm nguồn (design.md mục 8.3): phiếu tìm NCC cho một sản phẩm, kèm các NCC ứng viên và trạng thái làm việc với từng NCC.
/// NCC chưa có trong danh mục thì thêm ở danh mục NCC (trạng thái Tiềm năng) rồi chọn vào đây.
/// </summary>
[Auditable("Danh sách tìm nguồn", LabelProperty = nameof(No))]
public sealed class SourcingRequest : AuditableEntity, ICompanyOwned, IHasDocumentNumber
{
    private readonly List<SourcingCandidate> _candidates = [];

    private SourcingRequest()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Số phiếu.</summary>
    public string No { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string? DocumentNumber => No;

    /// <summary>Ngày lập.</summary>
    public DateOnly Date { get; private set; }

    /// <summary>Mặt hàng (null = hàng chưa có mã).</summary>
    public Guid? ItemId { get; private set; }

    /// <summary>Mô tả.</summary>
    public string? Description { get; private set; }

    /// <summary>Yêu cầu kỹ thuật.</summary>
    public string? Specification { get; private set; }

    /// <summary>Số lượng dự kiến – numeric(18,4).</summary>
    public decimal? Qty { get; private set; }

    /// <summary>Hạn chót.</summary>
    public DateOnly? Deadline { get; private set; }

    /// <summary>Người phụ trách.</summary>
    public string? Responsible { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Trạng thái.</summary>
    public SourcingStatus Status { get; private set; } = SourcingStatus.Open;

    /// <summary>NCC ứng viên.</summary>
    public IReadOnlyList<SourcingCandidate> Candidates => _candidates;

    /// <summary>Lập phiếu tìm nguồn.</summary>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, số, hoặc không có cả mặt hàng lẫn mô tả.</exception>
    public static SourcingRequest Create(Guid companyId, string no, SourcingHeader header)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Phiếu tìm nguồn phải thuộc một công ty.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Phiếu phải có số.");
        var request = new SourcingRequest { CompanyId = companyId, No = no };
        request.Update(header);
        return request;
    }

    /// <summary>Sửa đầu phiếu (khi đang tìm).</summary>
    /// <exception cref="BusinessRuleException">Khi phiếu đã đóng/hủy hoặc dữ liệu sai.</exception>
    public void Update(SourcingHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureOpen();
        if (header.ItemId is null && string.IsNullOrWhiteSpace(header.Description))
            throw new BusinessRuleException("Hãy chọn mặt hàng hoặc nhập mô tả hàng cần tìm.");
        if (header.Qty is { } qty && (qty <= 0 || decimal.Round(qty, 4) != qty))
            throw new BusinessRuleException("Số lượng dự kiến phải lớn hơn 0, tối đa 4 chữ số thập phân.");
        Date = header.Date;
        ItemId = header.ItemId;
        Description = Clean(header.Description);
        Specification = Clean(header.Specification);
        Qty = header.Qty;
        Deadline = header.Deadline;
        Responsible = Clean(header.Responsible);
        Note = Clean(header.Note);
    }

    /// <summary>Thay danh sách NCC ứng viên; mỗi NCC một dòng.</summary>
    /// <exception cref="BusinessRuleException">Khi phiếu đã đóng/hủy hoặc trùng NCC.</exception>
    public void SetCandidates(IReadOnlyList<SourcingCandidateData> candidates)
    {
        EnsureOpen();
        if (candidates.GroupBy(c => c.SupplierId).Any(g => g.Count() > 1)) throw new BusinessRuleException("Mỗi NCC chỉ có một dòng ứng viên.");
        var keep = new List<SourcingCandidate>();
        foreach (var data in candidates)
        {
            var candidate = data.Id is { } id
                ? _candidates.FirstOrDefault(c => c.Id == id) ?? throw new BusinessRuleException("Không tìm thấy dòng NCC cần sửa – hãy tải lại phiếu.")
                : AddCandidate(data.SupplierId);
            candidate.Set(data.SupplierId, data.Status, data.Note);
            keep.Add(candidate);
        }

        _candidates.RemoveAll(c => !keep.Contains(c));
    }

    /// <summary>
    /// Ghi nhận tiến độ từ RFQ: NCC đã gửi RFQ / đã báo giá. Chỉ tiến lên (không lùi trạng thái Đạt/Không đạt do người dùng chấm),
    /// NCC chưa có trong danh sách thì thêm vào.
    /// </summary>
    /// <param name="supplierId">NCC.</param>
    /// <param name="status">RfqSent hoặc Quoted.</param>
    public void TrackRfq(Guid supplierId, CandidateStatus status)
    {
        if (Status != SourcingStatus.Open) return;
        var candidate = _candidates.FirstOrDefault(c => c.SupplierId == supplierId) ?? AddCandidate(supplierId);
        if (candidate.Status < status && candidate.Status < CandidateStatus.Passed) candidate.Set(supplierId, status, candidate.Note);
    }

    /// <summary>Đóng phiếu (đã xong).</summary>
    /// <exception cref="BusinessRuleException">Khi phiếu không còn mở.</exception>
    public void Close()
    {
        EnsureOpen();
        Status = SourcingStatus.Closed;
    }

    /// <summary>Hủy phiếu.</summary>
    /// <exception cref="BusinessRuleException">Khi phiếu không còn mở.</exception>
    public void Cancel()
    {
        EnsureOpen();
        Status = SourcingStatus.Cancelled;
    }

    private SourcingCandidate AddCandidate(Guid supplierId)
    {
        var candidate = new SourcingCandidate(Id, supplierId);
        _candidates.Add(candidate);
        return candidate;
    }

    private void EnsureOpen()
    {
        if (Status != SourcingStatus.Open) throw new BusinessRuleException($"Phiếu {No} đã đóng hoặc đã hủy – không sửa được.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Một NCC ứng viên của phiếu tìm nguồn.</summary>
[Auditable("NCC ứng viên")]
public sealed class SourcingCandidate : Entity
{
    private SourcingCandidate()
    {
    }

    internal SourcingCandidate(Guid sourcingId, Guid supplierId)
    {
        SourcingId = sourcingId;
        SupplierId = supplierId;
        Status = CandidateStatus.Contacted;
    }

    /// <summary>Phiếu tìm nguồn.</summary>
    public Guid SourcingId { get; private set; }

    /// <summary>NCC.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Trạng thái.</summary>
    public CandidateStatus Status { get; private set; }

    /// <summary>Ghi chú (hồ sơ năng lực, nhận xét).</summary>
    public string? Note { get; private set; }

    internal void Set(Guid supplierId, CandidateStatus status, string? note)
    {
        if (!Enum.IsDefined(status)) throw new BusinessRuleException("Trạng thái NCC ứng viên không hợp lệ.");
        SupplierId = supplierId;
        Status = status;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}

/// <summary>Trạng thái RFQ.</summary>
public enum RfqStatus
{
    /// <summary>Nháp.</summary>
    Draft = 1,

    /// <summary>Đã gửi NCC – đang nhận báo giá.</summary>
    Sent = 2,

    /// <summary>Đã chọn thầu.</summary>
    Awarded = 3,

    /// <summary>Đã hủy.</summary>
    Cancelled = 4,
}

/// <summary>Trọng số tiêu chí đánh giá (%), tổng 100 (design.md mục 8.4 bước 1).</summary>
/// <param name="Price">Giá.</param>
/// <param name="Quality">Chất lượng.</param>
/// <param name="Delivery">Thời gian giao.</param>
/// <param name="Payment">Điều kiện thanh toán.</param>
/// <param name="Warranty">Bảo hành.</param>
public sealed record RfqWeights(int Price, int Quality, int Delivery, int Payment, int Warranty)
{
    /// <summary>Mặc định: giá 60, chất lượng 20, giao hàng 10, thanh toán 5, bảo hành 5.</summary>
    public static RfqWeights Default { get; } = new(60, 20, 10, 5, 5);

    /// <summary>Tổng trọng số.</summary>
    public int Total => Price + Quality + Delivery + Payment + Warranty;
}

/// <summary>Đầu RFQ.</summary>
public sealed record RfqHeader(DateOnly Date, string Title, DateOnly Deadline, Guid? SourcingId, RfqWeights Weights, string? Note);

/// <summary>Dữ liệu một dòng RFQ.</summary>
public sealed record RfqLineData(Guid? Id, Guid ItemId, Guid UomId, decimal Qty, Guid? SourcePrLineId, string? Note);

/// <summary>Báo giá của một NCC (đầu).</summary>
/// <param name="QuoteDate">Ngày báo giá.</param>
/// <param name="ValidUntil">Hiệu lực báo giá đến.</param>
/// <param name="PaymentTerms">Điều kiện thanh toán.</param>
/// <param name="QualityScore">Điểm chất lượng 0–10.</param>
/// <param name="DeliveryScore">Điểm giao hàng 0–10.</param>
/// <param name="PaymentScore">Điểm thanh toán 0–10.</param>
/// <param name="WarrantyScore">Điểm bảo hành 0–10.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record QuotationHeader(
    DateOnly QuoteDate, DateOnly? ValidUntil, string? PaymentTerms, decimal QualityScore, decimal DeliveryScore, decimal PaymentScore,
    decimal WarrantyScore, string? Note);

/// <summary>Báo giá cho một dòng RFQ.</summary>
public sealed record QuotationLineData(Guid RfqLineId, decimal UnitPrice, decimal DiscountPct, string? VatCode, int? LeadTimeDays, string? Note);

/// <summary>Chọn thầu một dòng RFQ.</summary>
/// <param name="RfqLineId">Dòng RFQ.</param>
/// <param name="SupplierId">NCC trúng thầu dòng này; null = không trao dòng này.</param>
/// <param name="Reason">Lý do (bắt buộc khi không chọn giá thấp nhất).</param>
public sealed record AwardLineData(Guid RfqLineId, Guid? SupplierId, string? Reason);

/// <summary>
/// Yêu cầu báo giá / đấu thầu (design.md mục 8.4): danh sách mặt hàng, hạn nộp, trọng số tiêu chí; gửi cho đủ số NCC tối thiểu
/// (tham số công ty <see cref="Organization.CompanyParameter.RfqMinSuppliers"/>); nhập báo giá từng NCC; chọn NCC trúng thầu theo dòng; từ kết quả sinh PO nháp.
/// Vòng đời: Nháp → Đã gửi → Đã chọn thầu; hủy được khi Nháp/Đã gửi.
/// </summary>
[Auditable("Yêu cầu báo giá", LabelProperty = nameof(No))]
public sealed class Rfq : AuditableEntity, ICompanyOwned, IHasDocumentNumber
{
    private readonly List<RfqLine> _lines = [];
    private readonly List<RfqSupplier> _suppliers = [];

    private Rfq()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Số phiếu.</summary>
    public string No { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string? DocumentNumber => No;

    /// <summary>Ngày lập.</summary>
    public DateOnly Date { get; private set; }

    /// <summary>Tiêu đề.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Hạn nộp báo giá.</summary>
    public DateOnly Deadline { get; private set; }

    /// <summary>Phiếu tìm nguồn liên quan.</summary>
    public Guid? SourcingId { get; private set; }

    /// <summary>Trọng số giá (%).</summary>
    public int PriceWeight { get; private set; }

    /// <summary>Trọng số chất lượng (%).</summary>
    public int QualityWeight { get; private set; }

    /// <summary>Trọng số giao hàng (%).</summary>
    public int DeliveryWeight { get; private set; }

    /// <summary>Trọng số thanh toán (%).</summary>
    public int PaymentWeight { get; private set; }

    /// <summary>Trọng số bảo hành (%).</summary>
    public int WarrantyWeight { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Trạng thái.</summary>
    public RfqStatus Status { get; private set; } = RfqStatus.Draft;

    /// <summary>Thời điểm gửi (UTC).</summary>
    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>Thời điểm chọn thầu (UTC).</summary>
    public DateTimeOffset? AwardedAt { get; private set; }

    /// <summary>Ngày chọn thầu (giờ Việt Nam) – ngày của giá trúng thầu trong lịch sử giá.</summary>
    public DateOnly? AwardDate { get; private set; }

    /// <summary>Các dòng hàng.</summary>
    public IReadOnlyList<RfqLine> Lines => _lines;

    /// <summary>NCC được mời và báo giá của họ.</summary>
    public IReadOnlyList<RfqSupplier> Suppliers => _suppliers;

    /// <summary>Trọng số hiện tại.</summary>
    public RfqWeights Weights => new(PriceWeight, QualityWeight, DeliveryWeight, PaymentWeight, WarrantyWeight);

    /// <summary>Lập RFQ nháp.</summary>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, số hoặc đầu phiếu sai.</exception>
    public static Rfq Create(Guid companyId, string no, RfqHeader header)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("RFQ phải thuộc một công ty.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Phiếu phải có số.");
        var rfq = new Rfq { CompanyId = companyId, No = no };
        rfq.Update(header);
        return rfq;
    }

    /// <summary>Sửa đầu RFQ (nháp).</summary>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, thiếu tiêu đề, hạn nộp trước ngày lập, trọng số sai.</exception>
    public void Update(RfqHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(header.Title)) throw new BusinessRuleException("Hãy nhập tiêu đề RFQ.");
        if (header.Deadline < header.Date) throw new BusinessRuleException("Hạn nộp báo giá không được trước ngày lập.");
        var w = header.Weights;
        if (new[] { w.Price, w.Quality, w.Delivery, w.Payment, w.Warranty }.Any(x => x < 0) || w.Total != 100)
            throw new BusinessRuleException($"Trọng số các tiêu chí phải ≥ 0 và cộng lại bằng 100 (đang là {w.Total}).");
        Date = header.Date;
        Title = header.Title.Trim();
        Deadline = header.Deadline;
        SourcingId = header.SourcingId;
        (PriceWeight, QualityWeight, DeliveryWeight, PaymentWeight, WarrantyWeight) = (w.Price, w.Quality, w.Delivery, w.Payment, w.Warranty);
        Note = string.IsNullOrWhiteSpace(header.Note) ? null : header.Note.Trim();
    }

    /// <summary>Thay toàn bộ dòng (nháp).</summary>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, không có dòng hoặc dòng sai.</exception>
    public void SetLines(IReadOnlyList<(RfqLineData Data, Item Item)> lines)
    {
        EnsureDraft();
        if (lines.Count == 0) throw new BusinessRuleException("RFQ phải có ít nhất một dòng hàng.");
        var keep = new List<RfqLine>();
        var lineNo = 0;
        foreach (var (data, item) in lines)
        {
            lineNo++;
            var line = data.Id is { } id
                ? _lines.FirstOrDefault(l => l.Id == id) ?? throw new BusinessRuleException($"Dòng {lineNo}: không tìm thấy dòng cần sửa – hãy tải lại phiếu.")
                : AddLine();
            line.Set(lineNo, data, item);
            keep.Add(line);
        }

        _lines.RemoveAll(l => !keep.Contains(l));
    }

    /// <summary>Thay danh sách NCC được mời (nháp).</summary>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc trùng NCC.</exception>
    public void SetSuppliers(IReadOnlyList<Guid> supplierIds)
    {
        EnsureDraft();
        if (supplierIds.Distinct().Count() != supplierIds.Count) throw new BusinessRuleException("Mỗi NCC chỉ mời một lần.");
        _suppliers.RemoveAll(s => !supplierIds.Contains(s.SupplierId));
        foreach (var id in supplierIds.Where(id => _suppliers.All(s => s.SupplierId != id))) _suppliers.Add(new RfqSupplier(Id, id));
    }

    /// <summary>Gửi RFQ cho NCC: Nháp → Đã gửi.</summary>
    /// <param name="at">Thời điểm gửi.</param>
    /// <param name="minSuppliers">Số NCC tối thiểu theo tham số của công ty lập RFQ.</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, chưa có dòng hoặc mời ít hơn <paramref name="minSuppliers"/> NCC.</exception>
    public void Send(DateTimeOffset at, int minSuppliers)
    {
        EnsureDraft();
        if (_lines.Count == 0) throw new BusinessRuleException("RFQ phải có ít nhất một dòng hàng.");
        // Đủ số NCC tối thiểu để có cạnh tranh – design.md mục 8.4 bước 2; con số là tham số công ty (mục 5.4).
        if (_suppliers.Count < minSuppliers)
            throw new BusinessRuleException($"Phải mời ít nhất {minSuppliers} nhà cung cấp mới gửi RFQ được (đang có {_suppliers.Count}).");
        Status = RfqStatus.Sent;
        SentAt = at;
    }

    /// <summary>Nhập/sửa báo giá của một NCC (khi đã gửi, chưa chọn thầu). Dòng không có trong danh sách = NCC không chào dòng đó.</summary>
    /// <exception cref="BusinessRuleException">Khi RFQ không ở trạng thái đã gửi, NCC không được mời, dòng sai.</exception>
    public RfqSupplier SaveQuotation(Guid supplierId, QuotationHeader header, IReadOnlyList<QuotationLineData> lines)
    {
        if (Status != RfqStatus.Sent) throw new BusinessRuleException($"RFQ {No} không ở trạng thái đang nhận báo giá.");
        var supplier = _suppliers.FirstOrDefault(s => s.SupplierId == supplierId)
            ?? throw new BusinessRuleException("NCC này không có trong danh sách được mời của RFQ.");
        foreach (var line in lines)
            if (_lines.All(l => l.Id != line.RfqLineId)) throw new BusinessRuleException("Báo giá có dòng không thuộc RFQ – hãy tải lại phiếu.");
        if (lines.GroupBy(l => l.RfqLineId).Any(g => g.Count() > 1)) throw new BusinessRuleException("Mỗi dòng RFQ chỉ một giá trong một báo giá.");
        supplier.SetQuotation(header, lines);
        return supplier;
    }

    /// <summary>
    /// Chọn thầu: mỗi dòng chọn một NCC đã chào giá dòng đó (hoặc không trao). Không chọn NCC có giá quy đổi thấp nhất của dòng thì
    /// bắt buộc ghi lý do (design.md mục 8.4 bước 4). Phải trao ít nhất một dòng. Đã gửi → Đã chọn thầu.
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi RFQ không ở trạng thái đã gửi hoặc lựa chọn sai quy tắc.</exception>
    public void Award(IReadOnlyList<AwardLineData> awards, DateTimeOffset at, DateOnly today)
    {
        if (Status != RfqStatus.Sent) throw new BusinessRuleException($"RFQ {No} không ở trạng thái đang nhận báo giá.");
        if (!awards.Any(a => a.SupplierId is not null)) throw new BusinessRuleException("Hãy chọn NCC trúng thầu cho ít nhất một dòng.");
        foreach (var line in _lines)
        {
            var award = awards.FirstOrDefault(a => a.RfqLineId == line.Id);
            if (award?.SupplierId is not { } supplierId)
            {
                line.SetAward(null, null);
                continue;
            }

            var quotes = _suppliers
                .Select(s => (s.SupplierId, Quote: s.QuotationLines.FirstOrDefault(q => q.RfqLineId == line.Id)))
                .Where(x => x.Quote is not null)
                .ToList();
            var chosen = quotes.FirstOrDefault(x => x.SupplierId == supplierId).Quote
                ?? throw new BusinessRuleException($"Dòng {line.LineNo}: NCC được chọn chưa chào giá dòng này.");
            var lowest = quotes.Min(x => x.Quote!.BaseNetPrice(line.UomFactor));
            var reason = string.IsNullOrWhiteSpace(award.Reason) ? null : award.Reason.Trim();
            if (chosen.BaseNetPrice(line.UomFactor) > lowest && reason is null)
                throw new BusinessRuleException($"Dòng {line.LineNo}: không chọn giá thấp nhất thì phải ghi lý do.");
            line.SetAward(supplierId, reason);
        }

        Status = RfqStatus.Awarded;
        AwardedAt = at;
        AwardDate = today;
    }

    /// <summary>Ghi nhận PO đã sinh cho NCC trúng thầu (mỗi NCC một PO).</summary>
    /// <exception cref="BusinessRuleException">Khi RFQ chưa chọn thầu, NCC không trúng dòng nào hoặc đã có PO.</exception>
    public void LinkPurchaseOrder(Guid supplierId, Guid purchaseOrderId)
    {
        if (Status != RfqStatus.Awarded) throw new BusinessRuleException($"RFQ {No} chưa chọn thầu.");
        var supplier = _suppliers.FirstOrDefault(s => s.SupplierId == supplierId) ?? throw new BusinessRuleException("NCC không có trong RFQ.");
        if (_lines.All(l => l.AwardedSupplierId != supplierId)) throw new BusinessRuleException("NCC này không trúng dòng nào.");
        if (supplier.PurchaseOrderId is not null) throw new BusinessRuleException("Đã tạo đơn mua hàng cho NCC này từ RFQ.");
        supplier.LinkPurchaseOrder(purchaseOrderId);
    }

    /// <summary>Hủy RFQ (Nháp hoặc Đã gửi).</summary>
    /// <exception cref="BusinessRuleException">Khi đã chọn thầu hoặc đã hủy.</exception>
    public void Cancel()
    {
        if (Status is RfqStatus.Awarded or RfqStatus.Cancelled) throw new BusinessRuleException($"RFQ {No} đã chọn thầu hoặc đã hủy.");
        Status = RfqStatus.Cancelled;
    }

    private RfqLine AddLine()
    {
        var line = new RfqLine(Id);
        _lines.Add(line);
        return line;
    }

    private void EnsureDraft()
    {
        if (Status != RfqStatus.Draft) throw new BusinessRuleException($"RFQ {No} không còn ở trạng thái nháp – không sửa được.");
    }
}

/// <summary>Dòng hàng của RFQ.</summary>
[Auditable("Dòng RFQ")]
public sealed class RfqLine : Entity
{
    private RfqLine()
    {
    }

    internal RfqLine(Guid rfqId) => RfqId = rfqId;

    /// <summary>RFQ.</summary>
    public Guid RfqId { get; private set; }

    /// <summary>Số thứ tự.</summary>
    public int LineNo { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>ĐVT hỏi giá.</summary>
    public Guid UomId { get; private set; }

    /// <summary>Hệ số ĐVT cơ sở – numeric(18,6).</summary>
    public decimal UomFactor { get; private set; }

    /// <summary>Số lượng – numeric(18,4).</summary>
    public decimal Qty { get; private set; }

    /// <summary>Dòng PR nguồn.</summary>
    public Guid? SourcePrLineId { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>NCC trúng thầu dòng này.</summary>
    public Guid? AwardedSupplierId { get; private set; }

    /// <summary>Lý do chọn (bắt buộc khi không phải giá thấp nhất).</summary>
    public string? AwardReason { get; private set; }

    internal void Set(int lineNo, RfqLineData data, Item item)
    {
        var prefix = $"Dòng {lineNo} ({item.Code})";
        if (data.ItemId != item.Id) throw new BusinessRuleException($"{prefix}: mặt hàng không khớp.");
        if (!item.IsActive && item.Id != ItemId) throw new BusinessRuleException($"{prefix}: mặt hàng đã ngừng dùng.");
        if (data.Qty <= 0 || decimal.Round(data.Qty, 4) != data.Qty || data.Qty >= 100_000_000_000_000m)
            throw new BusinessRuleException($"{prefix}: số lượng phải lớn hơn 0, tối đa 4 chữ số thập phân.");
        LineNo = lineNo;
        ItemId = item.Id;
        UomId = data.UomId;
        UomFactor = data.UomId == item.BaseUomId ? 1m
            : item.Conversions.FirstOrDefault(c => c.UomId == data.UomId)?.Factor
              ?? throw new BusinessRuleException($"{prefix}: ĐVT phải là ĐVT cơ sở hoặc ĐVT quy đổi của mặt hàng.");
        Qty = data.Qty;
        SourcePrLineId = data.SourcePrLineId;
        Note = string.IsNullOrWhiteSpace(data.Note) ? null : data.Note.Trim();
    }

    internal void SetAward(Guid? supplierId, string? reason)
    {
        AwardedSupplierId = supplierId;
        AwardReason = supplierId is null ? null : reason;
    }
}

/// <summary>NCC được mời trong RFQ kèm báo giá của NCC (gộp Quotation của design.md mục 12.1 – mỗi NCC một báo giá cho một RFQ).</summary>
[Auditable("NCC được mời RFQ")]
public sealed class RfqSupplier : Entity
{
    private readonly List<QuotationLine> _quotationLines = [];

    private RfqSupplier()
    {
    }

    internal RfqSupplier(Guid rfqId, Guid supplierId)
    {
        RfqId = rfqId;
        SupplierId = supplierId;
    }

    /// <summary>RFQ.</summary>
    public Guid RfqId { get; private set; }

    /// <summary>NCC.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Đã nhập báo giá.</summary>
    public bool HasQuoted { get; private set; }

    /// <summary>Ngày báo giá.</summary>
    public DateOnly? QuoteDate { get; private set; }

    /// <summary>Báo giá hiệu lực đến.</summary>
    public DateOnly? ValidUntil { get; private set; }

    /// <summary>Điều kiện thanh toán NCC chào.</summary>
    public string? PaymentTerms { get; private set; }

    /// <summary>Điểm chất lượng 0–10 – numeric(5,2).</summary>
    public decimal QualityScore { get; private set; }

    /// <summary>Điểm giao hàng 0–10.</summary>
    public decimal DeliveryScore { get; private set; }

    /// <summary>Điểm thanh toán 0–10.</summary>
    public decimal PaymentScore { get; private set; }

    /// <summary>Điểm bảo hành 0–10.</summary>
    public decimal WarrantyScore { get; private set; }

    /// <summary>Ghi chú báo giá.</summary>
    public string? Note { get; private set; }

    /// <summary>PO đã sinh từ kết quả chọn thầu.</summary>
    public Guid? PurchaseOrderId { get; private set; }

    /// <summary>Giá từng dòng.</summary>
    public IReadOnlyList<QuotationLine> QuotationLines => _quotationLines;

    internal void SetQuotation(QuotationHeader header, IReadOnlyList<QuotationLineData> lines)
    {
        foreach (var (score, name) in new[] { (header.QualityScore, "chất lượng"), (header.DeliveryScore, "giao hàng"), (header.PaymentScore, "thanh toán"), (header.WarrantyScore, "bảo hành") })
            if (score is < 0 or > 10 || decimal.Round(score, 2) != score) throw new BusinessRuleException($"Điểm {name} phải từ 0 đến 10, tối đa 2 chữ số thập phân.");
        if (header.ValidUntil is { } until && until < header.QuoteDate) throw new BusinessRuleException("Hiệu lực báo giá không được trước ngày báo giá.");
        if (lines.Count == 0) throw new BusinessRuleException("Báo giá phải có giá cho ít nhất một dòng.");
        HasQuoted = true;
        QuoteDate = header.QuoteDate;
        ValidUntil = header.ValidUntil;
        PaymentTerms = string.IsNullOrWhiteSpace(header.PaymentTerms) ? null : header.PaymentTerms.Trim();
        (QualityScore, DeliveryScore, PaymentScore, WarrantyScore) = (header.QualityScore, header.DeliveryScore, header.PaymentScore, header.WarrantyScore);
        Note = string.IsNullOrWhiteSpace(header.Note) ? null : header.Note.Trim();
        _quotationLines.RemoveAll(q => lines.All(l => l.RfqLineId != q.RfqLineId));
        foreach (var data in lines)
        {
            var line = _quotationLines.FirstOrDefault(q => q.RfqLineId == data.RfqLineId);
            if (line is null)
            {
                line = new QuotationLine(Id, data.RfqLineId);
                _quotationLines.Add(line);
            }

            line.Set(data);
        }
    }

    internal void LinkPurchaseOrder(Guid purchaseOrderId) => PurchaseOrderId = purchaseOrderId;
}

/// <summary>Giá NCC chào cho một dòng RFQ (theo ĐVT của dòng RFQ).</summary>
[Auditable("Dòng báo giá")]
public sealed class QuotationLine : Entity
{
    private QuotationLine()
    {
    }

    internal QuotationLine(Guid rfqSupplierId, Guid rfqLineId)
    {
        RfqSupplierId = rfqSupplierId;
        RfqLineId = rfqLineId;
    }

    /// <summary>NCC trong RFQ.</summary>
    public Guid RfqSupplierId { get; private set; }

    /// <summary>Dòng RFQ.</summary>
    public Guid RfqLineId { get; private set; }

    /// <summary>Đơn giá trước CK, VAT – numeric(19,6).</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>% chiết khấu – numeric(9,4).</summary>
    public decimal DiscountPct { get; private set; }

    /// <summary>Thuế suất.</summary>
    public string? VatCode { get; private set; }

    /// <summary>Thời gian giao (ngày).</summary>
    public int? LeadTimeDays { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Giá quy về ĐVT cơ sở, trước VAT (xem <see cref="PricingMath.BaseNetPrice"/>).</summary>
    /// <param name="uomFactor">Hệ số ĐVT của dòng RFQ.</param>
    /// <returns>Giá cơ sở.</returns>
    public decimal BaseNetPrice(decimal uomFactor) => PricingMath.BaseNetPrice(UnitPrice, DiscountPct, uomFactor);

    internal void Set(QuotationLineData data)
    {
        if (data.UnitPrice < 0 || decimal.Round(data.UnitPrice, 6) != data.UnitPrice || data.UnitPrice >= 10_000_000_000_000m)
            throw new BusinessRuleException("Đơn giá báo giá phải ≥ 0, tối đa 6 chữ số thập phân.");
        if (data.DiscountPct is < 0 or > 100) throw new BusinessRuleException("Chiết khấu phải từ 0 đến 100%.");
        if (data.LeadTimeDays is < 0 or > 365) throw new BusinessRuleException("Thời gian giao phải từ 0 đến 365 ngày.");
        var vat = string.IsNullOrWhiteSpace(data.VatCode) ? null : data.VatCode.Trim().ToUpperInvariant();
        if (vat is not null && !VatRates.All.Contains(vat))
            throw new BusinessRuleException($"Thuế suất phải là một trong {string.Join(", ", VatRates.All)}.");
        UnitPrice = data.UnitPrice;
        DiscountPct = data.DiscountPct;
        VatCode = vat;
        LeadTimeDays = data.LeadTimeDays;
        Note = string.IsNullOrWhiteSpace(data.Note) ? null : data.Note.Trim();
    }
}

/// <summary>
/// Chấm điểm báo giá theo trọng số (design.md mục 8.4 bước 3).
/// </summary>
public static class QuotationScoring
{
    /// <summary>
    /// Điểm giá của một NCC (0–10) = trung bình trên các dòng NCC chào của (giá quy đổi thấp nhất dòng ÷ giá quy đổi của NCC) × 10;
    /// giá 0 tính 10 điểm. Ví dụ hai dòng, NCC thấp nhất dòng 1 (10 điểm), dòng 2 đắt hơn 25% (100/125 × 10 = 8) → 9 điểm.
    /// </summary>
    /// <param name="ratios">Các cặp (giá thấp nhất dòng, giá NCC) của những dòng NCC có chào.</param>
    /// <returns>Điểm giá, làm tròn 2 số.</returns>
    public static decimal PriceScore(IReadOnlyList<(decimal Lowest, decimal Price)> ratios) =>
        ratios.Count == 0
            ? 0m
            : Math.Round(ratios.Average(r => r.Price <= 0 ? 10m : r.Lowest / r.Price * 10m), 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Tổng điểm = Σ trọng số × điểm tiêu chí / 100 (thang 10). Ví dụ trọng số 60/20/10/5/5, điểm 9/8/10/10/6 → 8,8.
    /// </summary>
    /// <param name="weights">Trọng số.</param>
    /// <param name="price">Điểm giá.</param>
    /// <param name="quality">Điểm chất lượng.</param>
    /// <param name="delivery">Điểm giao hàng.</param>
    /// <param name="payment">Điểm thanh toán.</param>
    /// <param name="warranty">Điểm bảo hành.</param>
    /// <returns>Tổng điểm, làm tròn 2 số.</returns>
    public static decimal Total(RfqWeights weights, decimal price, decimal quality, decimal delivery, decimal payment, decimal warranty) =>
        Math.Round(
            (weights.Price * price + weights.Quality * quality + weights.Delivery * delivery + weights.Payment * payment + weights.Warranty * warranty) / 100m,
            2,
            MidpointRounding.AwayFromZero);

}
