import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { ApprovalStep } from './purchasing'

/** Trạng thái phiếu duyệt NCC và giá tháng (design.md mục 18.25 – lát G2). */
export type MonthlyApprovalStatus = 'Draft' | 'Submitted' | 'Approved' | 'Replaced' | 'Cancelled'

/** Nhãn tiếng Việt của trạng thái. */
export const monthlyApprovalStatusLabels: Record<MonthlyApprovalStatus, string> = {
  Draft: 'Nháp',
  Submitted: 'Chờ duyệt',
  Approved: 'Đã duyệt',
  Replaced: 'Đã thay bằng điều chỉnh',
  Cancelled: 'Đã hủy',
}

/** Màu đèn theo trạng thái (luôn kèm chữ – docs/ui-style.md mục 2). */
export const monthlyApprovalStatusTone: Record<MonthlyApprovalStatus, 'success' | 'warning' | 'error' | 'neutral'> = {
  Draft: 'neutral',
  Submitted: 'warning',
  Approved: 'success',
  Replaced: 'neutral',
  Cancelled: 'error',
}

/** Một dòng danh sách phiếu. */
export interface MonthlyApprovalListItem {
  id: string
  no: string
  period: string
  categoryCode: string
  categoryName: string
  companyCodes: string
  lineCount: number
  status: MonthlyApprovalStatus
  replacesNo: string | null
  createdByName: string | null
}

/** Một dòng mặt hàng của phiếu. */
export interface MonthlyApprovalLine {
  id: string
  lineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  mainSupplierId: string
  mainSupplierCode: string
  mainSupplierName: string
  unitPrice: number
  backupSupplierId: string | null
  backupSupplierCode: string | null
  backupSupplierName: string | null
  backupUnitPrice: number | null
  quantityLimit: number | null
  note: string | null
  /** Hệ số ĐVT dòng so với ĐVT cơ sở. */
  uomFactor: number
  /** Giá chuẩn – mốc tính tiết kiệm (lát G4b). */
  standardPrice: number | null
  /** Giá trần – giá duyệt không vượt; PO vượt thì cảnh báo. */
  ceilingPrice: number | null
  /** Giá mục tiêu để đàm phán. */
  targetPrice: number | null
}

/** Công ty chọn được để áp dụng phiếu. */
export interface MonthlyApprovalCompany {
  id: string
  code: string
  name: string
}

/** Thao tác được phép (server tính). */
export interface MonthlyApprovalActions {
  canEdit: boolean
  canApprove: boolean
  canRecall: boolean
  canAdjust: boolean
  canCancel: boolean
  canCopyNextMonth: boolean
  readOnlyReason: string | null
}

/** Chi tiết phiếu. */
export interface MonthlyApproval {
  id: string
  no: string
  companyCode: string
  period: string
  itemCategoryId: string
  categoryCode: string
  categoryName: string
  companies: MonthlyApprovalCompany[]
  note: string | null
  status: MonthlyApprovalStatus
  rejectReason: string | null
  approvedAt: string | null
  cancelledOn: string | null
  cancelReason: string | null
  replacesId: string | null
  replacesNo: string | null
  replacedById: string | null
  replacedByNo: string | null
  createdAt: string
  createdByName: string | null
  lines: MonthlyApprovalLine[]
  approvals: ApprovalStep[]
  actions: MonthlyApprovalActions
}

/** Dữ liệu lưu phiếu. */
export interface SaveMonthlyApprovalInput {
  period: string
  itemCategoryId: string
  companyIds: string[]
  note: string | null
  lines: {
    id: string | null
    itemId: string
    uomId: string
    mainSupplierId: string
    unitPrice: number
    backupSupplierId: string | null
    backupUnitPrice: number | null
    quantityLimit: number | null
    note: string | null
    standardPrice: number | null
    ceilingPrice: number | null
    targetPrice: number | null
  }[]
}

const BASE = '/api/monthly-supplier-approvals'

function useCompanyId(): string {
  return useAuth().workingCompany?.id ?? ''
}

/** Danh sách phiếu (tổng công ty: mọi phiếu; công ty thành viên: phiếu đã duyệt áp cho mình). */
export function useMonthlyApprovals() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['monthly-approvals', companyId], queryFn: () => apiFetch<MonthlyApprovalListItem[]>(BASE) })
}

/**
 * Chi tiết một phiếu.
 * @param id Id phiếu; null = không tải (đang lập mới).
 */
export function useMonthlyApproval(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['monthly-approvals', companyId, id],
    queryFn: () => apiFetch<MonthlyApproval>(`${BASE}/${id}`),
    enabled: id !== null,
  })
}

/** Công ty đang hoạt động để chọn công ty áp dụng. */
export function useMonthlyApprovalCompanies() {
  return useQuery({ queryKey: ['monthly-approvals', 'companies'], queryFn: () => apiFetch<MonthlyApprovalCompany[]>(`${BASE}/companies`) })
}

/** Lệnh trên phiếu: lập, sửa, gửi, rút, duyệt, từ chối, hủy, điều chỉnh, chép sang tháng sau. */
export function useMonthlyApprovalMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['monthly-approvals'] })
    void queryClient.invalidateQueries({ queryKey: ['purchasing'] })
    void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
  }
  const post = (action: string) => (id: string) => apiFetch<MonthlyApproval>(`${BASE}/${id}/${action}`, { method: 'POST' })
  const decide = (action: 'approve' | 'reject') => ({ id, comment }: { id: string; comment?: string }) =>
    apiFetch<MonthlyApproval>(`${BASE}/${id}/${action}`, { method: 'POST', body: { comment: comment ?? null } })
  return {
    create: useMutation({
      mutationFn: (input: SaveMonthlyApprovalInput) => apiFetch<MonthlyApproval>(BASE, { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveMonthlyApprovalInput }) =>
        apiFetch<MonthlyApproval>(`${BASE}/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    submit: useMutation({ mutationFn: ({ id }: { id: string }) => post('submit')(id), onSuccess }),
    recall: useMutation({ mutationFn: post('recall'), onSuccess }),
    adjust: useMutation({ mutationFn: post('adjust'), onSuccess }),
    copyNextMonth: useMutation({ mutationFn: post('copy-next-month'), onSuccess }),
    approve: useMutation({ mutationFn: decide('approve'), onSuccess }),
    reject: useMutation({ mutationFn: decide('reject'), onSuccess }),
    cancel: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) =>
        apiFetch<MonthlyApproval>(`${BASE}/${id}/cancel`, { method: 'POST', body: { reason } }),
      onSuccess,
    }),
  }
}
