import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Kiểu dữ liệu và hàm gọi API bảng giá NCC, so sánh giá (khớp Proc.Application.Pricing – SupplierPrices.cs).
 */

const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/** Trạng thái bảng giá NCC. */
export type PriceListStatus = 'Draft' | 'Active' | 'Inactive'

/** Nhãn trạng thái bảng giá. */
export const priceListStatusLabels: Record<PriceListStatus, string> = { Draft: 'Nháp', Active: 'Hiệu lực', Inactive: 'Ngừng' }

/** Tông màu trạng thái bảng giá. */
export const priceListStatusTone: Record<PriceListStatus, 'success' | 'warning' | 'neutral'> = {
  Draft: 'warning',
  Active: 'success',
  Inactive: 'neutral',
}

/** Một dòng danh sách bảng giá. */
export interface PriceListListItem {
  id: string
  no: string
  title: string
  companyCode: string
  isGroupWide: boolean
  supplierCode: string
  supplierName: string
  validFrom: string
  validTo: string | null
  status: PriceListStatus
  lineCount: number
  isOwn: boolean
}

/** Một dòng bảng giá. */
export interface PriceListLine {
  id: string
  lineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  uomFactor: number
  minQty: number
  unitPrice: number
  discountPct: number
  vatCode: string | null
  baseNetPrice: number
  note: string | null
}

/** Chi tiết bảng giá. */
export interface PriceList {
  id: string
  no: string
  title: string
  companyId: string
  companyCode: string
  isGroupWide: boolean
  supplierId: string
  supplierCode: string
  supplierName: string
  validFrom: string
  validTo: string | null
  currencyCode: string
  deliveryTerms: string | null
  note: string | null
  status: PriceListStatus
  createdAt: string
  createdByName: string | null
  activatedAt: string | null
  lines: PriceListLine[]
  actions: { canEdit: boolean; canActivate: boolean; canDeactivate: boolean; readOnlyReason: string | null }
}

/** Nội dung bảng giá gửi lên. */
export interface SavePriceListInput {
  supplierId: string
  title: string
  isGroupWide: boolean
  validFrom: string
  validTo: string | null
  deliveryTerms: string | null
  note: string | null
  lines: { id: string | null; itemId: string; uomId: string; minQty: number; unitPrice: number; discountPct: number; vatCode: string | null; note: string | null }[]
}

/** Một bậc giá gợi ý cho PO. */
export interface ResolvedSupplierPrice {
  itemId: string
  uomId: string
  uomFactor: number
  minQty: number
  unitPrice: number
  discountPct: number
  vatCode: string | null
  priceListNo: string
}

/** Danh sách bảng giá (của công ty và toàn tập đoàn). */
export function usePriceLists() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['price-lists', companyId], queryFn: () => apiFetch<PriceListListItem[]>('/api/price-lists') })
}

/** Chi tiết bảng giá; null thì không tải (đang lập mới). */
export function usePriceList(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['price-lists', companyId, id], queryFn: () => apiFetch<PriceList>(`/api/price-lists/${id}`), enabled: !!id })
}

