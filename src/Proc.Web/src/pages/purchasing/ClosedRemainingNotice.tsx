import { Alert } from 'antd'
import type { ClosedRemaining } from '../../api/purchasing'
import { formatDateTime } from '../../utils/format'

/**
 * Dòng báo phiếu đã đóng phần còn lại: ai đóng, lúc nào, lý do (lát R6, design.md mục 18.26).
 * @param doc Phiếu PR/PO.
 */
export function ClosedRemainingNotice({ doc }: { doc: ClosedRemaining | undefined }) {
  if (!doc?.closedReason) return null
  const who = [doc.closedByName, doc.closedAt ? formatDateTime(doc.closedAt) : null].filter(Boolean).join(', ')
  return <Alert type="info" showIcon title={`Đã đóng phần còn lại${who ? ` (${who})` : ''}: ${doc.closedReason}`} />
}
