using FluentValidation;
using Proc.Domain.MasterData;

namespace Proc.Application.MasterData;

/// <summary>Kê khai xung đột lợi ích trên danh sách và trong ngăn kéo chi tiết.</summary>
/// <param name="Id">Id kê khai.</param>
/// <param name="UserId">Người có quan hệ.</param>
/// <param name="UserName">Tên đăng nhập.</param>
/// <param name="UserFullName">Họ tên.</param>
/// <param name="SupplierId">NCC.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="Relation">Loại quan hệ (tên enum <see cref="ConflictRelation"/>).</param>
/// <param name="Description">Mô tả quan hệ.</param>
/// <param name="ValidFrom">Hiệu lực từ ngày.</param>
/// <param name="ValidTo">Hiệu lực đến ngày (null = chưa kết thúc).</param>
/// <param name="EndReason">Lý do kết thúc.</param>
/// <param name="IsActive">Còn hiệu lực hôm nay – đang bị chặn duyệt, chấm thầu, chọn thầu.</param>
/// <param name="CreatedAt">Thời điểm ghi nhận (UTC).</param>
/// <param name="CreatedByName">Người ghi nhận (chính người kê khai hoặc người ghi thay).</param>
public sealed record ConflictDeclarationDto(
    Guid Id, Guid UserId, string UserName, string UserFullName, Guid SupplierId, string SupplierCode, string SupplierName, string Relation,
    string Description, DateOnly ValidFrom, DateOnly? ValidTo, string? EndReason, bool IsActive, DateTimeOffset CreatedAt, string? CreatedByName);

/// <summary>Ghi nhận kê khai. UserId để trống = tự kê khai; ghi thay người khác cần quyền quản lý kê khai.</summary>
/// <param name="UserId">Người có quan hệ (null = người đang đăng nhập).</param>
/// <param name="SupplierId">NCC.</param>
/// <param name="Relation">Loại quan hệ: Family, Ownership, Employment, Other.</param>
/// <param name="Description">Mô tả quan hệ.</param>
/// <param name="ValidFrom">Hiệu lực từ ngày (null = hôm nay).</param>
public sealed record CreateConflictDeclarationRequest(Guid? UserId, Guid SupplierId, string Relation, string Description, DateOnly? ValidFrom);

/// <summary>Kết thúc kê khai.</summary>
/// <param name="ValidTo">Ngày cuối còn quan hệ.</param>
/// <param name="Reason">Lý do, bắt buộc.</param>
public sealed record EndConflictDeclarationRequest(DateOnly ValidTo, string Reason);

/// <summary>Người dùng để chọn khi ghi kê khai thay.</summary>
/// <param name="Id">Id người dùng.</param>
/// <param name="UserName">Tên đăng nhập.</param>
/// <param name="FullName">Họ tên.</param>
public sealed record ConflictUserOptionDto(Guid Id, string UserName, string FullName);

/// <summary>NCC để chọn khi kê khai.</summary>
/// <param name="Id">Id NCC.</param>
/// <param name="Code">Mã NCC.</param>
/// <param name="Name">Tên NCC.</param>
public sealed record ConflictSupplierOptionDto(Guid Id, string Code, string Name);

/// <summary>
/// Kê khai xung đột lợi ích nhân sự ↔ NCC (design.md mục 18.8 – đề xuất Ban CUVT N4). Mọi người dùng tự kê khai và xem kê khai của mình;
/// người có quyền xem mọi kê khai (KSNB, Trưởng ban) xem toàn bộ để kiểm tra; người có quyền quản lý ghi thay và kết thúc kê khai.
/// Kê khai còn hiệu lực chặn người đó duyệt phiếu, chấm thầu, chọn thầu có NCC tương ứng.
/// </summary>
public interface IConflictDeclarationService
{
    /// <summary>
    /// Danh sách kê khai: có quyền xem mọi kê khai thì toàn bộ, không thì của chính mình; mới nhất trước.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách kê khai.</returns>
    Task<IReadOnlyList<ConflictDeclarationDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Chi tiết kê khai.
    /// </summary>
    /// <param name="id">Id kê khai.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc là kê khai của người khác mà không có quyền xem mọi kê khai.</exception>
    Task<ConflictDeclarationDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Ghi nhận kê khai.
    /// </summary>
    /// <param name="request">Nội dung kê khai.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kê khai vừa ghi.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Khi ghi thay người khác mà không có quyền quản lý kê khai.</exception>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi dữ liệu sai hoặc đã có kê khai còn hiệu lực cùng người × NCC.</exception>
    Task<ConflictDeclarationDto> CreateAsync(CreateConflictDeclarationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Kết thúc kê khai (hết quan hệ, kê khai nhầm). Cần quyền quản lý kê khai.
    /// </summary>
    /// <param name="id">Id kê khai.</param>
    /// <param name="request">Ngày kết thúc, lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kê khai sau khi kết thúc.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi đã kết thúc hoặc dữ liệu sai.</exception>
    Task<ConflictDeclarationDto> EndAsync(Guid id, EndConflictDeclarationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Người dùng đang hoạt động để chọn khi ghi kê khai thay.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Người dùng theo tên đăng nhập.</returns>
    Task<IReadOnlyList<ConflictUserOptionDto>> UserOptionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Mọi NCC (cả tiềm năng, tạm ngưng, danh sách đen) để chọn khi kê khai – người kê khai có thể không có quyền xem danh mục NCC.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>NCC theo mã.</returns>
    Task<IReadOnlyList<ConflictSupplierOptionDto>> SupplierOptionsAsync(CancellationToken cancellationToken);
}

/// <summary>Kiểm tra ghi nhận kê khai.</summary>
public sealed class CreateConflictDeclarationRequestValidator : AbstractValidator<CreateConflictDeclarationRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CreateConflictDeclarationRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("Hãy chọn nhà cung cấp.");
        RuleFor(x => x.Relation).Must(r => Enum.TryParse<ConflictRelation>(r, out var v) && Enum.IsDefined(v))
            .WithMessage("Hãy chọn loại quan hệ.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Hãy mô tả quan hệ lợi ích.").MaximumLength(ConflictDeclaration.TextMaxLength);
    }
}

/// <summary>Kiểm tra kết thúc kê khai.</summary>
public sealed class EndConflictDeclarationRequestValidator : AbstractValidator<EndConflictDeclarationRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public EndConflictDeclarationRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Hãy nhập lý do kết thúc kê khai.").MaximumLength(ConflictDeclaration.TextMaxLength);
    }
}
