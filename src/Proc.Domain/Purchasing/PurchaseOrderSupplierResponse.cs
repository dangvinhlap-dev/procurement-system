using Proc.Domain.Common;

namespace Proc.Domain.Purchasing;

/// <summary>Phản hồi của NCC với đơn đặt hàng trên Portal (lát P4a).</summary>
public enum SupplierOrderDecision
{
    /// <summary>NCC xác nhận nhận đơn, giao theo PO (kèm ngày giao cam kết).</summary>
    Confirmed = 1,

    /// <summary>NCC từ chối đơn (bắt buộc lý do) – Phòng CUVT xử lý: hủy PO hoặc hoàn thành PO, đặt NCC khác.</summary>
    Rejected = 2,
}

/// <summary>
/// NCC xác nhận hoặc từ chối PO đã duyệt trên Portal (lát P4a – design.md mục 19.5, quy tắc P4-2). Bản ghi riêng, không ghi lên PO: PO đã duyệt
/// là đã khóa (design.md mục 20 – R1). Mỗi PO **một phản hồi có hiệu lực, không sửa** (chỉ mục duy nhất trên phản hồi chưa mở lại – hai người cùng
/// NCC bấm cùng lúc thì người sau bị chặn); Phòng CUVT **mở lại** thì phản hồi cũ thành lịch sử, NCC phản hồi lại. Không gắn [Auditable]: phản hồi do tài khoản Portal (không phải người dùng nội bộ) tạo, không ai sửa, tự ghi tài khoản – giờ phản hồi
/// nên chính bản ghi là dấu vết (như bản nộp báo giá P3a, hỏi đáp P3b-2). <see cref="CompanyId"/> là công ty đặt hàng (công ty của PO).
/// </summary>
public sealed class PurchaseOrderSupplierResponse : Entity, ICompanyOwned
{
    /// <summary>Độ dài tối đa ghi chú / lý do từ chối.</summary>
    public const int NoteMaxLength = 1000;

    private PurchaseOrderSupplierResponse()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Đơn đặt hàng.</summary>
    public Guid PurchaseOrderId { get; private set; }

    /// <summary>NCC của PO (NCC của phiên Portal).</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Xác nhận hay từ chối.</summary>
    public SupplierOrderDecision Decision { get; private set; }

    /// <summary>Ngày giao NCC cam kết (chỉ khi xác nhận).</summary>
    public DateOnly? PromisedDate { get; private set; }

    /// <summary>Ghi chú khi xác nhận (tùy chọn) hoặc lý do từ chối (bắt buộc).</summary>
    public string? Note { get; private set; }

    /// <summary>Thời điểm phản hồi (UTC).</summary>
    public DateTimeOffset RespondedAt { get; private set; }

    /// <summary>Tài khoản Portal phản hồi.</summary>
    public Guid PortalUserId { get; private set; }

    /// <summary>Email tài khoản lúc phản hồi (giữ lại dù tài khoản đổi sau này).</summary>
    public string RespondedByEmail { get; private set; } = string.Empty;

    /// <summary>Lúc Phòng CUVT mở lại phản hồi (UTC); null = phản hồi đang có hiệu lực.</summary>
    public DateTimeOffset? ReopenedAt { get; private set; }

    /// <summary>Người dùng nội bộ mở lại.</summary>
    public Guid? ReopenedBy { get; private set; }

    /// <summary>Họ tên người mở lại lúc mở (giữ lại dù người dùng đổi tên sau này).</summary>
    public string? ReopenedByName { get; private set; }

    /// <summary>Lý do mở lại – gửi cho NCC (thư và trên Portal).</summary>
    public string? ReopenReason { get; private set; }

    /// <summary>Phản hồi đang có hiệu lực (chưa bị mở lại).</summary>
    public bool IsCurrent => ReopenedAt is null;

