using Microsoft.EntityFrameworkCore;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Pricing;

/// <summary>Giá bán nội bộ đang áp dụng cho một mặt hàng: giá chưa VAT theo ĐVT cơ sở và thuế suất.</summary>
/// <param name="UnitPrice">Giá theo ĐVT cơ sở – numeric(19,6).</param>
/// <param name="VatCode">Thuế suất của dòng giá, trống thì theo mặt hàng.</param>
internal sealed record InternalPriceQuote(decimal UnitPrice, string? VatCode);

/// <summary>
/// Tra bảng giá nội bộ của <b>công ty bán</b> cho một công ty mua vào một ngày (design.md mục 6.6; lát X7 mục 19.3). Dùng chung cho phiếu
/// xuất bán nội bộ cũ (công ty bán đang làm việc) và PO mua nội bộ (công ty mua đang làm việc). Bảng giá thuộc công ty bán nên khi công ty mua
/// tra thì bỏ filter công ty – chỉ đúng công ty bán đã ghi trên PO và chỉ giá dành cho công ty mua này hoặc cho mọi công ty (CLAUDE.md mục 4:
/// ngoại lệ đọc theo liên kết, như cặp ICS–ICR). Filter nhóm hàng vẫn áp qua phép join mặt hàng.
/// </summary>
internal static class InternalPriceLookup
{
    /// <summary>Giá áp dụng theo mặt hàng: giá riêng cho công ty mua thắng giá chung; mặt hàng chưa có giá thì không có trong kết quả.</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="sellerCompanyId">Công ty bán (chủ bảng giá).</param>
    /// <param name="buyerCompanyId">Công ty mua.</param>
    /// <param name="date">Ngày chứng từ.</param>
    /// <param name="itemIds">Mặt hàng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Giá theo Id mặt hàng.</returns>
    public static async Task<Dictionary<Guid, InternalPriceQuote>> ResolveAsync(
        ProcDbContext db, Guid sellerCompanyId, Guid buyerCompanyId, DateOnly date, IReadOnlyCollection<Guid> itemIds,
        CancellationToken cancellationToken)
    {
        if (itemIds.Count == 0) return [];
        var candidates = await (
                from p in db.InternalPrices.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking()
                join i in db.Items on p.ItemId equals i.Id
                where p.CompanyId == sellerCompanyId && itemIds.Contains(p.ItemId)
                      && (p.BuyerCompanyId == null || p.BuyerCompanyId == buyerCompanyId)
                      && p.ValidFrom <= date && (p.ValidTo == null || p.ValidTo >= date)
                select new { p.ItemId, p.BuyerCompanyId, p.UnitPrice, VatCode = p.VatCode ?? i.VatCode, p.ValidFrom })
            .ToListAsync(cancellationToken);
        // Cùng loại (riêng / chung) không chồng hiệu lực nên mỗi loại tối đa một dòng; giá riêng thắng.
        return candidates
            .GroupBy(c => c.ItemId)
            .Select(g => g.OrderByDescending(c => c.BuyerCompanyId.HasValue).ThenByDescending(c => c.ValidFrom).First())
            .ToDictionary(c => c.ItemId, c => new InternalPriceQuote(c.UnitPrice, c.VatCode));
    }
}
