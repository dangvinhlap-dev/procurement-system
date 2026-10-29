import {
  AuditOutlined,
  BarChartOutlined,
  BankOutlined,
  BellOutlined,
  CalendarOutlined,
  CheckSquareOutlined,
  ContainerOutlined,
  DatabaseOutlined,
  DiffOutlined,
  FileAddOutlined,
  FileDoneOutlined,
  FileSearchOutlined,
  FileTextOutlined,
  HomeOutlined,
  ImportOutlined,
  InboxOutlined,
  LineChartOutlined,
  ProfileOutlined,
  RollbackOutlined,
  SafetyOutlined,
  ScheduleOutlined,
  ShopOutlined,
  ShoppingCartOutlined,
  SolutionOutlined,
  SwapOutlined,
  TagsOutlined,
  TeamOutlined,
  UserOutlined,
} from '@ant-design/icons'
import type { ReactNode } from 'react'

/** Nhóm chức năng theo module – dùng cho thanh menu và cây điều hướng (design.md mục 1.2, 17.1). */
export type ModuleKey = 'daily' | 'purchasing' | 'inventory' | 'pricing' | 'sourcing' | 'reports' | 'system'

/** Một nhóm module trên thanh menu. */
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
  /** Tên màn hình đầy đủ: tiêu đề trang và thanh trạng thái. */
  title: string
  /** Tên ngắn trên cây điều hướng và menu. */
  navLabel: string
  path: string
  icon: ReactNode
  group: ModuleKey
}

/**
 * Các nhóm menu theo thứ tự hiển thị. Chữ gạch chân chọn sao cho không trùng nhau:
 * Hằng _n_gày, _M_ua hàng, _K_ho, _G_iá và NCC, Đấu _t_hầu, _B_áo cáo, _H_ệ thống.
 */
