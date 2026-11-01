using Proc.Application.Purchasing;

namespace Proc.Application.MasterData;

/// <summary>Một yêu cầu duyệt NCC (design.md mục 18.6).</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số yêu cầu.</param>
/// <param name="SupplierId">NCC.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="Kind">Status (đổi trạng thái) hoặc BankAccounts (đổi tài khoản ngân hàng).</param>
/// <param name="CurrentSummary">Giá trị hiện tại lúc lập.</param>
/// <param name="ProposedSummary">Giá trị đề nghị.</param>
/// <param name="Status">Submitted, Approved, Rejected, Cancelled.</param>
/// <param name="CreatedByName">Người lập.</param>
/// <param name="CreatedAt">Thời điểm lập (UTC).</param>
/// <param name="ApprovedAt">Thời điểm duyệt xong (UTC).</param>
/// <param name="RejectReason">Lý do từ chối.</param>
/// <param name="WaitingFor">Đang chờ công ty nào duyệt (mã công ty, cách nhau dấu phẩy); null khi đã kết thúc.</param>
/// <param name="Approvals">Lịch sử duyệt theo chặng, công ty, cấp.</param>
/// <param name="CanApprove">Người xem duyệt/từ chối được ngay (đến lượt công ty đang làm việc, có quyền).</param>
/// <param name="CanCancel">Người xem là người lập và yêu cầu còn chờ duyệt.</param>
public sealed record SupplierChangeRequestDto(
    Guid Id, string No, Guid SupplierId, string SupplierCode, string SupplierName, string Kind, string CurrentSummary, string ProposedSummary,
    string Status, string? CreatedByName, DateTimeOffset CreatedAt, DateTimeOffset? ApprovedAt, string? RejectReason, string? WaitingFor,
    IReadOnlyList<ApprovalStepDto> Approvals, bool CanApprove, bool CanCancel);

/// <summary>
/// Yêu cầu duyệt NCC (design.md mục 18.6, quyết định Q7): đưa NCC vào Đã duyệt/Danh sách đen, đổi tài khoản ngân hàng. Yêu cầu tự lập khi
/// lưu NCC có thay đổi các phần này; duyệt hai chặng – các công ty liên quan (đang mua của NCC) duyệt xong rồi Tổng công ty duyệt lần cuối.
/// </summary>
public interface ISupplierChangeRequestService
{
    /// <summary>Danh sách yêu cầu, mới nhất trước.</summary>
    /// <param name="supplierId">Chỉ yêu cầu của một NCC; null = mọi NCC.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<SupplierChangeRequestDto>> ListAsync(Guid? supplierId, CancellationToken cancellationToken);

    /// <summary>Chi tiết một yêu cầu.</summary>
    /// <exception cref="Common.NotFoundException">Khi không có yêu cầu.</exception>
    Task<SupplierChangeRequestDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Duyệt bước đến lượt công ty đang làm việc; duyệt xong chặng Tổng công ty thì áp thay đổi vào NCC.</summary>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi chưa đến lượt, không có quyền hoặc là người lập.</exception>
    Task<SupplierChangeRequestDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Từ chối (bắt buộc lý do): cả yêu cầu kết thúc, NCC giữ nguyên.</summary>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi chưa đến lượt, không có quyền hoặc thiếu lý do.</exception>
    Task<SupplierChangeRequestDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Người lập rút yêu cầu còn chờ duyệt.</summary>
    /// <exception cref="Common.ForbiddenAccessException">Khi không phải người lập.</exception>
    Task<SupplierChangeRequestDto> CancelAsync(Guid id, CancellationToken cancellationToken);
}
