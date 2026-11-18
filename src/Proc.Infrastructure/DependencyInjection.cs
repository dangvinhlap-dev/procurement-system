using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Proc.Application.Administration;
using Proc.Application.Auth;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Excel;
using Proc.Application.Logging;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Infrastructure.Administration;
using Proc.Infrastructure.Documents;
using Proc.Infrastructure.Excel;
using Proc.Infrastructure.Identity;
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
        services.AddScoped<Proc.Application.Purchasing.IApprovalRuleAdministration, Proc.Infrastructure.Purchasing.ApprovalRuleAdministration>();
        services.AddScoped<Proc.Application.Purchasing.IPurchaseRequestService, Proc.Infrastructure.Purchasing.PurchaseRequestService>();
        services.AddScoped<Proc.Application.Purchasing.IPurchaseOrderService, Proc.Infrastructure.Purchasing.PurchaseOrderService>();
        services.AddScoped<Proc.Application.Purchasing.IInternalSaleService, Proc.Infrastructure.Purchasing.InternalSaleService>();
        services.AddScoped<Proc.Application.Common.ICancelChainService, Proc.Infrastructure.Purchasing.CancelChainService>();
        services.AddScoped<Proc.Application.Purchasing.IPooledOrderService, Proc.Infrastructure.Purchasing.PooledOrderService>();
        services.AddScoped<Proc.Application.Purchasing.IBudgetService, Proc.Infrastructure.Purchasing.BudgetService>();
        services.AddScoped<Proc.Application.Purchasing.IConstructionProjectService, Proc.Infrastructure.Purchasing.ConstructionProjectService>();
        services.AddScoped<Proc.Application.Purchasing.IPurchasingUnitService, Proc.Infrastructure.Purchasing.PurchasingUnitService>();
        services.AddScoped<Proc.Application.Purchasing.IPurchaseSummaryService, Proc.Infrastructure.Purchasing.PurchaseSummaryService>();
        services.AddScoped<Proc.Application.Purchasing.IPurchaseRequestNewItemService, Proc.Infrastructure.Purchasing.PurchaseRequestNewItemService>();
        services.AddScoped<Proc.Application.Sourcing.IRfqAwardPriceReader, Proc.Infrastructure.Sourcing.RfqAwardPriceReader>();
        services.AddScoped<Proc.Application.Printing.IDocumentPdfService, Proc.Infrastructure.Printing.DocumentPdfService>();
        services.AddScoped<Proc.Application.Sourcing.ISourcingService, Proc.Infrastructure.Sourcing.SourcingService>();
        services.AddScoped<Proc.Application.Sourcing.IRfqService, Proc.Infrastructure.Sourcing.RfqService>();
        services.AddScoped<Proc.Application.Sourcing.IRfqClarificationService, Proc.Infrastructure.Sourcing.RfqClarificationService>();
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
        // Lát P2a: đề nghị cập nhật hồ sơ NCC gửi từ Portal – phía nội bộ.
        services.AddScoped<ISupplierProfileRequestService, SupplierProfileRequestService>();
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
        services.AddScoped<Proc.Application.Reports.IPurchaseAccountingExportService, Proc.Infrastructure.Reports.PurchaseAccountingExportService>();
        services.AddScoped<Proc.Application.Notifications.INotificationService, Proc.Infrastructure.Notifications.NotificationService>();
        // Email bản sao thông báo (lát O2b): bộ gửi SMTP + dịch vụ quét; dịch vụ nền đăng ký ở Api cùng cấu hình mục Email.
        // Mọi thư đi qua chốt chuyển hướng (máy TEST gửi về hộp thư test – lát QT1, design.md mục 21.3 E5).
        services.AddSingleton<Proc.Infrastructure.Notifications.SmtpEmailSender>();
        services.AddSingleton<Proc.Application.Notifications.IEmailSender>(sp => new Proc.Infrastructure.Notifications.RedirectingEmailSender(
            sp.GetRequiredService<Proc.Infrastructure.Notifications.SmtpEmailSender>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Proc.Infrastructure.Notifications.EmailOptions>>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Proc.Application.Operations.AppEnvironmentOptions>>()));
        // Module Quản trị vận hành (lát QT1, design.md mục 21).
        services.AddScoped<Proc.Application.Operations.IOperationLog, Proc.Infrastructure.Operations.OperationLogWriter>();
        services.AddScoped<Proc.Application.Operations.IOperationLogQueries, Proc.Infrastructure.Operations.OperationLogQueries>();
        services.AddScoped<Proc.Application.Operations.ISystemOverview, Proc.Infrastructure.Operations.SystemOverviewService>();
        services.AddScoped<Proc.Application.Operations.IWalArchiveStatus, Proc.Infrastructure.Operations.WalArchiveStatusService>();
        // Sao lưu (lát QT3b): bộ chạy singleton (mỗi lần sao lưu một scope CSDL riêng), tín hiệu đánh thức dịch vụ nền.
        services.AddSingleton<Proc.Infrastructure.Operations.PgToolRunner>();
        services.AddSingleton<Proc.Application.Operations.IBackupQueueSignal, Proc.Infrastructure.Operations.BackupQueueSignal>();
        services.AddSingleton<Proc.Application.Operations.IBackupExecutor, Proc.Infrastructure.Operations.BackupExecutor>();
        services.AddScoped<Proc.Application.Operations.IBackupService, Proc.Infrastructure.Operations.BackupService>();
        services.AddScoped<Proc.Application.Operations.IBackupScheduler, Proc.Infrastructure.Operations.BackupScheduler>();
        // Bảo trì CSDL (lát QT4): cùng khuôn với sao lưu – bộ chạy singleton, tín hiệu riêng; xác nhận việc nguy hiểm Q6 dùng chung các lát QT sau.
        services.AddSingleton<Proc.Application.Operations.IDbMaintenanceQueueSignal, Proc.Infrastructure.Operations.DbMaintenanceQueueSignal>();
        services.AddSingleton<Proc.Application.Operations.IDbMaintenanceExecutor, Proc.Infrastructure.Operations.DbMaintenanceExecutor>();
        services.AddScoped<Proc.Application.Operations.IDbMaintenanceService, Proc.Infrastructure.Operations.DbMaintenanceService>();
        services.AddScoped<Proc.Application.Operations.IDbMaintenanceScheduler, Proc.Infrastructure.Operations.DbMaintenanceScheduler>();
        // Phục hồi (lát QT6): hàng chờ khôi phục pg_dump, nối lệnh bộ thực thi với lần phục hồi.
        services.AddSingleton<Proc.Application.Operations.IRestoreQueueSignal, Proc.Infrastructure.Operations.RestoreQueueSignal>();
        services.AddSingleton<Proc.Application.Operations.IRestoreExecutor, Proc.Infrastructure.Operations.RestoreExecutor>();
        services.AddScoped<Proc.Application.Operations.IRestoreService, Proc.Infrastructure.Operations.RestoreService>();
        services.AddScoped<Proc.Application.Operations.IRestoreAgentHooks, Proc.Infrastructure.Operations.RestoreAgentHooks>();
        services.AddScoped<Proc.Application.Operations.IDangerousActionGuard, Proc.Infrastructure.Operations.DangerousActionGuard>();
        services.AddScoped<Proc.Application.Operations.IOperationsAccess, Proc.Infrastructure.Operations.OperationsAccess>();
        // Bộ thực thi + chế độ bảo trì (lát QT5): chế độ bảo trì singleton có đệm – middleware hỏi ở mọi request.
        services.AddScoped<Proc.Application.Operations.IAgentAdminService, Proc.Infrastructure.Operations.AgentAdminService>();
        services.AddScoped<Proc.Application.Operations.IAgentGateway, Proc.Infrastructure.Operations.AgentGateway>();
        services.AddSingleton<Proc.Application.Operations.IMaintenanceMode, Proc.Infrastructure.Operations.MaintenanceModeService>();
        // Cấu hình truy cập CFG (lát QT7): bản đệm dùng chung mọi request + dịch vụ màn hình.
        services.AddSingleton<Proc.Application.Operations.IAccessPolicyProvider, Proc.Infrastructure.Operations.AccessPolicyProvider>();
        services.AddScoped<Proc.Application.Operations.IAccessConfigService, Proc.Infrastructure.Operations.AccessConfigService>();
        // Trung tâm phát hành (lát QT8) – một đối tượng cho cả màn hình REL và phần bộ thực thi gọi tới. ReleaseGitHubHttp do Api đăng ký.
        services.AddScoped<Proc.Infrastructure.Operations.ReleaseService>();
        // Làm mới TEST từ PROD, báo lỗi từ TEST (lát QT9). UpstreamHttp do Api đăng ký.
        services.AddScoped<Proc.Infrastructure.Operations.TestRefreshService>();
        services.AddScoped<Proc.Application.Operations.ITestRefreshService>(sp => sp.GetRequiredService<Proc.Infrastructure.Operations.TestRefreshService>());
        services.AddScoped<Proc.Application.Operations.ITestRefreshAgentHooks>(
            sp => sp.GetRequiredService<Proc.Infrastructure.Operations.TestRefreshService>());
        services.AddScoped<Proc.Application.Operations.IReleaseDefectInbox, Proc.Infrastructure.Operations.ReleaseDefectInbox>();
        services.AddScoped<Proc.Application.Operations.ITestFeedbackService, Proc.Infrastructure.Operations.TestFeedbackService>();
        services.AddScoped<Proc.Application.Operations.IReleaseService>(sp => sp.GetRequiredService<Proc.Infrastructure.Operations.ReleaseService>());
        services.AddScoped<Proc.Application.Operations.IReleaseAgentHooks>(
            sp => sp.GetRequiredService<Proc.Infrastructure.Operations.ReleaseService>());
        services.AddSingleton<Proc.Application.Operations.IEnvironmentStatus, Proc.Infrastructure.Operations.EnvironmentStatus>();
        // Nhật ký lỗi (lát QT2): bộ ghi singleton – mỗi lần ghi một scope CSDL riêng, không phụ thuộc DbContext của request vừa lỗi.
        services.AddSingleton<Proc.Application.Operations.IErrorRecorder, Proc.Infrastructure.Operations.ErrorRecorder>();
        services.AddScoped<Proc.Application.Operations.IErrorLogService, Proc.Infrastructure.Operations.ErrorLogService>();
        services.AddSingleton<Proc.Application.Operations.IBackgroundHeartbeat, Proc.Infrastructure.Operations.BackgroundHeartbeat>();
        services.AddScoped<Proc.Application.Notifications.INotificationEmailService, Proc.Infrastructure.Notifications.NotificationEmailService>();
        services.AddScoped<Proc.Application.Analysis.IPurchaseHistoryService, Proc.Infrastructure.Analysis.PurchaseHistoryService>();
        services.AddScoped<Proc.Application.Analysis.IPurchaseHistoryMappingService, Proc.Infrastructure.Analysis.PurchaseHistoryMappingService>();
        services.AddScoped<Proc.Application.Analysis.IPurchaseAnalysisService, Proc.Infrastructure.Analysis.PurchaseAnalysisService>();
        // Lớp cụ thể đăng ký riêng: Portal NCC dùng các hàm file báo giá không qua phạm vi người dùng nội bộ (lát P3b-1).
        services.AddScoped<Proc.Infrastructure.Documents.AttachmentService>();
        services.AddScoped<Proc.Application.Documents.IAttachmentService>(
            sp => sp.GetRequiredService<Proc.Infrastructure.Documents.AttachmentService>());
        services.AddScoped<Proc.Application.Documents.IAppraisalOpinionService, Proc.Infrastructure.Documents.AppraisalOpinionService>();
        services.AddScoped<Proc.Application.Documents.IUserSignatureService, Proc.Infrastructure.Documents.UserSignatureService>();
        services.AddScoped<Proc.Application.Documents.ICompanyLogoService, Proc.Infrastructure.Documents.CompanyLogoService>();
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
                // Quy tắc mật khẩu: có chữ thường và chữ số; không bắt buộc chữ hoa/ký tự đặc biệt để người dùng dễ nhớ mà vẫn đủ mạnh khi kết hợp
                // khóa tạm. Độ dài tối thiểu (≥ 8) và mật khẩu phổ biến do PolicyPasswordValidator kiểm theo cấu hình truy cập CFG (lát QT7) – đặt 1
                // ở đây để không báo hai câu độ dài khác số.
                options.Password.RequiredLength = 1;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                // Khóa tạm khi đăng nhập sai nhiều lần (design.md mục 2.2: "khóa tài khoản"; người dùng chốt 15/11/2026: hết thời gian tự đăng nhập
                // được). Số lần, số phút theo cấu hình truy cập CFG – AuthService tự đếm (lát QT7), không dùng AccessFailedAsync của Identity.
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = false;
            })
            .AddEntityFrameworkStores<ProcDbContext>()
            .AddErrorDescriber<VietnameseIdentityErrorDescriber>()
            .AddPasswordValidator<PolicyPasswordValidator>();

        services.AddScoped<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        // Portal NCC (design.md 19.5, lát P1): tài khoản riêng, token audience riêng.
        services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<Proc.Domain.MasterData.SupplierPortalUser>,
            Microsoft.AspNetCore.Identity.PasswordHasher<Proc.Domain.MasterData.SupplierPortalUser>>();
        services.AddScoped<Proc.Infrastructure.Portal.PortalTokenService>();
        services.AddScoped<Proc.Infrastructure.Portal.SupplierPortalService>();
        services.AddScoped<Proc.Application.Portal.ISupplierPortalAccountService>(
            sp => sp.GetRequiredService<Proc.Infrastructure.Portal.SupplierPortalService>());
        services.AddScoped<Proc.Application.Portal.IPortalAuthService>(
            sp => sp.GetRequiredService<Proc.Infrastructure.Portal.SupplierPortalService>());
        // Lát P3a: thư mời, báo giá online niêm phong.
        services.AddScoped<Proc.Application.Portal.IPortalRfqService, Proc.Infrastructure.Portal.PortalRfqService>();
        services.AddScoped<Proc.Application.Portal.IPortalOrderService, Proc.Infrastructure.Portal.PortalOrderService>();
        services.AddScoped<Proc.Application.Portal.IPortalDeliveryNoticeService, Proc.Infrastructure.Portal.PortalDeliveryNoticeService>();
        // Lát P6b: trạng thái thanh toán trên Portal.
        services.AddScoped<Proc.Application.Portal.IPortalPaymentService, Proc.Infrastructure.Portal.PortalPaymentService>();
        services.AddScoped<Proc.Application.Portal.IPortalPriceListService, Proc.Infrastructure.Portal.PortalPriceListService>();
        // Lát P2a: hồ sơ NCC trên Portal – đề nghị cập nhật.
        services.AddScoped<Proc.Application.Portal.IPortalProfileService, Proc.Infrastructure.Portal.PortalProfileService>();
        services.AddScoped<Proc.Application.Purchasing.IDeliveryNoticeService, Proc.Infrastructure.Purchasing.DeliveryNoticeService>();
        services.AddScoped<Proc.Application.Purchasing.IPaymentRequestService, Proc.Infrastructure.Purchasing.PaymentRequestService>();
        services.AddScoped<Proc.Application.Purchasing.ISundryExpenseSheetService, Proc.Infrastructure.Purchasing.SundryExpenseSheetService>();
        // Lát P3b-3: thư báo NCC (thư mời, gia hạn, hủy, trả lời, thông báo) – hàng chờ gửi qua dịch vụ nền email.
        services.AddScoped<Proc.Application.Portal.IPortalEmailService, Proc.Infrastructure.Portal.PortalEmailService>();

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