export const moduleGroups: ModuleGroup[] = [
  { key: 'daily', label: 'Hằng ngày', hotkeyIndex: 5 },
  { key: 'purchasing', label: 'Mua hàng', hotkeyIndex: 0 },
  { key: 'inventory', label: 'Kho', hotkeyIndex: 0 },
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
  { code: 'NOTI', title: 'Thông báo', navLabel: 'Thông báo', path: '/hang-ngay/thong-bao', icon: <BellOutlined />, group: 'daily' },

  { code: 'PR', title: 'Danh sách đề nghị mua hàng (PR)', navLabel: 'Đề nghị mua hàng', path: '/mua-hang/pr', icon: <FileAddOutlined />, group: 'purchasing' },
  { code: 'PO', title: 'Danh sách đơn mua hàng (PO)', navLabel: 'Đơn mua hàng', path: '/mua-hang/po', icon: <ShoppingCartOutlined />, group: 'purchasing' },
  { code: 'POTRK', title: 'Theo dõi giao hàng', navLabel: 'Theo dõi giao hàng', path: '/mua-hang/theo-doi-giao-hang', icon: <ScheduleOutlined />, group: 'purchasing' },
  { code: 'CTR', title: 'Hợp đồng khung', navLabel: 'Hợp đồng khung', path: '/mua-hang/hop-dong-khung', icon: <SolutionOutlined />, group: 'purchasing' },

  { code: 'GRN', title: 'Phiếu nhập mua từ NCC (GRN)', navLabel: 'Nhập mua NCC', path: '/kho/grn', icon: <ImportOutlined />, group: 'inventory' },
  { code: 'RTV', title: 'Phiếu xuất trả hàng NCC (RTV)', navLabel: 'Xuất trả NCC', path: '/kho/rtv', icon: <RollbackOutlined />, group: 'inventory' },
  { code: 'TRF', title: 'Phiếu chuyển kho nội bộ (TRF)', navLabel: 'Chuyển kho nội bộ', path: '/kho/trf', icon: <SwapOutlined />, group: 'inventory' },
  { code: 'ICS', title: 'Phiếu xuất bán nội bộ (ICS)', navLabel: 'Xuất bán nội bộ', path: '/kho/ics', icon: <FileDoneOutlined />, group: 'inventory' },
  { code: 'ICR', title: 'Phiếu nhập mua nội bộ (ICR)', navLabel: 'Nhập mua nội bộ', path: '/kho/icr', icon: <InboxOutlined />, group: 'inventory' },
  { code: 'STK', title: 'Tồn kho hiện tại', navLabel: 'Tồn kho hiện tại', path: '/kho/ton-kho', icon: <DatabaseOutlined />, group: 'inventory' },
  { code: 'CARD', title: 'Thẻ kho', navLabel: 'Thẻ kho', path: '/kho/the-kho', icon: <ProfileOutlined />, group: 'inventory' },

  { code: 'SUP', title: 'Danh mục nhà cung cấp', navLabel: 'Nhà cung cấp', path: '/gia-ncc/nha-cung-cap', icon: <ShopOutlined />, group: 'pricing' },
  { code: 'ITEM', title: 'Danh mục hàng hóa', navLabel: 'Hàng hóa', path: '/gia-ncc/hang-hoa', icon: <TagsOutlined />, group: 'pricing' },
  { code: 'PRL', title: 'Bảng giá NCC', navLabel: 'Bảng giá NCC', path: '/gia-ncc/bang-gia', icon: <FileTextOutlined />, group: 'pricing' },
  { code: 'PCMP', title: 'So sánh giá', navLabel: 'So sánh giá', path: '/gia-ncc/so-sanh-gia', icon: <LineChartOutlined />, group: 'pricing' },

  { code: 'SRC', title: 'Danh sách tìm nguồn', navLabel: 'Tìm nguồn', path: '/dau-thau/tim-nguon', icon: <FileSearchOutlined />, group: 'sourcing' },
  { code: 'RFQ', title: 'Yêu cầu báo giá (RFQ)', navLabel: 'Yêu cầu báo giá', path: '/dau-thau/rfq', icon: <ContainerOutlined />, group: 'sourcing' },
  { code: 'QCMP', title: 'So sánh báo giá', navLabel: 'So sánh báo giá', path: '/dau-thau/so-sanh-bao-gia', icon: <DiffOutlined />, group: 'sourcing' },

  { code: 'NXT', title: 'Báo cáo nhập – xuất – tồn', navLabel: 'Nhập – xuất – tồn', path: '/bao-cao/nxt', icon: <BarChartOutlined />, group: 'reports' },
  { code: 'RPSUP', title: 'Mua hàng theo NCC', navLabel: 'Mua hàng theo NCC', path: '/bao-cao/mua-hang-theo-ncc', icon: <BarChartOutlined />, group: 'reports' },

  { code: 'COMP', title: 'Danh mục công ty', navLabel: 'Công ty', path: '/he-thong/cong-ty', icon: <BankOutlined />, group: 'system' },
  { code: 'USER', title: 'Người dùng', navLabel: 'Người dùng', path: '/he-thong/nguoi-dung', icon: <UserOutlined />, group: 'system' },
  { code: 'ROLE', title: 'Vai trò và phân quyền', navLabel: 'Vai trò', path: '/he-thong/vai-tro', icon: <TeamOutlined />, group: 'system' },
  { code: 'PER', title: 'Kỳ kho', navLabel: 'Kỳ kho', path: '/he-thong/ky-kho', icon: <CalendarOutlined />, group: 'system' },
  { code: 'LOG', title: 'Nhật ký thao tác', navLabel: 'Nhật ký thao tác', path: '/he-thong/nhat-ky', icon: <AuditOutlined />, group: 'system' },
  { code: 'SEC', title: 'Tham số hệ thống', navLabel: 'Tham số hệ thống', path: '/he-thong/tham-so', icon: <SafetyOutlined />, group: 'system' },
]

/**
 * Tìm màn hình theo đường dẫn hiện tại.
 * @param pathname location.pathname.
 * @returns Màn hình tương ứng, hoặc undefined nếu không có.
 */
export function findScreenByPath(pathname: string): Screen | undefined {
  return screens.find((s) => s.path === pathname)
}
