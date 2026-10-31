using FluentValidation;

namespace Proc.Application.Purchasing;

/// <summary>Một cấp trong quy trình duyệt của công ty đang làm việc.</summary>
/// <param name="Id">Id.</param>
/// <param name="DocType">Loại chứng từ (PR, PO).</param>
/// <param name="Level">Cấp.</param>
/// <param name="MinAmount">Ngưỡng giá trị (phiếu ≥ ngưỡng mới qua cấp này).</param>
/// <param name="RoleId">Vai trò được duyệt.</param>
/// <param name="RoleCode">Mã vai trò.</param>
/// <param name="RoleName">Tên vai trò.</param>
public sealed record ApprovalRuleDto(Guid Id, string DocType, int Level, decimal MinAmount, Guid RoleId, string RoleCode, string RoleName);

/// <summary>Thêm/sửa một cấp duyệt.</summary>
public sealed record SaveApprovalRuleRequest(string DocType, int Level, decimal MinAmount, Guid RoleId);

/// <summary>Một bước trong lịch sử duyệt của phiếu.</summary>
/// <param name="Round">Lượt gửi.</param>
/// <param name="Level">Cấp.</param>
/// <param name="RoleName">Vai trò được duyệt (null = ai có quyền duyệt).</param>
/// <param name="Status">Pending, Approved, Rejected, Skipped.</param>
/// <param name="ActedByName">Người duyệt/từ chối.</param>
/// <param name="ActedAt">Thời điểm.</param>
/// <param name="Comment">Ý kiến.</param>
public sealed record ApprovalStepDto(int Round, int Level, string? RoleName, string Status, string? ActedByName, DateTimeOffset? ActedAt, string? Comment);

/// <summary>Một phiếu đang chờ người dùng hiện tại duyệt (Việc cần làm).</summary>
/// <param name="DocType">PR hoặc PO.</param>
/// <param name="DocumentId">Id phiếu.</param>
/// <param name="No">Số phiếu.</param>
/// <param name="Date">Ngày phiếu.</param>
/// <param name="Description">Bộ phận (PR) hoặc NCC (PO).</param>
/// <param name="Amount">Giá trị xét duyệt.</param>
/// <param name="Level">Cấp đang chờ.</param>
/// <param name="CreatedByName">Người lập.</param>
public sealed record PendingApprovalDto(string DocType, Guid DocumentId, string No, DateOnly Date, string? Description, decimal Amount, int Level, string? CreatedByName);

/// <summary>Duyệt/từ chối kèm ý kiến (bắt buộc khi từ chối).</summary>
public sealed record ApprovalActionRequest(string? Comment);

