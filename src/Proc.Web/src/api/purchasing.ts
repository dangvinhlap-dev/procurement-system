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
  /** Công ty duyệt bước này. */
  companyCode: string | null
  /** Chặng: 1 = công ty của phiếu / công ty liên quan; 2 = Tổng công ty duyệt lần cuối (design.md mục 18.6). */
  stage: number
  /**
   * Người duyệt tiếp theo (TC-4, design.md mục 18.22): họ tên những người duyệt được bước này – chỉ có ở bước đang đến lượt; rỗng khi chưa ai
   * đủ điều kiện (thiếu vai trò, chỉ có người lập hoặc người vướng xung đột lợi ích).
   */
  nextApprovers?: string[] | null
}

/** Số tên hiện tối đa – cấp "ai có quyền duyệt" có thể có rất nhiều người, ô lưới chỉ cần đủ để biết nhắc ai. */
const NEXT_APPROVERS_SHOWN = 5

/**
 * Chữ hiển thị "người duyệt tiếp theo", ví dụ "Lê B, Trần A" hoặc "A, B, C, D, E và 3 người khác".
 * @param names Danh sách họ tên từ máy chủ (đã xếp theo chữ cái).
 * @returns Chuỗi nối bằng dấu phẩy; null khi không chờ; câu báo khi chưa ai đủ điều kiện duyệt.
 */
export function nextApproversText(names: string[] | null | undefined): string | null {
  if (!names) return null
  if (names.length === 0) return 'Chưa có người đủ điều kiện duyệt'
  const shown = names.slice(0, NEXT_APPROVERS_SHOWN).join(', ')
  return names.length > NEXT_APPROVERS_SHOWN ? `${shown} và ${names.length - NEXT_APPROVERS_SHOWN} người khác` : shown
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
  /** PO: có dòng ngoài hợp đồng (null với PR). */
  outOfContract: boolean | null
  /** PO: có dòng lệch giá đã duyệt. */
  priceException: boolean | null
  /** PO: loại mua (null với PR). */
  purchaseType: PurchaseType | null
  /** PO khẩn cấp quá hạn bổ sung hồ sơ. */
  docsOverdue: boolean | null
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
  /** SL đang đặt của mặt hàng tại công ty (PO đã duyệt chưa nhận đủ, ĐVT cơ sở) – design.md mục 18.13. */
  onOrderQty: number
}

/** Chi tiết PR. */
export interface PurchaseRequest {
  id: string
  no: string
  date: string
  status: PurchaseStatus
  /** Bộ phận trong danh mục (design.md mục 18.5). */
  departmentId: string | null
  /** Tên bộ phận để hiển thị; PR cũ chưa có danh mục thì là chữ ghi trước đây. */
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
  /** Hàng mới chưa có mã (design.md mục 18.12). */
  newItems: PurchaseRequestNewItem[]
  /** PR đã duyệt còn hàng mới chưa gắn mã và người dùng lập được phiếu tìm nguồn. */
  canCreateSourcing: boolean
  /** PR đã duyệt còn hàng mới chưa gắn mã và người dùng gắn mã được. */
  canLinkItem: boolean
}

/** Hàng mới chưa có mã trên PR: không lên PO/RFQ cho tới khi gắn mã hàng. */
export interface PurchaseRequestNewItem {
  id: string
  lineNo: number
  description: string
  specification: string | null
  uomId: string
  uomCode: string
  qty: number
  estPrice: number
  amount: number
  neededDate: string | null
  note: string | null
  sourcingRequestId: string | null
  sourcingNo: string | null
  itemId: string | null
  itemCode: string | null
}

/** Nội dung PR gửi lên (đơn giá tham khảo do server lấy). */
export interface SavePurchaseRequestInput {
  date: string
  departmentId: string | null
  purpose: string | null
  neededDate: string | null
  warehouseId: string | null
  priority: PurchasePriority
  note: string | null
  lines: { id: string | null; itemId: string; uomId: string; qty: number; suggestedSupplierId: string | null; neededDate: string | null; note: string | null }[]
  newItems: {
    id: string | null
    description: string
    specification: string | null
    uomId: string
    qty: number
    estPrice: number
    neededDate: string | null
    note: string | null
  }[]
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
  /** Nguồn giá (design.md mục 18.10). */
  priceSource: PriceSource
  /** Đơn giá đã duyệt theo ĐVT dòng, trước CK; null khi ngoài hợp đồng. */
  approvedUnitPrice: number | null
  approvedDiscountPct: number | null
  priceListNo: string | null
  contractNo: string | null
  sourceRfqNo: string | null
  sourceRfqLineId: string | null
  priceDeviationReason: string | null
  /** Giá thực (sau CK) khác giá đã duyệt – cần lý do và chặng duyệt ngoại lệ. */
  isPriceDeviation: boolean
  /** Không có giá đã duyệt hoặc lệch giá đã duyệt. */
  isOutOfContract: boolean
  /** Số phiếu NCC độc quyền còn tác dụng vào ngày PO (ghi nhận cho KSNB). */
  soleSourceNo: string | null
}

/** Nguồn giá của dòng PO: kết quả chọn thầu, hợp đồng nguyên tắc, bảng giá, ngoài hợp đồng; Unknown = PO trước bản 0.12.0. */
export type PriceSource = 'Unknown' | 'Rfq' | 'Contract' | 'PriceList' | 'None'

