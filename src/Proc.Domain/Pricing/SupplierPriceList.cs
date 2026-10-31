using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;

namespace Proc.Domain.Pricing;

/// <summary>Trạng thái bảng giá NCC.</summary>
public enum PriceListStatus
{
    /// <summary>Nháp – đang soạn, chưa dùng để gợi ý giá.</summary>
    Draft = 1,

    /// <summary>Hiệu lực – PO lấy giá từ đây trong khoảng hiệu lực; đưa vào lịch sử giá.</summary>
    Active = 2,

    /// <summary>Ngừng – không dùng nữa (giữ để tra cứu).</summary>
    Inactive = 3,
}

/// <summary>Đầu bảng giá NCC.</summary>
/// <param name="SupplierId">Nhà cung cấp.</param>
/// <param name="Title">Tên bảng giá (ví dụ "Bảng giá năm 2026").</param>
/// <param name="IsGroupWide">Áp dụng toàn tập đoàn (chỉ tổng công ty lập được – tầng trên kiểm tra).</param>
/// <param name="ValidFrom">Hiệu lực từ ngày.</param>
/// <param name="ValidTo">Hiệu lực đến ngày; null = tới khi ngừng.</param>
/// <param name="DeliveryTerms">Điều kiện giao hàng.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record PriceListHeader(
    Guid SupplierId, string Title, bool IsGroupWide, DateOnly ValidFrom, DateOnly? ValidTo, string? DeliveryTerms, string? Note);

/// <summary>Dữ liệu một dòng bảng giá.</summary>
/// <param name="Id">Id dòng đang có (null = dòng mới).</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UomId">ĐVT báo giá (cơ sở hoặc quy đổi).</param>
/// <param name="MinQty">Bậc số lượng: áp dụng khi SL đặt (theo ĐVT dòng) ≥ giá trị này; 0 = mọi số lượng.</param>
/// <param name="UnitPrice">Đơn giá trước chiết khấu, VAT.</param>
/// <param name="DiscountPct">% chiết khấu.</param>
/// <param name="VatCode">Thuế suất; trống = theo mặt hàng.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record PriceListLineData(
    Guid? Id, Guid ItemId, Guid UomId, decimal MinQty, decimal UnitPrice, decimal DiscountPct, string? VatCode, string? Note);

/// <summary>
/// Bảng giá / thỏa thuận giá của một NCC (design.md mục 8.1). Thuộc công ty lập (<see cref="CompanyId"/>); khi
/// <see cref="IsGroupWide"/> thì mọi công ty trong tập đoàn dùng được. Chỉ VND ở giai đoạn này.
/// Vòng đời: Nháp → Hiệu lực → Ngừng; bảng đã hiệu lực không sửa – muốn đổi giá thì ngừng bảng cũ, lập bảng mới (giữ lịch sử).
/// </summary>
[Auditable("Bảng giá NCC", LabelProperty = nameof(No))]
public sealed class SupplierPriceList : AuditableEntity, ICompanyOwned, IHasDocumentNumber
{
    private readonly List<SupplierPriceListLine> _lines = [];

    private SupplierPriceList()
    {
    }

