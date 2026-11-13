using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Infrastructure.Identity;
using Proc.Infrastructure.Logging;

namespace Proc.Infrastructure.Persistence;

/// <summary>
/// DbContext chính của hệ thống, dùng chung cho mọi module (modular monolith, một CSDL – design.md mục 3).
/// Kế thừa IdentityUserContext để chứa bảng tài khoản của ASP.NET Core Identity (không gồm bảng vai trò của Identity,
/// vì vai trò gán theo công ty ở bảng UserCompanyRole – design.md mục 9.1).
/// Global query filter lọc dữ liệu theo phạm vi của người dùng (design.md mục 9.2) – xem <see cref="ApplyScopeFilters"/>.
/// </summary>
public sealed class ProcDbContext : IdentityUserContext<AppUser, Guid>
{
    /// <summary>Tên filter lọc theo công ty đang làm việc.</summary>
    public const string CompanyFilter = "Company";

    /// <summary>Tên filter lọc theo kho được phép.</summary>
    public const string WarehouseFilter = "WarehouseScope";

    /// <summary>Tên filter lọc theo nhóm hàng được phép.</summary>
    public const string ItemCategoryFilter = "ItemCategoryScope";

    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Khởi tạo DbContext với cấu hình do DI cung cấp.
    /// </summary>
    /// <param name="options">Tùy chọn kết nối (Npgsql, chuỗi kết nối, interceptor).</param>
    /// <param name="scope">Phạm vi của người dùng trong request hiện tại.</param>
    public ProcDbContext(DbContextOptions<ProcDbContext> options, ICurrentUserScope scope)
        : base(options)
    {
        _scope = scope;
    }

    // Các thuộc tính Scope* được global query filter đọc lại ở MỖI truy vấn (EF Core tham số hóa thành viên của DbContext),
    // nên cùng một mô hình dùng được cho mọi người dùng.

    /// <summary>Công ty đang làm việc; null thì filter không trả dòng nào.</summary>
    public Guid? ScopeCompanyId => _scope.CompanyId;

    /// <summary>Được mọi kho.</summary>
    public bool ScopeAllWarehouses => _scope.AllWarehouses;

    /// <summary>Kho được phép.</summary>
    public IReadOnlyList<Guid> ScopeWarehouseIds => _scope.WarehouseIds;

    /// <summary>Được mọi nhóm hàng.</summary>
    public bool ScopeAllItemCategories => _scope.AllItemCategories;

    /// <summary>Nhóm hàng được phép (gồm nhóm con).</summary>
    public IReadOnlyList<Guid> ScopeItemCategoryIds => _scope.ItemCategoryIds;

    /// <summary>Công ty.</summary>
    public DbSet<Company> Companies => Set<Company>();

    /// <summary>Vai trò.</summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>Gán vai trò cho người dùng theo công ty.</summary>
    public DbSet<UserCompanyRole> UserCompanyRoles => Set<UserCompanyRole>();

    /// <summary>Quyền chức năng (đồng bộ từ Permissions.All).</summary>
    public DbSet<Permission> Permissions => Set<Permission>();

    /// <summary>Quyền của vai trò.</summary>
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    /// <summary>Kho (lọc theo công ty và phạm vi kho).</summary>
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    /// <summary>Khối của công ty (lọc theo công ty – lát O1).</summary>
    public DbSet<Division> Divisions => Set<Division>();

    /// <summary>Bộ phận của người dùng tại công ty (lát O1) – không lọc tự động; truy vấn luôn ghi rõ công ty.</summary>
    public DbSet<UserDepartment> UserDepartments => Set<UserDepartment>();

    /// <summary>Bộ phận (lọc theo công ty).</summary>
    public DbSet<Department> Departments => Set<Department>();

    /// <summary>Ngân sách bộ phận × tháng (lọc theo công ty – design.md mục 18.24).</summary>
    public DbSet<Proc.Domain.Purchasing.DepartmentBudget> DepartmentBudgets => Set<Proc.Domain.Purchasing.DepartmentBudget>();

    /// <summary>Công trình có tổng dự toán (lọc theo công ty – lát Y2, design.md mục 19.2).</summary>
    public DbSet<Proc.Domain.Purchasing.ConstructionProject> ConstructionProjects => Set<Proc.Domain.Purchasing.ConstructionProject>();

