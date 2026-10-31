using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Inventory;

/// <summary>
/// Bán nội bộ giữa hai công ty (design.md mục 4, 6.3, 10.7, 10.8): phiếu xuất bán (ICS) ở công ty bán, phiếu nhập mua (ICR) ở công
/// ty mua, liên kết hai chiều. Chỗ duy nhất đọc/ghi chứng từ của công ty khác công ty đang làm việc là phiếu liên kết của đúng cặp
/// ICS–ICR (bỏ qua filter công ty có chủ đích, theo Id liên kết) – không mở rộng ra truy vấn khác.
/// </summary>
public sealed partial class InventoryDocumentService
{
    /// <summary>Bộ lọc bỏ qua khi đọc phiếu liên kết của công ty bên kia.</summary>
    private static readonly string[] CrossCompany = [ProcDbContext.CompanyFilter, ProcDbContext.WarehouseFilter];

    /// <inheritdoc />
    public async Task<InventoryDocumentDto> CreateSaleAsync(SaveSaleRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        await CheckSaleHeaderAsync(request.FromWarehouseId, request.BuyerCompanyId, cancellationToken);
        var lines = await ResolveSaleLinesAsync(request, cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.IntercompanySale, request.Date, cancellationToken);
        var document = InventoryDocument.CreateSale(companyId, no, ToSaleHeader(request));
        document.SetLines(lines);
        _db.InventoryDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(document.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InventoryDocumentDto> UpdateSaleAsync(Guid id, SaveSaleRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var document = await LoadAsync(id, DocumentTypes.IntercompanySale, cancellationToken);
        await EnsureAllItemsVisibleAsync(document, cancellationToken);
        await CheckSaleHeaderAsync(request.FromWarehouseId, request.BuyerCompanyId, cancellationToken);
        var lines = await ResolveSaleLinesAsync(request, cancellationToken);
        document.UpdateSale(ToSaleHeader(request));
        document.SetLines(lines);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InventoryDocumentDto> ConfirmIntercompanyReceiptAsync(
        Guid id, ConfirmIntercompanyReceiptRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var receipt = await LoadAsync(id, DocumentTypes.IntercompanyReceipt, cancellationToken);
        await EnsureAllItemsVisibleAsync(receipt, cancellationToken);
        var sale = await LinkedSaleAsync(receipt, cancellationToken);
        receipt.ConfirmIntercompanyReceipt(
            request.Date, sale.Date, request.Note,
            request.Lines.Select(l => new ReceiptConfirmLine(l.LineId, l.ReceivedQty, l.LotNo, l.Note)).ToList(),
            sale.Lines.ToDictionary(l => l.Id, l => l.Qty));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GoodsInTransitDto>> GetGoodsInTransitAsync(CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        // Hai chiều của công ty đang làm việc: phiếu bán ra đã ghi sổ (bên mua chưa nhận) và phiếu bán của công ty khác cho mình.
        var sales = await _db.InventoryDocuments.AsNoTracking().IgnoreQueryFilters(CrossCompany)
            .Where(d => d.DocType == DocumentTypes.IntercompanySale && d.Status == DocumentStatus.Posted
                        && (d.CompanyId == companyId || d.CounterCompanyId == companyId))
            .Select(d => new { d.Id, d.No, d.Date, d.CompanyId, d.CounterCompanyId, d.LinkedDocumentId, d.TotalAmount, d.TotalVat })
            .ToListAsync(cancellationToken);
        var receiptIds = sales.Select(s => s.LinkedDocumentId).OfType<Guid>().ToList();
        var receipts = await _db.InventoryDocuments.AsNoTracking().IgnoreQueryFilters(CrossCompany)
            .Where(d => receiptIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => new { d.No, d.Status }, cancellationToken);
        var codes = await _db.Companies.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);
        return sales
            .Where(s => s.LinkedDocumentId is not { } r || !receipts.TryGetValue(r, out var receipt) || receipt.Status != DocumentStatus.Posted)
            .OrderBy(s => s.Date).ThenBy(s => s.No)
            .Select(s =>
            {
                var receipt = s.LinkedDocumentId is { } r ? receipts.GetValueOrDefault(r) : null;
                return new GoodsInTransitDto(
                    s.CompanyId == companyId ? "Out" : "In", s.Id, s.No, s.Date, codes[s.CompanyId], codes[s.CounterCompanyId!.Value],
                    s.LinkedDocumentId, receipt?.No, s.TotalAmount, s.TotalVat);
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IntercompanyReconciliationDto>> GetReconciliationAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        if (from > to) throw new BusinessRuleException("Từ ngày phải trước hoặc bằng đến ngày.");
        // Chỉ các cặp có công ty đang làm việc là một bên; số tổng hợp, không trả dòng chi tiết của công ty khác.
        var sales = await _db.InventoryDocuments.AsNoTracking().IgnoreQueryFilters(CrossCompany)
            .Where(d => d.DocType == DocumentTypes.IntercompanySale && d.Status == DocumentStatus.Posted && d.Date >= from && d.Date <= to
                        && (d.CompanyId == companyId || d.CounterCompanyId == companyId))
            .Select(d => new { d.CompanyId, Counter = d.CounterCompanyId!.Value, d.LinkedDocumentId, d.TotalAmount })
            .ToListAsync(cancellationToken);
        var receiptIds = sales.Select(s => s.LinkedDocumentId).OfType<Guid>().ToList();
        var received = await _db.InventoryDocuments.AsNoTracking().IgnoreQueryFilters(CrossCompany)
            .Where(d => receiptIds.Contains(d.Id) && d.Status == DocumentStatus.Posted)
            .ToDictionaryAsync(d => d.Id, d => d.TotalAmount, cancellationToken);
        decimal Received(Guid? id) => id is { } r ? received.GetValueOrDefault(r) : 0m;

        var companies = await _db.Companies.AsNoTracking().ToDictionaryAsync(c => c.Id, c => new { c.Code, c.Name }, cancellationToken);
        return sales
            .GroupBy(s => s.CompanyId == companyId ? s.Counter : s.CompanyId)
            .Select(g =>
            {
                var mine = g.Where(s => s.CompanyId == companyId).ToList();
                var theirs = g.Where(s => s.CompanyId != companyId).ToList();
                var counter = companies[g.Key];
                return new IntercompanyReconciliationDto(
                    g.Key, counter.Code, counter.Name,
                    mine.Sum(s => s.TotalAmount), mine.Sum(s => Received(s.LinkedDocumentId)),
                    theirs.Sum(s => s.TotalAmount), theirs.Sum(s => Received(s.LinkedDocumentId)));
            })
            .OrderBy(r => r.CounterCompanyCode)
            .ToList();
    }

    /// <summary>
    /// Kho xuất là kho tổng đang dùng của công ty bán (trong phạm vi người lập); công ty mua đang hoạt động, khác công ty bán, có kho
    /// tổng đang dùng – liên công ty chỉ đi kho tổng → kho tổng (CLAUDE.md mục 4).
    /// </summary>
    /// <returns>Kho tổng của công ty mua.</returns>
    private async Task<Guid> CheckSaleHeaderAsync(Guid fromWarehouseId, Guid buyerCompanyId, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var warehouse = await _db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.Id == fromWarehouseId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho xuất (hoặc kho ngoài phạm vi của bạn).");
        if (!warehouse.IsActive) throw new BusinessRuleException($"Kho {warehouse.Code} đã ngừng dùng.");
        if (warehouse.Type != WarehouseType.Main)
            throw new BusinessRuleException($"Bán nội bộ chỉ xuất từ kho tổng; kho {warehouse.Code} là kho phụ – chuyển về kho tổng trước (design.md mục 4).");
        if (buyerCompanyId == companyId) throw new BusinessRuleException("Công ty mua phải khác công ty bán – trong một công ty dùng phiếu chuyển kho.");
        if (!await _db.Companies.AnyAsync(c => c.Id == buyerCompanyId && c.IsActive, cancellationToken))
            throw new NotFoundException("Không tìm thấy công ty mua (hoặc công ty đã ngừng hoạt động).");
        var buyerMain = await _db.Warehouses.AsNoTracking().IgnoreQueryFilters(CrossCompany)
            .Where(w => w.CompanyId == buyerCompanyId && w.Type == WarehouseType.Main && w.IsActive)
            .Select(w => (Guid?)w.Id).SingleOrDefaultAsync(cancellationToken);
        return buyerMain ?? throw new BusinessRuleException("Công ty mua chưa có kho tổng đang dùng để nhận hàng.");
    }

    /// <summary>
    /// Dòng phiếu bán: mặt hàng qua filter nhóm hàng; đơn giá = giá bán nội bộ theo ĐVT cơ sở × hệ số ĐVT trên phiếu (làm tròn 6 số
    /// lẻ), thuế suất theo bảng giá – không nhận giá từ client. Ví dụ giá 15.000 đ/cái, ĐVT hộp 10 cái → 150.000 đ/hộp.
    /// </summary>
    private async Task<List<(InventoryLineData Data, Item Item)>> ResolveSaleLinesAsync(SaveSaleRequest request, CancellationToken cancellationToken)
    {
        var ids = request.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Include(i => i.Conversions).Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        var prices = (await _prices.ResolveAsync(request.BuyerCompanyId, request.Date, ids, cancellationToken)).ToDictionary(p => p.ItemId);
        return request.Lines.Select((l, index) =>
        {
            var item = items.GetValueOrDefault(l.ItemId)
                ?? throw new NotFoundException($"Dòng {index + 1}: không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).");
            var price = prices.GetValueOrDefault(l.ItemId)
                ?? throw new BusinessRuleException($"Dòng {index + 1} ({item.Code}): chưa có giá bán nội bộ hiệu lực ngày {request.Date:dd/MM/yyyy} – khai báo ở Bảng giá nội bộ.");
            var factor = l.UomId == item.BaseUomId ? 1m : item.Conversions.FirstOrDefault(c => c.UomId == l.UomId)?.Factor ?? 1m;
            var unitPrice = Math.Round(price.UnitPrice * factor, 6, MidpointRounding.AwayFromZero);
            return (new InventoryLineData(l.Id, l.ItemId, l.UomId, l.Qty, unitPrice, price.VatCode, l.LotNo, null, l.Note), item);
        }).ToList();
    }

    /// <summary>Phiếu xuất bán liên kết của một phiếu nhập mua nội bộ (thuộc công ty bán – đọc theo Id liên kết).</summary>
    private async Task<InventoryDocument> LinkedSaleAsync(InventoryDocument receipt, CancellationToken cancellationToken)
    {
        var sale = await _db.InventoryDocuments.AsNoTracking().IgnoreQueryFilters(CrossCompany).Include(d => d.Lines)
            .SingleOrDefaultAsync(d => d.Id == receipt.LinkedDocumentId && d.DocType == DocumentTypes.IntercompanySale, cancellationToken);
        if (sale is null || sale.Status != DocumentStatus.Posted || sale.LinkedDocumentId != receipt.Id)
            throw new BusinessRuleException($"Phiếu xuất bán của phiếu {receipt.No} không còn ghi sổ – không nhận hàng được.");
        return sale;
    }

    /// <summary>Phiếu liên kết của công ty bên kia (ICS ↔ ICR) kèm dòng, hoặc null.</summary>
    private async Task<InventoryDocument?> CounterpartAsync(InventoryDocument document, CancellationToken cancellationToken) =>
        document.LinkedDocumentId is { } linked && document.DocType is DocumentTypes.IntercompanySale or DocumentTypes.IntercompanyReceipt
            ? await _db.InventoryDocuments.IgnoreQueryFilters(CrossCompany).Include(d => d.Lines).SingleOrDefaultAsync(d => d.Id == linked, cancellationToken)
            : null;

    /// <summary>
    /// Trạng thái nhận hàng (design.md mục 10.7): Pending = chờ nhận; Full = đã nhận đủ; Short = nhận thiếu.
    /// </summary>
    private static string? ReceiveStatusOf(InventoryDocument? sale, InventoryDocument? receipt)
    {
        if (sale is null || receipt is null || receipt.Status == DocumentStatus.Cancelled) return null;
        if (receipt.Status == DocumentStatus.Draft) return "Pending";
        var received = receipt.Lines.Where(l => l.SourceLineId != null).GroupBy(l => l.SourceLineId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.BaseQty));
        return sale.Lines.All(l => received.GetValueOrDefault(l.Id) >= l.BaseQty) ? "Full" : "Short";
    }

    private static SaleHeader ToSaleHeader(SaveSaleRequest r) =>
        new(r.Date, r.FromWarehouseId, r.BuyerCompanyId, r.InvoiceNo, r.InvoiceDate, r.Deliverer, r.Note);

    /// <summary>
    /// Báo thủ kho công ty mua có phiếu nhập mua nội bộ chờ nhận (design.md mục 6.3 bước 3, 11.6): người có quyền lập chứng từ kho
    /// (quyền xác nhận nhận hàng) tại công ty mua và được làm việc với kho tổng nhận hàng.
    /// </summary>
    /// <param name="sale">Phiếu xuất bán vừa ghi sổ.</param>
    /// <param name="receipt">Phiếu nhập mua nội bộ vừa sinh ở công ty mua.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    private async Task NotifyIntercompanyReceiptAsync(InventoryDocument sale, InventoryDocument receipt, CancellationToken cancellationToken)
    {
        var notifications = new Notifications.NotificationPublisher(_db, _scope, _clock);
        var keepers = await notifications.UsersWithPermissionAsync(
            receipt.CompanyId, Permissions.Inventory.DocumentCreate, null, receipt.ToWarehouseId, cancellationToken);
        var seller = await _db.Companies.AsNoTracking().Where(c => c.Id == sale.CompanyId).Select(c => c.Code + " – " + c.Name).SingleAsync(cancellationToken);
        notifications.Add(receipt.CompanyId, keepers, Proc.Domain.Notifications.NotificationTypes.IntercompanyReceipt,
            $"Phiếu nhập mua nội bộ {receipt.No} chờ nhận hàng",
            $"{seller} đã xuất bán nội bộ theo phiếu {sale.No}. Phiếu {receipt.No} đang chờ bạn xác nhận số lượng thực nhận và ghi sổ.",
            DocumentTypes.IntercompanyReceipt, receipt.Id);
    }
}
