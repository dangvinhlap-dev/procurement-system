import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Kiểu dữ liệu và hàm gọi API mua hàng (khớp Proc.Application.Purchasing): quy trình duyệt, PR, PO, theo dõi giao hàng.
 */

const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/** Trạng thái PR/PO. */
export type PurchaseStatus = 'Draft' | 'Submitted' | 'Approved' | 'Closed' | 'Cancelled'

/** Nhãn trạng thái PR/PO. */
export const purchaseStatusLabels: Record<PurchaseStatus, string> = {
  Draft: 'Nháp',
  Submitted: 'Chờ duyệt',
  Approved: 'Đã duyệt',
  Closed: 'Đã nhận đủ',
  Cancelled: 'Đã hủy',
}

/** Tông màu trạng thái (ui-guide.md): xanh hoàn tất, cam chờ, xám trung tính. */
export const purchaseStatusTone: Record<PurchaseStatus, 'success' | 'warning' | 'error' | 'neutral'> = {
  Draft: 'neutral',
  Submitted: 'warning',
  Approved: 'success',
  Closed: 'success',
  Cancelled: 'neutral',
}

/** Mức ưu tiên PR. */
export type PurchasePriority = 'Low' | 'Normal' | 'Urgent'

/** Nhãn mức ưu tiên. */
export const priorityLabels: Record<PurchasePriority, string> = { Low: 'Thấp', Normal: 'Thường', Urgent: 'Gấp' }

/** Một bước trong lịch sử duyệt. */
export interface ApprovalStep {
  round: number
  level: number
  roleName: string | null
  status: 'Pending' | 'Approved' | 'Rejected' | 'Skipped'
  actedByName: string | null
  actedAt: string | null
  comment: string | null
}

/** Nhãn kết quả bước duyệt. */
export const stepStatusLabels: Record<ApprovalStep['status'], string> = {
  Pending: 'Chờ duyệt',
  Approved: 'Đã duyệt',
  Rejected: 'Từ chối',
  Skipped: 'Bỏ qua',
}

/** Thao tác được phép trên phiếu. */
export interface PurchaseActions {
  canEdit: boolean
  canSubmit: boolean
  canApprove: boolean
  canCancel: boolean
  readOnlyReason: string | null
}

/** Một dòng danh sách PR/PO. */
export interface PurchaseListItem {
  id: string
  no: string
  date: string
  status: PurchaseStatus
  description: string | null
  purpose: string | null
  neededDate: string | null
  amount: number
  lineCount: number
  createdByName: string | null
  progress: number
  priority: PurchasePriority | null
}

/** Kết quả tìm PR/PO. */
export interface PurchaseListResult {
  items: PurchaseListItem[]
  truncated: boolean
  limit: number
}

/** Dòng PR. */
export interface PurchaseRequestLine {
  id: string
  lineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  qty: number
  uomFactor: number
  baseQty: number
  baseUomCode: string
  estPrice: number
  amount: number
  suggestedSupplierId: string | null
  suggestedSupplierCode: string | null
  neededDate: string | null
  note: string | null
  orderedQty: number
  onHandQty: number
}

/** Chi tiết PR. */
export interface PurchaseRequest {
  id: string
  no: string
  date: string
  status: PurchaseStatus
  department: string | null
  requesterName: string | null
  purpose: string | null
  neededDate: string | null
  warehouseId: string | null
  priority: PurchasePriority
  note: string | null
  totalAmount: number
  rejectReason: string | null
  createdAt: string
  createdByName: string | null
  approvedAt: string | null
  lines: PurchaseRequestLine[]
  approvals: ApprovalStep[]
  actions: PurchaseActions
}

/** Nội dung PR gửi lên (đơn giá tham khảo do server lấy). */
export interface SavePurchaseRequestInput {
  date: string
  department: string | null
  purpose: string | null
  neededDate: string | null
  warehouseId: string | null
  priority: PurchasePriority
  note: string | null
  lines: { id: string | null; itemId: string; uomId: string; qty: number; suggestedSupplierId: string | null; neededDate: string | null; note: string | null }[]
}

/** Dòng PO. */
export interface PurchaseOrderLine {
  id: string
  lineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  qty: number
  uomFactor: number
  baseQty: number
  unitPrice: number
  discountPct: number
  grossAmount: number
  discountAmount: number
  amount: number
  vatCode: string | null
  vatAmount: number
  deliveryDate: string | null
  note: string | null
  sourcePrLineId: string | null
  sourcePrNo: string | null
  receivedQty: number
  remainingQty: number
}

/** Chi tiết PO. */
export interface PurchaseOrder {
  id: string
  no: string
  date: string
  status: PurchaseStatus
  supplierId: string
  supplierCode: string
  supplierName: string
  warehouseId: string
  warehouseCode: string | null
  expectedDate: string | null
  paymentTerms: string | null
  deliveryTerms: string | null
  deliveryAddress: string | null
  currencyCode: string
  receiveTolerancePct: number
  note: string | null
  grossAmount: number
  totalDiscount: number
  totalAmount: number
  totalVat: number
  rejectReason: string | null
  createdAt: string
  createdByName: string | null
  approvedAt: string | null
  lines: PurchaseOrderLine[]
  approvals: ApprovalStep[]
  actions: PurchaseActions
}

/** Nội dung PO gửi lên. */
export interface SavePurchaseOrderInput {
  date: string
  supplierId: string
  warehouseId: string
  expectedDate: string | null
  paymentTerms: string | null
  deliveryTerms: string | null
  deliveryAddress: string | null
  receiveTolerancePct: number
  note: string | null
  lines: {
    id: string | null
    itemId: string
    uomId: string
    qty: number
    unitPrice: number
    discountPct: number
    vatCode: string | null
    deliveryDate: string | null
    note: string | null
    sourcePrLineId: string | null
  }[]
}

