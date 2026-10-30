using Proc.Api.Security;
using Proc.Api.Validation;
using Proc.Application.Inventory;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// Endpoint kho: kỳ kho (design.md mục 7, 7.6), chứng từ kho (mục 6, 10). Sổ kho, tồn kho thêm ở các lát sau của giai đoạn 2.
/// </summary>
public static class InventoryEndpoints
{
    /// <summary>
    /// Khai báo các endpoint.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var periods = app.MapGroup("/api/inventory-periods").WithTags("Kỳ kho");
        periods.MapGet("/", (int? year, IInventoryPeriodService s, CancellationToken ct) => s.GetCalendarAsync(year, ct))
            .RequirePermission(Permissions.Inventory.PeriodView);
        periods.MapPost("/years", (CreateInventoryYearRequest request, IInventoryPeriodService s, CancellationToken ct) =>
                s.CreateYearAsync(request, ct))
            .RequirePermission(Permissions.Inventory.PeriodOpen)
            .Validate<CreateInventoryYearRequest>();
        periods.MapPost("/{id:guid}/open", (Guid id, IInventoryPeriodService s, CancellationToken ct) => s.OpenAsync(id, ct))
            .RequirePermission(Permissions.Inventory.PeriodOpen);
        periods.MapPost("/{id:guid}/close", (Guid id, IInventoryPeriodService s, CancellationToken ct) => s.CloseAsync(id, ct))
            .RequirePermission(Permissions.Inventory.PeriodClose);
        periods.MapPost("/{id:guid}/reopen", (Guid id, ReopenPeriodRequest request, IInventoryPeriodService s, CancellationToken ct) =>
                s.ReopenAsync(id, request, ct))
            .RequirePermission(Permissions.Inventory.PeriodReopen);

