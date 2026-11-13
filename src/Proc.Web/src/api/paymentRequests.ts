import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { ApprovalStep, PurchaseActions, PurchaseStatus } from './purchasing'

/** Biểu mẫu đề nghị thanh toán theo loại mua của PO: 5A công trình, 5B hàng hóa – dịch vụ (lát Y8). */
export type PaymentRequestForm = '5A' | '5B'

/** Nhãn biểu mẫu. */
export const paymentRequestFormLabels: Record<PaymentRequestForm, string> = {
  '5A': '5A – Vật tư công trình',
  '5B': '5B – Hàng hóa, dịch vụ',
}

/** Hình thức thanh toán. */
export type PaymentMethod = 'Cash' | 'BankTransfer'

/** Nhãn hình thức thanh toán (theo tên biểu mẫu "Phiếu đề nghị thanh toán tiền mặt / chuyển khoản"). */
export const paymentMethodLabels: Record<PaymentMethod, string> = { Cash: 'Tiền mặt', BankTransfer: 'Chuyển khoản' }

/**
 * Nhãn trạng thái hiển thị: phiếu đã duyệt mà kế toán đã ghi chi thì "Đã chi".
 * @param status Trạng thái duyệt.
 * @param isPaid Đã ghi Đã chi.
 * @param labels Nhãn trạng thái chung của chứng từ mua.
 */
export function paymentStatusLabel(status: PurchaseStatus, isPaid: boolean, labels: Record<PurchaseStatus, string>): string {
  return isPaid ? 'Đã chi' : labels[status]
}

/** Một dòng danh sách – khớp PaymentRequestListItemDto. */
export interface PaymentRequestListItem {
  id: string
  no: string
  date: string
  form: PaymentRequestForm
  status: PurchaseStatus
  isPaid: boolean
  paidDate: string | null
  purchaseOrderId: string
  purchaseOrderNo: string
  supplierCode: string
  supplierName: string
  method: PaymentMethod
  totalAmount: number
  deductionAmount: number
  payableAmount: number
  createdByName: string | null
}

/** Dòng đề nghị – một hóa đơn của một XNH (khớp PaymentRequestLineDto). */
export interface PaymentRequestLine {
  id: string
  lineNo: number
  receiptConfirmationId: string
  receiptNo: string
  receiptDate: string | null
  invoiceNo: string
  invoiceSeries: string | null
  invoiceDate: string | null
  amount: number
  description: string
}

/** Chi tiết – khớp PaymentRequestDto. */
export interface PaymentRequest {
  id: string
  no: string
  date: string
  form: PaymentRequestForm
  status: PurchaseStatus
  purchaseOrderId: string
  purchaseOrderNo: string
  purchaseOrderDate: string
  supplierId: string
  supplierCode: string
  supplierName: string
  supplierAddress: string | null
  departmentName: string | null
  constructionProjectCode: string | null
  constructionProjectName: string | null
  method: PaymentMethod
  bankAccountId: string | null
  bankName: string | null
  bankBranch: string | null
  bankAccountNumber: string | null
  bankAccountName: string | null
  totalAmount: number
  deductionAmount: number
  deductionReason: string | null
  payableAmount: number
  note: string | null
  paidDate: string | null
  paidByName: string | null
  paidRecordedAt: string | null
  paymentNote: string | null
  canPay: boolean
  rejectReason: string | null
  cancelReason: string | null
  createdAt: string
  createdByName: string | null
  approvedAt: string | null
  lines: PaymentRequestLine[]
  approvals: ApprovalStep[]
  actions: PurchaseActions
  version: number
}

/** PO lập được đề nghị (còn XNH có hóa đơn chưa đề nghị). */
export interface PaymentOrderOption {
  id: string
  no: string
  date: string
  supplierCode: string
  supplierName: string
  openReceiptCount: number
  openAmount: number
}

/** Tài khoản ngân hàng NCC. */
export interface PaymentBankAccount {
  id: string
  bankName: string
  branch: string | null
  accountNumber: string
  accountName: string | null
  isDefault: boolean
}