    /// <summary>Lịch sử dự toán công trình – ban đầu và các lần điều chỉnh (lát Y2); đọc qua công trình cha.</summary>
    public DbSet<Proc.Domain.Purchasing.ConstructionProjectEstimateRevision> ConstructionProjectEstimateRevisions =>
        Set<Proc.Domain.Purchasing.ConstructionProjectEstimateRevision>();

    /// <summary>Quyết định của công ty bán với PO mua nội bộ (lọc theo công ty bán – lát X7, design.md mục 19.3).</summary>
    public DbSet<Proc.Domain.Purchasing.InternalSaleConfirmation> InternalSaleConfirmations => Set<Proc.Domain.Purchasing.InternalSaleConfirmation>();

    /// <summary>NCC xác nhận / từ chối đơn đặt hàng trên Portal (lát P4a).</summary>
    public DbSet<Proc.Domain.Purchasing.PurchaseOrderSupplierResponse> PurchaseOrderSupplierResponses => Set<Proc.Domain.Purchasing.PurchaseOrderSupplierResponse>();

    /// <summary>Phiếu báo giao hàng NCC gửi trên Portal (lát P5).</summary>
    public DbSet<Proc.Domain.Purchasing.PurchaseOrderDeliveryNotice> PurchaseOrderDeliveryNotices => Set<Proc.Domain.Purchasing.PurchaseOrderDeliveryNotice>();

    /// <summary>Danh mục đơn vị mua (lọc theo công ty – lát Y5a, design.md mục 19.3).</summary>
    public DbSet<Proc.Domain.Purchasing.PurchasingUnit> PurchasingUnits => Set<Proc.Domain.Purchasing.PurchasingUnit>();

    /// <summary>Bảng tổng hợp đề xuất mua 2A/2B (lọc theo công ty – lát Y5b, design.md mục 19.3).</summary>
    public DbSet<Proc.Domain.Purchasing.PurchaseSummary> PurchaseSummaries => Set<Proc.Domain.Purchasing.PurchaseSummary>();

    /// <summary>Nhóm hàng (lọc theo phạm vi nhóm hàng).</summary>
    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();

    /// <summary>Đơn vị tính (dùng chung).</summary>
    public DbSet<Uom> Uoms => Set<Uom>();

    /// <summary>Dải đánh số danh mục (design.md mục 5.11).</summary>
    public DbSet<CatalogNumbering> CatalogNumberings => Set<CatalogNumbering>();

    /// <summary>Hạng xếp loại nhà cung cấp (design.md mục 11.3).</summary>
    public DbSet<SupplierRatingRule> SupplierRatingRules => Set<SupplierRatingRule>();

    /// <summary>Hàng hóa (lọc theo phạm vi nhóm hàng).</summary>
    public DbSet<Item> Items => Set<Item>();

    /// <summary>Nhóm nhà cung cấp (dùng chung).</summary>
    public DbSet<SupplierGroup> SupplierGroups => Set<SupplierGroup>();

    /// <summary>Nhà cung cấp (dùng chung).</summary>
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    /// <summary>Yêu cầu duyệt NCC (design.md mục 18.6).</summary>
    public DbSet<SupplierChangeRequest> SupplierChangeRequests => Set<SupplierChangeRequest>();

    /// <summary>Tài khoản Portal NCC (design.md 19.5, lát P1).</summary>
    public DbSet<SupplierPortalUser> SupplierPortalUsers => Set<SupplierPortalUser>();

    /// <summary>Mã dùng một lần của Portal NCC: link mời, đặt lại mật khẩu, OTP (chỉ lưu băm).</summary>
    public DbSet<SupplierPortalCode> SupplierPortalCodes => Set<SupplierPortalCode>();

    /// <summary>Hàng chờ thư báo NCC qua Portal: thư mời, gia hạn, hủy, trả lời, thông báo RFQ (lát P3b-3).</summary>
    public DbSet<SupplierPortalEmail> SupplierPortalEmails => Set<SupplierPortalEmail>();

    /// <summary>Kê khai xung đột lợi ích (dùng chung, không lọc theo công ty – design.md mục 18.8).</summary>
    public DbSet<ConflictDeclaration> ConflictDeclarations => Set<ConflictDeclaration>();

