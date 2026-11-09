namespace Proc.Application.Documents;

/// <summary>Một ý kiến thẩm định (lát H1 – design.md mục 18.25).</summary>
/// <param name="Id">Khóa.</param>
/// <param name="Aspect">Khía cạnh: Quality / Legal / Other.</param>
/// <param name="Result">Kết luận: Pass / Fail.</param>
/// <param name="Content">Nội dung.</param>
/// <param name="CreatedAt">Thời điểm ghi (UTC).</param>
/// <param name="AuthorName">Họ tên người ghi.</param>
/// <param name="AuthorRoles">Tên các vai trò của người ghi ở công ty của chủ (để Hội đồng biết ý kiến của ai: thẩm định chất lượng, pháp lý…).</param>
/// <param name="IsWithdrawn">Đã rút.</param>
/// <param name="WithdrawReason">Lý do rút.</param>
/// <param name="WithdrawnAt">Thời điểm rút (UTC).</param>
/// <param name="CanWithdraw">Người đang xem rút được (là người ghi, chủ còn mở, chưa rút).</param>
public sealed record AppraisalOpinionDto(
    Guid Id, string Aspect, string Result, string Content, DateTimeOffset CreatedAt, string? AuthorName, string? AuthorRoles,
    bool IsWithdrawn, string? WithdrawReason, DateTimeOffset? WithdrawnAt, bool CanWithdraw);

/// <summary>Ý kiến thẩm định của một đối tượng kèm tổng hợp cho người duyệt.</summary>
/// <param name="Opinions">Mọi ý kiến (cả đã rút), cũ trước mới sau.</param>
/// <param name="PassCount">Số ý kiến Đạt còn hiệu lực.</param>
/// <param name="FailCount">Số ý kiến Không đạt còn hiệu lực.</param>
/// <param name="CanGive">Người đang xem ghi được ý kiến.</param>
/// <param name="GiveBlockedReason">Lý do không ghi được (null khi ghi được) – giao diện hiện trên nút.</param>
public sealed record AppraisalOpinionListDto(
    IReadOnlyList<AppraisalOpinionDto> Opinions, int PassCount, int FailCount, bool CanGive, string? GiveBlockedReason);

/// <summary>Yêu cầu ghi ý kiến thẩm định.</summary>
/// <param name="OwnerType">Loại chủ: MonthlySupplierApproval, Rfq, PurchaseContract, Supplier.</param>
/// <param name="OwnerId">Id chủ.</param>
/// <param name="Aspect">Khía cạnh: Quality / Legal / Other.</param>
/// <param name="Result">Kết luận: Pass / Fail.</param>
/// <param name="Content">Nội dung.</param>
public sealed record GiveAppraisalOpinionRequest(string OwnerType, Guid OwnerId, string Aspect, string Result, string? Content);

/// <summary>Yêu cầu rút ý kiến.</summary>
/// <param name="Reason">Lý do (bắt buộc).</param>
public sealed record WithdrawAppraisalOpinionRequest(string? Reason);

/// <summary>
/// Ý kiến thẩm định của thành viên chuyên môn (lát H1 – design.md mục 18.25) – không phải cấp duyệt. Xem theo quyền xem chủ; ghi cần thêm quyền
/// <c>Sourcing.Appraisal.Give</c>. Chủ đọc qua global query filter nên ngoài phạm vi công ty coi như không có.
/// </summary>
public interface IAppraisalOpinionService
{
    /// <summary>Ý kiến của một đối tượng.</summary>
    /// <param name="ownerType">Loại chủ.</param>
    /// <param name="ownerId">Id chủ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách và tổng hợp.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Không có quyền xem chủ.</exception>
    /// <exception cref="Common.NotFoundException">Chủ không tồn tại hoặc ngoài phạm vi.</exception>
    Task<AppraisalOpinionListDto> ListAsync(string ownerType, Guid ownerId, CancellationToken cancellationToken);

    /// <summary>Ghi ý kiến.</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Ý kiến vừa ghi.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Chủ đã có quyết định cuối cùng, người lập tự thẩm định, nội dung sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Không có quyền ghi ý kiến hoặc quyền xem chủ.</exception>
    /// <exception cref="Common.NotFoundException">Chủ không tồn tại hoặc ngoài phạm vi.</exception>
    Task<AppraisalOpinionDto> GiveAsync(GiveAppraisalOpinionRequest request, CancellationToken cancellationToken);

    /// <summary>Người ghi rút ý kiến.</summary>
    /// <param name="id">Ý kiến.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Ý kiến sau khi rút.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Không phải người ghi, đã rút, chủ đã có quyết định cuối cùng, thiếu lý do.</exception>
    /// <exception cref="Common.NotFoundException">Không tìm thấy ý kiến hoặc chủ ngoài phạm vi.</exception>
    Task<AppraisalOpinionDto> WithdrawAsync(Guid id, WithdrawAppraisalOpinionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Kiểm tra quyền dùng đính kèm của một ý kiến: xem theo quyền xem chủ; thêm/xóa chỉ người ghi khi ý kiến chưa rút và chủ còn mở.
    /// </summary>
    /// <param name="opinionId">Ý kiến.</param>
    /// <param name="manage">Thêm/xóa (true) hay chỉ xem (false).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>CompanyId của chủ (null với NCC) – đính kèm lưu cùng công ty.</returns>
    Task<Guid?> RequireAttachmentAccessAsync(Guid opinionId, bool manage, CancellationToken cancellationToken);
}
