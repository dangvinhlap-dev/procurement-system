using Microsoft.EntityFrameworkCore;
using Proc.Application.Analysis;
using Proc.Application.Common;
using Proc.Domain.Analysis;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Analysis;

/// <summary>
/// Phân tích giá hợp nhất trên dữ liệu mua lịch sử (xem <see cref="IPurchaseAnalysisService"/>, design.md mục 18.3).
/// SQL gom sẵn theo pháp nhân × mặt hàng × ĐVT × NCC × ngày mua (ít hơn nhiều so với số dòng), phần còn lại tính trong bộ nhớ: ĐVT phải so
/// không dấu và khóa NCC chưa ánh xạ (MST hoặc tên không dấu) không viết được bằng SQL đơn giản.
/// Xem toàn tập đoàn (đã kiểm quyền) thì bỏ filter công ty, giữ filter nhóm hàng qua phép join với Items – như PCMP (design.md mục 8.5).
/// </summary>
public sealed class PurchaseAnalysisService : IPurchaseAnalysisService
{
    /// <summary>Tiền tệ duy nhất được phân tích – chưa quy đổi ngoại tệ (design.md mục 18.3).</summary>
    private const string Vnd = "VND";

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>
    /// Khởi tạo dịch vụ.
    /// </summary>
    /// <param name="db">CSDL.</param>
    /// <param name="scope">Phạm vi người dùng (quyền, công ty đang làm việc).</param>
    /// <param name="clock">Đồng hồ – ngày hôm nay theo giờ Việt Nam.</param>
    public PurchaseAnalysisService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<PurchaseAnalysisDto> AnalyzeAsync(PurchaseAnalysisQuery query, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.Analysis.PurchaseHistoryView);
        _scope.RequireCompany();
        if (query.AllCompanies) _scope.EnsurePermission(Permissions.Analysis.PurchaseHistoryViewAllCompanies);
        var discountPct = query.TargetDiscountPct ?? PurchaseAnalysis.DefaultTargetDiscountPct;
        PurchaseAnalysis.EnsureDiscountPct(discountPct);
        var to = query.To ?? _clock.TodayInVietnam();
        // Mặc định 12 tháng tính cả hai đầu: đến 31/10/2026 thì từ 01/11/2025.
        var from = query.From ?? to.AddMonths(-12).AddDays(1);
        if (from > to) throw new BusinessRuleException("Từ ngày phải trước hoặc bằng đến ngày.");

        // IgnoreQueryFilters chỉ bỏ filter công ty (filter có tên), filter nhóm hàng trên Items vẫn áp ở phép join bên dưới.
        var records = (query.AllCompanies
                ? _db.PurchaseHistoryRecords.IgnoreQueryFilters([ProcDbContext.CompanyFilter])
                : _db.PurchaseHistoryRecords)
            .AsNoTracking()
            .Where(r => r.PurchaseDate >= from && r.PurchaseDate <= to);

        var excluded = await records
            .Where(r => r.ItemId == null || r.Currency != Vnd || r.UnitPrice == 0)
            .GroupBy(r => new { Unmapped = r.ItemId == null, Foreign = r.Currency != Vnd })
            .Select(g => new { g.Key.Unmapped, g.Key.Foreign, Count = g.Count(), Amount = g.Sum(r => r.Amount) })
            .ToListAsync(cancellationToken);

        var slices = await (
                from r in records
                where r.ItemId != null && r.Currency == Vnd && r.UnitPrice > 0
                join i in _db.Items on r.ItemId equals i.Id
                group r by new { r.CompanyId, r.ItemId, r.Uom, r.SupplierId, r.SupplierTaxCode, r.SupplierName, r.PurchaseDate }
                into g
                select new Slice(
                    g.Key.CompanyId, g.Key.ItemId!.Value, g.Key.Uom, g.Key.SupplierId, g.Key.SupplierTaxCode, g.Key.SupplierName,
                    g.Key.PurchaseDate, g.Sum(r => r.Quantity), g.Sum(r => r.Amount), g.Min(r => r.UnitPrice), g.Max(r => r.UnitPrice),
                    g.Count()))
            .ToListAsync(cancellationToken);

