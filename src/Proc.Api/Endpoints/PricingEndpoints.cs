using Proc.Api.Security;
using Proc.Api.Validation;
using Proc.Application.Pricing;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>API bảng giá nội bộ (design.md mục 4, 6.3), bảng giá NCC và so sánh giá (mục 8.1, 8.2).</summary>
public static class PricingEndpoints
{
    /// <summary>Đăng ký các endpoint.</summary>
    /// <param name="app">Ứng dụng.</param>
    /// <returns>Ứng dụng (để nối tiếp).</returns>
    public static IEndpointRouteBuilder MapPricingEndpoints(this IEndpointRouteBuilder app)
    {
        var prices = app.MapGroup("/api/internal-prices").WithTags("Giá nội bộ");
        prices.MapGet("/", (IInternalPriceAdministration s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.Pricing.PriceListView);
        prices.MapPost("/", async (SaveInternalPriceRequest request, IInternalPriceAdministration s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/internal-prices/{created.Id}", created);
            })
            .RequirePermission(Permissions.Pricing.PriceListManage)
            .Validate<SaveInternalPriceRequest>();
        prices.MapPut("/{id:guid}", (Guid id, SaveInternalPriceRequest request, IInternalPriceAdministration s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Pricing.PriceListManage)
            .Validate<SaveInternalPriceRequest>();
        // Người lập phiếu xuất bán nội bộ cần giá và danh sách công ty mua dù không có quyền xem bảng giá.
        prices.MapGet("/resolve", (Guid buyerCompanyId, DateOnly date, Guid[]? itemIds, IInternalPriceAdministration s, CancellationToken ct) =>
                s.ResolveAsync(buyerCompanyId, date, itemIds ?? [], ct))
            .RequirePermission(Permissions.Inventory.DocumentView);
        app.MapGet("/api/group-companies", (IInternalPriceAdministration s, CancellationToken ct) => s.OtherCompaniesAsync(ct))
            .WithTags("Giá nội bộ")
            .RequirePermission(Permissions.Inventory.DocumentView);

        var lists = app.MapGroup("/api/price-lists").WithTags("Bảng giá NCC");
        lists.MapGet("/", (IPriceListService s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.Pricing.PriceListView);
        // Người lập PO cần giá gợi ý dù không có quyền xem bảng giá.
        lists.MapGet("/resolve", (Guid supplierId, DateOnly date, Guid[]? itemIds, IPriceListService s, CancellationToken ct) =>
                s.ResolveAsync(supplierId, date, itemIds ?? [], ct))
            .RequireAnyPermission(Permissions.Pricing.PriceListView, Permissions.Purchase.PoCreate);
        lists.MapGet("/{id:guid}", (Guid id, IPriceListService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.Pricing.PriceListView);
        lists.MapPost("/", async (SavePriceListRequest request, IPriceListService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/price-lists/{created.Id}", created);
            })
            .RequirePermission(Permissions.Pricing.PriceListManage)
            .Validate<SavePriceListRequest>();
        lists.MapPut("/{id:guid}", (Guid id, SavePriceListRequest request, IPriceListService s, CancellationToken ct) => s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Pricing.PriceListManage)
            .Validate<SavePriceListRequest>();
        lists.MapPost("/{id:guid}/activate", (Guid id, IPriceListService s, CancellationToken ct) => s.ActivateAsync(id, ct))
            .RequirePermission(Permissions.Pricing.PriceListManage);
        lists.MapPost("/{id:guid}/deactivate", (Guid id, IPriceListService s, CancellationToken ct) => s.DeactivateAsync(id, ct))
            .RequirePermission(Permissions.Pricing.PriceListManage);

        app.MapGet("/api/price-comparison", (Guid itemId, bool? allCompanies, decimal? thresholdPct, IPriceComparisonService s, CancellationToken ct) =>
                s.CompareAsync(itemId, allCompanies ?? false, Math.Clamp(thresholdPct ?? 10m, 0m, 1000m), ct))
            .WithTags("So sánh giá")
            .RequirePermission(Permissions.Pricing.CompareView);
        return app;
    }
}