    /// <summary>Công ty lập (quản lý) bảng giá.</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Số bảng giá.</summary>
    public string No { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string? DocumentNumber => No;

    /// <summary>Nhà cung cấp.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Tên bảng giá.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Áp dụng toàn tập đoàn.</summary>
    public bool IsGroupWide { get; private set; }

    /// <summary>Hiệu lực từ ngày.</summary>
    public DateOnly ValidFrom { get; private set; }

    /// <summary>Hiệu lực đến ngày (null = không hạn).</summary>
    public DateOnly? ValidTo { get; private set; }

    /// <summary>Tiền tệ (chỉ VND).</summary>
    public string CurrencyCode { get; private set; } = "VND";

    /// <summary>Điều kiện giao hàng.</summary>
    public string? DeliveryTerms { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Trạng thái.</summary>
    public PriceListStatus Status { get; private set; } = PriceListStatus.Draft;

    /// <summary>Thời điểm đưa vào hiệu lực (UTC).</summary>
    public DateTimeOffset? ActivatedAt { get; private set; }

    /// <summary>Các dòng giá.</summary>
    public IReadOnlyList<SupplierPriceListLine> Lines => _lines;

    /// <summary>Lập bảng giá nháp.</summary>
    /// <param name="companyId">Công ty lập.</param>
    /// <param name="no">Số bảng giá.</param>
    /// <param name="header">Đầu bảng giá.</param>
    /// <returns>Bảng giá mới.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, số hoặc đầu phiếu sai.</exception>
    public static SupplierPriceList Create(Guid companyId, string no, PriceListHeader header)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Bảng giá phải thuộc một công ty.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Bảng giá phải có số.");
        var list = new SupplierPriceList { CompanyId = companyId, No = no };
        list.Update(header);
        return list;
    }

    /// <summary>Sửa đầu bảng giá (chỉ khi nháp).</summary>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc dữ liệu sai.</exception>
    public void Update(PriceListHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureDraft();
        if (header.SupplierId == Guid.Empty) throw new BusinessRuleException("Hãy chọn nhà cung cấp.");
        if (string.IsNullOrWhiteSpace(header.Title)) throw new BusinessRuleException("Hãy nhập tên bảng giá.");
        if (header.ValidTo is { } to && to < header.ValidFrom) throw new BusinessRuleException("Ngày hết hiệu lực phải sau hoặc bằng ngày bắt đầu.");
        SupplierId = header.SupplierId;
        Title = header.Title.Trim();
        IsGroupWide = header.IsGroupWide;
        ValidFrom = header.ValidFrom;
        ValidTo = header.ValidTo;
        DeliveryTerms = Clean(header.DeliveryTerms);
        Note = Clean(header.Note);
    }

    /// <summary>
    /// Thay toàn bộ dòng. Mỗi (mặt hàng, ĐVT, bậc SL) chỉ một dòng – trùng thì một SL có hai giá.
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, không có dòng, dòng sai hoặc trùng bậc.</exception>
    public void SetLines(IReadOnlyList<(PriceListLineData Data, Item Item)> lines)
    {
        EnsureDraft();
        if (lines.Count == 0) throw new BusinessRuleException("Bảng giá phải có ít nhất một dòng.");
        var duplicate = lines.GroupBy(l => (l.Data.ItemId, l.Data.UomId, l.Data.MinQty)).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new BusinessRuleException($"Mặt hàng {duplicate.First().Item.Code} có hai dòng cùng ĐVT, cùng bậc số lượng {duplicate.Key.MinQty:0.####}.");
        var keep = new List<SupplierPriceListLine>();
        var lineNo = 0;
        foreach (var (data, item) in lines)
        {
            lineNo++;
            SupplierPriceListLine line;
            if (data.Id is { } id)
            {
                line = _lines.FirstOrDefault(l => l.Id == id) ?? throw new BusinessRuleException($"Dòng {lineNo}: không tìm thấy dòng cần sửa – hãy tải lại phiếu.");
            }
            else
            {
                line = new SupplierPriceListLine(Id);
                _lines.Add(line);
            }

            line.Set(lineNo, data, item);
            keep.Add(line);
        }

        _lines.RemoveAll(l => !keep.Contains(l));
    }

    /// <summary>Đưa vào hiệu lực: Nháp → Hiệu lực.</summary>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc chưa có dòng.</exception>
    public void Activate(DateTimeOffset at)
    {
        EnsureDraft();
        if (_lines.Count == 0) throw new BusinessRuleException("Bảng giá phải có ít nhất một dòng mới đưa vào hiệu lực được.");
        Status = PriceListStatus.Active;
        ActivatedAt = at;
    }

    /// <summary>Ngừng áp dụng: Hiệu lực → Ngừng.</summary>
    /// <exception cref="BusinessRuleException">Khi bảng giá không ở trạng thái hiệu lực.</exception>
    public void Deactivate()
    {
        if (Status != PriceListStatus.Active) throw new BusinessRuleException($"Bảng giá {No} không ở trạng thái hiệu lực.");
        Status = PriceListStatus.Inactive;
    }

    /// <summary>Bảng giá có hiệu lực vào ngày <paramref name="date"/> không.</summary>
    /// <param name="date">Ngày cần tra.</param>
    /// <returns>true nếu đang hiệu lực và ngày nằm trong khoảng hiệu lực.</returns>
    public bool AppliesOn(DateOnly date) => Status == PriceListStatus.Active && ValidFrom <= date && (ValidTo is null || ValidTo >= date);

    private void EnsureDraft()
    {
        if (Status != PriceListStatus.Draft) throw new BusinessRuleException($"Bảng giá {No} không còn ở trạng thái nháp – không sửa được.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Dòng bảng giá NCC: một mặt hàng × ĐVT × bậc số lượng.</summary>
[Auditable("Dòng bảng giá NCC")]
public sealed class SupplierPriceListLine : Entity
{
    private SupplierPriceListLine()
    {
    }

    internal SupplierPriceListLine(Guid priceListId) => PriceListId = priceListId;

    /// <summary>Bảng giá.</summary>
    public Guid PriceListId { get; private set; }

    /// <summary>Số thứ tự.</summary>
    public int LineNo { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>ĐVT báo giá.</summary>
    public Guid UomId { get; private set; }

    /// <summary>Hệ số ĐVT cơ sở – numeric(18,6).</summary>
    public decimal UomFactor { get; private set; }

    /// <summary>Bậc số lượng (theo ĐVT dòng) – numeric(18,4).</summary>
    public decimal MinQty { get; private set; }

    /// <summary>Đơn giá trước chiết khấu, VAT – numeric(19,6).</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>% chiết khấu – numeric(9,4).</summary>
    public decimal DiscountPct { get; private set; }

    /// <summary>Thuế suất; null = theo mặt hàng.</summary>
    public string? VatCode { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Giá thực mua quy về ĐVT cơ sở, trước VAT = đơn giá × (100 − %CK) / 100 / hệ số; làm tròn 6 số – dùng để so sánh giá
    /// (design.md mục 8.2). Ví dụ thùng 12 hộp giá 600.000, CK 5% → 570.000 / 12 = 47.500 đ/hộp.
    /// </summary>
    public decimal BaseNetPrice => PricingMath.BaseNetPrice(UnitPrice, DiscountPct, UomFactor);

    internal void Set(int lineNo, PriceListLineData data, Item item)
    {
        var prefix = $"Dòng {lineNo} ({item.Code})";
        if (data.ItemId != item.Id) throw new BusinessRuleException($"{prefix}: mặt hàng không khớp.");
        if (!item.IsActive && item.Id != ItemId) throw new BusinessRuleException($"{prefix}: mặt hàng đã ngừng dùng.");
        if (data.MinQty < 0 || decimal.Round(data.MinQty, 4) != data.MinQty || data.MinQty >= 100_000_000_000_000m)
            throw new BusinessRuleException($"{prefix}: bậc số lượng phải ≥ 0, tối đa 4 chữ số thập phân.");
        if (data.UnitPrice < 0 || decimal.Round(data.UnitPrice, 6) != data.UnitPrice || data.UnitPrice >= 10_000_000_000_000m)
            throw new BusinessRuleException($"{prefix}: đơn giá phải ≥ 0, tối đa 6 chữ số thập phân.");
        if (data.DiscountPct is < 0 or > 100) throw new BusinessRuleException($"{prefix}: chiết khấu phải từ 0 đến 100%.");
        var vat = string.IsNullOrWhiteSpace(data.VatCode) ? null : data.VatCode.Trim().ToUpperInvariant();
        if (vat is not null && !VatRates.All.Contains(vat))
            throw new BusinessRuleException($"{prefix}: thuế suất phải là một trong {string.Join(", ", VatRates.All)}.");
        LineNo = lineNo;
        ItemId = item.Id;
        UomId = data.UomId;
        UomFactor = data.UomId == item.BaseUomId ? 1m
            : item.Conversions.FirstOrDefault(c => c.UomId == data.UomId)?.Factor
              ?? throw new BusinessRuleException($"{prefix}: ĐVT phải là ĐVT cơ sở hoặc ĐVT quy đổi của mặt hàng.");
        MinQty = data.MinQty;
        UnitPrice = data.UnitPrice;
        DiscountPct = data.DiscountPct;
        VatCode = vat;
        Note = string.IsNullOrWhiteSpace(data.Note) ? null : data.Note.Trim();
    }
}

/// <summary>Phép tính giá dùng chung cho bảng giá, PO, báo giá khi so sánh (design.md mục 8.2).</summary>
public static class PricingMath
{
    /// <summary>
    /// Giá thực mua theo ĐVT cơ sở, trước VAT = đơn giá × (100 − %CK) / 100 / hệ số ĐVT, làm tròn 6 số (0,5 lên).
    /// Ví dụ 600.000 đ/thùng 12 hộp, CK 5% → 47.500 đ/hộp.
    /// </summary>
    /// <param name="unitPrice">Đơn giá theo ĐVT trên chứng từ.</param>
    /// <param name="discountPct">% chiết khấu.</param>
    /// <param name="uomFactor">Hệ số ĐVT cơ sở (&gt; 0).</param>
    /// <returns>Giá theo ĐVT cơ sở.</returns>
    public static decimal BaseNetPrice(decimal unitPrice, decimal discountPct, decimal uomFactor) =>
        uomFactor <= 0 ? 0m : Math.Round(unitPrice * (100m - discountPct) / 100m / uomFactor, 6, MidpointRounding.AwayFromZero);

}
