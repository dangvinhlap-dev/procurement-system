import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { ApprovalStep, PurchaseActions, PurchaseStatus } from './purchasing'

/**
 * Biểu mẫu đề nghị thanh toán: 5A công trình, 5B hàng hóa – dịch vụ theo loại mua của PO (lát Y8); 1C tổng hợp chi phí tạp vụ từ bảng kê 2C,
 * không gắn PO (lát Y11b).
 */
export type PaymentRequestForm = '5A' | '5B' | '1C'

/** Nhãn biểu mẫu. */
export const paymentRequestFormLabels: Record<PaymentRequestForm, string> = {
  '5A': '5A – Vật tư công trình',
  '5B': '5B – Hàng hóa, dịch vụ',
  '1C': '1C – Tổng hợp chi phí tạp vụ',
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
  /** Trống với 1C (tạp vụ không qua PO). */
  purchaseOrderId: string | null
  purchaseOrderNo: string | null
  supplierCode: string | null
  supplierName: string | null
  method: PaymentMethod
  totalAmount: number
  deductionAmount: number
  payableAmount: number
  createdByName: string | null
  /** Người nhận tiền (1C). */
  payeeName: string | null
  departmentName: string | null
}

/** Dòng đề nghị – một hóa đơn của một XNH (5A/5B) hoặc một bảng kê tạp vụ 2C (1C) – khớp PaymentRequestLineDto. */
export interface PaymentRequestLine {
  id: string
  lineNo: number
  receiptConfirmationId: string | null
  receiptNo: string | null
  receiptDate: string | null
  invoiceNo: string | null
  invoiceSeries: string | null
  /** 5A/5B: ngày hóa đơn; 1C: ngày bảng kê. */
  invoiceDate: string | null
  amount: number
  description: string
  sundryExpenseSheetId: string | null
  sundryExpenseSheetNo: string | null
}

/** Chi tiết – khớp PaymentRequestDto. */
export interface PaymentRequest {
  id: string
  no: string
  date: string
  form: PaymentRequestForm
  status: PurchaseStatus
  purchaseOrderId: string | null
  purchaseOrderNo: string | null
  purchaseOrderDate: string | null
  supplierId: string | null
  supplierCode: string | null
  supplierName: string | null
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
  /** Người nhận tiền (1C). */
  payeeName: string | null
  departmentId: string | null
}

/** Bảng kê tạp vụ 2C đã duyệt chọn được vào đề nghị 1C (lát Y11b); heldBy = số đề nghị khác đang giữ. */
export interface SundryPaymentSheetOption {
  id: string
  no: string
  date: string
  departmentId: string
  departmentCode: string
  departmentName: string
  lineCount: number
  totalAmount: number
  note: string | null
  heldBy: string | null
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

/** Dữ liệu lưu nháp – khớp SavePaymentRequestRequest. 1C: form = '1C', bộ phận, người nhận tiền, ngân hàng nhập tay, dòng là bảng kê. */
export interface SavePaymentRequestInput {
  date: string
  purchaseOrderId: string | null
  method: PaymentMethod
  bankAccountId: string | null
  deductionAmount: number
  deductionReason: string | null
  note: string | null
  lines: { receiptConfirmationId: string | null; description: string | null; sundryExpenseSheetId?: string | null }[]
  form?: '1C' | null
  departmentId?: string | null
  payeeName?: string | null
  bankName?: string | null
  bankBranch?: string | null
  bankAccountNumber?: string | null
  bankAccountName?: string | null
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

/**
 * Bảng kê tạp vụ 2C đã duyệt cho đề nghị 1C (`GET /api/payment-requests/sundry-sheets`).
 * @param departmentId Bộ phận; null = chưa chọn (không tải).
 * @param requestId Đề nghị đang sửa (bảng kê của chính nó không tính là đang giữ).
 */
export function useSundryPaymentSheets(departmentId: string | null, requestId: string | null) {
  const companyId = useCompanyId()
  const params = new URLSearchParams({ departmentId: departmentId ?? '' })
  if (requestId) params.set('requestId', requestId)
  const query = params.toString()
  return useQuery({
    queryKey: ['purchasing', 'payment-requests', companyId, 'sundry-sheets', query],
    queryFn: () => apiFetch<SundryPaymentSheetOption[]>(`/api/payment-requests/sundry-sheets?${query}`),
    enabled: !!departmentId && !!companyId,
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