        var documents = app.MapGroup("/api/inventory-documents").WithTags("Chứng từ kho");
        documents.MapGet("/", (string docType, DateOnly? from, DateOnly? to, string? status, IInventoryDocumentService s, CancellationToken ct) =>
                s.SearchAsync(new InventoryDocumentQuery(docType, from, to, status), ct))
            .RequirePermission(Permissions.Inventory.DocumentView);
        documents.MapGet("/{id:guid}", (Guid id, IInventoryDocumentService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.Inventory.DocumentView);
        documents.MapPost("/{id:guid}/cancel", (Guid id, IInventoryDocumentService s, CancellationToken ct) => s.CancelAsync(id, ct))
            .RequirePermission(Permissions.Inventory.DocumentCreate);
        documents.MapPost("/{id:guid}/post", (Guid id, IInventoryDocumentService s, CancellationToken ct) => s.PostAsync(id, ct))
            .RequirePermission(Permissions.Inventory.DocumentPost);
        documents.MapPost("/{id:guid}/unpost", (Guid id, IInventoryDocumentService s, CancellationToken ct) => s.UnpostAsync(id, ct))
            .RequirePermission(Permissions.Inventory.DocumentUnpost);

        var receipts = app.MapGroup("/api/goods-receipts").WithTags("Chứng từ kho");
        receipts.MapPost("/", async (SaveReceiptRequest request, IInventoryDocumentService s, CancellationToken ct) =>
            {
                var created = await s.CreateReceiptAsync(request, ct);
                return Results.Created($"/api/inventory-documents/{created.Id}", created);
            })
            .RequirePermission(Permissions.Inventory.DocumentCreate)
            .Validate<SaveReceiptRequest>();
        receipts.MapPut("/{id:guid}", (Guid id, SaveReceiptRequest request, IInventoryDocumentService s, CancellationToken ct) =>
                s.UpdateReceiptAsync(id, request, ct))
            .RequirePermission(Permissions.Inventory.DocumentCreate)
            .Validate<SaveReceiptRequest>();

        receipts.MapGet("/{id:guid}/returnable", (Guid id, Guid? excludeDocumentId, IInventoryDocumentService s, CancellationToken ct) =>
                s.GetReturnableLinesAsync(id, excludeDocumentId, ct))
            .RequirePermission(Permissions.Inventory.DocumentView);

        var returns = app.MapGroup("/api/goods-returns").WithTags("Chứng từ kho");
        returns.MapPost("/", async (SaveReturnRequest request, IInventoryDocumentService s, CancellationToken ct) =>
            {
                var created = await s.CreateReturnAsync(request, ct);
                return Results.Created($"/api/inventory-documents/{created.Id}", created);
            })
            .RequirePermission(Permissions.Inventory.DocumentCreate)
            .Validate<SaveReturnRequest>();
        returns.MapPut("/{id:guid}", (Guid id, SaveReturnRequest request, IInventoryDocumentService s, CancellationToken ct) =>
                s.UpdateReturnAsync(id, request, ct))
            .RequirePermission(Permissions.Inventory.DocumentCreate)
            .Validate<SaveReturnRequest>();

        var transfers = app.MapGroup("/api/stock-transfers").WithTags("Chứng từ kho");
        transfers.MapPost("/", async (SaveTransferRequest request, IInventoryDocumentService s, CancellationToken ct) =>
            {
                var created = await s.CreateTransferAsync(request, ct);
                return Results.Created($"/api/inventory-documents/{created.Id}", created);
            })
            .RequirePermission(Permissions.Inventory.DocumentCreate)
            .Validate<SaveTransferRequest>();
        transfers.MapPut("/{id:guid}", (Guid id, SaveTransferRequest request, IInventoryDocumentService s, CancellationToken ct) =>
                s.UpdateTransferAsync(id, request, ct))
            .RequirePermission(Permissions.Inventory.DocumentCreate)
            .Validate<SaveTransferRequest>();

        var sales = app.MapGroup("/api/intercompany-sales").WithTags("Bán nội bộ");
        sales.MapPost("/", async (SaveSaleRequest request, IInventoryDocumentService s, CancellationToken ct) =>
            {
                var created = await s.CreateSaleAsync(request, ct);
                return Results.Created($"/api/inventory-documents/{created.Id}", created);
            })
            .RequirePermission(Permissions.Inventory.DocumentCreate)
            .Validate<SaveSaleRequest>();
        sales.MapPut("/{id:guid}", (Guid id, SaveSaleRequest request, IInventoryDocumentService s, CancellationToken ct) =>
                s.UpdateSaleAsync(id, request, ct))
            .RequirePermission(Permissions.Inventory.DocumentCreate)
            .Validate<SaveSaleRequest>();
        app.MapPut("/api/intercompany-receipts/{id:guid}", (Guid id, ConfirmIntercompanyReceiptRequest request, IInventoryDocumentService s, CancellationToken ct) =>
                s.ConfirmIntercompanyReceiptAsync(id, request, ct))
            .WithTags("Bán nội bộ")
            .RequirePermission(Permissions.Inventory.DocumentCreate)
            .Validate<ConfirmIntercompanyReceiptRequest>();
        app.MapGet("/api/reports/goods-in-transit", (IInventoryDocumentService s, CancellationToken ct) => s.GetGoodsInTransitAsync(ct))
            .WithTags("Bán nội bộ")
            .RequirePermission(Permissions.Report.StockView);
        app.MapGet("/api/reports/intercompany-reconciliation", (DateOnly from, DateOnly to, IInventoryDocumentService s, CancellationToken ct) =>
                s.GetReconciliationAsync(from, to, ct))
            .WithTags("Bán nội bộ")
            .RequirePermission(Permissions.Report.StockView);

        // Tồn khả dụng hiện trên dòng phiếu xuất/chuyển: người lập phiếu có quyền xem chứng từ kho là đủ.
        app.MapGet("/api/stock-on-hand/available", (Guid warehouseId, Guid[]? itemIds, IInventoryDocumentService s, CancellationToken ct) =>
                s.GetAvailableStockAsync(warehouseId, itemIds ?? [], ct))
            .WithTags("Chứng từ kho")
            .RequirePermission(Permissions.Inventory.DocumentView);

        // Tồn kho hiện tại, thẻ kho (design.md mục 11): quyền xem tồn kho; phạm vi kho, nhóm hàng do global query filter.
        var stock = app.MapGroup("/api").WithTags("Tồn kho");
        stock.MapGet("/stock-on-hand", (Guid? warehouseId, Guid? categoryId, bool? includeZero, IStockQueryService s, CancellationToken ct) =>
                s.GetOnHandAsync(new StockOnHandQuery(warehouseId, categoryId, includeZero ?? false), ct))
            .RequirePermission(Permissions.Inventory.StockView);
        stock.MapGet("/reports/stock-movement", (Guid periodId, Guid? warehouseId, Guid? categoryId, IStockQueryService s, CancellationToken ct) =>
                s.GetMovementAsync(new StockMovementQuery(periodId, warehouseId, categoryId), ct))
            .RequirePermission(Permissions.Report.StockView);
        stock.MapGet("/stock-card", (Guid itemId, Guid? warehouseId, DateOnly from, DateOnly to, IStockQueryService s, CancellationToken ct) =>
                s.GetStockCardAsync(new StockCardQuery(itemId, warehouseId, from, to), ct))
            .RequirePermission(Permissions.Inventory.StockView);

        return app;
    }
}
