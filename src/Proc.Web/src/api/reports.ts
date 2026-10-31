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

/** Một dòng PR chưa xử lý: chờ duyệt, hoặc đã duyệt chưa đặt đủ. */
export interface PendingRequestLine {
  requestId: string
  requestNo: string
  date: string
  department: string | null
  status: 'Submitted' | 'Approved'
  priority: 'Low' | 'Normal' | 'Urgent'
  lineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uomCode: string
  qty: number
  orderedQty: number
  remainingQty: number
  estAmount: number
  neededDate: string | null
  overdueDays: number
}

/** Cách nhóm báo cáo mua hàng. */
export type PurchaseSpendGroup = 'supplier' | 'category' | 'item' | 'company'

/** Một dòng mua hàng theo nhóm. */
export interface PurchaseSpendRow {
  key: string
  code: string
  name: string
  receiptCount: number
  receiptAmount: number
  returnAmount: number
  netAmount: number
  sharePct: number
}

/** Báo cáo mua hàng theo nhóm. */
export interface PurchaseSpendResult {
  groupBy: PurchaseSpendGroup
  allCompanies: boolean
  totalNet: number
  items: PurchaseSpendRow[]
}

/** Chỉ số đánh giá một NCC. */
export interface SupplierEvaluationRow {
  supplierId: string
  supplierCode: string
  supplierName: string
  receiptCount: number
  receiptAmount: number
  returnAmount: number
  returnPct: number | null
  deliveryLines: number
  onTimeLines: number
  onTimePct: number | null
  avgLateDays: number | null
}

/** Một dòng RFQ đã chọn thầu và tiền tiết kiệm. */
export interface SourcingSavingRow {
  rfqId: string
  rfqNo: string
  title: string
  awardDate: string
  companyCode: string
  itemCode: string
  itemName: string
  uomCode: string
  qty: number
  supplierCode: string
  supplierName: string
  quoteCount: number
  awardedPrice: number
  minPrice: number
  avgPrice: number
  maxPrice: number
  awardedAmount: number
  savingVsAvg: number
  savingVsMax: number
  isLowest: boolean
  awardReason: string | null
}

/** Khoảng ngày và phạm vi của báo cáo mua hàng. */
export interface PurchaseReportPeriod {
  from: string
  to: string
  allCompanies: boolean
}

const periodQuery = (p: PurchaseReportPeriod) => new URLSearchParams({ from: p.from, to: p.to, allCompanies: String(p.allCompanies) })

/** Dòng PR chưa xử lý của công ty đang làm việc. */
export function usePendingRequests() {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['reports', companyId, 'pending-requests'],
    queryFn: () => apiFetch<PendingRequestLine[]>('/api/reports/pending-requests'),
  })
}

/**
 * Mua hàng thuần theo nhóm.
 * @param period Khoảng ngày, phạm vi.
 * @param groupBy Cách nhóm.
 */
export function usePurchaseSpend(period: PurchaseReportPeriod, groupBy: PurchaseSpendGroup) {
  const companyId = useCompanyId()
  const params = periodQuery(period)
  params.set('groupBy', groupBy)
  const query = params.toString()
  return useQuery({
    queryKey: ['reports', companyId, 'purchase-spend', query],
    queryFn: () => apiFetch<PurchaseSpendResult>(`/api/reports/purchase-spend?${query}`),
    placeholderData: keepPreviousData,
  })
}

/**
 * Đánh giá NCC trong khoảng ngày.
 * @param period Khoảng ngày, phạm vi.
 */
export function useSupplierEvaluation(period: PurchaseReportPeriod) {
  const companyId = useCompanyId()
  const query = periodQuery(period).toString()
  return useQuery({
    queryKey: ['reports', companyId, 'supplier-evaluation', query],
    queryFn: () => apiFetch<SupplierEvaluationRow[]>(`/api/reports/supplier-evaluation?${query}`),
    placeholderData: keepPreviousData,
  })
}

/**
 * Tiết kiệm từ đấu thầu theo ngày chọn thầu.
 * @param period Khoảng ngày, phạm vi.
 */
export function useSourcingSavings(period: PurchaseReportPeriod) {
  const companyId = useCompanyId()
  const query = periodQuery(period).toString()
  return useQuery({
    queryKey: ['reports', companyId, 'sourcing-savings', query],
    queryFn: () => apiFetch<SourcingSavingRow[]>(`/api/reports/sourcing-savings?${query}`),
    placeholderData: keepPreviousData,
  })
}
