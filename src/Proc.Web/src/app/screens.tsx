import {
  AccountBookOutlined,
  AlertOutlined,
  UserSwitchOutlined,
  ApartmentOutlined,
  AuditOutlined,
  DashboardOutlined,
  BarChartOutlined,
  BankOutlined,
  BuildOutlined,
  BellOutlined,
  ClusterOutlined,
  FieldNumberOutlined,
  ExportOutlined,
  TrophyOutlined,
  FieldTimeOutlined,
  FundOutlined,
  HistoryOutlined,
  NodeIndexOutlined,
  FundViewOutlined,
  StarOutlined,
  ColumnWidthOutlined,
  CalendarOutlined,
  CheckSquareOutlined,
  ContainerOutlined,
  DiffOutlined,
  FileAddOutlined,
  FileSearchOutlined,
  FileTextOutlined,
  GroupOutlined,
  HomeOutlined,
  InboxOutlined,
  LineChartOutlined,
  LoginOutlined,
  NumberOutlined,
  ProfileOutlined,
  SafetyOutlined,
  SafetyCertificateOutlined,
  ScheduleOutlined,
  ShopOutlined,
  ShoppingCartOutlined,
  SolutionOutlined,
  SwapOutlined,
  TableOutlined,
  ReconciliationOutlined,
  DollarOutlined,
  TagsOutlined,
  TeamOutlined,
  UserOutlined,
} from '@ant-design/icons'
import type { ReactNode } from 'react'
import { Permission } from '../auth/permissions'

/** Nhóm chức năng theo module – dùng cho menu module trên Shellbar và cây điều hướng (design.md mục 1.2, 17.1). */
export type ModuleKey = 'daily' | 'purchasing' | 'pricing' | 'sourcing' | 'reports' | 'system'

/** Một nhóm module trên Shellbar. */
export interface ModuleGroup {
  key: ModuleKey
  label: string
  /** Vị trí chữ gạch chân trong nhãn; Alt + chữ đó mở menu (ui-guide.md mục Bàn phím). */
  hotkeyIndex: number
}

/** Một màn hình của ứng dụng. */
export interface Screen {
  /** Mã gõ được ở ô lệnh, ví dụ "PO". */
  code: string
  /** Tên màn hình đầy đủ: tiêu đề trang và tiêu đề tab trình duyệt. */
  title: string
  /** Tên ngắn trên cây điều hướng và menu. */
  navLabel: string
  path: string
  icon: ReactNode
  group: ModuleKey
  /** Quyền cần có để thấy và mở màn hình; không khai báo = ai đăng nhập cũng mở được. */
  permission?: string
  /** Có một trong các quyền này là mở được (thay cho `permission` khi nhiều vai trò cùng xem, ví dụ sổ vi phạm: KSNB hoặc lãnh đạo). */
  anyPermission?: readonly string[]
}

/**
 * Người dùng có được mở màn hình không (theo `permission` hoặc `anyPermission`).
 * @param screen Màn hình.
 * @param hasPermission Hàm kiểm tra quyền của người dùng tại công ty đang làm việc.
 * @returns true nếu được mở.
 */
export function canOpenScreen(screen: Screen, hasPermission: (code: string) => boolean): boolean {
  if (screen.permission && !hasPermission(screen.permission)) return false
  return !screen.anyPermission || screen.anyPermission.some(hasPermission)
}

/**
 * Các nhóm menu theo thứ tự hiển thị. Chữ gạch chân chọn sao cho không trùng nhau:
 * Hằng _n_gày, _M_ua hàng, _G_iá và NCC, Đấu _t_hầu, _B_áo cáo, _H_ệ thống.
 */
export const moduleGroups: ModuleGroup[] = [
  { key: 'daily', label: 'Hằng ngày', hotkeyIndex: 5 },
  { key: 'purchasing', label: 'Mua hàng', hotkeyIndex: 0 },
  { key: 'pricing', label: 'Giá và NCC', hotkeyIndex: 0 },
  { key: 'sourcing', label: 'Đấu thầu', hotkeyIndex: 4 },
  { key: 'reports', label: 'Báo cáo', hotkeyIndex: 0 },
  { key: 'system', label: 'Hệ thống', hotkeyIndex: 0 },
]

