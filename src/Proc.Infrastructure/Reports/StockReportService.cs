using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Reports;
using Proc.Domain.Common;
using Proc.Domain.Inventory;
using Proc.Domain.Organization;
using Proc.Infrastructure.MasterData;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Reports;

/// <summary>
/// Báo cáo hàng chậm luân chuyển, sắp hết hạn, tồn tập đoàn (design.md mục 11, 11.2). Hai báo cáo đầu đi qua global query filter như
/// màn hình tồn kho (công ty đang làm việc, kho và nhóm hàng được phép); tồn tập đoàn bỏ filter công ty và kho ở đúng một truy vấn, sau khi
/// kiểm quyền <c>Report.Stock.ViewAllCompanies</c>, và chỉ trả số lượng.
/// </summary>
public sealed class StockReportService : IStockReportService
{
    /// <summary>Số ngày lớn nhất của tham số báo cáo (10 năm).</summary>
    public const int MaxDays = 3650;

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">Ngữ cảnh CSDL (đã gắn phạm vi người dùng).</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    public StockReportService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<SlowMovingResult> GetSlowMovingAsync(SlowMovingQuery query, CancellationToken cancellationToken)
    {
        if (query.Days is < 1 or > MaxDays) throw new BusinessRuleException($"Số ngày không xuất phải từ 1 đến {MaxDays}.");
        _scope.RequireCompany();
        var today = _clock.TodayInVietnam();

        var onHand = await OnHandAsync(query.WarehouseId, query.CategoryId, cancellationToken);
        var stock = onHand
            .GroupBy(s => (s.WarehouseId, s.ItemId))
            .Select(g => (g.Key, Qty: g.Sum(s => s.Qty)))
            .Where(s => s.Qty > 0)
            .ToList();
        var itemIds = stock.Select(s => s.Key.ItemId).Distinct().ToList();

        // Ngày nhập đầu, nhập cuối, xuất cuối và giá trị theo sổ kho của đúng kho × hàng. Chuyển kho đi, trả NCC, bán nội bộ đều là
        // xuất (Direction = −1): hàng đã rời kho nên kho này không còn "nằm im" với mặt hàng đó.
        var ledger = _db.StockLedger.AsNoTracking().Where(l => itemIds.Contains(l.ItemId));
        if (query.WarehouseId is { } warehouseId) ledger = ledger.Where(l => l.WarehouseId == warehouseId);
        var moves = (await ledger
                .GroupBy(l => new { l.WarehouseId, l.ItemId })
                .Select(g => new
                {
                    g.Key.WarehouseId,
                    g.Key.ItemId,
                    FirstIn = g.Where(l => l.Direction > 0).Min(l => (DateOnly?)l.PostingDate),
                    LastIn = g.Where(l => l.Direction > 0).Max(l => (DateOnly?)l.PostingDate),
                    LastOut = g.Where(l => l.Direction < 0).Max(l => (DateOnly?)l.PostingDate),
                    Amount = g.Sum(l => l.Direction * l.Amount),
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(m => (m.WarehouseId, m.ItemId));

        var names = await NamesAsync(stock.Select(s => s.Key.WarehouseId), itemIds, cancellationToken);
        var rows = new List<SlowMovingRowDto>();
        foreach (var (key, qty) in stock)
        {
            var move = moves.GetValueOrDefault(key);
            // Chưa xuất lần nào thì tính từ ngày nhập đầu tiên: hàng nằm kho từ hôm đó. Ví dụ nhập 05/01/2025, chưa xuất,
            // hôm nay 31/10/2026 → 664 ngày.
            var since = move?.LastOut ?? move?.FirstIn;
            if (since is null) continue;
            var idle = today.DayNumber - since.Value.DayNumber;
            if (idle < query.Days) continue;
            var (w, i) = (names.Warehouses[key.WarehouseId], names.Items[key.ItemId]);
            rows.Add(new SlowMovingRowDto(
                key.WarehouseId, w.Code, w.Name, key.ItemId, i.Code, i.Name, i.CategoryCode, i.CategoryName, i.UomCode,
                qty, move!.Amount, move.FirstIn, move.LastIn, move.LastOut, idle));
        }

        return new SlowMovingResult(today, query.Days,
            rows.OrderByDescending(r => r.IdleDays).ThenBy(r => r.WarehouseCode).ThenBy(r => r.ItemCode).ToList());
    }

    /// <inheritdoc />
    public async Task<ExpiringResult> GetExpiringAsync(ExpiringQuery query, CancellationToken cancellationToken)
    {
        if (query.Days is < 0 or > MaxDays) throw new BusinessRuleException($"Số ngày tới phải từ 0 đến {MaxDays}.");
        _scope.RequireCompany();
        var today = _clock.TodayInVietnam();
        var limit = today.AddDays(query.Days);

        var lots = (await OnHandAsync(query.WarehouseId, query.CategoryId, cancellationToken))
            .Where(s => s.Qty > 0 && s.LotNo != null)
            .ToList();
        var itemIds = lots.Select(s => s.ItemId).Distinct().ToList();

        // Hạn dùng ghi trên dòng phiếu (nhập mua, nhập nội bộ, chuyển kho chép theo lô) đã ghi sổ của công ty, theo mặt hàng × lô.
        // Cùng một lô mà các phiếu ghi hạn khác nhau (nhập nhầm) thì lấy hạn sớm nhất – báo sớm còn hơn để hàng hết hạn trong kho.
        var expiries = (await (
                    from l in _db.InventoryDocumentLines.AsNoTracking()
                    join d in _db.InventoryDocuments on l.DocumentId equals d.Id
                    where d.Status == DocumentStatus.Posted && l.ExpiryDate != null && l.LotNo != null && itemIds.Contains(l.ItemId)
                    group l by new { l.ItemId, l.LotNo } into g
                    select new { g.Key.ItemId, g.Key.LotNo, Expiry = g.Min(l => l.ExpiryDate) })
                .ToListAsync(cancellationToken))
            .ToDictionary(e => (e.ItemId, e.LotNo!), e => e.Expiry!.Value);

        var due = lots
            .Select(s => (Stock: s, Expiry: expiries.TryGetValue((s.ItemId, s.LotNo!), out var e) ? e : (DateOnly?)null))
            .Where(x => x.Expiry is { } e && e <= limit)
            .ToList();

        var ledger = _db.StockLedger.AsNoTracking().Where(l => itemIds.Contains(l.ItemId) && l.LotNo != null);
        if (query.WarehouseId is { } warehouseId) ledger = ledger.Where(l => l.WarehouseId == warehouseId);
        var amounts = due.Count == 0
            ? []
            : (await ledger
                    .GroupBy(l => new { l.WarehouseId, l.ItemId, l.LotNo })
                    .Select(g => new { g.Key.WarehouseId, g.Key.ItemId, g.Key.LotNo, Amount = g.Sum(l => l.Direction * l.Amount) })
                    .ToListAsync(cancellationToken))
                .ToDictionary(a => (a.WarehouseId, a.ItemId, a.LotNo!), a => a.Amount);

        var names = await NamesAsync(due.Select(x => x.Stock.WarehouseId), due.Select(x => x.Stock.ItemId), cancellationToken);
        var rows = due.Select(x =>
            {
                var (s, expiry) = (x.Stock, x.Expiry!.Value);
                var (w, i) = (names.Warehouses[s.WarehouseId], names.Items[s.ItemId]);
                return new ExpiringRowDto(
                    s.WarehouseId, w.Code, w.Name, s.ItemId, i.Code, i.Name, i.CategoryCode, i.UomCode, s.LotNo!, expiry,
                    expiry.DayNumber - today.DayNumber, s.Qty, amounts.GetValueOrDefault((s.WarehouseId, s.ItemId, s.LotNo!)));
            })
            .OrderBy(r => r.ExpiryDate).ThenBy(r => r.WarehouseCode).ThenBy(r => r.ItemCode).ThenBy(r => r.LotNo)
            .ToList();
        return new ExpiringResult(today, query.Days, rows);
    }

    /// <inheritdoc />
    public async Task<GroupStockResult> GetGroupStockAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        _scope.EnsurePermission(Permissions.Report.StockViewAllCompanies);
        // Mặt hàng vẫn qua filter nhóm hàng: người chỉ được nhóm Vật tư không tra được tồn tập đoàn của nhóm khác.
        var item = await (
                from i in _db.Items.AsNoTracking()
                join u in _db.Uoms on i.BaseUomId equals u.Id
                where i.Id == itemId
                select new { i.Id, i.Code, i.Name, Uom = u.Code, i.MinStock })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).");

        // Bỏ filter công ty và kho ở truy vấn này (đã kiểm quyền xem toàn tập đoàn – design.md mục 9.2, 11.2). Phạm vi kho của người
        // dùng chỉ khai báo theo từng công ty nên không áp được sang công ty khác. IgnoreQueryFilters áp cho cả câu truy vấn, vì vậy
        // truy vấn chỉ đọc số lượng của đúng một mặt hàng đã kiểm tra ở trên – không đọc giá trị, không đọc sổ kho.
        var stock = await (
                from s in _db.StockOnHand.IgnoreQueryFilters([ProcDbContext.CompanyFilter, ProcDbContext.WarehouseFilter]).AsNoTracking()
                join w in _db.Warehouses on s.WarehouseId equals w.Id
                where s.ItemId == item.Id && s.Qty != 0
                group s by new { s.CompanyId, w.Type } into g
                select new { g.Key.CompanyId, g.Key.Type, Qty = g.Sum(s => s.Qty), Reserved = g.Sum(s => s.ReservedQty) })
            .ToListAsync(cancellationToken);

        var companyIds = stock.Select(s => s.CompanyId).Distinct().ToList();
        var companies = await _db.Companies.AsNoTracking().Where(c => companyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new { c.Code, c.Name }, cancellationToken);
        var rows = stock
            .GroupBy(s => s.CompanyId)
            .Where(g => companies.ContainsKey(g.Key))
            .Select(g =>
            {
                var main = g.Where(s => s.Type == WarehouseType.Main).ToList();
                var mainQty = main.Sum(s => s.Qty);
                var subQty = g.Where(s => s.Type != WarehouseType.Main).Sum(s => s.Qty);
                return new GroupStockRowDto(
                    g.Key, companies[g.Key].Code, companies[g.Key].Name, g.Key == companyId,
                    mainQty, mainQty - main.Sum(s => s.Reserved), subQty, mainQty + subQty);
            })
            .OrderBy(r => r.CompanyCode)
            .ToList();
        return new GroupStockResult(item.Id, item.Code, item.Name, item.Uom, item.MinStock, rows);
    }

    /// <summary>Tồn hiện tại (qua global query filter) theo kho, nhóm hàng (gồm nhóm con).</summary>
    private async Task<List<StockOnHand>> OnHandAsync(Guid? warehouseId, Guid? categoryId, CancellationToken cancellationToken)
    {
        var onHand = _db.StockOnHand.AsNoTracking().Where(s => s.Qty != 0);
        if (warehouseId is { } w) onHand = onHand.Where(s => s.WarehouseId == w);
        if (categoryId is { } c)
        {
            // Chọn nhóm cha thì gồm cả hàng của mọi nhóm con (như màn hình tồn kho).
            var parents = await _db.ItemCategories.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.ParentId, cancellationToken);
            var ids = parents.Keys.Where(id => ItemAdministration.IsUnder(id, c, parents)).ToList();
            onHand = onHand.Where(s => _db.Items.Any(i => i.Id == s.ItemId && ids.Contains(i.CategoryId)));
        }

        return await onHand.ToListAsync(cancellationToken);
    }

