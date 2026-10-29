/**
 * Mã quyền dùng ở giao diện – phải khớp src/Proc.Domain/Organization/Permissions.cs (design.md mục 9.1).
 * Giao diện chỉ dùng để ẩn menu, nút; API luôn kiểm tra lại quyền ở mỗi request.
 */
export const Permission = {
  CompanyView: 'System.Company.View',
  UserView: 'System.User.View',
  RoleView: 'System.Role.View',
  LogView: 'System.Log.View',
  ParameterView: 'System.Parameter.View',
  WarehouseView: 'Organization.Warehouse.View',
  ItemCategoryView: 'MasterData.ItemCategory.View',
  ItemView: 'MasterData.Item.View',
  SupplierView: 'MasterData.Supplier.View',
  PrView: 'Purchase.PR.View',
  PoView: 'Purchase.PO.View',
  ContractView: 'Purchase.Contract.View',
  InventoryDocumentView: 'Inventory.Document.View',
  StockView: 'Inventory.Stock.View',
  PeriodView: 'Inventory.Period.View',
  PriceListView: 'Pricing.PriceList.View',
  PriceCompareView: 'Pricing.Compare.View',
  RfqView: 'Sourcing.Rfq.View',
  ReportStockView: 'Report.Stock.View',
  ReportPurchaseView: 'Report.Purchase.View',
} as const
