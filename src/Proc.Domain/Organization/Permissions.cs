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
        public const string CompanyView = "System.Company.View";
        public const string CompanyManage = "System.Company.Manage";
        public const string UserView = "System.User.View";
        public const string UserManage = "System.User.Manage";
        public const string RoleView = "System.Role.View";
        public const string RoleManage = "System.Role.Manage";
        public const string LogView = "System.Log.View";
        public const string ParameterView = "System.Parameter.View";
        public const string ParameterManage = "System.Parameter.Manage";
    }

    /// <summary>Tổ chức: kho.</summary>
    public static class Organization
    {
        public const string WarehouseView = "Organization.Warehouse.View";
        public const string WarehouseManage = "Organization.Warehouse.Manage";
    }

    /// <summary>Danh mục dùng chung: nhóm hàng, hàng hóa, nhà cung cấp.</summary>
    public static class MasterData
    {
        public const string ItemCategoryView = "MasterData.ItemCategory.View";
        public const string ItemCategoryManage = "MasterData.ItemCategory.Manage";
        public const string ItemView = "MasterData.Item.View";
        public const string ItemManage = "MasterData.Item.Manage";
        public const string SupplierView = "MasterData.Supplier.View";
        public const string SupplierManage = "MasterData.Supplier.Manage";
    }

    /// <summary>Mua hàng: PR, PO, hợp đồng khung.</summary>
    public static class Purchase
    {
        public const string PrView = "Purchase.PR.View";
        public const string PrCreate = "Purchase.PR.Create";
        public const string PrApprove = "Purchase.PR.Approve";
        public const string PoView = "Purchase.PO.View";
        public const string PoCreate = "Purchase.PO.Create";
        public const string PoApprove = "Purchase.PO.Approve";
        public const string ContractView = "Purchase.Contract.View";
        public const string ContractManage = "Purchase.Contract.Manage";
    }

    /// <summary>Kho: chứng từ kho, tồn kho, kỳ kho.</summary>
    public static class Inventory
    {
        public const string DocumentView = "Inventory.Document.View";
        public const string DocumentCreate = "Inventory.Document.Create";
        public const string DocumentPost = "Inventory.Document.Post";
        public const string DocumentUnpost = "Inventory.Document.Unpost";
        public const string StockView = "Inventory.Stock.View";
        public const string PeriodView = "Inventory.Period.View";
        public const string PeriodClose = "Inventory.Period.Close";
        public const string PeriodReopen = "Inventory.Period.Reopen";
    }

    /// <summary>Giá và nhà cung cấp.</summary>
    public static class Pricing
    {
        public const string PriceListView = "Pricing.PriceList.View";
        public const string PriceListManage = "Pricing.PriceList.Manage";
        public const string CompareView = "Pricing.Compare.View";
    }

    /// <summary>Tìm nguồn và đấu thầu.</summary>
    public static class Sourcing
    {
        public const string RfqView = "Sourcing.Rfq.View";
        public const string RfqManage = "Sourcing.Rfq.Manage";
        public const string RfqAward = "Sourcing.Rfq.Award";
    }

    /// <summary>Báo cáo.</summary>
    public static class Report
    {
        public const string StockView = "Report.Stock.View";
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
}