    private sealed record WarehouseName(string Code, string Name);

    private sealed record ItemName(string Code, string Name, string CategoryCode, string CategoryName, string UomCode);

    /// <summary>Tên kho và mặt hàng cho các dòng báo cáo.</summary>
    private async Task<(Dictionary<Guid, WarehouseName> Warehouses, Dictionary<Guid, ItemName> Items)> NamesAsync(
        IEnumerable<Guid> warehouseIds, IEnumerable<Guid> itemIds, CancellationToken cancellationToken)
    {
        var wIds = warehouseIds.Distinct().ToList();
        var iIds = itemIds.Distinct().ToList();
        var warehouses = await _db.Warehouses.AsNoTracking().Where(w => wIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => new WarehouseName(w.Code, w.Name), cancellationToken);
        var items = await (
                from i in _db.Items.AsNoTracking()
                join c in _db.ItemCategories on i.CategoryId equals c.Id
                join u in _db.Uoms on i.BaseUomId equals u.Id
                where iIds.Contains(i.Id)
                select new { i.Id, i.Code, i.Name, CategoryCode = c.Code, CategoryName = c.Name, UomCode = u.Code })
            .ToDictionaryAsync(i => i.Id, i => new ItemName(i.Code, i.Name, i.CategoryCode, i.CategoryName, i.UomCode), cancellationToken);
        return (warehouses, items);
    }
}
