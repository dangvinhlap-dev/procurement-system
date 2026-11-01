using Proc.Domain.Common;

namespace Proc.Domain.Analysis;

/// <summary>
/// Một lần nhập file Excel dữ liệu mua lịch sử (design.md mục 18.1). Ghi nhật ký khi nhập và khi xóa cả lô (nhập nhầm); các dòng
/// của lô không ghi nhật ký từng dòng vì một lô tới 50.000 dòng – nhật ký của lô đã đủ để biết ai nhập, ai xóa, bao nhiêu dòng.
/// </summary>
[Auditable("Lô dữ liệu mua lịch sử", LabelProperty = nameof(FileName))]
public sealed class PurchaseHistoryBatch : AuditableEntity, ICompanyOwned
{
    /// <summary>Độ dài tối đa của tên file.</summary>
    public const int FileNameMaxLength = 255;

    private PurchaseHistoryBatch()
    {
    }

    /// <summary>Pháp nhân mua (công ty làm việc lúc nhập).</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Tên file người dùng tải lên.</summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>Số dòng đã nhập.</summary>
    public int RecordCount { get; private set; }

    /// <summary>Ngày mua sớm nhất trong lô.</summary>
    public DateOnly FromDate { get; private set; }

    /// <summary>Ngày mua muộn nhất trong lô.</summary>
    public DateOnly ToDate { get; private set; }

    /// <summary>
    /// Tạo lô nhập. Người nhập, thời điểm nhập là CreatedBy/CreatedAt do interceptor điền.
    /// </summary>
    /// <param name="id">Id lô, cấp trước khi kiểm tra dòng để các dòng tham chiếu được lô (ghi cùng một lần bằng COPY).</param>
    /// <param name="companyId">Pháp nhân mua.</param>
    /// <param name="fileName">Tên file (dài quá thì cắt).</param>
    /// <param name="records">Các dòng của lô (đã kiểm tra), ít nhất một dòng.</param>
    /// <returns>Lô mới.</returns>
    /// <exception cref="BusinessRuleException">Khi lô không có dòng nào hoặc có dòng không thuộc lô, công ty này.</exception>
    public static PurchaseHistoryBatch Create(Guid id, Guid companyId, string fileName, IReadOnlyCollection<PurchaseHistoryRecord> records)
    {
        if (records.Count == 0) throw new BusinessRuleException("File không có dòng dữ liệu nào.");
        if (records.Any(r => r.BatchId != id || r.CompanyId != companyId))
            throw new BusinessRuleException("Dòng dữ liệu mua không thuộc lô hoặc công ty của lô.");
        var name = string.IsNullOrWhiteSpace(fileName) ? "du-lieu-mua.xlsx" : fileName.Trim();
        return new PurchaseHistoryBatch
        {
            Id = id,
            CompanyId = companyId,
            FileName = name.Length <= FileNameMaxLength ? name : name[..FileNameMaxLength],
            RecordCount = records.Count,
            FromDate = records.Min(r => r.PurchaseDate),
            ToDate = records.Max(r => r.PurchaseDate),
        };
    }
}

