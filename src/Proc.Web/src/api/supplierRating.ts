import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from './client'

/*
 * Hạng xếp loại nhà cung cấp do người dùng cấu hình (khớp Proc.Application.MasterData – SupplierRatingAdministration.cs, design.md mục 11.3).
 */

/** Một hạng xếp loại. */
export interface SupplierRatingRule {
  id: string
  code: string
  name: string
  sortOrder: number
  minOnTimePct: number
  maxReturnPct: number
}

/** Nội dung hạng gửi lên (mã chỉ dùng khi thêm). */
export type SaveSupplierRatingRuleInput = Omit<SupplierRatingRule, 'id'>

/** Các hạng theo thứ tự xét (dùng chung toàn tập đoàn). */
export function useSupplierRatingRules() {
  return useQuery({ queryKey: ['supplier-rating-rules'], queryFn: () => apiFetch<SupplierRatingRule[]>('/api/supplier-rating-rules') })
}

/** Thêm, sửa, xóa hạng; xong thì tải lại danh sách hạng và báo cáo (cột xếp loại). */
export function useSupplierRatingMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['supplier-rating-rules'] })
    void queryClient.invalidateQueries({ queryKey: ['reports'] })
  }
  return {
    create: useMutation({
      mutationFn: (input: SaveSupplierRatingRuleInput) => apiFetch<SupplierRatingRule>('/api/supplier-rating-rules', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveSupplierRatingRuleInput }) =>
        apiFetch<SupplierRatingRule>(`/api/supplier-rating-rules/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    remove: useMutation({
      mutationFn: (id: string) => apiFetch<void>(`/api/supplier-rating-rules/${id}`, { method: 'DELETE' }),
      onSuccess,
    }),
  }
}
