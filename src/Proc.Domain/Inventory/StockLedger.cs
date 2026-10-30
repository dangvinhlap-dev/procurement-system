using Proc.Domain.Common;

namespace Proc.Domain.Inventory;

/// <summary>
/// Một dòng sổ kho (design.md mục 12): mỗi dòng chứng từ khi ghi sổ sinh một (nhập hoặc xuất) hoặc hai dòng (chuyển kho).
/// Tồn kho, nhập – xuất – tồn, thẻ kho, giá vốn đều tính từ sổ này; chứng từ chỉ là nguồn.
/// Không gắn [Auditable]: bản thân sổ kho đã là lịch sử; việc ghi sổ/bỏ ghi sổ được ghi nhật ký qua trạng thái chứng từ.
/// </summary>
public sealed class StockLedgerEntry : Entity, ICompanyOwned
{
    private StockLedgerEntry()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Kho.</summary>
    public Guid WarehouseId { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>Số lô (null = không theo lô).</summary>
    public string? LotNo { get; private set; }

    /// <summary>Kỳ kho.</summary>
    public Guid PeriodId { get; private set; }

    /// <summary>Ngày ghi sổ = ngày chứng từ.</summary>
    public DateOnly PostingDate { get; private set; }

    /// <summary>Chứng từ.</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Dòng chứng từ.</summary>
    public Guid DocumentLineId { get; private set; }

    /// <summary>Loại chứng từ (GRN, RTV, TRF…).</summary>
    public string DocType { get; private set; } = string.Empty;

    /// <summary>+1 nhập, −1 xuất.</summary>
    public int Direction { get; private set; }

    /// <summary>Số lượng theo ĐVT cơ sở, luôn dương (chiều ở <see cref="Direction"/>) – numeric(18,4).</summary>
    public decimal BaseQty { get; private set; }

    /// <summary>Đơn giá vốn theo ĐVT cơ sở – numeric(19,6).</summary>
    public decimal UnitCost { get; private set; }

    /// <summary>Giá trị, luôn dương – numeric(19,2).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Giá đã là giá cuối cùng (nhập mua: giá mua; xuất: chốt khi đóng kỳ – giai đoạn 3).</summary>
    public bool IsCostFinal { get; private set; }

    /// <summary>Số lượng có dấu: + nhập, − xuất.</summary>
    public decimal SignedQty => Direction * BaseQty;

    /// <summary>
    /// Gán giá vốn chính thức khi đóng kỳ (design.md mục 7.2 bước 4): giá trị = SL × đơn giá, làm tròn 2 số lẻ (0,5 lên).
    /// Ví dụ 24 × 10.857,142857 = 260.571,43.
    /// </summary>
    /// <param name="unitCost">Giá vốn đơn vị kỳ (đã làm tròn 6 số lẻ).</param>
    public void ApplyPeriodCost(decimal unitCost)
    {
        UnitCost = unitCost;
        Amount = Math.Round(BaseQty * unitCost, 2, MidpointRounding.AwayFromZero);
        IsCostFinal = true;
    }

    /// <summary>Dồn chênh lệch làm tròn vào dòng này (design.md mục 7.3) – giá trị mới phải không âm.</summary>
    /// <param name="amount">Giá trị sau điều chỉnh.</param>
    public void AdjustAmount(decimal amount) => Amount = amount < 0 ? 0 : amount;

    /// <summary>Mở lại kỳ: giá vốn xuất về trạng thái tạm tính, tính lại khi đóng kỳ lần sau.</summary>
    public void ResetPeriodCost() => IsCostFinal = false;

    /// <summary>
    /// Dòng nhập kho từ một dòng phiếu nhập mua NCC: giá trị nhập = thành tiền trước VAT (design.md mục 6.1; chi phí mua phân bổ
    /// thêm ở giai đoạn 3). Đơn giá vốn theo ĐVT cơ sở = thành tiền ÷ SL cơ sở, ví dụ 960.000 ÷ 48 hộp = 20.000 đ/hộp.
    /// </summary>
    /// <param name="document">Phiếu nhập (có kho nhập).</param>
    /// <param name="line">Dòng phiếu.</param>
    /// <param name="periodId">Kỳ kho.</param>
    /// <returns>Dòng sổ kho nhập.</returns>
    /// <exception cref="BusinessRuleException">Khi phiếu không có kho nhập.</exception>
    public static StockLedgerEntry Receipt(InventoryDocument document, InventoryDocumentLine line, Guid periodId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(line);
        var warehouseId = document.ToWarehouseId ?? throw new BusinessRuleException($"Phiếu {document.No} chưa có kho nhập.");
        return Create(document, line, warehouseId, 1, periodId, line.Amount, isCostFinal: true);
    }

    /// <summary>
    /// Dòng xuất kho trả NCC theo giá nhập gốc đích danh (design.md mục 6.1): giá trị = thành tiền dòng trả (SL trả × đơn giá
    /// của dòng nhập gốc). Giá trả NCC không đổi khi đóng kỳ nên là giá cuối cùng.
    /// </summary>
    /// <param name="document">Phiếu trả (có kho xuất).</param>
    /// <param name="line">Dòng phiếu.</param>
    /// <param name="periodId">Kỳ kho.</param>
    /// <returns>Dòng sổ kho xuất.</returns>
    /// <exception cref="BusinessRuleException">Khi phiếu không có kho xuất.</exception>
    public static StockLedgerEntry Return(InventoryDocument document, InventoryDocumentLine line, Guid periodId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(line);
        var warehouseId = document.FromWarehouseId ?? throw new BusinessRuleException($"Phiếu {document.No} chưa có kho xuất.");
        return Create(document, line, warehouseId, -1, periodId, line.Amount, isCostFinal: true);
    }

    /// <summary>
    /// Cặp dòng chuyển kho: xuất kho đi, nhập kho đến cùng một giá vốn tạm tính (design.md mục 6.1, 7.5). Giá trị =
    /// SL cơ sở × đơn giá vốn tạm tính, làm tròn 2 chữ số; chưa phải giá cuối cùng – đóng kỳ tính lại theo giá vốn kỳ.
    /// Ví dụ 48 hộp × 20.416,666667 = 980.000,00.
    /// </summary>
    /// <param name="document">Phiếu chuyển (có kho xuất, kho nhập).</param>
    /// <param name="line">Dòng phiếu.</param>
    /// <param name="periodId">Kỳ kho.</param>
    /// <param name="unitCost">Đơn giá vốn tạm tính theo ĐVT cơ sở.</param>
    /// <returns>Dòng xuất kho đi và dòng nhập kho đến.</returns>
    /// <exception cref="BusinessRuleException">Khi phiếu thiếu kho.</exception>
    public static (StockLedgerEntry Out, StockLedgerEntry In) Transfer(
        InventoryDocument document, InventoryDocumentLine line, Guid periodId, decimal unitCost)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(line);
        var from = document.FromWarehouseId ?? throw new BusinessRuleException($"Phiếu {document.No} chưa có kho xuất.");
        var to = document.ToWarehouseId ?? throw new BusinessRuleException($"Phiếu {document.No} chưa có kho nhập.");
        var cost = decimal.Round(unitCost, 6, MidpointRounding.AwayFromZero);
        var amount = decimal.Round(line.BaseQty * cost, 2, MidpointRounding.AwayFromZero);
        return (Create(document, line, from, -1, periodId, amount, false, cost), Create(document, line, to, 1, periodId, amount, false, cost));
    }

