using Proc.Domain.Common;
using Proc.Domain.Organization;

namespace Proc.Application.Common;

/// <summary>
/// Quy tắc chung của lệnh Hủy phiếu (design.md mục 20.2a, người dùng chốt 11/11/2026): hủy là **quyền đặc biệt chỉ cấp cho quản trị**
/// (<see cref="Permissions.System.DocumentCancel"/>); ngoại lệ phiếu đang **Nháp** (chưa gửi duyệt, bị từ chối hoặc rút về nháp) thì
/// **người lập tự hủy được** phiếu của mình. Mọi lần hủy **bắt buộc lý do**. Điều kiện trạng thái riêng của từng loại phiếu (ví dụ PO đã có
/// phiếu nhận hàng) vẫn do service/domain của loại đó kiểm tra.
/// </summary>
public static class DocumentCancelPolicy
{
    /// <summary>
    /// Người dùng hiện tại có được hủy phiếu không (chỉ xét quyền – dùng cho cờ CanCancel trên giao diện).
    /// </summary>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="isDraft">Phiếu đang ở trạng thái Nháp (hoặc tương đương: phiếu tìm nguồn đang mở – không có bước duyệt).</param>
    /// <param name="createdBy">Người lập phiếu.</param>
    /// <returns>true khi có quyền Hủy phiếu, hoặc phiếu Nháp và người dùng là người lập.</returns>
    public static bool CanCancel(ICurrentUserScope scope, bool isDraft, Guid? createdBy) =>
        scope.HasPermission(Permissions.System.DocumentCancel) || (isDraft && createdBy is { } c && c == scope.UserId);

    /// <summary>
    /// Kiểm tra quyền hủy và lý do trước khi hủy.
    /// </summary>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="docLabel">Tên phiếu trong câu báo lỗi, ví dụ "PO PO-BHA-2611-0001".</param>
    /// <param name="isDraft">Phiếu đang Nháp.</param>
    /// <param name="createdBy">Người lập.</param>
    /// <param name="reason">Lý do hủy người dùng nhập.</param>
    /// <returns>Lý do đã bỏ khoảng trắng hai đầu.</returns>
    /// <exception cref="ForbiddenAccessException">Khi không phải người lập phiếu Nháp và không có quyền Hủy phiếu.</exception>
    /// <exception cref="BusinessRuleException">Khi thiếu lý do.</exception>
    public static string Authorize(ICurrentUserScope scope, string docLabel, bool isDraft, Guid? createdBy, string? reason)
    {
        if (!CanCancel(scope, isDraft, createdBy))
            throw new ForbiddenAccessException(isDraft
                ? $"Chỉ người lập hoặc quản trị được hủy {docLabel} (phiếu nháp)."
                : $"{docLabel} đã gửi duyệt hoặc đã dùng – chỉ quản trị (quyền Hủy phiếu) được hủy (design.md mục 20.2a).");
        var text = reason?.Trim();
        if (string.IsNullOrEmpty(text)) throw new BusinessRuleException($"Hãy nhập lý do hủy {docLabel}.");
        return text;
    }
}
