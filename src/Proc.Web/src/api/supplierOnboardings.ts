import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/** Trạng thái hồ sơ đánh giá NCC mới (lát G8, design.md mục 18.25). */
export type SupplierOnboardingStatus = 'Draft' | 'Evaluated' | 'Submitted' | 'Approved' | 'Rejected' | 'NotRecommended' | 'Cancelled'

/** Nhãn tiếng Việt của trạng thái. */
export const onboardingStatusLabels: Record<SupplierOnboardingStatus, string> = {
  Draft: 'Nháp',
  Evaluated: 'Đã đánh giá',
  Submitted: 'Chờ Hội đồng duyệt',
  Approved: 'Đã duyệt',
  Rejected: 'Từ chối',
  NotRecommended: 'Không đề xuất',
  Cancelled: 'Đã hủy',
}

/** Màu đèn theo trạng thái (luôn kèm chữ – docs/ui-style.md mục 2). */
export const onboardingStatusTone: Record<SupplierOnboardingStatus, 'success' | 'warning' | 'error' | 'neutral'> = {
  Draft: 'neutral',
  Evaluated: 'warning',
  Submitted: 'warning',
  Approved: 'success',
  Rejected: 'error',
  NotRecommended: 'error',
  Cancelled: 'error',
}

/** Kết luận đánh giá. */
export type OnboardingResult = 'Pass' | 'Fail'

/** Nhãn kết luận. */
export const onboardingResultLabels: Record<OnboardingResult, string> = { Pass: 'Đạt', Fail: 'Không đạt' }

/** 6 tiêu chí chấm điểm, theo thứ tự hiển thị (khớp tên thuộc tính trên DTO). */
export const onboardingCriteria = [
  { key: 'priceScore', label: 'Giá chào, tổng chi phí' },
  { key: 'sampleQualityScore', label: 'Chất lượng hàng mẫu' },
  { key: 'capacityScore', label: 'Năng lực cung ứng' },
  { key: 'deliveryScore', label: 'Khả năng giao hàng' },
  { key: 'paymentScore', label: 'Công nợ, thanh toán' },
  { key: 'legalScore', label: 'Pháp lý, năng lực tài chính' },
] as const

/** Khóa điểm của một tiêu chí. */
export type OnboardingScoreKey = (typeof onboardingCriteria)[number]['key']

/** Một dòng danh sách hồ sơ. */
export interface SupplierOnboardingListItem {
  id: string
  no: string
  supplierId: string
  supplierCode: string
  supplierName: string
  categoryName: string
  date: string
  score: number | null
  result: OnboardingResult | null
  recommended: boolean | null
  status: SupplierOnboardingStatus
  createdByName: string | null
  createdAt: string
}

/** Một giấy tờ trong hồ sơ pháp lý. */
export interface OnboardingDocument {
  code: string
  name: string
  provided: boolean
  validTo: string | null
  note: string | null
  /** Bắt buộc khi kết luận Đạt (giấy phép kinh doanh). */
  isRequiredForPass: boolean
}

/** Nút được bấm (server tính theo quyền và trạng thái). */
export interface SupplierOnboardingActions {
  canEdit: boolean
  canReopen: boolean
  canPropose: boolean
  canWithdraw: boolean
  canCancel: boolean
}

/** Chi tiết hồ sơ. */
export type SupplierOnboarding = {
  id: string
  no: string
  supplierId: string
  supplierCode: string
  supplierName: string
  supplierStatus: string
  categoryId: string
  categoryCode: string
  categoryName: string
  date: string
  source: string | null
  documents: OnboardingDocument[]
  score: number | null
  result: OnboardingResult | null
  comment: string | null
  note: string | null
  status: SupplierOnboardingStatus
  evaluatedAt: string | null
  evaluatedByName: string | null
  recommended: boolean | null
  proposalComment: string | null
  proposedAt: string | null
  proposedByName: string | null
  changeRequestId: string | null
  changeRequestNo: string | null
  approvedAt: string | null
  rejectReason: string | null
  cancelReason: string | null
  createdAt: string
  createdByName: string | null
  actions: SupplierOnboardingActions
} & Record<OnboardingScoreKey, number | null>

/** Dữ liệu lập/sửa/hoàn thành đánh giá. */
export type SaveSupplierOnboardingInput = {
  supplierId: string
  categoryId: string
  date: string
  source: string | null
  documents: { code: string; provided: boolean; validTo: string | null; note: string | null }[]
  result: OnboardingResult | null
  comment: string | null
  note: string | null
} & Record<OnboardingScoreKey, number | null>

const BASE = '/api/supplier-onboardings'

/** Id công ty đang làm việc – đưa vào khóa truy vấn để đổi công ty thì tải lại dữ liệu. */
function useCompanyId(): string {
  return useAuth().workingCompany?.id ?? ''
}

/** Danh sách hồ sơ đánh giá NCC mới. */
export function useSupplierOnboardings() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['supplier-onboardings', companyId], queryFn: () => apiFetch<SupplierOnboardingListItem[]>(BASE) })
}

/**
 * Chi tiết một hồ sơ.
 * @param id Id hồ sơ; null = không tải (đang lập mới).
 */
export function useSupplierOnboarding(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['supplier-onboardings', companyId, id],
    queryFn: () => apiFetch<SupplierOnboarding>(`${BASE}/${id}`),
    enabled: id !== null,
  })
}

/** Lập, sửa, đánh giá, đề xuất, rút, hủy hồ sơ. Xong thì tải lại danh sách hồ sơ, NCC và yêu cầu duyệt NCC (trạng thái đi kèm). */
export function useSupplierOnboardingMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['supplier-onboardings'] })
    void queryClient.invalidateQueries({ queryKey: ['supplier-change-requests'] })
    void queryClient.invalidateQueries({ queryKey: ['suppliers'] })
  }
  const post = (path: string, body?: unknown) => apiFetch<SupplierOnboarding>(`${BASE}/${path}`, { method: 'POST', body })
  return {
    create: useMutation({ mutationFn: (input: SaveSupplierOnboardingInput) => apiFetch<SupplierOnboarding>(BASE, { method: 'POST', body: input }), onSuccess }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveSupplierOnboardingInput }) =>
        apiFetch<SupplierOnboarding>(`${BASE}/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    evaluate: useMutation({ mutationFn: ({ id, input }: { id: string; input: SaveSupplierOnboardingInput }) => post(`${id}/evaluate`, input), onSuccess }),
    reopen: useMutation({ mutationFn: (id: string) => post(`${id}/reopen`), onSuccess }),
    propose: useMutation({
      mutationFn: ({ id, recommend, comment }: { id: string; recommend: boolean; comment: string | null }) => post(`${id}/propose`, { recommend, comment }),
      onSuccess,
    }),
    withdraw: useMutation({ mutationFn: (id: string) => post(`${id}/withdraw`), onSuccess }),
    cancel: useMutation({ mutationFn: ({ id, reason }: { id: string; reason: string }) => post(`${id}/cancel`, { reason }), onSuccess }),
  }
}
