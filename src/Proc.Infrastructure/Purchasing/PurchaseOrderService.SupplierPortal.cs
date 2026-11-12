using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Phần PO liên quan NCC trên Portal (lát P4a – design.md mục 19.5, P4-4, P4-5): thư "Đơn đặt hàng mới" khi duyệt xong, phản hồi của NCC hiện
/// trên PO, Phòng CUVT mở lại phản hồi (yêu cầu người dùng 12/11/2026).
/// </summary>
public sealed partial class PurchaseOrderService
{
    /// <inheritdoc />
    public async Task<PurchaseOrderDto> ReopenSupplierResponseAsync(
        Guid id, ReopenSupplierResponseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _scope.EnsurePermission(Permissions.Purchase.PoCreate);
        var order = await LoadAsync(id, cancellationToken);
        var current = await _db.PurchaseOrderSupplierResponses
                .SingleOrDefaultAsync(r => r.PurchaseOrderId == order.Id && r.ReopenedAt == null, cancellationToken)
            ?? throw new BusinessRuleException($"NCC chưa phản hồi đơn {order.No} trên Portal – không có gì để mở lại.");
        var userId = _scope.UserId ?? throw new ForbiddenAccessException("Cần đăng nhập.");
        var name = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken);
        current.Reopen(order, userId, string.IsNullOrWhiteSpace(name) ? "(không rõ)" : name, request.Reason, _clock.UtcNow);
        // Lý do mở lại do Phòng CUVT viết để gửi NCC (khác lý do hủy là ghi chú nội bộ) – ghi vào thư.
        await _portalEmails.QueuePurchaseOrderAsync(order, SupplierPortalEmailKind.PurchaseOrderReopened, current.ReopenReason, cancellationToken);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException($"Phản hồi của NCC với đơn {order.No} vừa được người khác mở lại – tải lại phiếu.");
        }

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// PO vừa duyệt xong: soạn thư "Đơn đặt hàng mới" cho tài khoản Portal đang dùng của NCC (P4-5). Gọi sau lệnh duyệt / gửi duyệt; PO chưa duyệt
    /// xong, PO mua nội bộ thì không làm gì.
    /// </summary>
    private async Task NotifySupplierIssuedAsync(PurchaseOrder order, CancellationToken cancellationToken)
    {
        if (order.Status != ApprovalDocumentStatus.Approved) return;
        await _portalEmails.QueuePurchaseOrderAsync(order, SupplierPortalEmailKind.PurchaseOrderIssued, null, cancellationToken);
    }

    /// <summary>
    /// Phản hồi của NCC hiện trên PO (P4-4); null với PO mua nội bộ (bên bán xác nhận ở Đơn bán nội bộ). Bảng phản hồi có CompanyId = công ty
    /// đặt hàng nên đi qua bộ lọc công ty như PO.
    /// </summary>
    /// <param name="order">PO.</param>
    /// <param name="canEdit">Người dùng có quyền lập PO và phiếu không bị giới hạn chỉ xem.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    private async Task<SupplierOrderResponseDto?> SupplierResponseAsync(PurchaseOrder order, bool canEdit, CancellationToken cancellationToken)
    {
        if (order.InternalSellerCompanyId is not null) return null;
        var rows = await (
                from r in _db.PurchaseOrderSupplierResponses.AsNoTracking()
                join pu in _db.SupplierPortalUsers.AsNoTracking() on r.PortalUserId equals pu.Id into users
                from pu in users.DefaultIfEmpty()
                where r.PurchaseOrderId == order.Id
                orderby r.RespondedAt descending
                select new { r, Name = pu == null ? null : pu.FullName })
            .ToListAsync(cancellationToken);
        var activeUsers = await _db.SupplierPortalUsers.AsNoTracking()
            .CountAsync(u => u.SupplierId == order.SupplierId && u.Status == SupplierPortalUserStatus.Active, cancellationToken);
        var current = rows.FirstOrDefault(x => x.r.ReopenedAt is null)?.r;
        var issued = order.ApprovedAt is not null && order.Status is ApprovalDocumentStatus.Approved or ApprovalDocumentStatus.Closed
            or ApprovalDocumentStatus.Cancelled;
        var status = current?.Decision.ToString() ?? (issued ? "Pending" : "NotSent");
        return new SupplierOrderResponseDto(
            status, activeUsers, canEdit && current is not null && order.Status == ApprovalDocumentStatus.Approved,
            rows.Select(x => new SupplierOrderResponseItemDto(
                x.r.Decision.ToString(), x.r.PromisedDate, x.r.Note, x.r.RespondedAt, x.r.RespondedByEmail, x.Name, x.r.ReopenedAt,
                x.r.ReopenedByName, x.r.ReopenReason)).ToList());
    }
}
