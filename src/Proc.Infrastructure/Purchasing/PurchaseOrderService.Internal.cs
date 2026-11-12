using Microsoft.EntityFrameworkCore;
using Proc.Application.Purchasing;
using Proc.Domain.Notifications;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Notifications;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Pricing;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Mua nội bộ qua PO (lát X7, design.md mục 19.3, 19.4): PO đặt cho NCC nội bộ (NCC gắn một công ty thành viên). Giá bán nội bộ của công ty
/// bán chỉ là giá gợi ý – người lập nhập tay được (người dùng chốt 12/11/2026); công ty bán xác nhận / từ chối ở "Đơn bán nội bộ".
/// </summary>
public sealed partial class PurchaseOrderService
{
    /// <summary>Mã màn hình "Đơn bán nội bộ" – thông báo cho công ty bán mở màn hình này.</summary>
    internal const string InternalSaleScreen = "DBN";

    /// <summary>Công ty mà NCC đại diện (NCC nội bộ); null với NCC ngoài.</summary>
    private Task<Guid?> InternalSellerAsync(Guid supplierId, CancellationToken cancellationToken) =>
        _db.Suppliers.AsNoTracking().Where(s => s.Id == supplierId).Select(s => s.InternalCompanyId).SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Ghi giá bán nội bộ gợi ý cho từng dòng: giá theo ĐVT cơ sở × hệ số ĐVT của dòng, làm tròn 6 số (0,5 lên). Ví dụ giá 15.000/cái, dòng
    /// đặt theo thùng 12 cái → 180.000/thùng. Mã hàng theo NCC vẫn chép như PO thường.
    /// </summary>
    private async Task ApplyInternalPricesAsync(PurchaseOrder order, CancellationToken cancellationToken)
    {
        var itemIds = order.Lines.Select(l => l.ItemId).Distinct().ToList();
        var prices = await InternalPriceLookup.ResolveAsync(_db, order.InternalSellerCompanyId!.Value, order.CompanyId, order.Date, itemIds, cancellationToken);
        order.ApplyInternalPrices(line => prices.TryGetValue(line.ItemId, out var p)
            ? Math.Round(p.UnitPrice * line.UomFactor, 6, MidpointRounding.AwayFromZero)
            : null);
        var supplierCodes = await _db.SupplierItems.AsNoTracking()
            .Where(x => x.SupplierId == order.SupplierId && itemIds.Contains(x.ItemId) && x.SupplierItemCode != null)
            .ToDictionaryAsync(x => x.ItemId, x => x.SupplierItemCode, cancellationToken);
        order.ApplySupplierItemCodes(line => supplierCodes.GetValueOrDefault(line.ItemId));
    }

    /// <summary>
    /// Giá gợi ý cho giao diện PO khi chọn NCC nội bộ: mỗi mặt hàng một bậc theo ĐVT cơ sở từ SL 0, nguồn "Internal" (giao diện tự quy ĐVT như
    /// bảng giá NCC). NCC nội bộ là chính công ty đang làm việc thì không có giá (lưu sẽ báo lỗi).
    /// </summary>
    private async Task<IReadOnlyList<PoApprovedPriceDto>> InternalApprovedPricesAsync(
        Guid sellerCompanyId, DateOnly date, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        if (sellerCompanyId == companyId) return [];
        var prices = await InternalPriceLookup.ResolveAsync(_db, sellerCompanyId, companyId, date, itemIds, cancellationToken);
        if (prices.Count == 0) return [];
        var ids = prices.Keys.ToList();
        var baseUoms = await _db.Items.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.BaseUomId, cancellationToken);
        var seller = await _db.Companies.AsNoTracking().Where(c => c.Id == sellerCompanyId).Select(c => c.Code).SingleAsync(cancellationToken);
        return prices.Where(p => baseUoms.ContainsKey(p.Key))
            .Select(p => new PoApprovedPriceDto(
                p.Key, baseUoms[p.Key], 1m, 0m, p.Value.UnitPrice, 0m, p.Value.VatCode, PurchasePriceSource.Internal.ToString(), $"Giá nội bộ {seller}", null))
            .ToList();
    }

    /// <summary>
    /// PO mua nội bộ vừa duyệt xong: báo người có quyền xác nhận đơn bán nội bộ tại công ty bán (thông báo thuộc công ty bán, mở màn hình
    /// DBN). Gọi sau lệnh duyệt / gửi duyệt; PO chưa duyệt xong thì không làm gì.
    /// </summary>
    private async Task NotifyInternalSellerAsync(PurchaseOrder order, CancellationToken cancellationToken)
    {
        if (order.InternalSellerCompanyId is not { } sellerId || order.Status != ApprovalDocumentStatus.Approved) return;
        var publisher = new NotificationPublisher(_db, _scope, _clock);
        var receivers = await publisher.UsersWithPermissionAsync(sellerId, Permissions.Purchase.InternalSaleManage, null, null, cancellationToken);
        var buyer = await _db.Companies.AsNoTracking().Where(c => c.Id == order.CompanyId).Select(c => c.Code + " – " + c.Name).SingleAsync(cancellationToken);
        publisher.Add(sellerId, receivers, NotificationTypes.InternalSaleOrder, $"Đơn mua nội bộ {order.No} chờ xác nhận",
            $"{buyer} đặt mua theo đơn {order.No} (đã duyệt). Vào Mua hàng → Đơn bán nội bộ để xác nhận hoặc từ chối.",
            InternalSaleScreen, order.Id);
    }

    /// <summary>
    /// Trạng thái bên bán của PO mua nội bộ cho công ty mua: đọc quyết định của công ty bán theo đúng Id PO (bỏ filter công ty có chủ đích –
    /// bản ghi thuộc công ty bán). NotSent khi PO chưa duyệt.
    /// </summary>
    private async Task<InternalSaleStatusDto?> InternalSaleStatusAsync(PurchaseOrder order, CancellationToken cancellationToken)
    {
        if (order.InternalSellerCompanyId is not { } sellerId) return null;
        var seller = await _db.Companies.AsNoTracking().Where(c => c.Id == sellerId).Select(c => new { c.Code, c.Name }).SingleAsync(cancellationToken);
        var decision = await (
                from d in _db.InternalSaleConfirmations.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking()
                join u in _db.Users on d.CreatedBy equals u.Id into users
                from u in users.DefaultIfEmpty()
                where d.PurchaseOrderId == order.Id
                select new { d.Decision, d.Note, d.CreatedAt, By = u == null ? null : u.FullName })
            .SingleOrDefaultAsync(cancellationToken);
        if (decision is not null)
            return new InternalSaleStatusDto(seller.Code, seller.Name, decision.Decision.ToString(), decision.Note, decision.By, decision.CreatedAt);
        var sent = order.ApprovedAt is not null && order.Status is ApprovalDocumentStatus.Approved or ApprovalDocumentStatus.Closed;
        return new InternalSaleStatusDto(seller.Code, seller.Name, sent ? "Pending" : "NotSent", null, null, null);
    }
}
