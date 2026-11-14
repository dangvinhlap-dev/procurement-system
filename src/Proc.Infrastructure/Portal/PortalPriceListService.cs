using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Portal;
using Proc.Domain.Pricing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Portal;

/// <summary>
/// Bảng giá trên Portal (lát P7a – design.md 19.5, P7-1…P7-4). Phiên Portal không có công ty nên truy vấn bỏ bộ lọc toàn cục và tự lọc theo NCC
/// của phiên (P1b-4). NCC chỉ thấy bảng giá đã duyệt của mình (hiệu lực hoặc đã ngừng) – bảng nháp, chờ duyệt, đã hủy là việc nội bộ, giá còn
/// đổi; không lộ ghi chú nội bộ, người lập / duyệt, ý kiến duyệt, file đính kèm (P7-4).
/// </summary>
public sealed class PortalPriceListService : IPortalPriceListService
{
    private readonly ProcDbContext _db;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="clock">Đồng hồ – ngày hôm nay (giờ Việt Nam) để tính tình trạng.</param>
    public PortalPriceListService(ProcDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PortalPriceListDto>> ListAsync(PortalSession session, CancellationToken cancellationToken)
    {
        var lists = await Visible(session.SupplierId)
            .Select(l => new { List = l, LineCount = l.Lines.Count })
            .ToListAsync(cancellationToken);
        var companies = await CompanyNamesAsync(lists.Select(l => l.List.CompanyId), cancellationToken);
        var today = _clock.TodayInVietnam();
        return lists
            .Select(l => Header(l.List, l.LineCount, companies, today))
            .OrderBy(l => Array.IndexOf(StatusOrder, l.Status))
            .ThenByDescending(l => l.ValidFrom)
            .ThenByDescending(l => l.No, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<PortalPriceListDetailDto> GetAsync(PortalSession session, Guid id, CancellationToken cancellationToken)
    {
        var list = await Visible(session.SupplierId).Include(l => l.Lines).SingleOrDefaultAsync(l => l.Id == id, cancellationToken)
                   ?? throw new NotFoundException("Không tìm thấy bảng giá.");
        var itemIds = list.Lines.Select(l => l.ItemId).Distinct().ToList();
        var uomIds = list.Lines.Select(l => l.UomId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().IgnoreQueryFilters().Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => new { i.Code, i.Name, i.VatCode }, cancellationToken);
        var uoms = await _db.Uoms.AsNoTracking().IgnoreQueryFilters().Where(u => uomIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        // Mã hàng NCC lấy từ danh mục NCC – mặt hàng (một NCC – một mặt hàng một dòng); chưa khai thì để trống.
        var supplierCodes = (await _db.SupplierItems.AsNoTracking().IgnoreQueryFilters()
                .Where(s => s.SupplierId == session.SupplierId && itemIds.Contains(s.ItemId) && s.SupplierItemCode != null)
                .Select(s => new { s.ItemId, s.SupplierItemCode })
                .ToListAsync(cancellationToken))
            .GroupBy(s => s.ItemId)
            .ToDictionary(g => g.Key, g => g.First().SupplierItemCode);
        var companies = await CompanyNamesAsync([list.CompanyId], cancellationToken);
        var lines = list.Lines.OrderBy(l => l.LineNo)
            .Select(l =>
            {
                var item = items.GetValueOrDefault(l.ItemId);
                return new PortalPriceListLineDto(
                    l.LineNo, item?.Code ?? "", supplierCodes.GetValueOrDefault(l.ItemId), item?.Name ?? "", uoms.GetValueOrDefault(l.UomId) ?? "",
                    l.MinQty, l.UnitPrice, l.DiscountPct,
                    // Đơn giá sau CK theo ĐVT dòng = đơn giá × (100 − %CK) / 100 (hệ số 1), ví dụ 50.000 CK 4% → 48.000 – design.md 8.2.
                    PricingMath.BaseNetPrice(l.UnitPrice, l.DiscountPct, 1m),
                    l.VatCode ?? item?.VatCode);
            })
            .ToList();
        return new PortalPriceListDetailDto(Header(list, list.Lines.Count, companies, _clock.TodayInVietnam()), lines);
    }

    /// <summary>Thứ tự nhóm tình trạng trên danh sách: bảng đang dùng được lên đầu.</summary>
    private static readonly string[] StatusOrder = ["Current", "Upcoming", "Expired", "Inactive"];

    /// <summary>Bảng giá NCC được thấy (P7-2): của chính NCC, đã duyệt – đang hiệu lực hoặc đã ngừng.</summary>
    private IQueryable<SupplierPriceList> Visible(Guid supplierId) =>
        _db.SupplierPriceLists.AsNoTracking().IgnoreQueryFilters()
            .Where(l => l.SupplierId == supplierId && (l.Status == PriceListStatus.Active || l.Status == PriceListStatus.Inactive));

    private Task<Dictionary<Guid, string>> CompanyNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        return _db.Companies.AsNoTracking().IgnoreQueryFilters().Where(c => list.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
    }

    /// <summary>
    /// Đầu bảng giá kèm tình trạng (P7-3): ngừng → "Inactive"; hiệu lực mà hôm nay trước ngày bắt đầu → "Upcoming", sau ngày hết hạn →
    /// "Expired", còn lại "Current". Ví dụ hiệu lực 01/11–30/11, hôm nay 14/11 → đang áp dụng; hôm nay 01/12 → hết hạn.
    /// </summary>
    private static PortalPriceListDto Header(SupplierPriceList l, int lineCount, Dictionary<Guid, string> companies, DateOnly today)
    {
        var status = l.Status == PriceListStatus.Inactive ? "Inactive"
            : today < l.ValidFrom ? "Upcoming"
            : l.ValidTo is { } to && today > to ? "Expired"
            : "Current";
        var approved = l.ActivatedAt is { } at ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, VietnamTime.Zone).DateTime) : (DateOnly?)null;
        return new PortalPriceListDto(
            l.Id, l.No, l.Title, companies.GetValueOrDefault(l.CompanyId) ?? "", l.IsGroupWide, l.ValidFrom, l.ValidTo, l.DeliveryTerms, approved,
            lineCount, status);
    }
}
