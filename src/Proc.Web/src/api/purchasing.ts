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

/**
 * Nhãn trạng thái có xét đóng tay (lát R6): PR/PO đã đóng phần còn lại hiện "Đã đóng phần còn lại" thay vì "Đã nhận đủ".
 * @param status Trạng thái.
 * @param closedManually Đã đóng phần còn lại.
 */
export function purchaseStatusText(status: PurchaseStatus, closedManually?: boolean | null): string {
  return status === 'Closed' && closedManually ? 'Đã đóng phần còn lại' : purchaseStatusLabels[status]
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
  /** Người sẽ duyệt bước còn chờ nhưng chưa đến lượt (cấp/chặng sau) – yêu cầu người dùng 03/11/2026. */
  upcomingApprovers?: string[] | null
  /** Người và thời điểm gửi duyệt lượt của bước; null với dữ liệu cũ. */
  submittedAt?: string | null
  submittedByName?: string | null
}

/**
 * Nhãn trạng thái kèm người đang chờ duyệt, ví dụ "Chờ duyệt: Nguyễn A, Trần B" (yêu cầu người dùng 03/11/2026). Phiếu không còn bước
 * đang đến lượt thì giữ nguyên nhãn.
 * @param label Nhãn trạng thái gốc.
 * @param steps Lịch sử duyệt của phiếu.
 */
export function withPendingApprovers(label: string, steps: ApprovalStep[] | null | undefined): string {
  const names = [...new Set((steps ?? []).flatMap((s) => (s.status === 'Pending' && s.nextApprovers ? s.nextApprovers : [])))]
  if (!steps?.some((s) => s.status === 'Pending' && s.nextApprovers)) return label
  if (names.length === 0) return `${label}: chưa có người đủ điều kiện`
  return `${label}: ${names.length > 3 ? `${names.slice(0, 3).join(', ')} và ${names.length - 3} người khác` : names.join(', ')}`
}

/** Chuỗi duyệt dự kiến khi gửi – khớp ApprovalPreviewDto. */
export interface ApprovalPreview {
  approvedImmediately: boolean
  steps: ApprovalStep[]
  /** Duyệt NCC: các yêu cầu duyệt sẽ sinh khi lưu hồ sơ (rỗng = lưu không cần duyệt). */
  groups?: { title: string; approvedImmediately: boolean; steps: ApprovalStep[] }[] | null
}

/**
 * Xem trước chuỗi duyệt (server chạy thử lệnh gửi duyệt rồi hủy – không lưu gì). Lỗi như khi gửi thật (thiếu lý do lệch giá…) ném ra.
 * @param docType PR, PO, PRL, RFQ, CTR, CTA, NDQ.
 * @param documentId Id phiếu đã lưu.
 * @param award RFQ: lựa chọn trúng thầu sẽ gửi.
 * @param supplier NCC: hồ sơ sắp lưu (documentId = Id NCC, hoặc Guid rỗng khi lập mới).
 */
export function previewApproval(docType: string, documentId: string, award?: unknown, supplier?: unknown) {
  return apiFetch<ApprovalPreview>('/api/approvals/preview', {
    method: 'POST',
    body: { docType, documentId, award: award ?? null, supplier: supplier ?? null },
  })
}

/** Guid rỗng – xem trước duyệt NCC khi lập NCC mới (chưa có Id). */
export const EMPTY_GUID = '00000000-0000-0000-0000-000000000000'

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
  /** Đóng phần còn lại (lát R6): PR đã lên PO một phần, PO đã nhận một phần. */
  canCloseRemaining?: boolean
}