    /// <summary>Phê duyệt NCC độc quyền (lọc theo công ty và phạm vi nhóm hàng của mặt hàng – design.md mục 18.9).</summary>
    public DbSet<SoleSourceApproval> SoleSourceApprovals => Set<SoleSourceApproval>();

    /// <summary>Vi phạm KSNB (lọc theo công ty – design.md mục 18.19).</summary>
    public DbSet<Proc.Domain.Control.ControlFinding> ControlFindings => Set<Proc.Domain.Control.ControlFinding>();

    /// <summary>Phiếu đánh giá NCC định kỳ theo quý (lọc theo công ty – design.md mục 18.20).</summary>
    public DbSet<SupplierReview> SupplierReviews => Set<SupplierReview>();

    /// <summary>Hồ sơ đánh giá NCC mới (thuộc tổng công ty, lọc theo công ty – lát G8, design.md mục 18.25).</summary>
    public DbSet<SupplierOnboarding> SupplierOnboardings => Set<SupplierOnboarding>();

    /// <summary>NCC áp dụng cho công ty (lọc theo công ty).</summary>
    public DbSet<SupplierCompany> SupplierCompanies => Set<SupplierCompany>();

    /// <summary>NCC – sản phẩm (lọc theo phạm vi nhóm hàng của mặt hàng).</summary>
    public DbSet<SupplierItem> SupplierItems => Set<SupplierItem>();

    /// <summary>
    /// Ngành hàng NCC cung cấp (NCC × nhóm hàng). Không lọc theo phạm vi nhóm hàng: danh mục nhóm hàng ai cũng xem đủ (design.md mục 9.2).
    /// </summary>
    public DbSet<SupplierCategory> SupplierCategories => Set<SupplierCategory>();

    /// <summary>Kỳ kho (lọc theo công ty).</summary>
    public DbSet<InventoryPeriod> InventoryPeriods => Set<InventoryPeriod>();

    /// <summary>Chứng từ kho (lọc theo công ty và kho được phép).</summary>
    public DbSet<InventoryDocument> InventoryDocuments => Set<InventoryDocument>();

    /// <summary>Dòng chứng từ kho (theo chứng từ thấy được).</summary>
    public DbSet<InventoryDocumentLine> InventoryDocumentLines => Set<InventoryDocumentLine>();

    /// <summary>Sổ kho (lọc theo công ty, kho, nhóm hàng).</summary>
    public DbSet<StockLedgerEntry> StockLedger => Set<StockLedgerEntry>();

    /// <summary>Tồn tức thời (lọc theo công ty, kho, nhóm hàng).</summary>
    public DbSet<StockOnHand> StockOnHand => Set<StockOnHand>();

    /// <summary>Số dư kỳ đã đóng (lọc theo công ty, kho, nhóm hàng).</summary>
    public DbSet<StockPeriodBalance> StockPeriodBalances => Set<StockPeriodBalance>();

    /// <summary>Giá vốn kỳ theo mặt hàng (lọc theo công ty, nhóm hàng).</summary>
    public DbSet<ItemPeriodCost> ItemPeriodCosts => Set<ItemPeriodCost>();

    /// <summary>Quy trình duyệt (lọc theo công ty).</summary>
    public DbSet<Proc.Domain.Purchasing.ApprovalRule> ApprovalRules => Set<Proc.Domain.Purchasing.ApprovalRule>();

    /// <summary>Lịch sử duyệt (lọc theo công ty).</summary>
    public DbSet<Proc.Domain.Purchasing.ApprovalStep> ApprovalSteps => Set<Proc.Domain.Purchasing.ApprovalStep>();

    /// <summary>Ủy quyền duyệt có thời hạn (lọc theo công ty – lát O2).</summary>
    public DbSet<Proc.Domain.Purchasing.ApprovalDelegation> ApprovalDelegations => Set<Proc.Domain.Purchasing.ApprovalDelegation>();

    /// <summary>Các lần nhắc quá hạn duyệt (lọc theo công ty – lát O2c).</summary>
    public DbSet<Proc.Domain.Purchasing.ApprovalReminder> ApprovalReminders => Set<Proc.Domain.Purchasing.ApprovalReminder>();

    /// <summary>Đề nghị mua hàng (lọc theo công ty).</summary>
    public DbSet<Proc.Domain.Purchasing.PurchaseRequest> PurchaseRequests => Set<Proc.Domain.Purchasing.PurchaseRequest>();

