import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { ApprovalStep } from './purchasing'

/*
 * Kiểu dữ liệu và hàm gọi API tìm nguồn, RFQ, báo giá, chọn thầu (khớp Proc.Application.Sourcing).
 */

const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/** Trạng thái phiếu tìm nguồn. */
export type SourcingStatus = 'Open' | 'Closed' | 'Cancelled'

/** Nhãn trạng thái phiếu tìm nguồn. */
export const sourcingStatusLabels: Record<SourcingStatus, string> = { Open: 'Đang tìm', Closed: 'Đã xong', Cancelled: 'Đã hủy' }

/** Tông màu trạng thái phiếu tìm nguồn. */
export const sourcingStatusTone: Record<SourcingStatus, 'success' | 'warning' | 'neutral'> = { Open: 'warning', Closed: 'success', Cancelled: 'neutral' }

/** Trạng thái NCC ứng viên (design.md mục 8.3). */
export type CandidateStatus = 'Contacted' | 'RfqSent' | 'Quoted' | 'Passed' | 'Failed'

/** Nhãn trạng thái NCC ứng viên. */
export const candidateStatusLabels: Record<CandidateStatus, string> = {
  Contacted: 'Đã liên hệ',
  RfqSent: 'Đã gửi RFQ',
  Quoted: 'Đã báo giá',
  Passed: 'Đạt',
  Failed: 'Không đạt',
}

/** Một dòng danh sách tìm nguồn. */
export interface SourcingListItem {
  id: string
  no: string
  date: string
  subject: string
  qty: number | null
  deadline: string | null
  responsible: string | null
  candidateCount: number
  passedCount: number
  status: SourcingStatus
}

/** NCC ứng viên. */
export interface SourcingCandidate {
  id: string
  supplierId: string
  supplierCode: string
  supplierName: string
  supplierStatus: string
  status: CandidateStatus
  note: string | null
}

/** Chi tiết phiếu tìm nguồn. */
export interface Sourcing {
  id: string
  no: string
  date: string
  itemId: string | null
  itemCode: string | null
  itemName: string | null
  uomCode: string | null
  description: string | null
  specification: string | null
  qty: number | null
  deadline: string | null
  responsible: string | null
  note: string | null
  status: SourcingStatus
  createdAt: string
  createdByName: string | null
  candidates: SourcingCandidate[]
  canEdit: boolean
}

/** Nội dung phiếu tìm nguồn gửi lên. */
export interface SaveSourcingInput {
  date: string
  itemId: string | null
  description: string | null
  specification: string | null
  qty: number | null
  deadline: string | null
  responsible: string | null
  note: string | null
  candidates: { id: string | null; supplierId: string; status: CandidateStatus; note: string | null }[]
}

/** Danh sách phiếu tìm nguồn. */
export function useSourcingList() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['sourcing', companyId], queryFn: () => apiFetch<SourcingListItem[]>('/api/sourcing') })
}

/** Chi tiết phiếu tìm nguồn; null thì không tải. */
export function useSourcing(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['sourcing', companyId, id], queryFn: () => apiFetch<Sourcing>(`/api/sourcing/${id}`), enabled: !!id })
}

