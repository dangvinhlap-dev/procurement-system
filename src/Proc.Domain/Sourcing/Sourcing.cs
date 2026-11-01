using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;

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

    /// <summary>Đã chọn thầu, chờ hội đồng duyệt kết quả (lát B3 – design.md mục 8.6).</summary>
    AwardPending = 5,
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
/// Các mục của hồ sơ trình kết quả chọn thầu (lát B3b – design.md mục 8.6; đề xuất Ban CUVT: hồ sơ trình gồm so sánh, biên bản, kỹ thuật,
/// chi phí, pháp lý, đề xuất, xác nhận ngân sách). Bảng so sánh do hệ thống lập nên tự tính, các mục còn lại người mua đánh dấu.
/// </summary>
public static class RfqDossier
{
    /// <summary>Bảng so sánh báo giá – tự có khi đã nhập ít nhất một báo giá.</summary>
    public const string Comparison = "COMPARE";

    /// <summary>Mọi mục theo thứ tự hiển thị (mã, tên).</summary>
    public static readonly IReadOnlyList<(string Code, string Name)> Items =
    [
        (Comparison, "Bảng so sánh báo giá"),
        ("MINUTES", "Biên bản đàm phán có xác nhận thành viên"),
        ("TECHNICAL", "Đánh giá kỹ thuật"),
        ("COST", "Phân tích chi phí"),
        ("LEGAL", "Hồ sơ pháp lý NCC"),
        ("PROPOSAL", "Tờ trình đề xuất lựa chọn"),
        ("BUDGET", "Xác nhận ngân sách"),
    ];
}

/// <summary>Đánh dấu một mục hồ sơ trình.</summary>
/// <param name="Code">Mã mục (<see cref="RfqDossier.Items"/>, trừ bảng so sánh).</param>
/// <param name="IsDone">Đã có.</param>
/// <param name="Note">Ghi chú (số biên bản, nơi lưu…).</param>
public sealed record RfqDossierData(string Code, bool IsDone, string? Note);

/// <summary>Tình trạng một mục hồ sơ trình.</summary>
/// <param name="Code">Mã mục.</param>
/// <param name="Name">Tên mục.</param>
/// <param name="IsDone">Đã có.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="IsAutomatic">Hệ thống tự tính (không đánh dấu tay).</param>
public sealed record RfqDossierStatus(string Code, string Name, bool IsDone, string? Note, bool IsAutomatic);

/// <summary>
/// Yêu cầu báo giá / đấu thầu (design.md mục 8.4): danh sách mặt hàng, hạn nộp, trọng số tiêu chí; gửi cho đủ số NCC tối thiểu
/// (tham số công ty <see cref="Organization.CompanyParameter.RfqMinSuppliers"/>); nhập báo giá từng NCC; chọn NCC trúng thầu theo dòng; từ kết quả sinh PO nháp.
/// Vòng đời: Nháp → Đã gửi → Chờ duyệt kết quả → Đã chọn thầu; từ chối/rút về Đã gửi; hủy được khi Nháp/Đã gửi.
/// Kết quả chọn thầu đi qua quy trình duyệt dùng chung loại RFQ (design.md mục 8.6, 18.6).
/// </summary>
[Auditable("Yêu cầu báo giá", LabelProperty = nameof(No))]
public sealed class Rfq : AuditableEntity, ICompanyOwned, IHasDocumentNumber, IApprovalSubject
{
    private readonly List<RfqLine> _lines = [];
    private readonly List<RfqSupplier> _suppliers = [];
    private readonly List<RfqDossierItem> _dossierItems = [];

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

    /// <summary>Ngày chọn thầu (giờ Việt Nam) – ngày của giá trúng thầu trong lịch sử giá (chỉ tính khi kết quả đã duyệt).</summary>
    public DateOnly? AwardDate { get; private set; }

    /// <summary>Người chọn thầu (đề xuất kết quả) – không tự duyệt kết quả của mình.</summary>
    public Guid? AwardedBy { get; private set; }

    /// <summary>Giải trình khi mời ít hơn số NCC tối thiểu (null = mời đủ).</summary>
    public string? FewSuppliersReason { get; private set; }

    /// <inheritdoc />
    public int ApprovalRound { get; private set; }

    /// <summary>Lý do từ chối kết quả chọn thầu gần nhất (xóa khi gửi duyệt lại).</summary>
    public string? RejectReason { get; private set; }

    /// <inheritdoc />
    public string DocType => DocumentTypes.RequestForQuotation;

    /// <inheritdoc />
    public bool IsAwaitingApproval => Status == RfqStatus.AwardPending;