    /// <summary>
    /// Phòng CUVT mở lại phản hồi (yêu cầu người dùng 12/11/2026 – P4-2b): phản hồi này thành lịch sử, NCC xác nhận / từ chối lại được. Chỉ khi
    /// PO còn Đã duyệt; bắt buộc lý do (gửi NCC). Không xóa bản ghi – lịch sử các lần phản hồi giữ nguyên.
    /// </summary>
    /// <param name="order">PO.</param>
    /// <param name="userId">Người mở lại.</param>
    /// <param name="userName">Họ tên người mở lại.</param>
    /// <param name="reason">Lý do (bắt buộc).</param>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi phản hồi đã được mở lại, PO không còn Đã duyệt, thiếu lý do hoặc lý do quá dài.</exception>
    public void Reopen(PurchaseOrder order, Guid userId, string userName, string? reason, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (order.Id != PurchaseOrderId) throw new BusinessRuleException("Phản hồi không thuộc đơn này.");
        if (!IsCurrent) throw new BusinessRuleException("Phản hồi này đã được mở lại trước đó.");
        if (order.Status != ApprovalDocumentStatus.Approved)
            throw new BusinessRuleException($"Đơn {order.No} không còn ở trạng thái Đã duyệt – không mở lại phản hồi của NCC được.");
        var clean = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (clean is null) throw new BusinessRuleException("Nhập lý do mở lại (lý do được gửi cho NCC).");
        if (clean.Length > NoteMaxLength) throw new BusinessRuleException($"Lý do tối đa {NoteMaxLength} ký tự.");
        ReopenedAt = at;
        ReopenedBy = userId;
        ReopenedByName = userName;
        ReopenReason = clean;
    }

    /// <summary>
    /// Ghi phản hồi của NCC (P4-2): PO phải **Đã duyệt** (chưa nhận đủ, chưa hủy) và đặt cho đúng NCC; xác nhận cần ngày giao cam kết không
    /// trước ngày đặt hàng; từ chối bắt buộc lý do.
    /// </summary>
    /// <param name="order">PO.</param>
    /// <param name="supplierId">NCC của phiên Portal.</param>
    /// <param name="portalUserId">Tài khoản Portal.</param>
    /// <param name="email">Email tài khoản.</param>
    /// <param name="decision">Xác nhận hoặc từ chối.</param>
    /// <param name="promisedDate">Ngày giao cam kết (bắt buộc khi xác nhận; bỏ qua khi từ chối).</param>
    /// <param name="note">Ghi chú / lý do từ chối.</param>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <returns>Phản hồi mới.</returns>
    /// <exception cref="BusinessRuleException">Khi PO không ở trạng thái chờ NCC phản hồi, sai NCC, thiếu ngày giao / lý do, ghi chú quá dài.</exception>
    public static PurchaseOrderSupplierResponse Create(
        PurchaseOrder order, Guid supplierId, Guid portalUserId, string email, SupplierOrderDecision decision, DateOnly? promisedDate,
        string? note, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (order.SupplierId != supplierId) throw new BusinessRuleException("Đơn đặt hàng không thuộc nhà cung cấp này.");
        if (order.Status != ApprovalDocumentStatus.Approved)
            throw new BusinessRuleException($"Đơn {order.No} không còn chờ xác nhận (đã hủy hoặc đã nhận đủ hàng).");
        if (!Enum.IsDefined(decision)) throw new BusinessRuleException("Phản hồi phải là Xác nhận hoặc Từ chối.");
        var clean = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (clean is { Length: > NoteMaxLength }) throw new BusinessRuleException($"Ghi chú / lý do tối đa {NoteMaxLength} ký tự.");
        if (decision == SupplierOrderDecision.Rejected)
        {
            if (clean is null) throw new BusinessRuleException("Phải nhập lý do từ chối đơn để bên đặt hàng biết xử lý.");
            promisedDate = null;
        }
        else
        {
            if (promisedDate is null) throw new BusinessRuleException("Nhập ngày giao hàng cam kết.");
            if (promisedDate < order.Date)
                throw new BusinessRuleException($"Ngày giao cam kết không được trước ngày đặt hàng {order.Date:dd/MM/yyyy}.");
        }

        return new PurchaseOrderSupplierResponse
        {
            CompanyId = order.CompanyId,
            PurchaseOrderId = order.Id,
            SupplierId = supplierId,
            Decision = decision,
            PromisedDate = promisedDate,
            Note = clean,
            RespondedAt = at,
            PortalUserId = portalUserId,
            RespondedByEmail = email,
        };
    }
}