/** Lập, sửa, đóng, hủy phiếu tìm nguồn. */
export function useSourcingMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['sourcing'] })
  return {
    create: useMutation({ mutationFn: (input: SaveSourcingInput) => apiFetch<Sourcing>('/api/sourcing', { method: 'POST', body: input }), onSuccess }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveSourcingInput }) => apiFetch<Sourcing>(`/api/sourcing/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    close: useMutation({ mutationFn: (id: string) => apiFetch<Sourcing>(`/api/sourcing/${id}/close`, { method: 'POST' }), onSuccess }),
    cancel: useMutation({ mutationFn: (id: string) => apiFetch<Sourcing>(`/api/sourcing/${id}/cancel`, { method: 'POST' }), onSuccess }),
  }
}

/** Trạng thái RFQ; AwardPending = đã chọn thầu, chờ hội đồng duyệt kết quả (lát B3). */
export type RfqStatus = 'Draft' | 'Sent' | 'AwardPending' | 'Awarded' | 'Cancelled'

/** Nhãn trạng thái RFQ. */
export const rfqStatusLabels: Record<RfqStatus, string> = {
  Draft: 'Nháp',
  Sent: 'Đang nhận báo giá',
  AwardPending: 'Chờ duyệt kết quả',
  Awarded: 'Đã chọn thầu',
  Cancelled: 'Đã hủy',
}

/** Tông màu trạng thái RFQ. */
export const rfqStatusTone: Record<RfqStatus, 'success' | 'warning' | 'neutral'> = {
  Draft: 'neutral',
  Sent: 'warning',
  AwardPending: 'warning',
  Awarded: 'success',
  Cancelled: 'neutral',
}

/** Một dòng danh sách RFQ. */
export interface RfqListItem {
  id: string
  no: string
  date: string
  title: string
  deadline: string
  lineCount: number
  supplierCount: number
  quotedCount: number
  status: RfqStatus
  createdByName: string | null
}

/** Dòng RFQ. */
export interface RfqLine {
  id: string
  lineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  uomFactor: number
  qty: number
  sourcePrLineId: string | null
  sourcePrNo: string | null
  note: string | null
  awardedSupplierId: string | null
  awardReason: string | null
}

/** Giá một NCC chào cho một dòng. */
export interface QuotationLine {
  rfqLineId: string
  unitPrice: number
  discountPct: number
  vatCode: string | null
  leadTimeDays: number | null
  note: string | null
  baseNetPrice: number
  isLowest: boolean
}

/** NCC được mời kèm báo giá, điểm. */
export interface RfqSupplier {
  supplierId: string
  supplierCode: string
  supplierName: string
  supplierStatus: string
  hasQuoted: boolean
  quoteDate: string | null
  validUntil: string | null
  paymentTerms: string | null
  qualityScore: number
  deliveryScore: number
  paymentScore: number
  warrantyScore: number
  note: string | null
  priceScore: number
  totalScore: number
  quotedLineCount: number
  awardedAmount: number
  purchaseOrderId: string | null
  purchaseOrderNo: string | null
  lines: QuotationLine[]
  /** Bảng giá NCC nháp sinh từ kết quả thầu (lát B3b). */
  priceListId: string | null
  priceListNo: string | null
  /** Hợp đồng nguyên tắc nháp sinh từ kết quả thầu (design.md mục 8.6); hợp đồng đã hủy thì null. */
  contractId: string | null
  contractNo: string | null
}

/** Một mục hồ sơ trình kết quả chọn thầu (lát B3b); isAutomatic = hệ thống tự tính (bảng so sánh). */
export interface RfqDossierItem {
  code: string
  name: string
  isDone: boolean
  note: string | null
  isAutomatic: boolean
}

/** Hợp đồng nguyên tắc nháp vừa sinh từ kết quả chọn thầu. */
export interface CreatedContract {
  supplierId: string
  supplierCode: string
  contractId: string
  contractNo: string
}

/** Bảng giá NCC nháp vừa sinh từ kết quả chọn thầu. */
export interface CreatedPriceList {
  supplierId: string
  supplierCode: string
  priceListId: string
  priceListNo: string
}

/** Trọng số tiêu chí (%), tổng 100. */
export interface RfqWeights {
  price: number
  quality: number
  delivery: number
  payment: number
  warranty: number
}

/** Chi tiết RFQ. */
export interface Rfq {
  id: string
  no: string
  date: string
  title: string
  deadline: string
  sourcingId: string | null
  sourcingNo: string | null
  weights: RfqWeights
  note: string | null
  status: RfqStatus
  createdAt: string
  createdByName: string | null
  sentAt: string | null
  awardedAt: string | null
  minSuppliers: number
  lines: RfqLine[]
  suppliers: RfqSupplier[]
  actions: {
    canEdit: boolean
    canSend: boolean
    canQuote: boolean
    /** Chọn thầu và gửi hội đồng duyệt. */
    canAward: boolean
    canCreateOrders: boolean
    canCancel: boolean
    readOnlyReason: string | null
    /** Duyệt/từ chối được bước đang đến lượt. */
    canApprove: boolean
    /** Rút kết quả đang chờ duyệt về để chọn lại. */
    canRecall: boolean
    /** Sửa hồ sơ trình (Nháp/Đang nhận báo giá). */
    canEditDossier: boolean
    /** Người dùng có kê khai xung đột lợi ích với NCC được mời – không nhập báo giá, chọn thầu được (design.md mục 18.8). */
    conflictReason?: string | null
    /** Sinh bảng giá nháp từ kết quả đã duyệt. */
    canCreatePriceLists: boolean
    /** Sinh hợp đồng nguyên tắc nháp từ kết quả đã duyệt (cần bảng giá từ RFQ). */
    canCreateContracts: boolean
  }
  /** Giải trình khi mời ít hơn số NCC tối thiểu. */
  fewSuppliersReason: string | null
  /** Giá trị trúng thầu (sau CK, trước VAT) – xét ngưỡng duyệt. */
  awardAmount: number
  /** Người chọn thầu. */
  awardedByName: string | null
  /** Lý do hội đồng từ chối kết quả gần nhất. */
  rejectReason: string | null
  /** Lịch sử duyệt kết quả chọn thầu. */
  approvals: ApprovalStep[]
  /** Hồ sơ trình kết quả chọn thầu. */
  dossier: RfqDossierItem[]
}

/** Nội dung RFQ gửi lên. */
export interface SaveRfqInput {
  date: string
  title: string
  deadline: string
  sourcingId: string | null
  weights: RfqWeights
  note: string | null
  lines: { id: string | null; itemId: string; uomId: string; qty: number; sourcePrLineId: string | null; note: string | null }[]
  supplierIds: string[]
}

/** Báo giá của một NCC gửi lên. */
export interface SaveQuotationInput {
  quoteDate: string
  validUntil: string | null
  paymentTerms: string | null
  qualityScore: number
  deliveryScore: number
  paymentScore: number
  warrantyScore: number
  note: string | null
  lines: { rfqLineId: string; unitPrice: number; discountPct: number; vatCode: string | null; leadTimeDays: number | null; note: string | null }[]
}

/** PO vừa sinh từ kết quả chọn thầu. */
export interface CreatedOrder {
  supplierId: string
  supplierCode: string
  purchaseOrderId: string
  purchaseOrderNo: string
}

/**
 * Danh sách RFQ.
 * @param status Lọc trạng thái, nhiều trạng thái cách nhau dấu phẩy (ví dụ "Sent,Awarded"); trống = tất cả.
 */
export function useRfqs(status?: string) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['rfqs', companyId, status ?? ''],
    queryFn: () => apiFetch<RfqListItem[]>(`/api/rfqs${status ? `?status=${encodeURIComponent(status)}` : ''}`),
  })
}

/** Chi tiết RFQ; null thì không tải. */
export function useRfq(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['rfqs', companyId, 'detail', id], queryFn: () => apiFetch<Rfq>(`/api/rfqs/${id}`), enabled: !!id })
}

/** Lập, sửa, gửi, nhập báo giá, chọn thầu, sinh PO, hủy RFQ. */
export function useRfqMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['rfqs'] })
    void queryClient.invalidateQueries({ queryKey: ['sourcing'] })
  }
  return {
    create: useMutation({ mutationFn: (input: SaveRfqInput) => apiFetch<Rfq>('/api/rfqs', { method: 'POST', body: input }), onSuccess }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveRfqInput }) => apiFetch<Rfq>(`/api/rfqs/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    send: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string | null }) => apiFetch<Rfq>(`/api/rfqs/${id}/send`, { method: 'POST', body: { reason } }),
      onSuccess,
    }),
    quote: useMutation({
      mutationFn: ({ id, supplierId, input }: { id: string; supplierId: string; input: SaveQuotationInput }) =>
        apiFetch<Rfq>(`/api/rfqs/${id}/quotations/${supplierId}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    award: useMutation({
      mutationFn: ({ id, lines }: { id: string; lines: { rfqLineId: string; supplierId: string | null; reason: string | null }[] }) =>
        apiFetch<Rfq>(`/api/rfqs/${id}/award`, { method: 'POST', body: { lines } }),
      onSuccess,
    }),
    createOrders: useMutation({
      mutationFn: (id: string) => apiFetch<CreatedOrder[]>(`/api/rfqs/${id}/create-orders`, { method: 'POST' }),
      onSuccess: () => {
        onSuccess()
        void queryClient.invalidateQueries({ queryKey: ['purchasing'] })
      },
    }),
    cancel: useMutation({ mutationFn: (id: string) => apiFetch<Rfq>(`/api/rfqs/${id}/cancel`, { method: 'POST' }), onSuccess }),
    recall: useMutation({ mutationFn: (id: string) => apiFetch<Rfq>(`/api/rfqs/${id}/recall`, { method: 'POST' }), onSuccess }),
    saveDossier: useMutation({
      mutationFn: ({ id, items }: { id: string; items: { code: string; isDone: boolean; note: string | null }[] }) =>
        apiFetch<Rfq>(`/api/rfqs/${id}/dossier`, { method: 'PUT', body: { items } }),
      onSuccess,
    }),
    createPriceLists: useMutation({
      mutationFn: (id: string) => apiFetch<CreatedPriceList[]>(`/api/rfqs/${id}/create-price-lists`, { method: 'POST' }),
      onSuccess: () => {
        onSuccess()
        void queryClient.invalidateQueries({ queryKey: ['price-lists'] })
      },
    }),
    createContracts: useMutation({
      mutationFn: (id: string) => apiFetch<CreatedContract[]>(`/api/rfqs/${id}/create-contracts`, { method: 'POST' }),
      onSuccess: () => {
        onSuccess()
        void queryClient.invalidateQueries({ queryKey: ['contracts'] })
      },
    }),
    approve: useMutation({
      mutationFn: ({ id, comment }: { id: string; comment?: string | null }) =>
        apiFetch<Rfq>(`/api/rfqs/${id}/approve`, { method: 'POST', body: { comment: comment ?? null } }),
      onSuccess,
    }),
    reject: useMutation({
      mutationFn: ({ id, comment }: { id: string; comment?: string | null }) =>
        apiFetch<Rfq>(`/api/rfqs/${id}/reject`, { method: 'POST', body: { comment: comment ?? null } }),
      onSuccess,
    }),
  }
}
