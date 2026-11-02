using FluentValidation;
using Proc.Application.Purchasing;
using Proc.Domain.MasterData;

namespace Proc.Application.MasterData;

/// <summary>Một dòng trên danh sách phê duyệt NCC độc quyền.</summary>
/// <param name="Id">Id phiếu.</param>
/// <param name="No">Số phiếu.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="ValidFrom">Hiệu lực từ ngày.</param>
/// <param name="ReviewDate">Hạn đánh giá lại.</param>
/// <param name="Status">Trạng thái (tên enum <see cref="SoleSourceStatus"/>).</param>
/// <param name="IsEffective">Có tác dụng hôm nay.</param>
/// <param name="IsOverdue">Đã duyệt nhưng qua hạn đánh giá lại.</param>
/// <param name="DaysToReview">Số ngày còn tới hạn đánh giá lại (âm = đã quá hạn) – chỉ khi đã duyệt.</param>
/// <param name="CreatedByName">Người lập.</param>
public sealed record SoleSourceListItemDto(
    Guid Id, string No, string SupplierCode, string SupplierName, string ItemCode, string ItemName, DateOnly ValidFrom, DateOnly ReviewDate,
    string Status, bool IsEffective, bool IsOverdue, int? DaysToReview, string? CreatedByName);

/// <summary>Nút được bấm trên phiếu NCC độc quyền.</summary>
/// <param name="CanEdit">Sửa, gửi duyệt (nháp, có quyền quản lý).</param>
/// <param name="CanApprove">Duyệt/từ chối (đến lượt mình, không phải người lập, không vướng kê khai xung đột lợi ích).</param>
/// <param name="CanRecall">Rút về nháp khi đang chờ duyệt.</param>
/// <param name="CanEnd">Kết thúc sớm (đã duyệt, chưa quá hạn).</param>
/// <param name="CanRenew">Lập phiếu đánh giá lại (đã duyệt, chưa có phiếu kế tiếp).</param>
public sealed record SoleSourceActions(bool CanEdit, bool CanApprove, bool CanRecall, bool CanEnd, bool CanRenew);

/// <summary>Chi tiết phiếu NCC độc quyền.</summary>
/// <param name="Id">Id phiếu.</param>
/// <param name="No">Số phiếu.</param>
/// <param name="CompanyCode">Mã công ty lập.</param>
/// <param name="SupplierId">NCC.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="Reason">Giải trình.</param>
/// <param name="ValidFrom">Hiệu lực từ ngày.</param>
/// <param name="ReviewDate">Hạn đánh giá lại.</param>
/// <param name="Status">Trạng thái.</param>
/// <param name="IsEffective">Có tác dụng hôm nay.</param>
/// <param name="IsOverdue">Đã duyệt nhưng qua hạn đánh giá lại.</param>
/// <param name="RejectReason">Lý do từ chối gần nhất.</param>
/// <param name="ApprovedAt">Thời điểm duyệt đủ.</param>
/// <param name="EndedOn">Ngày kết thúc sớm.</param>
/// <param name="EndReason">Lý do kết thúc sớm.</param>
/// <param name="RenewedFromId">Phiếu được đánh giá lại.</param>
/// <param name="RenewedFromNo">Số phiếu được đánh giá lại.</param>
/// <param name="RenewedById">Phiếu đánh giá lại phiếu này (nếu đã lập).</param>
/// <param name="RenewedByNo">Số phiếu đánh giá lại.</param>
/// <param name="CreatedAt">Thời điểm lập.</param>
/// <param name="CreatedByName">Người lập.</param>
/// <param name="Approvals">Lịch sử duyệt.</param>
/// <param name="Actions">Nút được bấm.</param>
public sealed record SoleSourceDto(
    Guid Id, string No, string CompanyCode, Guid SupplierId, string SupplierCode, string SupplierName, Guid ItemId, string ItemCode, string ItemName,
    string Reason, DateOnly ValidFrom, DateOnly ReviewDate, string Status, bool IsEffective, bool IsOverdue, string? RejectReason,
    DateTimeOffset? ApprovedAt, DateOnly? EndedOn, string? EndReason, Guid? RenewedFromId, string? RenewedFromNo, Guid? RenewedById,
    string? RenewedByNo, DateTimeOffset CreatedAt, string? CreatedByName, IReadOnlyList<ApprovalStepDto> Approvals, SoleSourceActions Actions);