/// <summary>Cấu hình quy trình duyệt của công ty đang làm việc (design.md mục 5 – ApprovalRule, mục 6.2).</summary>
public interface IApprovalRuleAdministration
{
    /// <summary>Mọi cấp duyệt, theo loại chứng từ rồi cấp.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<ApprovalRuleDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Thêm cấp duyệt.</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Cấp vừa thêm.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi trùng cấp hoặc ngưỡng không tăng dần theo cấp.</exception>
    Task<ApprovalRuleDto> CreateAsync(SaveApprovalRuleRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa cấp duyệt (loại chứng từ giữ nguyên).</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Cấp sau khi sửa.</returns>
    Task<ApprovalRuleDto> UpdateAsync(Guid id, SaveApprovalRuleRequest request, CancellationToken cancellationToken);

    /// <summary>Xóa cấp duyệt (phiếu đang chờ duyệt giữ các bước đã sinh).</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Các phiếu PR, PO đang chờ người dùng hiện tại duyệt ở công ty đang làm việc.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách theo ngày.</returns>
    Task<IReadOnlyList<PendingApprovalDto>> PendingForMeAsync(CancellationToken cancellationToken);
}

/// <summary>Điều kiện tìm PR/PO.</summary>
/// <param name="From">Từ ngày (mặc định 90 ngày trước).</param>
/// <param name="To">Đến ngày (mặc định hôm nay).</param>
/// <param name="Status">Trạng thái (Draft, Submitted, Approved, Closed, Cancelled).</param>
public sealed record PurchaseDocumentQuery(DateOnly? From, DateOnly? To, string? Status);

/// <summary>Một dòng danh sách PR/PO.</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số phiếu.</param>
/// <param name="Date">Ngày.</param>
/// <param name="Status">Trạng thái.</param>
/// <param name="Description">Bộ phận (PR) hoặc mã – tên NCC (PO).</param>
/// <param name="Purpose">Mục đích (PR) hoặc kho nhận (PO).</param>
/// <param name="NeededDate">Ngày cần (PR) hoặc ngày giao dự kiến (PO).</param>
/// <param name="Amount">Tổng giá trị (PR: dự kiến; PO: tổng thanh toán).</param>
/// <param name="LineCount">Số dòng.</param>
/// <param name="CreatedByName">Người lập.</param>
/// <param name="Progress">PR: % SL đã đặt hàng; PO: % SL đã nhận (theo ĐVT cơ sở).</param>
/// <param name="Priority">Mức ưu tiên (PR).</param>
public sealed record PurchaseDocumentListItemDto(
    Guid Id, string No, DateOnly Date, string Status, string? Description, string? Purpose, DateOnly? NeededDate, decimal Amount,
    int LineCount, string? CreatedByName, decimal Progress, string? Priority);

/// <summary>Kết quả tìm PR/PO.</summary>
public sealed record PurchaseDocumentListResult(IReadOnlyList<PurchaseDocumentListItemDto> Items, bool Truncated, int Limit);

/// <summary>Quyền thao tác trên phiếu theo trạng thái và người xem.</summary>
/// <param name="CanEdit">Sửa, lưu nháp.</param>
/// <param name="CanSubmit">Gửi duyệt.</param>
/// <param name="CanApprove">Duyệt/từ chối (đúng cấp đang chờ, không phải người lập).</param>
/// <param name="CanCancel">Hủy.</param>
/// <param name="ReadOnlyReason">Lý do chỉ xem.</param>
public sealed record PurchaseDocumentActions(bool CanEdit, bool CanSubmit, bool CanApprove, bool CanCancel, string? ReadOnlyReason);

/// <summary>
/// Dòng PR. OrderedQty: SL đã lên PO (chưa hủy) theo ĐVT trên phiếu; OnHandQty: tồn hiện tại của mặt hàng ở công ty (ĐVT cơ sở,
/// trong phạm vi kho của người xem).
/// </summary>
public sealed record PurchaseRequestLineDto(
    Guid Id, int LineNo, Guid ItemId, string ItemCode, string ItemName, Guid UomId, string UomCode, decimal Qty, decimal UomFactor,
    decimal BaseQty, string BaseUomCode, decimal EstPrice, decimal Amount, Guid? SuggestedSupplierId, string? SuggestedSupplierCode,
    DateOnly? NeededDate, string? Note, decimal OrderedQty, decimal OnHandQty);

/// <summary>Chi tiết PR.</summary>
public sealed record PurchaseRequestDto(
    Guid Id, string No, DateOnly Date, string Status, string? Department, string? RequesterName, string? Purpose, DateOnly? NeededDate,
    Guid? WarehouseId, string Priority, string? Note, decimal TotalAmount, string? RejectReason, DateTimeOffset CreatedAt, string? CreatedByName,
    DateTimeOffset? ApprovedAt, IReadOnlyList<PurchaseRequestLineDto> Lines, IReadOnlyList<ApprovalStepDto> Approvals, PurchaseDocumentActions Actions);

/// <summary>Dòng PR gửi lên. Đơn giá tham khảo do server lấy (giá nhập gần nhất), không nhận từ client.</summary>
public sealed record SavePurchaseRequestLineRequest(Guid? Id, Guid ItemId, Guid UomId, decimal Qty, Guid? SuggestedSupplierId, DateOnly? NeededDate, string? Note);

/// <summary>Lưu PR nháp.</summary>
public sealed record SavePurchaseRequestRequest(
    DateOnly Date, string? Department, string? Purpose, DateOnly? NeededDate, Guid? WarehouseId, string Priority, string? Note,
    IReadOnlyList<SavePurchaseRequestLineRequest> Lines);

/// <summary>Dòng PR đã duyệt còn chưa đặt hàng (để lập PO). RemainingQty: SL còn chưa lên PO theo ĐVT của dòng PR.</summary>
public sealed record OpenRequestLineDto(
    Guid PrLineId, Guid RequestId, string RequestNo, DateOnly? NeededDate, string? Department, Guid ItemId, string ItemCode, string ItemName,
    Guid UomId, string UomCode, decimal RemainingQty, decimal EstPrice, string? VatCode, Guid? SuggestedSupplierId);

/// <summary>
/// Dòng PO. ReceivedQty: SL đã nhận (phiếu nhập đã ghi sổ) theo ĐVT trên PO; RemainingQty = SL đặt − đã nhận (không âm).
/// </summary>
public sealed record PurchaseOrderLineDto(
    Guid Id, int LineNo, Guid ItemId, string ItemCode, string ItemName, Guid UomId, string UomCode, decimal Qty, decimal UomFactor,
    decimal BaseQty, decimal UnitPrice, decimal DiscountPct, decimal GrossAmount, decimal DiscountAmount, decimal Amount, string? VatCode,
    decimal VatAmount, DateOnly? DeliveryDate, string? Note, Guid? SourcePrLineId, string? SourcePrNo, decimal ReceivedQty, decimal RemainingQty);

/// <summary>Chi tiết PO.</summary>
public sealed record PurchaseOrderDto(
    Guid Id, string No, DateOnly Date, string Status, Guid SupplierId, string SupplierCode, string SupplierName, Guid WarehouseId, string? WarehouseCode,
    DateOnly? ExpectedDate, string? PaymentTerms, string? DeliveryTerms, string? DeliveryAddress, string CurrencyCode, decimal ReceiveTolerancePct,
    string? Note, decimal GrossAmount, decimal TotalDiscount, decimal TotalAmount, decimal TotalVat, string? RejectReason, DateTimeOffset CreatedAt,
    string? CreatedByName, DateTimeOffset? ApprovedAt, IReadOnlyList<PurchaseOrderLineDto> Lines, IReadOnlyList<ApprovalStepDto> Approvals,
    PurchaseDocumentActions Actions);

/// <summary>Dòng PO gửi lên.</summary>
public sealed record SavePurchaseOrderLineRequest(
    Guid? Id, Guid ItemId, Guid UomId, decimal Qty, decimal UnitPrice, decimal DiscountPct, string? VatCode, DateOnly? DeliveryDate, string? Note,
    Guid? SourcePrLineId);

/// <summary>Lưu PO nháp.</summary>
public sealed record SavePurchaseOrderRequest(
    DateOnly Date, Guid SupplierId, Guid WarehouseId, DateOnly? ExpectedDate, string? PaymentTerms, string? DeliveryTerms, string? DeliveryAddress,
    decimal ReceiveTolerancePct, string? Note, IReadOnlyList<SavePurchaseOrderLineRequest> Lines);

/// <summary>Một dòng PO còn chờ giao (theo dõi giao hàng – design.md mục 3, nhóm Mua hàng).</summary>
public sealed record DeliveryTrackingDto(
    Guid OrderId, string OrderNo, DateOnly OrderDate, string SupplierCode, string SupplierName, Guid LineId, string ItemCode, string ItemName,
    string UomCode, decimal Qty, decimal ReceivedQty, decimal RemainingQty, DateOnly? DueDate, bool Overdue);

/// <summary>Đề nghị mua hàng (design.md mục 6.2, 10.2).</summary>
public interface IPurchaseRequestService
{
    /// <summary>Tìm PR theo khoảng ngày, trạng thái.</summary>
    Task<PurchaseDocumentListResult> SearchAsync(PurchaseDocumentQuery query, CancellationToken cancellationToken);

