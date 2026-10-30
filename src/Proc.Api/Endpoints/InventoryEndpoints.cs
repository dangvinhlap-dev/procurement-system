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

        return app;
    }
}
