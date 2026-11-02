import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Đánh giá NCC định kỳ theo quý (design.md mục 18.20). Khớp Proc.Application.MasterData.SupplierReviews.
 */

const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/** Trạng thái phiếu đánh giá. */
export type SupplierReviewStatus = 'Draft' | 'Completed' | 'Cancelled'

/** Kết luận đánh giá. */
export type SupplierReviewResult = 'Continue' | 'Monitor' | 'Stop'

/** Nhãn trạng thái phiếu. */
export const reviewStatusLabels: Record<SupplierReviewStatus, string> = {
  Draft: 'Nháp',
  Completed: 'Hoàn thành',
  Cancelled: 'Đã hủy',
}

/** Nhãn kết luận. */
export const reviewResultLabels: Record<SupplierReviewResult, string> = {
  Continue: 'Tiếp tục hợp tác',
  Monitor: 'Theo dõi, yêu cầu cải thiện',
  Stop: 'Đề xuất ngừng hợp tác',
}

/** 6 tiêu chí chấm điểm (Bước 10 đề xuất Ban CUVT), theo thứ tự hiển thị. */
export const reviewCriteria = [
  { key: 'priceScore', label: 'Giá, tổng chi phí' },
  { key: 'qualityScore', label: 'Chất lượng' },
  { key: 'deliveryScore', label: 'Giao hàng' },
  { key: 'warrantyScore', label: 'Bảo hành, hỗ trợ' },
  { key: 'paymentScore', label: 'Công nợ, thanh toán' },
  { key: 'complianceScore', label: 'Năng lực tài chính, tuân thủ' },
] as const

/** Khóa điểm của một tiêu chí. */
export type ReviewScoreKey = (typeof reviewCriteria)[number]['key']

/** Thang điểm 1–5. */
export const reviewScoreOptions = [
  { value: 1, label: '1 – Rất kém' },
  { value: 2, label: '2 – Kém' },
  { value: 3, label: '3 – Trung bình' },
  { value: 4, label: '4 – Tốt' },
  { value: 5, label: '5 – Rất tốt' },
]

/** Phiếu đánh giá NCC. */
export interface SupplierReview extends Record<ReviewScoreKey, number | null> {
  id: string
  no: string
  supplierId: string
  supplierCode: string
  supplierName: string
  year: number
  quarter: number
  date: string
  score: number | null
  result: SupplierReviewResult | null
  comment: string | null
  receiptAmount: number
  onTimePct: number | null
  returnPct: number | null
  qualityPct: number | null
  systemRating: string | null
  status: SupplierReviewStatus
  completedAt: string | null
  completedByName: string | null
  cancelReason: string | null
  createdAt: string
  createdByName: string | null
}

/** Một NCC trên bảng đánh giá của quý. */
export interface SupplierReviewBoardRow {
  supplierId: string
  supplierCode: string
  supplierName: string
  isDue: boolean
  receiptAmount: number
  onTimePct: number | null
  returnPct: number | null
  qualityPct: number | null
  systemRating: string | null
  reviewId: string | null
  reviewNo: string | null
  status: SupplierReviewStatus | null
  date: string | null
  score: number | null
  result: SupplierReviewResult | null
  reviewerName: string | null
}

/** Bảng đánh giá NCC của một quý. */
export interface SupplierReviewBoard {
  year: number
  quarter: number
  from: string
  to: string
  dueCount: number
  completedCount: number
  completedPct: number | null
  rows: SupplierReviewBoardRow[]
}

/** Nội dung lập/sửa/hoàn thành phiếu. */
export interface SupplierReviewInput extends Record<ReviewScoreKey, number | null> {
  supplierId: string
  year: number
  quarter: number
  date: string
  result: SupplierReviewResult | null
  comment: string | null
}

/**
 * Bảng đánh giá NCC của một quý tại công ty đang làm việc.
 * @param year Năm.
 * @param quarter Quý 1–4.
 */
export function useSupplierReviewBoard(year: number, quarter: number) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['supplier-reviews', companyId, 'board', year, quarter],
    queryFn: () => apiFetch<SupplierReviewBoard>(`/api/supplier-reviews?year=${year}&quarter=${quarter}`),
    placeholderData: keepPreviousData,
  })
}

/**
 * Chi tiết phiếu đánh giá.
 * @param id Id; null thì không tải.
 */
export function useSupplierReview(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['supplier-reviews', companyId, 'detail', id],
    queryFn: () => apiFetch<SupplierReview>(`/api/supplier-reviews/${id}`),
    enabled: !!id,
  })
}

/** Lập, sửa, hoàn thành, hủy phiếu. Xong thì tải lại bảng đánh giá và bảng KPI (hoàn thành đánh giá NCC). */
export function useSupplierReviewMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['supplier-reviews'] })
    void queryClient.invalidateQueries({ queryKey: ['reports'] })
  }
  return {
    create: useMutation({
      mutationFn: (input: SupplierReviewInput) => apiFetch<SupplierReview>('/api/supplier-reviews', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SupplierReviewInput }) =>
        apiFetch<SupplierReview>(`/api/supplier-reviews/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    complete: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SupplierReviewInput }) =>
        apiFetch<SupplierReview>(`/api/supplier-reviews/${id}/complete`, { method: 'POST', body: input }),
      onSuccess,
    }),
    cancel: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) =>
        apiFetch<SupplierReview>(`/api/supplier-reviews/${id}/cancel`, { method: 'POST', body: { reason } }),
      onSuccess,
    }),
  }
}
