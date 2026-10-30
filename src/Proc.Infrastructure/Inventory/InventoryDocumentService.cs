using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Inventory;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Inventory;

/// <summary>
/// Chứng từ kho (design.md mục 6, 10.1, 10.4). Chứng từ, kho, mặt hàng đều đọc qua global query filter: kho ngoài phạm vi,
/// hàng ngoài nhóm được phép như không tồn tại – người lập không thể đưa chúng vào phiếu.
/// </summary>
public sealed class InventoryDocumentService : IInventoryDocumentService
{
    /// <summary>Số phiếu tối đa mỗi lần tìm.</summary>
    public const int SearchLimit = 2000;

    /// <summary>Mã hàng hiển thị cho dòng có mặt hàng ngoài phạm vi nhóm hàng của người xem.</summary>
    public const string HiddenItem = "(ngoài phạm vi)";

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IDocumentNumberService _numbers;
    private readonly IInventoryPeriodService _periods;
    private readonly IClock _clock;

    /// <summary>
    /// Tạo dịch vụ.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    /// <param name="numbers">Cấp số chứng từ.</param>
    /// <param name="periods">Kỳ kho (kiểm tra kỳ mở khi ghi sổ).</param>
    /// <param name="clock">Đồng hồ (khoảng ngày mặc định khi tìm, thời điểm ghi sổ).</param>
    public InventoryDocumentService(
        ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IInventoryPeriodService periods, IClock clock)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
        _periods = periods;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<InventoryDocumentListResult> SearchAsync(InventoryDocumentQuery query, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var docType = CodeRules.Normalize(query.DocType);
        var to = query.To ?? _clock.TodayInVietnam();
        var from = query.From ?? to.AddDays(-90);
        var documents = _db.InventoryDocuments.AsNoTracking().Where(d => d.DocType == docType && d.Date >= from && d.Date <= to);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = Enum.TryParse<DocumentStatus>(query.Status, ignoreCase: true, out var s) && Enum.IsDefined(s)
                ? s
                : throw new BusinessRuleException("Trạng thái phải là Draft, Posted hoặc Cancelled.");
            documents = documents.Where(d => d.Status == status);
        }