        var itemIds = slices.Select(s => s.ItemId).Distinct().ToList();
        var companyIds = slices.Select(s => s.CompanyId).Distinct().ToList();
        var supplierIds = slices.Where(s => s.SupplierId != null).Select(s => s.SupplierId!.Value).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => (i.Code, i.Name), cancellationToken);
        var companies = await _db.Companies.AsNoTracking().Where(c => companyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => (c.Code, c.Name), cancellationToken);
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => $"{s.Code} – {s.Name}", cancellationToken);

        var rows = slices
            .GroupBy(s => (s.ItemId, Uom: PurchaseAnalysis.UomKey(s.Uom)))
            .Select(g => BuildRow(g.Key.ItemId, g.ToList(), discountPct, items[g.Key.ItemId], companies, suppliers))
            .OrderByDescending(r => r.Savings).ThenByDescending(r => r.Amount).ThenBy(r => r.ItemCode, StringComparer.Ordinal)
            .ToList();

        var amount = rows.Sum(r => r.Amount);
        var savings = rows.Sum(r => r.Savings);
        var unmapped = excluded.Where(e => e.Unmapped).ToList();
        return new PurchaseAnalysisDto(
            from, to, discountPct, query.AllCompanies, companyIds.Count, amount, savings,
            amount == 0 ? 0 : Math.Round(savings / amount * 100m, 2, MidpointRounding.AwayFromZero),
            unmapped.Sum(e => e.Count),
            unmapped.Where(e => !e.Foreign).Sum(e => e.Amount),
            excluded.Where(e => !e.Unmapped && e.Foreign).Sum(e => e.Count),
            // Nhóm "đã gán, VND" trong excluded chỉ có thể là đơn giá 0 (điều kiện Where ở trên).
            excluded.Where(e => !e.Unmapped && !e.Foreign).Sum(e => e.Count),
            rows);
    }

    /// <summary>Gom các lát của một mặt hàng × ĐVT thành số liệu từng pháp nhân, hợp nhất, chọn NCC giá tốt nhất.</summary>
    private static PurchaseAnalysisRowDto BuildRow(
        Guid itemId, IReadOnlyList<Slice> slices, decimal discountPct, (string Code, string Name) item,
        IReadOnlyDictionary<Guid, (string Code, string Name)> companies, IReadOnlyDictionary<Guid, string> suppliers)
    {
        var figures = slices.GroupBy(s => s.CompanyId).Select(g =>
        {
            var lastDate = g.Max(s => s.Date);
            var last = g.Where(s => s.Date == lastDate).ToList();
            return new CompanyPurchaseFigures(
                g.Key, g.Sum(s => s.Quantity), g.Sum(s => s.Amount), g.Min(s => s.MinPrice), g.Max(s => s.MaxPrice), g.Sum(s => s.Count),
                g.Select(SupplierKey).Distinct().Count(), lastDate, last.Sum(s => s.Quantity), last.Sum(s => s.Amount));
        }).ToList();
        var result = PurchaseAnalysis.Consolidate(figures, discountPct);

        // NCC giá tốt nhất: bình quân gia quyền thấp nhất trong kỳ trên mọi pháp nhân; bằng giá thì NCC bán nhiều hơn.
        var best = slices.GroupBy(SupplierKey)
            .Select(g => new
            {
                Slice = g.First(),
                Price = PurchaseAnalysis.AveragePrice(g.Sum(s => s.Amount), g.Sum(s => s.Quantity)),
                Quantity = g.Sum(s => s.Quantity),
            })
            .OrderBy(s => s.Price).ThenByDescending(s => s.Quantity)
            .First();
        var bestName = best.Slice.SupplierId is { } id && suppliers.TryGetValue(id, out var name) ? name : best.Slice.SupplierName;
        // ĐVT hiển thị: cách ghi gặp nhiều nhất (các cách ghi chỉ khác dấu/hoa thường đã gom làm một).
        var uom = slices.GroupBy(s => s.Uom.Trim()).OrderByDescending(g => g.Sum(s => s.Count)).ThenBy(g => g.Key, StringComparer.Ordinal).First().Key;

        var companyRows = result.Companies.Select(c => new PurchaseAnalysisCompanyDto(
                c.Figures.CompanyId, companies[c.Figures.CompanyId].Code, companies[c.Figures.CompanyId].Name, c.Figures.Quantity,
                c.Figures.Amount, c.AvgPrice, c.Figures.MinPrice, c.Figures.MaxPrice, c.LastPrice, c.Figures.LastDate, c.Figures.PurchaseCount,
                c.Figures.SupplierCount, c.Savings))
            .ToList();
        return new PurchaseAnalysisRowDto(
            itemId, item.Code, item.Name, uom, companyRows.Count, result.Quantity, result.Amount, result.AvgPrice, result.MinPrice,
            result.MaxPrice, slices.Max(s => s.Date), result.PurchaseCount, slices.Select(SupplierKey).Distinct().Count(), result.PriceGapPct,
            result.TargetPrice, result.Savings, result.SavingsPct, bestName, best.Price, companyRows);
    }

    /// <summary>Khóa NCC: Id khi đã gán NCC trong danh mục, không thì khóa NCC cũ (MST hoặc tên không dấu – design.md mục 18.2).</summary>
    private static string SupplierKey(Slice s) =>
        s.SupplierId?.ToString() ?? PurchaseHistoryKeys.Supplier(s.SupplierTaxCode, s.SupplierName);

    /// <summary>Số liệu gom sẵn ở SQL: một pháp nhân, mặt hàng, ĐVT (cách ghi gốc), NCC, ngày mua.</summary>
    private sealed record Slice(
        Guid CompanyId, Guid ItemId, string Uom, Guid? SupplierId, string? SupplierTaxCode, string SupplierName, DateOnly Date,
        decimal Quantity, decimal Amount, decimal MinPrice, decimal MaxPrice, int Count);
}
