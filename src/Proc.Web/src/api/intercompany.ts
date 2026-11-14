import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Kiểu dữ liệu và hàm gọi API bảng giá nội bộ (khớp Proc.Application.Pricing). Hàng đi đường, đối chiếu nội bộ, gợi ý giá theo giá vốn
 * gỡ cùng phân hệ kho ở lát K3 (design.md mục 19.4).
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

