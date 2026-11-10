using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Reports;
using Proc.Domain.Common;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.MasterData;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Purchasing;

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

    /// <summary>Số dòng tối đa của báo cáo gợi ý đặt hàng (như màn hình tồn kho).</summary>
    public const int ReorderLimit = 5000;

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

        // Tổng giá trị tồn của mọi kho × hàng còn tồn trong phạm vi – mẫu số KPI tồn chậm luân chuyển (design.md mục 18.18).
        var stockAmount = stock.Sum(s => moves.GetValueOrDefault(s.Key)?.Amount ?? 0m);
        return new SlowMovingResult(today, query.Days,
            rows.OrderByDescending(r => r.IdleDays).ThenBy(r => r.WarehouseCode).ThenBy(r => r.ItemCode).ToList(), stockAmount);
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

    /// <inheritdoc />
    public async Task<ReorderSuggestionResult> GetReorderSuggestionsAsync(ReorderSuggestionQuery query, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        // Mặt hàng đi qua filter nhóm hàng của người xem; ngưỡng = điểm đặt hàng lại, chưa khai báo thì dùng tồn tối thiểu (hàng cũ chỉ có
        // tối thiểu vẫn được gợi ý) – design.md mục 18.16.
        var items = _db.Items.AsNoTracking()
            .Where(i => i.IsActive && i.Type == ItemType.Inventory && (i.ReorderPoint ?? i.MinStock) > 0);
        if (query.CriticalOnly) items = items.Where(i => i.IsCritical);
        if (query.CategoryId is { } c)
        {
            var parents = await _db.ItemCategories.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.ParentId, cancellationToken);
            var categoryIds = parents.Keys.Where(id => ItemAdministration.IsUnder(id, c, parents)).ToList();
            items = items.Where(i => categoryIds.Contains(i.CategoryId));
        }

        var list = await (
                from i in items
                join g in _db.ItemCategories on i.CategoryId equals g.Id
                join u in _db.Uoms on i.BaseUomId equals u.Id
                select new
                {
                    i.Id, i.Code, i.Name, CategoryCode = g.Code, CategoryName = g.Name, i.BaseUomId, Uom = u.Code, i.IsCritical,
                    i.SafetyStock, Threshold = (i.ReorderPoint ?? i.MinStock)!.Value, i.MaxStock,
                })
            .ToListAsync(cancellationToken);
        var ids = list.Select(i => i.Id).ToList();

        // Đặt hàng là việc của cả công ty nên cộng tồn mọi kho: bỏ filter kho chỉ ở phép cộng này (cùng lý do tồn tối thiểu – mục 11.1);
        // chỉ trả số lượng của mặt hàng đã qua filter nhóm hàng, không trả giá trị, không chia theo kho.
        var onHand = await _db.StockOnHand.AsNoTracking()
            .IgnoreQueryFilters([ProcDbContext.WarehouseFilter])
            .Where(s => ids.Contains(s.ItemId))
            .GroupBy(s => s.ItemId)
            .Select(g => new { g.Key, Qty = g.Sum(s => s.Qty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);

        // Đang đặt: PO chưa hủy, chưa đóng. PO nháp/chờ duyệt tính đủ SL (đã định mua, tránh gợi ý trùng); PO đã duyệt tính phần chưa
        // nhận theo phiếu nhập đã ghi sổ. Ví dụ PO 100 đã nhận 30 → 70.
        var poLines = await (
                from l in _db.Set<PurchaseOrderLine>().AsNoTracking()
                join o in _db.PurchaseOrders on l.OrderId equals o.Id
                where ids.Contains(l.ItemId) && (o.Status == ApprovalDocumentStatus.Draft || o.Status == ApprovalDocumentStatus.Submitted
                                                 || o.Status == ApprovalDocumentStatus.Approved)
                select new { l.Id, l.ItemId, l.BaseQty, o.Status })
            .ToListAsync(cancellationToken);
        var received = await PurchaseOrderReceipts.LegacyGrnReceivedBaseByLineAsync(
            _db, poLines.Where(l => l.Status == ApprovalDocumentStatus.Approved).Select(l => l.Id).ToList(), null, cancellationToken);
        var onOrder = poLines.GroupBy(l => l.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(l => Math.Max(0, l.BaseQty - received.GetValueOrDefault(l.Id))));

        // Đề nghị còn mở: PR chưa hủy (cả nháp – vừa lập từ gợi ý mà chưa gửi thì không gợi ý lại), phần chưa lên PO chưa hủy.
        var prLines = await (
                from l in _db.Set<PurchaseRequestLine>().AsNoTracking()
                join r in _db.PurchaseRequests on l.RequestId equals r.Id
                where ids.Contains(l.ItemId) && r.Status != ApprovalDocumentStatus.Cancelled && r.Status != ApprovalDocumentStatus.Closed
                select new { l.Id, l.ItemId, l.BaseQty })
            .ToListAsync(cancellationToken);
        var prLineIds = prLines.Select(l => l.Id).ToList();
        var ordered = await (
                from ol in _db.Set<PurchaseOrderLine>().AsNoTracking()
                join o in _db.PurchaseOrders on ol.OrderId equals o.Id
                where ol.SourcePrLineId != null && prLineIds.Contains(ol.SourcePrLineId.Value) && o.Status != ApprovalDocumentStatus.Cancelled
                group ol by ol.SourcePrLineId!.Value into g
                select new { g.Key, Qty = g.Sum(x => x.BaseQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);
        var openRequests = prLines.GroupBy(l => l.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(l => Math.Max(0, l.BaseQty - ordered.GetValueOrDefault(l.Id))));

        var preferred = await (
                from si in _db.SupplierItems.AsNoTracking()
                join s in _db.Suppliers on si.SupplierId equals s.Id
                where si.IsPreferred && ids.Contains(si.ItemId)
                select new { si.ItemId, s.Id, s.Code })
            .ToDictionaryAsync(x => x.ItemId, cancellationToken);

        // Khả dụng = tồn + đang đặt + đề nghị còn mở; cần đặt khi khả dụng < ngưỡng; SL gợi ý đưa khả dụng lên tồn tối đa (không có thì lên
        // ngưỡng). Ví dụ điểm đặt lại 50, tối đa 200, tồn 30, đang đặt 10 → khả dụng 40 < 50 → gợi ý 200 − 40 = 160.
        // Mức khẩn xét tồn thực có (hàng đang đặt chưa về chưa dùng được): tồn 30 < tồn an toàn 40 → Urgent.
        var rows = list.Select(i =>
            {
                var qty = onHand.GetValueOrDefault(i.Id);
                var ordering = onOrder.GetValueOrDefault(i.Id);
                var requested = openRequests.GetValueOrDefault(i.Id);
                var available = qty + ordering + requested;
                var target = Math.Max(i.MaxStock ?? i.Threshold, i.Threshold);
                var suggested = available < i.Threshold ? target - available : 0;
                var level = suggested <= 0 ? "Ok" : i.SafetyStock is { } safety && qty < safety ? "Urgent" : "Reorder";
                var supplier = preferred.GetValueOrDefault(i.Id);
                return new ReorderSuggestionRowDto(
                    i.Id, i.Code, i.Name, i.CategoryCode, i.CategoryName, i.BaseUomId, i.Uom, i.IsCritical, i.SafetyStock, i.Threshold,
                    i.MaxStock, qty, ordering, requested, available, suggested, level, supplier?.Id, supplier?.Code);
            })
            .Where(r => query.IncludeAll || r.SuggestedQty > 0)
            .OrderByDescending(r => r.IsCritical)
            .ThenBy(r => r.Level switch { "Urgent" => 0, "Reorder" => 1, _ => 2 })
            .ThenBy(r => r.ItemCode, StringComparer.Ordinal)
            .ToList();
        return new ReorderSuggestionResult(rows.Take(ReorderLimit).ToList(), rows.Count > ReorderLimit, ReorderLimit);
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

    /// <summary>Mã và tên kho cho dòng báo cáo.</summary>
    private sealed record WarehouseName(string Code, string Name);

    /// <summary>Mã, tên, nhóm hàng và ĐVT cơ sở của mặt hàng cho dòng báo cáo.</summary>
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
