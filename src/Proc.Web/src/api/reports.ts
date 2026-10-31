import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Kiểu dữ liệu và hàm gọi API báo cáo kho (khớp Proc.Application.Reports – StockReports.cs, design.md mục 11.2).
 */

const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/** Một dòng hàng chậm luân chuyển: kho × mặt hàng còn tồn, lâu không xuất. */
export interface SlowMovingRow {
  warehouseId: string
  warehouseCode: string
  warehouseName: string
  itemId: string
  itemCode: string
  itemName: string
  categoryCode: string
  categoryName: string
  uomCode: string
  qty: number
  amount: number
  firstInDate: string | null
  lastInDate: string | null
  lastOutDate: string | null
  idleDays: number
}

/** Báo cáo hàng chậm luân chuyển. */
export interface SlowMovingResult {
  asOf: string
  days: number
  items: SlowMovingRow[]
}

/** Một lô còn tồn sắp (hoặc đã) hết hạn. */
export interface ExpiringRow {
  warehouseId: string
  warehouseCode: string
  warehouseName: string
  itemId: string
  itemCode: string
  itemName: string
  categoryCode: string
  uomCode: string
  lotNo: string
  expiryDate: string
  daysLeft: number
  qty: number
  amount: number
}

/** Báo cáo hàng sắp hết hạn. */
export interface ExpiringResult {
  asOf: string
  days: number
  items: ExpiringRow[]
}

/** Tồn một mặt hàng tại một công ty – chỉ số lượng. */
export interface GroupStockRow {
  companyId: string
  companyCode: string
  companyName: string
  isCurrent: boolean
  mainQty: number
  mainAvailable: number
  subQty: number
  totalQty: number
}

/** Tồn tập đoàn của một mặt hàng. */
export interface GroupStockResult {
  itemId: string
  itemCode: string
  itemName: string
  uomCode: string
  minStock: number | null
  items: GroupStockRow[]
}

/** Điều kiện chung của báo cáo kho: kho, nhóm hàng (gồm nhóm con), số ngày. */
export interface StockReportFilter {
  warehouseId?: string
  categoryId?: string
  days: number
}

function toQuery(filter: StockReportFilter) {
  const params = new URLSearchParams({ days: String(filter.days) })
  if (filter.warehouseId) params.set('warehouseId', filter.warehouseId)
  if (filter.categoryId) params.set('categoryId', filter.categoryId)
  return params.toString()
}

/**
 * Hàng chậm luân chuyển: còn tồn mà không xuất từ filter.days ngày.
 * @param filter Kho, nhóm hàng, số ngày không xuất.
 */
export function useSlowMoving(filter: StockReportFilter) {
  const companyId = useCompanyId()
  const query = toQuery(filter)
  return useQuery({
    queryKey: ['reports', companyId, 'slow-moving', query],
    queryFn: () => apiFetch<SlowMovingResult>(`/api/reports/slow-moving?${query}`),
    placeholderData: keepPreviousData,
  })
}

/**
 * Lô sắp hết hạn trong filter.days ngày tới (gồm lô đã hết hạn).
 * @param filter Kho, nhóm hàng, số ngày tới.
 */
export function useExpiring(filter: StockReportFilter) {
  const companyId = useCompanyId()
  const query = toQuery(filter)
  return useQuery({
    queryKey: ['reports', companyId, 'expiring', query],
    queryFn: () => apiFetch<ExpiringResult>(`/api/reports/expiring?${query}`),
    placeholderData: keepPreviousData,
  })
}

/**
 * Tồn một mặt hàng ở mọi công ty (cần quyền xem toàn tập đoàn).
 * @param itemId Mặt hàng; trống thì không tải.
 */
export function useGroupStock(itemId: string | undefined) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['reports', companyId, 'group-stock', itemId],
    queryFn: () => apiFetch<GroupStockResult>(`/api/reports/group-stock?itemId=${itemId}`),
    enabled: !!itemId,
  })
}
