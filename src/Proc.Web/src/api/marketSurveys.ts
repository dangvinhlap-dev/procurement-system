import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/** Trạng thái phiếu khảo sát giá thị trường (design.md mục 18.25 – lát G4a). */
export type MarketSurveyStatus = 'Draft' | 'Confirmed' | 'Cancelled'

/** Nhãn tiếng Việt của trạng thái. */
export const marketSurveyStatusLabels: Record<MarketSurveyStatus, string> = {
  Draft: 'Nháp',
  Confirmed: 'Đã chốt',
  Cancelled: 'Đã hủy',
}

/** Màu đèn theo trạng thái (luôn kèm chữ – docs/ui-style.md mục 2). */
export const marketSurveyStatusTone: Record<MarketSurveyStatus, 'success' | 'warning' | 'error' | 'neutral'> = {
  Draft: 'neutral',
  Confirmed: 'success',
  Cancelled: 'error',
}

/** Một dòng danh sách phiếu. */
export interface MarketSurveyListItem {
  id: string
  no: string
  surveyDate: string
  categoryCode: string
  categoryName: string
  lineCount: number
  status: MarketSurveyStatus
  createdByName: string | null
}

/** Một dòng giá của phiếu. */
export interface MarketSurveyLine {
  id: string
  lineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  supplierId: string | null
  supplierCode: string | null
  supplierName: string | null
  sourceName: string | null
  unitPrice: number
  /** Giá quy về ĐVT cơ sở. */
  baseUnitPrice: number
  note: string | null
}

/** Thao tác được phép (server tính). */
export interface MarketSurveyActions {
  canEdit: boolean
  canCancel: boolean
  readOnlyReason: string | null
}

/** Chi tiết phiếu. */
export interface MarketSurvey {
  id: string
  no: string
  surveyDate: string
  itemCategoryId: string
  categoryCode: string
  categoryName: string
  note: string | null
  status: MarketSurveyStatus
  confirmedAt: string | null
  confirmedByName: string | null
  cancelledOn: string | null
  cancelReason: string | null
  createdAt: string
  createdByName: string | null
  lines: MarketSurveyLine[]
  actions: MarketSurveyActions
}

/** Dữ liệu lưu phiếu. */
export interface SaveMarketSurveyInput {
  surveyDate: string
  itemCategoryId: string
  note: string | null
  lines: {
    id: string | null
    itemId: string
    uomId: string
    supplierId: string | null
    sourceName: string | null
    unitPrice: number
    note: string | null
  }[]
}

/** Một giá thị trường trong ngân hàng giá (phiếu đã chốt, quy về ĐVT cơ sở). */
export interface MarketPrice {
  itemId: string
  surveyId: string
  surveyNo: string
  surveyDate: string
  source: string
  baseUnitPrice: number
}

const BASE = '/api/market-price-surveys'

function useCompanyId(): string {
  return useAuth().workingCompany?.id ?? ''
}

/** Danh sách phiếu khảo sát giá của tổng công ty. */
export function useMarketSurveys() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['market-surveys', companyId], queryFn: () => apiFetch<MarketSurveyListItem[]>(BASE) })
}

/**
 * Chi tiết một phiếu.
 * @param id Id phiếu; null = không tải (đang lập mới).
 */
export function useMarketSurvey(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['market-surveys', companyId, id],
    queryFn: () => apiFetch<MarketSurvey>(`${BASE}/${id}`),
    enabled: id !== null,
  })
}

/**
 * Giá thị trường của các mặt hàng (phiếu đã chốt trong số ngày gần nhất).
 * @param itemIds Mặt hàng; rỗng thì không tải.
 * @param days Số ngày nhìn lại (mặc định 180).
 */
export function useMarketPrices(itemIds: string[], days = 180) {
  const companyId = useCompanyId()
  const params = new URLSearchParams({ days: String(days) })
  for (const id of [...new Set(itemIds)].sort()) params.append('itemIds', id)
  const query = params.toString()
  return useQuery({
    queryKey: ['market-surveys', companyId, 'prices', query],
    queryFn: () => apiFetch<MarketPrice[]>(`${BASE}/prices?${query}`),
    enabled: itemIds.length > 0,
  })
}

/** Lệnh trên phiếu: lập, sửa, chốt, hủy. */
export function useMarketSurveyMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['market-surveys'] })
  return {
    create: useMutation({
      mutationFn: (input: SaveMarketSurveyInput) => apiFetch<MarketSurvey>(BASE, { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveMarketSurveyInput }) =>
        apiFetch<MarketSurvey>(`${BASE}/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    confirm: useMutation({ mutationFn: (id: string) => apiFetch<MarketSurvey>(`${BASE}/${id}/confirm`, { method: 'POST' }), onSuccess }),
    cancel: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) =>
        apiFetch<MarketSurvey>(`${BASE}/${id}/cancel`, { method: 'POST', body: { reason } }),
      onSuccess,
    }),
  }
}