/** Nhãn nguồn giá. */
export const priceSourceLabels: Record<PriceSource, string> = {
  Unknown: '',
  Rfq: 'Kết quả chọn thầu',
  Contract: 'Hợp đồng',
  PriceList: 'Bảng giá',
  None: 'Ngoài hợp đồng',
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
  /** Có dòng lệch giá đã duyệt – gửi duyệt phải qua thêm chặng ngoại lệ giá (loại POX). */
  hasPriceException: boolean
  /** Có dòng mua ngoài hợp đồng. */
  isOutOfContract: boolean
  /** Bộ phận đặt hàng (design.md mục 18.10). */
  departmentId: string | null
  departmentName: string | null
  /** Hợp đồng hiệu lực không cho bộ phận của PO đặt hàng – lý do không tính giá theo hợp đồng. */
  contractScopeNote: string | null
  /** Loại mua (design.md mục 18.11). */
  purchaseType: PurchaseType
  emergencyReason: string | null
  /** Hạn bổ sung hồ sơ của PO khẩn cấp. */
  docsDueDate: string | null
  docsCompletedAt: string | null
  docsCompletedByName: string | null
  docsCompletionNote: string | null
  /** PO khẩn đã duyệt, quá hạn mà chưa xác nhận bổ sung hồ sơ. */
  isDocsOverdue: boolean
  canCompleteDocs: boolean
}

/** Loại mua của PO: thường hoặc khẩn cấp (duyệt theo cấp POK, bổ sung hồ sơ sau). */
export type PurchaseType = 'Normal' | 'Emergency'

/** Nhãn loại mua. */
export const purchaseTypeLabels: Record<PurchaseType, string> = { Normal: 'Mua thường', Emergency: 'Khẩn cấp' }

/** Nội dung PO gửi lên. */
export interface SavePurchaseOrderInput {
  date: string
  departmentId: string | null
  purchaseType: PurchaseType
  emergencyReason: string | null
  docsDueDate: string | null
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
    priceDeviationReason: string | null
    sourceRfqLineId: string | null
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

/** Mã loại phiếu có duyệt (design.md mục 18.6). PRL, RFQ, NCC, CTR cấu hình được từ lát A3, gửi duyệt ở lát B1–B4. */
export type ApprovalDocType = 'PR' | 'PO' | 'POX' | 'PRL' | 'RFQ' | 'NCC' | 'CTR' | 'CTA' | 'NDQ'

/** Một loại phiếu có duyệt. */
export interface ApprovalType {
  code: ApprovalDocType
  name: string
  /** Giá trị xét ngưỡng; null = không xét giá trị (ngưỡng luôn 0). */
  amountLabel: string | null
}

/** Phiếu chờ tôi duyệt. */
export interface PendingApproval {
  docType: ApprovalDocType
  docTypeName: string
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
  docType: ApprovalDocType
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
 * Xử lý hàng mới chưa có mã của PR đã duyệt (design.md mục 18.12): lập phiếu tìm nguồn, gắn mã hàng. Xong tải lại dữ liệu mua hàng.
 */
export function usePrNewItemActions() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['purchasing'] })
    void queryClient.invalidateQueries({ queryKey: ['sourcing'] })
  }
  return {
    createSourcing: useMutation({
      mutationFn: ({ requestId, newItemId }: { requestId: string; newItemId: string }) =>
        apiFetch<PurchaseRequest>(`/api/purchase-requests/${requestId}/new-items/${newItemId}/sourcing`, { method: 'POST' }),
      onSuccess,
    }),
    linkItem: useMutation({
      mutationFn: ({ requestId, newItemId, itemId }: { requestId: string; newItemId: string; itemId: string }) =>
        apiFetch<PurchaseRequest>(`/api/purchase-requests/${requestId}/new-items/${newItemId}/link-item`, { method: 'POST', body: { itemId } }),
      onSuccess,
    }),
  }
}

/** Xác nhận đã bổ sung hồ sơ cho PO khẩn cấp đã duyệt (design.md mục 18.11); xong tải lại dữ liệu mua hàng. */
export function useCompletePoDocs() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, note }: { id: string; note: string }) =>
      apiFetch<PurchaseOrder>(`/api/purchase-orders/${id}/complete-docs`, { method: 'POST', body: { note } }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['purchasing'] }),
  })
}

/**
 * Lập, sửa, gửi duyệt, duyệt, từ chối, hủy PR hoặc PO. Xong thì tải lại mọi dữ liệu mua hàng (danh sách, chi tiết, việc cần làm).
 * @param kind Loại phiếu.
 */
export function usePurchaseMutations<TDoc extends { id: string; no: string }, TInput>(kind: PurchaseKind) {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['purchasing'] })
    // Số đếm PR, PO, phiếu chờ duyệt trên cây điều hướng, trang chủ.
    void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
  }
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

/** Các loại phiếu có duyệt (danh sách cố định phía máy chủ). */
export function useApprovalTypes() {
  return useQuery({ queryKey: ['approval-types'], queryFn: () => apiFetch<ApprovalType[]>('/api/approval-rules/types'), staleTime: Infinity })
}

/** Thêm, sửa, xóa cấp duyệt. */
export function useApprovalRuleMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['purchasing'] })
    // Số đếm PR, PO, phiếu chờ duyệt trên cây điều hướng, trang chủ.
    void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
  }
  return {
    create: useMutation({ mutationFn: (input: SaveApprovalRuleInput) => apiFetch<ApprovalRule>('/api/approval-rules', { method: 'POST', body: input }), onSuccess }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveApprovalRuleInput }) => apiFetch<ApprovalRule>(`/api/approval-rules/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    remove: useMutation({ mutationFn: (id: string) => apiFetch<void>(`/api/approval-rules/${id}`, { method: 'DELETE' }), onSuccess }),
  }
}
