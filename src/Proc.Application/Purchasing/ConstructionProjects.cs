using FluentValidation;
using Proc.Domain.Purchasing;

namespace Proc.Application.Purchasing;

/// <summary>Công trình trên danh sách và trong ngăn kéo chi tiết (lát Y2, design.md mục 19.2).</summary>
/// <param name="Id">Id công trình.</param>
/// <param name="Code">Mã.</param>
/// <param name="Name">Tên.</param>
/// <param name="DepartmentId">Phòng ban đề xuất.</param>
/// <param name="DepartmentCode">Mã phòng ban.</param>
/// <param name="DepartmentName">Tên phòng ban.</param>
/// <param name="Location">Địa điểm.</param>
/// <param name="StartDate">Ngày khởi công.</param>
/// <param name="ExpectedEndDate">Ngày dự kiến hoàn thành.</param>
/// <param name="EstimateAmount">Tổng dự toán hiện hành (VND chưa VAT).</param>
/// <param name="InitialEstimateAmount">Dự toán ban đầu (lần 0).</param>
/// <param name="RevisionCount">Số lần điều chỉnh (không tính lần 0).</param>
/// <param name="Status">Trạng thái: InProgress, Completed, Suspended.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="CreatedAt">Thời điểm tạo (UTC).</param>
/// <param name="CreatedByName">Người tạo.</param>
/// <param name="Revisions">Lịch sử dự toán (lần 0 trước); chỉ có ở chi tiết, danh sách trả rỗng.</param>
public sealed record ConstructionProjectDto(
    Guid Id, string Code, string Name, Guid DepartmentId, string DepartmentCode, string DepartmentName, string? Location, DateOnly? StartDate,
    DateOnly? ExpectedEndDate, decimal EstimateAmount, decimal InitialEstimateAmount, int RevisionCount, string Status, string? Note,
    DateTimeOffset CreatedAt, string? CreatedByName, IReadOnlyList<ConstructionProjectRevisionDto> Revisions)
{
    /// <summary>Lũy kế đã mua chưa VAT: dòng PO đã duyệt / đã đóng lấy từ đề nghị loại A của công trình (lát Y3, Q4).</summary>
    public decimal SpentAmount { get; init; }

    /// <summary>Lũy kế đã mua gồm VAT.</summary>
    public decimal SpentAmountWithVat { get; init; }

    /// <summary>Còn lại = dự toán − lũy kế chưa VAT (âm = đã vượt dự toán).</summary>
    public decimal RemainingAmount { get; init; }
}

/// <summary>Một lần ghi dự toán.</summary>
/// <param name="Id">Id dòng.</param>
/// <param name="RevisionNo">Lần: 0 = ban đầu.</param>
/// <param name="EffectiveDate">Ngày hiệu lực.</param>
/// <param name="PreviousAmount">Dự toán trước.</param>
/// <param name="NewAmount">Dự toán sau.</param>
/// <param name="Difference">Chênh lệch (sau − trước).</param>
/// <param name="Basis">Căn cứ.</param>
/// <param name="Reason">Lý do.</param>
/// <param name="CreatedAt">Thời điểm ghi (UTC).</param>
/// <param name="CreatedByName">Người ghi.</param>
public sealed record ConstructionProjectRevisionDto(
    Guid Id, int RevisionNo, DateOnly EffectiveDate, decimal PreviousAmount, decimal NewAmount, decimal Difference, string? Basis, string? Reason,
    DateTimeOffset CreatedAt, string? CreatedByName);

/// <summary>Công trình để chọn trên phiếu (đề nghị mua loại A – lát Y3).</summary>
/// <param name="Id">Id.</param>
/// <param name="Code">Mã.</param>
/// <param name="Name">Tên.</param>
/// <param name="DepartmentId">Phòng ban đề xuất.</param>
/// <param name="EstimateAmount">Tổng dự toán hiện hành.</param>
/// <param name="Status">Trạng thái.</param>
public sealed record ConstructionProjectOptionDto(Guid Id, string Code, string Name, Guid DepartmentId, decimal EstimateAmount, string Status);

/// <summary>Tạo công trình.</summary>
/// <param name="Code">Mã.</param>
/// <param name="Name">Tên.</param>
/// <param name="DepartmentId">Phòng ban đề xuất.</param>
/// <param name="Location">Địa điểm.</param>
/// <param name="StartDate">Ngày khởi công.</param>
/// <param name="ExpectedEndDate">Ngày dự kiến hoàn thành.</param>
/// <param name="EstimateAmount">Tổng dự toán ban đầu (VND chưa VAT).</param>
/// <param name="Basis">Căn cứ dự toán ban đầu (số quyết định…).</param>
/// <param name="Note">Ghi chú.</param>
public sealed record CreateConstructionProjectRequest(
    string Code, string Name, Guid DepartmentId, string? Location, DateOnly? StartDate, DateOnly? ExpectedEndDate, decimal EstimateAmount,
    string? Basis, string? Note);

