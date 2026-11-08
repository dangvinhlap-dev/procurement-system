using FluentValidation;

namespace Proc.Application.Purchasing;

/// <summary>Một dòng danh sách phiếu đặt hàng gộp (design.md mục 18.25, lát G7).</summary>
public sealed record PooledOrderListItemDto(
    Guid Id, string No, DateOnly OrderDate, string MonthlyApprovalNo, string SupplierCode, string SupplierName, int LineCount,
    decimal TotalAmount, int OrderCount, string Status, string? CreatedByName);

/// <summary>Công ty nhận hàng của phiếu đặt gộp, kèm PO đã sinh.</summary>
public sealed record PooledOrderCompanyDto(
    Guid CompanyId, string CompanyCode, string CompanyName, Guid WarehouseId, string? WarehouseCode, Guid? DepartmentId, string? DepartmentName,
    decimal Amount, Guid? PurchaseOrderId, string? PurchaseOrderNo, string? PurchaseOrderStatus);

/// <summary>
/// SL phân bổ của một dòng cho một công ty. PlanQty: kế hoạch nhu cầu tháng đã chốt của công ty (ĐVT dòng); ExpectedQty: đề xuất theo tỷ lệ
/// kế hoạch với tổng SL hiện tại (null khi dòng không có kế hoạch); IsOutOfMargin: lệch đề xuất quá dung sai.
/// </summary>
public sealed record PooledAllocationDto(Guid CompanyId, decimal Qty, decimal PlanQty, decimal? ExpectedQty, bool IsOutOfMargin);

/// <summary>Dòng phiếu đặt gộp. Tolerance: dung sai mỗi công ty = max(tổng × biên độ %, 1 ĐVT).</summary>
public sealed record PooledOrderLineDto(
    Guid Id, int LineNo, Guid MonthlyApprovalLineId, Guid ItemId, string ItemCode, string ItemName, Guid UomId, string UomCode, decimal UnitPrice,
    decimal TotalQty, decimal Amount, decimal Tolerance, bool HasPlan, string? AllocationReason, string? Note,
    IReadOnlyList<PooledAllocationDto> Allocations);

/// <summary>Thao tác được phép trên phiếu đặt gộp; ReadOnlyReason giải thích khi không sửa được.</summary>
public sealed record PooledOrderActions(bool CanEdit, bool CanCreateOrders, bool CanCancel, string? ReadOnlyReason);

/// <summary>
/// Chi tiết phiếu đặt hàng gộp. MarginPercent: % biên độ – tham số hiện hành của tổng công ty khi còn nháp, mức đã áp khi đã tạo PO.
/// </summary>
public sealed record PooledOrderDto(
    Guid Id, string No, DateOnly OrderDate, Guid MonthlyApprovalId, string MonthlyApprovalNo, DateOnly Period, string CategoryCode,
    string CategoryName, Guid SupplierId, string SupplierCode, string SupplierName, DateOnly? ExpectedDate, string? Note, string Status,
    decimal MarginPercent, DateTimeOffset? OrderedAt, string? OrderedByName, DateOnly? CancelledOn, string? CancelReason, DateTimeOffset CreatedAt,
    string? CreatedByName, decimal TotalAmount, IReadOnlyList<PooledOrderCompanyDto> Companies, IReadOnlyList<PooledOrderLineDto> Lines,
    PooledOrderActions Actions, uint Version = 0);

/// <summary>NCC đặt được theo phiếu GTH (NCC chính hoặc dự phòng của ít nhất một dòng).</summary>
public sealed record PooledSupplierOptionDto(Guid SupplierId, string Code, string Name);

/// <summary>Công ty áp dụng của phiếu GTH.</summary>
public sealed record PooledCompanyOptionDto(Guid CompanyId, string Code, string Name);

/// <summary>Phiếu GTH đã duyệt dùng được để đặt gộp trong tháng.</summary>
public sealed record PooledApprovalOptionDto(
    Guid Id, string No, DateOnly Period, string CategoryCode, string CategoryName, IReadOnlyList<PooledSupplierOptionDto> Suppliers,
    IReadOnlyList<PooledCompanyOptionDto> Companies);

