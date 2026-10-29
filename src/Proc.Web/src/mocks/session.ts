/**
 * DỮ LIỆU GIẢ của phiên làm việc (người dùng, công ty, số đếm trên cây điều hướng) cho lát giao diện khung.
 * Sẽ thay bằng dữ liệu đăng nhập và API ở lát phân quyền (design.md mục 13.3, bước 3).
 */
import { demoCompanies, mockPurchaseOrders } from './purchaseOrders'

export const demoUser = { userName: 'an.nguyen', fullName: 'Nguyễn Văn An', initials: 'NA' }

/** Công ty người dùng được làm việc; công ty đầu tiên là mặc định. */
export const demoWorkingCompanies = demoCompanies.map((c) => ({ value: c.code, label: `${c.code} – ${c.name}` }))

/** Số thông báo chưa đọc. */
export const demoUnreadNotifications = 7

/** Mã màn hình người dùng đã ghim lên đầu cây điều hướng. */
export const demoPinnedScreens = ['PO', 'PR', 'NXT']

/** Số đếm bên phải mục trên cây điều hướng; attention = true thì badge đỏ (ui-guide.md). */
export const demoNavCounts: Record<string, { count: number; attention?: boolean }> = {
  PO: { count: mockPurchaseOrders.length },
  PR: { count: 12, attention: true },
  TODO: { count: 5, attention: true },
  NOTI: { count: demoUnreadNotifications },
  POTRK: { count: mockPurchaseOrders.filter((p) => p.status === 'Quá hạn giao').length, attention: true },
  GRN: { count: 41 },
  TRF: { count: 9 },
  ICS: { count: 3 },
  ICR: { count: 2, attention: true },
  SUP: { count: 128 },
  ITEM: { count: 2350 },
  RFQ: { count: 4 },
}
