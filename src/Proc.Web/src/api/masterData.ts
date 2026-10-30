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

/** Nhóm NCC. */
export interface SupplierGroup {
  id: string
  code: string
  name: string
  isActive: boolean
  supplierCount: number
}

/** Trạng thái NCC (design.md mục 5.3). */
export type SupplierStatus = 'Potential' | 'Approved' | 'Suspended' | 'Blacklisted'

/** Tên tiếng Việt và màu chấm trạng thái của NCC. */
export const supplierStatusLabels: Record<SupplierStatus, string> = {
  Potential: 'Tiềm năng',
  Approved: 'Đã duyệt',
  Suspended: 'Tạm ngưng',
  Blacklisted: 'Danh sách đen',
}

/** Một dòng trên danh sách NCC. */
export interface SupplierListItem {
  id: string
  code: string
  name: string
  shortName: string | null
  groupCode: string
  groupName: string
  isForeign: boolean
  taxCode: string | null
  phone: string | null
  email: string | null
  address: string | null
  primaryContact: string | null
  status: SupplierStatus
  rating: number | null
  internalCompanyCode: string | null
}

/** Kết quả tìm NCC. */
export interface SupplierListResult {
  items: SupplierListItem[]
  truncated: boolean
  limit: number
}

/** Người liên hệ; id null = dòng mới. */
export interface SupplierContact {
  id: string | null
  name: string
  position: string | null
  phone: string | null
  email: string | null
  isPrimary: boolean
}

/** Tài khoản ngân hàng; id null = dòng mới. */
export interface SupplierBankAccount {
  id: string | null
  bankName: string
  branch: string | null
  accountNumber: string
  accountName: string | null
  isDefault: boolean
}

/** Thông tin NCC gửi lên khi lưu (code chỉ dùng khi thêm). */
export interface SupplierInput {
  code: string | null
  groupId: string
  name: string
  shortName: string | null
  isForeign: boolean
  taxCode: string | null
  address: string | null
  phone: string | null
  email: string | null
  website: string | null
  paymentTerms: string | null
  supplyScope: string | null
  rating: number | null
  status: SupplierStatus
  statusNote: string | null
  internalCompanyId: string | null
  contacts: SupplierContact[]
  bankAccounts: SupplierBankAccount[]
}

/** Chi tiết NCC. */
export interface SupplierDetail extends Omit<SupplierInput, 'code'> {
  id: string
  code: string
  /** Sửa được: có quyền và đang làm việc ở tổng công ty. */
  canEdit: boolean
}

/** Danh sách nhóm NCC. */
export function useSupplierGroups() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['supplier-groups', companyId], queryFn: () => apiFetch<SupplierGroup[]>('/api/supplier-groups') })
}

/** Lưu nhóm NCC. */
export function useSupplierGroupMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['supplier-groups'] })
  return {
    create: useMutation({
      mutationFn: (input: { code: string; name: string }) =>
        apiFetch<SupplierGroup>('/api/supplier-groups', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: { name: string; isActive: boolean } }) =>
        apiFetch<SupplierGroup>(`/api/supplier-groups/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
  }
}

/**
 * Tìm NCC theo nhóm, trạng thái (lọc ở server); từ khóa lọc tiếp ở danh sách.
 * @param filter Nhóm NCC và trạng thái.
 */
export function useSuppliers(filter: { groupId?: string; status?: SupplierStatus }) {
  const companyId = useCompanyId()
  const params = new URLSearchParams()
  if (filter.groupId) params.set('groupId', filter.groupId)
  if (filter.status) params.set('status', filter.status)
  const query = params.toString()
  return useQuery({
    queryKey: ['suppliers', companyId, query],
    queryFn: () => apiFetch<SupplierListResult>(`/api/suppliers${query ? `?${query}` : ''}`),
    placeholderData: keepPreviousData,
  })
}

/**
 * Chi tiết NCC.
 * @param id Id; null thì không tải (đang thêm mới).
 */
export function useSupplier(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['suppliers', companyId, 'detail', id ?? ''],
    queryFn: () => apiFetch<SupplierDetail>(`/api/suppliers/${id}`),
    enabled: id !== null,
  })
}

/** Lưu NCC. Lưu xong tải lại NCC và số NCC của nhóm. */
export function useSupplierMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['suppliers'] })
    void queryClient.invalidateQueries({ queryKey: ['supplier-groups'] })
  }
  return {
    create: useMutation({
      mutationFn: (input: SupplierInput) => apiFetch<SupplierDetail>('/api/suppliers', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SupplierInput }) =>
        apiFetch<SupplierDetail>(`/api/suppliers/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
  }
}
