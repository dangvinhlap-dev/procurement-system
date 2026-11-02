using System.Globalization;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;

namespace Proc.Domain.Purchasing;

/// <summary>Mức ưu tiên của đề nghị mua hàng.</summary>
public enum PurchasePriority
{
    /// <summary>Thấp.</summary>
    Low = 1,

    /// <summary>Thường.</summary>
    Normal = 2,

    /// <summary>Gấp.</summary>
    Urgent = 3,
}

/// <summary>Đầu phiếu đề nghị mua hàng (design.md mục 10.2).</summary>
/// <param name="Date">Ngày đề nghị.</param>
/// <param name="DepartmentId">Bộ phận đề nghị, bắt buộc (danh mục bộ phận của công ty – design.md mục 18.5).</param>
/// <param name="Purpose">Mục đích sử dụng.</param>
/// <param name="NeededDate">Ngày cần hàng.</param>
/// <param name="WarehouseId">Kho nhận dự kiến.</param>
/// <param name="Priority">Mức ưu tiên.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record PurchaseRequestHeader(
    DateOnly Date, Guid? DepartmentId, string? Purpose, DateOnly? NeededDate, Guid? WarehouseId, PurchasePriority Priority, string? Note);

/// <summary>Dữ liệu một dòng đề nghị mua hàng.</summary>
public sealed record PurchaseRequestLineData(
    Guid? Id, Guid ItemId, Guid UomId, decimal Qty, decimal EstPrice, Guid? SuggestedSupplierId, DateOnly? NeededDate, string? Note);

/// <summary>Đầu phiếu đơn mua hàng (design.md mục 10.3). Giai đoạn 5 chỉ dùng VND.</summary>
/// <param name="DepartmentId">Bộ phận đặt hàng (không bắt buộc) – xét phạm vi "bộ phận được phép đặt" của hợp đồng (design.md mục 18.10).</param>
public sealed record PurchaseOrderHeader(
    DateOnly Date, Guid SupplierId, Guid WarehouseId, DateOnly? ExpectedDate, string? PaymentTerms, string? DeliveryTerms,
    string? DeliveryAddress, decimal ReceiveTolerancePct, string? Note, Guid? DepartmentId = null);

/// <summary>Dữ liệu một dòng đơn mua hàng.</summary>
/// <param name="PriceDeviationReason">Lý do đơn giá khác giá đã duyệt (bắt buộc khi gửi duyệt nếu lệch – design.md mục 18.10).</param>
/// <param name="SourceRfqLineId">Dòng RFQ đã chọn thầu mà dòng PO lấy giá (chỉ khi lập PO từ kết quả chọn thầu; server kiểm tra lại).</param>
public sealed record PurchaseOrderLineData(
    Guid? Id, Guid ItemId, Guid UomId, decimal Qty, decimal UnitPrice, decimal DiscountPct, string? VatCode, DateOnly? DeliveryDate,
    string? Note, Guid? SourcePrLineId, string? PriceDeviationReason = null, Guid? SourceRfqLineId = null);

/// <summary>
/// Nguồn giá của dòng PO (design.md mục 18.10 – đề xuất Ban CUVT M1, P5): thứ tự ưu tiên kết quả chọn thầu → hợp đồng nguyên tắc →
/// bảng giá NCC → ngoài hợp đồng (không có giá đã duyệt).
/// </summary>
public enum PurchasePriceSource
{
    /// <summary>Dòng lập trước bản có nguồn giá (0.12.0) – không xác định.</summary>
    Unknown = 0,

    /// <summary>Giá trúng thầu của RFQ đã chọn thầu (kết quả đã được hội đồng duyệt).</summary>
    Rfq = 1,

    /// <summary>Giá theo bảng giá của hợp đồng nguyên tắc đang hiệu lực của công ty với NCC.</summary>
    Contract = 2,

    /// <summary>Giá theo bảng giá NCC đang hiệu lực (không có hợp đồng).</summary>
    PriceList = 3,

    /// <summary>Không có giá đã duyệt – mua ngoài hợp đồng.</summary>
    None = 4,
}

/// <summary>Giá đã duyệt tìm được cho một dòng PO.</summary>
/// <param name="Source">Nguồn giá (Rfq, Contract hoặc PriceList).</param>
/// <param name="UnitPrice">Đơn giá theo ĐVT của dòng PO, trước chiết khấu.</param>
/// <param name="DiscountPct">% chiết khấu.</param>
/// <param name="PriceListId">Bảng giá nguồn (nguồn hợp đồng hoặc bảng giá).</param>
/// <param name="ContractId">Hợp đồng nguyên tắc (nguồn hợp đồng).</param>
/// <param name="RfqLineId">Dòng RFQ (nguồn chọn thầu).</param>
public sealed record ApprovedPrice(
    PurchasePriceSource Source, decimal UnitPrice, decimal DiscountPct, Guid? PriceListId = null, Guid? ContractId = null, Guid? RfqLineId = null);