/// <summary>
/// Dữ liệu một dòng mua lịch sử như trên file Excel – 12 nhóm trường của đề xuất Ban Cung ứng (design.md mục 18.1).
/// Chữ trống là null; Có/Không trống là chưa đánh giá.
/// </summary>
/// <param name="Department">Bộ phận sử dụng.</param>
/// <param name="ItemCode">Mã vật tư theo pháp nhân (bắt buộc).</param>
/// <param name="ItemName">Tên vật tư (bắt buộc).</param>
/// <param name="Specification">Quy cách.</param>
/// <param name="Brand">Thương hiệu.</param>
/// <param name="Origin">Xuất xứ.</param>
/// <param name="Standard">Tiêu chuẩn kỹ thuật.</param>
/// <param name="Uom">Đơn vị tính (bắt buộc, chữ như trên chứng từ cũ – chưa quy đổi theo danh mục).</param>
/// <param name="RequestDate">Ngày yêu cầu.</param>
/// <param name="PurchaseDate">Ngày mua (bắt buộc).</param>
/// <param name="Quantity">Số lượng &gt; 0.</param>
/// <param name="UnitPrice">Đơn giá trước VAT ≥ 0.</param>
/// <param name="VatPercent">% VAT từ 0 đến 100.</param>
/// <param name="Currency">Mã tiền tệ 3 chữ; trống = VND.</param>
/// <param name="RelatedCost">Chi phí liên quan (vận chuyển, bốc xếp…) ≥ 0.</param>
/// <param name="RelatedCostNote">Ghi chú chi phí liên quan.</param>
/// <param name="SupplierName">Tên NCC (bắt buộc).</param>
/// <param name="SupplierTaxCode">Mã số thuế NCC – dùng để tự khớp NCC trong danh mục.</param>
/// <param name="SupplierContact">Người liên hệ, điện thoại NCC.</param>
/// <param name="PaymentTerms">Điều kiện thanh toán.</param>
/// <param name="Discount">Chiết khấu (chữ tự do: "2%", "500.000 đ"…).</param>
/// <param name="Sponsorship">Tài trợ, khuyến mại kèm theo.</param>
/// <param name="OnTime">Giao đúng hạn.</param>
/// <param name="QualityPassed">Đạt chất lượng.</param>
/// <param name="Complaint">Đổi trả, khiếu nại.</param>
public sealed record PurchaseHistoryData(
    string? Department, string? ItemCode, string? ItemName, string? Specification, string? Brand, string? Origin, string? Standard,
    string? Uom, DateOnly? RequestDate, DateOnly? PurchaseDate, decimal? Quantity, decimal? UnitPrice, decimal? VatPercent,
    string? Currency, decimal? RelatedCost, string? RelatedCostNote, string? SupplierName, string? SupplierTaxCode,
    string? SupplierContact, string? PaymentTerms, string? Discount, string? Sponsorship, bool? OnTime, bool? QualityPassed,
    string? Complaint);

/// <summary>
/// Một dòng dữ liệu mua lịch sử của một pháp nhân (design.md mục 18.1), dùng để phân tích (E2, E3). Không ghi sổ kho, không ảnh hưởng
/// tồn, giá vốn hay lịch sử giá. <see cref="ItemId"/>, <see cref="SupplierId"/> trống khi chưa khớp danh mục – lát E2 ánh xạ tiếp.
/// </summary>
public sealed class PurchaseHistoryRecord : Entity, ICompanyOwned
{
    /// <summary>Độ dài tối đa của chuỗi tìm kiếm.</summary>
    public const int SearchTextMaxLength = 1000;

    /// <summary>Độ dài tối đa của các trường chữ (tên trường → độ dài) – cấu hình bảng dùng chung để không lệch với kiểm tra.</summary>
    public static readonly IReadOnlyDictionary<string, int> MaxLengths = new Dictionary<string, int>
    {
        [nameof(Department)] = 200,
        [nameof(ItemCode)] = 50,
        [nameof(ItemName)] = 300,
        [nameof(Specification)] = 500,
        [nameof(Brand)] = 100,
        [nameof(Origin)] = 100,
        [nameof(Standard)] = 200,
        [nameof(Uom)] = 50,
        [nameof(RelatedCostNote)] = 500,
        [nameof(SupplierName)] = 300,
        [nameof(SupplierTaxCode)] = 20,
        [nameof(SupplierContact)] = 300,
        [nameof(PaymentTerms)] = 300,
        [nameof(Discount)] = 200,
        [nameof(Sponsorship)] = 300,
        [nameof(Complaint)] = 500,
    };

    private PurchaseHistoryRecord()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Lô nhập.</summary>
    public Guid BatchId { get; private set; }

    /// <summary>Số dòng trên file Excel – để đối chiếu với file gốc.</summary>
    public int RowNumber { get; private set; }

    /// <summary>Bộ phận sử dụng.</summary>
    public string? Department { get; private set; }

    /// <summary>Mã vật tư theo pháp nhân (chữ hoa).</summary>
    public string ItemCode { get; private set; } = string.Empty;

    /// <summary>Tên vật tư.</summary>
    public string ItemName { get; private set; } = string.Empty;

    /// <summary>Quy cách.</summary>
    public string? Specification { get; private set; }

    /// <summary>Thương hiệu.</summary>
    public string? Brand { get; private set; }

    /// <summary>Xuất xứ.</summary>
    public string? Origin { get; private set; }

    /// <summary>Tiêu chuẩn kỹ thuật.</summary>
    public string? Standard { get; private set; }

    /// <summary>Đơn vị tính (chữ).</summary>
    public string Uom { get; private set; } = string.Empty;

    /// <summary>Ngày yêu cầu.</summary>
    public DateOnly? RequestDate { get; private set; }

    /// <summary>Ngày mua.</summary>
    public DateOnly PurchaseDate { get; private set; }