/** Thông tin đóng phần còn lại của PR/PO (lát R6, design.md mục 18.26). */
export interface ClosedRemaining {
  /** Lý do; null = phiếu không đóng tay. */
  closedReason?: string | null
  /** Thời điểm đóng (UTC, ISO). */
  closedAt?: string | null
  /** Họ tên người đóng. */
  closedByName?: string | null
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
  /** Đã đóng phần còn lại (lát R6). */
  closedManually?: boolean
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
export interface PurchaseRequest extends ClosedRemaining {
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
  /** Kiểm tra ngân sách bộ phận × tháng lúc lưu/gửi gần nhất (design.md mục 18.24); null = không kiểm soát. */
  budget: BudgetCheck | null
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
  /** Số phiếu duyệt NCC và giá tháng phủ mặt hàng của dòng (design.md mục 18.25). */
  monthlyApprovalNo: string | null
  /** NCC của PO không phải NCC chính/dự phòng của phiếu tháng – mua ngoài danh sách, phải qua chặng POL. */
  isOffMonthlyList: boolean
  /** Giá trần của phiếu tháng quy về ĐVT dòng (lát G4b); null = không có trần. */
  ceilingPrice: number | null
  /** Giá ròng (sau chiết khấu) vượt giá trần – chỉ cảnh báo, không chặn. */
  isOverCeiling: boolean
  /** Mã hàng theo NCC chép từ NCC – sản phẩm lúc lưu/gửi (design.md mục 5.9); null = NCC chưa khai báo. */
  supplierItemCode: string | null
}

/**
 * Nguồn giá của dòng PO: phiếu duyệt NCC và giá tháng (đứng đầu – design.md mục 18.25), kết quả chọn thầu, hợp đồng nguyên tắc, bảng giá,
 * ngoài hợp đồng; Unknown = PO trước bản 0.12.0.
 */
export type PriceSource = 'Unknown' | 'MonthlyApproval' | 'Rfq' | 'Contract' | 'PriceList' | 'None'

/** Nhãn nguồn giá. */
export const priceSourceLabels: Record<PriceSource, string> = {
  Unknown: '',
  MonthlyApproval: 'Duyệt tháng',
  Rfq: 'Kết quả chọn thầu',
  Contract: 'Hợp đồng',
  PriceList: 'Bảng giá',
  None: 'Ngoài hợp đồng',
}

/** Chi tiết PO. */
export interface PurchaseOrder extends ClosedRemaining {
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
  /** Có dòng ngoài danh sách tháng hoặc không có giá đã duyệt – gửi duyệt qua thêm chặng "Ngoài danh sách/giá" (POL, mục 18.25). */
  isOffApprovedList: boolean
  /** Có dòng giá ròng vượt giá trần của phiếu duyệt tháng – cảnh báo, không chặn (mục 18.25, G4b). */
  isOverCeiling: boolean
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
  /** Kiểm tra ngân sách bộ phận × tháng lúc lưu/gửi gần nhất (design.md mục 18.24); null = không kiểm soát. */
  budget: BudgetCheck | null
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
export type ApprovalDocType = 'PR' | 'PO' | 'POX' | 'POK' | 'PRB' | 'POB' | 'PRL' | 'RFQ' | 'NCC' | 'CTR' | 'CTA' | 'NDQ' | 'GTH' | 'CKB'

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
        apiFetch<TDoc>(`/api/${kind}/${id}/${name}`, { method: 'POST', body: actionBody(name, comment) }),
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
    closeRemaining: useAction('close-remaining'),
  }
}

/**
 * Thân request của lệnh trên phiếu: duyệt/từ chối gửi ý kiến; đóng phần còn lại (lát R6) và hủy phiếu đã duyệt (lát R7) gửi lý do;
 * lệnh khác không có thân.
 * @param name Tên lệnh trên đường dẫn.
 * @param comment Ý kiến hoặc lý do người dùng nhập.
 */
