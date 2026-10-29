import { normalizeVi } from '../utils/text'
import type { Screen } from './screens'

/**
 * Đưa con trỏ vào ô Tìm kiếm của màn hình danh sách đang mở (nếu có).
 * Dùng cho nút Tìm kiếm và phím F3.
 * @returns true nếu tìm thấy ô tìm kiếm.
 */
export function focusListSearch(): boolean {
  const input = document.querySelector<HTMLInputElement>('input[data-list-search]')
  input?.focus()
  input?.select()
  return Boolean(input)
}

/**
 * Tìm màn hình cho ô lệnh: khớp đúng mã trước, rồi mã bắt đầu bằng từ gõ, rồi tên chứa từ gõ (không dấu).
 * @param text Nội dung người dùng gõ, ví dụ "po" hoặc "don mua".
 * @param screens Các màn hình được phép mở (useVisibleScreens).
 * @returns Danh sách màn hình theo thứ tự ưu tiên.
 */
export function searchScreens(text: string, screens: Screen[]): Screen[] {
  const q = normalizeVi(text)
  if (!q) return []
  const rank = (s: Screen) => {
    const code = s.code.toLowerCase()
    if (code === q) return 0
    if (code.startsWith(q)) return 1
    if (normalizeVi(`${s.navLabel} ${s.title}`).includes(q)) return 2
    return -1
  }
  return screens
    .map((s) => ({ s, r: rank(s) }))
    .filter((x) => x.r >= 0)
    .sort((a, b) => a.r - b.r)
    .map((x) => x.s)
}
