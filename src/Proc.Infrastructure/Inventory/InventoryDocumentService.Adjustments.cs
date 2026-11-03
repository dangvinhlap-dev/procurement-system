using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;

namespace Proc.Infrastructure.Inventory;

/// <summary>
/// Phiếu điều chỉnh / kiểm kê kho – ADJ (design.md mục 6.1): một kho bất kỳ của công ty, dòng tăng nhập giá tay, dòng giảm xuất theo giá
/// vốn; bắt buộc lý do và số biên bản kiểm kê; quyền riêng <see cref="Permissions.Inventory.AdjustmentCreate"/> (lập, sửa, hủy) và
/// <see cref="Permissions.Inventory.AdjustmentPost"/> (ghi sổ, bỏ ghi sổ) – kiểm tra lại ở đây, không tin quyền của endpoint dùng chung.
/// </summary>
public sealed partial class InventoryDocumentService
{
    /// <inheritdoc />
    public async Task<InventoryDocumentDto> CreateAdjustmentAsync(SaveAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        RequirePermission(Permissions.Inventory.AdjustmentCreate, "lập phiếu điều chỉnh, kiểm kê kho");
        await CheckAdjustmentWarehouseAsync(request.WarehouseId, cancellationToken);
        var lines = await ResolveAdjustmentLinesAsync(request.WarehouseId, request.Lines, cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.Adjustment, request.Date, cancellationToken);
        var document = InventoryDocument.CreateAdjustment(companyId, no, ToAdjustmentHeader(request));
        document.SetLines(lines);
        _db.InventoryDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(document.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InventoryDocumentDto> UpdateAdjustmentAsync(Guid id, SaveAdjustmentRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        RequirePermission(Permissions.Inventory.AdjustmentCreate, "sửa phiếu điều chỉnh, kiểm kê kho");
        var document = await LoadAsync(id, DocumentTypes.Adjustment, cancellationToken);
        await EnsureAllItemsVisibleAsync(document, cancellationToken);
        await CheckAdjustmentWarehouseAsync(request.WarehouseId, cancellationToken);
        var lines = await ResolveAdjustmentLinesAsync(request.WarehouseId, request.Lines, cancellationToken);

        document.UpdateAdjustment(ToAdjustmentHeader(request));
        document.SetLines(lines);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Quyền theo loại phiếu cho thao tác dùng chung (hủy, ghi sổ, bỏ ghi sổ): phiếu điều chỉnh dùng quyền riêng, phiếu khác dùng quyền
    /// chứng từ kho. Endpoint chỉ đòi một trong hai nên phải kiểm tra lại đúng loại ở đây (CLAUDE.md mục 4).
    /// </summary>
    /// <param name="document">Phiếu.</param>
    /// <param name="ordinary">Quyền với phiếu kho thường.</param>
    /// <param name="adjustment">Quyền với phiếu điều chỉnh.</param>
    /// <param name="action">Tên thao tác trong câu báo lỗi.</param>
    /// <exception cref="ForbiddenAccessException">Khi thiếu quyền.</exception>
    private void RequireDocumentPermission(InventoryDocument document, string ordinary, string adjustment, string action)
    {
        var isAdjustment = document.DocType == DocumentTypes.Adjustment;
        RequirePermission(isAdjustment ? adjustment : ordinary, isAdjustment ? $"{action} phiếu điều chỉnh, kiểm kê kho" : $"{action} chứng từ kho");
    }

    /// <summary>Đòi một quyền chức năng tại công ty đang làm việc.</summary>
    /// <exception cref="ForbiddenAccessException">Khi thiếu quyền.</exception>
    private void RequirePermission(string permission, string action)
    {
        if (!_scope.HasPermission(permission)) throw new ForbiddenAccessException($"Bạn không có quyền {action}.");
    }

    /// <summary>Kho điều chỉnh: thuộc công ty, trong phạm vi người dùng (qua filter), còn dùng; kho tổng hay kho con đều được.</summary>
    private async Task CheckAdjustmentWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        var warehouse = await _db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.Id == warehouseId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho điều chỉnh (hoặc kho ngoài phạm vi của bạn).");
        if (!warehouse.IsActive) throw new BusinessRuleException($"Kho {warehouse.Code} đã ngừng dùng.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CountSheetRowDto>> GetCountSheetAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        RequirePermission(Permissions.Inventory.AdjustmentCreate, "lập phiếu điều chỉnh, kiểm kê kho");
        await CheckAdjustmentWarehouseAsync(warehouseId, cancellationToken);
        // Đọc qua filter phạm vi kho, nhóm hàng: hàng ngoài phạm vi không hiện trên bảng kiểm kê.
        var rows = await (
                from s in _db.StockOnHand.AsNoTracking()
                join i in _db.Items.AsNoTracking() on s.ItemId equals i.Id
                join u in _db.Uoms.AsNoTracking() on i.BaseUomId equals u.Id
                where s.WarehouseId == warehouseId && s.Qty != 0
                orderby i.Code, s.LotNo
                select new { s.ItemId, i.Code, i.Name, UomId = u.Id, UomCode = u.Code, s.LotNo, s.Qty })
            .ToListAsync(cancellationToken);

        // Hạn dùng của lô: lấy từ dòng phiếu đã ghi sổ gần nhất có cùng mặt hàng, lô (StockOnHand không lưu hạn dùng).
        var itemIds = rows.Where(r => r.LotNo != null).Select(r => r.ItemId).Distinct().ToList();
        var expiries = itemIds.Count == 0
            ? []
            : (await (
                    from l in _db.InventoryDocumentLines.AsNoTracking()
                    join d in _db.InventoryDocuments.AsNoTracking() on l.DocumentId equals d.Id
                    where d.Status == DocumentStatus.Posted && itemIds.Contains(l.ItemId) && l.LotNo != null && l.ExpiryDate != null
                    group l by new { l.ItemId, l.LotNo } into g
                    select new { g.Key.ItemId, g.Key.LotNo, Expiry = g.Max(x => x.ExpiryDate) })
                .ToListAsync(cancellationToken))
                .ToDictionary(x => (x.ItemId, x.LotNo), x => x.Expiry);
        return rows
            .Select(r => new CountSheetRowDto(r.ItemId, r.Code, r.Name, r.UomId, r.UomCode, r.LotNo,
                r.LotNo is null ? null : expiries.GetValueOrDefault((r.ItemId, r.LotNo)), r.Qty))
            .ToList();
    }

    /// <summary>
    /// Dòng điều chỉnh: chiều tăng/giảm; tăng có đơn giá nhập tay, giảm đơn giá 0 (giá vốn ghi ở sổ kho lúc ghi sổ). Dòng kiểm kê (có SL
    /// thực đếm): tồn sổ lấy tại kho lúc lưu (đúng lô nếu dòng có lô, không lô thì cộng mọi lô), domain tự tính chiều và SL chênh lệch.
    /// Tồn sổ có thể đổi sau khi lưu (phiếu khác ghi sổ) – lưu lại phiếu để tính lại theo tồn mới.
    /// </summary>
    private async Task<List<(InventoryLineData Data, Item Item)>> ResolveAdjustmentLinesAsync(
        Guid warehouseId, IReadOnlyList<SaveAdjustmentLineRequest> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        var countedIds = lines.Where(l => l.CountedQty is not null).Select(l => l.ItemId).Distinct().ToList();
        var stock = countedIds.Count == 0
            ? []
            : await _db.StockOnHand.AsNoTracking()
                .Where(s => s.WarehouseId == warehouseId && countedIds.Contains(s.ItemId))
                .Select(s => new { s.ItemId, s.LotNo, s.Qty })
                .ToListAsync(cancellationToken);
        decimal? Book(SaveAdjustmentLineRequest l)
        {
            if (l.CountedQty is null) return null;
            var lot = string.IsNullOrWhiteSpace(l.LotNo) ? null : l.LotNo.Trim();
            return stock.Where(s => s.ItemId == l.ItemId && (lot == null || s.LotNo == lot)).Sum(s => s.Qty);
        }

        return lines
            .Select((l, index) => (
                new InventoryLineData(l.Id, l.ItemId, l.UomId, l.Qty, l.UnitPrice, null, l.LotNo, l.ExpiryDate, l.Note, Direction: l.Direction,
                    BookQty: Book(l), CountedQty: l.CountedQty),
                items.GetValueOrDefault(l.ItemId)
                ?? throw new NotFoundException($"Dòng {index + 1}: không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).")))
            .ToList();
    }

    private static AdjustmentHeader ToAdjustmentHeader(SaveAdjustmentRequest r) => new(r.Date, r.WarehouseId, r.Reason, r.CountReportNo, r.Note);
}