    /// <summary>Đơn mua hàng (lọc theo công ty).</summary>
    public DbSet<Proc.Domain.Purchasing.PurchaseOrder> PurchaseOrders => Set<Proc.Domain.Purchasing.PurchaseOrder>();

    /// <summary>Phiếu xác nhận nhận hàng XNH (lát K2, design.md mục 19.4).</summary>
    public DbSet<Proc.Domain.Purchasing.GoodsReceiptConfirmation> GoodsReceiptConfirmations => Set<Proc.Domain.Purchasing.GoodsReceiptConfirmation>();

    /// <summary>Bảng giá nội bộ (lọc theo công ty bán).</summary>
    public DbSet<Proc.Domain.Pricing.InternalPrice> InternalPrices => Set<Proc.Domain.Pricing.InternalPrice>();

    /// <summary>Bảng giá NCC (design.md mục 8.1).</summary>
    public DbSet<Proc.Domain.Pricing.SupplierPriceList> SupplierPriceLists => Set<Proc.Domain.Pricing.SupplierPriceList>();

    /// <summary>Phiếu duyệt NCC và giá tháng (lọc theo công ty và phạm vi nhóm hàng của phiếu – design.md mục 18.25).</summary>
    public DbSet<Proc.Domain.Pricing.MonthlySupplierApproval> MonthlySupplierApprovals => Set<Proc.Domain.Pricing.MonthlySupplierApproval>();

    /// <summary>Công ty áp dụng của phiếu duyệt tháng.</summary>
    public DbSet<Proc.Domain.Pricing.MonthlySupplierApprovalCompany> MonthlySupplierApprovalCompanies => Set<Proc.Domain.Pricing.MonthlySupplierApprovalCompany>();

    /// <summary>Thỏa thuận chiết khấu bậc sản lượng gộp (lọc theo công ty và nhóm hàng của thỏa thuận – design.md mục 18.25, G6a).</summary>
    public DbSet<Proc.Domain.Pricing.VolumeRebateAgreement> VolumeRebateAgreements => Set<Proc.Domain.Pricing.VolumeRebateAgreement>();

    /// <summary>Công ty áp dụng của thỏa thuận chiết khấu sản lượng gộp.</summary>
    public DbSet<Proc.Domain.Pricing.VolumeRebateCompany> VolumeRebateCompanies => Set<Proc.Domain.Pricing.VolumeRebateCompany>();

    /// <summary>
    /// Chiết khấu sản lượng gộp thực nhận (lát G6b) – thuộc tổng công ty (filter công ty); công ty áp dụng đọc qua thỏa thuận hiệu lực của mình.
    /// </summary>
    public DbSet<Proc.Domain.Pricing.VolumeRebateReceipt> VolumeRebateReceipts => Set<Proc.Domain.Pricing.VolumeRebateReceipt>();

    /// <summary>Phiếu khảo sát giá thị trường – ngân hàng giá (lọc theo công ty và phạm vi nhóm hàng của phiếu – design.md mục 18.25).</summary>
    public DbSet<Proc.Domain.Pricing.MarketPriceSurvey> MarketPriceSurveys => Set<Proc.Domain.Pricing.MarketPriceSurvey>();

    /// <summary>Phiếu kế hoạch nhu cầu (lọc theo công ty và phạm vi nhóm hàng của phiếu – design.md mục 18.25, lát G5a).</summary>
    public DbSet<Proc.Domain.Purchasing.DemandPlan> DemandPlans => Set<Proc.Domain.Purchasing.DemandPlan>();

    /// <summary>Phiếu đặt hàng gộp (lọc theo công ty và phạm vi nhóm hàng của phiếu – design.md mục 18.25, lát G7).</summary>
    public DbSet<Proc.Domain.Purchasing.PooledOrder> PooledOrders => Set<Proc.Domain.Purchasing.PooledOrder>();

    /// <summary>Danh sách tìm nguồn (design.md mục 8.3).</summary>
    public DbSet<Proc.Domain.Sourcing.SourcingRequest> SourcingRequests => Set<Proc.Domain.Sourcing.SourcingRequest>();

