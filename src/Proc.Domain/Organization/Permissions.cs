namespace Proc.Domain.Organization;

/// <summary>Khai báo một quyền chức năng.</summary>
/// <param name="Code">Mã quyền dạng Module.Đối tượng.Hành động (design.md mục 9.1).</param>
/// <param name="Name">Tên tiếng Việt hiển thị trên màn hình phân quyền.</param>
public sealed record PermissionDefinition(string Code, string Name)
{
    /// <summary>Phần module của mã quyền, ví dụ "Purchase" trong "Purchase.PO.Create".</summary>
    public string Module => Code[..Code.IndexOf('.')];
}

/// <summary>
/// Danh mục quyền chức năng của hệ thống (design.md mục 9.1). Đây là nơi duy nhất khai báo quyền:
/// khi khởi động, bảng Permission được đồng bộ theo danh sách này, và vai trò ADMIN luôn có đủ mọi quyền.
/// Thêm chức năng mới thì thêm quyền ở đây.
/// </summary>
public static class Permissions
{
    /// <summary>Hệ thống: công ty, người dùng, vai trò, nhật ký, tham số.</summary>
    public static class System
    {
        /// <summary>Quyền "Xem danh mục công ty".</summary>
        public const string CompanyView = "System.Company.View";
        /// <summary>Quyền "Thêm, sửa công ty".</summary>
        public const string CompanyManage = "System.Company.Manage";
        /// <summary>Quyền "Xem người dùng".</summary>
        public const string UserView = "System.User.View";
        /// <summary>Quyền "Thêm, sửa người dùng và gán vai trò".</summary>
        public const string UserManage = "System.User.Manage";
        /// <summary>Quyền "Xem vai trò".</summary>
        public const string RoleView = "System.Role.View";
        /// <summary>Quyền "Thêm, sửa vai trò và quyền".</summary>
        public const string RoleManage = "System.Role.Manage";
        /// <summary>Quyền "Xem nhật ký thao tác".</summary>
        public const string LogView = "System.Log.View";
        /// <summary>Quyền "Xem tham số hệ thống".</summary>
        public const string ParameterView = "System.Parameter.View";
        /// <summary>Quyền "Sửa tham số hệ thống".</summary>
        public const string ParameterManage = "System.Parameter.Manage";
    }

    /// <summary>Tổ chức: kho, bộ phận.</summary>
    public static class Organization
    {
        /// <summary>Quyền "Xem danh mục kho".</summary>
        public const string WarehouseView = "Organization.Warehouse.View";
        /// <summary>Quyền "Thêm, sửa kho".</summary>
        public const string WarehouseManage = "Organization.Warehouse.Manage";
        /// <summary>Quyền "Xem danh mục bộ phận".</summary>
        public const string DepartmentView = "Organization.Department.View";
        /// <summary>Quyền "Thêm, sửa bộ phận".</summary>
        public const string DepartmentManage = "Organization.Department.Manage";
    }

    /// <summary>Danh mục dùng chung: nhóm hàng, hàng hóa, nhà cung cấp.</summary>
    public static class MasterData
    {
        /// <summary>Quyền "Xem nhóm hàng".</summary>
        public const string ItemCategoryView = "MasterData.ItemCategory.View";
        /// <summary>Quyền "Thêm, sửa nhóm hàng".</summary>
        public const string ItemCategoryManage = "MasterData.ItemCategory.Manage";
        /// <summary>Quyền "Xem hàng hóa".</summary>
        public const string ItemView = "MasterData.Item.View";
        /// <summary>Quyền "Thêm, sửa hàng hóa".</summary>
        public const string ItemManage = "MasterData.Item.Manage";
        /// <summary>Quyền "Xem nhà cung cấp".</summary>
        public const string SupplierView = "MasterData.Supplier.View";
        /// <summary>Quyền "Thêm, sửa nhà cung cấp".</summary>
        public const string SupplierManage = "MasterData.Supplier.Manage";
        /// <summary>Quyền "Duyệt hồ sơ, tài khoản ngân hàng nhà cung cấp" (design.md mục 18.6).</summary>
        public const string SupplierApprove = "MasterData.Supplier.Approve";
        /// <summary>
        /// Quyền "Xem mọi kê khai xung đột lợi ích" (KSNB, Trưởng ban – design.md mục 18.8). Không có quyền này thì chỉ xem kê khai của mình.
        /// </summary>
        public const string ConflictView = "MasterData.Conflict.View";
        /// <summary>Quyền "Ghi nhận kê khai thay người khác, kết thúc kê khai" (design.md mục 18.8).</summary>
        public const string ConflictManage = "MasterData.Conflict.Manage";
        /// <summary>Quyền "Xem phê duyệt NCC độc quyền" (design.md mục 18.9).</summary>
        public const string SoleSourceView = "MasterData.SoleSource.View";
        /// <summary>Quyền "Lập, gửi duyệt, kết thúc phê duyệt NCC độc quyền" (design.md mục 18.9).</summary>
        public const string SoleSourceManage = "MasterData.SoleSource.Manage";
        /// <summary>Quyền "Duyệt NCC độc quyền" (design.md mục 18.9) – người lập không tự duyệt.</summary>
        public const string SoleSourceApprove = "MasterData.SoleSource.Approve";
        /// <summary>Quyền "Xem đánh giá NCC định kỳ" (design.md mục 18.20).</summary>
        public const string SupplierReviewView = "MasterData.SupplierReview.View";
        /// <summary>Quyền "Lập, hoàn thành, hủy phiếu đánh giá NCC định kỳ" (design.md mục 18.20).</summary>
        public const string SupplierReviewManage = "MasterData.SupplierReview.Manage";