/// <summary>Lập/sửa phiếu NCC độc quyền.</summary>
/// <param name="SupplierId">NCC.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="Reason">Giải trình.</param>
/// <param name="ValidFrom">Hiệu lực từ ngày.</param>
/// <param name="ReviewDate">Hạn đánh giá lại (sau ngày hiệu lực, tối đa 12 tháng).</param>
public sealed record SaveSoleSourceRequest(Guid SupplierId, Guid ItemId, string Reason, DateOnly ValidFrom, DateOnly ReviewDate);

/// <summary>Kết thúc sớm phiếu NCC độc quyền.</summary>
/// <param name="Reason">Lý do, bắt buộc.</param>
public sealed record EndSoleSourceRequest(string Reason);

/// <summary>
/// Phê duyệt NCC độc quyền (design.md mục 18.9 – đề xuất Ban CUVT N6): công ty đang làm việc giải trình một mặt hàng chỉ mua được của một
/// NCC, gửi duyệt theo quy trình loại NDQ, đánh giá lại định kỳ. Phiếu còn tác dụng thay cho giải trình khi gửi RFQ mời ít NCC.
/// </summary>
public interface ISoleSourceService
{
    /// <summary>Danh sách phiếu của công ty đang làm việc, mới nhất trước.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<SoleSourceListItemDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Chi tiết phiếu.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc ngoài phạm vi (công ty, nhóm hàng).</exception>
    Task<SoleSourceDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập phiếu nháp.</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu vừa lập.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">
    /// Khi dữ liệu sai, NCC ở danh sách đen, hoặc đã có phiếu chưa kết thúc cùng NCC × mặt hàng trùng thời gian.
    /// </exception>
    Task<SoleSourceDto> CreateAsync(SaveSoleSourceRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa phiếu nháp.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="request">Nội dung mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi sửa.</returns>
    Task<SoleSourceDto> UpdateAsync(Guid id, SaveSoleSourceRequest request, CancellationToken cancellationToken);

    /// <summary>Gửi duyệt.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi gửi.</returns>
    Task<SoleSourceDto> SubmitAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Duyệt cấp đang đến lượt.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="request">Ý kiến.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi duyệt.</returns>
    Task<SoleSourceDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Từ chối (bắt buộc lý do) – phiếu về nháp.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi từ chối.</returns>
    Task<SoleSourceDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Rút về nháp khi đang chờ duyệt.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi rút.</returns>
    Task<SoleSourceDto> RecallAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Kết thúc sớm phiếu đã duyệt.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi kết thúc.</returns>
    Task<SoleSourceDto> EndAsync(Guid id, EndSoleSourceRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Lập phiếu đánh giá lại (nháp) từ phiếu đã duyệt: chép NCC, mặt hàng, giải trình; hiệu lực từ ngày sau hạn đánh giá lại cũ (hoặc hôm nay
    /// nếu đã quá hạn), hạn mới +12 tháng.
    /// </summary>
    /// <param name="id">Id phiếu đã duyệt.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu nháp mới.</returns>
    Task<SoleSourceDto> RenewAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra lập/sửa phiếu NCC độc quyền.</summary>
public sealed class SaveSoleSourceRequestValidator : AbstractValidator<SaveSoleSourceRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public SaveSoleSourceRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("Hãy chọn nhà cung cấp.");
        RuleFor(x => x.ItemId).NotEmpty().WithMessage("Hãy chọn mặt hàng.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Hãy ghi giải trình vì sao chỉ mua được của NCC này.")
            .MaximumLength(SoleSourceApproval.TextMaxLength);
        RuleFor(x => x.ReviewDate).GreaterThan(x => x.ValidFrom).WithMessage("Hạn đánh giá lại phải sau ngày hiệu lực.");
    }
}

/// <summary>Kiểm tra kết thúc sớm.</summary>
public sealed class EndSoleSourceRequestValidator : AbstractValidator<EndSoleSourceRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public EndSoleSourceRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Hãy nhập lý do kết thúc.").MaximumLength(SoleSourceApproval.TextMaxLength);
    }
}
