import { formatDate, formatDateTime, formatMoney, formatQuantity } from '../../utils/format'

/** Tông màu của cột trạng thái (ui-guide.md): xanh lá hoàn tất, cam chờ duyệt, đỏ quá hạn/lỗi, xám trung tính. */
export type StatusTone = 'success' | 'warning' | 'error' | 'neutral'

/** Kiểu cột, quyết định định dạng, căn lề và cách sắp xếp. */
export type GridColumnType = 'text' | 'date' | 'datetime' | 'money' | 'quantity' | 'status'

/** Khai báo một cột của DataGrid. */
export interface GridColumn<T> {
  key: string
  title: string
  /** Độ rộng ban đầu (px); người dùng kéo để đổi. */
  width: number
  type?: GridColumnType
  /** Giá trị thô của ô: dùng để sắp xếp, tìm kiếm, nhóm và xuất CSV. Ngày dạng ISO yyyy-MM-dd, ngày giờ dạng ISO 8601 UTC. */
  value: (row: T) => string | number | null
  /** Với cột trạng thái: tông màu của chấm theo dòng. */
  tone?: (row: T) => StatusTone
  /** Cho phép nhóm theo cột này. */
  groupable?: boolean
}

/**
 * Chuỗi hiển thị của một ô theo kiểu cột (ngày dd/MM/yyyy, tiền không số lẻ…).
 * Dùng chung cho hiển thị, tìm kiếm, nhãn nhóm và xuất CSV để mọi nơi thấy cùng một chuỗi.
 * @param column Cột.
 * @param row Dòng dữ liệu.
 * @returns Chuỗi đã định dạng.
 */
export function cellText<T>(column: GridColumn<T>, row: T): string {
  const value = column.value(row)
  if (value == null) return ''
  switch (column.type) {
    case 'date':
      return formatDate(String(value))
    case 'datetime':
      return formatDateTime(String(value))
    case 'money':
      return formatMoney(Number(value))
    case 'quantity':
      return formatQuantity(Number(value))
    default:
      return String(value)
  }
}

/**
 * So sánh hai giá trị ô để sắp xếp: số so theo số, chuỗi so theo thứ tự chữ cái tiếng Việt;
 * ô rỗng luôn nằm cuối.
 * @returns Số âm, 0 hoặc dương như Array.sort.
 */
export function compareCellValues(a: string | number | null, b: string | number | null): number {
  if (a == null && b == null) return 0
  if (a == null) return 1
  if (b == null) return -1
  if (typeof a === 'number' && typeof b === 'number') return a - b
  return String(a).localeCompare(String(b), 'vi', { numeric: true })
}
