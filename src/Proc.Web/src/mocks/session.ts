/**
 * DỮ LIỆU GIẢ cho phần khung chưa có API: số thông báo, màn hình ghim, số đếm trên cây điều hướng.
 * Người dùng và công ty đã lấy từ đăng nhập thật (src/auth).
 */
import { mockPurchaseOrders } from './purchaseOrders'

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