/** XNH đã xác nhận của PO; invoiceNo trống = chưa cập nhật hóa đơn; heldBy = số đề nghị khác đang giữ. */
export interface PaymentReceiptOption {
  id: string
  no: string
  date: string
  deliveryNoteNo: string | null
  invoiceNo: string | null
  invoiceSeries: string | null
  invoiceDate: string | null
  invoiceAmount: number | null
  receivedAmount: number
  heldBy: string | null
}

/** Dữ liệu chọn khi lập đề nghị cho một PO. */
export interface PaymentRequestSource {
  purchaseOrderId: string
  purchaseOrderNo: string
  form: PaymentRequestForm
  supplierCode: string
  supplierName: string
  departmentName: string | null
  constructionProjectCode: string | null
  bankAccounts: PaymentBankAccount[]
  receipts: PaymentReceiptOption[]
}

/** Dữ liệu lưu nháp – khớp SavePaymentRequestRequest. */
export interface SavePaymentRequestInput {
  date: string
  purchaseOrderId: string
  method: PaymentMethod
  bankAccountId: string | null
  deductionAmount: number
  deductionReason: string | null
  note: string | null
  lines: { receiptConfirmationId: string; description: string | null }[]
}

const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/**
 * Danh sách đề nghị thanh toán (`GET /api/payment-requests`). Khóa dưới `purchasing` để lưu/duyệt xong tự tải lại.
 * @param range Khoảng ngày đề nghị (yyyy-MM-dd).
 * @param range.from Từ ngày.
 * @param range.to Đến ngày.
 */
export function usePaymentRequests(range: { from: string; to: string }) {
  const companyId = useCompanyId()
  const query = new URLSearchParams(range).toString()
  return useQuery({
    queryKey: ['purchasing', 'payment-requests', companyId, query],
    queryFn: () => apiFetch<PaymentRequestListItem[]>(`/api/payment-requests?${query}`),
    enabled: !!companyId,
    placeholderData: keepPreviousData,
  })
}

/**
 * Chi tiết đề nghị.
 * @param id Id; null = không tải (phiếu mới).
 */
export function usePaymentRequest(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['purchasing', 'payment-requests', companyId, id],
    queryFn: () => apiFetch<PaymentRequest>(`/api/payment-requests/${id}`),
    enabled: !!id && !!companyId,
  })
}

/**
 * PO lập được đề nghị thanh toán (`GET /api/payment-requests/orders`).
 * @param enabled Tải khi đang lập mới.
 */
export function usePaymentOrders(enabled: boolean) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['purchasing', 'payment-requests', companyId, 'orders'],
    queryFn: () => apiFetch<PaymentOrderOption[]>('/api/payment-requests/orders'),
    enabled: enabled && !!companyId,
  })
}

/**
 * Dữ liệu chọn cho một PO: tài khoản NCC, XNH (`GET /api/payment-requests/source`).
 * @param purchaseOrderId PO; null = chưa chọn.
 * @param requestId Đề nghị đang sửa (XNH của chính nó không tính là đang giữ).
 */
export function usePaymentSource(purchaseOrderId: string | null, requestId: string | null) {
  const companyId = useCompanyId()
  const params = new URLSearchParams({ purchaseOrderId: purchaseOrderId ?? '' })
  if (requestId) params.set('requestId', requestId)
  const query = params.toString()
  return useQuery({
    queryKey: ['purchasing', 'payment-requests', companyId, 'source', query],
    queryFn: () => apiFetch<PaymentRequestSource>(`/api/payment-requests/source?${query}`),
    enabled: !!purchaseOrderId && !!companyId,
  })
}

/** Kế toán ghi Đã chi (`POST /api/payment-requests/{id}/pay`). */
export function useMarkPaymentPaid() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, paidDate, note }: { id: string; paidDate: string; note: string | null }) =>
      apiFetch<PaymentRequest>(`/api/payment-requests/${id}/pay`, { method: 'POST', body: { paidDate, note } }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['purchasing'] }),
  })
}