function actionBody(name: string, comment: string | null | undefined): unknown {
  if (name === 'approve' || name === 'reject') return { comment: comment ?? null }
  if (name === 'close-remaining' || name === 'cancel') return comment ? { reason: comment } : undefined
  return undefined
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

/** Tình trạng một cấp duyệt mẫu: sẽ tạo, loại đã cấu hình (giữ nguyên), thiếu vai trò mẫu, chỉ áp ở tổng công ty. */
export type ApprovalRuleTemplateStatus = 'Create' | 'Configured' | 'MissingRole' | 'HeadOfficeOnly'

/** Một cấp duyệt mẫu theo sơ đồ phê duyệt mua sắm (lát G10 – design.md mục 18.25). */
export interface ApprovalRuleTemplate {
  docType: string
  typeName: string
  level: number
  roleCode: string
  roleName: string | null
  note: string
  status: ApprovalRuleTemplateStatus
}

/**
 * Xem trước cấu hình duyệt mẫu cho công ty đang làm việc.
 * @param enabled Chỉ tải khi hộp thoại mở.
 */
export function useApprovalRuleTemplate(enabled: boolean) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['purchasing', companyId, 'approval-rules', 'template'],
    queryFn: () => apiFetch<ApprovalRuleTemplate[]>('/api/approval-rules/template'),
    enabled,
  })
}

/** Thêm, sửa, xóa cấp duyệt; áp cấu hình duyệt mẫu. */
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
    applyTemplate: useMutation({
      mutationFn: () => apiFetch<{ created: number; rows: ApprovalRuleTemplate[] }>('/api/approval-rules/template', { method: 'POST' }),
      onSuccess,
    }),
  }
}

/**
 * Kiểm tra ngân sách của PR/PO (design.md mục 18.24): ngân sách bộ phận × tháng của ngày phiếu, đã dùng của các phiếu khác, giá trị phiếu
 * (chưa VAT). Vượt thì gửi duyệt qua thêm chặng "Vượt ngân sách" – không chặn.
 */
export interface BudgetCheck {
  year: number
  month: number
  budgetAmount: number
  usedAmount: number
  documentAmount: number
  isOver: boolean
  /** Số vượt = đã dùng + phiếu − ngân sách (0 khi không vượt). */
  overAmount: number
}

/** Một tháng ngân sách của bộ phận. */
export interface BudgetMonth {
  month: number
  /** null = tháng này bộ phận không kiểm soát ngân sách. */
  budget: number | null
  /** Đã dùng: PO chờ duyệt/đã duyệt/đã đóng + phần PR chờ duyệt/đã duyệt chưa lên PO. */
  used: number
  /** Ngân sách − đã dùng (âm khi vượt); null khi không có ngân sách. */
  remaining: number | null
}

/** Ngân sách cả năm của một bộ phận. */
export interface DepartmentBudgetYear {
  departmentId: string
  departmentCode: string
  departmentName: string
  isActive: boolean
  months: BudgetMonth[]
  totalBudget: number
  totalUsed: number
  /** Số tháng vượt ngân sách. */
  overMonths: number
}

/** Ngân sách các bộ phận của công ty đang làm việc trong một năm (design.md mục 18.24). */
export function useBudgetYear(year: number) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['purchasing', companyId, 'budgets', year],
    queryFn: () => apiFetch<DepartmentBudgetYear[]>(`/api/budgets/${year}`),
  })
}

/** Lưu ngân sách 12 tháng của một bộ phận (null = bỏ ngân sách tháng đó); xong tải lại bảng ngân sách. */
export function useSaveBudget() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ year, departmentId, months }: { year: number; departmentId: string; months: (number | null)[] }) =>
      apiFetch<DepartmentBudgetYear>(`/api/budgets/${year}/${departmentId}`, { method: 'PUT', body: { months } }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchasing'] }),
  })
}

/** Nhãn chặng phụ của PR, PO trong lịch sử duyệt: 2 = ngoại lệ giá (design.md mục 18.10), 3 = vượt ngân sách (mục 18.24). */
export const PURCHASE_STAGE_LABELS: Record<number, string> = { 2: 'Ngoại lệ giá', 3: 'Vượt ngân sách', 4: 'Ngoài danh sách/giá' }
