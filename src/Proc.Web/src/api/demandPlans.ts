import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/** Trạng thái phiếu kế hoạch nhu cầu (design.md mục 18.25 – lát G5a). */
export type DemandPlanStatus = 'Draft' | 'Confirmed' | 'Replaced' | 'Cancelled'

/** Nhãn tiếng Việt của trạng thái. */
export const demandPlanStatusLabels: Record<DemandPlanStatus, string> = {
  Draft: 'Nháp',
  Confirmed: 'Đã chốt',
  Replaced: 'Đã thay',
  Cancelled: 'Đã hủy',
}

/** Màu đèn theo trạng thái (luôn kèm chữ – docs/ui-style.md mục 2). */
export const demandPlanStatusTone: Record<DemandPlanStatus, 'success' | 'warning' | 'error' | 'neutral'> = {
  Draft: 'neutral',
  Confirmed: 'success',
  Replaced: 'warning',
  Cancelled: 'error',
}

/** Một dòng danh sách phiếu. */
export interface DemandPlanListItem {
  id: string
  no: string
  year: number
  categoryCode: string
  categoryName: string
  lineCount: number
  status: DemandPlanStatus
  /** Số phiếu được điều chỉnh (null = phiếu gốc). */
  replacesNo: string | null
  createdByName: string | null
}

/** Một dòng mặt hàng của phiếu. */
export interface DemandPlanLine {
  id: string
  lineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  /** SL 12 tháng theo ĐVT dòng, phần tử 0 là tháng 1. */
  months: number[]
  total: number
  note: string | null
}

/** Thao tác được phép (server tính). */
export interface DemandPlanActions {
  canEdit: boolean
  canCancel: boolean
  canAdjust: boolean
  readOnlyReason: string | null
}

/** Chi tiết phiếu. */
export interface DemandPlan {
  id: string
  no: string
  year: number
  itemCategoryId: string
  categoryCode: string
  categoryName: string
  note: string | null
  status: DemandPlanStatus
  replacesId: string | null
  replacesNo: string | null
  replacedById: string | null
  replacedByNo: string | null
  confirmedAt: string | null
  confirmedByName: string | null
  cancelledOn: string | null
  cancelReason: string | null
  createdAt: string
  createdByName: string | null
  version: number
  lines: DemandPlanLine[]
  actions: DemandPlanActions
}

/** Dữ liệu lưu phiếu. */
export interface SaveDemandPlanInput {
  year: number
  itemCategoryId: string
  note: string | null
  lines: { id: string | null; itemId: string; uomId: string; months: number[]; note: string | null }[]
}

const BASE = '/api/demand-plans'

function useCompanyId(): string {
  return useAuth().workingCompany?.id ?? ''
}

/** Danh sách phiếu kế hoạch nhu cầu của công ty đang làm việc. */
export function useDemandPlans() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['demand-plans', companyId], queryFn: () => apiFetch<DemandPlanListItem[]>(BASE) })
}

/**
 * Chi tiết một phiếu.
 * @param id Id phiếu; null = không tải (đang lập mới).
 */
export function useDemandPlan(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['demand-plans', companyId, id],
    queryFn: () => apiFetch<DemandPlan>(`${BASE}/${id}`),
    enabled: id !== null,
  })
}