    /// <summary>Yêu cầu báo giá (design.md mục 8.4).</summary>
    public DbSet<Proc.Domain.Sourcing.Rfq> Rfqs => Set<Proc.Domain.Sourcing.Rfq>();

    /// <summary>Bản nộp báo giá Portal niêm phong (lát P3a).</summary>
    public DbSet<Proc.Domain.Sourcing.RfqPortalSubmission> RfqPortalSubmissions => Set<Proc.Domain.Sourcing.RfqPortalSubmission>();

    /// <summary>Hỏi đáp làm rõ RFQ giữa NCC và Phòng CUVT (lát P3b-2).</summary>
    public DbSet<Proc.Domain.Sourcing.RfqClarification> RfqClarifications => Set<Proc.Domain.Sourcing.RfqClarification>();

    /// <summary>Hợp đồng nguyên tắc (lát B4a – design.md mục 18.7).</summary>
    public DbSet<Proc.Domain.Purchasing.PurchaseContract> PurchaseContracts => Set<Proc.Domain.Purchasing.PurchaseContract>();

    /// <summary>Phụ lục hợp đồng nguyên tắc (lát B4b – design.md mục 18.7).</summary>
    public DbSet<Proc.Domain.Purchasing.PurchaseContractAmendment> PurchaseContractAmendments => Set<Proc.Domain.Purchasing.PurchaseContractAmendment>();

    /// <summary>Chiết khấu/tài trợ thực nhận theo hợp đồng (lát F4b – design.md mục 18.21).</summary>
    public DbSet<Proc.Domain.Purchasing.ContractRebateReceipt> ContractRebateReceipts => Set<Proc.Domain.Purchasing.ContractRebateReceipt>();

    /// <summary>Phạm vi kho của người dùng.</summary>
    public DbSet<UserWarehouseScope> UserWarehouseScopes => Set<UserWarehouseScope>();

    /// <summary>Phạm vi nhóm hàng của người dùng.</summary>
    public DbSet<UserItemCategoryScope> UserItemCategoryScopes => Set<UserItemCategoryScope>();

    /// <summary>Tham số công ty (lọc theo công ty).</summary>
    public DbSet<CompanyParameter> CompanyParameters => Set<CompanyParameter>();

    /// <summary>Cấu hình đánh số chứng từ (lọc theo công ty).</summary>
    public DbSet<DocumentNumbering> DocumentNumberings => Set<DocumentNumbering>();

    /// <summary>Bộ đếm số chứng từ (lọc theo công ty).</summary>
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();

    /// <summary>Nhật ký thao tác (chỉ thêm).</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>Thông báo (design.md mục 11.6).</summary>
    public DbSet<Proc.Domain.Notifications.Notification> Notifications => Set<Proc.Domain.Notifications.Notification>();

    /// <summary>Nhật ký truy cập (chỉ thêm).</summary>
    public DbSet<AccessLog> AccessLogs => Set<AccessLog>();

    /// <summary>Lô nhập dữ liệu mua lịch sử (design.md mục 18.1).</summary>
    public DbSet<Proc.Domain.Analysis.PurchaseHistoryBatch> PurchaseHistoryBatches => Set<Proc.Domain.Analysis.PurchaseHistoryBatch>();

    /// <summary>Dòng dữ liệu mua lịch sử.</summary>
    public DbSet<Proc.Domain.Analysis.PurchaseHistoryRecord> PurchaseHistoryRecords => Set<Proc.Domain.Analysis.PurchaseHistoryRecord>();

    /// <summary>Ánh xạ mã vật tư cũ → mã hàng chung (design.md mục 18.2).</summary>
    public DbSet<Proc.Domain.Analysis.PurchaseHistoryItemMapping> PurchaseHistoryItemMappings => Set<Proc.Domain.Analysis.PurchaseHistoryItemMapping>();

    /// <summary>Ánh xạ NCC cũ → NCC trong danh mục (design.md mục 18.2).</summary>
    public DbSet<Proc.Domain.Analysis.PurchaseHistorySupplierMapping> PurchaseHistorySupplierMappings => Set<Proc.Domain.Analysis.PurchaseHistorySupplierMapping>();

    /// <summary>File đính kèm dùng chung (design.md mục 18.4).</summary>
    public DbSet<Proc.Domain.Documents.Attachment> Attachments => Set<Proc.Domain.Documents.Attachment>();