    /// <summary>Số lượng – numeric(18,4).</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Đơn giá trước VAT – numeric(19,6).</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>% VAT.</summary>
    public decimal? VatPercent { get; private set; }

    /// <summary>Mã tiền tệ.</summary>
    public string Currency { get; private set; } = "VND";

    /// <summary>Thành tiền trước VAT = SL × đơn giá, làm tròn 2 số lẻ – numeric(19,2).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Chi phí liên quan – numeric(19,2).</summary>
    public decimal? RelatedCost { get; private set; }

    /// <summary>Ghi chú chi phí liên quan.</summary>
    public string? RelatedCostNote { get; private set; }

    /// <summary>Tên NCC.</summary>
    public string SupplierName { get; private set; } = string.Empty;

    /// <summary>Mã số thuế NCC.</summary>
    public string? SupplierTaxCode { get; private set; }

    /// <summary>Liên hệ NCC.</summary>
    public string? SupplierContact { get; private set; }

    /// <summary>Điều kiện thanh toán.</summary>
    public string? PaymentTerms { get; private set; }

    /// <summary>Chiết khấu.</summary>
    public string? Discount { get; private set; }

    /// <summary>Tài trợ.</summary>
    public string? Sponsorship { get; private set; }

    /// <summary>Giao đúng hạn; null = chưa đánh giá.</summary>
    public bool? OnTime { get; private set; }

    /// <summary>Đạt chất lượng; null = chưa đánh giá.</summary>
    public bool? QualityPassed { get; private set; }

    /// <summary>Đổi trả, khiếu nại.</summary>
    public string? Complaint { get; private set; }

    /// <summary>Mặt hàng trong danh mục chung đã khớp; null = chưa khớp.</summary>
    public Guid? ItemId { get; private set; }

    /// <summary>NCC trong danh mục đã khớp; null = chưa khớp.</summary>
    public Guid? SupplierId { get; private set; }

    /// <summary>Chuỗi tìm kiếm không dấu (mã, tên vật tư, NCC, MST, bộ phận) – tìm có dấu hay không dấu đều được.</summary>
    public string SearchText { get; private set; } = string.Empty;