/// <summary>Kho hoặc bộ phận của một công ty nhận hàng.</summary>
public sealed record PooledPlaceOptionDto(Guid Id, string Code, string Name, bool IsMain);

/// <summary>Công ty nhận hàng kèm kho (kho Main đứng đầu) và bộ phận đang dùng.</summary>
public sealed record PooledCompanyContextDto(
    Guid CompanyId, string Code, string Name, IReadOnlyList<PooledPlaceOptionDto> Warehouses, IReadOnlyList<PooledPlaceOptionDto> Departments);

/// <summary>Kế hoạch nhu cầu tháng của một công ty cho một mặt hàng (ĐVT dòng GTH).</summary>
public sealed record PooledPlanDto(Guid CompanyId, decimal PlanQty);

/// <summary>Dòng phiếu GTH đặt được với NCC đã chọn: giá của NCC đó (chính hoặc dự phòng), hạn mức, kế hoạch các công ty.</summary>
public sealed record PooledSourceLineDto(
    Guid MonthlyApprovalLineId, Guid ItemId, string ItemCode, string ItemName, Guid UomId, string UomCode, decimal UnitPrice, bool IsBackup,
    decimal? QuantityLimit, IReadOnlyList<PooledPlanDto> Plans);

/// <summary>Dữ liệu cho form đặt gộp theo phiếu GTH + NCC.</summary>
public sealed record PooledOrderContextDto(
    decimal MarginPercent, IReadOnlyList<PooledCompanyContextDto> Companies, IReadOnlyList<PooledSourceLineDto> Lines);

/// <summary>Công ty nhận hàng gửi lên.</summary>
/// <param name="CompanyId">Công ty áp dụng của phiếu GTH.</param>
/// <param name="WarehouseId">Kho nhận của công ty.</param>
/// <param name="DepartmentId">Bộ phận đặt hàng (không bắt buộc).</param>
public sealed record SavePooledOrderCompanyRequest(Guid CompanyId, Guid WarehouseId, Guid? DepartmentId);

/// <summary>SL phân bổ gửi lên.</summary>
/// <param name="CompanyId">Công ty.</param>
/// <param name="Qty">Số lượng theo ĐVT dòng.</param>
public sealed record PooledAllocationRequest(Guid CompanyId, decimal Qty);

/// <summary>Dòng đặt gộp gửi lên.</summary>
/// <param name="Id">Id dòng khi sửa; null = dòng mới.</param>
/// <param name="MonthlyApprovalLineId">Dòng phiếu GTH.</param>
/// <param name="Allocations">SL phân bổ theo công ty.</param>
/// <param name="AllocationReason">Lý do phân bổ (bắt buộc khi không có kế hoạch nhu cầu – kiểm tra lúc tạo PO).</param>
/// <param name="Note">Ghi chú dòng – chép sang dòng PO.</param>
public sealed record SavePooledOrderLineRequest(
    Guid? Id, Guid MonthlyApprovalLineId, IReadOnlyList<PooledAllocationRequest> Allocations, string? AllocationReason, string? Note);

/// <summary>Lưu phiếu đặt hàng gộp nháp.</summary>
/// <param name="OrderDate">Ngày đặt – thuộc tháng của phiếu GTH.</param>
/// <param name="MonthlyApprovalId">Phiếu GTH đã duyệt.</param>
/// <param name="SupplierId">NCC chính hoặc dự phòng trên phiếu GTH.</param>
/// <param name="ExpectedDate">Ngày giao dự kiến.</param>
/// <param name="Note">Ghi chú – chép sang PO.</param>
/// <param name="Companies">Công ty nhận hàng.</param>
/// <param name="Lines">Dòng hàng.</param>
public sealed record SavePooledOrderRequest(
    DateOnly OrderDate, Guid MonthlyApprovalId, Guid SupplierId, DateOnly? ExpectedDate, string? Note,
    IReadOnlyList<SavePooledOrderCompanyRequest> Companies, IReadOnlyList<SavePooledOrderLineRequest> Lines);