        /// <summary>
        /// Sửa thông tin danh mục dùng chung (công ty, ĐVT, nhóm hàng, hàng hóa, nhóm NCC, NCC) ở bất kỳ công ty nào – trừ mã; thêm mới vẫn
        /// chỉ ở tổng công ty (design.md mục 5.7).
        /// </summary>
        public const string CatalogEditShared = "MasterData.Catalog.EditShared";
    }

    /// <summary>Mua hàng: PR, PO, hợp đồng nguyên tắc.</summary>
    public static class Purchase
    {
        /// <summary>Quyền "Xem đề nghị mua hàng".</summary>
        public const string PrView = "Purchase.PR.View";
        /// <summary>Quyền "Lập đề nghị mua hàng".</summary>
        public const string PrCreate = "Purchase.PR.Create";
        /// <summary>Quyền "Duyệt đề nghị mua hàng".</summary>
        public const string PrApprove = "Purchase.PR.Approve";
        /// <summary>Quyền "Xem đơn mua hàng".</summary>
        public const string PoView = "Purchase.PO.View";
        /// <summary>Quyền "Lập đơn mua hàng".</summary>
        public const string PoCreate = "Purchase.PO.Create";
        /// <summary>Quyền "Duyệt đơn mua hàng".</summary>
        public const string PoApprove = "Purchase.PO.Approve";
        /// <summary>Quyền "Xem hợp đồng nguyên tắc".</summary>
        public const string ContractView = "Purchase.Contract.View";
        /// <summary>Quyền "Thêm, sửa hợp đồng nguyên tắc".</summary>
        public const string ContractManage = "Purchase.Contract.Manage";
        /// <summary>Quyền "Duyệt hợp đồng nguyên tắc" (design.md mục 18.6).</summary>
        public const string ContractApprove = "Purchase.Contract.Approve";
        /// <summary>Quyền "Xem ngân sách bộ phận" (design.md mục 18.24).</summary>
        public const string BudgetView = "Purchase.Budget.View";
        /// <summary>Quyền "Lập, sửa ngân sách bộ phận".</summary>
        public const string BudgetManage = "Purchase.Budget.Manage";
        /// <summary>Quyền "Xem kế hoạch nhu cầu" (lát G5, design.md mục 18.25).</summary>
        public const string DemandPlanView = "Purchase.DemandPlan.View";
        /// <summary>Quyền "Lập, chốt, điều chỉnh, hủy phiếu kế hoạch nhu cầu".</summary>
        public const string DemandPlanManage = "Purchase.DemandPlan.Manage";
        /// <summary>
        /// Quyền "Xem kế hoạch nhu cầu toàn tập đoàn" (lát G5b): tổng hợp nhu cầu gộp mọi công ty – Văn phòng Ban dự báo sản lượng gộp để
        /// thương lượng. Không có quyền này chỉ thấy nhu cầu của công ty đang làm việc.
        /// </summary>
        public const string DemandPlanViewAllCompanies = "Purchase.DemandPlan.ViewAllCompanies";
        /// <summary>Quyền "Xem phiếu đặt hàng gộp" (lát G7, design.md mục 18.25).</summary>
        public const string PooledOrderView = "Purchase.PooledOrder.View";
        /// <summary>
        /// Quyền "Lập, sửa, hủy phiếu đặt hàng gộp và tạo PO cho các công ty" – tạo PO còn cần quyền tạo PO tại từng công ty nhận hàng.
        /// </summary>
        public const string PooledOrderManage = "Purchase.PooledOrder.Manage";
    }

