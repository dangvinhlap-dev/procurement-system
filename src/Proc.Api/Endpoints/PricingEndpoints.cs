using Microsoft.AspNetCore.Mvc;
using Proc.Api.Security;
using Proc.Api.Validation;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>API bảng giá nội bộ (design.md mục 4, 6.3), bảng giá NCC và so sánh giá (mục 8.1, 8.2).</summary>
public static class PricingEndpoints
{
    /// <summary>Kích thước file nhập Excel tối đa (như nhập hợp đồng).</summary>
    private const long MaxFileBytes = 10 * 1024 * 1024;

    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

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
        // Bảng giá nội bộ (IPR) chọn công ty mua trong danh sách công ty của tập đoàn.
        app.MapGet("/api/group-companies", (IInternalPriceAdministration s, CancellationToken ct) => s.OtherCompaniesAsync(ct))
            .WithTags("Giá nội bộ")
            .RequirePermission(Permissions.Pricing.PriceListView);

        var lists = app.MapGroup("/api/price-lists").WithTags("Bảng giá NCC");
        // Người lập hợp đồng nguyên tắc cần danh sách bảng giá để gắn bảng giá dùng chung (lát B4a).
        lists.MapGet("/", (IPriceListService s, CancellationToken ct) => s.ListAsync(ct))
            .RequireAnyPermission(Permissions.Pricing.PriceListView, Permissions.Purchase.ContractManage);
        // Người lập PO cần giá gợi ý dù không có quyền xem bảng giá.
        lists.MapGet("/resolve", (Guid supplierId, DateOnly date, Guid[]? itemIds, IPriceListService s, CancellationToken ct) =>
                s.ResolveAsync(supplierId, date, itemIds ?? [], ct))
            .RequireAnyPermission(Permissions.Pricing.PriceListView, Permissions.Purchase.PoCreate);
        lists.MapGet("/{id:guid}", (Guid id, IPriceListService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequireAnyPermission(Permissions.Pricing.PriceListView, Permissions.Purchase.ContractManage);
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
        // Bảng giá phải duyệt mới hiệu lực (lát B1, design.md mục 8.5): gửi duyệt/rút lại cần quyền sửa, duyệt/từ chối cần quyền duyệt.
        lists.MapPost("/{id:guid}/submit", (Guid id, IPriceListService s, CancellationToken ct) => s.SubmitAsync(id, ct))
            .RequirePermission(Permissions.Pricing.PriceListManage);
        lists.MapPost("/{id:guid}/recall", (Guid id, IPriceListService s, CancellationToken ct) => s.RecallAsync(id, ct))
            .RequirePermission(Permissions.Pricing.PriceListManage);
        lists.MapPost("/{id:guid}/approve", (Guid id, ApprovalActionRequest request, IPriceListService s, CancellationToken ct) => s.ApproveAsync(id, request, ct))
            .RequirePermission(Permissions.Pricing.PriceListApprove);
        lists.MapPost("/{id:guid}/reject", (Guid id, ApprovalActionRequest request, IPriceListService s, CancellationToken ct) => s.RejectAsync(id, request, ct))
            .RequirePermission(Permissions.Pricing.PriceListApprove);
        lists.MapPost("/{id:guid}/deactivate", (Guid id, IPriceListService s, CancellationToken ct) => s.DeactivateAsync(id, ct))
            .RequirePermission(Permissions.Pricing.PriceListManage);
        // Nhập Excel bảng giá NCC (design.md mục 8.5): nhập thành bảng giá nháp của công ty đang làm việc, quyền như lập bảng giá.
        lists.MapGet("/import-template", (IPriceListImportService s) =>
            {
                var file = s.GetTemplate();
                return Results.File(file.Content, XlsxContentType, file.FileName);
            })
            .RequirePermission(Permissions.Pricing.PriceListManage);
        lists.MapPost("/import", async (bool? commit, IFormFile file, IPriceListImportService s, CancellationToken ct) =>
            {
                if (file.Length == 0) return Results.Problem(title: "File rỗng.", statusCode: StatusCodes.Status400BadRequest);
                if (file.Length > MaxFileBytes)
                    return Results.Problem(title: "File quá 10 MB. Hãy chia nhỏ file.", statusCode: StatusCodes.Status400BadRequest);
                await using var stream = file.OpenReadStream();
                return Results.Ok(await s.ImportAsync(stream, commit ?? false, ct));
            })
            .RequirePermission(Permissions.Pricing.PriceListManage)
            // API dùng JWT trong header, không dùng cookie, nên không có nguy cơ CSRF – tắt kiểm tra antiforgery cho form tải file.
            .DisableAntiforgery();

        app.MapGet("/api/price-comparison", (Guid itemId, bool? allCompanies, decimal? thresholdPct, IPriceComparisonService s, CancellationToken ct) =>
                s.CompareAsync(itemId, allCompanies ?? false, Math.Clamp(thresholdPct ?? 10m, 0m, 1000m), ct))
            .WithTags("So sánh giá")
            .RequirePermission(Permissions.Pricing.CompareView);
        // Phiếu duyệt NCC và giá tháng (design.md mục 18.25 – lát G2): tổng công ty lập, Ban cung ứng duyệt theo quy trình loại GTH.
        var monthly = app.MapGroup("/api/monthly-supplier-approvals").WithTags("Duyệt NCC và giá tháng");
        monthly.MapGet("/", (IMonthlySupplierApprovalService s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.Pricing.MonthlyApprovalView);
        monthly.MapGet("/companies", (IMonthlySupplierApprovalService s, CancellationToken ct) => s.CompaniesAsync(ct))
            .RequirePermission(Permissions.Pricing.MonthlyApprovalView);
        monthly.MapGet("/{id:guid}", (Guid id, IMonthlySupplierApprovalService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.Pricing.MonthlyApprovalView);
        monthly.MapPost("/", async (SaveMonthlyApprovalRequest request, IMonthlySupplierApprovalService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/monthly-supplier-approvals/{created.Id}", created);
            })
            .RequirePermission(Permissions.Pricing.MonthlyApprovalManage)
            .Validate<SaveMonthlyApprovalRequest>();
        monthly.MapPut("/{id:guid}", (Guid id, SaveMonthlyApprovalRequest request, IMonthlySupplierApprovalService s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Pricing.MonthlyApprovalManage)
            .Validate<SaveMonthlyApprovalRequest>();
        monthly.MapPost("/{id:guid}/submit", (Guid id, IMonthlySupplierApprovalService s, CancellationToken ct) => s.SubmitAsync(id, ct))
            .RequirePermission(Permissions.Pricing.MonthlyApprovalManage);
        monthly.MapPost("/{id:guid}/recall", (Guid id, IMonthlySupplierApprovalService s, CancellationToken ct) => s.RecallAsync(id, ct))
            .RequirePermission(Permissions.Pricing.MonthlyApprovalManage);
        monthly.MapPost("/{id:guid}/approve", (Guid id, ApprovalActionRequest request, IMonthlySupplierApprovalService s, CancellationToken ct) =>
                s.ApproveAsync(id, request, ct))
            .RequirePermission(Permissions.Pricing.MonthlyApprovalApprove);
        monthly.MapPost("/{id:guid}/reject", (Guid id, ApprovalActionRequest request, IMonthlySupplierApprovalService s, CancellationToken ct) =>
                s.RejectAsync(id, request, ct))
            .RequirePermission(Permissions.Pricing.MonthlyApprovalApprove);
        monthly.MapPost("/{id:guid}/cancel", (Guid id, CancelMonthlyApprovalRequest request, IMonthlySupplierApprovalService s, CancellationToken ct) =>
                s.CancelAsync(id, request, ct))
            .RequirePermission(Permissions.Pricing.MonthlyApprovalManage)
            .Validate<CancelMonthlyApprovalRequest>();
        monthly.MapPost("/{id:guid}/adjust", async (Guid id, IMonthlySupplierApprovalService s, CancellationToken ct) =>
            {
                var created = await s.AdjustAsync(id, ct);
                return Results.Created($"/api/monthly-supplier-approvals/{created.Id}", created);
            })
            .RequirePermission(Permissions.Pricing.MonthlyApprovalManage);
        monthly.MapPost("/{id:guid}/copy-next-month", async (Guid id, IMonthlySupplierApprovalService s, CancellationToken ct) =>
            {
                var created = await s.CopyNextMonthAsync(id, ct);
                return Results.Created($"/api/monthly-supplier-approvals/{created.Id}", created);
            })
            .RequirePermission(Permissions.Pricing.MonthlyApprovalManage);

        // Thỏa thuận chiết khấu bậc sản lượng gộp (design.md mục 18.25 – lát G6a): tổng công ty lập, Ban cung ứng duyệt theo quy trình loại CKB;
        // theo dõi lũy kế các công ty so với bậc, cảnh báo sắp chạm/hụt bậc, chia chiết khấu theo tỷ lệ doanh số.
        var rebates = app.MapGroup("/api/volume-rebates").WithTags("Chiết khấu sản lượng gộp");
        rebates.MapGet("/", (IVolumeRebateService s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.Pricing.VolumeRebateView);
        rebates.MapGet("/tracking", (decimal? nearPercent, bool? onlyAlerts, IVolumeRebateService s, CancellationToken ct) =>
                s.TrackingAsync(nearPercent, onlyAlerts ?? false, ct))
            .RequirePermission(Permissions.Pricing.VolumeRebateView);
        rebates.MapGet("/{id:guid}", (Guid id, IVolumeRebateService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.Pricing.VolumeRebateView);
        rebates.MapGet("/{id:guid}/progress", (Guid id, decimal? nearPercent, IVolumeRebateService s, CancellationToken ct) =>
                s.ProgressAsync(id, nearPercent, ct))
            .RequirePermission(Permissions.Pricing.VolumeRebateView);
        rebates.MapPost("/", async (SaveVolumeRebateRequest request, IVolumeRebateService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/volume-rebates/{created.Id}", created);
            })
            .RequirePermission(Permissions.Pricing.VolumeRebateManage)
            .Validate<SaveVolumeRebateRequest>();
        rebates.MapPut("/{id:guid}", (Guid id, SaveVolumeRebateRequest request, IVolumeRebateService s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Pricing.VolumeRebateManage)
            .Validate<SaveVolumeRebateRequest>();
        rebates.MapPost("/{id:guid}/submit", (Guid id, IVolumeRebateService s, CancellationToken ct) => s.SubmitAsync(id, ct))
            .RequirePermission(Permissions.Pricing.VolumeRebateManage);
        rebates.MapPost("/{id:guid}/recall", (Guid id, IVolumeRebateService s, CancellationToken ct) => s.RecallAsync(id, ct))
            .RequirePermission(Permissions.Pricing.VolumeRebateManage);
        rebates.MapPost("/{id:guid}/approve", (Guid id, ApprovalActionRequest request, IVolumeRebateService s, CancellationToken ct) =>
                s.ApproveAsync(id, request, ct))
            .RequirePermission(Permissions.Pricing.VolumeRebateApprove);
        rebates.MapPost("/{id:guid}/reject", (Guid id, ApprovalActionRequest request, IVolumeRebateService s, CancellationToken ct) =>
                s.RejectAsync(id, request, ct))
            .RequirePermission(Permissions.Pricing.VolumeRebateApprove);
        rebates.MapPost("/{id:guid}/cancel", (Guid id, CancelVolumeRebateRequest request, IVolumeRebateService s, CancellationToken ct) =>
                s.CancelAsync(id, request, ct))
            .RequirePermission(Permissions.Pricing.VolumeRebateManage)
            .Validate<CancelVolumeRebateRequest>();
        rebates.MapPost("/{id:guid}/copy", async (Guid id, IVolumeRebateService s, CancellationToken ct) =>
            {
                var created = await s.CopyAsync(id, ct);
                return Results.Created($"/api/volume-rebates/{created.Id}", created);
            })
            .RequirePermission(Permissions.Pricing.VolumeRebateManage);
        // Thực nhận chiết khấu theo kỳ đã kết thúc (lát G6b): tổng công ty ghi, chia phần cho công ty áp dụng theo tỷ lệ doanh số.
        rebates.MapPost("/{id:guid}/receipts", (Guid id, SaveVolumeRebateReceiptRequest request, IVolumeRebateService s, CancellationToken ct) =>
                s.RecordReceiptAsync(id, request, ct))
            .RequirePermission(Permissions.Pricing.VolumeRebateManage)
            .Validate<SaveVolumeRebateReceiptRequest>();
        rebates.MapPost("/receipts/{receiptId:guid}/cancel",
                (Guid receiptId, CancelVolumeRebateRequest request, IVolumeRebateService s, CancellationToken ct) => s.CancelReceiptAsync(receiptId, request, ct))
            .RequirePermission(Permissions.Pricing.VolumeRebateManage)
            .Validate<CancelVolumeRebateRequest>();

        // Phiếu khảo sát giá thị trường – ngân hàng giá (design.md mục 18.25 – lát G4a): Văn phòng Ban lập ở tổng công ty, chốt, không qua duyệt.
        var surveys = app.MapGroup("/api/market-price-surveys").WithTags("Khảo sát giá thị trường");
        surveys.MapGet("/", (IMarketPriceSurveyService s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.Pricing.PriceBankView);
        surveys.MapGet("/prices", ([FromQuery] Guid[]? itemIds, int? days, IMarketPriceSurveyService s, CancellationToken ct) =>
                s.PricesAsync(itemIds ?? [], days ?? 180, ct))
            .RequirePermission(Permissions.Pricing.PriceBankView);
        surveys.MapGet("/{id:guid}", (Guid id, IMarketPriceSurveyService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.Pricing.PriceBankView);
        surveys.MapPost("/", async (SaveMarketSurveyRequest request, IMarketPriceSurveyService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/market-price-surveys/{created.Id}", created);
            })
            .RequirePermission(Permissions.Pricing.PriceBankManage)
            .Validate<SaveMarketSurveyRequest>();
        surveys.MapPut("/{id:guid}", (Guid id, SaveMarketSurveyRequest request, IMarketPriceSurveyService s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Pricing.PriceBankManage)
            .Validate<SaveMarketSurveyRequest>();
        surveys.MapPost("/{id:guid}/confirm", (Guid id, IMarketPriceSurveyService s, CancellationToken ct) => s.ConfirmAsync(id, ct))
            .RequirePermission(Permissions.Pricing.PriceBankManage);
        surveys.MapPost("/{id:guid}/cancel", (Guid id, CancelMarketSurveyRequest request, IMarketPriceSurveyService s, CancellationToken ct) =>
                s.CancelAsync(id, request, ct))
            .RequirePermission(Permissions.Pricing.PriceBankManage)
            .Validate<CancelMarketSurveyRequest>();

        return app;
    }
}
