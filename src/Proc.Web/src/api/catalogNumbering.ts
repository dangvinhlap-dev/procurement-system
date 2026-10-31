import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from './client'

/*
 * Dải đánh số danh mục tự động (khớp Proc.Application.MasterData – CatalogNumberingAdministration.cs, design.md mục 5.11).
 */

/** Danh mục đánh số tự động. */
export type NumberedCatalog = 'supplier' | 'supplier-group' | 'item-category' | 'item'

/** Tên danh mục đánh số. */
export const numberedCatalogLabels: Record<NumberedCatalog, string> = {
  supplier: 'Nhà cung cấp',
  'supplier-group': 'Nhóm nhà cung cấp',
  'item-category': 'Nhóm hàng',
  item: 'Hàng hóa',
}

/** Một dải đánh số. */
export interface CatalogNumbering {
  id: string
  catalog: NumberedCatalog
  catalogName: string
  categoryId: string | null
  categoryCode: string | null
  categoryName: string | null
  prefix: string
  digits: number
  fromNumber: number
  toNumber: number
  sample: string
  nextCode: string | null
}

/** Nội dung dải gửi lên (danh mục, nhóm hàng chỉ dùng khi thêm). */
export interface SaveCatalogNumberingInput {
  catalog: NumberedCatalog
  categoryId: string | null
  prefix: string
  digits: number
  fromNumber: number
  toNumber: number
}

/** Các dải đánh số (dùng chung toàn tập đoàn). */
export function useCatalogNumberings() {
  return useQuery({ queryKey: ['catalog-numberings'], queryFn: () => apiFetch<CatalogNumbering[]>('/api/catalog-numberings') })
}

/** Thêm, sửa, xóa dải. */
export function useCatalogNumberingMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['catalog-numberings'] })
  return {
    create: useMutation({
      mutationFn: (input: SaveCatalogNumberingInput) => apiFetch<CatalogNumbering>('/api/catalog-numberings', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveCatalogNumberingInput }) =>
        apiFetch<CatalogNumbering>(`/api/catalog-numberings/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    remove: useMutation({
      mutationFn: (id: string) => apiFetch<void>(`/api/catalog-numberings/${id}`, { method: 'DELETE' }),
      onSuccess,
    }),
  }
}
