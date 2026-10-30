import type { DocumentStatus } from '../../api/inventoryDocuments'
import type { StatusTone } from '../../components/DataGrid/types'

/** Màu chấm/thẻ trạng thái chứng từ kho: nháp vàng, đã ghi sổ xanh, đã hủy xám. */
export const documentStatusTone: Record<DocumentStatus, StatusTone> = {
  Draft: 'warning',
  Posted: 'success',
  Cancelled: 'neutral',
}