    /// <summary>
    /// Dòng xuất bán nội bộ tại kho tổng công ty bán (design.md mục 6.1): theo giá vốn tạm tính của công ty bán, chưa chốt – đóng kỳ
    /// gán giá vốn kỳ. Doanh thu theo giá bán nội bộ nằm trên phiếu, không ở sổ kho.
    /// </summary>
    /// <param name="document">Phiếu xuất bán (có kho xuất).</param>
    /// <param name="line">Dòng.</param>
    /// <param name="periodId">Kỳ tháng.</param>
    /// <param name="unitCost">Giá vốn tạm tính theo ĐVT cơ sở.</param>
    /// <returns>Dòng sổ −1.</returns>
    public static StockLedgerEntry Sale(InventoryDocument document, InventoryDocumentLine line, Guid periodId, decimal unitCost)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(line);
        var warehouseId = document.FromWarehouseId ?? throw new BusinessRuleException($"Phiếu {document.No} chưa có kho xuất.");
        var amount = Math.Round(line.BaseQty * unitCost, 2, MidpointRounding.AwayFromZero);
        return Create(document, line, warehouseId, -1, periodId, amount, isCostFinal: false, unitCost);
    }

    private static StockLedgerEntry Create(
        InventoryDocument document, InventoryDocumentLine line, Guid warehouseId, int direction, Guid periodId, decimal amount,
        bool isCostFinal, decimal? unitCost = null) =>
        new()
        {
            CompanyId = document.CompanyId,
            WarehouseId = warehouseId,
            ItemId = line.ItemId,
            LotNo = line.LotNo,
            PeriodId = periodId,
            PostingDate = document.Date,
            DocumentId = document.Id,
            DocumentLineId = line.Id,
            DocType = document.DocType,
            Direction = direction,
            BaseQty = line.BaseQty,
            UnitCost = unitCost ?? decimal.Round(amount / line.BaseQty, 6, MidpointRounding.AwayFromZero),
            Amount = amount,
            IsCostFinal = isCostFinal,
        };
}

/// <summary>
/// Tồn tức thời theo công ty × kho × mặt hàng × lô (design.md mục 12.1), cập nhật cùng giao dịch ghi sổ.
/// Tầng Infrastructure cộng/trừ bằng một câu SQL nguyên tử (khóa dòng tới hết giao dịch) nên entity này chỉ để đọc.
/// </summary>
public sealed class StockOnHand : ICompanyOwned
{
    private StockOnHand()
    {
    }

    /// <summary>Khóa chính (số lô có thể trống nên không làm khóa được; tính duy nhất do chỉ mục công ty × kho × hàng × lô).</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Kho.</summary>
    public Guid WarehouseId { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>Số lô (null = không theo lô).</summary>
    public string? LotNo { get; private set; }

    /// <summary>Số lượng tồn theo ĐVT cơ sở – numeric(18,4).</summary>
    public decimal Qty { get; private set; }

    /// <summary>Số lượng đã giữ chỗ cho phiếu xuất chưa ghi sổ (dùng ở giai đoạn sau).</summary>
    public decimal ReservedQty { get; private set; }
}