/** Dòng PR đã duyệt còn chưa đặt hàng. */
export interface OpenRequestLine {
  prLineId: string
  requestId: string
  requestNo: string
  neededDate: string | null
  department: string | null
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  remainingQty: number
  estPrice: number
  vatCode: string | null
  suggestedSupplierId: string | null
}

/** Dòng PO còn chờ giao. */
export interface DeliveryTracking {
  orderId: string
  orderNo: string
  orderDate: string
  supplierCode: string
  supplierName: string
  lineId: string
  itemCode: string
  itemName: string
  uomCode: string
  qty: number
  receivedQty: number
  remainingQty: number
  dueDate: string | null
  overdue: boolean
}

/** Phiếu chờ tôi duyệt. */
export interface PendingApproval {
  docType: 'PR' | 'PO'
  documentId: string
  no: string
  date: string
  description: string | null
  amount: number
  level: number
  createdByName: string | null
}

/** Một cấp duyệt. */
export interface ApprovalRule {
  id: string
  docType: 'PR' | 'PO'
  level: number
  minAmount: number
  roleId: string
  roleCode: string
  roleName: string
}

/** Nội dung cấp duyệt. */
export interface SaveApprovalRuleInput {
  docType: string
  level: number
  minAmount: number
  roleId: string
}

/** Loại phiếu mua hàng: đường dẫn API. */
export type PurchaseKind = 'purchase-requests' | 'purchase-orders'

/**
 * Danh sách PR hoặc PO trong khoảng ngày.
 * @param kind Loại phiếu.
 * @param filter Khoảng ngày yyyy-MM-dd, trạng thái.
 */
export function usePurchaseDocuments(kind: PurchaseKind, filter: { from: string; to: string; status?: PurchaseStatus }) {
  const companyId = useCompanyId()
  const params = new URLSearchParams({ from: filter.from, to: filter.to })
  if (filter.status) params.set('status', filter.status)
  return useQuery({
    queryKey: ['purchasing', companyId, kind, params.toString()],
    queryFn: () => apiFetch<PurchaseListResult>(`/api/${kind}?${params.toString()}`),
    placeholderData: keepPreviousData,
  })
}

/** Chi tiết PR. */
export function usePurchaseRequest(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['purchasing', companyId, 'purchase-requests', id],
    queryFn: () => apiFetch<PurchaseRequest>(`/api/purchase-requests/${id}`),
    enabled: id !== null,
  })
}

/** Chi tiết PO. */
export function usePurchaseOrder(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['purchasing', companyId, 'purchase-orders', id],
    queryFn: () => apiFetch<PurchaseOrder>(`/api/purchase-orders/${id}`),
    enabled: id !== null,
  })
}

/**
 * Lập, sửa, gửi duyệt, duyệt, từ chối, hủy PR hoặc PO. Xong thì tải lại mọi dữ liệu mua hàng (danh sách, chi tiết, việc cần làm).
 * @param kind Loại phiếu.
 */
export function usePurchaseMutations<TDoc extends { id: string; no: string }, TInput>(kind: PurchaseKind) {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['purchasing'] })
  const useAction = (name: string) =>
    useMutation({
      mutationFn: ({ id, comment }: { id: string; comment?: string | null }) =>
        apiFetch<TDoc>(`/api/${kind}/${id}/${name}`, { method: 'POST', body: name === 'approve' || name === 'reject' ? { comment: comment ?? null } : undefined }),
      onSuccess,
    })
  return {
    create: useMutation({ mutationFn: (input: TInput) => apiFetch<TDoc>(`/api/${kind}`, { method: 'POST', body: input }), onSuccess }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: TInput }) => apiFetch<TDoc>(`/api/${kind}/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    submit: useAction('submit'),
    approve: useAction('approve'),
    reject: useAction('reject'),
    cancel: useAction('cancel'),
  }
}

/** Dòng PR đã duyệt còn chưa đặt hàng (lập PO). */
export function useOpenRequestLines(enabled: boolean) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['purchasing', companyId, 'open-lines'],
    queryFn: () => apiFetch<OpenRequestLine[]>('/api/purchase-requests/open-lines'),
    enabled,
  })
}

/** PO đã duyệt còn nhận hàng được (phiếu nhập theo PO). */
export function useReceivableOrders(enabled: boolean) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['purchasing', companyId, 'receivable'],
    queryFn: () => apiFetch<PurchaseListResult>('/api/purchase-orders/receivable'),
    enabled,
  })
}

/** Dòng PO còn chờ giao. */
export function useDeliveryTracking() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['purchasing', companyId, 'tracking'], queryFn: () => apiFetch<DeliveryTracking[]>('/api/purchase-orders/tracking') })
}

/** Phiếu chờ tôi duyệt. */
export function usePendingApprovals() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['purchasing', companyId, 'pending'], queryFn: () => apiFetch<PendingApproval[]>('/api/approvals/pending') })
}

/** Quy trình duyệt của công ty. */
export function useApprovalRules() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['purchasing', companyId, 'approval-rules'], queryFn: () => apiFetch<ApprovalRule[]>('/api/approval-rules') })
}

/** Thêm, sửa, xóa cấp duyệt. */
export function useApprovalRuleMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['purchasing'] })
  return {
    create: useMutation({ mutationFn: (input: SaveApprovalRuleInput) => apiFetch<ApprovalRule>('/api/approval-rules', { method: 'POST', body: input }), onSuccess }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveApprovalRuleInput }) => apiFetch<ApprovalRule>(`/api/approval-rules/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    remove: useMutation({ mutationFn: (id: string) => apiFetch<void>(`/api/approval-rules/${id}`, { method: 'DELETE' }), onSuccess }),
  }
}
