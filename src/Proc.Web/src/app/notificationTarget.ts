import { screens } from './screens'

/**
 * Đường dẫn mở một thông báo (bấm trong danh sách Thông báo, hoặc nút "Mở trên phần mềm" trong email – lát O2b): phiếu (PR, PO, ICR…) mở thẳng
 * trang phiếu; kỳ kho (PER) mở màn hình Kỳ kho vì kỳ không có trang riêng. Mã màn hình không còn (đổi menu) thì mở trang chủ.
 * @param screenCode Mã màn hình của thông báo.
 * @param targetId Phiếu cần mở; null nếu chỉ mở màn hình.
 * @returns Đường dẫn trong ứng dụng.
 */
export function notificationTargetPath(screenCode: string, targetId: string | null): string {
  // Phiếu vốn mở trong ngăn kéo danh sách (yêu cầu duyệt NCC, NCC độc quyền, vi phạm KSNB, phụ lục hợp đồng) mở trang đầy đủ của nó
  // (TC-3) – xem đủ nội dung và duyệt ngay trên trang. Mã thông báo NCC/CTA không trùng mã màn hình nên tra riêng.
  const drawerPaths: Record<string, string> = {
    NCC: '/gia-ncc/duyet-ncc', NDQ: '/gia-ncc/ncc-doc-quyen', VIOL: '/bao-cao/so-vi-pham', CTA: '/mua-hang/hop-dong-khung/phu-luc',
  }
  const drawerPath = drawerPaths[screenCode]
  if (drawerPath) return targetId ? `${drawerPath}/${targetId}` : screenCode === 'CTA' ? '/mua-hang/hop-dong-khung' : drawerPath
  const screen = screens.find((s) => s.code === screenCode)
  if (!screen) return '/'
  return targetId && screenCode !== 'PER' ? `${screen.path}/${targetId}` : screen.path
}
