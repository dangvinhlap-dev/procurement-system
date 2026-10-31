using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Domain.Common;
using Proc.Domain.Inventory;
using Proc.Infrastructure.MasterData;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Inventory;

/// <summary>
/// Tồn kho hiện tại và thẻ kho (design.md mục 11, 12). Mọi truy vấn đi qua global query filter của sổ kho, tồn kho: chỉ công ty
/// đang làm việc, kho được phép, nhóm hàng được phép (design.md mục 9.2) – không chỗ nào bỏ qua bộ lọc.
/// </summary>
public sealed class StockQueryService : IStockQueryService
{
    /// <summary>Số dòng tồn tối đa mỗi lần xem.</summary>
    public const int OnHandLimit = 5000;

    /// <summary>Khoảng ngày tối đa của thẻ kho (hai năm) – tránh quét cả sổ kho một lần.</summary>
    public const int StockCardMaxDays = 731;

    private readonly ProcDbContext _db;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">Ngữ cảnh CSDL (đã gắn phạm vi người dùng).</param>
    public StockQueryService(ProcDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<StockOnHandResult> GetOnHandAsync(StockOnHandQuery query, CancellationToken cancellationToken)
    {
        var onHand = _db.StockOnHand.AsNoTracking();
        if (query.WarehouseId is { } warehouseId) onHand = onHand.Where(s => s.WarehouseId == warehouseId);
        if (!query.IncludeZero) onHand = onHand.Where(s => s.Qty != 0);
        if (query.CategoryId is { } categoryId)
        {
            // Chọn nhóm cha thì gồm cả hàng của mọi nhóm con (như danh mục hàng hóa).
            var parents = await _db.ItemCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.ParentId, cancellationToken);
            var categoryIds = parents.Keys.Where(id => ItemAdministration.IsUnder(id, categoryId, parents)).ToList();
            onHand = onHand.Where(s => _db.Items.Any(i => i.Id == s.ItemId && categoryIds.Contains(i.CategoryId)));
        }

        var rows = await (
                from s in onHand
                join w in _db.Warehouses on s.WarehouseId equals w.Id
                join i in _db.Items on s.ItemId equals i.Id
                join c in _db.ItemCategories on i.CategoryId equals c.Id
                join u in _db.Uoms on i.BaseUomId equals u.Id
                orderby w.Code, i.Code, s.LotNo
                select new
                {
                    s.WarehouseId, WarehouseCode = w.Code, WarehouseName = w.Name, s.ItemId, ItemCode = i.Code, ItemName = i.Name,
                    CategoryCode = c.Code, CategoryName = c.Name, UomCode = u.Code, s.LotNo, s.Qty, i.MinStock,
                })
            .Take(OnHandLimit + 1)
            .ToListAsync(cancellationToken);
        var truncated = rows.Count > OnHandLimit;
        if (truncated) rows = rows[..OnHandLimit];

        // Giá trị tồn = Σ (chiều × giá trị) trên sổ kho của đúng kho × hàng × lô. Lọc theo các mặt hàng có trên màn hình
        // để không cộng cả sổ kho của công ty.
        var itemIds = rows.Select(r => r.ItemId).Distinct().ToList();
        var ledger = _db.StockLedger.AsNoTracking().Where(l => itemIds.Contains(l.ItemId));
        if (query.WarehouseId is { } onlyWarehouse) ledger = ledger.Where(l => l.WarehouseId == onlyWarehouse);
        var amounts = (await ledger
                .GroupBy(l => new { l.WarehouseId, l.ItemId, l.LotNo })
                .Select(g => new { g.Key.WarehouseId, g.Key.ItemId, g.Key.LotNo, Amount = g.Sum(l => l.Direction * l.Amount) })
                .ToListAsync(cancellationToken))
            .ToDictionary(a => (a.WarehouseId, a.ItemId, a.LotNo), a => a.Amount);

        // Tồn tối thiểu là một số cho cả công ty nên so với tổng tồn của mặt hàng trên MỌI kho, mọi lô – không theo kho đang lọc
        // hay kho được phép (design.md mục 11.1). Ví dụ KT 500 + KP1 24, tối thiểu 60: xem riêng KP1 vẫn không báo.
        // Bỏ qua bộ lọc kho chỉ ở phép cộng này (cùng công ty, nhóm hàng vẫn lọc); màn hình chỉ nhận cờ báo, không nhận số tồn
        // của kho ngoài phạm vi.
        var minItemIds = rows.Where(r => r.MinStock != null).Select(r => r.ItemId).Distinct().ToList();
        var totals = minItemIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await _db.StockOnHand.AsNoTracking()
                .IgnoreQueryFilters([ProcDbContext.WarehouseFilter])
                .Where(x => minItemIds.Contains(x.ItemId))
                .GroupBy(x => x.ItemId)
                .Select(g => new { ItemId = g.Key, Qty = g.Sum(x => x.Qty) })
                .ToDictionaryAsync(x => x.ItemId, x => x.Qty, cancellationToken);
        var items = rows.Select(r =>
        {
            var amount = amounts.GetValueOrDefault((r.WarehouseId, r.ItemId, r.LotNo));
            // Đơn giá bình quân, ví dụ 24 hộp giá trị 480.000 → 20.000 đ/hộp; làm tròn 6 số lẻ theo kiểu numeric(19,6).
            var unitCost = r.Qty > 0 ? Math.Round(amount / r.Qty, 6, MidpointRounding.AwayFromZero) : 0m;
            return new StockOnHandRowDto(
                r.WarehouseId, r.WarehouseCode, r.WarehouseName, r.ItemId, r.ItemCode, r.ItemName, r.CategoryCode, r.CategoryName,
                r.UomCode, r.LotNo, r.Qty, amount, unitCost, r.MinStock, r.MinStock is { } min && totals.GetValueOrDefault(r.ItemId) < min);
        }).ToList();
        return new StockOnHandResult(items, truncated, OnHandLimit);
    }

