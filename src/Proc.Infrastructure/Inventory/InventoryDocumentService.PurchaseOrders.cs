using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Domain.Common;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Purchasing;

namespace Proc.Infrastructure.Inventory;

/// <summary>
/// Phiếu nhập mua theo đơn mua hàng (design.md mục 6.2 bước 4, 6.1 – "SL không vượt PO + dung sai %"): mỗi dòng trỏ về một dòng PO,
/// mặt hàng và ĐVT theo dòng PO, đơn giá = giá thực mua sau chiết khấu, thuế suất theo PO (không nhận từ client). Nhận đủ thì PO
/// tự chuyển Đã nhận đủ; bỏ ghi sổ phiếu nhập làm thiếu lại thì PO mở lại.
/// </summary>
public sealed partial class InventoryDocumentService
{
    /// <summary>Áp dòng PO vào dòng phiếu nhập; phiếu không theo PO thì không được có dòng PO.</summary>
    private async Task<List<(InventoryLineData Data, Item Item)>> ApplyPurchaseOrderAsync(
        SaveReceiptRequest request, List<(InventoryLineData Data, Item Item)> lines, Guid? documentId, CancellationToken cancellationToken)
    {
        if (request.PurchaseOrderId is not { } orderId)
        {
            if (lines.Any(l => l.Data.PurchaseOrderLineId is not null)) throw new BusinessRuleException("Dòng theo đơn mua hàng nhưng phiếu chưa chọn đơn mua hàng.");
            return lines;
        }

        var order = await LoadReceivableOrderAsync(orderId, request.SupplierId, cancellationToken);
        var result = lines.Select((l, index) =>
        {
            var poLine = order.Lines.FirstOrDefault(p => p.Id == l.Data.PurchaseOrderLineId)
                ?? throw new BusinessRuleException($"Dòng {index + 1}: phải chọn một dòng của đơn mua hàng {order.No}.");
            if (poLine.ItemId != l.Data.ItemId || poLine.UomId != l.Data.UomId)
                throw new BusinessRuleException($"Dòng {index + 1}: mặt hàng, ĐVT phải theo dòng {poLine.LineNo} của đơn {order.No}.");
            return (l.Data with { UnitPrice = poLine.NetUnitPrice, VatCode = poLine.VatCode }, l.Item);
        }).ToList();
        await CheckOrderQuantitiesAsync(
            order,
            result.Select((l, index) => (l.Item1.PurchaseOrderLineId!.Value, l.Item1.Qty, index + 1)).ToList(),
            documentId, cancellationToken);
        return result;
    }

    /// <summary>Kiểm tra lại SL theo PO lúc ghi sổ (phiếu nhập khác có thể đã nhận bớt từ lúc lưu nháp).</summary>
    private async Task CheckOrderOnPostAsync(InventoryDocument document, CancellationToken cancellationToken)
    {
        if (document.PurchaseOrderId is not { } orderId) return;
        var order = await LoadReceivableOrderAsync(orderId, document.SupplierId ?? Guid.Empty, cancellationToken);
        await CheckOrderQuantitiesAsync(
            order, document.Lines.Select(l => (l.PurchaseOrderLineId ?? Guid.Empty, l.Qty, l.LineNo)).ToList(), document.Id, cancellationToken);
    }

    /// <summary>
    /// Sau ghi sổ/bỏ ghi sổ phiếu nhập theo PO: mọi dòng PO đã nhận đủ (ĐVT cơ sở) thì PO Đã nhận đủ, ngược lại về Đã duyệt.
    /// </summary>
    /// <param name="document">Phiếu nhập.</param>
    /// <param name="posting">true = đang ghi sổ (cộng phiếu này); false = đang bỏ ghi sổ (không tính phiếu này).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    private async Task SyncOrderStatusAsync(InventoryDocument document, bool posting, CancellationToken cancellationToken)
    {
        if (document.PurchaseOrderId is not { } orderId) return;
        var order = await _db.PurchaseOrders.Include(o => o.Lines).SingleAsync(o => o.Id == orderId, cancellationToken);
        var received = await PurchaseOrderReceipts.ReceivedBaseByLineAsync(_db, order.Lines.Select(l => l.Id).ToList(), document.Id, cancellationToken);
        if (posting)
        {
            foreach (var line in document.Lines.Where(l => l.PurchaseOrderLineId is not null))
                received[line.PurchaseOrderLineId!.Value] = received.GetValueOrDefault(line.PurchaseOrderLineId.Value) + line.BaseQty;
        }

        if (order.Lines.All(l => received.GetValueOrDefault(l.Id) >= l.BaseQty)) order.CloseReceived();
        else order.ReopenReceiving();
    }

    /// <summary>PO nhận hàng được: cùng công ty, Đã duyệt (chưa nhận đủ, chưa hủy), đúng NCC của phiếu nhập.</summary>
    private async Task<PurchaseOrder> LoadReceivableOrderAsync(Guid orderId, Guid supplierId, CancellationToken cancellationToken)
    {
        var order = await _db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đơn mua hàng.");
        if (order.Status != ApprovalDocumentStatus.Approved)
            throw new BusinessRuleException($"Đơn mua hàng {order.No} chưa duyệt, đã nhận đủ hoặc đã hủy – không nhập hàng theo đơn này được.");
        if (order.SupplierId != supplierId) throw new BusinessRuleException($"Nhà cung cấp của phiếu nhập phải là NCC của đơn {order.No}.");
        return order;
    }

    /// <summary>
    /// SL nhận (đã ghi sổ ở phiếu khác + phiếu này) không vượt SL đặt × (1 + dung sai %), tính theo ĐVT cơ sở.
    /// Ví dụ đặt 100, dung sai 5%, đã nhận 60 → phiếu này nhận tối đa 45.
    /// </summary>
    private async Task CheckOrderQuantitiesAsync(
        PurchaseOrder order, IReadOnlyList<(Guid PoLineId, decimal Qty, int LineNo)> lines, Guid? documentId, CancellationToken cancellationToken)
    {
        var received = await PurchaseOrderReceipts.ReceivedBaseByLineAsync(_db, order.Lines.Select(l => l.Id).ToList(), documentId, cancellationToken);
        foreach (var group in lines.GroupBy(l => l.PoLineId))
        {
            var poLine = order.Lines.FirstOrDefault(p => p.Id == group.Key)
                ?? throw new BusinessRuleException($"Dòng {group.First().LineNo}: phải chọn một dòng của đơn mua hàng {order.No}.");
            var thisDocument = group.Sum(l => decimal.Round(l.Qty * poLine.UomFactor, 4, MidpointRounding.AwayFromZero));
            var allowed = decimal.Round(poLine.BaseQty * (100m + order.ReceiveTolerancePct) / 100m, 4, MidpointRounding.AwayFromZero);
            var remaining = allowed - received.GetValueOrDefault(poLine.Id);
            if (thisDocument > remaining)
                throw new BusinessRuleException(
                    $"Dòng {group.First().LineNo}: vượt SL còn nhận được của đơn {order.No} dòng {poLine.LineNo} " +
                    $"({Math.Max(0, remaining / poLine.UomFactor):0.####}, dung sai {order.ReceiveTolerancePct:0.##}%).");
        }
    }
}