    /// <summary>Kho: chứng từ kho, tồn kho, kỳ kho.</summary>
    public static class Inventory
    {
        /// <summary>Quyền "Xem chứng từ kho".</summary>
        public const string DocumentView = "Inventory.Document.View";
        /// <summary>Quyền "Lập chứng từ kho".</summary>
        public const string DocumentCreate = "Inventory.Document.Create";
        /// <summary>Quyền "Ghi sổ chứng từ kho".</summary>
        public const string DocumentPost = "Inventory.Document.Post";
        /// <summary>Quyền "Bỏ ghi sổ chứng từ kho".</summary>
        public const string DocumentUnpost = "Inventory.Document.Unpost";
        /// <summary>
        /// Quyền "Lập phiếu điều chỉnh, kiểm kê kho" – quyền riêng của phiếu ADJ (design.md mục 6.1): lập, sửa, hủy phiếu nháp.
        /// </summary>
        public const string AdjustmentCreate = "Inventory.Adjustment.Create";
        /// <summary>
        /// Quyền "Ghi sổ phiếu điều chỉnh, kiểm kê kho" – ghi sổ và bỏ ghi sổ phiếu ADJ; tách khỏi quyền ghi sổ chứng từ kho thường để người
        /// kiểm kê không tự ghi tăng/giảm tồn (design.md mục 6.1).
        /// </summary>
        public const string AdjustmentPost = "Inventory.Adjustment.Post";
        /// <summary>Quyền "Xem tồn kho, thẻ kho".</summary>
        public const string StockView = "Inventory.Stock.View";
        /// <summary>Quyền "Xem kỳ kho".</summary>
        public const string PeriodView = "Inventory.Period.View";
        /// <summary>Quyền "Tạo lịch và mở kỳ kho".</summary>
        public const string PeriodOpen = "Inventory.Period.Open";
        /// <summary>Quyền "Đóng kỳ kho".</summary>
        public const string PeriodClose = "Inventory.Period.Close";
        /// <summary>Quyền "Mở lại kỳ kho".</summary>
        public const string PeriodReopen = "Inventory.Period.Reopen";
    }

    /// <summary>Giá và nhà cung cấp.</summary>
    public static class Pricing
    {
        /// <summary>Quyền "Xem bảng giá nhà cung cấp".</summary>
        public const string PriceListView = "Pricing.PriceList.View";
        /// <summary>Quyền "Thêm, sửa bảng giá".</summary>
        public const string PriceListManage = "Pricing.PriceList.Manage";
        /// <summary>Quyền "Duyệt bảng giá nhà cung cấp" (design.md mục 18.6).</summary>
        public const string PriceListApprove = "Pricing.PriceList.Approve";
        /// <summary>Quyền "So sánh giá".</summary>
        public const string CompareView = "Pricing.Compare.View";
        /// <summary>Quyền "Xem phiếu duyệt NCC và giá tháng" (lát G2, design.md mục 18.25).</summary>
        public const string MonthlyApprovalView = "Pricing.MonthlyApproval.View";
        /// <summary>Quyền "Lập, gửi duyệt, điều chỉnh, hủy phiếu duyệt NCC và giá tháng" (Văn phòng Ban – chỉ đề xuất).</summary>
        public const string MonthlyApprovalManage = "Pricing.MonthlyApproval.Manage";
        /// <summary>Quyền "Duyệt phiếu NCC và giá tháng" (Ban cung ứng).</summary>
        public const string MonthlyApprovalApprove = "Pricing.MonthlyApproval.Approve";
        /// <summary>Quyền "Xem thỏa thuận chiết khấu sản lượng gộp, theo dõi bậc" (lát G6a, design.md mục 18.25).</summary>
        public const string VolumeRebateView = "Pricing.VolumeRebate.View";
        /// <summary>Quyền "Lập, gửi duyệt, hủy thỏa thuận chiết khấu sản lượng gộp".</summary>
        public const string VolumeRebateManage = "Pricing.VolumeRebate.Manage";
        /// <summary>Quyền "Duyệt thỏa thuận chiết khấu sản lượng gộp" (Ban cung ứng).</summary>
        public const string VolumeRebateApprove = "Pricing.VolumeRebate.Approve";
        /// <summary>Quyền "Xem ngân hàng giá – phiếu khảo sát giá thị trường" (lát G4a, design.md mục 18.25).</summary>
        public const string PriceBankView = "Pricing.PriceBank.View";
        /// <summary>Quyền "Lập, chốt, hủy phiếu khảo sát giá thị trường" (Văn phòng Ban).</summary>
        public const string PriceBankManage = "Pricing.PriceBank.Manage";
    }

