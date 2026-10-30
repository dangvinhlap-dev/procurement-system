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
        return new StockLedgerEntry
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
            Direction = 1,
            BaseQty = line.BaseQty,
            UnitCost = decimal.Round(line.Amount / line.BaseQty, 6, MidpointRounding.AwayFromZero),
            Amount = line.Amount,
            IsCostFinal = true,
        };
    }
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