/** Lập, sửa, đưa vào hiệu lực, ngừng bảng giá. */
export function usePriceListMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['price-lists'] })
  return {
    create: useMutation({ mutationFn: (input: SavePriceListInput) => apiFetch<PriceList>('/api/price-lists', { method: 'POST', body: input }), onSuccess }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SavePriceListInput }) => apiFetch<PriceList>(`/api/price-lists/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    activate: useMutation({ mutationFn: (id: string) => apiFetch<PriceList>(`/api/price-lists/${id}/activate`, { method: 'POST' }), onSuccess }),
    deactivate: useMutation({ mutationFn: (id: string) => apiFetch<PriceList>(`/api/price-lists/${id}/deactivate`, { method: 'POST' }), onSuccess }),
  }
}

/**
 * Bậc giá gợi ý từ bảng giá hiệu lực của NCC vào ngày đặt hàng (PO).
 * @param supplierId NCC; trống thì không tải.
 * @param date Ngày yyyy-MM-dd.
 * @param itemIds Mặt hàng trên phiếu.
 */
export function useSupplierPrices(supplierId: string | undefined, date: string | undefined, itemIds: string[]) {
  const companyId = useCompanyId()
  const params = new URLSearchParams({ supplierId: supplierId ?? '', date: date ?? '' })
  for (const id of [...new Set(itemIds)].sort()) params.append('itemIds', id)
  const query = params.toString()
  return useQuery({
    queryKey: ['price-lists', companyId, 'resolve', query],
    queryFn: () => apiFetch<ResolvedSupplierPrice[]>(`/api/price-lists/resolve?${query}`),
    enabled: !!supplierId && !!date && itemIds.length > 0,
    placeholderData: keepPreviousData,
  })
}

/**
 * Chọn bậc giá cho một dòng PO: cùng ĐVT, bậc SL lớn nhất ≤ SL đặt. Không có bậc cùng ĐVT thì quy từ bậc theo ĐVT khác
 * (giá cơ sở × hệ số ĐVT dòng), vẫn chọn bậc lớn nhất có SL quy đổi ≤ SL đặt.
 * Ví dụ bậc 0 cái 50.000, bậc 100 cái 48.000: đặt 120 cái → 48.000; đặt 2 thùng 12 cái (không có bậc theo thùng) → 600.000/thùng.
 * @param tiers Các bậc của mặt hàng.
 * @param uomId ĐVT dòng.
 * @param uomFactor Hệ số ĐVT dòng.
 * @param qty SL đặt.
 * @returns Đơn giá, % CK, thuế suất gợi ý; undefined nếu không có bậc nào.
 */
export function pickSupplierPrice(tiers: ResolvedSupplierPrice[], uomId: string, uomFactor: number, qty: number) {
  const same = tiers.filter((t) => t.uomId === uomId && t.minQty <= qty).sort((a, b) => b.minQty - a.minQty)[0]
  if (same) return { unitPrice: same.unitPrice, discountPct: same.discountPct, vatCode: same.vatCode, priceListNo: same.priceListNo }
  const baseQty = qty * uomFactor
  const other = tiers.filter((t) => t.minQty * t.uomFactor <= baseQty).sort((a, b) => b.minQty * b.uomFactor - a.minQty * a.uomFactor)[0]
  if (!other) return undefined
  const unitPrice = Math.round((other.unitPrice / other.uomFactor) * uomFactor * 1_000_000) / 1_000_000
  return { unitPrice, discountPct: other.discountPct, vatCode: other.vatCode, priceListNo: other.priceListNo }
}

/** Giá bảng giá đang hiệu lực khi so sánh. */
export interface ComparePriceListRow {
  companyCode: string
  isGroupWide: boolean
  supplierCode: string
  supplierName: string
  priceListNo: string
  validFrom: string
  validTo: string | null
  uomCode: string
  minQty: number
  unitPrice: number
  discountPct: number
  baseNetPrice: number
}

/** Giá thực mua theo công ty × NCC. */
export interface ComparePurchaseRow {
  companyCode: string
  supplierCode: string
  supplierName: string
  lastDate: string
  lastSource: 'Receipt' | 'PO'
  lastDocumentNo: string
  lastPrice: number
  minPrice: number | null
  maxPrice: number | null
  avgPrice: number | null
  qty12Months: number
  aboveGroupMinPct: number
  isAboveThreshold: boolean
}

/** Một mục lịch sử giá. */
export interface PriceHistoryEntry {
  date: string
  companyCode: string
  supplierCode: string
  source: 'PriceList' | 'PO' | 'Receipt' | 'Quotation'
  documentNo: string
  baseQty: number
  basePrice: number
}

/** Nhãn nguồn giá. */
export const priceSourceLabels: Record<PriceHistoryEntry['source'], string> = {
  PriceList: 'Bảng giá',
  PO: 'Đơn mua hàng',
  Receipt: 'Phiếu nhập',
  Quotation: 'Báo giá trúng thầu',
}

/** Kết quả so sánh giá một mặt hàng. */
export interface PriceComparison {
  itemId: string
  itemCode: string
  itemName: string
  baseUomCode: string
  allCompanies: boolean
  thresholdPct: number
  groupMinPrice: number | null
  groupMaxPrice: number | null
  groupAvgPrice: number | null
  priceLists: ComparePriceListRow[]
  purchases: ComparePurchaseRow[]
  history: PriceHistoryEntry[]
}

/**
 * So sánh giá một mặt hàng.
 * @param itemId Mặt hàng; trống thì không tải.
 * @param allCompanies Toàn tập đoàn (cần quyền).
 * @param thresholdPct Ngưỡng X% tô màu.
 */
export function usePriceComparison(itemId: string | undefined, allCompanies: boolean, thresholdPct: number) {
  const companyId = useCompanyId()
  const query = new URLSearchParams({ itemId: itemId ?? '', allCompanies: String(allCompanies), thresholdPct: String(thresholdPct) }).toString()
  return useQuery({
    queryKey: ['price-comparison', companyId, query],
    queryFn: () => apiFetch<PriceComparison>(`/api/price-comparison?${query}`),
    enabled: !!itemId,
  })
}