        var rows = await (
                from d in documents
                join w in _db.Warehouses on (d.ToWarehouseId ?? d.FromWarehouseId) equals w.Id into wh
                from w in wh.DefaultIfEmpty()
                join s in _db.Suppliers on d.SupplierId equals s.Id into sup
                from s in sup.DefaultIfEmpty()
                orderby d.Date descending, d.No descending
                select new InventoryDocumentListItemDto(
                    d.Id, d.DocType, d.No, d.Date, d.Status.ToString(), w == null ? null : w.Code, w == null ? null : w.Name,
                    s == null ? null : s.Code, s == null ? null : s.Name, d.InvoiceNo, d.TotalAmount, d.TotalVat, d.Lines.Count))
            .Take(SearchLimit + 1)
            .ToListAsync(cancellationToken);
        return new InventoryDocumentListResult(rows.Take(SearchLimit).ToList(), rows.Count > SearchLimit, SearchLimit);
    }

    /// <inheritdoc />
    public async Task<InventoryDocumentDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var document = await _db.InventoryDocuments.AsNoTracking().Include(d => d.Lines)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chứng từ (hoặc chứng từ thuộc kho ngoài phạm vi của bạn).");
        return await ToDtoAsync(document, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InventoryDocumentDto> CreateReceiptAsync(SaveReceiptRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        await CheckReceiptHeaderAsync(request, cancellationToken);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);

        // Cấp số và lưu phiếu trong cùng giao dịch: lưu lỗi thì số được trả lại, không nhảy số (design.md mục 5.5).
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.GoodsReceipt, request.Date, cancellationToken);
        var document = InventoryDocument.CreateReceipt(companyId, no, ToHeader(request));
        document.SetLines(lines);
        _db.InventoryDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(document.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InventoryDocumentDto> UpdateReceiptAsync(Guid id, SaveReceiptRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var document = await LoadAsync(id, DocumentTypes.GoodsReceipt, cancellationToken);
        await EnsureAllItemsVisibleAsync(document, cancellationToken);
        await CheckReceiptHeaderAsync(request, cancellationToken);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);

        document.UpdateReceipt(ToHeader(request));
        document.SetLines(lines);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InventoryDocumentDto> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var document = await _db.InventoryDocuments.Include(d => d.Lines).SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chứng từ.");
        document.Cancel();
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InventoryDocumentDto> PostAsync(Guid id, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var document = await _db.InventoryDocuments.Include(d => d.Lines).SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chứng từ.");
        if (document.DocType != DocumentTypes.GoodsReceipt)
            throw new BusinessRuleException($"Chưa hỗ trợ ghi sổ chứng từ loại {document.DocType}.");
        if (document.Status != DocumentStatus.Draft)
            throw new BusinessRuleException($"Phiếu {document.No} không ở trạng thái nháp nên không ghi sổ được.");
        await EnsureAllItemsVisibleAsync(document, cancellationToken);

        // Kiểm tra lại lúc ghi sổ: từ lúc lưu nháp tới giờ NCC có thể bị tạm ngưng, kho ngừng dùng, mặt hàng ngừng dùng.
        await CheckReceiptHeaderAsync(ToRequest(document), cancellationToken);
        var periodId = await _periods.RequireOpenPeriodAsync(document.Date, cancellationToken);
        var itemIds = document.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        foreach (var line in document.Lines.Where(l => !items[l.ItemId].IsActive))
            throw new BusinessRuleException($"Dòng {line.LineNo}: mặt hàng {items[line.ItemId].Code} đã ngừng dùng.");

        var warehouse = await _db.Warehouses.AsNoTracking().SingleAsync(w => w.Id == document.ToWarehouseId, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        document.MarkPosted(periodId, _clock.UtcNow, _scope.UserId);
        foreach (var line in document.Lines)
        {
            _db.StockLedger.Add(StockLedgerEntry.Receipt(document, line, periodId));
            await ApplyStockAsync(companyId, warehouse, items[line.ItemId], line.LotNo, line.BaseQty, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InventoryDocumentDto> UnpostAsync(Guid id, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var document = await _db.InventoryDocuments.Include(d => d.Lines).SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chứng từ.");
        if (document.Status != DocumentStatus.Posted) throw new BusinessRuleException($"Phiếu {document.No} chưa ghi sổ.");
        await EnsureAllItemsVisibleAsync(document, cancellationToken);

        // Chỉ bỏ ghi sổ khi kỳ còn mở; kỳ đã đóng thì lập chứng từ điều chỉnh ở kỳ đang mở (CLAUDE.md mục 4).
        var period = await _db.InventoryPeriods.AsNoTracking().SingleAsync(p => p.Id == document.PeriodId, cancellationToken);
        if (period.Status != PeriodStatus.Open)
            throw new BusinessRuleException($"Kỳ kho {period.Label} không còn mở nên không bỏ ghi sổ phiếu {document.No} được. Lập chứng từ điều chỉnh ở kỳ đang mở.");

        var entries = await _db.StockLedger.Where(x => x.DocumentId == id).ToListAsync(cancellationToken);
        var warehouseIds = entries.Select(e => e.WarehouseId).Distinct().ToList();
        var warehouses = await _db.Warehouses.AsNoTracking().Where(w => warehouseIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, cancellationToken);
        var itemIds = entries.Select(e => e.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var entry in entries)
            await ApplyStockAsync(companyId, warehouses[entry.WarehouseId], items[entry.ItemId], entry.LotNo, -entry.SignedQty, cancellationToken);

        _db.StockLedger.RemoveRange(entries);
        document.MarkUnposted();
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Cộng (delta &gt; 0) hoặc trừ (delta &lt; 0) tồn tức thời bằng MỘT câu SQL nguyên tử: tạo dòng tồn nếu chưa có, ngược lại cộng
    /// dồn, trả về số tồn mới. PostgreSQL khóa dòng tồn tới hết giao dịch, nên hai người ghi sổ cùng mặt hàng cùng kho phải lần lượt
    /// – không mất số, và kiểm tra tồn âm dựa trên số tồn đúng (design.md mục 12.2: khóa dòng StockOnHand).
    /// Câu lệnh ghi thẳng theo CompanyId/WarehouseId/ItemId đã kiểm tra phạm vi ở trên (kho, mặt hàng đọc qua filter).
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi tồn sau khi trừ âm và kho không cho phép tồn âm (cả giao dịch bị hủy).</exception>
    private async Task ApplyStockAsync(
        Guid companyId, Warehouse warehouse, Item item, string? lotNo, decimal delta, CancellationToken cancellationToken)
    {
        var rows = await _db.Database
            .SqlQuery<decimal>($"""
                INSERT INTO "StockOnHand" ("Id", "CompanyId", "WarehouseId", "ItemId", "LotNo", "Qty", "ReservedQty")
                VALUES ({Guid.CreateVersion7()}, {companyId}, {warehouse.Id}, {item.Id}, {lotNo}, {delta}, 0)
                ON CONFLICT ("CompanyId", "WarehouseId", "ItemId", "LotNo")
                DO UPDATE SET "Qty" = "StockOnHand"."Qty" + EXCLUDED."Qty"
                RETURNING "Qty" AS "Value"
                """)
            .ToListAsync(cancellationToken);
        var qty = rows.Single();
        // Không cho tồn âm trừ khi tham số kho cho phép (CLAUDE.md mục 4).
        if (qty < 0 && !warehouse.AllowNegativeStock)
        {
            var lot = lotNo is null ? string.Empty : $", lô {lotNo}";
            var shown = qty.ToString("#,##0.####", VietnameseCulture);
            throw new BusinessRuleException($"Tồn {item.Code}{lot} tại kho {warehouse.Code} sẽ âm ({shown}). Kho không cho phép tồn âm.");
        }
    }

    private static readonly System.Globalization.CultureInfo VietnameseCulture = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");

    private static SaveReceiptRequest ToRequest(InventoryDocument d) =>
        new(d.Date, d.ToWarehouseId ?? Guid.Empty, d.SupplierId ?? Guid.Empty, d.InvoiceNo, d.InvoiceSeries, d.InvoiceDate,
            d.DeliveryNoteNo, d.Deliverer, d.Note, []);

    /// <summary>
    /// Kho nhập thuộc công ty, trong phạm vi, đang dùng; NCC đã duyệt và đang áp dụng tại công ty (design.md mục 6.1, 5.9).
    /// </summary>
    private async Task CheckReceiptHeaderAsync(SaveReceiptRequest request, CancellationToken cancellationToken)
    {
        var warehouse = await _db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho nhập (hoặc kho ngoài phạm vi của bạn).");
        if (!warehouse.IsActive) throw new BusinessRuleException($"Kho {warehouse.Code} đã ngừng dùng.");

        var supplier = await _db.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhà cung cấp.");
        if (supplier.Status != SupplierStatus.Approved)
            throw new BusinessRuleException($"NCC {supplier.Code} chưa ở trạng thái Đã duyệt nên chưa nhập mua được.");
        // SupplierCompanies tự lọc theo công ty đang làm việc: có dòng đang giao dịch = công ty này đang mua của NCC đó.
        if (!await _db.SupplierCompanies.AnyAsync(sc => sc.SupplierId == supplier.Id && sc.IsActive, cancellationToken))
            throw new BusinessRuleException($"NCC {supplier.Code} chưa được áp dụng tại công ty này (Nhà cung cấp → tab Áp dụng tại …).");
    }

    /// <summary>Đọc mặt hàng của các dòng qua filter phạm vi nhóm hàng; hàng ngoài phạm vi báo như không tồn tại.</summary>
    private async Task<List<(InventoryLineData Data, Item Item)>> ResolveLinesAsync(
        IReadOnlyList<SaveInventoryLineRequest> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        return lines
            .Select((l, index) => (
                new InventoryLineData(l.Id, l.ItemId, l.UomId, l.Qty, l.UnitPrice, l.VatCode, l.LotNo, l.ExpiryDate, l.Note),
                items.GetValueOrDefault(l.ItemId)
                ?? throw new NotFoundException($"Dòng {index + 1}: không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).")))
            .ToList();
    }

    /// <summary>
    /// Sửa phiếu là thay toàn bộ dòng; nếu phiếu có dòng hàng người sửa không được xem thì lưu sẽ xóa mất dòng đó – chặn lại.
    /// </summary>
    private async Task EnsureAllItemsVisibleAsync(InventoryDocument document, CancellationToken cancellationToken)
    {
        var ids = document.Lines.Select(l => l.ItemId).Distinct().ToList();
        var visible = await _db.Items.AsNoTracking().CountAsync(i => ids.Contains(i.Id), cancellationToken);
        if (visible < ids.Count)
            throw new ForbiddenAccessException("Phiếu có mặt hàng ngoài phạm vi nhóm hàng của bạn nên bạn không sửa được.");
    }

    private async Task<InventoryDocument> LoadAsync(Guid id, string docType, CancellationToken cancellationToken)
    {
        var document = await _db.InventoryDocuments.Include(d => d.Lines).SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chứng từ.");
        if (document.DocType != docType) throw new BusinessRuleException($"Chứng từ {document.No} không phải loại {docType}.");
        return document;
    }

    private async Task<InventoryDocumentDto> ToDtoAsync(InventoryDocument d, CancellationToken cancellationToken)
    {
        var itemIds = d.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Code, i.Name, i.BaseUomId }).ToDictionaryAsync(i => i.Id, cancellationToken);
        var uomIds = d.Lines.Select(l => l.UomId).Concat(items.Values.Select(i => i.BaseUomId)).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var supplier = d.SupplierId is { } sid
            ? await _db.Suppliers.AsNoTracking().Where(s => s.Id == sid).Select(s => new { s.Code, s.Name }).SingleOrDefaultAsync(cancellationToken)
            : null;
        var createdBy = d.CreatedBy is { } uid
            ? await _db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken)
            : null;

        var lines = d.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var item = items.GetValueOrDefault(l.ItemId);
            return new InventoryLineDto(
                l.Id, l.LineNo, l.ItemId, item?.Code ?? HiddenItem, item?.Name ?? string.Empty, l.UomId, uoms.GetValueOrDefault(l.UomId) ?? "",
                l.Qty, l.UomFactor, l.BaseQty, item is null ? "" : uoms.GetValueOrDefault(item.BaseUomId) ?? "", l.UnitPrice, l.Amount,
                l.VatCode, l.VatAmount, l.LotNo, l.ExpiryDate, l.Note);
        }).ToList();

        string? readOnlyReason = null;
        if (items.Count < itemIds.Count) readOnlyReason = "Phiếu có mặt hàng ngoài phạm vi nhóm hàng của bạn nên chỉ xem được.";
        else if (!_scope.HasPermission(Permissions.Inventory.DocumentCreate)) readOnlyReason = "Bạn không có quyền lập, sửa chứng từ kho.";
        var canEdit = d.Status == DocumentStatus.Draft && readOnlyReason is null;
        var itemsVisible = items.Count == itemIds.Count;
        var canPost = d.Status == DocumentStatus.Draft && itemsVisible && _scope.HasPermission(Permissions.Inventory.DocumentPost);
        var canUnpost = d.Status == DocumentStatus.Posted && itemsVisible && _scope.HasPermission(Permissions.Inventory.DocumentUnpost);

        return new InventoryDocumentDto(
            d.Id, d.DocType, d.No, d.Date, d.Status.ToString(), d.FromWarehouseId, d.ToWarehouseId, d.SupplierId, supplier?.Code, supplier?.Name,
            d.InvoiceNo, d.InvoiceSeries, d.InvoiceDate, d.DeliveryNoteNo, d.Deliverer, d.Note, d.TotalAmount, d.TotalVat, d.CreatedAt,
            createdBy, d.PostedAt, lines, canEdit, readOnlyReason, canPost, canUnpost);
    }

    private static ReceiptHeader ToHeader(SaveReceiptRequest r) =>
        new(r.Date, r.WarehouseId, r.SupplierId, r.InvoiceNo, r.InvoiceSeries, r.InvoiceDate, r.DeliveryNoteNo, r.Deliverer, r.Note);
}
