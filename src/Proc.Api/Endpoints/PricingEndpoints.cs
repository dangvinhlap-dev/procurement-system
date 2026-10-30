using Proc.Api.Security;
using Proc.Api.Validation;
using Proc.Application.Pricing;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>API bảng giá nội bộ (design.md mục 4, 6.3).</summary>
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
        return app;
    }
}
