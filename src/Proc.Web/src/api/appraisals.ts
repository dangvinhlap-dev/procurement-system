import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Ý kiến thẩm định của thành viên chuyên môn (khớp Proc.Application.Documents – AppraisalOpinions.cs, design.md mục 18.25 – lát H1).
 * Không phải cấp duyệt: không chặn gửi/duyệt, người duyệt xem trước khi quyết.
 */

/** Loại đối tượng được thẩm định (khớp AppraisalOwnerTypes). */
export type AppraisalOwnerType = 'MonthlySupplierApproval' | 'Rfq' | 'PurchaseContract' | 'Supplier'

/** Khía cạnh thẩm định. */
export type AppraisalAspect = 'Quality' | 'Legal' | 'Other'

/** Kết luận thẩm định. */
export type AppraisalResult = 'Pass' | 'Fail'

/** Nhãn khía cạnh. */
export const appraisalAspectLabels: Record<AppraisalAspect, string> = { Quality: 'Chất lượng', Legal: 'Pháp lý', Other: 'Khác' }

/** Nhãn kết luận. */
export const appraisalResultLabels: Record<AppraisalResult, string> = { Pass: 'Đạt', Fail: 'Không đạt' }

/** Một ý kiến thẩm định. */
export interface AppraisalOpinion {
  id: string
  aspect: AppraisalAspect
  result: AppraisalResult
  content: string
  createdAt: string
  authorName: string | null
  /** Vai trò của người ghi ở công ty đang làm việc – Hội đồng biết ý kiến của thẩm định chất lượng hay pháp lý. */
  authorRoles: string | null
  isWithdrawn: boolean
  withdrawReason: string | null
  withdrawnAt: string | null
  canWithdraw: boolean
}

/** Danh sách ý kiến kèm tổng hợp. */
export interface AppraisalOpinionList {
  opinions: AppraisalOpinion[]
  /** Số ý kiến Đạt còn hiệu lực (không tính ý kiến đã rút). */
  passCount: number
  /** Số ý kiến Không đạt còn hiệu lực. */
  failCount: number
  canGive: boolean
  /** Lý do không ghi được – hiện cạnh nút. */
  giveBlockedReason: string | null
}

/** Nội dung ghi ý kiến. */
export interface GiveAppraisalInput {
  ownerType: AppraisalOwnerType
  ownerId: string
  aspect: AppraisalAspect
  result: AppraisalResult
  content: string
}

const BASE = '/api/appraisal-opinions'

/** Khóa bộ nhớ đệm TanStack Query cho danh sách ý kiến thẩm định của một chứng từ (tách theo công ty để đổi công ty không lẫn dữ liệu). */
const queryKey = (companyId: string, ownerType: AppraisalOwnerType, ownerId: string) => ['appraisals', companyId, ownerType, ownerId]

/**
 * Ý kiến thẩm định của một đối tượng.
 * @param ownerType Loại đối tượng.
 * @param ownerId Id đối tượng; rỗng (chưa lưu) thì không tải.
 */
export function useAppraisals(ownerType: AppraisalOwnerType, ownerId: string | undefined) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: queryKey(companyId, ownerType, ownerId ?? ''),
    queryFn: () => apiFetch<AppraisalOpinionList>(`${BASE}?ownerType=${ownerType}&ownerId=${ownerId}`),
    enabled: !!companyId && !!ownerId,
  })
}

/**
 * Ghi, rút ý kiến thẩm định; xong thì tải lại danh sách của mọi đối tượng (ít dữ liệu, đơn giản hơn tính khóa từng chủ).
 */
export function useAppraisalMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['appraisals'] })
  return {
    give: useMutation({ mutationFn: (input: GiveAppraisalInput) => apiFetch<AppraisalOpinion>(BASE, { method: 'POST', body: input }), onSuccess }),
    withdraw: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) =>
        apiFetch<AppraisalOpinion>(`${BASE}/${id}/withdraw`, { method: 'POST', body: { reason } }),
      onSuccess,
    }),
  }
}
