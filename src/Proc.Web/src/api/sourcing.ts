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
    // Hủy bắt buộc lý do (design.md mục 20.2a).
    cancel: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) =>
        apiFetch<Sourcing>(`/api/sourcing/${id}/cancel`, { method: 'POST', body: { reason } }),
      onSuccess,
    }),
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
  /** Id PR nguồn – bấm đúp ô số chứng từ mở PR (design.md mục 20.1 R5). */
  sourcePrId?: string | null
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
  /** Báo giá NCC tự nộp trên Portal, đã mở (lát P3a) – không sửa giá thay, chỉ chấm điểm. */
  isFromPortal?: boolean
  /** Lần nộp cuối trên Portal (hiện cả khi còn niêm phong – chỉ giờ, không có giá). */
  portalSubmittedAt?: string | null
  /** Số lần NCC nộp trên Portal. */
  portalSubmissionCount?: number
  /** Mã biên nhận bản nộp cuối – chỉ có sau khi mở. */
  portalContentHash?: string | null
  /** Id dòng NCC được mời – chủ của file báo giá Portal (lát P3b-1). */
  invitationId?: string
  /** Số file báo giá NCC tải trên Portal (chỉ số lượng khi còn niêm phong). */
  portalAttachmentCount?: number
}

/** Bản nộp Portal còn niêm phong (xem trước giờ mở – cần quyền riêng, có ghi nhật ký). */
export interface SealedQuote {
  supplierId: string
  supplierCode: string
  supplierName: string
  sequence: number
  submittedAt: string
  submittedByEmail: string
  quoteDate: string
  validUntil: string | null
  paymentTerms: string | null
  note: string | null
  contentHash: string
  totalAmount: number
  lines: {
    rfqLineId: string
    lineNo: number
    itemCode: string
    itemName: string
    uomCode: string
    qty: number
    unitPrice: number
    discountPct: number
    vatCode: string | null
    leadTimeDays: number | null
    note: string | null
    amount: number
  }[]
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
    /** Gia hạn nhận báo giá (đang nhận báo giá – lát R7). */
    canExtendDeadline?: boolean
    /** Mở báo giá Portal niêm phong (đã qua giờ hạn nộp, có bản nộp, chưa mở – lát P3a). */
    canOpenPortalQuotes?: boolean
    /** Có quyền xem giá báo giá Portal trước giờ mở (ghi nhật ký). */
    canViewSealedQuotes?: boolean
    /** Chấm điểm báo giá đã có (cả sau hạn nộp). */
    canScore?: boolean
  }
  /** Giải trình khi mời ít hơn số NCC tối thiểu. */
  fewSuppliersReason: string | null
  /** Giải trình khi số NCC chào giá ít hơn tối thiểu lúc chọn thầu (lát R7). */
  fewQuotesReason?: string | null
  /** Lý do gia hạn nhận báo giá gần nhất (lát R7). */
  deadlineExtensionReason?: string | null
  /** Số NCC đã chào giá ít nhất một dòng. */
  quotedSupplierCount?: number
  /** Loại mua (lát Y6): Construction = A công trình (theo dòng đề nghị nguồn), Goods = B hàng hóa / dịch vụ. */
  category?: 'Construction' | 'Goods'
  /** Công trình của RFQ loại A. */
  constructionProjectId?: string | null
  /** Mã – tên công trình. */
  constructionProjectName?: string | null
  /** Biểu mẫu kết quả 3A / 3a / 3B; null = loại A chưa chọn thầu (tùy giá trị trúng thầu so với ngưỡng). */
  formCode?: string | null
  /** Ngưỡng so sánh 3 NCC loại A của công ty (VND chưa VAT). */
  comparisonThreshold?: number
  /** Lý do chọn NCC (biểu mẫu 3A). */
  selectionReason?: string | null
  /** Giờ hạn nộp "HH:mm:ss" (giờ Việt Nam, lát P3a); RFQ cũ 23:59. */
  deadlineTime?: string
  /** Thời điểm mở báo giá Portal; null = chưa mở. */
  portalQuotesOpenedAt?: string | null
  /** Người mở báo giá Portal. */
  portalQuotesOpenedByName?: string | null
  /** Số NCC đã nộp báo giá trên Portal (kể cả còn niêm phong). */
  portalSubmittedSupplierCount?: number
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
  /** Giờ hạn nộp "HH:mm" (lát P3a). */
  deadlineTime: string
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

/**
 * Xem giá báo giá Portal còn niêm phong (lát P3a) – cần quyền "Xem báo giá Portal trước giờ mở"; mỗi lần gọi máy chủ ghi nhật ký truy cập,
 * nên chỉ gọi khi người dùng bấm xem (không tự tải).
 * @param id RFQ.
 * @returns Bản nộp cuối của từng NCC.
 */
