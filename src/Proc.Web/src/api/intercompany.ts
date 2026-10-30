import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Kiểu dữ liệu và hàm gọi API bảng giá nội bộ, hàng đi đường, đối chiếu nội bộ (khớp Proc.Application.Pricing, Inventory).
 */

const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/** Một dòng bảng giá nội bộ của công ty bán đang làm việc. */
export interface InternalPrice {
  id: string
  buyerCompanyId: string | null
  buyerCompanyCode: string | null
  itemId: string
  itemCode: string
  itemName: string
  uomCode: string
  unitPrice: number
  vatCode: string | null
  validFrom: string
  validTo: string | null
  note: string | null
}

/** Nội dung thêm/sửa giá nội bộ. */
export interface SaveInternalPriceInput {
  buyerCompanyId: string | null
  itemId: string
  unitPrice: number
  vatCode: string | null
  validFrom: string
  validTo: string | null
  note: string | null
}

/** Một công ty khác trong tập đoàn. */
export interface GroupCompany {
  id: string
  code: string
  name: string
}

/** Giá nội bộ áp cho một mặt hàng tại một ngày. */
export interface ResolvedPrice {
  itemId: string
  unitPrice: number
  vatCode: string | null
}

/** Bảng giá nội bộ của công ty đang làm việc. */
export function useInternalPrices() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['internal-prices', companyId], queryFn: () => apiFetch<InternalPrice[]>('/api/internal-prices') })
}

/** Thêm, sửa giá nội bộ. */
export function useInternalPriceMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['internal-prices'] })
  return {
    create: useMutation({
      mutationFn: (input: SaveInternalPriceInput) => apiFetch<InternalPrice>('/api/internal-prices', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveInternalPriceInput }) =>
        apiFetch<InternalPrice>(`/api/internal-prices/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
  }
}

/** Các công ty khác trong tập đoàn (chọn công ty mua). */
export function useGroupCompanies() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['group-companies', companyId], queryFn: () => apiFetch<GroupCompany[]>('/api/group-companies'), staleTime: 300_000 })
}

/**
 * Giá nội bộ áp dụng khi bán cho một công ty vào một ngày – hiện trước trên phiếu xuất bán (server vẫn tự lấy lại khi lưu).
 * @param buyerCompanyId Công ty mua; trống thì không tải.
 * @param date Ngày phiếu yyyy-MM-dd.
 * @param itemIds Mặt hàng trên phiếu.
 */
export function useResolvedPrices(buyerCompanyId: string | undefined, date: string | undefined, itemIds: string[]) {
  const companyId = useCompanyId()
  const params = new URLSearchParams({ buyerCompanyId: buyerCompanyId ?? '', date: date ?? '' })
  for (const id of [...new Set(itemIds)].sort()) params.append('itemIds', id)
  const query = params.toString()
  return useQuery({
    queryKey: ['internal-prices', companyId, 'resolve', query],
    queryFn: () => apiFetch<ResolvedPrice[]>(`/api/internal-prices/resolve?${query}`),
    enabled: !!buyerCompanyId && !!date && itemIds.length > 0,
    placeholderData: keepPreviousData,
  })
}

/** Một phiếu bán nội bộ đang đi đường. */
export interface GoodsInTransit {
  direction: 'Out' | 'In'
  saleId: string
  saleNo: string
  saleDate: string
  sellerCode: string
  buyerCode: string
  receiptId: string | null
  receiptNo: string | null
  totalAmount: number
  totalVat: number
}

/** Hàng đi đường của công ty đang làm việc (bán ra chưa được nhận, mua vào đang chờ nhận). */
export function useGoodsInTransit() {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['inventory-documents', companyId, 'transit'],
    queryFn: () => apiFetch<GoodsInTransit[]>('/api/reports/goods-in-transit'),
  })
}

/** Đối chiếu mua bán nội bộ với một công ty. */
export interface Reconciliation {
  counterCompanyId: string
  counterCompanyCode: string
  counterCompanyName: string
  soldAmount: number
  receivedByCounterAmount: number
  soldByCounterAmount: number
  purchasedAmount: number
}

/**
 * Đối chiếu mua bán nội bộ hai chiều theo khoảng ngày phiếu xuất bán.
 * @param range Từ ngày, đến ngày (yyyy-MM-dd).
 */
export function useReconciliation(range: { from: string; to: string }) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['inventory-documents', companyId, 'reconciliation', range.from, range.to],
    queryFn: () => apiFetch<Reconciliation[]>(`/api/reports/intercompany-reconciliation?from=${range.from}&to=${range.to}`),
    placeholderData: keepPreviousData,
  })
}