    /// <summary>Ý kiến thẩm định của thành viên chuyên môn (lát H1 – design.md mục 18.25).</summary>
    public DbSet<Proc.Domain.Documents.AppraisalOpinion> AppraisalOpinions => Set<Proc.Domain.Documents.AppraisalOpinion>();

    /// <summary>
    /// Cấu hình bảng Identity, áp dụng các lớp IEntityTypeConfiguration trong assembly Infrastructure, rồi gắn filter phạm vi.
    /// </summary>
    /// <param name="modelBuilder">Bộ dựng mô hình của EF Core.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Đặt lại tên bảng Identity cho cùng kiểu với các bảng khác (số ít, không tiền tố AspNet).
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaim");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogin");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserToken");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProcDbContext).Assembly);
        ApplyScopeFilters(modelBuilder);
        ApplyRowVersions(modelBuilder);
    }

    /// <summary>
    /// Entity <see cref="IVersioned"/>: thuộc tính Version (uint) là cột hệ thống <c>xmin</c> của PostgreSQL, làm concurrency token – UPDATE
    /// kèm điều kiện xmin như lúc đọc, dòng đã bị giao dịch khác đổi thì EF báo xung đột (lát R2, design.md mục 18.26). Không thêm cột thật.
    /// </summary>
    private static void ApplyRowVersions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(IVersioned).IsAssignableFrom(t.ClrType) && t.BaseType is null))
            modelBuilder.Entity(entityType.ClrType).Property(nameof(IVersioned.Version)).IsRowVersion();
    }

    /// <summary>
    /// Gắn global query filter theo phạm vi (design.md mục 9.2):
    /// <list type="bullet">
    /// <item>Mọi entity có CompanyId (<see cref="ICompanyOwned"/>): chỉ thấy dòng của công ty đang làm việc – tự động,
    /// để bảng giao dịch thêm sau không thể quên lọc.</item>
    /// <item>Kho: thêm điều kiện kho được phép. Nhóm hàng: thêm điều kiện nhóm được phép.</item>
    /// </list>
    /// Filter có tên riêng (EF Core 10) để sau này tắt riêng từng filter khi thật cần, có ghi lý do.
    /// </summary>
    private void ApplyScopeFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(ICompanyOwned).IsAssignableFrom(t.ClrType) && t.BaseType is null))
        {
            // Dựng biểu thức e => (Guid?)e.CompanyId == this.ScopeCompanyId cho từng loại entity.
            var e = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Equal(
                Expression.Convert(Expression.Property(e, nameof(ICompanyOwned.CompanyId)), typeof(Guid?)),
                Expression.Property(Expression.Constant(this), nameof(ScopeCompanyId)));
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(CompanyFilter, Expression.Lambda(body, e));
        }

        modelBuilder.Entity<Warehouse>().HasQueryFilter(
            WarehouseFilter, w => ScopeAllWarehouses || ScopeWarehouseIds.Contains(w.Id));
        modelBuilder.Entity<ItemCategory>().HasQueryFilter(
            ItemCategoryFilter, c => ScopeAllItemCategories || ScopeItemCategoryIds.Contains(c.Id));
        // Hàng hóa theo nhóm hàng của nó: người chỉ được nhóm Vật tư không thấy mã hàng nhóm khác (design.md mục 9.2).
        modelBuilder.Entity<Item>().HasQueryFilter(
            ItemCategoryFilter, i => ScopeAllItemCategories || ScopeItemCategoryIds.Contains(i.CategoryId));
        // Chứng từ kho theo kho được phép: thủ kho chỉ được kho KP1 thì chỉ thấy phiếu nhập/xuất của KP1 (design.md mục 9.2).
        modelBuilder.Entity<InventoryDocument>().HasQueryFilter(
            WarehouseFilter,
            d => ScopeAllWarehouses
                 || (d.ToWarehouseId != null && ScopeWarehouseIds.Contains(d.ToWarehouseId.Value))
                 || (d.FromWarehouseId != null && ScopeWarehouseIds.Contains(d.FromWarehouseId.Value)));
        // Dòng đi theo chứng từ: chỉ thấy dòng của chứng từ thấy được (công ty, kho).
        modelBuilder.Entity<InventoryDocumentLine>().HasQueryFilter(
            CompanyFilter, l => InventoryDocuments.Any(d => d.Id == l.DocumentId));
        // Sổ kho và tồn kho: chỉ thấy kho được phép và mặt hàng thuộc nhóm được phép (design.md mục 9.2).
        modelBuilder.Entity<StockLedgerEntry>().HasQueryFilter(
            WarehouseFilter, x => ScopeAllWarehouses || ScopeWarehouseIds.Contains(x.WarehouseId));
        modelBuilder.Entity<StockLedgerEntry>().HasQueryFilter(
            ItemCategoryFilter, x => ScopeAllItemCategories || Items.Any(i => i.Id == x.ItemId));
        modelBuilder.Entity<StockOnHand>().HasQueryFilter(
            WarehouseFilter, x => ScopeAllWarehouses || ScopeWarehouseIds.Contains(x.WarehouseId));
        modelBuilder.Entity<StockOnHand>().HasQueryFilter(
            ItemCategoryFilter, x => ScopeAllItemCategories || Items.Any(i => i.Id == x.ItemId));
        modelBuilder.Entity<StockPeriodBalance>().HasQueryFilter(
            WarehouseFilter, x => ScopeAllWarehouses || ScopeWarehouseIds.Contains(x.WarehouseId));
        modelBuilder.Entity<StockPeriodBalance>().HasQueryFilter(
            ItemCategoryFilter, x => ScopeAllItemCategories || Items.Any(i => i.Id == x.ItemId));
        modelBuilder.Entity<ItemPeriodCost>().HasQueryFilter(
            ItemCategoryFilter, x => ScopeAllItemCategories || Items.Any(i => i.Id == x.ItemId));
        // NCC – sản phẩm theo phạm vi của mặt hàng: Items đã có filter nhóm hàng, nên chỉ thấy dòng của hàng mình được xem.
        modelBuilder.Entity<SupplierItem>().HasQueryFilter(
            ItemCategoryFilter, si => ScopeAllItemCategories || Items.Any(i => i.Id == si.ItemId));
        // Phiếu NCC độc quyền cũng theo phạm vi nhóm hàng của mặt hàng (filter công ty đã có vì ICompanyOwned).
        modelBuilder.Entity<SoleSourceApproval>().HasQueryFilter(
            ItemCategoryFilter, x => ScopeAllItemCategories || Items.Any(i => i.Id == x.ItemId));
        // Phiếu duyệt NCC và giá tháng theo nhóm hàng của phiếu: người không được nhóm hàng đó không thấy phiếu (design.md mục 9.2, 18.25).
        modelBuilder.Entity<Proc.Domain.Pricing.MonthlySupplierApproval>().HasQueryFilter(
            ItemCategoryFilter, x => ScopeAllItemCategories || ScopeItemCategoryIds.Contains(x.ItemCategoryId));
        // Thỏa thuận chiết khấu sản lượng gộp cũng theo nhóm hàng của thỏa thuận (design.md mục 9.2, 18.25 – G6a).
        modelBuilder.Entity<Proc.Domain.Pricing.VolumeRebateAgreement>().HasQueryFilter(
            ItemCategoryFilter, x => ScopeAllItemCategories || ScopeItemCategoryIds.Contains(x.ItemCategoryId));
        // Phiếu khảo sát giá cũng theo nhóm hàng của phiếu.
        modelBuilder.Entity<Proc.Domain.Pricing.MarketPriceSurvey>().HasQueryFilter(
            ItemCategoryFilter, x => ScopeAllItemCategories || ScopeItemCategoryIds.Contains(x.ItemCategoryId));
        // Phiếu kế hoạch nhu cầu cũng theo nhóm hàng của phiếu.
        modelBuilder.Entity<Proc.Domain.Purchasing.DemandPlan>().HasQueryFilter(
            ItemCategoryFilter, x => ScopeAllItemCategories || ScopeItemCategoryIds.Contains(x.ItemCategoryId));
        // Phiếu đặt hàng gộp theo nhóm hàng của phiếu GTH mà nó dựa vào.
        modelBuilder.Entity<Proc.Domain.Purchasing.PooledOrder>().HasQueryFilter(
            ItemCategoryFilter, x => ScopeAllItemCategories || ScopeItemCategoryIds.Contains(x.ItemCategoryId));
    }
}
