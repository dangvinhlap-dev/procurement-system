using FluentValidation;
using Proc.Application.Purchasing;

namespace Proc.Application.Pricing;

/// <summary>Một dòng danh sách phiếu duyệt NCC và giá tháng.</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số phiếu.</param>
/// <param name="Period">Tháng áp dụng (ngày đầu tháng).</param>
/// <param name="CategoryCode">Mã nhóm hàng.</param>
/// <param name="CategoryName">Tên nhóm hàng.</param>
/// <param name="CompanyCodes">Các công ty áp dụng, cách nhau dấu phẩy.</param>
/// <param name="LineCount">Số mặt hàng.</param>
/// <param name="Status">Draft, Submitted, Approved, Replaced, Cancelled.</param>
/// <param name="ReplacesNo">Số phiếu được điều chỉnh (nếu là phiếu điều chỉnh).</param>
/// <param name="CreatedByName">Người lập.</param>
/// <param name="HasException">Lượt gửi gần nhất có ngoại lệ – qua chặng Chủ tịch/TGĐ (lát H2).</param>
public sealed record MonthlyApprovalListItemDto(
    Guid Id, string No, DateOnly Period, string CategoryCode, string CategoryName, string CompanyCodes, int LineCount, string Status,
    string? ReplacesNo, string? CreatedByName, bool HasException = false);

/// <summary>Một dòng phiếu duyệt tháng.</summary>
/// <param name="Id">Id dòng.</param>
/// <param name="LineNo">Số thứ tự.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomId">ĐVT.</param>
/// <param name="UomCode">Mã ĐVT.</param>
/// <param name="MainSupplierId">NCC chính.</param>
/// <param name="MainSupplierCode">Mã NCC chính.</param>
/// <param name="MainSupplierName">Tên NCC chính.</param>
/// <param name="UnitPrice">Giá NCC chính.</param>
/// <param name="BackupSupplierId">NCC dự phòng.</param>
/// <param name="BackupSupplierCode">Mã NCC dự phòng.</param>
/// <param name="BackupSupplierName">Tên NCC dự phòng.</param>
/// <param name="BackupUnitPrice">Giá NCC dự phòng.</param>
/// <param name="QuantityLimit">Hạn mức SL tháng (null = không giới hạn).</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="UomFactor">Hệ số ĐVT dòng so với ĐVT cơ sở (quy giá thị trường về ĐVT dòng).</param>
/// <param name="StandardPrice">Giá chuẩn (lát G4b).</param>
/// <param name="CeilingPrice">Giá trần.</param>
/// <param name="TargetPrice">Giá mục tiêu.</param>
/// <param name="Exceptions">
/// Ngoại lệ tự phát hiện của dòng (lát H2: chưa đủ 03 NCC đủ điều kiện, độc quyền, giá vượt giá mục tiêu) – tính lúc đọc khi phiếu nháp/chờ
/// duyệt, rỗng khi phiếu đã có quyết định.
/// </param>
public sealed record MonthlyApprovalLineDto(
    Guid Id, int LineNo, Guid ItemId, string ItemCode, string ItemName, Guid UomId, string UomCode, Guid MainSupplierId, string MainSupplierCode,
    string MainSupplierName, decimal UnitPrice, Guid? BackupSupplierId, string? BackupSupplierCode, string? BackupSupplierName,
    decimal? BackupUnitPrice, decimal? QuantityLimit, string? Note, decimal UomFactor = 1m, decimal? StandardPrice = null,
    decimal? CeilingPrice = null, decimal? TargetPrice = null, IReadOnlyList<string>? Exceptions = null);

/// <summary>Thao tác được phép trên phiếu duyệt tháng.</summary>
/// <param name="CanEdit">Sửa, gửi duyệt (nháp, đang làm việc ở tổng công ty, có quyền lập).</param>
/// <param name="CanApprove">Duyệt / từ chối: đến lượt, có quyền duyệt, không phải người lập, không xung đột lợi ích.</param>
/// <param name="CanRecall">Rút về nháp khi đang chờ duyệt.</param>
/// <param name="CanAdjust">Lập phiếu điều chỉnh (đã duyệt, chưa có phiếu điều chỉnh đang soạn/chờ duyệt).</param>
/// <param name="CanCancel">Hủy (nháp hoặc đã duyệt).</param>
/// <param name="CanCopyNextMonth">Chép sang tháng sau.</param>
/// <param name="ReadOnlyReason">Lý do chỉ xem.</param>
public sealed record MonthlyApprovalActions(
    bool CanEdit, bool CanApprove, bool CanRecall, bool CanAdjust, bool CanCancel, bool CanCopyNextMonth, string? ReadOnlyReason);

