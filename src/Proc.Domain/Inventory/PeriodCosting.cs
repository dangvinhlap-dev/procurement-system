using Proc.Domain.Common;

namespace Proc.Domain.Inventory;

/// <summary>
/// Số dư một kỳ đã đóng theo công ty × kho × mặt hàng × lô (design.md mục 7.2 bước 5, 12.1): tồn cuối kỳ này là tồn đầu kỳ sau.
/// Chỉ sinh khi đóng kỳ, xóa khi mở lại kỳ; không gắn [Auditable] – việc đóng/mở kỳ đã ghi nhật ký trên kỳ.
/// </summary>
public sealed class StockPeriodBalance : Entity, ICompanyOwned
{
    private StockPeriodBalance()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Kỳ tháng.</summary>
    public Guid PeriodId { get; private set; }

    /// <summary>Kho.</summary>
    public Guid WarehouseId { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>Số lô (null = không theo lô).</summary>
    public string? LotNo { get; private set; }

    /// <summary>Tồn đầu kỳ (SL theo ĐVT cơ sở).</summary>
    public decimal OpenQty { get; private set; }

    /// <summary>Giá trị tồn đầu kỳ.</summary>
    public decimal OpenAmount { get; private set; }

    /// <summary>SL nhập trong kỳ.</summary>
    public decimal InQty { get; private set; }

    /// <summary>Giá trị nhập trong kỳ.</summary>
    public decimal InAmount { get; private set; }

    /// <summary>SL xuất trong kỳ.</summary>
    public decimal OutQty { get; private set; }

    /// <summary>Giá trị xuất trong kỳ.</summary>
    public decimal OutAmount { get; private set; }

    /// <summary>Tồn cuối kỳ = đầu + nhập − xuất.</summary>
    public decimal CloseQty { get; private set; }

    /// <summary>Giá trị tồn cuối kỳ = đầu + nhập − xuất.</summary>
    public decimal CloseAmount { get; private set; }

    /// <summary>Tạo số dư một dòng; tồn cuối tự tính.</summary>
    /// <returns>Số dư.</returns>
    public static StockPeriodBalance Create(
        Guid companyId, Guid periodId, Guid warehouseId, Guid itemId, string? lotNo,
        decimal openQty, decimal openAmount, decimal inQty, decimal inAmount, decimal outQty, decimal outAmount) =>
        new()
        {
            CompanyId = companyId, PeriodId = periodId, WarehouseId = warehouseId, ItemId = itemId, LotNo = lotNo,
            OpenQty = openQty, OpenAmount = openAmount, InQty = inQty, InAmount = inAmount, OutQty = outQty, OutAmount = outAmount,
            CloseQty = openQty + inQty - outQty, CloseAmount = openAmount + inAmount - outAmount,
        };
}

/// <summary>Giá vốn bình quân một mặt hàng của công ty trong một kỳ đã đóng (design.md mục 7.3, 12.1).</summary>
public sealed class ItemPeriodCost : Entity, ICompanyOwned
{
    private ItemPeriodCost()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Kỳ tháng.</summary>
    public Guid PeriodId { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>Giá vốn đơn vị kỳ theo ĐVT cơ sở – numeric(19,6).</summary>
    public decimal UnitCost { get; private set; }

    /// <summary>Thời điểm tính (UTC).</summary>
    public DateTimeOffset CalculatedAt { get; private set; }

    /// <summary>Tạo bản ghi giá vốn kỳ.</summary>
    /// <returns>Giá vốn kỳ.</returns>
    public static ItemPeriodCost Create(Guid companyId, Guid periodId, Guid itemId, decimal unitCost, DateTimeOffset at) =>
        new() { CompanyId = companyId, PeriodId = periodId, ItemId = itemId, UnitCost = unitCost, CalculatedAt = at };
}

/// <summary>Công thức giá vốn bình quân gia quyền cuối kỳ (design.md mục 7.3), tách riêng để kiểm thử bằng số.</summary>
public static class PeriodCosting
{
    /// <summary>
    /// Chứng từ "nhập có giá" tham gia công thức: nhập mua NCC cộng vào, trả NCC trừ ra theo giá đích danh. Chuyển kho trong
    /// công ty không tham gia (chỉ đổi vị trí). Nhập mua nội bộ, nhập điều chỉnh thêm khi có các loại phiếu đó (giai đoạn 4).
    /// </summary>
    public static readonly IReadOnlySet<string> PricedDocTypes = new HashSet<string> { "GRN", "RTV" };

    /// <summary>
    /// Giá vốn đơn vị kỳ = (giá trị tồn đầu + giá trị nhập có giá) ÷ (SL tồn đầu + SL nhập có giá), làm tròn 6 số lẻ (0,5 lên).
    /// Ví dụ (design.md mục 7.3): (1.000.000 + 650.000 − 130.000) ÷ (100 + 50 − 10) = 1.520.000 ÷ 140 = 10.857,142857.
    /// </summary>
    /// <param name="openQty">SL tồn đầu kỳ (cả công ty).</param>
    /// <param name="openAmount">Giá trị tồn đầu kỳ.</param>
    /// <param name="pricedInQty">SL nhập có giá (trả NCC là số âm).</param>
    /// <param name="pricedInAmount">Giá trị nhập có giá (trả NCC là số âm).</param>
    /// <param name="fallback">Giá dùng khi mẫu số ≤ 0 (không có tồn, không nhập): giá vốn kỳ trước, hoặc 0.</param>
    /// <returns>Giá vốn đơn vị.</returns>
    public static decimal UnitCost(decimal openQty, decimal openAmount, decimal pricedInQty, decimal pricedInAmount, decimal fallback)
    {
        var qty = openQty + pricedInQty;
        if (qty <= 0) return fallback;
        var cost = (openAmount + pricedInAmount) / qty;
        return cost < 0 ? fallback : Math.Round(cost, 6, MidpointRounding.AwayFromZero);
    }
}
