import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from './client'
import type { ApprovalStep } from './purchasing'

/** Trạng thái yêu cầu duyệt NCC. */
export type SupplierChangeStatus = 'Submitted' | 'Approved' | 'Rejected' | 'Cancelled'

/**
 * Yêu cầu duyệt NCC (design.md mục 18.6): đưa vào Đã duyệt/Danh sách đen, đổi tài khoản ngân hàng. Duyệt hai chặng: công ty liên quan
 * (đang mua của NCC) duyệt xong rồi Tổng công ty duyệt lần cuối.
 */
export interface SupplierChangeRequest {
  id: string
  no: string
  supplierId: string
  supplierCode: string
  supplierName: string
  kind: 'Status' | 'BankAccounts'
  currentSummary: string
  proposedSummary: string
  status: SupplierChangeStatus
  createdByName: string | null
  createdAt: string
  approvedAt: string | null
  rejectReason: string | null
  /** Mã công ty đang đến lượt duyệt, ví dụ "BHA, BHB" hoặc "TCT"; null khi đã kết thúc. */
  waitingFor: string | null
  approvals: ApprovalStep[]
  canApprove: boolean
  canCancel: boolean
  /** Hồ sơ đánh giá NCC mới đi kèm (lát G8) – chỉ có khi đang làm việc ở tổng công ty. */
  onboarding?: SupplierOnboardingRef | null
}

/** Tham chiếu ngắn tới hồ sơ đánh giá NCC mới (HSN). */
export interface SupplierOnboardingRef {
  id: string
  no: string
  status: string
}

/** Nhãn loại thay đổi. */
export const changeKindLabels: Record<SupplierChangeRequest['kind'], string> = {
  Status: 'Đổi trạng thái',
  BankAccounts: 'Đổi tài khoản ngân hàng',
}

/** Nhãn trạng thái yêu cầu. */
export const changeStatusLabels: Record<SupplierChangeStatus, string> = {
  Submitted: 'Chờ duyệt',
  Approved: 'Đã duyệt',
  Rejected: 'Từ chối',
  Cancelled: 'Đã rút',
}

/** Danh sách yêu cầu duyệt NCC (mọi NCC). */
export function useSupplierChangeRequests() {
  return useQuery({ queryKey: ['supplier-change-requests'], queryFn: () => apiFetch<SupplierChangeRequest[]>('/api/supplier-change-requests') })
}

/**
 * Chi tiết một yêu cầu.
 * @param id Id yêu cầu; null thì không tải.
 */
export function useSupplierChangeRequest(id: string | null) {
  return useQuery({
    queryKey: ['supplier-change-requests', id ?? ''],
    queryFn: () => apiFetch<SupplierChangeRequest>(`/api/supplier-change-requests/${id}`),
    enabled: id !== null,
  })
}

/** Duyệt, từ chối, rút yêu cầu. Xong tải lại yêu cầu, NCC, việc cần làm. */
export function useSupplierChangeActions() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['supplier-change-requests'] })
    void queryClient.invalidateQueries({ queryKey: ['suppliers'] })
    void queryClient.invalidateQueries({ queryKey: ['purchasing'] })
    void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
  }
  const act = (action: 'approve' | 'reject') => ({ id, comment }: { id: string; comment?: string }) =>
    apiFetch<SupplierChangeRequest>(`/api/supplier-change-requests/${id}/${action}`, { method: 'POST', body: { comment: comment ?? null } })
  return {
    approve: useMutation({ mutationFn: act('approve'), onSuccess }),
    reject: useMutation({ mutationFn: act('reject'), onSuccess }),
    cancel: useMutation({
      mutationFn: (id: string) => apiFetch<SupplierChangeRequest>(`/api/supplier-change-requests/${id}/cancel`, { method: 'POST' }),
      onSuccess,
    }),
  }
}