    /// <summary>
    /// Kiểm tra và tạo một dòng.
    /// </summary>
    /// <param name="companyId">Pháp nhân mua.</param>
    /// <param name="batchId">Lô nhập.</param>
    /// <param name="rowNumber">Số dòng trên file.</param>
    /// <param name="data">Dữ liệu đọc từ file.</param>
    /// <param name="today">Ngày hiện tại theo giờ Việt Nam – ngày mua không được sau hôm nay.</param>
    /// <returns>Dòng mới, chưa khớp danh mục.</returns>
    /// <exception cref="BusinessRuleException">Khi dữ liệu sai quy tắc.</exception>
    public static PurchaseHistoryRecord Create(Guid companyId, Guid batchId, int rowNumber, PurchaseHistoryData data, DateOnly today)
    {
        var itemCode = CodeRules.Normalize(data.ItemCode);
        if (itemCode.Length == 0) throw new BusinessRuleException("Thiếu mã vật tư.");
        var itemName = Clean(data.ItemName) ?? throw new BusinessRuleException("Thiếu tên vật tư.");
        var uom = Clean(data.Uom) ?? throw new BusinessRuleException("Thiếu đơn vị tính.");
        var supplierName = Clean(data.SupplierName) ?? throw new BusinessRuleException("Thiếu tên nhà cung cấp.");
        var purchaseDate = data.PurchaseDate ?? throw new BusinessRuleException("Thiếu ngày mua.");
        if (purchaseDate > today) throw new BusinessRuleException($"Ngày mua {purchaseDate:dd/MM/yyyy} sau ngày hôm nay.");
        if (data.RequestDate is { } requestDate && requestDate > purchaseDate)
            throw new BusinessRuleException($"Ngày yêu cầu {requestDate:dd/MM/yyyy} sau ngày mua {purchaseDate:dd/MM/yyyy}.");

        var quantity = data.Quantity ?? throw new BusinessRuleException("Thiếu số lượng.");
        if (quantity <= 0) throw new BusinessRuleException("Số lượng phải lớn hơn 0.");
        var unitPrice = data.UnitPrice ?? throw new BusinessRuleException("Thiếu đơn giá.");
        // Giá 0 vẫn hợp lệ: hàng tặng kèm, hàng tài trợ là dữ liệu mua thật cần phân tích.
        if (unitPrice < 0) throw new BusinessRuleException("Đơn giá không được âm.");
        if (data.VatPercent is < 0 or > 100) throw new BusinessRuleException("% VAT phải từ 0 đến 100.");
        if (data.RelatedCost is < 0) throw new BusinessRuleException("Chi phí liên quan không được âm.");

        // Phần mềm hiện chỉ dùng VND (ngoại tệ làm khi có yêu cầu – design.md mục 6.7); dữ liệu cũ mua bằng ngoại tệ vẫn lưu đúng mã
        // tiền để lát phân tích quy đổi, không đổi thành VND ở đây.
        var currency = CodeRules.Normalize(data.Currency);
        if (currency.Length == 0) currency = "VND";
        if (!CodeRules.IsAlphanumeric(currency, 3, 3) || currency.Any(char.IsDigit))
            throw new BusinessRuleException($"Tiền tệ ghi mã 3 chữ cái như VND, USD (đang là \"{data.Currency}\").");

        var record = new PurchaseHistoryRecord
        {
            CompanyId = companyId,
            BatchId = batchId,
            RowNumber = rowNumber,
            Department = Clean(data.Department),
            ItemCode = itemCode,
            ItemName = itemName,
            Specification = Clean(data.Specification),
            Brand = Clean(data.Brand),
            Origin = Clean(data.Origin),
            Standard = Clean(data.Standard),
            Uom = uom,
            RequestDate = data.RequestDate,
            PurchaseDate = purchaseDate,
            Quantity = quantity,
            UnitPrice = unitPrice,
            VatPercent = data.VatPercent,
            Currency = currency,
            // Tiền làm tròn 2 số lẻ kiểu thường (0,5 lên): 3,5 × 1.234,567 = 4.320,9845 → 4.320,98.
            Amount = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero),
            RelatedCost = data.RelatedCost,
            RelatedCostNote = Clean(data.RelatedCostNote),
            SupplierName = supplierName,
            // Chữ hoa như MST trong danh mục NCC (mã thuế nước ngoài có chữ) để tự khớp được.
            SupplierTaxCode = Clean(data.SupplierTaxCode)?.ToUpperInvariant(),
            SupplierContact = Clean(data.SupplierContact),
            PaymentTerms = Clean(data.PaymentTerms),
            Discount = Clean(data.Discount),
            Sponsorship = Clean(data.Sponsorship),
            OnTime = data.OnTime,
            QualityPassed = data.QualityPassed,
            Complaint = Clean(data.Complaint),
        };
        record.CheckLengths();
        var search = Common.SearchText.Combine(itemCode, itemName, record.Specification, supplierName, record.SupplierTaxCode, record.Department);
        record.SearchText = search.Length <= SearchTextMaxLength ? search : search[..SearchTextMaxLength];
        return record;
    }

    /// <summary>
    /// Gán kết quả khớp danh mục (mã vật tư trùng mã hàng chung, MST trùng NCC).
    /// </summary>
    /// <param name="itemId">Mặt hàng đã khớp; null = chưa khớp.</param>
    /// <param name="supplierId">NCC đã khớp; null = chưa khớp.</param>
    public void Match(Guid? itemId, Guid? supplierId)
    {
        ItemId = itemId;
        SupplierId = supplierId;
    }

    private void CheckLengths()
    {
        (string Field, string Label, string? Value)[] fields =
        [
            (nameof(Department), "Bộ phận sử dụng", Department), (nameof(ItemCode), "Mã vật tư", ItemCode),
            (nameof(ItemName), "Tên vật tư", ItemName), (nameof(Specification), "Quy cách", Specification),
            (nameof(Brand), "Thương hiệu", Brand), (nameof(Origin), "Xuất xứ", Origin), (nameof(Standard), "Tiêu chuẩn", Standard),
            (nameof(Uom), "ĐVT", Uom), (nameof(RelatedCostNote), "Ghi chú chi phí", RelatedCostNote),
            (nameof(SupplierName), "Tên NCC", SupplierName), (nameof(SupplierTaxCode), "MST NCC", SupplierTaxCode),
            (nameof(SupplierContact), "Liên hệ NCC", SupplierContact), (nameof(PaymentTerms), "Điều kiện thanh toán", PaymentTerms),
            (nameof(Discount), "Chiết khấu", Discount), (nameof(Sponsorship), "Tài trợ", Sponsorship),
            (nameof(Complaint), "Đổi trả / khiếu nại", Complaint),
        ];
        foreach (var (field, label, value) in fields)
        {
            if (value is not null && value.Length > MaxLengths[field])
                throw new BusinessRuleException($"\"{label}\" dài quá {MaxLengths[field]} ký tự.");
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
