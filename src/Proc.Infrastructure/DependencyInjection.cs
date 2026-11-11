using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Proc.Application.Administration;
using Proc.Application.Auth;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Excel;
using Proc.Application.Inventory;
using Proc.Application.Logging;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Infrastructure.Administration;
using Proc.Infrastructure.Documents;
using Proc.Infrastructure.Excel;
using Proc.Infrastructure.Identity;
using Proc.Infrastructure.Inventory;
using Proc.Infrastructure.Logging;
using Proc.Infrastructure.MasterData;
using Proc.Infrastructure.Organization;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Security;

namespace Proc.Infrastructure;

/// <summary>
/// Đăng ký các dịch vụ của lớp Infrastructure vào DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Tên health check kiểm tra kết nối PostgreSQL.</summary>
    public const string DatabaseHealthCheckName = "database";

    /// <summary>Số lần nhập sai mật khẩu liên tiếp trước khi khóa tạm tài khoản.</summary>
    public const int MaxFailedLoginAttempts = 5;

    /// <summary>Thời gian khóa tạm sau khi nhập sai quá số lần cho phép.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Đăng ký DbContext (PostgreSQL), Identity, dịch vụ đăng nhập, đồng hồ và health check CSDL.
    /// Cấu hình JwtOptions, DatabaseOptions, SeedOptions do lớp Api bind từ appsettings.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="connectionString">Chuỗi kết nối PostgreSQL.</param>
    /// <returns>Chính <paramref name="services"/> để gọi nối tiếp.</returns>
    /// <exception cref="ArgumentException">Khi chuỗi kết nối rỗng.</exception>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton<IClock, SystemClock>();
        // Api thay bằng người dùng lấy từ token; khi chạy ngoài HTTP (khởi tạo dữ liệu) thì không có người dùng.
        services.TryAddScoped<ICurrentUser, NoCurrentUser>();
        services.TryAddScoped<IRequestContext, NoRequestContext>();
        services.AddScoped<AuditFieldsInterceptor>();
        services.AddScoped<AuditLogInterceptor>();
        services.AddScoped<IExpectedVersion, ExpectedVersion>();
        services.AddScoped<VersionCheckInterceptor>();
        services.AddScoped<IAccessLog, AccessLogWriter>();
        services.AddScoped<ILogQueries, LogQueries>();
        services.AddScoped<IChangeHistoryQueries, ChangeHistoryQueries>();
        services.AddScoped<IDocumentNumberService, DocumentNumberService>();
        services.AddScoped<Proc.Application.Organization.ISystemParameterService, Proc.Infrastructure.Organization.SystemParameterService>();
        services.AddScoped<IInventoryPeriodService, InventoryPeriodService>();
        services.AddScoped<IInventoryDocumentService, InventoryDocumentService>();
        services.AddScoped<IStockQueryService, StockQueryService>();
        services.AddScoped<Proc.Application.Purchasing.IApprovalRuleAdministration, Proc.Infrastructure.Purchasing.ApprovalRuleAdministration>();
        services.AddScoped<Proc.Application.Purchasing.IPurchaseRequestService, Proc.Infrastructure.Purchasing.PurchaseRequestService>();
        services.AddScoped<Proc.Application.Purchasing.IPurchaseOrderService, Proc.Infrastructure.Purchasing.PurchaseOrderService>();
        services.AddScoped<Proc.Application.Common.ICancelChainService, Proc.Infrastructure.Purchasing.CancelChainService>();
        services.AddScoped<Proc.Application.Purchasing.IPooledOrderService, Proc.Infrastructure.Purchasing.PooledOrderService>();
        services.AddScoped<Proc.Application.Purchasing.IBudgetService, Proc.Infrastructure.Purchasing.BudgetService>();
        services.AddScoped<Proc.Application.Purchasing.IConstructionProjectService, Proc.Infrastructure.Purchasing.ConstructionProjectService>();
        services.AddScoped<Proc.Application.Purchasing.IPurchasingUnitService, Proc.Infrastructure.Purchasing.PurchasingUnitService>();
        services.AddScoped<Proc.Application.Purchasing.IPurchaseRequestNewItemService, Proc.Infrastructure.Purchasing.PurchaseRequestNewItemService>();
        services.AddScoped<Proc.Application.Sourcing.IRfqAwardPriceReader, Proc.Infrastructure.Sourcing.RfqAwardPriceReader>();
        services.AddScoped<Proc.Application.Printing.IDocumentPdfService, Proc.Infrastructure.Printing.DocumentPdfService>();
        services.AddScoped<Proc.Application.Sourcing.ISourcingService, Proc.Infrastructure.Sourcing.SourcingService>();
        services.AddScoped<Proc.Application.Sourcing.IRfqService, Proc.Infrastructure.Sourcing.RfqService>();
        services.AddScoped<Proc.Application.Purchasing.IContractService, Proc.Infrastructure.Purchasing.ContractService>();
        services.AddScoped<Proc.Application.Purchasing.IContractRebateService, Proc.Infrastructure.Purchasing.ContractRebateService>();
        services.AddScoped<Proc.Application.Purchasing.IContractAmendmentService, Proc.Infrastructure.Purchasing.ContractService>();
        services.AddScoped<Proc.Application.Purchasing.IContractImportService, Proc.Infrastructure.Purchasing.ContractImportService>();
        services.AddScoped<Proc.Application.Pricing.IPriceListImportService, Proc.Infrastructure.Pricing.PriceListImportService>();
        services.AddScoped<Proc.Application.Purchasing.IApprovalPreviewService, Proc.Infrastructure.Purchasing.ApprovalPreviewService>();

        // Một đối tượng phạm vi cho mỗi request: UserScopeLoader nạp, DbContext (filter) và Application đọc.
        services.AddScoped<CurrentUserScope>();
        services.AddScoped<ICurrentUserScope>(sp => sp.GetRequiredService<CurrentUserScope>());
        services.AddScoped<UserScopeLoader>();
        services.AddScoped<ICompanyScopeRunner, CompanyScopeRunner>();
        services.AddScoped<IOrganizationQueries, OrganizationQueries>();
        services.AddScoped<ICompanyAdministration, CompanyAdministration>();
        services.AddScoped<IWarehouseAdministration, WarehouseAdministration>();
        services.AddScoped<IDepartmentAdministration, DepartmentAdministration>();
        services.AddScoped<IDivisionAdministration, DivisionAdministration>();
        services.AddScoped<Proc.Application.Purchasing.IApprovalDelegationService, Proc.Infrastructure.Purchasing.ApprovalDelegationService>();
        services.AddScoped<Proc.Application.Purchasing.IApprovalReminderService, Proc.Infrastructure.Purchasing.ApprovalReminderService>();
        services.AddScoped<IUomAdministration, UomAdministration>();
        services.AddScoped<Proc.Application.Pricing.IInternalPriceAdministration, Proc.Infrastructure.Pricing.InternalPriceAdministration>();
        services.AddScoped<Proc.Application.Pricing.IPriceListService, Proc.Infrastructure.Pricing.PriceListService>();
        services.AddScoped<Proc.Application.Pricing.IPriceComparisonService, Proc.Infrastructure.Pricing.PriceComparisonService>();
        services.AddScoped<IItemCategoryAdministration, ItemCategoryAdministration>();
        services.AddScoped<IItemAdministration, ItemAdministration>();
        services.AddScoped<ISupplierGroupAdministration, SupplierGroupAdministration>();
        services.AddScoped<ISupplierAdministration, SupplierAdministration>();
        services.AddScoped<ISupplierChangeRequestService, SupplierChangeRequestService>();
        services.AddScoped<IConflictDeclarationService, ConflictDeclarationService>();
        services.AddScoped<ISoleSourceService, SoleSourceService>();
        services.AddScoped<Proc.Application.Pricing.IMonthlySupplierApprovalService, Proc.Infrastructure.Pricing.MonthlySupplierApprovalService>();
        services.AddScoped<Proc.Application.Pricing.IVolumeRebateService, Proc.Infrastructure.Pricing.VolumeRebateService>();
        services.AddScoped<Proc.Application.Pricing.IMarketPriceSurveyService, Proc.Infrastructure.Pricing.MarketPriceSurveyService>();
        services.AddScoped<Proc.Application.Purchasing.IDemandPlanService, Proc.Infrastructure.Purchasing.DemandPlanService>();
        services.AddScoped<ISupplierLinkAdministration, SupplierLinkAdministration>();
        services.AddScoped<ISupplierRatingAdministration, SupplierRatingAdministration>();
        services.AddScoped<CatalogNumberAllocator>();
        services.AddScoped<ICatalogNumberingAdministration, CatalogNumberingAdministration>();
        // Nhập/xuất Excel: mỗi danh mục một lớp; ExcelTransfer chọn theo mã danh mục.
        services.AddScoped<ExcelCatalog, UomExcelCatalog>();
        services.AddScoped<ExcelCatalog, ItemCategoryExcelCatalog>();
        services.AddScoped<ExcelCatalog, ItemExcelCatalog>();
        services.AddScoped<ExcelCatalog, SupplierGroupExcelCatalog>();
        services.AddScoped<ExcelCatalog, SupplierExcelCatalog>();
        services.AddScoped<IGridExcelExporter, GridExcelExporter>();
        services.AddScoped<Proc.Application.Reports.IStockReportService, Proc.Infrastructure.Reports.StockReportService>();
        services.AddScoped<Proc.Application.Reports.IPurchaseReportService, Proc.Infrastructure.Reports.PurchaseReportService>();
        services.AddScoped<Proc.Application.Reports.IKpiService, Proc.Infrastructure.Reports.KpiService>();
        services.AddScoped<Proc.Application.Reports.IBoardMonthlyReportService, Proc.Infrastructure.Reports.BoardMonthlyReportService>();
        services.AddScoped<Proc.Application.Documents.IDocumentRegistryService, Proc.Infrastructure.Documents.DocumentRegistryService>();
        services.AddScoped<Proc.Application.Control.IControlAnomalyService, Proc.Infrastructure.Control.ControlAnomalyService>();
        services.AddScoped<Proc.Application.Control.IControlFindingService, Proc.Infrastructure.Control.ControlFindingService>();
        services.AddScoped<Proc.Application.MasterData.ISupplierReviewService, Proc.Infrastructure.MasterData.SupplierReviewService>();
        services.AddScoped<Proc.Application.MasterData.ISupplierOnboardingService, Proc.Infrastructure.MasterData.SupplierOnboardingService>();
        services.AddScoped<Proc.Application.Purchasing.IReceiptConfirmationService, Proc.Infrastructure.Purchasing.ReceiptConfirmationService>();
        services.AddScoped<Proc.Application.Reports.IDashboardService, Proc.Infrastructure.Reports.DashboardService>();
        services.AddScoped<Proc.Application.Reports.IAccountingExportService, Proc.Infrastructure.Reports.AccountingExportService>();
        services.AddScoped<Proc.Application.Notifications.INotificationService, Proc.Infrastructure.Notifications.NotificationService>();
        // Email bản sao thông báo (lát O2b): bộ gửi SMTP + dịch vụ quét; dịch vụ nền đăng ký ở Api cùng cấu hình mục Email.
        services.AddSingleton<Proc.Application.Notifications.IEmailSender, Proc.Infrastructure.Notifications.SmtpEmailSender>();
        services.AddScoped<Proc.Application.Notifications.INotificationEmailService, Proc.Infrastructure.Notifications.NotificationEmailService>();
        services.AddScoped<Proc.Application.Analysis.IPurchaseHistoryService, Proc.Infrastructure.Analysis.PurchaseHistoryService>();
        services.AddScoped<Proc.Application.Analysis.IPurchaseHistoryMappingService, Proc.Infrastructure.Analysis.PurchaseHistoryMappingService>();
        services.AddScoped<Proc.Application.Analysis.IPurchaseAnalysisService, Proc.Infrastructure.Analysis.PurchaseAnalysisService>();
        services.AddScoped<Proc.Application.Documents.IAttachmentService, Proc.Infrastructure.Documents.AttachmentService>();
        services.AddScoped<Proc.Application.Documents.IAppraisalOpinionService, Proc.Infrastructure.Documents.AppraisalOpinionService>();
        services.AddScoped<IExcelTransfer>(sp => new ExcelTransfer(
            sp.GetRequiredService<ProcDbContext>(), sp.GetRequiredService<ICurrentUserScope>(), sp.GetServices<ExcelCatalog>()));
        services.AddScoped<IUserAdministration, UserAdministration>();
        services.AddScoped<IRoleAdministration, RoleAdministration>();

        services.AddDbContext<ProcDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            // Thứ tự quan trọng: so phiên bản (If-Match) trước hết, điền CreatedAt/UpdatedAt, rồi mới sinh nhật ký.
            .AddInterceptors(
                sp.GetRequiredService<VersionCheckInterceptor>(), sp.GetRequiredService<AuditFieldsInterceptor>(),
                sp.GetRequiredService<AuditLogInterceptor>()));

        services.AddIdentityCore<AppUser>(options =>
            {
                // Quy tắc mật khẩu: tối thiểu 8 ký tự, có chữ thường và chữ số; không bắt buộc chữ hoa/ký tự đặc biệt
                // để người dùng dễ nhớ mà vẫn đủ mạnh khi kết hợp khóa tạm.
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                // Khóa tạm khi đăng nhập sai nhiều lần (design.md mục 2.2: "khóa tài khoản").
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = MaxFailedLoginAttempts;
                options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;

                options.User.RequireUniqueEmail = false;
            })
            .AddEntityFrameworkStores<ProcDbContext>()
            .AddErrorDescriber<VietnameseIdentityErrorDescriber>();

        services.AddScoped<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        // Health check thực sự mở kết nối tới CSDL, không chỉ kiểm tra cấu hình.
        services.AddHealthChecks()
            .AddDbContextCheck<ProcDbContext>(DatabaseHealthCheckName);

        return services;
    }

    /// <summary>Đồng hồ thật của hệ thống.</summary>
    private sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    /// <summary>Không có người dùng (chạy nền).</summary>
    private sealed class NoCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;

        public string? UserName => null;
    }

    /// <summary>Không có request HTTP (chạy nền).</summary>
    private sealed class NoRequestContext : IRequestContext
    {
        public string? IpAddress => null;

        public string? UserAgent => null;

        public string? CorrelationId => null;
    }
}