/** Lệnh trên phiếu: lập, sửa, chốt, điều chỉnh, hủy. */
export function useDemandPlanMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['demand-plans'] })
  return {
    create: useMutation({
      mutationFn: (input: SaveDemandPlanInput) => apiFetch<DemandPlan>(BASE, { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveDemandPlanInput }) =>
        apiFetch<DemandPlan>(`${BASE}/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    confirm: useMutation({ mutationFn: (id: string) => apiFetch<DemandPlan>(`${BASE}/${id}/confirm`, { method: 'POST' }), onSuccess }),
    adjust: useMutation({ mutationFn: (id: string) => apiFetch<DemandPlan>(`${BASE}/${id}/adjust`, { method: 'POST' }), onSuccess }),
    cancel: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) =>
        apiFetch<DemandPlan>(`${BASE}/${id}/cancel`, { method: 'POST', body: { reason } }),
      onSuccess,
    }),
  }
}

/** Một công ty (cột) của bảng tổng hợp nhu cầu (lát G5b). */
export interface DemandSummaryCompany {
  companyId: string
  code: string
  name: string
  /** Số phiếu Đã chốt của năm – đã tính vào tổng hợp. */
  confirmedPlans: number
  /** Số phiếu Nháp của năm – chưa tính. */
  draftPlans: number
}

/** Nhu cầu của một công ty cho một mặt hàng, theo ĐVT cơ sở. */
export interface DemandSummaryCell {
  companyId: string
  /** SL 12 tháng, phần tử 0 là tháng 1. */
  months: number[]
  total: number
  planNos: string[]
}

/** Một dòng tổng hợp: một mặt hàng. */
export interface DemandSummaryRow {
  itemId: string
  itemCode: string
  itemName: string
  baseUomCode: string
  categoryCode: string
  categoryName: string
  /** Chỉ các công ty có kế hoạch đã chốt cho mặt hàng. */
  companies: DemandSummaryCell[]
  months: number[]
  total: number
}

/** Bảng tổng hợp nhu cầu gộp của một năm. */
export interface DemandSummary {
  year: number
  /** true khi gộp mọi công ty (toàn tập đoàn). */
  allCompanies: boolean
  companies: DemandSummaryCompany[]
  rows: DemandSummaryRow[]
}

/**
 * Tổng hợp nhu cầu gộp (design.md mục 18.25, lát G5b): chỉ phiếu Đã chốt, SL theo ĐVT cơ sở.
 * @param year Năm kế hoạch.
 * @param categoryId Nhóm hàng (gồm nhóm con); undefined = mọi nhóm.
 * @param allCompanies true = gộp mọi công ty (cần quyền "Xem kế hoạch nhu cầu toàn tập đoàn").
 */
export function useDemandSummary(year: number, categoryId: string | undefined, allCompanies: boolean) {
  const companyId = useCompanyId()
  const params = new URLSearchParams({ year: String(year), allCompanies: String(allCompanies) })
  if (categoryId) params.set('categoryId', categoryId)
  return useQuery({
    queryKey: ['demand-plans', companyId, 'summary', year, categoryId ?? null, allCompanies],
    queryFn: () => apiFetch<DemandSummary>(`${BASE}/summary?${params}`),
  })
}

/** Kế hoạch của một công ty cho một dòng (lát G5c). */
export interface PlannedQuantityCompany {
  companyCode: string
  /** SL theo ĐVT dòng. */
  qty: number
  planNos: string[]
}

/** SL kế hoạch của một dòng RFQ / hợp đồng, theo ĐVT dòng. */
export interface PlannedQuantity {
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  /** null = không có kế hoạch đã chốt trong khoảng (hoặc ĐVT không quy đổi được). */
  qty: number | null
  companies: PlannedQuantityCompany[]
}

/** Điều kiện lấy SL kế hoạch. */
export interface PlannedQuantityQuery {
  /** Tháng đầu, yyyy-MM-01. */
  fromMonth: string
  /** Tháng cuối, yyyy-MM-01. */
  toMonth: string
  allCompanies: boolean
  lines: { itemId: string; uomId: string }[]
}

/**
 * SL kế hoạch nhu cầu (phiếu Đã chốt) của các dòng trong khoảng tháng, quy về ĐVT dòng – design.md mục 18.25, lát G5c.
 * @param query Điều kiện; null = chưa tải.
 */
export function usePlannedQuantities(query: PlannedQuantityQuery | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['demand-plans', companyId, 'quantities', query],
    queryFn: () => apiFetch<PlannedQuantity[]>(`${BASE}/quantities`, { method: 'POST', body: query }),
    enabled: query !== null && query.lines.length > 0,
  })
}
