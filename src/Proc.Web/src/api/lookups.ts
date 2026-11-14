import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import { hasWildcard } from '../utils/text'

/*
 * Ô chọn mặt hàng, NCC dùng chung trên các phiếu mua – giá – thầu. Tách khỏi API chứng từ kho khi gỡ phân hệ kho (lát K3, design.md mục 19.4).
 */

/** Mặt hàng cho ô chọn hàng trên chứng từ, kèm ĐVT chọn được. */
export interface ItemLookup {
  id: string
  code: string
  name: string
  type: string
  baseUomId: string
  vatCode: string | null
  trackLot: boolean
  trackExpiry: boolean
  uoms: { uomId: string; code: string; factor: number }[]
}

/** Id công ty đang làm việc – đưa vào khóa truy vấn để đổi công ty thì tải lại dữ liệu. */
function useCompanyId(): string {
  return useAuth().workingCompany?.id ?? ''
}

/**
 * Tìm hàng cho ô chọn hàng: theo từ khóa (≥ 2 ký tự, hoặc có dấu `*` – gõ `*` liệt kê tất cả kiểu SAP ECC, tối đa 500 dòng)
 * và/hoặc theo Id (hiển thị lại các dòng đã có).
 * @param search Từ khóa.
 * @param ids Id các mặt hàng đang có trên phiếu.
 * @param inventoryOnly Chỉ hàng loại tồn kho (ItemType.Inventory).
 */
export function useItemLookup(search: string, ids: string[], inventoryOnly = true) {
  const companyId = useCompanyId()
  const keyword = search.trim()
  const params = new URLSearchParams({ inventoryOnly: String(inventoryOnly) })
  const searching = keyword.length >= 2 || hasWildcard(keyword)
  if (searching) params.set('search', keyword)
  for (const id of [...new Set(ids)].sort()) params.append('ids', id)
  const query = params.toString()
  return useQuery({
    queryKey: ['items', companyId, 'lookup', query],
    queryFn: () => apiFetch<ItemLookup[]>(`/api/items/lookup?${query}`),
    enabled: searching || ids.length > 0,
    placeholderData: keepPreviousData,
  })
}

/** NCC chọn được trên phiếu mua (PR, PO…). */
export interface SupplierLookup {
  id: string
  code: string
  name: string
  taxCode: string | null
}

/**
 * NCC đã duyệt và đang áp dụng tại công ty đang làm việc (kèm NCC đang có trên phiếu dù không còn thỏa điều kiện).
 * @param ids NCC đang có trên phiếu.
 */
export function useSupplierLookup(ids: string[]) {
  const companyId = useCompanyId()
  const params = new URLSearchParams()
  for (const id of ids) params.append('ids', id)
  const query = params.toString()
  return useQuery({
    queryKey: ['suppliers', companyId, 'lookup', query],
    queryFn: () => apiFetch<SupplierLookup[]>(`/api/suppliers/lookup${query ? `?${query}` : ''}`),
  })
}
