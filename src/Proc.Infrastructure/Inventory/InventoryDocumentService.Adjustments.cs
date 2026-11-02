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
        var lines = await ResolveAdjustmentLinesAsync(request.Lines, cancellationToken);

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
        var lines = await ResolveAdjustmentLinesAsync(request.Lines, cancellationToken);

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

    /// <summary>Dòng điều chỉnh: chiều tăng/giảm; tăng có đơn giá nhập tay, giảm đơn giá 0 (giá vốn ghi ở sổ kho lúc ghi sổ).</summary>
    private async Task<List<(InventoryLineData Data, Item Item)>> ResolveAdjustmentLinesAsync(
        IReadOnlyList<SaveAdjustmentLineRequest> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        return lines
            .Select((l, index) => (
                new InventoryLineData(l.Id, l.ItemId, l.UomId, l.Qty, l.UnitPrice, null, l.LotNo, l.ExpiryDate, l.Note, Direction: l.Direction),
                items.GetValueOrDefault(l.ItemId)
                ?? throw new NotFoundException($"Dòng {index + 1}: không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).")))
            .ToList();
    }

    private static AdjustmentHeader ToAdjustmentHeader(SaveAdjustmentRequest r) => new(r.Date, r.WarehouseId, r.Reason, r.CountReportNo, r.Note);
}
