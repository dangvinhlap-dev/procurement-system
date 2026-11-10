import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/** Trạng thái phiếu xác nhận nhận hàng XNH (lát K2, design.md mục 19.4). */
export type ReceiptConfirmationStatus = 'Draft' | 'Confirmed' | 'Cancelled'

/** Nhãn tiếng Việt của trạng thái. */
export const receiptConfirmationStatusLabels: Record<ReceiptConfirmationStatus, string> = {
  Draft: 'Nháp',
  Confirmed: 'Đã xác nhận',
  Cancelled: 'Đã hủy',
}

/** Màu đèn theo trạng thái (luôn kèm chữ – docs/ui-style.md mục 2). */
export const receiptConfirmationStatusTone: Record<ReceiptConfirmationStatus, 'success' | 'warning' | 'error' | 'neutral'> = {
  Draft: 'warning',
  Confirmed: 'success',
  Cancelled: 'error',
}

/** Kết quả kiểm tra dòng. */
export type ReceiptCheckResult = 'NotChecked' | 'Passed' | 'Failed'

/** Nhãn kết quả kiểm tra. */
export const receiptCheckResultLabels: Record<ReceiptCheckResult, string> = {
  NotChecked: 'Chưa kiểm tra',
  Passed: 'Đạt',
  Failed: 'Không đạt',
}

/** Nghiệm thu của đơn vị đặt hàng. */
export type ReceiptAcceptance = 'None' | 'Accepted' | 'Disputed'

/** Nhãn nghiệm thu. */
export const receiptAcceptanceLabels: Record<ReceiptAcceptance, string> = {
  None: 'Chưa nghiệm thu',
  Accepted: 'Đạt',
  Disputed: 'Không đạt',
}

/** Một dòng danh sách XNH. */
export interface ReceiptConfirmationListItem {
  id: string
  no: string
  date: string
  status: ReceiptConfirmationStatus
  purchaseOrderId: string
  purchaseOrderNo: string
  supplierCode: string
  supplierName: string
  warehouseCode: string | null
  deliveryNoteNo: string | null
  invoiceNo: string | null
  totalWithVat: number
  acceptance: ReceiptAcceptance
  createdByName: string | null
}

/** Kết quả danh sách (giới hạn số phiếu mỗi lần xem). */
export interface ReceiptConfirmationListResult {
  items: ReceiptConfirmationListItem[]
  truncated: boolean
  limit: number
}

/** Dòng XNH – SL theo ĐVT dòng PO. */
export interface ReceiptConfirmationLine {
  id: string
  lineNo: number
  purchaseOrderLineId: string
  poLineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uomCode: string
  orderedQty: number
  /** SL thực nhận ở các XNH khác đã xác nhận. */
  otherReceivedQty: number
  remainingQty: number
  deliveredQty: number
  acceptedQty: number
  rejectedQty: number
  adjustedQty: number
  receivedQty: number
  unitPrice: number
  vatCode: string | null
  amount: number
  vatAmount: number
  checkResult: ReceiptCheckResult
  checkNote: string | null
  note: string | null
}

/** Một lần điều chỉnh SL thực nhận. */
export interface ReceiptAdjustment {
  id: string
  lineNo: number
  qty: number
  reason: string
  adjustedAt: string
  adjustedByName: string | null
}

/** Thao tác người xem được làm. */
export interface ReceiptConfirmationActions {
  canEdit: boolean
  canConfirm: boolean
  canCancel: boolean
  canAdjust: boolean
  canEditInvoice: boolean
  canAccept: boolean
  readOnlyReason: string | null
}

/** Chi tiết XNH. */
export interface ReceiptConfirmation {
  id: string
  no: string
  date: string
  status: ReceiptConfirmationStatus
  purchaseOrderId: string
  purchaseOrderNo: string
  purchaseOrderDate: string
  supplierId: string
  supplierCode: string
  supplierName: string
  warehouseId: string
  warehouseCode: string | null
  warehouseName: string | null
  departmentName: string | null
  deliveryNoteNo: string | null
  deliverer: string | null
  invoiceNo: string | null
  invoiceSeries: string | null
  invoiceDate: string | null
  invoiceAmount: number | null
  note: string | null
  cancelReason: string | null
  totalAmount: number
  totalVat: number
  createdAt: string
  createdByName: string | null
  confirmedAt: string | null
  confirmedByName: string | null
  acceptance: ReceiptAcceptance
  acceptanceNote: string | null
  acceptedAt: string | null
  acceptedByName: string | null
  lines: ReceiptConfirmationLine[]
  adjustments: ReceiptAdjustment[]
  actions: ReceiptConfirmationActions
  version: number
}

