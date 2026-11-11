import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/** Công ty đang làm việc – ủy quyền theo từng công ty nên khóa bộ nhớ đệm có công ty. */
const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/** Trạng thái ủy quyền – khớp ApprovalDelegationDto.Status. */
export type DelegationStatus = 'Upcoming' | 'Active' | 'Expired' | 'Revoked'

/** Nhãn trạng thái ủy quyền. */
export const delegationStatusLabels: Record<DelegationStatus, string> = {
  Upcoming: 'Chưa hiệu lực',
  Active: 'Đang hiệu lực',
  Expired: 'Hết hạn',
  Revoked: 'Đã thu hồi',
}

/**
 * Ủy quyền duyệt có thời hạn (lát O2, design.md mục 19.3) – khớp Proc.Application.Purchasing.ApprovalDelegationDto. Trong thời hạn, người được
 * ủy quyền duyệt/từ chối thay người ủy quyền ở những cấp người ủy quyền đang giữ.
 */
export interface ApprovalDelegation {
  id: string
  delegatorUserId: string
  delegatorName: string
  delegateUserId: string
  delegateName: string
  /** Mã loại phiếu; null = mọi loại. */
  docType: string | null
  docTypeName: string
  validFrom: string
  validTo: string
  reason: string
  note: string | null
  status: DelegationStatus
  revokedAt: string | null
  revokedByName: string | null
  createdAt: string
  createdByName: string | null
  /** Người đang xem thu hồi được. */
  canRevoke: boolean
}

/** Lập ủy quyền; delegatorUserId null = chính tôi ủy quyền. */
export interface ApprovalDelegationInput {
  delegatorUserId: string | null
  delegateUserId: string
  docType: string | null
  validFrom: string
  validTo: string
  reason: string
  note: string | null
}

/** Danh sách ủy quyền: của tôi (lập hoặc nhận), hoặc toàn công ty khi có quyền quản lý (máy chủ tự giới hạn). */
export function useApprovalDelegations() {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['approval-delegations', companyId],
    queryFn: () => apiFetch<ApprovalDelegation[]>('/api/approval-delegations'),
  })
}

/** Người dùng có quyền duyệt tại công ty đang làm việc – chọn người ủy quyền / người được ủy quyền. */
export function useDelegationUserOptions(enabled = true) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['approval-delegations', companyId, 'users'],
    queryFn: () => apiFetch<{ id: string; userName: string; fullName: string }[]>('/api/approval-delegations/users'),
    enabled,
  })
}

/** Loại phiếu chọn được khi ủy quyền. */
export function useDelegationDocTypes(enabled = true) {
  return useQuery({
    queryKey: ['approval-delegations', 'doc-types'],
    queryFn: () => apiFetch<{ code: string; name: string }[]>('/api/approval-delegations/doc-types'),
    enabled,
    staleTime: Infinity,
  })
}

/** Lập, thu hồi ủy quyền. Xong thì tải lại danh sách và Việc cần làm (người được ủy quyền có thể thêm/bớt phiếu chờ duyệt). */
export function useApprovalDelegationMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['approval-delegations'] })
    void queryClient.invalidateQueries({ queryKey: ['purchasing'] })
  }
  return {
    create: useMutation({
      mutationFn: (input: ApprovalDelegationInput) =>
        apiFetch<ApprovalDelegation>('/api/approval-delegations', { method: 'POST', body: input }),
      onSuccess,
    }),
    revoke: useMutation({
      mutationFn: (id: string) => apiFetch<ApprovalDelegation>(`/api/approval-delegations/${id}/revoke`, { method: 'POST' }),
      onSuccess,
    }),
  }
}