    /// <inheritdoc />
    /// <remarks>
    /// Giá trị trúng thầu (sau CK, trước VAT) = Σ SL × đơn giá × (100 − %CK)/100 của các dòng đã trao, làm tròn 2 số mỗi dòng.
    /// Ví dụ 100 cái × 50.000 + 10 thùng × 600.000 × 95% = 5.000.000 + 5.700.000 = 10.700.000.
    /// </remarks>
    public decimal ApprovalAmount => _lines.Sum(AwardedAmount);

    /// <summary>Các dòng hàng.</summary>
    public IReadOnlyList<RfqLine> Lines => _lines;

    /// <summary>NCC được mời và báo giá của họ.</summary>
    public IReadOnlyList<RfqSupplier> Suppliers => _suppliers;

    /// <summary>Các mục hồ sơ trình đã đánh dấu tay (lát B3b).</summary>
    public IReadOnlyList<RfqDossierItem> DossierItems => _dossierItems;

    /// <summary>
    /// Tình trạng đủ các mục hồ sơ trình theo thứ tự <see cref="RfqDossier.Items"/>: bảng so sánh có khi đã có báo giá; mục chưa đánh dấu
    /// là chưa có.
    /// </summary>
    public IReadOnlyList<RfqDossierStatus> Dossier =>
        RfqDossier.Items.Select(i =>
        {
            if (i.Code == RfqDossier.Comparison) return new RfqDossierStatus(i.Code, i.Name, _suppliers.Any(s => s.HasQuoted), null, true);
            var item = _dossierItems.FirstOrDefault(x => x.Code == i.Code);
            return new RfqDossierStatus(i.Code, i.Name, item?.IsDone == true, item?.Note, false);
        }).ToList();

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
    /// <param name="fewSuppliersReason">Giải trình khi mời ít hơn <paramref name="minSuppliers"/> NCC (bỏ qua khi mời đủ).</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, chưa có dòng, chưa mời NCC nào, hoặc mời thiếu mà không giải trình.</exception>
    public void Send(DateTimeOffset at, int minSuppliers, string? fewSuppliersReason)
    {
        EnsureDraft();
        if (_lines.Count == 0) throw new BusinessRuleException("RFQ phải có ít nhất một dòng hàng.");
        if (_suppliers.Count == 0) throw new BusinessRuleException("Hãy mời ít nhất một nhà cung cấp.");
        // Số NCC tối thiểu để có cạnh tranh là tham số công ty (mục 5.4). Lát B3 (M3): mời thiếu vẫn gửi được khi có giải trình –
        // ngoại lệ được ghi lại, còn kết quả chọn thầu luôn phải qua duyệt (design.md mục 8.6).
        var reason = Clean(fewSuppliersReason);
        if (_suppliers.Count < minSuppliers && reason is null)
            throw new BusinessRuleException(
                $"Mời ít hơn {minSuppliers} nhà cung cấp (đang có {_suppliers.Count}) thì phải ghi giải trình mới gửi RFQ được.");
        FewSuppliersReason = _suppliers.Count < minSuppliers ? reason : null;
        Status = RfqStatus.Sent;
        SentAt = at;
    }

    /// <summary>
    /// Đánh dấu hồ sơ trình (Nháp hoặc Đang nhận báo giá – sau khi gửi duyệt hồ sơ khóa để hội đồng xét đúng hồ sơ đã trình). Thiếu mục
    /// không chặn gửi duyệt; hội đồng thấy số mục còn thiếu (lát B3b).
    /// </summary>
    /// <param name="items">Các mục cần lưu (mục không gửi giữ nguyên).</param>
    /// <exception cref="BusinessRuleException">Khi RFQ đang chờ duyệt/đã chọn thầu/đã hủy, mã mục sai hoặc trùng, ghi chú quá dài.</exception>
    public void SetDossier(IReadOnlyList<RfqDossierData> items)
    {
        if (Status is not (RfqStatus.Draft or RfqStatus.Sent))
            throw new BusinessRuleException($"RFQ {No} đã gửi duyệt kết quả hoặc đã hủy – hồ sơ trình không sửa được.");
        if (items.GroupBy(i => i.Code).Any(g => g.Count() > 1)) throw new BusinessRuleException("Mỗi mục hồ sơ trình chỉ lưu một lần.");
        foreach (var data in items)
        {
            if (data.Code == RfqDossier.Comparison) throw new BusinessRuleException("Bảng so sánh báo giá do hệ thống lập – không đánh dấu tay.");
            if (RfqDossier.Items.All(i => i.Code != data.Code)) throw new BusinessRuleException($"Mục hồ sơ trình {data.Code} không có.");
            var note = Clean(data.Note);
            if (note?.Length > 500) throw new BusinessRuleException("Ghi chú mục hồ sơ trình tối đa 500 ký tự.");
            var item = _dossierItems.FirstOrDefault(x => x.Code == data.Code);
            if (item is null)
            {
                item = new RfqDossierItem(Id, data.Code);
                _dossierItems.Add(item);
            }

            item.Set(data.IsDone, note);
        }
    }