/// <summary>Một bậc giá của bảng giá NCC.</summary>
/// <param name="UomId">ĐVT của bậc.</param>
/// <param name="UomFactor">Hệ số ĐVT của bậc về ĐVT cơ sở.</param>
/// <param name="MinQty">Bậc số lượng (theo ĐVT của bậc).</param>
/// <param name="UnitPrice">Đơn giá theo ĐVT của bậc.</param>
/// <param name="DiscountPct">% chiết khấu.</param>
public sealed record PriceTier(Guid UomId, decimal UomFactor, decimal MinQty, decimal UnitPrice, decimal DiscountPct);

/// <summary>Chọn bậc giá cho dòng PO – cùng quy tắc với giá gợi ý trên giao diện (<c>pickSupplierPrice</c>, design.md mục 8.5).</summary>
public static class PriceTierPicker
{
    /// <summary>
    /// Cùng ĐVT: bậc SL lớn nhất ≤ SL đặt. Không có bậc cùng ĐVT thì quy từ bậc theo ĐVT khác: đơn giá = giá bậc / hệ số bậc × hệ số
    /// dòng (làm tròn 6 số), chọn bậc lớn nhất có SL quy về cơ sở ≤ SL cơ sở của dòng. Ví dụ bậc 0 cái 50.000, bậc 100 cái 48.000: đặt
    /// 120 cái → 48.000; đặt 2 thùng 12 cái (không có bậc theo thùng) → bậc 0 cái → 600.000/thùng.
    /// </summary>
    /// <param name="tiers">Các bậc của mặt hàng trong một bảng giá.</param>
    /// <param name="uomId">ĐVT dòng.</param>
    /// <param name="uomFactor">Hệ số ĐVT dòng.</param>
    /// <param name="qty">SL đặt theo ĐVT dòng.</param>
    /// <returns>(Đơn giá theo ĐVT dòng, % CK), hoặc null nếu không bậc nào áp được.</returns>
    public static (decimal UnitPrice, decimal DiscountPct)? Pick(IEnumerable<PriceTier> tiers, Guid uomId, decimal uomFactor, decimal qty)
    {
        var list = tiers.ToList();
        var same = list.Where(t => t.UomId == uomId && t.MinQty <= qty).OrderByDescending(t => t.MinQty).FirstOrDefault();
        if (same is not null) return (same.UnitPrice, same.DiscountPct);
        var baseQty = qty * uomFactor;
        var other = list.Where(t => t.MinQty * t.UomFactor <= baseQty).OrderByDescending(t => t.MinQty * t.UomFactor).FirstOrDefault();
        if (other is null) return null;
        return (Math.Round(other.UnitPrice / other.UomFactor * uomFactor, 6, MidpointRounding.AwayFromZero), other.DiscountPct);
    }
}

/// <summary>
/// Phần chung của chứng từ mua hàng có duyệt (PR, PO): số phiếu, trạng thái, lượt gửi duyệt, lý do từ chối gần nhất.
/// Vòng đời: Nháp → (Gửi duyệt) Chờ duyệt → Đã duyệt; bị từ chối về Nháp kèm lý do; hủy được khi Nháp/Chờ duyệt/Đã duyệt chưa dùng.
/// </summary>
public abstract class ApprovableDocument : AuditableEntity, ICompanyOwned, IHasDocumentNumber, IApprovalSubject
{
    /// <inheritdoc />
    public Guid CompanyId { get; protected set; }

    /// <summary>Số phiếu (cấp khi lưu lần đầu).</summary>
    public string No { get; protected set; } = string.Empty;

    /// <inheritdoc />
    public string? DocumentNumber => No;

    /// <summary>Ngày chứng từ.</summary>
    public DateOnly Date { get; protected set; }

    /// <summary>Trạng thái.</summary>
    public ApprovalDocumentStatus Status { get; protected set; } = ApprovalDocumentStatus.Draft;

    /// <summary>Số lần đã gửi duyệt (lượt hiện tại).</summary>
    public int ApprovalRound { get; protected set; }

    /// <summary>Lý do từ chối gần nhất (xóa khi gửi lại).</summary>
    public string? RejectReason { get; protected set; }

    /// <summary>Thời điểm duyệt đủ (UTC).</summary>
    public DateTimeOffset? ApprovedAt { get; protected set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; protected set; }

    /// <summary>Tổng giá trị dùng để xét ngưỡng duyệt.</summary>
    public abstract decimal ApprovalAmount { get; }

    /// <summary>Loại chứng từ (PR, PO).</summary>
    public abstract string DocType { get; }

    /// <inheritdoc />
    public virtual IReadOnlyCollection<Guid> RelatedSupplierIds => [];

    /// <inheritdoc />
    public bool IsAwaitingApproval => Status == ApprovalDocumentStatus.Submitted;

