import { useQuery } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { ApprovalStep, PurchaseActions, PurchaseStatus } from './purchasing'

/** Biểu mẫu bảng tổng hợp: 2A công trình (đề nghị loại A cùng một công trình), 2B hàng hóa – dịch vụ (đề nghị loại B, DV). */
export type PurchaseSummaryForm = 'Construction' | 'Goods'

/** Nhãn biểu mẫu (design.md mục 19.3, lát Y5b). */
export const purchaseSummaryFormLabels: Record<PurchaseSummaryForm, string> = {
  Construction: '2A – Công trình',
  Goods: '2B – Hàng hóa, dịch vụ',
}

/** Mã ngắn của biểu mẫu trên danh sách. */
export const purchaseSummaryFormCodes: Record<PurchaseSummaryForm, string> = { Construction: '2A', Goods: '2B' }

/** Một dòng danh sách bảng tổng hợp – khớp PurchaseSummaryListItemDto. */
export interface PurchaseSummaryListItem {
  id: string
  no: string
  date: string
  form: PurchaseSummaryForm
  status: PurchaseStatus
  departmentName: string | null
  constructionProjectCode: string | null
  lineCount: number
  totalAmount: number
  createdByName: string | null
}

/** Dòng bảng tổng hợp – khớp PurchaseSummaryLineDto. SL, đơn giá chụp lúc lưu nháp và lúc gửi duyệt. */
export interface PurchaseSummaryLine {
  id: string
  lineNo: number
  requestId: string
  requestNo: string
  requestLineId: string
  requestCategory: string
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  qty: number
  estPrice: number
  amount: number
  neededDate: string | null
  purchasingUnitId: string
  purchasingUnitCode: string
  purchasingUnitName: string
  note: string | null
}

/**
 * Khung chi phí đầu phiếu – khớp PurchaseSummaryFrameDto. 2A: dự toán, lũy kế đã mua, tổng đề xuất, còn lại = dự toán − lũy kế − đề xuất;
 * 2B: ngân sách bộ phận × tháng, lũy kế đã dùng (đã gồm các đề nghị của bảng), còn lại = ngân sách − lũy kế.
 */
export interface PurchaseSummaryFrame {
  limitAmount: number | null
  accumulatedAmount: number
  proposedAmount: number
  remainingAmount: number | null
  exceeded: boolean
  projectCode: string | null
  projectName: string | null
  budgetMonth: string | null
}

/** Chi tiết bảng tổng hợp – khớp PurchaseSummaryDto. */
export interface PurchaseSummary {
  id: string
  no: string
  date: string
  form: PurchaseSummaryForm
  status: PurchaseStatus
  departmentId: string
  departmentName: string | null
  constructionProjectId: string | null
  note: string | null
  totalAmount: number
  frame: PurchaseSummaryFrame
  rejectReason: string | null
  cancelReason: string | null
  createdAt: string
  createdByName: string | null
  approvedAt: string | null
  lines: PurchaseSummaryLine[]
  approvals: ApprovalStep[]
  actions: PurchaseActions
  version: number
}

/** Dòng đề nghị đã duyệt còn cần mua, chưa ở bảng tổng hợp chưa hủy nào – khớp PurchaseSummaryCandidateDto. */
export interface PurchaseSummaryCandidate {
  requestLineId: string
  requestId: string
  requestNo: string
  requestDate: string
  requestCategory: string
  departmentId: string | null
  departmentName: string | null
  constructionProjectId: string | null
  constructionProjectCode: string | null
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  openQty: number
  estPrice: number
  amount: number
  neededDate: string | null
}

/** Dữ liệu lưu bảng tổng hợp nháp – khớp SavePurchaseSummaryRequest. */
export interface SavePurchaseSummaryInput {
  date: string
  form: PurchaseSummaryForm
  departmentId: string
  constructionProjectId: string | null
  note: string | null
  lines: { requestLineId: string; purchasingUnitId: string; note: string | null }[]
}

/** Công ty đang làm việc – phiếu theo công ty nên khóa bộ nhớ đệm có công ty. */
const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/** Danh sách bảng tổng hợp của công ty đang làm việc (mới nhất trước). Khóa dưới `purchasing` để lưu/duyệt xong tự tải lại. */
export function usePurchaseSummaries() {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['purchasing', 'purchase-summaries', companyId],
    queryFn: () => apiFetch<PurchaseSummaryListItem[]>('/api/purchase-summaries'),
    enabled: !!companyId,
  })
}

/**
 * Chi tiết bảng tổng hợp.
 * @param id Id phiếu; null = không tải (phiếu mới hoặc trang khác loại).
 */
export function usePurchaseSummary(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['purchasing', 'purchase-summaries', companyId, id],
    queryFn: () => apiFetch<PurchaseSummary>(`/api/purchase-summaries/${id}`),
    enabled: !!id && !!companyId,
  })
}

/**
 * Dòng đề nghị chọn được vào bảng tổng hợp theo biểu mẫu, bộ phận, công trình của đầu phiếu.
 * @param filter Bộ lọc; null = chưa đủ điều kiện để tải.
 * @param filter.form Biểu mẫu.
 * @param filter.departmentId Bộ phận đề xuất.
 * @param filter.constructionProjectId Công trình (2A).
 * @param filter.summaryId Bảng đang sửa – dòng đã ở chính bảng này vẫn hiện.
 */
export function usePurchaseSummaryCandidates(
  filter: { form: PurchaseSummaryForm; departmentId: string; constructionProjectId: string | null; summaryId: string | null } | null,
) {
  const companyId = useCompanyId()
  const params = new URLSearchParams()
  if (filter) {
    params.set('form', filter.form)
    params.set('departmentId', filter.departmentId)
    if (filter.constructionProjectId) params.set('constructionProjectId', filter.constructionProjectId)
    if (filter.summaryId) params.set('summaryId', filter.summaryId)
  }
  const query = params.toString()
  return useQuery({
    queryKey: ['purchasing', 'purchase-summaries', companyId, 'candidates', query],
    queryFn: () => apiFetch<PurchaseSummaryCandidate[]>(`/api/purchase-summaries/candidates?${query}`),
    enabled: !!filter && !!companyId,
  })
}