    /// <summary>Tìm nguồn và đấu thầu.</summary>
    public static class Sourcing
    {
        /// <summary>Quyền "Xem yêu cầu báo giá, tìm nguồn".</summary>
        public const string RfqView = "Sourcing.Rfq.View";
        /// <summary>Quyền "Lập yêu cầu báo giá".</summary>
        public const string RfqManage = "Sourcing.Rfq.Manage";
        /// <summary>Quyền "Chọn nhà cung cấp trúng thầu".</summary>
        public const string RfqAward = "Sourcing.Rfq.Award";
        /// <summary>Quyền "Duyệt kết quả chọn thầu" (hội đồng – design.md mục 18.6).</summary>
        public const string RfqApproveAward = "Sourcing.Rfq.ApproveAward";
    }

    /// <summary>Báo cáo.</summary>
    public static class Report
    {
        /// <summary>Quyền "Xem báo cáo nhập – xuất – tồn".</summary>
        public const string StockView = "Report.Stock.View";
        /// <summary>Quyền "Xem báo cáo mua hàng".</summary>
        public const string PurchaseView = "Report.Purchase.View";

        /// <summary>Xem báo cáo giá hợp nhất toàn tập đoàn (design.md mục 9.2: quyền *.ViewAllCompanies).</summary>
        public const string PriceViewAllCompanies = "Report.Price.ViewAllCompanies";

        /// <summary>Xem tồn kho một mặt hàng ở mọi công ty – chỉ số lượng (design.md mục 11.2).</summary>
        public const string StockViewAllCompanies = "Report.Stock.ViewAllCompanies";

        /// <summary>Xem báo cáo mua hàng, đánh giá NCC, tiết kiệm đấu thầu toàn tập đoàn (design.md mục 11.3).</summary>
        public const string PurchaseViewAllCompanies = "Report.Purchase.ViewAllCompanies";

        /// <summary>Xuất chứng từ kho đã ghi sổ ra Excel cho kế toán (design.md mục 11.5).</summary>
        public const string AccountingExport = "Report.Accounting.Export";
    }

    /// <summary>Phân tích dữ liệu mua (design.md mục 18.1–18.3).</summary>
    public static class Analysis
    {
        /// <summary>Quyền "Xem dữ liệu mua lịch sử" của công ty đang làm việc.</summary>
        public const string PurchaseHistoryView = "Analysis.PurchaseHistory.View";
        /// <summary>Quyền "Nhập, xóa lô dữ liệu mua lịch sử".</summary>
        public const string PurchaseHistoryImport = "Analysis.PurchaseHistory.Import";
        /// <summary>Xem phân tích giá hợp nhất toàn tập đoàn (design.md mục 18.3, quyền *.ViewAllCompanies của mục 9.2).</summary>
        public const string PurchaseHistoryViewAllCompanies = "Analysis.PurchaseHistory.ViewAllCompanies";
    }

    /// <summary>Kiểm soát nội bộ – KSNB (design.md mục 18.19).</summary>
    public static class Control
    {
        /// <summary>Quyền "Xem dấu hiệu bất thường và sổ vi phạm" (KSNB). Xem toàn tập đoàn cần thêm <see cref="Report.PurchaseViewAllCompanies"/>.</summary>
        public const string AnomalyView = "Control.Anomaly.View";
        /// <summary>Quyền "Ghi nhận, kết luận vi phạm" (KSNB).</summary>
        public const string FindingManage = "Control.Finding.Manage";
        /// <summary>
        /// Quyền "Nhận thông báo vi phạm, xem sổ vi phạm" (lãnh đạo) – kết quả KSNB gửi thẳng lãnh đạo, không qua Nhóm Đàm phán/Vận hành.
        /// </summary>
        public const string FindingReceive = "Control.Finding.Receive";
    }