    /// <summary>Gửi duyệt: Nháp → Chờ duyệt, tăng lượt gửi.</summary>
    /// <returns>Số lượt gửi mới.</returns>
    /// <exception cref="BusinessRuleException">Khi phiếu không ở trạng thái nháp hoặc không có dòng.</exception>
    public int Submit()
    {
        EnsureDraft();
        if (LineCount == 0) throw new BusinessRuleException("Phiếu phải có ít nhất một dòng hàng mới gửi duyệt được.");
        Status = ApprovalDocumentStatus.Submitted;
        RejectReason = null;
        return ++ApprovalRound;
    }

    /// <summary>Duyệt đủ các cấp (hoặc không cần duyệt vì dưới mọi ngưỡng).</summary>
    /// <exception cref="BusinessRuleException">Khi phiếu không ở trạng thái chờ duyệt.</exception>
    public void MarkApproved(DateTimeOffset at)
    {
        if (Status != ApprovalDocumentStatus.Submitted) throw new BusinessRuleException($"Phiếu {No} không ở trạng thái chờ duyệt.");
        Status = ApprovalDocumentStatus.Approved;
        ApprovedAt = at;
    }

    /// <summary>Bị từ chối (hoặc người lập rút lại): về Nháp để sửa, giữ lý do.</summary>
    /// <exception cref="BusinessRuleException">Khi phiếu không ở trạng thái chờ duyệt.</exception>
    public void ReturnToDraft(string? reason)
    {
        if (Status != ApprovalDocumentStatus.Submitted) throw new BusinessRuleException($"Phiếu {No} không ở trạng thái chờ duyệt.");
        Status = ApprovalDocumentStatus.Draft;
        RejectReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    /// <summary>Hủy phiếu (giữ số, lịch sử). Phiếu đã dùng (PR đã lên PO, PO đã nhận hàng) do tầng trên chặn.</summary>
    /// <exception cref="BusinessRuleException">Khi phiếu đã hủy hoặc đã đóng.</exception>
    public void Cancel()
    {
        if (Status is ApprovalDocumentStatus.Cancelled or ApprovalDocumentStatus.Closed)
            throw new BusinessRuleException($"Phiếu {No} đã hủy hoặc đã đóng.");
        Status = ApprovalDocumentStatus.Cancelled;
    }

    /// <summary>Số dòng.</summary>
    protected abstract int LineCount { get; }

    /// <summary>Chỉ sửa được khi nháp (CLAUDE.md mục 4: chứng từ đã duyệt/ghi sổ không sửa trực tiếp).</summary>
    protected void EnsureDraft()
    {
        if (Status != ApprovalDocumentStatus.Draft)
            throw new BusinessRuleException($"Phiếu {No} không còn ở trạng thái nháp nên không sửa được.");
    }

    /// <summary>Chuỗi đã cắt khoảng trắng, rỗng thành null.</summary>
    protected static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Đề nghị mua hàng – PR (design.md mục 6.2, 10.2).</summary>
[Auditable("Đề nghị mua hàng", LabelProperty = nameof(No))]
public sealed class PurchaseRequest : ApprovableDocument
{
    private readonly List<PurchaseRequestLine> _lines = [];

    private PurchaseRequest()
    {
    }

    /// <summary>Bộ phận đề nghị (danh mục <see cref="Organization.Department"/> cùng công ty).</summary>
    public Guid? DepartmentId { get; private set; }

    /// <summary>
    /// Bộ phận ghi dạng chữ trước khi có danh mục bộ phận – <b>ngừng dùng</b> từ lát A2 (design.md mục 18.5): chỉ đọc để hiện trên
    /// phiếu cũ, không ghi mới. Giữ cột vì đang có dữ liệu (CLAUDE.md mục 8), xóa ở bản sau.
    /// </summary>
    public string? LegacyDepartment { get; private set; }

    /// <summary>Người đề nghị.</summary>
    public Guid? RequesterId { get; private set; }

    /// <summary>Mục đích sử dụng.</summary>
    public string? Purpose { get; private set; }

    /// <summary>Ngày cần hàng.</summary>
    public DateOnly? NeededDate { get; private set; }

    /// <summary>Kho nhận dự kiến.</summary>
    public Guid? WarehouseId { get; private set; }

    /// <summary>Mức ưu tiên.</summary>
    public PurchasePriority Priority { get; private set; } = PurchasePriority.Normal;

    /// <summary>Tổng giá trị dự kiến = Σ SL × đơn giá tham khảo – numeric(19,2).</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Các dòng.</summary>
    public IReadOnlyList<PurchaseRequestLine> Lines => _lines;

    /// <inheritdoc />
    public override decimal ApprovalAmount => TotalAmount;

    /// <inheritdoc />
    public override string DocType => DocumentTypes.PurchaseRequest;

    /// <inheritdoc />
    protected override int LineCount => _lines.Count;

    /// <summary>Lập PR nháp.</summary>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty hoặc số.</exception>
    public static PurchaseRequest Create(Guid companyId, string no, PurchaseRequestHeader header, Guid? requesterId)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Đề nghị mua hàng phải thuộc một công ty.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Chứng từ phải có số.");
        var request = new PurchaseRequest { CompanyId = companyId, No = no, RequesterId = requesterId };
        request.Update(header);
        return request;
    }

    /// <summary>Sửa đầu phiếu.</summary>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, chưa chọn bộ phận hoặc ngày cần trước ngày đề nghị.</exception>
    public void Update(PurchaseRequestHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureDraft();
        // PR bắt buộc có bộ phận đề nghị (yêu cầu 01/11/2026): ngân sách công ty × bộ phận × tháng (lát C3) tính theo bộ phận
        // của PR, PR không có bộ phận sẽ lọt ngoài ngân sách – design.md mục 18.5.
        if (header.DepartmentId is null || header.DepartmentId == Guid.Empty) throw new BusinessRuleException("Hãy chọn bộ phận đề nghị.");
        if (header.NeededDate is { } needed && needed < header.Date) throw new BusinessRuleException("Ngày cần hàng không được trước ngày đề nghị.");
        if (!Enum.IsDefined(header.Priority)) throw new BusinessRuleException("Mức ưu tiên không hợp lệ.");
        Date = header.Date;
        DepartmentId = header.DepartmentId;
        Purpose = Clean(header.Purpose);
        NeededDate = header.NeededDate;
        WarehouseId = header.WarehouseId;
        Priority = header.Priority;
        Note = Clean(header.Note);
    }

    /// <summary>Thay toàn bộ dòng, tính lại tổng.</summary>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, không có dòng hoặc dòng sai.</exception>
    public void SetLines(IReadOnlyList<(PurchaseRequestLineData Data, Item Item)> lines)
    {
        EnsureDraft();
        if (lines.Count == 0) throw new BusinessRuleException("Phiếu phải có ít nhất một dòng hàng.");
        var keep = new List<PurchaseRequestLine>();
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
        TotalAmount = _lines.Sum(l => l.Amount);

        PurchaseRequestLine AddLine()
        {
            var line = new PurchaseRequestLine(Id);
            _lines.Add(line);
            return line;
        }
    }
}

/// <summary>Dòng đề nghị mua hàng.</summary>
[Auditable("Dòng đề nghị mua hàng")]
public sealed class PurchaseRequestLine : Entity
{
    private PurchaseRequestLine()
    {
    }

