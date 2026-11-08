import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { RebatePeriodStatus, RebateReceiptForm } from './contracts'
import type { ApprovalStep } from './purchasing'

/** Trạng thái thỏa thuận chiết khấu sản lượng gộp (design.md mục 18.25 – lát G6a). */
export type VolumeRebateStatus = 'Draft' | 'Submitted' | 'Active' | 'Cancelled'

/** Kỳ tính bậc (lũy kế bắt đầu lại mỗi kỳ). */
export type VolumeRebatePeriod = 'Month' | 'Quarter' | 'Year' | 'Whole'

/** Cơ sở tính bậc. */
export type VolumeRebateBasis = 'Quantity' | 'Amount'

/** Tình trạng kỳ: chưa tới, đang chạy, đã hết. */
export type VolumeRebatePeriodState = 'Future' | 'Open' | 'Ended'

/** Tình trạng so bậc mục tiêu. */
export type VolumeRebateTargetStatus = 'None' | 'NotStarted' | 'Reached' | 'OnTrack' | 'AtRisk' | 'Missed'

/** Nhãn tiếng Việt của trạng thái. */
export const volumeRebateStatusLabels: Record<VolumeRebateStatus, string> = {
  Draft: 'Nháp',
  Submitted: 'Chờ duyệt',
  Active: 'Hiệu lực',
  Cancelled: 'Đã hủy',
}

/** Màu đèn theo trạng thái (luôn kèm chữ – docs/ui-style.md mục 2). */
export const volumeRebateStatusTone: Record<VolumeRebateStatus, 'success' | 'warning' | 'error' | 'neutral'> = {
  Draft: 'neutral',
  Submitted: 'warning',
  Active: 'success',
  Cancelled: 'error',
}

/** Nhãn kỳ tính bậc. */
export const volumeRebatePeriodLabels: Record<VolumeRebatePeriod, string> = {
  Month: 'Tháng',
  Quarter: 'Quý',
  Year: 'Năm',
  Whole: 'Cả thỏa thuận',
}

/** Nhãn cơ sở tính bậc. */
export const volumeRebateBasisLabels: Record<VolumeRebateBasis, string> = {
  Quantity: 'Số lượng gộp',
  Amount: 'Doanh số gộp (VND)',
}

/** Nhãn tình trạng kỳ. */
export const volumeRebateStateLabels: Record<VolumeRebatePeriodState, string> = {
  Future: 'Chưa tới kỳ',
  Open: 'Đang trong kỳ',
  Ended: 'Đã hết kỳ',
}

/** Nhãn tình trạng so bậc mục tiêu. */
export const volumeRebateTargetLabels: Record<VolumeRebateTargetStatus, string> = {
  None: 'Không đặt mục tiêu',
  NotStarted: 'Chưa bắt đầu',
  Reached: 'Đã đạt bậc mục tiêu',
  OnTrack: 'Dự báo đạt',
  AtRisk: 'Nguy cơ hụt bậc',
  Missed: 'Hụt bậc mục tiêu',
}

/** Màu đèn tình trạng so bậc mục tiêu. */
export const volumeRebateTargetTone: Record<VolumeRebateTargetStatus, 'success' | 'warning' | 'error' | 'neutral'> = {
  None: 'neutral',
  NotStarted: 'neutral',
  Reached: 'success',
  OnTrack: 'success',
  AtRisk: 'warning',
  Missed: 'error',
}

/** Một dòng danh sách thỏa thuận. */
export interface VolumeRebateListItem {
  id: string
  no: string
  title: string
  supplierCode: string
  supplierName: string
  categoryCode: string
  categoryName: string
  validFrom: string
  validTo: string
  period: VolumeRebatePeriod
  basis: VolumeRebateBasis
  companyCodes: string
  lineCount: number
  tierCount: number
  status: VolumeRebateStatus
  createdByName: string | null
}

/** Một dòng mặt hàng. */
export interface VolumeRebateLine {
  id: string
  lineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  note: string | null
}

/** Một bậc chiết khấu. */
export interface VolumeRebateTier {
  tierNo: number
  fromValue: number
  percent: number
}

/** Công ty áp dụng. */
export interface VolumeRebateCompany {
  id: string
  code: string
  name: string
}