    /// <inheritdoc />
    public async Task<StockCardDto> GetStockCardAsync(StockCardQuery query, CancellationToken cancellationToken)
    {
        if (query.From > query.To) throw new BusinessRuleException("Từ ngày phải trước hoặc bằng đến ngày.");
        if (query.To.DayNumber - query.From.DayNumber >= StockCardMaxDays)
            throw new BusinessRuleException("Thẻ kho xem tối đa hai năm mỗi lần.");

        var item = await (
                from i in _db.Items.AsNoTracking()
                join u in _db.Uoms on i.BaseUomId equals u.Id
                where i.Id == query.ItemId
                select new { i.Code, i.Name, UomCode = u.Code })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy mặt hàng.");
        if (query.WarehouseId is { } id && !await _db.Warehouses.AnyAsync(w => w.Id == id, cancellationToken))
            throw new NotFoundException("Không tìm thấy kho.");

        var ledger = _db.StockLedger.AsNoTracking().Where(l => l.ItemId == query.ItemId);
        if (query.WarehouseId is { } warehouseId) ledger = ledger.Where(l => l.WarehouseId == warehouseId);

        var openingRows = await OpeningAsync(query.From, query.WarehouseId, [query.ItemId], cancellationToken);
        var opening = new { Qty = openingRows.Sum(o => o.Qty), Amount = openingRows.Sum(o => o.Amount) };

        var rows = await (
                from l in ledger
                where l.PostingDate >= query.From && l.PostingDate <= query.To
                join d in _db.InventoryDocuments on l.DocumentId equals d.Id
                join w in _db.Warehouses on l.WarehouseId equals w.Id
                join s in _db.Suppliers on d.SupplierId equals s.Id into sj
                from s in sj.DefaultIfEmpty()
                join fw in _db.Warehouses on d.FromWarehouseId equals fw.Id into fwj
                from fw in fwj.DefaultIfEmpty()
                join tw in _db.Warehouses on d.ToWarehouseId equals tw.Id into twj
                from tw in twj.DefaultIfEmpty()
                orderby l.PostingDate, l.Id
                select new
                {
                    l.PostingDate, l.DocumentId, d.No, l.DocType, WarehouseCode = w.Code, SupplierCode = s != null ? s.Code : null,
                    SupplierName = s != null ? s.Name : null, FromCode = fw != null ? fw.Code : null, ToCode = tw != null ? tw.Code : null,
                    l.LotNo, l.Direction, l.BaseQty, l.UnitCost, l.Amount, l.IsCostFinal,
                })
            .ToListAsync(cancellationToken);

        // Tồn lũy kế: tồn đầu + từng phát sinh theo thứ tự ngày rồi thứ tự ghi sổ. Ví dụ tồn đầu 48, xuất 24 → 24, nhập 24 → 48.
        // Xem mọi kho thì phiếu chuyển giữa hai kho được phép sinh cả dòng xuất và dòng nhập – tồn chung không đổi, đúng thực tế.
        var balance = opening.Qty;
        var entries = new List<StockCardEntryDto>(rows.Count);
        foreach (var r in rows)
        {
            balance += r.Direction * r.BaseQty;
            var description = r.DocType == "TRF"
                ? $"{r.FromCode ?? "?"} → {r.ToCode ?? "?"}"
                : r.SupplierCode is null ? null : $"{r.SupplierCode} – {r.SupplierName}";
            entries.Add(new StockCardEntryDto(
                r.PostingDate, r.DocumentId, r.No, r.DocType, r.WarehouseCode, description, r.LotNo,
                r.Direction > 0 ? r.BaseQty : 0m, r.Direction < 0 ? r.BaseQty : 0m, balance, r.UnitCost, r.Direction * r.Amount,
                r.IsCostFinal));
        }

        var openingQty = opening.Qty;
        var openingAmount = opening.Amount;
        return new StockCardDto(
            item.Code, item.Name, item.UomCode, openingQty, openingAmount, entries,
            entries.Sum(e => e.InQty), entries.Sum(e => e.OutQty), balance, openingAmount + entries.Sum(e => e.Amount));
    }