/// <summary>Sửa thông tin chung, trạng thái (không đổi mã, dự toán).</summary>
/// <param name="Name">Tên.</param>
/// <param name="DepartmentId">Phòng ban đề xuất.</param>
/// <param name="Location">Địa điểm.</param>
/// <param name="StartDate">Ngày khởi công.</param>
/// <param name="ExpectedEndDate">Ngày dự kiến hoàn thành.</param>
/// <param name="Status">Trạng thái: InProgress, Completed, Suspended.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record UpdateConstructionProjectRequest(
    string Name, Guid DepartmentId, string? Location, DateOnly? StartDate, DateOnly? ExpectedEndDate, string Status, string? Note);

/// <summary>Điều chỉnh tổng dự toán.</summary>
/// <param name="NewAmount">Tổng dự toán mới.</param>
/// <param name="EffectiveDate">Ngày hiệu lực (ngày quyết định).</param>
/// <param name="Basis">Căn cứ.</param>
/// <param name="Reason">Lý do (bắt buộc).</param>
public sealed record ReviseConstructionEstimateRequest(decimal NewAmount, DateOnly EffectiveDate, string? Basis, string Reason);

/// <summary>
/// Danh mục công trình và dự toán của công ty đang làm việc (lát Y2, design.md mục 19.2). Lũy kế đã mua / còn lại tính theo PO đã duyệt lấy
/// dòng từ đề nghị mua loại A (lát Y3).
/// </summary>
public interface IConstructionProjectService
{
    /// <summary>Mọi công trình của công ty đang làm việc, mã tăng dần.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách (không kèm lịch sử dự toán).</returns>
    Task<IReadOnlyList<ConstructionProjectDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Công trình đang thực hiện để chọn trên phiếu.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo mã.</returns>
    Task<IReadOnlyList<ConstructionProjectOptionDto>> OptionsAsync(CancellationToken cancellationToken);

    /// <summary>Chi tiết kèm lịch sử dự toán.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc thuộc công ty khác.</exception>
    Task<ConstructionProjectDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tạo công trình kèm dự toán ban đầu tại công ty đang làm việc.</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Công trình vừa tạo.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi trùng mã, phòng ban không hợp lệ hoặc dữ liệu sai quy tắc.</exception>
    Task<ConstructionProjectDto> CreateAsync(CreateConstructionProjectRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa thông tin chung, trạng thái.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Công trình sau khi sửa.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có.</exception>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi dữ liệu sai quy tắc.</exception>
    Task<ConstructionProjectDto> UpdateAsync(Guid id, UpdateConstructionProjectRequest request, CancellationToken cancellationToken);

    /// <summary>Điều chỉnh tổng dự toán – thêm một dòng lịch sử.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Công trình sau khi điều chỉnh.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có.</exception>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi công trình không còn thực hiện hoặc dữ liệu sai quy tắc.</exception>
    Task<ConstructionProjectDto> ReviseEstimateAsync(Guid id, ReviseConstructionEstimateRequest request, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra tạo công trình.</summary>
public sealed class CreateConstructionProjectRequestValidator : AbstractValidator<CreateConstructionProjectRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CreateConstructionProjectRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Hãy nhập mã công trình.").MaximumLength(ConstructionProject.CodeMaxLength);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên công trình.").MaximumLength(ConstructionProject.NameMaxLength);
        RuleFor(x => x.DepartmentId).NotEmpty().WithMessage("Hãy chọn phòng ban đề xuất của công trình.");
        RuleFor(x => x.Location).MaximumLength(ConstructionProject.NameMaxLength);
        RuleFor(x => x.EstimateAmount).GreaterThan(0).WithMessage("Tổng dự toán phải lớn hơn 0.");
        RuleFor(x => x.Basis).MaximumLength(ConstructionProject.TextMaxLength);
        RuleFor(x => x.Note).MaximumLength(ConstructionProject.TextMaxLength);
    }
}

/// <summary>Kiểm tra sửa công trình.</summary>
public sealed class UpdateConstructionProjectRequestValidator : AbstractValidator<UpdateConstructionProjectRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public UpdateConstructionProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên công trình.").MaximumLength(ConstructionProject.NameMaxLength);
        RuleFor(x => x.DepartmentId).NotEmpty().WithMessage("Hãy chọn phòng ban đề xuất của công trình.");
        RuleFor(x => x.Location).MaximumLength(ConstructionProject.NameMaxLength);
        RuleFor(x => x.Status).Must(s => Enum.TryParse<ConstructionProjectStatus>(s, out var v) && Enum.IsDefined(v))
            .WithMessage("Trạng thái công trình không hợp lệ.");
        RuleFor(x => x.Note).MaximumLength(ConstructionProject.TextMaxLength);
    }
}

/// <summary>Kiểm tra điều chỉnh dự toán.</summary>
public sealed class ReviseConstructionEstimateRequestValidator : AbstractValidator<ReviseConstructionEstimateRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public ReviseConstructionEstimateRequestValidator()
    {
        RuleFor(x => x.NewAmount).GreaterThan(0).WithMessage("Tổng dự toán phải lớn hơn 0.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Hãy nhập lý do điều chỉnh dự toán.").MaximumLength(ConstructionProject.TextMaxLength);
        RuleFor(x => x.Basis).MaximumLength(ConstructionProject.TextMaxLength);
    }
}