    internal PurchaseRequestLine(Guid requestId) => RequestId = requestId;

    /// <summary>PR.</summary>
    public Guid RequestId { get; private set; }

    /// <summary>Số thứ tự.</summary>
    public int LineNo { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>ĐVT.</summary>
    public Guid UomId { get; private set; }

    /// <summary>SL đề nghị – numeric(18,4).</summary>
    public decimal Qty { get; private set; }

    /// <summary>Hệ số ĐVT cơ sở – numeric(18,6).</summary>
    public decimal UomFactor { get; private set; }

    /// <summary>SL theo ĐVT cơ sở – numeric(18,4).</summary>
    public decimal BaseQty { get; private set; }

    /// <summary>Đơn giá tham khảo theo ĐVT trên phiếu – numeric(19,6).</summary>
    public decimal EstPrice { get; private set; }

    /// <summary>Thành tiền dự kiến = SL × đơn giá tham khảo, làm tròn 2 số – numeric(19,2).</summary>
    public decimal Amount { get; private set; }

    /// <summary>NCC đề xuất.</summary>
    public Guid? SuggestedSupplierId { get; private set; }

    /// <summary>Ngày cần (riêng dòng).</summary>
    public DateOnly? NeededDate { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    internal void Set(int lineNo, PurchaseRequestLineData data, Item item)
    {
        var prefix = $"Dòng {lineNo} ({item.Code})";
        CheckItem(item, data, prefix);
        if (data.EstPrice < 0 || decimal.Round(data.EstPrice, 6) != data.EstPrice)
            throw new BusinessRuleException($"{prefix}: đơn giá tham khảo phải ≥ 0, tối đa 6 chữ số thập phân.");
        LineNo = lineNo;
        ItemId = item.Id;
        UomId = data.UomId;
        Qty = data.Qty;
        UomFactor = Factor(item, data.UomId, prefix);
        BaseQty = decimal.Round(data.Qty * UomFactor, 4, MidpointRounding.AwayFromZero);
        EstPrice = data.EstPrice;
        Amount = decimal.Round(data.Qty * data.EstPrice, 2, MidpointRounding.AwayFromZero);
        SuggestedSupplierId = data.SuggestedSupplierId;
        NeededDate = data.NeededDate;
        Note = string.IsNullOrWhiteSpace(data.Note) ? null : data.Note.Trim();
    }

    private void CheckItem(Item item, PurchaseRequestLineData data, string prefix)
    {
        if (data.ItemId != item.Id) throw new BusinessRuleException($"{prefix}: mặt hàng không khớp.");
        if (!item.IsActive && item.Id != ItemId) throw new BusinessRuleException($"{prefix}: mặt hàng đã ngừng dùng.");
        if (data.Qty <= 0 || decimal.Round(data.Qty, 4) != data.Qty || data.Qty >= 100_000_000_000_000m)
            throw new BusinessRuleException($"{prefix}: số lượng phải lớn hơn 0, tối đa 4 chữ số thập phân.");
    }

    private static decimal Factor(Item item, Guid uomId, string prefix) =>
        uomId == item.BaseUomId ? 1m
        : item.Conversions.FirstOrDefault(c => c.UomId == uomId)?.Factor
          ?? throw new BusinessRuleException($"{prefix}: ĐVT phải là ĐVT cơ sở hoặc ĐVT quy đổi của mặt hàng.");
}

/// <summary>Đơn mua hàng – PO (design.md mục 6.2, 10.3). Giai đoạn 5 chỉ dùng VND.</summary>
[Auditable("Đơn mua hàng", LabelProperty = nameof(No))]
public sealed class PurchaseOrder : ApprovableDocument
{
    /// <summary>Dung sai nhận hàng tối đa (%).</summary>
    public const decimal MaxTolerancePct = 20m;

    private readonly List<PurchaseOrderLine> _lines = [];

    private PurchaseOrder()
    {
    }

    /// <summary>Nhà cung cấp.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Kho nhận.</summary>
    public Guid WarehouseId { get; private set; }

    /// <summary>
    /// Bộ phận đặt hàng (danh mục bộ phận cùng công ty), không bắt buộc. Hợp đồng nguyên tắc có danh sách bộ phận được phép đặt thì
    /// chỉ áp giá hợp đồng cho PO của bộ phận trong danh sách (design.md mục 18.10 – G5).
    /// </summary>
    public Guid? DepartmentId { get; private set; }

    /// <summary>Ngày giao dự kiến.</summary>
    public DateOnly? ExpectedDate { get; private set; }

    /// <summary>Điều khoản thanh toán.</summary>
    public string? PaymentTerms { get; private set; }

    /// <summary>Điều khoản giao hàng.</summary>
    public string? DeliveryTerms { get; private set; }

    /// <summary>Địa chỉ giao.</summary>
    public string? DeliveryAddress { get; private set; }

    /// <summary>Tiền tệ (giai đoạn 5 chỉ VND, tỷ giá 1).</summary>
    public string CurrencyCode { get; private set; } = "VND";

    /// <summary>Dung sai nhận hàng (%): phiếu nhập được nhận tới SL đặt × (1 + dung sai).</summary>
    public decimal ReceiveTolerancePct { get; private set; }

    /// <summary>Tiền hàng trước chiết khấu.</summary>
    public decimal GrossAmount { get; private set; }

    /// <summary>Tiền chiết khấu.</summary>
    public decimal TotalDiscount { get; private set; }

    /// <summary>Tiền hàng sau chiết khấu, trước VAT.</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Tiền VAT.</summary>
    public decimal TotalVat { get; private set; }

    /// <summary>Các dòng.</summary>
    public IReadOnlyList<PurchaseOrderLine> Lines => _lines;

    /// <summary>Ngưỡng duyệt xét trên tổng thanh toán (gồm VAT).</summary>
    public override decimal ApprovalAmount => TotalAmount + TotalVat;

    /// <inheritdoc />
    public override string DocType => DocumentTypes.PurchaseOrder;

    /// <inheritdoc />
    public override IReadOnlyCollection<Guid> RelatedSupplierIds => [SupplierId];

    /// <inheritdoc />
    protected override int LineCount => _lines.Count;

    /// <summary>Lập PO nháp.</summary>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, số, NCC, kho.</exception>
    public static PurchaseOrder Create(Guid companyId, string no, PurchaseOrderHeader header)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Đơn mua hàng phải thuộc một công ty.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Chứng từ phải có số.");
        var order = new PurchaseOrder { CompanyId = companyId, No = no };
        order.Update(header);
        return order;
    }

    /// <summary>Sửa đầu phiếu.</summary>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, thiếu NCC/kho, dung sai sai.</exception>
    public void Update(PurchaseOrderHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureDraft();
        if (header.SupplierId == Guid.Empty) throw new BusinessRuleException("Hãy chọn nhà cung cấp.");
        if (header.WarehouseId == Guid.Empty) throw new BusinessRuleException("Hãy chọn kho nhận.");
        if (header.ExpectedDate is { } expected && expected < header.Date) throw new BusinessRuleException("Ngày giao dự kiến không được trước ngày đặt hàng.");
        if (header.ReceiveTolerancePct is < 0 or > MaxTolerancePct)
            throw new BusinessRuleException($"Dung sai nhận hàng phải từ 0 đến {MaxTolerancePct:0}%.");
        Date = header.Date;
        SupplierId = header.SupplierId;
        WarehouseId = header.WarehouseId;
        DepartmentId = header.DepartmentId == Guid.Empty ? null : header.DepartmentId;
        ExpectedDate = header.ExpectedDate;
        PaymentTerms = Clean(header.PaymentTerms);
        DeliveryTerms = Clean(header.DeliveryTerms);
        DeliveryAddress = Clean(header.DeliveryAddress);
        ReceiveTolerancePct = header.ReceiveTolerancePct;
        Note = Clean(header.Note);
    }

    /// <summary>Thay toàn bộ dòng, tính lại tổng.</summary>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, không có dòng hoặc dòng sai.</exception>
    public void SetLines(IReadOnlyList<(PurchaseOrderLineData Data, Item Item)> lines)
    {
        EnsureDraft();
        if (lines.Count == 0) throw new BusinessRuleException("Phiếu phải có ít nhất một dòng hàng.");
        var keep = new List<PurchaseOrderLine>();
        var lineNo = 0;
        foreach (var (data, item) in lines)
        {
            lineNo++;
            PurchaseOrderLine line;
            if (data.Id is { } id)
            {
                line = _lines.FirstOrDefault(l => l.Id == id) ?? throw new BusinessRuleException($"Dòng {lineNo}: không tìm thấy dòng cần sửa – hãy tải lại phiếu.");
            }
            else
            {
                line = new PurchaseOrderLine(Id);
                _lines.Add(line);
            }

            line.Set(lineNo, data, item);
            keep.Add(line);
        }

        _lines.RemoveAll(l => !keep.Contains(l));
        GrossAmount = _lines.Sum(l => l.GrossAmount);
        TotalDiscount = _lines.Sum(l => l.DiscountAmount);
        TotalAmount = _lines.Sum(l => l.Amount);
        TotalVat = _lines.Sum(l => l.VatAmount);
        RefreshPriceFlags();
    }

    /// <summary>
    /// Có dòng đơn giá khác giá đã duyệt – PO phải qua chặng duyệt ngoại lệ giá (design.md mục 18.10). Lưu thành cột để lọc danh sách,
    /// báo cáo mà không tính lại từ dòng.
    /// </summary>
    public bool HasPriceException { get; private set; }

    /// <summary>Có dòng mua ngoài hợp đồng (không có giá đã duyệt hoặc lệch giá đã duyệt) – đo tỷ lệ mua theo hợp đồng (P5).</summary>
    public bool IsOutOfContract { get; private set; }

    /// <summary>Gán giá đã duyệt cho từng dòng (server tính lúc lưu và lúc gửi duyệt – giá có thể đổi giữa hai lần).</summary>
    /// <param name="resolve">Giá đã duyệt của dòng, null nếu không có.</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp.</exception>
    public void ApplyApprovedPrices(Func<PurchaseOrderLine, ApprovedPrice?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        EnsureDraft();
        foreach (var line in _lines) line.ApplyApprovedPrice(resolve(line));
        RefreshPriceFlags();
    }

    /// <summary>
    /// Ghi phiếu NCC độc quyền còn tác dụng của từng dòng (design.md mục 18.10 – ghi nhận mua theo độc quyền cho KSNB), tính cùng lúc
    /// với giá đã duyệt.
    /// </summary>
    /// <param name="resolve">Phiếu độc quyền của dòng, null nếu không có.</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp.</exception>
    public void ApplySoleSources(Func<PurchaseOrderLine, Guid?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        EnsureDraft();
        foreach (var line in _lines) line.SoleSourceApprovalId = resolve(line);
    }

    private void RefreshPriceFlags()
    {
        HasPriceException = _lines.Any(l => l.IsPriceDeviation);
        IsOutOfContract = _lines.Any(l => l.IsOutOfContract);
    }

    /// <summary>
    /// Kiểm tra trước khi gửi duyệt: dòng lệch giá đã duyệt phải có lý do (quyết định M1 – Nhóm Vận hành không tự đổi giá đã đàm phán).
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi có dòng lệch giá chưa nhập lý do (liệt kê số dòng).</exception>
    public void EnsurePriceDeviationsExplained()
    {
        var missing = _lines.Where(l => l.IsPriceDeviation && l.PriceDeviationReason is null).OrderBy(l => l.LineNo).Select(l => l.LineNo).ToList();
        if (missing.Count > 0)
            throw new BusinessRuleException(
                $"Dòng {string.Join(", ", missing)}: đơn giá khác giá đã duyệt (hợp đồng/bảng giá/kết quả chọn thầu) – phải nhập lý do lệch giá trước khi gửi duyệt.");
    }

    /// <summary>Nhận đủ hàng: Đã duyệt → Đã nhận đủ (đóng).</summary>
    public void CloseReceived()
    {
        if (Status == ApprovalDocumentStatus.Approved) Status = ApprovalDocumentStatus.Closed;
    }

    /// <summary>Bỏ ghi sổ phiếu nhập làm PO thiếu hàng trở lại: Đã nhận đủ → Đã duyệt.</summary>
    public void ReopenReceiving()
    {
        if (Status == ApprovalDocumentStatus.Closed) Status = ApprovalDocumentStatus.Approved;
    }
}

/// <summary>Dòng đơn mua hàng.</summary>
[Auditable("Dòng đơn mua hàng")]
public sealed class PurchaseOrderLine : Entity
{
    private PurchaseOrderLine()
    {
    }