    /// <inheritdoc />
    public async Task<StockMovementResult> GetMovementAsync(StockMovementQuery query, CancellationToken cancellationToken)
    {
        var period = await _db.InventoryPeriods.AsNoTracking().SingleOrDefaultAsync(p => p.Id == query.PeriodId && p.Month > 0, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kỳ kho tháng.");
        List<Guid>? itemIds = null;
        if (query.CategoryId is { } categoryId)
        {
            var parents = await _db.ItemCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.ParentId, cancellationToken);
            var categoryIds = parents.Keys.Where(id => ItemAdministration.IsUnder(id, categoryId, parents)).ToList();
            itemIds = await _db.Items.AsNoTracking().Where(i => categoryIds.Contains(i.CategoryId)).Select(i => i.Id).ToListAsync(cancellationToken);
        }

        var opening = await OpeningAsync(period.StartDate, query.WarehouseId, itemIds, cancellationToken);
        // Lọc theo kỳ (mỗi dòng sổ kho thuộc đúng kỳ của ngày ghi sổ) để dùng chỉ mục (CompanyId, PeriodId, ItemId) – sổ kho nhiều năm không
        // phải quét theo khoảng ngày (design.md mục 14.6).
        var ledger = _db.StockLedger.AsNoTracking().Where(l => l.PeriodId == period.Id);
        if (query.WarehouseId is { } warehouseId) ledger = ledger.Where(l => l.WarehouseId == warehouseId);
        if (itemIds is not null) ledger = ledger.Where(l => itemIds.Contains(l.ItemId));
        var moves = await ledger
            .GroupBy(l => new { l.WarehouseId, l.ItemId, l.DocType, l.Direction })
            .Select(g => new { g.Key.WarehouseId, g.Key.ItemId, g.Key.DocType, g.Key.Direction, Qty = g.Sum(l => l.BaseQty), Amount = g.Sum(l => l.Amount) })
            .ToListAsync(cancellationToken);

        var keys = opening.Select(o => (o.WarehouseId, o.ItemId)).Concat(moves.Select(m => (m.WarehouseId, m.ItemId))).Distinct().ToList();
        var keyItems = keys.Select(k => k.ItemId).Distinct().ToList();
        var keyWarehouses = keys.Select(k => k.WarehouseId).Distinct().ToList();
        var items = await (
                from i in _db.Items.AsNoTracking()
                join c in _db.ItemCategories on i.CategoryId equals c.Id
                join u in _db.Uoms on i.BaseUomId equals u.Id
                where keyItems.Contains(i.Id)
                select new { i.Id, i.Code, i.Name, CategoryCode = c.Code, CategoryName = c.Name, UomCode = u.Code })
            .ToDictionaryAsync(i => i.Id, cancellationToken);
        var warehouses = await _db.Warehouses.AsNoTracking().Where(w => keyWarehouses.Contains(w.Id)).ToDictionaryAsync(w => w.Id, cancellationToken);

        // Tra theo khóa kho × mặt hàng thay vì quét cả danh sách cho từng dòng: 6.000 dòng × 9.000 nhóm phát sinh từng mất ~1 giây
        // (đo trên 200.000 dòng sổ kho – design.md mục 14.6).
        var openingByKey = opening.ToLookup(o => (o.WarehouseId, o.ItemId));
        var movesByKey = moves.ToLookup(x => (x.WarehouseId, x.ItemId));

        // NXT theo kho × mặt hàng (cộng mọi lô): tồn đầu + nhập − xuất = tồn cuối, tách nhập mua / trả NCC / chuyển kho (design.md mục 11).
        var rows = keys
            .Where(k => items.ContainsKey(k.ItemId) && warehouses.ContainsKey(k.WarehouseId))
            .Select(k =>
            {
                var open = openingByKey[(k.WarehouseId, k.ItemId)].ToList();
                var m = movesByKey[(k.WarehouseId, k.ItemId)].ToList();
                (decimal Qty, decimal Amount) Sum(Func<string, int, bool> match) =>
                    (m.Where(x => match(x.DocType, x.Direction)).Sum(x => x.Qty), m.Where(x => match(x.DocType, x.Direction)).Sum(x => x.Amount));
                var purchase = Sum((t, d) => t == "GRN" && d > 0);
                var returned = Sum((t, d) => t == "RTV" && d < 0);
                var transferIn = Sum((t, d) => t == "TRF" && d > 0);
                var transferOut = Sum((t, d) => t == "TRF" && d < 0);
                var totalIn = Sum((_, d) => d > 0);
                var totalOut = Sum((_, d) => d < 0);
                var openQty = open.Sum(o => o.Qty);
                var openAmount = open.Sum(o => o.Amount);
                var item = items[k.ItemId];
                var warehouse = warehouses[k.WarehouseId];
                return new StockMovementRowDto(
                    k.WarehouseId, warehouse.Code, warehouse.Name, k.ItemId, item.Code, item.Name, item.CategoryCode, item.CategoryName, item.UomCode,
                    openQty, openAmount, purchase.Qty, purchase.Amount, transferIn.Qty, transferIn.Amount, totalIn.Qty, totalIn.Amount,
                    returned.Qty, returned.Amount, transferOut.Qty, transferOut.Amount, totalOut.Qty, totalOut.Amount,
                    openQty + totalIn.Qty - totalOut.Qty, openAmount + totalIn.Amount - totalOut.Amount);
            })
            .Where(r => r.OpenQty != 0 || r.OpenAmount != 0 || r.InQty != 0 || r.OutQty != 0)
            .OrderBy(r => r.WarehouseCode).ThenBy(r => r.ItemCode)
            .ToList();
        var isFinal = period.Status is PeriodStatus.Closed or PeriodStatus.Locked;
        return new StockMovementResult(period.Label, period.Status.ToString(), isFinal, rows);
    }