/** Thao tác được phép (server tính). */
export interface VolumeRebateActions {
  canEdit: boolean
  canApprove: boolean
  canRecall: boolean
  canCancel: boolean
  canCopy: boolean
  readOnlyReason: string | null
}

/** Chi tiết thỏa thuận. */
export interface VolumeRebate {
  id: string
  no: string
  companyCode: string
  title: string
  supplierId: string
  supplierCode: string
  supplierName: string
  itemCategoryId: string
  categoryCode: string
  categoryName: string
  validFrom: string
  validTo: string
  period: VolumeRebatePeriod
  basis: VolumeRebateBasis
  targetTierNo: number | null
  companies: VolumeRebateCompany[]
  note: string | null
  status: VolumeRebateStatus
  rejectReason: string | null
  approvedAt: string | null
  cancelledOn: string | null
  cancelReason: string | null
  createdAt: string
  createdByName: string | null
  lines: VolumeRebateLine[]
  tiers: VolumeRebateTier[]
  approvals: ApprovalStep[]
  actions: VolumeRebateActions
}

/** Dữ liệu lưu thỏa thuận. */
export interface SaveVolumeRebateInput {
  title: string
  supplierId: string
  itemCategoryId: string
  validFrom: string
  validTo: string
  period: VolumeRebatePeriod
  basis: VolumeRebateBasis
  targetTierNo: number | null
  companyIds: string[]
  note: string | null
  lines: { id: string | null; itemId: string; uomId: string; note: string | null }[]
  tiers: { fromValue: number; percent: number }[]
}

/** Phần của một công ty trong kỳ. */
export interface VolumeRebateShare {
  companyId: string
  companyCode: string
  quantity: number
  amount: number
  sharePercent: number
  rebateShare: number
  /** Thực nhận được chia (lát G6b). */
  receivedShare: number
}

/** Lũy kế một kỳ tính bậc. */
export interface VolumeRebatePeriodRow {
  periodFrom: string
  periodTo: string
  state: VolumeRebatePeriodState
  volume: number
  amount: number
  tierNo: number | null
  percent: number
  rebateAmount: number
  nextTierNo: number | null
  nextFromValue: number | null
  shortfall: number | null
  forecast: number | null
  targetFromValue: number | null
  targetStatus: VolumeRebateTargetStatus
  isNearNextTier: boolean
  companies: VolumeRebateShare[]
  /** Hạn ghi nhận thực nhận = cuối kỳ + 30 ngày. */
  dueDate: string
  receivedAmount: number
  shortAmount: number
  receiptStatus: RebatePeriodStatus
}

/** Phần thực nhận của một công ty trong một lần ghi nhận (chia theo tỷ lệ doanh số kỳ lúc ghi). */
export interface VolumeRebateReceiptShare {
  companyId: string
  companyCode: string
  purchaseAmount: number
  sharePercent: number
  amount: number
}

/** Một lần ghi nhận chiết khấu sản lượng gộp thực nhận (lát G6b). */
export interface VolumeRebateReceipt {
  id: string
  agreementId: string
  periodFrom: string
  periodTo: string
  receivedOn: string
  amount: number
  form: RebateReceiptForm
  refNo: string | null
  note: string | null
  isCancelled: boolean
  cancelReason: string | null
  createdAt: string
  createdByName: string | null
  shares: VolumeRebateReceiptShare[]
}

/** Dữ liệu ghi nhận thực nhận. */
export interface SaveVolumeRebateReceiptInput {
  periodFrom: string
  receivedOn: string
  amount: number
  form: RebateReceiptForm
  refNo: string | null
  note: string | null
}

/** Lũy kế mọi kỳ của một thỏa thuận. */
export interface VolumeRebateProgress {
  agreementId: string
  basis: VolumeRebateBasis
  uomCode: string | null
  periods: VolumeRebatePeriodRow[]
  graceDays: number
  /** Được ghi/hủy thực nhận (tổng công ty, thỏa thuận hiệu lực, có quyền lập). */
  canRecord: boolean
  receipts: VolumeRebateReceipt[]
}

