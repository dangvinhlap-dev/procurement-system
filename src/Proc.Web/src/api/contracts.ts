import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { ApprovalStep } from './purchasing'

/*
 * Kiểu dữ liệu và hàm gọi API hợp đồng nguyên tắc (khớp Proc.Application.Purchasing.Contracts – lát B4a, design.md mục 18.7).
 */

/** Trạng thái hợp đồng nguyên tắc. */
export type ContractStatus = 'Draft' | 'Submitted' | 'Active' | 'Terminated' | 'Cancelled'

/** Nhãn trạng thái hợp đồng. */
export const contractStatusLabels: Record<ContractStatus, string> = {
  Draft: 'Nháp',
  Submitted: 'Chờ duyệt',
  Active: 'Hiệu lực',
  Terminated: 'Đã chấm dứt',
  Cancelled: 'Đã hủy',
}

/** Tông màu trạng thái hợp đồng. */
export const contractStatusTone: Record<ContractStatus, 'success' | 'warning' | 'neutral'> = {
  Draft: 'neutral',
  Submitted: 'warning',
  Active: 'success',
  Terminated: 'neutral',
  Cancelled: 'neutral',
}

/** Một dòng danh sách hợp đồng. */
export interface ContractListItem {
  id: string
  no: string
  contractNo: string | null
  title: string
  supplierCode: string
  supplierName: string
  validFrom: string
  validTo: string
  expectedAmount: number
  status: ContractStatus
  /** Đang hiệu lực và còn ≤ 30 ngày tới ngày hết hạn. */
  expiringSoon: boolean
  createdByName: string | null
}

/** Sản lượng dự kiến một mặt hàng; giá theo bảng giá gắn với hợp đồng. */
export interface ContractLine {
  id: string
  lineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  expectedQty: number
  minCommitQty: number | null
  unitPrice: number
  discountPct: number
  expectedAmount: number
  note: string | null
}

/** Chi tiết hợp đồng nguyên tắc. */
export interface Contract {
  id: string
  no: string
  companyCode: string
  supplierId: string
  supplierCode: string
  supplierName: string
  priceListId: string
  priceListNo: string
  priceListTitle: string
  priceListStatus: string
  contractNo: string | null
  title: string
  signDate: string | null
  validFrom: string
  validTo: string
  priceAdjustmentTerms: string | null
  paymentTerms: string | null
  creditLimit: number | null
  deliveryTerms: string | null
  discountTerms: string | null
  sponsorshipTerms: string | null
  penaltyTerms: string | null
  otherTerms: string | null
  note: string | null
  status: ContractStatus
  expectedAmount: number
  departmentIds: string[]
  departmentNames: string[]
  lines: ContractLine[]
  createdAt: string
  createdByName: string | null
  activatedAt: string | null
  terminatedOn: string | null
  terminationReason: string | null
  rejectReason: string | null
  daysToExpiry: number
  expiringSoon: boolean
  approvals: ApprovalStep[]
  actions: {
    canEdit: boolean
    canSubmit: boolean
    canApprove: boolean
    canRecall: boolean
    canTerminate: boolean
    canCancel: boolean
    readOnlyReason: string | null
  }
}

/** Nội dung hợp đồng gửi lên. */
export interface SaveContractInput {
  supplierId: string
  priceListId: string
  contractNo: string | null
  title: string
  signDate: string | null
  validFrom: string
  validTo: string
  priceAdjustmentTerms: string | null
  paymentTerms: string | null
  creditLimit: number | null
  deliveryTerms: string | null
  discountTerms: string | null
  sponsorshipTerms: string | null
  penaltyTerms: string | null
  otherTerms: string | null
  note: string | null
  departmentIds: string[]
  lines: { id: string | null; itemId: string; uomId: string; expectedQty: number; minCommitQty: number | null; note: string | null }[]
}

const useCompanyId = () => useAuth().workingCompany?.id

/** Danh sách hợp đồng nguyên tắc của công ty đang làm việc. */
export function useContracts() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['contracts', companyId], queryFn: () => apiFetch<ContractListItem[]>('/api/contracts') })
}

/** Chi tiết hợp đồng; null thì không tải (đang lập mới). */
export function useContract(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['contracts', companyId, id], queryFn: () => apiFetch<Contract>(`/api/contracts/${id}`), enabled: !!id })
}

/** Lập, sửa, gửi duyệt, duyệt, từ chối, rút, chấm dứt, hủy hợp đồng. */
export function useContractMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['contracts'] })
  return {
    create: useMutation({ mutationFn: (input: SaveContractInput) => apiFetch<Contract>('/api/contracts', { method: 'POST', body: input }), onSuccess }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveContractInput }) => apiFetch<Contract>(`/api/contracts/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    submit: useMutation({ mutationFn: (id: string) => apiFetch<Contract>(`/api/contracts/${id}/submit`, { method: 'POST' }), onSuccess }),
    recall: useMutation({ mutationFn: (id: string) => apiFetch<Contract>(`/api/contracts/${id}/recall`, { method: 'POST' }), onSuccess }),
    cancel: useMutation({ mutationFn: (id: string) => apiFetch<Contract>(`/api/contracts/${id}/cancel`, { method: 'POST' }), onSuccess }),
    approve: useMutation({
      mutationFn: ({ id, comment }: { id: string; comment?: string | null }) =>
        apiFetch<Contract>(`/api/contracts/${id}/approve`, { method: 'POST', body: { comment: comment ?? null } }),
      onSuccess,
    }),
    reject: useMutation({
      mutationFn: ({ id, comment }: { id: string; comment?: string | null }) =>
        apiFetch<Contract>(`/api/contracts/${id}/reject`, { method: 'POST', body: { comment: comment ?? null } }),
      onSuccess,
    }),
    terminate: useMutation({
      mutationFn: ({ id, date, reason }: { id: string; date: string; reason: string }) =>
        apiFetch<Contract>(`/api/contracts/${id}/terminate`, { method: 'POST', body: { date, reason } }),
      onSuccess,
    }),
  }
}