    /// <summary>Chi tiết PR.</summary>
    Task<PurchaseRequestDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập PR nháp (cấp số).</summary>
    Task<PurchaseRequestDto> CreateAsync(SavePurchaseRequestRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa PR nháp.</summary>
    Task<PurchaseRequestDto> UpdateAsync(Guid id, SavePurchaseRequestRequest request, CancellationToken cancellationToken);

    /// <summary>Gửi duyệt.</summary>
    Task<PurchaseRequestDto> SubmitAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Duyệt cấp đang chờ.</summary>
    Task<PurchaseRequestDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Từ chối (phiếu về nháp).</summary>
    Task<PurchaseRequestDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Hủy PR (chưa lên PO).</summary>
    Task<PurchaseRequestDto> CancelAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Dòng PR đã duyệt còn chưa đặt hàng.</summary>
    Task<IReadOnlyList<OpenRequestLineDto>> OpenLinesAsync(CancellationToken cancellationToken);
}

/// <summary>Đơn mua hàng (design.md mục 6.2, 10.3).</summary>
public interface IPurchaseOrderService
{
    /// <summary>Tìm PO theo khoảng ngày, trạng thái.</summary>
    Task<PurchaseDocumentListResult> SearchAsync(PurchaseDocumentQuery query, CancellationToken cancellationToken);

