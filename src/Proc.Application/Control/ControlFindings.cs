using FluentValidation;
using Proc.Domain.Control;

namespace Proc.Application.Control;

/// <summary>Vi phạm KSNB trên danh sách và trong ngăn kéo chi tiết.</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số phiếu.</param>
/// <param name="Date">Ngày phát hiện.</param>
/// <param name="Severity">Mức: Normal, Serious.</param>
/// <param name="Status">Trạng thái: Open, Concluded.</param>
/// <param name="AnomalyCode">Loại dấu hiệu gốc (<see cref="AnomalyCodes"/>), null = tự phát hiện.</param>
/// <param name="Title">Tiêu đề.</param>
/// <param name="Description">Mô tả.</param>
/// <param name="RefScreen">Màn hình chứng từ liên quan.</param>
/// <param name="RefId">Chứng từ liên quan.</param>
/// <param name="RefNo">Số chứng từ liên quan.</param>
/// <param name="SupplierId">NCC liên quan.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="Conclusion">Kết luận.</param>
/// <param name="ConcludedAt">Thời điểm kết luận (UTC).</param>
/// <param name="ConcludedByName">Người kết luận.</param>
/// <param name="CreatedAt">Thời điểm ghi nhận (UTC).</param>
/// <param name="CreatedByName">Người ghi nhận.</param>
public sealed record ControlFindingDto(
    Guid Id, string No, DateOnly Date, string Severity, string Status, string? AnomalyCode, string Title, string Description, string? RefScreen,
    Guid? RefId, string? RefNo, Guid? SupplierId, string? SupplierCode, string? SupplierName, string? Conclusion, DateTimeOffset? ConcludedAt,
    string? ConcludedByName, DateTimeOffset CreatedAt, string? CreatedByName);

/// <summary>Ghi nhận hoặc sửa vi phạm. Khi sửa, loại dấu hiệu và chứng từ liên quan giữ như lúc ghi nhận.</summary>
/// <param name="Date">Ngày phát hiện.</param>
/// <param name="Severity">Mức: Normal, Serious.</param>
/// <param name="Title">Tiêu đề.</param>
/// <param name="Description">Mô tả, bằng chứng.</param>
/// <param name="SupplierId">NCC liên quan.</param>
/// <param name="AnomalyCode">Loại dấu hiệu gốc.</param>
/// <param name="RefScreen">Màn hình chứng từ liên quan.</param>
/// <param name="RefId">Chứng từ liên quan.</param>
/// <param name="RefNo">Số chứng từ liên quan.</param>
public sealed record SaveControlFindingRequest(
    DateOnly Date, string Severity, string Title, string Description, Guid? SupplierId, string? AnomalyCode = null, string? RefScreen = null,
    Guid? RefId = null, string? RefNo = null);

/// <summary>Kết luận vi phạm.</summary>
/// <param name="Conclusion">Kết luận, hướng xử lý.</param>
/// <param name="Severity">Mức sau xác minh: Normal, Serious.</param>
public sealed record ConcludeControlFindingRequest(string Conclusion, string Severity);

/// <summary>
/// Sổ vi phạm KSNB (design.md mục 18.19): KSNB ghi nhận, kết luận; lãnh đạo nhận thông báo thẳng và xem. Phạm vi công ty đang làm việc.
/// </summary>
public interface IControlFindingService
{
    /// <summary>
    /// Vi phạm của công ty đang làm việc, mới nhất trước. Cần quyền xem dấu hiệu bất thường hoặc quyền nhận báo cáo vi phạm.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<ControlFindingDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Chi tiết vi phạm.
    /// </summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc thuộc công ty khác.</exception>
    Task<ControlFindingDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Ghi nhận vi phạm, cấp số loại VP, thông báo thẳng người có quyền nhận báo cáo vi phạm tại công ty.
    /// </summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Vi phạm vừa ghi.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi dữ liệu sai.</exception>
    Task<ControlFindingDto> CreateAsync(SaveControlFindingRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Sửa vi phạm còn mở.
    /// </summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Nội dung mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Vi phạm sau khi sửa.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi đã kết luận hoặc dữ liệu sai.</exception>
    Task<ControlFindingDto> UpdateAsync(Guid id, SaveControlFindingRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Kết luận vi phạm (khóa phiếu), thông báo lãnh đạo.
    /// </summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Kết luận, mức sau xác minh.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Vi phạm sau khi kết luận.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi đã kết luận hoặc thiếu kết luận.</exception>
    Task<ControlFindingDto> ConcludeAsync(Guid id, ConcludeControlFindingRequest request, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra ghi nhận/sửa vi phạm.</summary>
public sealed class SaveControlFindingRequestValidator : AbstractValidator<SaveControlFindingRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public SaveControlFindingRequestValidator()
    {
        RuleFor(x => x.Severity).Must(BeSeverity).WithMessage("Hãy chọn mức vi phạm.");
        RuleFor(x => x.Title).NotEmpty().WithMessage("Hãy nhập tiêu đề vi phạm.").MaximumLength(ControlFinding.TitleMaxLength);
        RuleFor(x => x.Description).NotEmpty().WithMessage("Hãy mô tả vi phạm.").MaximumLength(ControlFinding.TextMaxLength);
        RuleFor(x => x.AnomalyCode).MaximumLength(40);
        RuleFor(x => x.RefScreen).MaximumLength(20);
        RuleFor(x => x.RefNo).MaximumLength(50);
    }

    /// <summary>Mức hợp lệ: Normal, Serious.</summary>
    internal static bool BeSeverity(string? severity) => Enum.TryParse<FindingSeverity>(severity, out var v) && Enum.IsDefined(v);
}

/// <summary>Kiểm tra kết luận vi phạm.</summary>
public sealed class ConcludeControlFindingRequestValidator : AbstractValidator<ConcludeControlFindingRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public ConcludeControlFindingRequestValidator()
    {
        RuleFor(x => x.Conclusion).NotEmpty().WithMessage("Hãy nhập kết luận.").MaximumLength(ControlFinding.TextMaxLength);
        RuleFor(x => x.Severity).Must(SaveControlFindingRequestValidator.BeSeverity).WithMessage("Hãy chọn mức vi phạm.");
    }
}
