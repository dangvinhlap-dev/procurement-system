import dayjs from 'dayjs'

/** Định dạng tiền VND: không số lẻ, ví dụ 1234567.8 → "1.234.568" (CLAUDE.md mục 5). */
const moneyFormat = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 })

/** Định dạng số lượng: tối đa 4 chữ số lẻ (numeric(18,4)), ví dụ 1234.5 → "1.234,5". */
const quantityFormat = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 4 })

/**
 * Hiển thị tiền VND không số lẻ.
 * @param value Số tiền; null/undefined hiển thị rỗng.
 * @returns Chuỗi kiểu "1.234.567".
 */
export function formatMoney(value: number | null | undefined): string {
  return value == null ? '' : moneyFormat.format(value)
}

/**
 * Hiển thị số lượng kiểu Việt Nam.
 * @param value Số lượng; null/undefined hiển thị rỗng.
 * @returns Chuỗi kiểu "1.234,5".
 */
export function formatQuantity(value: number | null | undefined): string {
  return value == null ? '' : quantityFormat.format(value)
}

/**
 * Hiển thị ngày dd/MM/yyyy.
 * @param value Ngày dạng ISO "yyyy-MM-dd" (ngày chứng từ không có giờ) hoặc Date.
 * @returns Chuỗi kiểu "29/10/2026"; rỗng nếu không có giá trị.
 */
export function formatDate(value: string | Date | null | undefined): string {
  return value ? dayjs(value).format('DD/MM/YYYY') : ''
}

/**
 * Hiển thị ngày giờ dd/MM/yyyy HH:mm theo giờ máy người dùng (Việt Nam – CLAUDE.md mục 5).
 * @param value Thời điểm ISO 8601 (UTC từ server).
 */
export function formatDateTime(value: string | Date | null | undefined): string {
  return value ? dayjs(value).format('DD/MM/YYYY HH:mm') : ''
}

/**
 * Thuộc tính cho InputNumber của antd để gõ và hiển thị số kiểu Việt Nam: dấu chấm phân cách nghìn, dấu phẩy thập phân
 * (CLAUDE.md mục 5). Khi đang gõ giữ nguyên chữ người dùng gõ để con trỏ không nhảy.
 * Ví dụ: 1234567.5 hiển thị "1.234.567,5"; gõ "20.333,5" hiểu là 20333.5.
 */
export const vnNumberInput = {
  formatter: (value: string | number | undefined, info: { userTyping: boolean; input: string }) => {
    if (info.userTyping) return info.input
    if (value === undefined || value === '') return ''
    const [int, dec] = String(value).split('.')
    const grouped = int.replace(/\B(?=(\d{3})+(?!\d))/g, '.')
    return dec ? `${grouped},${dec}` : grouped
  },
  parser: (display: string | undefined) => (display ?? '').replace(/\./g, '').replace(',', '.'),
} as const