    /// <summary>Tồn đầu (SL, giá trị) theo kho × mặt hàng của một ngày.</summary>
    private sealed record OpeningRow(Guid WarehouseId, Guid ItemId, decimal Qty, decimal Amount);

    /// <summary>
    /// Tồn đầu tại ngày <paramref name="before"/> theo kho × mặt hàng: số dư kỳ đã đóng gần nhất kết thúc trước ngày đó + sổ kho từ
    /// sau kỳ đó tới trước ngày đó – không cộng từ đầu sổ (design.md mục 11, đoạn Hiệu năng). Chưa có kỳ đóng thì cộng từ đầu.
    /// Đi qua global query filter như mọi truy vấn của dịch vụ này.
    /// </summary>
    private async Task<List<OpeningRow>> OpeningAsync(DateOnly before, Guid? warehouseId, IReadOnlyList<Guid>? itemIds, CancellationToken cancellationToken)
    {
        var closed = await _db.InventoryPeriods.AsNoTracking()
            .Where(p => p.Month > 0 && p.EndDate < before && (p.Status == PeriodStatus.Closed || p.Status == PeriodStatus.Locked))
            .OrderByDescending(p => p.EndDate)
            .FirstOrDefaultAsync(cancellationToken);

        var result = new List<OpeningRow>();
        if (closed is not null)
        {
            var balances = _db.StockPeriodBalances.AsNoTracking().Where(b => b.PeriodId == closed.Id);
            if (warehouseId is { } w) balances = balances.Where(b => b.WarehouseId == w);
            if (itemIds is not null) balances = balances.Where(b => itemIds.Contains(b.ItemId));
            result.AddRange(await balances
                .GroupBy(b => new { b.WarehouseId, b.ItemId })
                .Select(g => new OpeningRow(g.Key.WarehouseId, g.Key.ItemId, g.Sum(b => b.CloseQty), g.Sum(b => b.CloseAmount)))
                .ToListAsync(cancellationToken));
        }

        var after = closed?.EndDate ?? DateOnly.MinValue;
        var ledger = _db.StockLedger.AsNoTracking().Where(l => l.PostingDate > after && l.PostingDate < before);
        if (warehouseId is { } wh) ledger = ledger.Where(l => l.WarehouseId == wh);
        if (itemIds is not null) ledger = ledger.Where(l => itemIds.Contains(l.ItemId));
        result.AddRange(await ledger
            .GroupBy(l => new { l.WarehouseId, l.ItemId })
            .Select(g => new OpeningRow(g.Key.WarehouseId, g.Key.ItemId, g.Sum(l => l.Direction * l.BaseQty), g.Sum(l => l.Direction * l.Amount)))
            .ToListAsync(cancellationToken));
        return result;
    }
}