/// <summary>Công ty chọn được để áp dụng phiếu duyệt tháng.</summary>
/// <param name="Id">Id.</param>
/// <param name="Code">Mã.</param>
/// <param name="Name">Tên.</param>
public sealed record MonthlyApprovalCompanyDto(Guid Id, string Code, string Name);

/// <summary>Chi tiết phiếu duyệt NCC và giá tháng.</summary>
/// <remarks>
/// <paramref name="ReplacesId"/>/<paramref name="ReplacesNo"/>: phiếu được điều chỉnh; <paramref name="ReplacedById"/>/<paramref name="ReplacedByNo"/>:
/// phiếu điều chỉnh của phiếu này (đang soạn, chờ duyệt hoặc đã duyệt); <paramref name="Approvals"/>: lịch sử duyệt (bước chặng 2 là chặng
/// ngoại lệ Chủ tịch/TGĐ); <paramref name="ExceptionNote"/>: giải trình ngoại lệ; <paramref name="HasException"/>: lượt gửi gần nhất có ngoại lệ
/// (lát H2).
/// </remarks>
public sealed record MonthlyApprovalDto(
    Guid Id, string No, string CompanyCode, DateOnly Period, Guid ItemCategoryId, string CategoryCode, string CategoryName,
    IReadOnlyList<MonthlyApprovalCompanyDto> Companies, string? Note, string Status, string? RejectReason, DateTimeOffset? ApprovedAt,
    DateOnly? CancelledOn, string? CancelReason, Guid? ReplacesId, string? ReplacesNo, Guid? ReplacedById, string? ReplacedByNo,
    DateTimeOffset CreatedAt, string? CreatedByName, IReadOnlyList<MonthlyApprovalLineDto> Lines, IReadOnlyList<ApprovalStepDto> Approvals,
    MonthlyApprovalActions Actions, string? ExceptionNote = null, bool HasException = false);

/// <summary>Lưu phiếu duyệt tháng.</summary>
/// <param name="Period">Tháng áp dụng (ngày nào trong tháng cũng được – lưu ngày đầu tháng).</param>
/// <param name="ItemCategoryId">Nhóm hàng.</param>
/// <param name="CompanyIds">Các công ty áp dụng.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="Lines">Các dòng.</param>
/// <param name="ExceptionNote">
/// Giải trình ngoại lệ (lát H2): bắt buộc khi phiếu có ngoại lệ tự phát hiện; ghi khi không có ngoại lệ tự phát hiện là người lập tự khai phiếu
/// có ngoại lệ – phiếu cũng qua chặng Chủ tịch/TGĐ.
/// </param>
public sealed record SaveMonthlyApprovalRequest(
    DateOnly Period, Guid ItemCategoryId, IReadOnlyList<Guid> CompanyIds, string? Note, IReadOnlyList<SaveMonthlyApprovalLineRequest> Lines,
    string? ExceptionNote = null);

/// <summary>Lưu một dòng phiếu duyệt tháng; giá chuẩn/trần/mục tiêu không bắt buộc (lát G4b).</summary>
public sealed record SaveMonthlyApprovalLineRequest(
    Guid? Id, Guid ItemId, Guid UomId, Guid MainSupplierId, decimal UnitPrice, Guid? BackupSupplierId, decimal? BackupUnitPrice,
    decimal? QuantityLimit, string? Note, decimal? StandardPrice = null, decimal? CeilingPrice = null, decimal? TargetPrice = null);

/// <summary>Hủy phiếu duyệt tháng.</summary>
/// <param name="Reason">Lý do, bắt buộc.</param>
public sealed record CancelMonthlyApprovalRequest(string Reason);

