import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { ApprovalStep } from './purchasing'

/** Trạng thái phiếu NCC độc quyền – khớp enum Proc.Domain.MasterData.SoleSourceStatus. */
export type SoleSourceStatus = 'Draft' | 'Submitted' | 'Approved' | 'Ended'

/** Một dòng danh sách phiếu NCC độc quyền (design.md mục 18.9) – khớp SoleSourceListItemDto. */
export interface SoleSourceListItem {
  id: string
  no: string
  supplierCode: string
  supplierName: string
  itemCode: string
  itemName: string
  validFrom: string
  reviewDate: string
  status: SoleSourceStatus
  /** Có tác dụng hôm nay (đã duyệt, trong khoảng hiệu lực – hạn đánh giá lại). */
  isEffective: boolean
  /** Đã duyệt nhưng qua hạn đánh giá lại. */
  isOverdue: boolean
  /** Số ngày còn tới hạn đánh giá lại (chỉ phiếu đã duyệt). */
  daysToReview: number | null
  createdByName: string | null
}

/** Nút được bấm trên phiếu. */
export interface SoleSourceActions {
  canEdit: boolean
  canApprove: boolean
  canRecall: boolean
  canEnd: boolean
  canRenew: boolean
}

/** Chi tiết phiếu NCC độc quyền – khớp SoleSourceDto. */
export interface SoleSource {
  id: string
  no: string
  companyCode: string
  supplierId: string
  supplierCode: string
  supplierName: string
  itemId: string
  itemCode: string
  itemName: string
  reason: string
  validFrom: string
  reviewDate: string
  status: SoleSourceStatus
  isEffective: boolean
  isOverdue: boolean
  rejectReason: string | null
  approvedAt: string | null
  endedOn: string | null
  endReason: string | null
  renewedFromId: string | null
  renewedFromNo: string | null
  renewedById: string | null
  renewedByNo: string | null
  createdAt: string
  createdByName: string | null
  approvals: ApprovalStep[]
  actions: SoleSourceActions
}

/** Nội dung lập/sửa phiếu. */
export interface SoleSourceInput {
  supplierId: string
  itemId: string
  reason: string
  validFrom: string
  reviewDate: string
}

/** Hạn đánh giá lại tối đa (tháng) tính từ ngày hiệu lực – khớp SoleSourceApproval.MaxReviewMonths. */
export const soleSourceMaxReviewMonths = 12

/** Nhãn trạng thái hiển thị, tính cả "Quá hạn đánh giá lại" và "Chưa tới ngày hiệu lực" của phiếu đã duyệt. */
export function soleSourceStateLabel(r: Pick<SoleSourceListItem, 'status' | 'isEffective' | 'isOverdue'>): string {
  if (r.status === 'Draft') return 'Nháp'
  if (r.status === 'Submitted') return 'Chờ duyệt'
  if (r.status === 'Ended') return 'Đã kết thúc'
  if (r.isOverdue) return 'Quá hạn đánh giá lại'
  return r.isEffective ? 'Đang hiệu lực' : 'Chưa tới ngày hiệu lực'
}

/** Id công ty đang làm việc – đưa vào khóa truy vấn để đổi công ty thì tải lại dữ liệu. */
function useCompanyId(): string {
  return useAuth().workingCompany?.id ?? ''
}

/** Danh sách phiếu của công ty đang làm việc. */
export function useSoleSources() {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['sole-sources', companyId],
    queryFn: () => apiFetch<SoleSourceListItem[]>('/api/sole-sources'),
  })
}

/**
 * Chi tiết phiếu.
 * @param id Id phiếu; null thì không tải (đang lập mới).
 */
export function useSoleSource(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['sole-sources', companyId, id],
    queryFn: () => apiFetch<SoleSource>(`/api/sole-sources/${id}`),
    enabled: id !== null,
  })
}

/** Lập, sửa, gửi, rút, duyệt, từ chối, kết thúc, đánh giá lại. Xong thì tải lại danh sách, Việc cần làm và trang chủ. */
export function useSoleSourceMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['sole-sources'] })
    void queryClient.invalidateQueries({ queryKey: ['purchasing'] })
    void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
  }
  const post = (action: string) => (id: string) => apiFetch<SoleSource>(`/api/sole-sources/${id}/${action}`, { method: 'POST' })
  const decide = (action: 'approve' | 'reject') => ({ id, comment }: { id: string; comment?: string }) =>
    apiFetch<SoleSource>(`/api/sole-sources/${id}/${action}`, { method: 'POST', body: { comment: comment ?? null } })
  return {
    create: useMutation({
      mutationFn: (input: SoleSourceInput) => apiFetch<SoleSource>('/api/sole-sources', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SoleSourceInput }) =>
        apiFetch<SoleSource>(`/api/sole-sources/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    submit: useMutation({ mutationFn: post('submit'), onSuccess }),
    recall: useMutation({ mutationFn: post('recall'), onSuccess }),
    renew: useMutation({ mutationFn: post('renew'), onSuccess }),
    approve: useMutation({ mutationFn: decide('approve'), onSuccess }),
    reject: useMutation({ mutationFn: decide('reject'), onSuccess }),
    end: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) =>
        apiFetch<SoleSource>(`/api/sole-sources/${id}/end`, { method: 'POST', body: { reason } }),
      onSuccess,
    }),
  }
}