/** Dòng gửi khi lưu. */
export interface SaveReceiptConfirmationLineInput {
  purchaseOrderLineId: string
  deliveredQty: number
  acceptedQty: number
  rejectedQty: number
  checkResult: ReceiptCheckResult | null
  checkNote: string | null
  note: string | null
}

/** Dữ liệu lập/sửa XNH nháp. */
export interface SaveReceiptConfirmationInput {
  purchaseOrderId: string
  date: string
  deliveryNoteNo: string | null
  deliverer: string | null
  note: string | null
  invoiceNo: string | null
  invoiceSeries: string | null
  invoiceDate: string | null
  invoiceAmount: number | null
  lines: SaveReceiptConfirmationLineInput[]
}

/** Hóa đơn NCC. */
export interface ReceiptInvoiceInput {
  invoiceNo: string | null
  invoiceSeries: string | null
  invoiceDate: string | null
  invoiceAmount: number | null
}

const BASE = '/api/receipt-confirmations'

/** Id công ty đang làm việc – đưa vào khóa truy vấn để đổi công ty thì tải lại dữ liệu. */
function useCompanyId(): string {
  return useAuth().workingCompany?.id ?? ''
}

/**
 * Danh sách XNH.
 * @param query Từ ngày, đến ngày (yyyy-MM-dd), trạng thái; trống = máy chủ tự chọn 90 ngày gần nhất.
 */
export function useReceiptConfirmations(query: { from?: string; to?: string; status?: string; purchaseOrderId?: string }) {
  const companyId = useCompanyId()
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) if (value) params.set(key, value)
  return useQuery({
    queryKey: ['receipt-confirmations', companyId, query],
    queryFn: () => apiFetch<ReceiptConfirmationListResult>(`${BASE}?${params.toString()}`),
  })
}

/**
 * Chi tiết một XNH.
 * @param id Id phiếu; null = không tải (đang lập mới).
 */
export function useReceiptConfirmation(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['receipt-confirmations', companyId, id],
    queryFn: () => apiFetch<ReceiptConfirmation>(`${BASE}/${id}`),
    enabled: id !== null,
  })
}

/**
 * Lập, sửa, xác nhận, hủy, cập nhật hóa đơn, nghiệm thu, điều chỉnh XNH. Xong thì tải lại phiếu và đơn mua hàng (SL đã nhận, trạng thái
 * PO đổi theo XNH).
 */
export function useReceiptConfirmationMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['receipt-confirmations'] })
    void queryClient.invalidateQueries({ queryKey: ['purchasing'] })
    void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
  }
  const post = (path: string, body?: unknown) => apiFetch<ReceiptConfirmation>(`${BASE}/${path}`, { method: 'POST', body })
  return {
    create: useMutation({
      mutationFn: (input: SaveReceiptConfirmationInput) => apiFetch<ReceiptConfirmation>(BASE, { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveReceiptConfirmationInput }) =>
        apiFetch<ReceiptConfirmation>(`${BASE}/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    confirm: useMutation({ mutationFn: (id: string) => post(`${id}/confirm`), onSuccess }),
    cancel: useMutation({ mutationFn: ({ id, reason }: { id: string; reason: string }) => post(`${id}/cancel`, { reason }), onSuccess }),
    invoice: useMutation({
      mutationFn: ({ id, input }: { id: string; input: ReceiptInvoiceInput }) =>
        apiFetch<ReceiptConfirmation>(`${BASE}/${id}/invoice`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    accept: useMutation({
      mutationFn: ({ id, accepted, note }: { id: string; accepted: boolean; note: string | null }) => post(`${id}/acceptance`, { accepted, note }),
      onSuccess,
    }),
    adjust: useMutation({
      mutationFn: ({ id, lineId, qty, reason }: { id: string; lineId: string; qty: number; reason: string }) =>
        post(`${id}/adjustments`, { lineId, qty, reason }),
      onSuccess,
    }),
  }
}