export function fetchSealedQuotes(id: string): Promise<SealedQuote[]> {
  return apiFetch<SealedQuote[]>(`/api/rfqs/${id}/sealed-quotes`)
}

/** Một mục hỏi đáp làm rõ của RFQ phía nội bộ (lát P3b-2): câu hỏi NCC (thấy NCC, người hỏi) hoặc thông báo làm rõ. */
export interface RfqClarification {
  id: string
  isNotice: boolean
  supplierId: string | null
  supplierCode: string | null
  supplierName: string | null
  question: string | null
  askedAt: string | null
  askedByEmail: string | null
  /** Câu trả lời / nội dung thông báo; null = chưa trả lời. */
  answer: string | null
  answeredAt: string | null
  answeredByName: string | null
}

/**
 * Hỏi đáp làm rõ của RFQ (lát P3b-2); null thì không tải.
 * @param id RFQ.
 */
export function useRfqClarifications(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['rfqs', companyId, 'clarifications', id],
    queryFn: () => apiFetch<RfqClarification[]>(`/api/rfqs/${id}/clarifications`),
    enabled: !!id,
  })
}

/**
 * Trả lời câu hỏi NCC (chỉ NCC hỏi thấy) hoặc đăng thông báo làm rõ (mọi NCC được mời thấy) – lát P3b-2.
 * Kết quả (danh sách mới) ghi thẳng vào bộ nhớ đệm của hỏi đáp RFQ đó.
 */
export function useRfqClarificationMutations() {
  const queryClient = useQueryClient()
  const companyId = useCompanyId()
  const onSuccess = (data: RfqClarification[], vars: { rfqId: string }) =>
    queryClient.setQueryData(['rfqs', companyId, 'clarifications', vars.rfqId], data)
  return {
    answer: useMutation({
      mutationFn: ({ rfqId, id, answer }: { rfqId: string; id: string; answer: string }) =>
        apiFetch<RfqClarification[]>(`/api/rfqs/${rfqId}/clarifications/${id}/answer`, { method: 'POST', body: { answer } }),
      onSuccess,
    }),
    announce: useMutation({
      mutationFn: ({ rfqId, text }: { rfqId: string; text: string }) =>
        apiFetch<RfqClarification[]>(`/api/rfqs/${rfqId}/clarifications`, { method: 'POST', body: { text } }),
      onSuccess,
    }),
  }
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
      mutationFn: ({ id, lines, fewQuotesReason, selectionReason }: {
        id: string; lines: { rfqLineId: string; supplierId: string | null; reason: string | null }[]; fewQuotesReason?: string | null
        selectionReason?: string | null
      }) => apiFetch<Rfq>(`/api/rfqs/${id}/award`, {
        method: 'POST', body: { lines, fewQuotesReason: fewQuotesReason ?? null, selectionReason: selectionReason ?? null },
      }),
      onSuccess,
    }),
    // Gia hạn nhận báo giá có lý do (lát R7, design.md mục 18.26).
    extendDeadline: useMutation({
      mutationFn: ({ id, deadline, deadlineTime, reason }: { id: string; deadline: string; deadlineTime: string; reason: string }) =>
        apiFetch<Rfq>(`/api/rfqs/${id}/extend-deadline`, { method: 'POST', body: { deadline, deadlineTime, reason } }),
      onSuccess,
    }),
    // Lát P3a: mở báo giá Portal niêm phong sau giờ hạn nộp; chấm điểm báo giá đã có (không đổi giá).
    openPortalQuotes: useMutation({
      mutationFn: (id: string) => apiFetch<Rfq>(`/api/rfqs/${id}/open-portal-quotes`, { method: 'POST' }),
      onSuccess,
    }),
    score: useMutation({
      mutationFn: ({ id, supplierId, scores }: {
        id: string; supplierId: string
        scores: { qualityScore: number; deliveryScore: number; paymentScore: number; warrantyScore: number }
      }) => apiFetch<Rfq>(`/api/rfqs/${id}/scores/${supplierId}`, { method: 'PUT', body: scores }),
      onSuccess,
    }),
    createOrders: useMutation({
      mutationFn: (id: string) => apiFetch<CreatedOrder[]>(`/api/rfqs/${id}/create-orders`, { method: 'POST' }),
      onSuccess: () => {
        onSuccess()
        void queryClient.invalidateQueries({ queryKey: ['purchasing'] })
      },
    }),
    cancel: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) => apiFetch<Rfq>(`/api/rfqs/${id}/cancel`, { method: 'POST', body: { reason } }),
      onSuccess,
    }),
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