/// <summary>
/// Phiếu duyệt NCC và giá tháng (design.md mục 18.25 – lát G2): tổng công ty lập theo tháng × nhóm hàng cho các công ty chọn, Ban cung ứng
/// duyệt qua quy trình loại GTH; công ty thành viên xem phiếu đã duyệt áp cho mình.
/// </summary>
public interface IMonthlySupplierApprovalService
{
    /// <summary>
    /// Danh sách: ở tổng công ty mọi phiếu; ở công ty thành viên chỉ phiếu đã duyệt/đã thay áp cho công ty đó. Theo phạm vi nhóm hàng.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách, tháng mới nhất trước.</returns>
    Task<IReadOnlyList<MonthlyApprovalListItemDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Chi tiết phiếu.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    /// <exception cref="Common.NotFoundException">Khi không thấy hoặc ngoài phạm vi.</exception>
    Task<MonthlyApprovalDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Công ty đang hoạt động – để chọn công ty áp dụng.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách công ty, tổng công ty đứng đầu.</returns>
    Task<IReadOnlyList<MonthlyApprovalCompanyDto>> CompaniesAsync(CancellationToken cancellationToken);

    /// <summary>Lập phiếu nháp (chỉ khi đang làm việc ở tổng công ty).</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu vừa lập.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Khi không làm việc ở tổng công ty.</exception>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi sai quy tắc (trùng mặt hàng × tháng × công ty, NCC chưa duyệt…).</exception>
    Task<MonthlyApprovalDto> CreateAsync(SaveMonthlyApprovalRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa phiếu nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi sửa.</returns>
    Task<MonthlyApprovalDto> UpdateAsync(Guid id, SaveMonthlyApprovalRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Gửi duyệt (kiểm tra lại NCC và trùng lặp). Phiếu có ngoại lệ (lát H2) phải có giải trình và qua thêm chặng Chủ tịch/TGĐ (GTX).
    /// </summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu (Chờ duyệt, hoặc Đã duyệt nếu không có cấp nào).</returns>
    Task<MonthlyApprovalDto> SubmitAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Duyệt cấp đến lượt; duyệt đủ thì phiếu có tác dụng, phiếu được điều chỉnh (nếu có) thành Đã thay.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Ý kiến.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<MonthlyApprovalDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Từ chối (bắt buộc lý do): phiếu về nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<MonthlyApprovalDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Rút phiếu đang chờ duyệt về nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<MonthlyApprovalDto> RecallAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Hủy phiếu nháp hoặc đã duyệt (bắt buộc lý do).</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<MonthlyApprovalDto> CancelAsync(Guid id, CancelMonthlyApprovalRequest request, CancellationToken cancellationToken);

    /// <summary>Lập phiếu điều chỉnh (nháp, cùng tháng, chép nội dung) cho phiếu đã duyệt.</summary>
    /// <param name="id">Phiếu đã duyệt.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu điều chỉnh.</returns>
    Task<MonthlyApprovalDto> AdjustAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Chép phiếu sang tháng sau (phiếu nháp mới, cùng nhóm hàng, công ty, dòng).</summary>
    /// <param name="id">Phiếu nguồn.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu mới.</returns>
    Task<MonthlyApprovalDto> CopyNextMonthAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra dữ liệu lưu phiếu duyệt tháng.</summary>
public sealed class SaveMonthlyApprovalRequestValidator : AbstractValidator<SaveMonthlyApprovalRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public SaveMonthlyApprovalRequestValidator()
    {
        RuleFor(x => x.ItemCategoryId).NotEmpty().WithMessage("Hãy chọn nhóm hàng.");
        RuleFor(x => x.CompanyIds).NotEmpty().WithMessage("Hãy chọn ít nhất một công ty áp dụng.");
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.ExceptionNote).MaximumLength(1000).WithMessage("Giải trình ngoại lệ tối đa 1.000 ký tự.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Phiếu phải có ít nhất một mặt hàng.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty().WithMessage("Hãy chọn mặt hàng.");
            l.RuleFor(x => x.UomId).NotEmpty().WithMessage("Hãy chọn ĐVT.");
            l.RuleFor(x => x.MainSupplierId).NotEmpty().WithMessage("Hãy chọn NCC chính.");
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Giá phải ≥ 0.");
            l.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}

/// <summary>Kiểm tra yêu cầu hủy phiếu duyệt tháng.</summary>
public sealed class CancelMonthlyApprovalRequestValidator : AbstractValidator<CancelMonthlyApprovalRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public CancelMonthlyApprovalRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Hãy nhập lý do hủy.").MaximumLength(1000);
    }
}
