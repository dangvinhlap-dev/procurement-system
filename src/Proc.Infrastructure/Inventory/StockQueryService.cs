using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Domain.Common;
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

        // Tồn tối thiểu đặt cho mặt hàng nên so với tổng tồn của mặt hàng trên các kho đang xem (mọi lô).
        var totals = rows.GroupBy(r => r.ItemId).ToDictionary(g => g.Key, g => g.Sum(r => r.Qty));
        var items = rows.Select(r =>
        {
            var amount = amounts.GetValueOrDefault((r.WarehouseId, r.ItemId, r.LotNo));
            // Đơn giá bình quân, ví dụ 24 hộp giá trị 480.000 → 20.000 đ/hộp; làm tròn 6 số lẻ theo kiểu numeric(19,6).
            var unitCost = r.Qty > 0 ? Math.Round(amount / r.Qty, 6, MidpointRounding.AwayFromZero) : 0m;
            return new StockOnHandRowDto(
                r.WarehouseId, r.WarehouseCode, r.WarehouseName, r.ItemId, r.ItemCode, r.ItemName, r.CategoryCode, r.CategoryName,
                r.UomCode, r.LotNo, r.Qty, amount, unitCost, r.MinStock, r.MinStock is { } min && totals[r.ItemId] < min);
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

        // Tồn đầu cộng từ sổ kho trước ngày bắt đầu. Khi có số dư kỳ đã chốt (StockPeriodBalance – đóng kỳ, giai đoạn 3) sẽ đọc
        // số dư kỳ trước rồi cộng phần trong kỳ, không cộng từ đầu (design.md mục 11, "Hiệu năng").
        var opening = await ledger
            .Where(l => l.PostingDate < query.From)
            .GroupBy(_ => 1)
            .Select(g => new { Qty = g.Sum(l => l.Direction * l.BaseQty), Amount = g.Sum(l => l.Direction * l.Amount) })
            .SingleOrDefaultAsync(cancellationToken);

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
        var balance = opening?.Qty ?? 0m;
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

        var openingQty = opening?.Qty ?? 0m;
        var openingAmount = opening?.Amount ?? 0m;
        return new StockCardDto(
            item.Code, item.Name, item.UomCode, openingQty, openingAmount, entries,
            entries.Sum(e => e.InQty), entries.Sum(e => e.OutQty), balance, openingAmount + entries.Sum(e => e.Amount));
    }
}
