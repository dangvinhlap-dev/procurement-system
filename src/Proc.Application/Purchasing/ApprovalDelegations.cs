using FluentValidation;
using Proc.Domain.Purchasing;

namespace Proc.Application.Purchasing;

/// <summary>Ủy quyền duyệt trên danh sách và trong ngăn kéo chi tiết (lát O2).</summary>
/// <param name="Id">Id ủy quyền.</param>
/// <param name="DelegatorUserId">Người ủy quyền.</param>
/// <param name="DelegatorName">Họ tên (tên đăng nhập) người ủy quyền.</param>
/// <param name="DelegateUserId">Người được ủy quyền.</param>
/// <param name="DelegateName">Họ tên (tên đăng nhập) người được ủy quyền.</param>
/// <param name="DocType">Loại phiếu; null = mọi loại.</param>
/// <param name="DocTypeName">Tên loại phiếu, hoặc "Mọi loại phiếu".</param>
/// <param name="ValidFrom">Từ ngày.</param>
/// <param name="ValidTo">Đến ngày.</param>
/// <param name="Reason">Lý do.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="Status">Trạng thái: Upcoming (chưa hiệu lực), Active (đang hiệu lực), Expired (hết hạn), Revoked (đã thu hồi).</param>
/// <param name="RevokedAt">Thời điểm thu hồi (UTC).</param>
/// <param name="RevokedByName">Người thu hồi.</param>
/// <param name="CreatedAt">Thời điểm lập (UTC).</param>
/// <param name="CreatedByName">Người lập (chính người ủy quyền hoặc người quản lý lập thay).</param>
/// <param name="CanRevoke">Người đang xem thu hồi được (người ủy quyền hoặc có quyền quản lý; chưa thu hồi, chưa hết hạn).</param>
public sealed record ApprovalDelegationDto(
    Guid Id, Guid DelegatorUserId, string DelegatorName, Guid DelegateUserId, string DelegateName, string? DocType, string DocTypeName,
    DateOnly ValidFrom, DateOnly ValidTo, string Reason, string? Note, string Status, DateTimeOffset? RevokedAt, string? RevokedByName,
    DateTimeOffset CreatedAt, string? CreatedByName, bool CanRevoke);

/// <summary>Lập ủy quyền duyệt.</summary>
/// <param name="DelegatorUserId">Người ủy quyền; null = chính người đang đăng nhập. Lập thay người khác cần quyền quản lý ủy quyền.</param>
/// <param name="DelegateUserId">Người được ủy quyền.</param>
/// <param name="DocType">Loại phiếu (mã ApprovalTypes); null/rỗng = mọi loại.</param>
/// <param name="ValidFrom">Từ ngày.</param>
/// <param name="ValidTo">Đến ngày.</param>
/// <param name="Reason">Lý do.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record CreateApprovalDelegationRequest(
    Guid? DelegatorUserId, Guid DelegateUserId, string? DocType, DateOnly ValidFrom, DateOnly ValidTo, string Reason, string? Note);

/// <summary>Người dùng để chọn khi lập ủy quyền: có vai trò tại công ty đang làm việc và có ít nhất một quyền duyệt.</summary>
/// <param name="Id">Id người dùng.</param>
/// <param name="UserName">Tên đăng nhập.</param>
/// <param name="FullName">Họ tên.</param>
public sealed record DelegationUserOptionDto(Guid Id, string UserName, string FullName);

/// <summary>Loại phiếu chọn được khi ủy quyền.</summary>
/// <param name="Code">Mã loại.</param>
/// <param name="Name">Tên loại.</param>
public sealed record DelegationDocTypeDto(string Code, string Name);

/// <summary>
/// Ủy quyền duyệt có thời hạn (lát O2, design.md mục 19.3). Người giữ cấp duyệt tự ủy quyền cho người khác duyệt thay mình; người có quyền
/// quản lý ủy quyền lập thay, thu hồi và xem mọi ủy quyền của công ty. Việc cho người được ủy quyền duyệt thay nằm ở cơ chế duyệt.
/// </summary>
public interface IApprovalDelegationService
{
    /// <summary>Ủy quyền của công ty đang làm việc: có quyền quản lý thì toàn bộ, không thì ủy quyền mình lập hoặc mình được nhận; mới nhất trước.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<ApprovalDelegationDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Chi tiết một ủy quyền.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc không được xem.</exception>
    Task<ApprovalDelegationDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lập ủy quyền tại công ty đang làm việc. Người ủy quyền và người được ủy quyền phải là tài khoản còn hoạt động, có quyền duyệt loại phiếu
    /// (mọi loại: ít nhất một quyền duyệt) tại công ty; không trùng ủy quyền còn hiệu lực cùng hai người, cùng loại, chồng ngày.
    /// </summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Ủy quyền vừa lập.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Khi lập thay người khác mà không có quyền quản lý ủy quyền.</exception>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi dữ liệu sai quy tắc.</exception>
    Task<ApprovalDelegationDto> CreateAsync(CreateApprovalDelegationRequest request, CancellationToken cancellationToken);

    /// <summary>Thu hồi ủy quyền (người ủy quyền hoặc người có quyền quản lý).</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Ủy quyền sau khi thu hồi.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Khi không phải người ủy quyền và không có quyền quản lý.</exception>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi đã thu hồi hoặc hết hạn.</exception>
    Task<ApprovalDelegationDto> RevokeAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Người dùng chọn được (người ủy quyền, người được ủy quyền) tại công ty đang làm việc.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo họ tên.</returns>
    Task<IReadOnlyList<DelegationUserOptionDto>> UserOptionsAsync(CancellationToken cancellationToken);

    /// <summary>Loại phiếu chọn được (bỏ các bộ cấp phụ như POX, POK – đã nằm trong loại phiếu gốc).</summary>
    /// <returns>Theo thứ tự hiển thị.</returns>
    IReadOnlyList<DelegationDocTypeDto> DocTypes();
}

/// <summary>Kiểm tra lập ủy quyền.</summary>
public sealed class CreateApprovalDelegationRequestValidator : AbstractValidator<CreateApprovalDelegationRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CreateApprovalDelegationRequestValidator()
    {
        RuleFor(x => x.DelegateUserId).NotEmpty().WithMessage("Hãy chọn người được ủy quyền.");
        RuleFor(x => x.DocType)
            .Must(t => string.IsNullOrWhiteSpace(t) || (ApprovalTypes.Find(t) is { } info && !ApprovalTypes.RuleOnlyCodes.Contains(info.Code)))
            .WithMessage("Loại phiếu ủy quyền không hợp lệ.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Hãy nhập lý do ủy quyền.").MaximumLength(ApprovalDelegation.TextMaxLength);
        RuleFor(x => x.Note).MaximumLength(ApprovalDelegation.TextMaxLength);
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).WithMessage("Ngày kết thúc phải từ ngày bắt đầu trở đi.");
    }
}