    /// <summary>Nhập/sửa báo giá của một NCC (khi đã gửi, chưa chọn thầu). Dòng không có trong danh sách = NCC không chào dòng đó.</summary>
    /// <exception cref="BusinessRuleException">Khi RFQ không ở trạng thái đã gửi, NCC không được mời, dòng sai.</exception>
    public RfqSupplier SaveQuotation(Guid supplierId, QuotationHeader header, IReadOnlyList<QuotationLineData> lines)
    {
        EnsureSent();
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
    /// bắt buộc ghi lý do (design.md mục 8.4 bước 4). Phải trao ít nhất một dòng. Trạng thái chưa đổi – sau đó phải gửi duyệt
    /// (<see cref="Submit"/>) để hội đồng duyệt kết quả (lát B3).
    /// </summary>
    /// <param name="awards">Lựa chọn từng dòng.</param>
    /// <param name="awardedBy">Người chọn thầu.</param>
    /// <param name="today">Ngày chọn thầu (giờ Việt Nam).</param>
    /// <exception cref="BusinessRuleException">Khi RFQ không ở trạng thái đã gửi hoặc lựa chọn sai quy tắc.</exception>
    public void Award(IReadOnlyList<AwardLineData> awards, Guid awardedBy, DateOnly today)
    {
        EnsureSent();
        if (!awards.Any(a => a.SupplierId is not null)) throw new BusinessRuleException("Hãy chọn NCC trúng thầu cho ít nhất một dòng.");
        // Kiểm tra hết các dòng trước rồi mới ghi, để lựa chọn sai không làm hỏng lựa chọn đang lưu.
        var chosen = new List<(RfqLine Line, Guid? SupplierId, string? Reason)>();
        foreach (var line in _lines)
        {
            var award = awards.FirstOrDefault(a => a.RfqLineId == line.Id);
            if (award?.SupplierId is not { } supplierId)
            {
                chosen.Add((line, null, null));
                continue;
            }

            var quotes = _suppliers
                .Select(s => (s.SupplierId, Quote: s.QuotationLines.FirstOrDefault(q => q.RfqLineId == line.Id)))
                .Where(x => x.Quote is not null)
                .ToList();
            var quote = quotes.FirstOrDefault(x => x.SupplierId == supplierId).Quote
                ?? throw new BusinessRuleException($"Dòng {line.LineNo}: NCC được chọn chưa chào giá dòng này.");
            var lowest = quotes.Min(x => x.Quote!.BaseNetPrice(line.UomFactor));
            var reason = Clean(award.Reason);
            if (quote.BaseNetPrice(line.UomFactor) > lowest && reason is null)
                throw new BusinessRuleException($"Dòng {line.LineNo}: không chọn giá thấp nhất thì phải ghi lý do.");
            chosen.Add((line, supplierId, reason));
        }

        foreach (var (line, supplierId, reason) in chosen) line.SetAward(supplierId, reason);
        AwardedBy = awardedBy;
        AwardDate = today;
    }

    /// <summary>Gửi duyệt kết quả chọn thầu: Đã gửi → Chờ duyệt, tăng lượt, xóa lý do từ chối cũ.</summary>
    /// <returns>Số lượt gửi mới.</returns>
    /// <exception cref="BusinessRuleException">Khi RFQ không ở trạng thái đã gửi hoặc chưa trao dòng nào.</exception>
    public int Submit()
    {
        EnsureSent();
        if (_lines.All(l => l.AwardedSupplierId is null) || AwardedBy is null)
            throw new BusinessRuleException("Hãy chọn NCC trúng thầu cho ít nhất một dòng trước khi gửi duyệt.");
        Status = RfqStatus.AwardPending;
        RejectReason = null;
        return ++ApprovalRound;
    }

    /// <summary>Hội đồng duyệt đủ các cấp: Chờ duyệt → Đã chọn thầu (giá trúng thầu từ đây mới vào lịch sử giá, sinh PO được).</summary>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi RFQ không chờ duyệt.</exception>
    public void MarkApproved(DateTimeOffset at)
    {
        EnsureAwardPending();
        Status = RfqStatus.Awarded;
        AwardedAt = at;
    }

    /// <summary>
    /// Bị từ chối hoặc người chọn thầu rút lại: về Đã gửi (đang nhận báo giá) để bổ sung báo giá, chọn lại. Lựa chọn cũ giữ làm gợi ý.
    /// </summary>
    /// <param name="reason">Lý do (rút lại thì null).</param>
    /// <exception cref="BusinessRuleException">Khi RFQ không chờ duyệt.</exception>
    public void ReturnToDraft(string? reason)
    {
        EnsureAwardPending();
        Status = RfqStatus.Sent;
        RejectReason = Clean(reason);
    }

    /// <inheritdoc />
    /// <remarks>Người lập RFQ và người chọn thầu đều không tự duyệt kết quả (tách người đề xuất – người duyệt, design.md mục 18.6).</remarks>
    public bool IsPreparedBy(Guid userId) => CreatedBy == userId || AwardedBy == userId;

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

    /// <summary>Ghi nhận bảng giá NCC nháp đã sinh cho NCC trúng thầu (mỗi NCC một bảng giá từ một RFQ – lát B3b).</summary>
    /// <exception cref="BusinessRuleException">Khi RFQ chưa duyệt kết quả, NCC không trúng dòng nào hoặc đã có bảng giá.</exception>
    public void LinkPriceList(Guid supplierId, Guid priceListId)
    {
        if (Status != RfqStatus.Awarded) throw new BusinessRuleException($"RFQ {No} chưa được duyệt kết quả chọn thầu.");
        var supplier = _suppliers.FirstOrDefault(s => s.SupplierId == supplierId) ?? throw new BusinessRuleException("NCC không có trong RFQ.");
        if (_lines.All(l => l.AwardedSupplierId != supplierId)) throw new BusinessRuleException("NCC này không trúng dòng nào.");
        if (supplier.PriceListId is not null) throw new BusinessRuleException("Đã tạo bảng giá cho NCC này từ RFQ.");
        supplier.LinkPriceList(priceListId);
    }

    /// <summary>Hủy RFQ (Nháp hoặc Đã gửi).</summary>
    /// <exception cref="BusinessRuleException">Khi đang chờ duyệt kết quả, đã chọn thầu hoặc đã hủy.</exception>
    public void Cancel()
    {
        if (Status == RfqStatus.AwardPending) throw new BusinessRuleException($"RFQ {No} đang chờ duyệt kết quả chọn thầu – rút về trước khi hủy.");
        if (Status is RfqStatus.Awarded or RfqStatus.Cancelled) throw new BusinessRuleException($"RFQ {No} đã chọn thầu hoặc đã hủy.");
        Status = RfqStatus.Cancelled;
    }

    private RfqLine AddLine()
    {
        var line = new RfqLine(Id);
        _lines.Add(line);
        return line;
    }

    /// <summary>Thành tiền trúng thầu của một dòng (0 nếu chưa trao) – xem <see cref="ApprovalAmount"/>.</summary>
    private decimal AwardedAmount(RfqLine line)
    {
        if (line.AwardedSupplierId is not { } supplierId) return 0m;
        var quote = _suppliers.FirstOrDefault(s => s.SupplierId == supplierId)?.QuotationLines.FirstOrDefault(q => q.RfqLineId == line.Id);
        return quote is null ? 0m : Math.Round(line.Qty * quote.UnitPrice * (100m - quote.DiscountPct) / 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureSent()
    {
        if (Status != RfqStatus.Sent) throw new BusinessRuleException($"RFQ {No} không ở trạng thái đang nhận báo giá.");
    }

    private void EnsureAwardPending()
    {
        if (Status != RfqStatus.AwardPending) throw new BusinessRuleException($"RFQ {No} không ở trạng thái chờ duyệt kết quả chọn thầu.");
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

    /// <summary>Bảng giá NCC nháp đã sinh từ kết quả chọn thầu (lát B3b).</summary>
    public Guid? PriceListId { get; private set; }

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

    internal void LinkPriceList(Guid priceListId) => PriceListId = priceListId;
}

/// <summary>Một mục hồ sơ trình kết quả chọn thầu đã đánh dấu (lát B3b).</summary>
[Auditable("Hồ sơ trình RFQ")]
public sealed class RfqDossierItem : Entity
{
    private RfqDossierItem()
    {
    }

    internal RfqDossierItem(Guid rfqId, string code)
    {
        RfqId = rfqId;
        Code = code;
    }

    /// <summary>RFQ.</summary>
    public Guid RfqId { get; private set; }

    /// <summary>Mã mục (<see cref="RfqDossier.Items"/>).</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Đã có.</summary>
    public bool IsDone { get; private set; }

    /// <summary>Ghi chú (số biên bản, nơi lưu…).</summary>
    public string? Note { get; private set; }

    internal void Set(bool isDone, string? note)
    {
        IsDone = isDone;
        Note = note;
    }
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