/// <summary>Hủy phiếu đặt gộp nháp.</summary>
/// <param name="Reason">Lý do, bắt buộc.</param>
public sealed record CancelPooledOrderRequest(string? Reason);

/// <summary>
/// Phiếu đặt hàng gộp – Phòng cung ứng chung đặt cho các công ty theo NCC và giá Ban đã duyệt (design.md mục 18.25, lát G7). Phiếu thuộc tổng
/// công ty: lập, sửa, hủy, tạo PO khi đang làm việc ở tổng công ty.
/// </summary>
public interface IPooledOrderService
{
    /// <summary>Danh sách phiếu của công ty đang làm việc (mới nhất trước, tối đa 2000).</summary>
    Task<IReadOnlyList<PooledOrderListItemDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Chi tiết phiếu.</summary>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc ngoài phạm vi.</exception>
    Task<PooledOrderDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Phiếu GTH đã duyệt của tháng chứa ngày đặt (kèm NCC, công ty áp dụng) để chọn khi lập phiếu.</summary>
    /// <param name="date">Ngày đặt.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<IReadOnlyList<PooledApprovalOptionDto>> ApprovalsAsync(DateOnly date, CancellationToken cancellationToken);

    /// <summary>Dòng GTH đặt được với NCC, kế hoạch tháng các công ty, kho và bộ phận của các công ty áp dụng, % biên độ.</summary>
    /// <param name="approvalId">Phiếu GTH.</param>
    /// <param name="supplierId">NCC.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<PooledOrderContextDto> ContextAsync(Guid approvalId, Guid supplierId, CancellationToken cancellationToken);

    /// <summary>Lập phiếu nháp.</summary>
    /// <exception cref="Common.ForbiddenAccessException">Khi không làm việc ở tổng công ty.</exception>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi phiếu GTH, NCC, công ty, kho, dòng không hợp lệ.</exception>
    Task<PooledOrderDto> CreateAsync(SavePooledOrderRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa phiếu nháp.</summary>
    Task<PooledOrderDto> UpdateAsync(Guid id, SavePooledOrderRequest request, CancellationToken cancellationToken);

    /// <summary>Hủy phiếu nháp kèm lý do.</summary>
    Task<PooledOrderDto> CancelAsync(Guid id, CancelPooledOrderRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Tạo PO: kiểm tra phân bổ trong biên độ, rồi sinh mỗi công ty có SL một PO nháp tại chính công ty đó (cần quyền tạo PO ở công ty đó),
    /// giá theo phiếu GTH – tất cả trong một giao dịch: một công ty lỗi thì không PO nào được tạo.
    /// </summary>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi vượt biên độ, phiếu GTH không còn hiệu lực hoặc PO của một công ty không lập được.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi người dùng không có vai trò/quyền tạo PO tại một công ty.</exception>
    Task<PooledOrderDto> CreateOrdersAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra lưu phiếu đặt gộp.</summary>
public sealed class SavePooledOrderRequestValidator : AbstractValidator<SavePooledOrderRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public SavePooledOrderRequestValidator()
    {
        RuleFor(x => x.MonthlyApprovalId).NotEmpty().WithMessage("Hãy chọn phiếu duyệt NCC và giá tháng.");
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("Hãy chọn nhà cung cấp.");
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Companies).NotEmpty().WithMessage("Phiếu phải có ít nhất một công ty nhận hàng.")
            .Must(c => c.Count <= 50).WithMessage("Tối đa 50 công ty.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Phiếu phải có ít nhất một dòng hàng.")
            .Must(l => l.Count <= 500).WithMessage("Mỗi phiếu tối đa 500 dòng.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.MonthlyApprovalLineId).NotEmpty().WithMessage("Dòng hàng chưa chọn mặt hàng.");
            l.RuleFor(x => x.AllocationReason).MaximumLength(500);
            l.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}
