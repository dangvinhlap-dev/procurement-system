import { apiFetch } from './client'

/** Một phiếu trong trình tự hủy dây chuyền – khớp CancelChainItemDto. */
export interface CancelChainItem {
  /** Thứ tự hủy (1 = hủy trước – phiếu cuối của chuỗi). */
  order: number
  docType: string
  docTypeName: string
  id: string
  no: string
  /** Trạng thái hiện tại (chữ tiếng Việt). */
  status: string
  note: string | null
}

/** Trình tự hủy dây chuyền – khớp CancelChainDto. */
export interface CancelChain {
  items: CancelChainItem[]
  /** Ảnh hưởng ngoài chuỗi (ví dụ PR khác của PO gộp trở về còn lại chưa đặt). */
  warnings: string[]
  /** Lý do không hủy được (thiếu quyền…); null = hủy được. */
  blockedReason: string | null
}

/**
 * Xem trước trình tự hủy dây chuyền (lát Z4b, design.md mục 20.2): phiếu cuối của chuỗi trước, phiếu được chọn cuối cùng.
 * @param docType Mã loại phiếu (PR, PO, XNH).
 * @param id Id phiếu.
 */
export function previewCancelChain(docType: string, id: string) {
  return apiFetch<CancelChain>(`/api/cancel-chain/${docType}/${id}`)
}

/**
 * Hủy cả chuỗi trong một giao dịch (một phiếu lỗi thì không hủy phiếu nào).
 * @param docType Mã loại phiếu.
 * @param id Id phiếu.
 * @param reason Lý do hủy (bắt buộc) – ghi vào mọi phiếu trong chuỗi.
 */
export function runCancelChain(docType: string, id: string, reason: string) {
  return apiFetch<CancelChain>(`/api/cancel-chain/${docType}/${id}`, { method: 'POST', body: { reason } })
}
