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