/** Một dòng màn hình theo dõi bậc (kỳ hiện tại của thỏa thuận hiệu lực). */
export interface VolumeRebateTrackingRow {
  agreementId: string
  no: string
  title: string
  supplierCode: string
  supplierName: string
  basis: VolumeRebateBasis
  uomCode: string | null
  targetTierNo: number | null
  period: VolumeRebatePeriodRow
  /** Số kỳ quá hạn ghi nhận mà còn thiếu thực nhận. */
  unreceivedPeriods: number
  unreceivedAmount: number
}

const BASE = '/api/volume-rebates'

function useCompanyId(): string {
  return useAuth().workingCompany?.id ?? ''
}

/** Danh sách thỏa thuận (tổng công ty: mọi thỏa thuận; công ty thành viên: thỏa thuận hiệu lực áp cho mình). */
export function useVolumeRebates() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['volume-rebates', companyId], queryFn: () => apiFetch<VolumeRebateListItem[]>(BASE) })
}

/**
 * Chi tiết một thỏa thuận.
 * @param id Id; null = không tải (đang lập mới).
 */
export function useVolumeRebate(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['volume-rebates', companyId, id], queryFn: () => apiFetch<VolumeRebate>(`${BASE}/${id}`), enabled: id !== null })
}

/**
 * Lũy kế theo kỳ của một thỏa thuận.
 * @param id Id; null = không tải.
 * @param nearPercent X% cảnh báo sắp chạm bậc.
 */
export function useVolumeRebateProgress(id: string | null, nearPercent: number) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['volume-rebates', companyId, id, 'progress', nearPercent],
    queryFn: () => apiFetch<VolumeRebateProgress>(`${BASE}/${id}/progress?nearPercent=${nearPercent}`),
    enabled: id !== null,
  })
}

/**
 * Theo dõi bậc: kỳ hiện tại của mọi thỏa thuận hiệu lực.
 * @param nearPercent X% cảnh báo sắp chạm bậc.
 * @param onlyAlerts Chỉ dòng có cảnh báo.
 */
export function useVolumeRebateTracking(nearPercent: number, onlyAlerts: boolean) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['volume-rebates', companyId, 'tracking', nearPercent, onlyAlerts],
    queryFn: () => apiFetch<VolumeRebateTrackingRow[]>(`${BASE}/tracking?nearPercent=${nearPercent}&onlyAlerts=${onlyAlerts}`),
  })
}

/** Lệnh trên thỏa thuận: lập, sửa, gửi, rút, duyệt, từ chối, hủy, chép. */
export function useVolumeRebateMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['volume-rebates'] })
    void queryClient.invalidateQueries({ queryKey: ['purchasing'] })
    void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
  }
  const post = (action: string) => (id: string) => apiFetch<VolumeRebate>(`${BASE}/${id}/${action}`, { method: 'POST' })
  const decide = (action: 'approve' | 'reject') => ({ id, comment }: { id: string; comment?: string }) =>
    apiFetch<VolumeRebate>(`${BASE}/${id}/${action}`, { method: 'POST', body: { comment: comment ?? null } })
  return {
    create: useMutation({ mutationFn: (input: SaveVolumeRebateInput) => apiFetch<VolumeRebate>(BASE, { method: 'POST', body: input }), onSuccess }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveVolumeRebateInput }) => apiFetch<VolumeRebate>(`${BASE}/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    submit: useMutation({ mutationFn: ({ id }: { id: string }) => post('submit')(id), onSuccess }),
    recall: useMutation({ mutationFn: post('recall'), onSuccess }),
    copy: useMutation({ mutationFn: post('copy'), onSuccess }),
    approve: useMutation({ mutationFn: decide('approve'), onSuccess }),
    reject: useMutation({ mutationFn: decide('reject'), onSuccess }),
    cancel: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) => apiFetch<VolumeRebate>(`${BASE}/${id}/cancel`, { method: 'POST', body: { reason } }),
      onSuccess,
    }),
    /** Ghi thực nhận cho kỳ đã kết thúc (lát G6b); trả bản ghi để tải đính kèm theo Id. */
    recordReceipt: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveVolumeRebateReceiptInput }) =>
        apiFetch<VolumeRebateReceipt>(`${BASE}/${id}/receipts`, { method: 'POST', body: input }),
      onSuccess,
    }),
    cancelReceipt: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) =>
        apiFetch<VolumeRebateReceipt>(`${BASE}/receipts/${id}/cancel`, { method: 'POST', body: { reason } }),
      onSuccess,
    }),
  }
}