/**
 * Danh mục màn hình. Màn hình chưa làm vẫn có mặt để menu và cây điều hướng đủ bố cục;
 * khi mở sẽ hiện trang "đang xây dựng" (router.tsx).
 */
export const screens: Screen[] = [
  { code: 'HOME', title: 'Trang chủ', navLabel: 'Trang chủ', path: '/', icon: <HomeOutlined />, group: 'daily' },
  { code: 'TODO', title: 'Việc cần làm', navLabel: 'Việc cần làm', path: '/hang-ngay/viec-can-lam', icon: <CheckSquareOutlined />, group: 'daily' },
  { code: 'UQD', title: 'Ủy quyền duyệt', navLabel: 'Ủy quyền duyệt', path: '/hang-ngay/uy-quyen-duyet', icon: <UserSwitchOutlined />, group: 'daily' },
  { code: 'NOTI', title: 'Thông báo', navLabel: 'Thông báo', path: '/hang-ngay/thong-bao', icon: <BellOutlined />, group: 'daily' },
  { code: 'DOCS', title: 'Tra cứu chứng từ', navLabel: 'Tra cứu chứng từ', path: '/hang-ngay/tra-cuu-chung-tu', icon: <FileSearchOutlined />, group: 'daily' },

  { code: 'PR', title: 'Danh sách đề nghị mua hàng (PR)', navLabel: 'Đề nghị mua hàng', path: '/mua-hang/pr', icon: <FileAddOutlined />, group: 'purchasing', permission: Permission.PrView },
  // Bảng tổng hợp đề xuất mua 2A/2B (lát Y5c-2, design.md mục 19.3 bước 2): Phòng CUVT gom dòng đề nghị đã duyệt, gán đơn vị mua, trình duyệt.
  { code: 'BTH', title: 'Bảng tổng hợp đề xuất mua (BTH)', navLabel: 'Bảng tổng hợp đề xuất', path: '/mua-hang/bang-tong-hop', icon: <TableOutlined />, group: 'purchasing', anyPermission: [Permission.SummaryView, Permission.SummaryManage, Permission.SummaryApprove] },
  { code: 'PO', title: 'Danh sách đơn mua hàng (PO)', navLabel: 'Đơn mua hàng', path: '/mua-hang/po', icon: <ShoppingCartOutlined />, group: 'purchasing', permission: Permission.PoView },
  // Phiếu xác nhận nhận hàng thay phiếu nhập kho từ 0.22.0 (lát K2, design.md mục 19.4): thủ kho / phòng ban đề xuất lập, đơn vị đặt hàng nghiệm thu.
  { code: 'XNH', title: 'Xác nhận nhận hàng (XNH)', navLabel: 'Xác nhận nhận hàng', path: '/mua-hang/xac-nhan-nhan-hang', icon: <InboxOutlined />, group: 'purchasing', anyPermission: [Permission.ReceiptView, Permission.ReceiptManage, Permission.ReceiptAccept] },
  // Đề nghị thanh toán 5A/5B (lát Y8, design.md mục 19.3 bước 7): lập cho PO đã nhận hàng có hóa đơn, duyệt, kế toán ghi Đã chi.
  { code: 'DNT', title: 'Đề nghị thanh toán (DNT)', navLabel: 'Đề nghị thanh toán', path: '/mua-hang/de-nghi-thanh-toan', icon: <AccountBookOutlined />, group: 'purchasing', anyPermission: [Permission.PaymentView, Permission.PaymentCreate, Permission.PaymentApprove, Permission.PaymentPay] },
  // Bảng kê chi phí tạp vụ 2C (lát Y11a, design.md mục 19.2 loại C): bộ phận kê chi phí lặt vặt đã phát sinh, tính định mức tháng, trình duyệt.
  { code: 'BKT', title: 'Bảng kê chi phí tạp vụ (BKT)', navLabel: 'Bảng kê tạp vụ', path: '/mua-hang/bang-ke-tap-vu', icon: <ProfileOutlined />, group: 'purchasing', anyPermission: [Permission.SundryView, Permission.SundryCreate, Permission.SundryApprove] },
  { code: 'POTRK', title: 'Theo dõi giao hàng', navLabel: 'Theo dõi giao hàng', path: '/mua-hang/theo-doi-giao-hang', icon: <ScheduleOutlined />, group: 'purchasing', permission: Permission.PoView },
  { code: 'KHN', title: 'Kế hoạch nhu cầu', navLabel: 'Kế hoạch nhu cầu', path: '/mua-hang/ke-hoach-nhu-cau', icon: <CalendarOutlined />, group: 'purchasing', permission: Permission.DemandPlanView },
  { code: 'KHNTH', title: 'Tổng hợp nhu cầu', navLabel: 'Tổng hợp nhu cầu', path: '/mua-hang/tong-hop-nhu-cau', icon: <BarChartOutlined />, group: 'purchasing', permission: Permission.DemandPlanView },
  // Đặt hàng gộp (design.md mục 18.25 – lát G7): Phòng cung ứng chung đặt cho các công ty ở tổng công ty, sinh mỗi pháp nhân một PO.
  { code: 'DHG', title: 'Đặt hàng gộp', navLabel: 'Đặt hàng gộp', path: '/mua-hang/dat-hang-gop', icon: <ClusterOutlined />, group: 'purchasing', permission: Permission.PooledOrderView },
  { code: 'CTDT', title: 'Công trình – dự toán', navLabel: 'Công trình – dự toán', path: '/mua-hang/cong-trinh', icon: <BuildOutlined />, group: 'purchasing', anyPermission: [Permission.ProjectView, Permission.ProjectManage] },
  // Đơn vị được giao mua từng dòng trên bảng tổng hợp 2A/2B (lát Y5a, design.md mục 19.3 – Q3).
  // Đơn bán nội bộ (lát X7, design.md mục 19.3): PO công ty khác đặt cho NCC nội bộ của công ty mình – công ty bán xác nhận / từ chối.
  { code: 'DBN', title: 'Đơn bán nội bộ', navLabel: 'Đơn bán nội bộ', path: '/mua-hang/don-ban-noi-bo', icon: <SwapOutlined />, group: 'purchasing', anyPermission: [Permission.InternalSaleView, Permission.InternalSaleManage] },
  { code: 'DVM', title: 'Danh mục đơn vị mua', navLabel: 'Đơn vị mua', path: '/mua-hang/don-vi-mua', icon: <ClusterOutlined />, group: 'purchasing', anyPermission: [Permission.UnitView, Permission.UnitManage] },
  { code: 'BUDG', title: 'Ngân sách bộ phận', navLabel: 'Ngân sách bộ phận', path: '/mua-hang/ngan-sach', icon: <FundOutlined />, group: 'purchasing', anyPermission: [Permission.BudgetView, Permission.BudgetManage] },
  { code: 'CTR', title: 'Hợp đồng nguyên tắc', navLabel: 'Hợp đồng nguyên tắc', path: '/mua-hang/hop-dong-khung', icon: <SolutionOutlined />, group: 'purchasing', permission: Permission.ContractView },

  { code: 'SUP', title: 'Danh mục nhà cung cấp', navLabel: 'Nhà cung cấp', path: '/gia-ncc/nha-cung-cap', icon: <ShopOutlined />, group: 'pricing', permission: Permission.SupplierView },
  { code: 'SUPAPR', title: 'Yêu cầu duyệt nhà cung cấp', navLabel: 'Duyệt NCC', path: '/gia-ncc/duyet-ncc', icon: <AuditOutlined />, group: 'pricing', permission: Permission.SupplierView },
  { code: 'COI', title: 'Kê khai xung đột lợi ích', navLabel: 'Xung đột lợi ích', path: '/gia-ncc/xung-dot-loi-ich', icon: <AlertOutlined />, group: 'pricing' },
  { code: 'NDQ', title: 'NCC độc quyền', navLabel: 'NCC độc quyền', path: '/gia-ncc/ncc-doc-quyen', icon: <SafetyCertificateOutlined />, group: 'pricing', permission: Permission.SoleSourceView },
  // Duyệt NCC và giá tháng (design.md mục 18.25 – lát G2): tổng công ty lập, Ban cung ứng duyệt, công ty áp dụng xem.
  { code: 'GTH', title: 'Duyệt NCC và giá tháng', navLabel: 'Duyệt NCC và giá tháng', path: '/gia-ncc/duyet-thang', icon: <CalendarOutlined />, group: 'pricing', permission: Permission.MonthlyApprovalView },
  // Khảo sát giá thị trường – ngân hàng giá (design.md mục 18.25 – lát G4a): Văn phòng Ban ghi giá khảo sát ở tổng công ty.
  { code: 'KSG', title: 'Khảo sát giá thị trường', navLabel: 'Khảo sát giá thị trường', path: '/gia-ncc/khao-sat-gia', icon: <FundOutlined />, group: 'pricing', permission: Permission.PriceBankView },
  // Chiết khấu bậc theo sản lượng gộp (design.md mục 18.25 – lát G6a): thỏa thuận tổng công ty lập, Ban duyệt; theo dõi lũy kế so với bậc.
  { code: 'CKB', title: 'Chiết khấu sản lượng gộp', navLabel: 'Chiết khấu sản lượng gộp', path: '/gia-ncc/chiet-khau-san-luong', icon: <FundOutlined />, group: 'pricing', permission: Permission.VolumeRebateView },
  { code: 'CKBT', title: 'Theo dõi bậc chiết khấu sản lượng gộp', navLabel: 'Theo dõi bậc chiết khấu', path: '/gia-ncc/theo-doi-bac-chiet-khau', icon: <FundOutlined />, group: 'pricing', permission: Permission.VolumeRebateView },
  { code: 'SUPCOV', title: 'NCC đủ điều kiện theo nhóm hàng', navLabel: 'NCC đủ điều kiện', path: '/gia-ncc/ncc-du-dieu-kien', icon: <SafetyCertificateOutlined />, group: 'pricing', permission: Permission.SupplierView },
  { code: 'SUPREV', title: 'Đánh giá NCC định kỳ', navLabel: 'Đánh giá NCC định kỳ', path: '/gia-ncc/danh-gia-ncc-dinh-ky', icon: <StarOutlined />, group: 'pricing', anyPermission: [Permission.SupplierReviewView, Permission.SupplierReviewManage] },
  // Hồ sơ đánh giá NCC mới (lát G8, design.md mục 18.25): Phòng cung ứng đánh giá, Văn phòng Ban đề xuất, Hội đồng duyệt ở SUPAPR.
  // Đề nghị cập nhật hồ sơ NCC gửi từ Portal (lát P2a, design.md 19.5): Phòng CUVT tổng công ty chấp nhận / từ chối.
  { code: 'HSC', title: 'Đề nghị cập nhật hồ sơ NCC', navLabel: 'Đề nghị cập nhật NCC', path: '/gia-ncc/de-nghi-cap-nhat-ncc', icon: <AuditOutlined />, group: 'pricing', permission: Permission.SupplierView },
  { code: 'HSN', title: 'Hồ sơ đánh giá NCC mới', navLabel: 'Hồ sơ NCC mới', path: '/gia-ncc/ho-so-ncc-moi', icon: <AuditOutlined />, group: 'pricing', anyPermission: [Permission.SupplierOnboardingView, Permission.SupplierOnboardingManage, Permission.SupplierOnboardingPropose] },
  { code: 'SGRP', title: 'Nhóm nhà cung cấp', navLabel: 'Nhóm NCC', path: '/gia-ncc/nhom-ncc', icon: <GroupOutlined />, group: 'pricing', permission: Permission.SupplierView },
  { code: 'ITEM', title: 'Danh mục hàng hóa', navLabel: 'Hàng hóa', path: '/gia-ncc/hang-hoa', icon: <TagsOutlined />, group: 'pricing', permission: Permission.ItemView },
  { code: 'CAT', title: 'Nhóm hàng', navLabel: 'Nhóm hàng', path: '/gia-ncc/nhom-hang', icon: <ClusterOutlined />, group: 'pricing', permission: Permission.ItemCategoryView },
  { code: 'UOM', title: 'Đơn vị tính', navLabel: 'Đơn vị tính', path: '/gia-ncc/don-vi-tinh', icon: <ColumnWidthOutlined />, group: 'pricing', permission: Permission.ItemView },
  { code: 'IPR', title: 'Bảng giá nội bộ', navLabel: 'Bảng giá nội bộ', path: '/gia-ncc/gia-noi-bo', icon: <DollarOutlined />, group: 'pricing', permission: Permission.PriceListView },
  { code: 'PRL', title: 'Bảng giá NCC', navLabel: 'Bảng giá NCC', path: '/gia-ncc/bang-gia', icon: <FileTextOutlined />, group: 'pricing', permission: Permission.PriceListView },
  { code: 'PCMP', title: 'So sánh giá', navLabel: 'So sánh giá', path: '/gia-ncc/so-sanh-gia', icon: <LineChartOutlined />, group: 'pricing', permission: Permission.PriceCompareView },

  { code: 'SRC', title: 'Danh sách tìm nguồn', navLabel: 'Tìm nguồn', path: '/dau-thau/tim-nguon', icon: <FileSearchOutlined />, group: 'sourcing', permission: Permission.RfqView },
  { code: 'RFQ', title: 'Yêu cầu báo giá (RFQ)', navLabel: 'Yêu cầu báo giá', path: '/dau-thau/rfq', icon: <ContainerOutlined />, group: 'sourcing', permission: Permission.RfqView },
  { code: 'QCMP', title: 'So sánh báo giá', navLabel: 'So sánh báo giá', path: '/dau-thau/so-sanh-bao-gia', icon: <DiffOutlined />, group: 'sourcing', permission: Permission.RfqView },

  { code: 'PRPEND', title: 'PR chưa xử lý', navLabel: 'PR chưa xử lý', path: '/bao-cao/pr-chua-xu-ly', icon: <FieldTimeOutlined />, group: 'reports', permission: Permission.ReportPurchaseView },
  { code: 'SLA', title: 'SLA mua hàng', navLabel: 'SLA mua hàng', path: '/bao-cao/sla-mua-hang', icon: <FieldTimeOutlined />, group: 'reports', permission: Permission.ReportPurchaseView },
  { code: 'QCDEV', title: 'Sai lệch nhận hàng', navLabel: 'Sai lệch nhận hàng', path: '/bao-cao/sai-lech-nhan-hang', icon: <FieldTimeOutlined />, group: 'reports', permission: Permission.ReportPurchaseView },
  { code: 'OTIF', title: 'SL đặt – SL thực nhận (OTIF)', navLabel: 'SL đặt – thực nhận', path: '/bao-cao/sl-dat-thuc-nhan', icon: <DiffOutlined />, group: 'reports', permission: Permission.ReportPurchaseView },
  // Lát Y9a (design.md mục 19.3 "Lát Y9"): hóa đơn XNH – đề nghị thanh toán – đã chi – còn phải trả theo PO.
  { code: 'TTPO', title: 'Theo dõi thanh toán', navLabel: 'Theo dõi thanh toán', path: '/bao-cao/theo-doi-thanh-toan', icon: <DollarOutlined />, group: 'reports', permission: Permission.ReportPurchaseView },
  { code: 'MATCH3', title: 'Đối chiếu PO – nhận – hóa đơn', navLabel: 'Đối chiếu PO – nhận – HĐ', path: '/bao-cao/doi-chieu-po-nhan-hoa-don', icon: <ReconciliationOutlined />, group: 'reports', permission: Permission.ReportPurchaseView },
  { code: 'RPSUP', title: 'Mua hàng theo NCC', navLabel: 'Mua hàng theo NCC', path: '/bao-cao/mua-hang-theo-ncc', icon: <BarChartOutlined />, group: 'reports', permission: Permission.ReportPurchaseView },
  { code: 'SUPEVAL', title: 'Đánh giá nhà cung cấp', navLabel: 'Đánh giá NCC', path: '/bao-cao/danh-gia-ncc', icon: <StarOutlined />, group: 'reports', permission: Permission.ReportPurchaseView },
  { code: 'SAVE', title: 'Tiết kiệm từ đấu thầu', navLabel: 'Tiết kiệm đấu thầu', path: '/bao-cao/tiet-kiem-dau-thau', icon: <FundOutlined />, group: 'reports', permission: Permission.ReportPurchaseView },
  { code: 'PRCW', title: 'Giá mua & mua ngoài hợp đồng', navLabel: 'Mua ngoài hợp đồng', path: '/bao-cao/gia-mua-ngoai-hop-dong', icon: <AuditOutlined />, group: 'reports', permission: Permission.ReportPurchaseView },
  { code: 'BCBAN', title: 'Báo cáo tháng cho Ban', navLabel: 'Báo cáo tháng cho Ban', path: '/bao-cao/bao-cao-thang-ban', icon: <SolutionOutlined />, group: 'reports', permission: Permission.ReportPurchaseView },
  { code: 'KPI', title: 'Bảng KPI cung ứng', navLabel: 'Bảng KPI', path: '/bao-cao/kpi', icon: <DashboardOutlined />, group: 'reports', permission: Permission.ReportPurchaseView },
  { code: 'PRVAR', title: 'Biến động giá mua', navLabel: 'Biến động giá mua', path: '/bao-cao/bien-dong-gia-mua', icon: <LineChartOutlined />, group: 'reports', permission: Permission.ReportPurchaseView },
  { code: 'SAVSTD', title: 'Tiết kiệm so giá chuẩn', navLabel: 'Tiết kiệm so giá chuẩn', path: '/bao-cao/tiet-kiem-gia-chuan', icon: <FundOutlined />, group: 'reports', permission: Permission.ReportPurchaseView },
  { code: 'PHIS', title: 'Dữ liệu mua lịch sử', navLabel: 'Dữ liệu mua lịch sử', path: '/bao-cao/du-lieu-mua-lich-su', icon: <HistoryOutlined />, group: 'reports', permission: Permission.PurchaseHistoryView },
  { code: 'PHMAP', title: 'Ánh xạ dữ liệu mua', navLabel: 'Ánh xạ dữ liệu mua', path: '/bao-cao/anh-xa-du-lieu-mua', icon: <NodeIndexOutlined />, group: 'reports', permission: Permission.PurchaseHistoryView },
  { code: 'ANOM', title: 'Dấu hiệu bất thường (KSNB)', navLabel: 'Dấu hiệu bất thường', path: '/bao-cao/dau-hieu-bat-thuong', icon: <AlertOutlined />, group: 'reports', permission: Permission.ControlAnomalyView },
  { code: 'VIOL', title: 'Sổ vi phạm (KSNB)', navLabel: 'Sổ vi phạm', path: '/bao-cao/so-vi-pham', icon: <SafetyOutlined />, group: 'reports', anyPermission: [Permission.ControlAnomalyView, Permission.ControlFindingReceive] },
  { code: 'PHAN', title: 'Phân tích giá hợp nhất', navLabel: 'Phân tích giá hợp nhất', path: '/bao-cao/phan-tich-gia-hop-nhat', icon: <FundViewOutlined />, group: 'reports', permission: Permission.PurchaseHistoryView },

  // Lát Y9b (design.md mục 19.3 "Lát Y9"): thay ACCX từ khi bỏ kho – PO, phiếu nhận hàng + hóa đơn, đề nghị thanh toán cho kế toán.
  { code: 'ACCP', title: 'Xuất chứng từ mua cho kế toán', navLabel: 'Xuất chứng từ mua', path: '/bao-cao/xuat-chung-tu-mua', icon: <ExportOutlined />, group: 'reports', permission: Permission.PurchaseAccountingExport },

  { code: 'COMP', title: 'Danh mục công ty', navLabel: 'Công ty', path: '/he-thong/cong-ty', icon: <BankOutlined />, group: 'system', permission: Permission.CompanyView },
  { code: 'WH', title: 'Danh mục kho', navLabel: 'Kho', path: '/he-thong/kho', icon: <ApartmentOutlined />, group: 'system', permission: Permission.WarehouseView },
  { code: 'DEPT', title: 'Danh mục bộ phận', navLabel: 'Bộ phận', path: '/he-thong/bo-phan', icon: <ClusterOutlined />, group: 'system', permission: Permission.DepartmentView },
  { code: 'DIV', title: 'Danh mục khối', navLabel: 'Khối', path: '/he-thong/khoi', icon: <ClusterOutlined />, group: 'system', permission: Permission.DepartmentView },
  { code: 'USER', title: 'Người dùng', navLabel: 'Người dùng', path: '/he-thong/nguoi-dung', icon: <UserOutlined />, group: 'system', permission: Permission.UserView },
  { code: 'ROLE', title: 'Vai trò và phân quyền', navLabel: 'Vai trò', path: '/he-thong/vai-tro', icon: <TeamOutlined />, group: 'system', permission: Permission.RoleView },
  { code: 'APRL', title: 'Quy trình duyệt', navLabel: 'Quy trình duyệt', path: '/he-thong/quy-trinh-duyet', icon: <CheckSquareOutlined />, group: 'system', permission: Permission.ParameterView },
  { code: 'LOG', title: 'Nhật ký thao tác', navLabel: 'Nhật ký thao tác', path: '/he-thong/nhat-ky', icon: <AuditOutlined />, group: 'system', permission: Permission.LogView },
  { code: 'ACC', title: 'Nhật ký truy cập', navLabel: 'Nhật ký truy cập', path: '/he-thong/nhat-ky-truy-cap', icon: <LoginOutlined />, group: 'system', permission: Permission.LogView },
  { code: 'RATE', title: 'Xếp loại nhà cung cấp', navLabel: 'Xếp loại NCC', path: '/he-thong/xep-loai-ncc', icon: <TrophyOutlined />, group: 'system', permission: Permission.ParameterView },
  { code: 'DSDM', title: 'Đánh số danh mục', navLabel: 'Đánh số danh mục', path: '/he-thong/danh-so-danh-muc', icon: <FieldNumberOutlined />, group: 'system', permission: Permission.ParameterView },
  { code: 'DSCT', title: 'Đánh số chứng từ', navLabel: 'Đánh số chứng từ', path: '/he-thong/danh-so-chung-tu', icon: <NumberOutlined />, group: 'system', permission: Permission.ParameterView },
  { code: 'SEC', title: 'Tham số hệ thống', navLabel: 'Tham số hệ thống', path: '/he-thong/tham-so', icon: <SafetyOutlined />, group: 'system', permission: Permission.ParameterView },
]

/**
 * Tìm màn hình theo đường dẫn hiện tại (kể cả trang con, ví dụ một phiếu của màn hình danh sách phiếu).
 * @param pathname location.pathname.
 * @returns Màn hình tương ứng, hoặc undefined nếu không có.
 */
export function findScreenByPath(pathname: string): Screen | undefined {
  // Trang con của màn hình (phiếu /mua-hang/po/123) thuộc màn hình cha /mua-hang/po: cùng quyền, cùng mục trên cây điều hướng.
  return screens.find((s) => s.path === pathname) ?? screens.find((s) => s.path !== '/' && pathname.startsWith(`${s.path}/`))
}
