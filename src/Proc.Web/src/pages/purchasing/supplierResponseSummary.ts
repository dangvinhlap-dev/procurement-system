import { supplierOrderResponseLabels, type SupplierOrderResponseInfo } from '../../api/purchasing'
import { formatDate, formatDateTime } from '../../utils/format'

/**
 * Một dòng tóm tắt phản hồi NCC cho ô chỉ đọc đầu PO, ví dụ "NCC đã xác nhận – giao 20/11/2026 · ban@ncc.vn 12/11/2026 10:30".
 * @param info Phản hồi NCC của PO.
 * @returns Chuỗi hiển thị.
 */
export function supplierResponseSummary(info: SupplierOrderResponseInfo): string {
  const current = info.responses.find((r) => !r.reopenedAt)
  const parts = [supplierOrderResponseLabels[info.status]]
  if (current?.promisedDate) parts[0] += ` – giao ${formatDate(current.promisedDate)}`
  if (current) parts.push(`${current.respondedByEmail} ${formatDateTime(current.respondedAt)}`, current.note ?? '')
  if (info.status !== 'NotSent' && info.activePortalUsers === 0) parts.push('NCC chưa có tài khoản Portal đang dùng – báo NCC theo cách cũ')
  return parts.filter(Boolean).join(' · ')
}
