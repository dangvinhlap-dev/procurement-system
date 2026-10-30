import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Kiểu dữ liệu và hàm gọi API danh mục hàng hóa: đơn vị tính, nhóm hàng, hàng hóa (khớp Proc.Application.MasterData).
 * Viết tay tạm thời; sẽ sinh tự động từ OpenAPI khi số API nhiều lên (design.md mục 3.2).
 */

/** Đơn vị tính. */
export interface Uom {
  id: string
  code: string
  name: string
  isActive: boolean
  /** Số mặt hàng (trong phạm vi người xem) dùng làm ĐVT cơ sở. */
  itemCount: number
}

/** Nhóm hàng. */
export interface ItemCategory {
  id: string
  code: string
  name: string
  parentId: string | null
  parentCode: string | null
  /** "VT / VTC / VTCK" – danh sách xếp theo đường dẫn để đọc như cây. */
  path: string
  level: number
  inventoryAccount: string | null
  isActive: boolean
  itemCount: number
}

/** Loại hàng (design.md mục 5.2). */
export type ItemType = 'Inventory' | 'Service' | 'Asset' | 'Consumable'

/** Tên tiếng Việt của loại hàng. */
export const itemTypeLabels: Record<ItemType, string> = {
  Inventory: 'Hàng tồn kho',
  Service: 'Dịch vụ',
  Asset: 'Tài sản',
  Consumable: 'Vật tư tiêu hao',
}

/** Thuế suất GTGT mặc định – khớp Proc.Domain.MasterData.VatRates. */
export const vatOptions = [
  { value: 'KCT', label: 'Không chịu thuế' },
  { value: '0', label: '0%' },
  { value: '5', label: '5%' },
  { value: '8', label: '8%' },
  { value: '10', label: '10%' },
]

/** Một dòng trên danh sách hàng hóa. */
export interface ItemListItem {
  id: string
  code: string
  name: string
  type: ItemType
  categoryId: string
  categoryCode: string
  categoryName: string
  baseUomCode: string
  specification: string | null
  brand: string | null
  origin: string | null
  barcode: string | null
  vatCode: string | null
  isActive: boolean
}

/** Kết quả tìm hàng hóa: tối đa `limit` dòng, `truncated` = còn dòng chưa trả. */
export interface ItemListResult {
  items: ItemListItem[]
  truncated: boolean
  limit: number
}

/** Một ĐVT quy đổi: 1 ĐVT này = factor ĐVT cơ sở. */
export interface ItemConversion {
  uomId: string
  uomCode: string
  uomName: string
  factor: number
}

/** Chi tiết hàng hóa. */
export interface ItemDetail {
  id: string
  code: string
  name: string
  englishName: string | null
  type: ItemType
  categoryId: string
  baseUomId: string
  specification: string | null
  brand: string | null
  origin: string | null
  barcode: string | null
  vatCode: string | null
  technicalSpecs: string | null
  trackLot: boolean
  trackExpiry: boolean
  minStock: number | null
  maxStock: number | null
  isActive: boolean
  conversions: ItemConversion[]
  /** Sửa được: có quyền và đang làm việc ở tổng công ty. */
  canEdit: boolean
}

/** Dữ liệu lưu hàng hóa (thêm: kèm code; sửa: kèm isActive). */
export interface ItemInput {
  name: string
  englishName: string | null
  type: ItemType
  categoryId: string
  baseUomId: string
  specification: string | null
  brand: string | null
  origin: string | null
  barcode: string | null
  vatCode: string | null
  technicalSpecs: string | null
  trackLot: boolean
  trackExpiry: boolean
  minStock: number | null
  maxStock: number | null
  conversions: { uomId: string; factor: number }[]
}

/** Điều kiện tìm hàng hóa phía server. */
export interface ItemFilter {
  categoryId?: string
  type?: ItemType
}

function useCompanyId(): string {
  return useAuth().workingCompany?.id ?? ''
}

/** Danh sách đơn vị tính. */
export function useUoms() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['uoms', companyId], queryFn: () => apiFetch<Uom[]>('/api/uoms') })
}

/** Lưu đơn vị tính. */
export function useUomMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['uoms'] })
  return {
    create: useMutation({
      mutationFn: (input: { code: string; name: string }) => apiFetch<Uom>('/api/uoms', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: { name: string; isActive: boolean } }) =>
        apiFetch<Uom>(`/api/uoms/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
  }
}

/** Danh sách nhóm hàng trong phạm vi, xếp theo đường dẫn. */
export function useItemCategories() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['item-categories', companyId], queryFn: () => apiFetch<ItemCategory[]>('/api/item-categories') })
}

/** Lưu nhóm hàng. Lưu xong tải lại nhóm hàng, hàng hóa và lựa chọn nhóm hàng khi phân quyền người dùng. */
export function useItemCategoryMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['item-categories'] })
    void queryClient.invalidateQueries({ queryKey: ['items'] })
    void queryClient.invalidateQueries({ queryKey: ['users'] })
  }
  type Input = { name: string; parentId: string | null; inventoryAccount: string | null }
  return {
    create: useMutation({
      mutationFn: (input: Input & { code: string }) =>
        apiFetch<ItemCategory>('/api/item-categories', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: Input & { isActive: boolean } }) =>
        apiFetch<ItemCategory>(`/api/item-categories/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
  }
}

/**
 * Tìm hàng hóa theo nhóm, loại (lọc ở server); từ khóa lọc tiếp ở danh sách.
 * @param filter Nhóm hàng (gồm nhóm con) và loại hàng.
 */
export function useItems(filter: ItemFilter) {
  const companyId = useCompanyId()
  const params = new URLSearchParams()
  if (filter.categoryId) params.set('categoryId', filter.categoryId)
  if (filter.type) params.set('type', filter.type)
  const query = params.toString()
  return useQuery({
    queryKey: ['items', companyId, query],
    queryFn: () => apiFetch<ItemListResult>(`/api/items${query ? `?${query}` : ''}`),
    // Đổi bộ lọc thì giữ danh sách cũ tới khi có danh sách mới, lưới không bị nháy trống.
    placeholderData: keepPreviousData,
  })
}

/**
 * Chi tiết một mặt hàng.
 * @param id Id; null thì không tải (đang thêm mới).
 */
export function useItem(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['items', companyId, 'detail', id ?? ''],
    queryFn: () => apiFetch<ItemDetail>(`/api/items/${id}`),
    enabled: id !== null,
  })
}

/** Lưu hàng hóa. Lưu xong tải lại hàng hóa, số mặt hàng của nhóm hàng và đơn vị tính. */
export function useItemMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['items'] })
    void queryClient.invalidateQueries({ queryKey: ['item-categories'] })
    void queryClient.invalidateQueries({ queryKey: ['uoms'] })
  }
  return {
    create: useMutation({
      mutationFn: (input: ItemInput & { code: string }) => apiFetch<ItemDetail>('/api/items', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: ItemInput & { isActive: boolean } }) =>
        apiFetch<ItemDetail>(`/api/items/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
  }
}