    /// <summary>Chi tiết PO.</summary>
    Task<PurchaseOrderDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập PO nháp (cấp số).</summary>
    Task<PurchaseOrderDto> CreateAsync(SavePurchaseOrderRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa PO nháp.</summary>
    Task<PurchaseOrderDto> UpdateAsync(Guid id, SavePurchaseOrderRequest request, CancellationToken cancellationToken);

    /// <summary>Gửi duyệt.</summary>
    Task<PurchaseOrderDto> SubmitAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Duyệt cấp đang chờ.</summary>
    Task<PurchaseOrderDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Từ chối (phiếu về nháp).</summary>
    Task<PurchaseOrderDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Hủy PO (chưa nhận hàng).</summary>
    Task<PurchaseOrderDto> CancelAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Dòng PO còn chờ giao của công ty đang làm việc.</summary>
    Task<IReadOnlyList<DeliveryTrackingDto>> TrackingAsync(CancellationToken cancellationToken);
}

/// <summary>Kiểm tra cấp duyệt.</summary>
public sealed class SaveApprovalRuleRequestValidator : AbstractValidator<SaveApprovalRuleRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public SaveApprovalRuleRequestValidator()
    {
        RuleFor(x => x.DocType).NotEmpty().WithMessage("Hãy chọn loại chứng từ.");
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("Hãy chọn vai trò duyệt.");
    }
}

/// <summary>Kiểm tra lưu PR.</summary>
public sealed class SavePurchaseRequestRequestValidator : AbstractValidator<SavePurchaseRequestRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public SavePurchaseRequestRequestValidator()
    {
        RuleFor(x => x.Department).MaximumLength(200);
        RuleFor(x => x.Purpose).MaximumLength(500);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Phiếu phải có ít nhất một dòng hàng.")
            .Must(l => l.Count <= 500).WithMessage("Mỗi phiếu tối đa 500 dòng.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty().WithMessage("Dòng hàng chưa chọn mặt hàng.");
            l.RuleFor(x => x.UomId).NotEmpty().WithMessage("Dòng hàng chưa chọn ĐVT.");
            l.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}

/// <summary>Kiểm tra lưu PO.</summary>
public sealed class SavePurchaseOrderRequestValidator : AbstractValidator<SavePurchaseOrderRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public SavePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("Hãy chọn nhà cung cấp.");
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("Hãy chọn kho nhận.");
        RuleFor(x => x.PaymentTerms).MaximumLength(200);
        RuleFor(x => x.DeliveryTerms).MaximumLength(200);
        RuleFor(x => x.DeliveryAddress).MaximumLength(500);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Phiếu phải có ít nhất một dòng hàng.")
            .Must(l => l.Count <= 500).WithMessage("Mỗi phiếu tối đa 500 dòng.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty().WithMessage("Dòng hàng chưa chọn mặt hàng.");
            l.RuleFor(x => x.UomId).NotEmpty().WithMessage("Dòng hàng chưa chọn ĐVT.");
            l.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}
