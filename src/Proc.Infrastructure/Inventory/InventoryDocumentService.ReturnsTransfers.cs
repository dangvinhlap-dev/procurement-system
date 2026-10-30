using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Inventory;

/// <summary>
/// Phiếu xuất trả NCC (RTV – design.md mục 10.5) và phiếu chuyển kho nội bộ (TRF – mục 10.6).
/// </summary>
public sealed partial class InventoryDocumentService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ReturnableLineDto>> GetReturnableLinesAsync(
        Guid receiptId, Guid? excludeDocumentId, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var receipt = await LoadPostedReceiptAsync(receiptId, cancellationToken);
        var returned = await ReturnedQuantitiesAsync(receipt, excludeDocumentId, cancellationToken);
        var itemIds = receipt.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Code, i.Name }).ToDictionaryAsync(i => i.Id, cancellationToken);
        var uomIds = receipt.Lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);

        // Dòng có mặt hàng ngoài phạm vi nhóm hàng của người xem thì không trả được – bỏ khỏi danh sách.
        return receipt.Lines
            .Where(l => items.ContainsKey(l.ItemId))
            .OrderBy(l => l.LineNo)
            .Select(l =>
            {
                var done = returned.GetValueOrDefault(l.Id);
                return new ReturnableLineDto(
                    l.Id, l.LineNo, l.ItemId, items[l.ItemId].Code, items[l.ItemId].Name, l.UomId, uoms.GetValueOrDefault(l.UomId) ?? "",
                    l.LotNo, l.UnitPrice, l.VatCode, l.Qty, done, l.Qty - done);
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<InventoryDocumentDto> CreateReturnAsync(SaveReturnRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var source = await LoadPostedReceiptAsync(request.SourceDocumentId, cancellationToken);
        var lines = await ResolveReturnLinesAsync(source, request.Lines, null, cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.ReturnToVendor, request.Date, cancellationToken);
        var document = InventoryDocument.CreateReturn(companyId, no, new ReturnHeader(request.Date, source, request.InvoiceNo, request.Note));
        document.SetLines(lines);
        _db.InventoryDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(document.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InventoryDocumentDto> UpdateReturnAsync(Guid id, SaveReturnRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var document = await LoadAsync(id, DocumentTypes.ReturnToVendor, cancellationToken);
        await EnsureAllItemsVisibleAsync(document, cancellationToken);
        var source = await LoadPostedReceiptAsync(request.SourceDocumentId, cancellationToken);
        var lines = await ResolveReturnLinesAsync(source, request.Lines, id, cancellationToken);

        document.UpdateReturn(new ReturnHeader(request.Date, source, request.InvoiceNo, request.Note));
        document.SetLines(lines);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InventoryDocumentDto> CreateTransferAsync(SaveTransferRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        await CheckTransferWarehousesAsync(request.FromWarehouseId, request.ToWarehouseId, cancellationToken);
        var lines = await ResolveTransferLinesAsync(request.Lines, cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.Transfer, request.Date, cancellationToken);
        var document = InventoryDocument.CreateTransfer(companyId, no, ToTransferHeader(request));
        document.SetLines(lines);
        _db.InventoryDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(document.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InventoryDocumentDto> UpdateTransferAsync(Guid id, SaveTransferRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var document = await LoadAsync(id, DocumentTypes.Transfer, cancellationToken);
        await EnsureAllItemsVisibleAsync(document, cancellationToken);
        await CheckTransferWarehousesAsync(request.FromWarehouseId, request.ToWarehouseId, cancellationToken);
        var lines = await ResolveTransferLinesAsync(request.Lines, cancellationToken);

        document.UpdateTransfer(ToTransferHeader(request));
        document.SetLines(lines);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StockAvailabilityDto>> GetAvailableStockAsync(
        Guid warehouseId, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await _db.StockOnHand.AsNoTracking()
            .Where(s => s.WarehouseId == warehouseId && itemIds.Contains(s.ItemId))
            .OrderBy(s => s.ItemId).ThenBy(s => s.LotNo)
            .Select(s => new StockAvailabilityDto(s.ItemId, s.LotNo, s.Qty))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Phiếu nhập mua đã ghi sổ (kèm dòng), đọc qua filter phạm vi – làm phiếu gốc cho phiếu trả.</summary>
    private async Task<InventoryDocument> LoadPostedReceiptAsync(Guid id, CancellationToken cancellationToken)
    {
        var receipt = await _db.InventoryDocuments.AsNoTracking().Include(d => d.Lines).SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phiếu nhập gốc (hoặc phiếu thuộc kho ngoài phạm vi của bạn).");
        if (receipt.DocType != DocumentTypes.GoodsReceipt || receipt.Status != DocumentStatus.Posted)
            throw new BusinessRuleException($"Phiếu gốc {receipt.No} phải là phiếu nhập mua đã ghi sổ.");
        return receipt;
    }

    /// <summary>
    /// Số đã trả theo từng dòng nhập: cộng các dòng phiếu trả ĐÃ GHI SỔ trỏ về dòng đó (theo ĐVT của dòng nhập), trừ phiếu đang sửa.
    /// Phiếu trả còn nháp không tính – lúc ghi sổ kiểm tra lại nên hai phiếu nháp không trả lố được.
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> ReturnedQuantitiesAsync(
        InventoryDocument receipt, Guid? excludeDocumentId, CancellationToken cancellationToken)
    {
        var sourceIds = receipt.Lines.Select(l => l.Id).ToList();
        return await (
                from l in _db.InventoryDocumentLines
                join d in _db.InventoryDocuments on l.DocumentId equals d.Id
                where l.SourceLineId != null && sourceIds.Contains(l.SourceLineId.Value)
                      && d.DocType == DocumentTypes.ReturnToVendor && d.Status == DocumentStatus.Posted
                      && (excludeDocumentId == null || d.Id != excludeDocumentId)
                group l by l.SourceLineId!.Value into g
                select new { SourceLineId = g.Key, Qty = g.Sum(x => x.Qty) })
            .ToDictionaryAsync(x => x.SourceLineId, x => x.Qty, cancellationToken);
    }

    /// <summary>
    /// Dòng trả lấy hàng, ĐVT, lô, đơn giá, thuế suất theo dòng nhập gốc (giá đích danh – design.md mục 6.1); số trả không vượt
    /// số còn trả được.
    /// </summary>
    private async Task<List<(InventoryLineData Data, Item Item)>> ResolveReturnLinesAsync(
        InventoryDocument source, IReadOnlyList<SaveReturnLineRequest> requests, Guid? currentDocumentId, CancellationToken cancellationToken)
    {
        var returned = await ReturnedQuantitiesAsync(source, currentDocumentId, cancellationToken);
        var itemIds = source.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        var duplicate = requests.GroupBy(r => r.SourceLineId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new BusinessRuleException("Mỗi dòng của phiếu nhập gốc chỉ chọn một lần trên phiếu trả.");

        return requests.Select((r, index) =>
        {
            var sourceLine = source.Lines.SingleOrDefault(l => l.Id == r.SourceLineId)
                ?? throw new NotFoundException($"Dòng {index + 1}: dòng hàng không thuộc phiếu nhập {source.No}.");
            var item = items.GetValueOrDefault(sourceLine.ItemId)
                ?? throw new NotFoundException($"Dòng {index + 1}: không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).");
            var remaining = sourceLine.Qty - returned.GetValueOrDefault(sourceLine.Id);
            if (r.Qty > remaining)
            {
                throw new BusinessRuleException(
                    $"Dòng {index + 1} ({item.Code}): số trả {Format(r.Qty)} vượt số còn trả được {Format(remaining)} của phiếu nhập {source.No}.");
            }

            return (new InventoryLineData(r.Id, sourceLine.ItemId, sourceLine.UomId, r.Qty, sourceLine.UnitPrice, sourceLine.VatCode,
                sourceLine.LotNo, sourceLine.ExpiryDate, r.Note, sourceLine.Id), item);
        }).ToList();
    }

    /// <summary>Lúc ghi sổ phiếu trả: kiểm tra lại số còn trả được (phiếu trả khác có thể vừa ghi sổ).</summary>
    private async Task CheckReturnQuantitiesAsync(InventoryDocument document, CancellationToken cancellationToken)
    {
        var source = await LoadPostedReceiptAsync(document.LinkedDocumentId!.Value, cancellationToken);
        await ResolveReturnLinesAsync(
            source,
            document.Lines.Select(l => new SaveReturnLineRequest(l.Id, l.SourceLineId!.Value, l.Qty, l.Note)).ToList(),
            document.Id,
            cancellationToken);
    }

    /// <summary>
    /// Hai kho của phiếu chuyển: đều thuộc công ty đang làm việc và trong phạm vi kho của người lập (đọc qua filter), đang dùng,
    /// khác nhau. Kho Sub không chuyển sang công ty khác được – kho công ty khác như không tồn tại (CLAUDE.md mục 4).
    /// </summary>
    private async Task CheckTransferWarehousesAsync(Guid fromId, Guid toId, CancellationToken cancellationToken)
    {
        if (fromId == toId) throw new BusinessRuleException("Kho xuất và kho nhập phải khác nhau.");
        var warehouses = await _db.Warehouses.AsNoTracking().Where(w => w.Id == fromId || w.Id == toId).ToListAsync(cancellationToken);
        var from = warehouses.SingleOrDefault(w => w.Id == fromId)
            ?? throw new NotFoundException("Không tìm thấy kho xuất (hoặc kho ngoài phạm vi của bạn).");
        var to = warehouses.SingleOrDefault(w => w.Id == toId)
            ?? throw new NotFoundException("Không tìm thấy kho nhập (kho phải cùng công ty và trong phạm vi của bạn).");
        foreach (var w in new[] { from, to }.Where(w => !w.IsActive))
            throw new BusinessRuleException($"Kho {w.Code} đã ngừng dùng.");
    }

    /// <summary>Dòng chuyển kho: không có đơn giá, thuế suất, hạn dùng (giá vốn tạm tính ghi ở sổ kho lúc ghi sổ).</summary>
    private async Task<List<(InventoryLineData Data, Item Item)>> ResolveTransferLinesAsync(
        IReadOnlyList<SaveTransferLineRequest> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        return lines
            .Select((l, index) => (
                new InventoryLineData(l.Id, l.ItemId, l.UomId, l.Qty, 0, null, l.LotNo, null, l.Note),
                items.GetValueOrDefault(l.ItemId)
                ?? throw new NotFoundException($"Dòng {index + 1}: không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).")))
            .ToList();
    }

    /// <summary>
    /// Giá vốn tạm tính theo công ty × mặt hàng (design.md mục 7.5): bình quân trên sổ kho tới thời điểm ghi sổ
    /// = Σ giá trị (nhập +, xuất −) ÷ Σ số lượng; tồn ≤ 0 thì 0. Ví dụ nhập 48 hộp 960.000 và 24 hộp 520.000 → 1.480.000 ÷ 72
    /// = 20.555,555556 đ/hộp.
    /// Giá vốn tính cho cả công ty (mọi kho chung một giá vốn – CLAUDE.md mục 4), nên tắt riêng filter phạm vi kho của người dùng;
    /// filter công ty và nhóm hàng vẫn giữ, và chỉ trả về một con số chứ không lộ dòng sổ của kho ngoài phạm vi.
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> ProvisionalCostsAsync(IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
    {
        var totals = await _db.StockLedger.AsNoTracking()
            .IgnoreQueryFilters([ProcDbContext.WarehouseFilter])
            .Where(x => itemIds.Contains(x.ItemId))
            .GroupBy(x => x.ItemId)
            .Select(g => new { ItemId = g.Key, Qty = g.Sum(x => x.Direction * x.BaseQty), Amount = g.Sum(x => x.Direction * x.Amount) })
            .ToListAsync(cancellationToken);
        return itemIds.ToDictionary(
            id => id,
            id => totals.SingleOrDefault(t => t.ItemId == id) is { Qty: > 0 } t ? t.Amount / t.Qty : 0m);
    }

    private static void EnsureItemsActive(InventoryDocument document, Dictionary<Guid, Item> items)
    {
        foreach (var line in document.Lines.Where(l => !items[l.ItemId].IsActive))
            throw new BusinessRuleException($"Dòng {line.LineNo}: mặt hàng {items[line.ItemId].Code} đã ngừng dùng.");
    }

    private static string Format(decimal value) => value.ToString("#,##0.####", VietnameseCulture);

    private static TransferHeader ToTransferHeader(SaveTransferRequest r) =>
        new(r.Date, r.FromWarehouseId, r.ToWarehouseId, r.Deliverer, r.Receiver, r.Note);
}
