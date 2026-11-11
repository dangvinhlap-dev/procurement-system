import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/** Công ty đang làm việc – đơn vị mua theo từng công ty nên khóa bộ nhớ đệm có công ty. */
const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/** Đơn vị mua (lát Y5a, design.md mục 19.3 – Q3) – khớp Proc.Application.Purchasing.PurchasingUnitDto. */
export interface PurchasingUnit {
  id: string
  code: string
  name: string
  note: string | null
  isActive: boolean
}

/** Đơn vị mua đang dùng để chọn trên bảng tổng hợp – khớp PurchasingUnitOptionDto. */
export interface PurchasingUnitOption {
  id: string
  code: string
  name: string
}

/** Thông tin đơn vị mua khi thêm/sửa. */
export interface PurchasingUnitInput {
  name: string
  note: string | null
}

/** Mọi đơn vị mua của công ty đang làm việc (cả ngừng dùng), theo mã. */
export function usePurchasingUnits(enabled = true) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['purchasing-units', companyId],
    queryFn: () => apiFetch<PurchasingUnit[]>('/api/purchasing-units'),
    enabled,
  })
}

/** Đơn vị mua đang dùng – danh sách chọn trên bảng tổng hợp 2A/2B (lát Y5b). */
export function usePurchasingUnitOptions(enabled = true) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['purchasing-units', companyId, 'options'],
    queryFn: () => apiFetch<PurchasingUnitOption[]>('/api/purchasing-units/options'),
    enabled,
  })
}

/** Thêm, sửa đơn vị mua. Lưu xong tải lại danh mục và danh sách chọn. */
export function usePurchasingUnitMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['purchasing-units'] })
  return {
    create: useMutation({
      mutationFn: (input: PurchasingUnitInput & { code: string }) =>
        apiFetch<PurchasingUnit>('/api/purchasing-units', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: PurchasingUnitInput & { isActive: boolean } }) =>
        apiFetch<PurchasingUnit>(`/api/purchasing-units/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
  }
}