    /// <summary>Toàn bộ quyền kèm tên tiếng Việt, theo thứ tự hiển thị.</summary>
    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(System.CompanyView, "Xem danh mục công ty"),
        new(System.CompanyManage, "Thêm, sửa công ty"),
        new(System.UserView, "Xem người dùng"),
        new(System.UserManage, "Thêm, sửa người dùng và gán vai trò"),
        new(System.RoleView, "Xem vai trò"),
        new(System.RoleManage, "Thêm, sửa vai trò và quyền"),
        new(System.LogView, "Xem nhật ký thao tác"),
        new(System.ParameterView, "Xem tham số hệ thống"),
        new(System.ParameterManage, "Sửa tham số hệ thống"),
        new(Organization.WarehouseView, "Xem danh mục kho"),
        new(Organization.WarehouseManage, "Thêm, sửa kho"),
        new(Organization.DepartmentView, "Xem danh mục bộ phận"),
        new(Organization.DepartmentManage, "Thêm, sửa bộ phận"),
        new(MasterData.ItemCategoryView, "Xem nhóm hàng"),
        new(MasterData.ItemCategoryManage, "Thêm, sửa nhóm hàng"),
        new(MasterData.ItemView, "Xem hàng hóa"),
        new(MasterData.ItemManage, "Thêm, sửa hàng hóa"),
        new(MasterData.SupplierView, "Xem nhà cung cấp"),
        new(MasterData.SupplierManage, "Thêm, sửa nhà cung cấp"),
        new(MasterData.SupplierApprove, "Duyệt hồ sơ, tài khoản ngân hàng nhà cung cấp"),
        new(MasterData.ConflictView, "Xem mọi kê khai xung đột lợi ích"),
        new(MasterData.ConflictManage, "Ghi nhận thay, kết thúc kê khai xung đột lợi ích"),
        new(MasterData.SoleSourceView, "Xem phê duyệt NCC độc quyền"),
        new(MasterData.SoleSourceManage, "Lập, gửi duyệt, kết thúc phê duyệt NCC độc quyền"),
        new(MasterData.SoleSourceApprove, "Duyệt NCC độc quyền"),
        new(MasterData.SupplierReviewView, "Xem đánh giá NCC định kỳ"),
        new(MasterData.SupplierReviewManage, "Lập, hoàn thành, hủy phiếu đánh giá NCC định kỳ"),
        new(MasterData.CatalogEditShared, "Sửa thông tin danh mục dùng chung (trừ mã)"),
        new(Purchase.PrView, "Xem đề nghị mua hàng"),
        new(Purchase.PrCreate, "Lập đề nghị mua hàng"),
        new(Purchase.PrApprove, "Duyệt đề nghị mua hàng"),
        new(Purchase.PoView, "Xem đơn mua hàng"),
        new(Purchase.PoCreate, "Lập đơn mua hàng"),
        new(Purchase.PoApprove, "Duyệt đơn mua hàng"),
        new(Purchase.ContractView, "Xem hợp đồng nguyên tắc"),
        new(Purchase.ContractManage, "Thêm, sửa hợp đồng nguyên tắc"),
        new(Purchase.ContractApprove, "Duyệt hợp đồng nguyên tắc"),
        new(Purchase.BudgetView, "Xem ngân sách bộ phận"),
        new(Purchase.BudgetManage, "Lập, sửa ngân sách bộ phận"),
        new(Purchase.DemandPlanView, "Xem kế hoạch nhu cầu"),
        new(Purchase.DemandPlanManage, "Lập, chốt, điều chỉnh, hủy kế hoạch nhu cầu"),
        new(Purchase.DemandPlanViewAllCompanies, "Xem kế hoạch nhu cầu toàn tập đoàn"),
        new(Purchase.PooledOrderView, "Xem phiếu đặt hàng gộp"),
        new(Purchase.PooledOrderManage, "Lập phiếu đặt hàng gộp, tạo PO cho các công ty"),
        new(Inventory.DocumentView, "Xem chứng từ kho"),
        new(Inventory.DocumentCreate, "Lập chứng từ kho"),
        new(Inventory.DocumentPost, "Ghi sổ chứng từ kho"),
        new(Inventory.DocumentUnpost, "Bỏ ghi sổ chứng từ kho"),
        new(Inventory.AdjustmentCreate, "Lập phiếu điều chỉnh, kiểm kê kho"),
        new(Inventory.AdjustmentPost, "Ghi sổ phiếu điều chỉnh, kiểm kê kho"),
        new(Inventory.StockView, "Xem tồn kho, thẻ kho"),
        new(Inventory.PeriodView, "Xem kỳ kho"),
        new(Inventory.PeriodOpen, "Tạo lịch và mở kỳ kho"),
        new(Inventory.PeriodClose, "Đóng kỳ kho"),
        new(Inventory.PeriodReopen, "Mở lại kỳ kho"),
        new(Pricing.PriceListView, "Xem bảng giá nhà cung cấp"),
        new(Pricing.PriceListManage, "Thêm, sửa bảng giá"),
        new(Pricing.PriceListApprove, "Duyệt bảng giá nhà cung cấp"),
        new(Pricing.CompareView, "So sánh giá"),
        new(Pricing.MonthlyApprovalView, "Xem phiếu duyệt NCC và giá tháng"),
        new(Pricing.MonthlyApprovalManage, "Lập, gửi duyệt, điều chỉnh, hủy phiếu duyệt NCC và giá tháng"),
        new(Pricing.MonthlyApprovalApprove, "Duyệt NCC và giá tháng"),
        new(Pricing.VolumeRebateView, "Xem thỏa thuận chiết khấu sản lượng gộp, theo dõi bậc"),
        new(Pricing.VolumeRebateManage, "Lập, gửi duyệt, hủy thỏa thuận chiết khấu sản lượng gộp"),
        new(Pricing.VolumeRebateApprove, "Duyệt thỏa thuận chiết khấu sản lượng gộp"),
        new(Pricing.PriceBankView, "Xem ngân hàng giá (khảo sát giá thị trường)"),
        new(Pricing.PriceBankManage, "Lập, chốt, hủy phiếu khảo sát giá thị trường"),
        new(Sourcing.RfqView, "Xem yêu cầu báo giá, tìm nguồn"),
        new(Sourcing.RfqManage, "Lập yêu cầu báo giá"),
        new(Sourcing.RfqAward, "Chọn nhà cung cấp trúng thầu"),
        new(Sourcing.RfqApproveAward, "Duyệt kết quả chọn thầu"),
        new(Report.StockView, "Xem báo cáo nhập – xuất – tồn"),
        new(Report.PurchaseView, "Xem báo cáo mua hàng"),
        new(Report.PriceViewAllCompanies, "Xem so sánh giá toàn tập đoàn"),
        new(Report.StockViewAllCompanies, "Xem tồn kho toàn tập đoàn"),
        new(Report.PurchaseViewAllCompanies, "Xem báo cáo mua hàng toàn tập đoàn"),
        new(Report.AccountingExport, "Xuất chứng từ cho kế toán"),
        new(Analysis.PurchaseHistoryView, "Xem dữ liệu mua lịch sử"),
        new(Analysis.PurchaseHistoryImport, "Nhập, xóa lô dữ liệu mua lịch sử"),
        new(Analysis.PurchaseHistoryViewAllCompanies, "Xem phân tích giá hợp nhất toàn tập đoàn"),
        new(Control.AnomalyView, "Xem dấu hiệu bất thường, sổ vi phạm (KSNB)"),
        new(Control.FindingManage, "Ghi nhận, kết luận vi phạm (KSNB)"),
        new(Control.FindingReceive, "Nhận thông báo vi phạm, xem sổ vi phạm (lãnh đạo)"),
    ];

    /// <summary>
    /// Lọc ra các mã quyền không có trong danh mục – dùng khi lưu quyền của vai trò, chặn client gửi mã tự đặt.
    /// </summary>
    /// <param name="codes">Mã quyền cần kiểm tra.</param>
    /// <returns>Các mã không hợp lệ (rỗng nếu tất cả hợp lệ).</returns>
    public static IReadOnlyList<string> FindUnknown(IEnumerable<string> codes) =>
        codes.Where(c => !KnownCodes.Contains(c)).Distinct().ToList();

    /// <summary>Tên tiếng Việt của module, theo thứ tự hiển thị trên màn hình phân quyền.</summary>
    public static readonly IReadOnlyList<(string Module, string Name)> Modules =
    [
        ("System", "Hệ thống"),
        ("Organization", "Tổ chức"),
        ("MasterData", "Danh mục"),
        ("Purchase", "Mua hàng"),
        ("Inventory", "Kho"),
        ("Pricing", "Giá và nhà cung cấp"),
        ("Sourcing", "Tìm nguồn và đấu thầu"),
        ("Report", "Báo cáo"),
        ("Analysis", "Phân tích dữ liệu mua"),
        ("Control", "Kiểm soát nội bộ"),
    ];

    private static readonly HashSet<string> KnownCodes = [.. All.Select(p => p.Code)];
}
