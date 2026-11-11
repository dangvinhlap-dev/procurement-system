import { useMutation, useQueryClient } from '@tanstack/react-query'
import { approvalApiBase } from '../app/approvalRoutes'
import { apiFetch } from './client'
import type { ApprovalDocType } from './purchasing'

/** Quyết định trên trang duyệt gọn: duyệt hoặc từ chối. */
export type ApprovalDecision = 'approve' | 'reject'

/**
 * Duyệt / từ chối một phiếu bất kỳ loại từ trang duyệt gọn trên điện thoại (lát O2d) – gọi đúng API duyệt của loại phiếu (bảng
 * {@link approvalApiBase}). Xong thì tải lại mọi dữ liệu: phiếu duyệt xong có thể đổi danh sách, số đếm trang chủ, thông báo của nhiều phân hệ.
 */
export function useApprovalDecision() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ docType, id, decision, comment }: { docType: ApprovalDocType; id: string; decision: ApprovalDecision; comment: string | null }) => {
      const base = approvalApiBase[docType]
      if (!base) throw new Error(`Loại phiếu ${docType} chưa duyệt được trên trang này.`)
      return apiFetch<unknown>(`${base}/${id}/${decision}`, { method: 'POST', body: { comment } })
    },
    onSuccess: () => void queryClient.invalidateQueries(),
  })
}
