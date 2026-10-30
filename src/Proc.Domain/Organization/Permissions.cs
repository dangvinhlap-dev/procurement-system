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

    /// <summary>Tổ chức: kho.</summary>
    public static class Organization
    {
        /// <summary>Quyền "Xem danh mục kho".</summary>
        public const string WarehouseView = "Organization.Warehouse.View";
        /// <summary>Quyền "Thêm, sửa kho".</summary>
        public const string WarehouseManage = "Organization.Warehouse.Manage";
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
    }

    /// <summary>Mua hàng: PR, PO, hợp đồng khung.</summary>
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
        /// <summary>Quyền "Xem hợp đồng khung".</summary>
        public const string ContractView = "Purchase.Contract.View";
        /// <summary>Quyền "Thêm, sửa hợp đồng khung".</summary>
        public const string ContractManage = "Purchase.Contract.Manage";
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
        /// <summary>Quyền "So sánh giá".</summary>
        public const string CompareView = "Pricing.Compare.View";
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
        new(MasterData.ItemCategoryView, "Xem nhóm hàng"),
        new(MasterData.ItemCategoryManage, "Thêm, sửa nhóm hàng"),
        new(MasterData.ItemView, "Xem hàng hóa"),
        new(MasterData.ItemManage, "Thêm, sửa hàng hóa"),
        new(MasterData.SupplierView, "Xem nhà cung cấp"),
        new(MasterData.SupplierManage, "Thêm, sửa nhà cung cấp"),
        new(Purchase.PrView, "Xem đề nghị mua hàng"),
        new(Purchase.PrCreate, "Lập đề nghị mua hàng"),
        new(Purchase.PrApprove, "Duyệt đề nghị mua hàng"),
        new(Purchase.PoView, "Xem đơn mua hàng"),
        new(Purchase.PoCreate, "Lập đơn mua hàng"),
        new(Purchase.PoApprove, "Duyệt đơn mua hàng"),
        new(Purchase.ContractView, "Xem hợp đồng khung"),
        new(Purchase.ContractManage, "Thêm, sửa hợp đồng khung"),
        new(Inventory.DocumentView, "Xem chứng từ kho"),
        new(Inventory.DocumentCreate, "Lập chứng từ kho"),
        new(Inventory.DocumentPost, "Ghi sổ chứng từ kho"),
        new(Inventory.DocumentUnpost, "Bỏ ghi sổ chứng từ kho"),
        new(Inventory.StockView, "Xem tồn kho, thẻ kho"),
        new(Inventory.PeriodView, "Xem kỳ kho"),
        new(Inventory.PeriodOpen, "Tạo lịch và mở kỳ kho"),
        new(Inventory.PeriodClose, "Đóng kỳ kho"),
        new(Inventory.PeriodReopen, "Mở lại kỳ kho"),
        new(Pricing.PriceListView, "Xem bảng giá nhà cung cấp"),
        new(Pricing.PriceListManage, "Thêm, sửa bảng giá"),
        new(Pricing.CompareView, "So sánh giá"),
        new(Sourcing.RfqView, "Xem yêu cầu báo giá, tìm nguồn"),
        new(Sourcing.RfqManage, "Lập yêu cầu báo giá"),
        new(Sourcing.RfqAward, "Chọn nhà cung cấp trúng thầu"),
        new(Report.StockView, "Xem báo cáo nhập – xuất – tồn"),
        new(Report.PurchaseView, "Xem báo cáo mua hàng"),
        new(Report.PriceViewAllCompanies, "Xem so sánh giá toàn tập đoàn"),
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
    ];

    private static readonly HashSet<string> KnownCodes = [.. All.Select(p => p.Code)];
}
