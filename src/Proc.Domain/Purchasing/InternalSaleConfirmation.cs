using Proc.Domain.Common;

namespace Proc.Domain.Purchasing;

/// <summary>Quyết định của công ty bán với một đơn mua nội bộ (lát X7).</summary>
public enum InternalSaleDecision
{
    /// <summary>Công ty bán nhận đơn, sẽ giao theo PO.</summary>
    Confirmed = 1,

    /// <summary>Công ty bán từ chối (bắt buộc lý do) – công ty mua hủy PO hoặc đóng phần còn lại.</summary>
    Rejected = 2,
}

/// <summary>
/// Công ty bán xác nhận hoặc từ chối đơn mua nội bộ ở màn hình "Đơn bán nội bộ" (lát X7, design.md mục 19.3, 19.4 – thay phiếu xuất bán nội
/// bộ ICS). Bản ghi thuộc <b>công ty bán</b> (<see cref="CompanyId"/>), không ghi lên PO của công ty mua: PO đã duyệt là đã khóa (design.md
/// mục 20 – R1) và mỗi công ty chỉ ghi dữ liệu của mình. Mỗi PO một quyết định, không đổi sau khi ghi; người ghi và giờ ghi là
/// <see cref="AuditableEntity.CreatedBy"/>, <see cref="AuditableEntity.CreatedAt"/>.
/// </summary>
[Auditable("Xác nhận đơn bán nội bộ", LabelProperty = nameof(OrderNo))]
public sealed class InternalSaleConfirmation : AuditableEntity, ICompanyOwned
{
    /// <summary>Độ dài ghi chú / lý do tối đa.</summary>
    public const int NoteMaxLength = 500;

    private InternalSaleConfirmation()
    {
    }

    /// <summary>Công ty bán (công ty mà NCC nội bộ đại diện).</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Đơn mua hàng của công ty mua.</summary>
    public Guid PurchaseOrderId { get; private set; }

    /// <summary>Số PO lúc ghi – để nhật ký đọc được mà không phải tra PO của công ty khác.</summary>
    public string OrderNo { get; private set; } = string.Empty;

    /// <summary>Công ty mua (công ty của PO).</summary>
    public Guid BuyerCompanyId { get; private set; }

    /// <summary>Xác nhận hay từ chối.</summary>
    public InternalSaleDecision Decision { get; private set; }

    /// <summary>Ghi chú khi xác nhận (tùy chọn) hoặc lý do từ chối (bắt buộc).</summary>
    public string? Note { get; private set; }

    /// <summary>Ghi quyết định của công ty bán.</summary>
    /// <param name="sellerCompanyId">Công ty bán (công ty đang làm việc).</param>
    /// <param name="purchaseOrderId">PO của công ty mua.</param>
    /// <param name="orderNo">Số PO.</param>
    /// <param name="buyerCompanyId">Công ty mua.</param>
    /// <param name="decision">Xác nhận hoặc từ chối.</param>
    /// <param name="note">Ghi chú; bắt buộc khi từ chối.</param>
    /// <returns>Bản ghi quyết định.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu dữ liệu, hai công ty trùng nhau, từ chối không có lý do hoặc ghi chú quá dài.</exception>
    public static InternalSaleConfirmation Create(
        Guid sellerCompanyId, Guid purchaseOrderId, string orderNo, Guid buyerCompanyId, InternalSaleDecision decision, string? note)
    {
        if (sellerCompanyId == Guid.Empty || purchaseOrderId == Guid.Empty || buyerCompanyId == Guid.Empty || string.IsNullOrWhiteSpace(orderNo))
            throw new BusinessRuleException("Thiếu công ty bán, công ty mua hoặc đơn mua hàng.");
        if (sellerCompanyId == buyerCompanyId) throw new BusinessRuleException("Công ty bán phải khác công ty mua.");
        if (!Enum.IsDefined(decision)) throw new BusinessRuleException("Quyết định phải là Xác nhận hoặc Từ chối.");
        var clean = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (decision == InternalSaleDecision.Rejected && clean is null)
            throw new BusinessRuleException("Phải nhập lý do từ chối đơn để công ty mua biết xử lý.");
        if (clean is { Length: > NoteMaxLength }) throw new BusinessRuleException($"Ghi chú / lý do tối đa {NoteMaxLength} ký tự.");
        return new InternalSaleConfirmation
        {
            CompanyId = sellerCompanyId, PurchaseOrderId = purchaseOrderId, OrderNo = orderNo.Trim(), BuyerCompanyId = buyerCompanyId,
            Decision = decision, Note = clean,
        };
    }
}
