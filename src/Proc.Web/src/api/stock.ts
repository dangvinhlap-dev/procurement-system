import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Kiểu dữ liệu và hàm gọi API tồn kho hiện tại, thẻ kho (khớp Proc.Application.Inventory.Stock).
 */

/** Một dòng tồn kho hiện tại: kho × mặt hàng × lô. */
export interface StockOnHandRow {
  warehouseId: string
  warehouseCode: string
  warehouseName: string
  itemId: string
  itemCode: string
  itemName: string
  categoryCode: string
  categoryName: string
  uomCode: string
  lotNo: string | null
  qty: number
  amount: number
  unitCost: number
  minStock: number | null
  belowMin: boolean
}

/** Kết quả xem tồn kho (có giới hạn số dòng). */
export interface StockOnHandResult {
  items: StockOnHandRow[]
  truncated: boolean
  limit: number
}

/** Một phát sinh trên thẻ kho. */
export interface StockCardEntry {
  date: string
  documentId: string
  documentNo: string
  docType: string
  warehouseCode: string
  description: string | null
  lotNo: string | null
  inQty: number
  outQty: number
  balanceQty: number
  unitCost: number
  amount: number
  isCostFinal: boolean
}

/** Thẻ kho một mặt hàng trong khoảng ngày. */
export interface StockCard {
  itemCode: string
  itemName: string
  uomCode: string
  openingQty: number
  openingAmount: number
  entries: StockCardEntry[]
  inQty: number
  outQty: number
  closingQty: number
  closingAmount: number
}

/**
 * Tồn kho hiện tại của công ty đang làm việc (trong phạm vi kho, nhóm hàng của người xem).
 * @param filter Kho, nhóm hàng (gồm nhóm con), có gồm dòng tồn 0.
 */
export function useStockOnHand(filter: { warehouseId?: string; categoryId?: string; includeZero: boolean }) {
  const companyId = useAuth().workingCompany?.id ?? ''
  const params = new URLSearchParams()
  if (filter.warehouseId) params.set('warehouseId', filter.warehouseId)
  if (filter.categoryId) params.set('categoryId', filter.categoryId)
  if (filter.includeZero) params.set('includeZero', 'true')
  const query = params.toString()
  return useQuery({
    queryKey: ['stock', companyId, 'on-hand', query],
    queryFn: () => apiFetch<StockOnHandResult>(`/api/stock-on-hand?${query}`),
    placeholderData: keepPreviousData,
  })
}

/**
 * Thẻ kho một mặt hàng.
 * @param filter Mặt hàng (chưa chọn thì không tải), kho (trống = mọi kho được phép), khoảng ngày yyyy-MM-dd.
 */
export function useStockCard(filter: { itemId?: string; warehouseId?: string; from: string; to: string }) {
  const companyId = useAuth().workingCompany?.id ?? ''
  const params = new URLSearchParams({ itemId: filter.itemId ?? '', from: filter.from, to: filter.to })
  if (filter.warehouseId) params.set('warehouseId', filter.warehouseId)
  const query = params.toString()
  return useQuery({
    queryKey: ['stock', companyId, 'card', query],
    queryFn: () => apiFetch<StockCard>(`/api/stock-card?${query}`),
    enabled: !!filter.itemId,
    placeholderData: keepPreviousData,
  })
}
