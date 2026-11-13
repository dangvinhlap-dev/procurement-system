import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { ApprovalStep, BudgetCheck, PurchaseActions, PurchaseStatus } from './purchasing'

/** Một dòng danh sách – khớp SundryExpenseSheetListItemDto (lát Y11a). */
export interface SundryExpenseSheetListItem {
  id: string
  no: string
  date: string
  status: PurchaseStatus
  departmentCode: string
  departmentName: string
  lineCount: number
  totalAmount: number
  isOverBudget: boolean
  /** Số đề nghị thanh toán tổng hợp 1C chưa hủy đang giữ bảng kê (lát Y11b). */
  paymentRequestNo: string | null
  paymentRequestId: string | null
  createdByName: string | null
}

/** Một khoản chi – khớp SundryExpenseLineDto. */
export interface SundryExpenseLine {
  id: string
  lineNo: number
  expenseDate: string
  description: string
  vendor: string | null
  receiptNo: string | null
  amount: number
}

/** Chi tiết – khớp SundryExpenseSheetDto. */
export interface SundryExpenseSheet {
  id: string
  no: string
  date: string
  status: PurchaseStatus
  departmentId: string
  departmentCode: string
  departmentName: string
  totalAmount: number
  /** Định mức tháng (ngân sách bộ phận × tháng) lúc lưu / gửi gần nhất; null = tháng đó bộ phận chưa có định mức. */
  budget: BudgetCheck | null
  note: string | null
  paymentRequestNo: string | null
  paymentRequestId: string | null
  rejectReason: string | null
  cancelReason: string | null
  createdAt: string
  createdByName: string | null
  approvedAt: string | null
  lines: SundryExpenseLine[]
  approvals: ApprovalStep[]
  actions: PurchaseActions
  version: number
}

/** Dữ liệu lưu nháp – khớp SaveSundryExpenseSheetRequest. */
export interface SaveSundryExpenseSheetInput {
  date: string
  departmentId: string
  note: string | null
  lines: { expenseDate: string; description: string | null; vendor: string | null; receiptNo: string | null; amount: number }[]
}

/**
 * Biểu mẫu 1C / 2C còn là mẫu giả lập (người dùng dặn 13/11/2026: chỉ nhập thử cho đến khi có mẫu thật – design.md 19.3 "Lát Y11d"). Đổi thành
 * false khi đã thay bằng mẫu thật (cùng cờ `CuvtPrintForms.SundryFormsSimulated` phía máy chủ).
 */
export const SUNDRY_FORMS_SIMULATED = true

/** Lời dặn hiện trên màn hình bảng kê 2C và đề nghị 1C khi còn mẫu giả lập. */
export const SUNDRY_SIMULATED_NOTICE =
  'Biểu mẫu 1C/2C đang là mẫu giả lập – chỉ nhập dữ liệu thử cho đến khi công ty có mẫu thật. Không dùng bản in làm chứng từ thanh toán.'

const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/**
 * Danh sách bảng kê chi phí tạp vụ (`GET /api/sundry-expense-sheets`). Khóa dưới `purchasing` để lưu/duyệt xong tự tải lại.
 * @param range Khoảng ngày bảng kê (yyyy-MM-dd).
 * @param range.from Từ ngày.
 * @param range.to Đến ngày.
 */
export function useSundryExpenseSheets(range: { from: string; to: string }) {
  const companyId = useCompanyId()
  const query = new URLSearchParams(range).toString()
  return useQuery({
    queryKey: ['purchasing', 'sundry-expense-sheets', companyId, query],
    queryFn: () => apiFetch<SundryExpenseSheetListItem[]>(`/api/sundry-expense-sheets?${query}`),
    enabled: !!companyId,
    placeholderData: keepPreviousData,
  })
}

/**
 * Chi tiết bảng kê.
 * @param id Id; null = không tải (phiếu mới).
 */
export function useSundryExpenseSheet(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['purchasing', 'sundry-expense-sheets', companyId, id],
    queryFn: () => apiFetch<SundryExpenseSheet>(`/api/sundry-expense-sheets/${id}`),
    enabled: !!id && !!companyId,
  })
}