    internal PurchaseOrderLine(Guid orderId) => OrderId = orderId;

    /// <summary>PO.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Số thứ tự.</summary>
    public int LineNo { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>ĐVT mua.</summary>
    public Guid UomId { get; private set; }

    /// <summary>SL đặt – numeric(18,4).</summary>
    public decimal Qty { get; private set; }

    /// <summary>Hệ số ĐVT cơ sở – numeric(18,6).</summary>
    public decimal UomFactor { get; private set; }

    /// <summary>SL đặt theo ĐVT cơ sở – numeric(18,4).</summary>
    public decimal BaseQty { get; private set; }

    /// <summary>Đơn giá trước chiết khấu, VAT – numeric(19,6).</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>% chiết khấu (0–100) – numeric(9,4).</summary>
    public decimal DiscountPct { get; private set; }

    /// <summary>Tiền hàng trước chiết khấu = SL × đơn giá – numeric(19,2).</summary>
    public decimal GrossAmount { get; private set; }

    /// <summary>Tiền chiết khấu = tiền hàng × % – numeric(19,2).</summary>
    public decimal DiscountAmount { get; private set; }

    /// <summary>Thành tiền sau chiết khấu, trước VAT – numeric(19,2).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Thuế suất.</summary>
    public string? VatCode { get; private set; }

    /// <summary>Tiền VAT = thành tiền × thuế suất – numeric(19,2).</summary>
    public decimal VatAmount { get; private set; }

    /// <summary>Ngày giao (riêng dòng).</summary>
    public DateOnly? DeliveryDate { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Dòng PR nguồn.</summary>
    public Guid? SourcePrLineId { get; private set; }

    /// <summary>
    /// Đơn giá thực mua theo ĐVT trên phiếu (sau chiết khấu) – giá nhập kho khi lập phiếu nhập từ PO.
    /// Ví dụ 100.000 đ, chiết khấu 5% → 95.000 đ.
    /// </summary>
    public decimal NetUnitPrice => Math.Round(UnitPrice * (100m - DiscountPct) / 100m, 6, MidpointRounding.AwayFromZero);

    /// <summary>Nguồn giá (design.md mục 18.10).</summary>
    public PurchasePriceSource PriceSource { get; private set; }

    /// <summary>Đơn giá đã duyệt theo ĐVT dòng, trước chiết khấu – numeric(19,6); null khi không có giá đã duyệt.</summary>
    public decimal? ApprovedUnitPrice { get; private set; }

    /// <summary>% chiết khấu đã duyệt – numeric(9,4).</summary>
    public decimal? ApprovedDiscountPct { get; private set; }

    /// <summary>Bảng giá nguồn (nguồn hợp đồng hoặc bảng giá).</summary>
    public Guid? PriceListId { get; private set; }

    /// <summary>Hợp đồng nguyên tắc nguồn.</summary>
    public Guid? ContractId { get; private set; }

    /// <summary>Dòng RFQ đã chọn thầu (nguồn chọn thầu).</summary>
    public Guid? SourceRfqLineId { get; private set; }

    /// <summary>Lý do đơn giá khác giá đã duyệt.</summary>
    public string? PriceDeviationReason { get; private set; }

    /// <summary>Phiếu NCC độc quyền còn tác dụng vào ngày PO cho NCC × mặt hàng của dòng (design.md mục 18.9, 18.10).</summary>
    public Guid? SoleSourceApprovalId { get; internal set; }

    /// <summary>
    /// Giá thực (sau chiết khấu) khác giá đã duyệt (sau chiết khấu). So sau chiết khấu để "đơn giá 100.000, CK 5%" và "đơn giá 95.000,
    /// CK 0%" coi là như nhau. Lệch cả khi rẻ hơn: giá đã đàm phán là giá mua, đổi giá phải được duyệt (quyết định M1).
    /// </summary>
    public bool IsPriceDeviation =>
        ApprovedUnitPrice is { } approved
        && NetUnitPrice != Math.Round(approved * (100m - (ApprovedDiscountPct ?? 0m)) / 100m, 6, MidpointRounding.AwayFromZero);

    /// <summary>Mua ngoài hợp đồng: không có giá đã duyệt hoặc lệch giá đã duyệt (P5). Dòng lập trước bản 0.12.0 không tính.</summary>
    public bool IsOutOfContract => PriceSource == PurchasePriceSource.None || IsPriceDeviation;

    /// <summary>
    /// Gán dòng và tính tiền: tiền hàng = SL × đơn giá; chiết khấu = tiền hàng × %; thành tiền = tiền hàng − chiết khấu;
    /// VAT = thành tiền × thuế suất; mỗi bước làm tròn 2 số (0,5 lên). Ví dụ 10 × 100.000, CK 5%, VAT 10% →
    /// 1.000.000; 50.000; 950.000; 95.000.
    /// </summary>
    internal void Set(int lineNo, PurchaseOrderLineData data, Item item)
    {
        var prefix = $"Dòng {lineNo} ({item.Code})";
        if (data.ItemId != item.Id) throw new BusinessRuleException($"{prefix}: mặt hàng không khớp.");
        if (!item.IsActive && item.Id != ItemId) throw new BusinessRuleException($"{prefix}: mặt hàng đã ngừng dùng.");
        if (data.Qty <= 0 || decimal.Round(data.Qty, 4) != data.Qty || data.Qty >= 100_000_000_000_000m)
            throw new BusinessRuleException($"{prefix}: số lượng phải lớn hơn 0, tối đa 4 chữ số thập phân.");
        if (data.UnitPrice < 0 || decimal.Round(data.UnitPrice, 6) != data.UnitPrice || data.UnitPrice >= 10_000_000_000_000m)
            throw new BusinessRuleException($"{prefix}: đơn giá phải ≥ 0, tối đa 6 chữ số thập phân.");
        if (data.DiscountPct is < 0 or > 100) throw new BusinessRuleException($"{prefix}: chiết khấu phải từ 0 đến 100%.");
        var vat = string.IsNullOrWhiteSpace(data.VatCode) ? null : data.VatCode.Trim().ToUpperInvariant();
        if (vat is not null && !VatRates.All.Contains(vat))
            throw new BusinessRuleException($"{prefix}: thuế suất phải là một trong {string.Join(", ", VatRates.All)}.");

        LineNo = lineNo;
        ItemId = item.Id;
        UomId = data.UomId;
        Qty = data.Qty;
        UomFactor = data.UomId == item.BaseUomId ? 1m
            : item.Conversions.FirstOrDefault(c => c.UomId == data.UomId)?.Factor
              ?? throw new BusinessRuleException($"{prefix}: ĐVT phải là ĐVT cơ sở hoặc ĐVT quy đổi của mặt hàng.");
        BaseQty = decimal.Round(data.Qty * UomFactor, 4, MidpointRounding.AwayFromZero);
        UnitPrice = data.UnitPrice;
        DiscountPct = data.DiscountPct;
        GrossAmount = decimal.Round(data.Qty * data.UnitPrice, 2, MidpointRounding.AwayFromZero);
        DiscountAmount = decimal.Round(GrossAmount * data.DiscountPct / 100m, 2, MidpointRounding.AwayFromZero);
        Amount = GrossAmount - DiscountAmount;
        VatCode = vat;
        VatAmount = vat is null or VatRates.NotSubject
            ? 0m
            : decimal.Round(Amount * decimal.Parse(vat, CultureInfo.InvariantCulture) / 100m, 2, MidpointRounding.AwayFromZero);
        DeliveryDate = data.DeliveryDate;
        Note = string.IsNullOrWhiteSpace(data.Note) ? null : data.Note.Trim();
        SourcePrLineId = data.SourcePrLineId;
        var reason = string.IsNullOrWhiteSpace(data.PriceDeviationReason) ? null : data.PriceDeviationReason.Trim();
        if (reason is { Length: > 500 }) throw new BusinessRuleException($"{prefix}: lý do lệch giá tối đa 500 ký tự.");
        PriceDeviationReason = reason;
        SourceRfqLineId = data.SourceRfqLineId;
    }

    /// <summary>
    /// Ghi giá đã duyệt của dòng; không có thì nguồn "ngoài hợp đồng". Hết lệch giá thì xóa lý do cũ để báo cáo không ghi nhầm.
    /// </summary>
    internal void ApplyApprovedPrice(ApprovedPrice? price)
    {
        PriceSource = price?.Source ?? PurchasePriceSource.None;
        ApprovedUnitPrice = price?.UnitPrice;
        ApprovedDiscountPct = price?.DiscountPct;
        PriceListId = price?.PriceListId;
        ContractId = price?.ContractId;
        SourceRfqLineId = price?.RfqLineId;
        if (!IsPriceDeviation) PriceDeviationReason = null;
    }
}
