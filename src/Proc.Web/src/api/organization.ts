import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Kiểu dữ liệu và hàm gọi API danh mục công ty, kho (khớp Proc.Application.Organization).
 * Viết tay tạm thời; sẽ sinh tự động từ OpenAPI khi số API nhiều lên (design.md mục 3.2).
 */

/** Một dòng trên danh sách công ty. */
export interface CompanyListItem {
  id: string
  code: string
  name: string
  shortName: string | null
  taxCode: string | null
  isHeadOffice: boolean
  isActive: boolean
  userCount: number
}

/** Chi tiết công ty. */
export interface CompanyDetail {
  id: string
  code: string
  name: string
  shortName: string | null
  taxCode: string | null
  address: string | null
  representative: string | null
  isHeadOffice: boolean
  accountingCurrency: string
  /** yyyy-MM-dd. */
  startDate: string | null
  isActive: boolean
  /** Sửa được: có quyền quản lý và đang làm việc ở tổng công ty. */
  canEdit: boolean
}

/** Thông tin chung khi thêm/sửa công ty. */
export interface CompanyInfoInput {
  name: string
  shortName: string | null
  taxCode: string | null
  address: string | null
  representative: string | null
  startDate: string | null
}

/** Thêm công ty kèm kho tổng. */
export interface CreateCompanyInput extends CompanyInfoInput {
  code: string
  mainWarehouseCode: string
  mainWarehouseName: string
}

/** Kho trên danh sách và trong ngăn kéo chi tiết. */
export interface WarehouseDetail {
  id: string
  code: string
  name: string
  type: 'Main' | 'Sub'
  parentWarehouseId: string | null
  /** Mã kho cha; null nếu không có hoặc kho cha ngoài phạm vi của người xem. */
  parentCode: string | null
  address: string | null
  keeper: string | null
  allowNegativeStock: boolean
  isActive: boolean
}

/** Thông tin kho khi thêm/sửa. */
export interface WarehouseInput {
  name: string
  parentWarehouseId: string | null
  address: string | null
  keeper: string | null
  allowNegativeStock: boolean
}

/** Khóa cache: có công ty đang làm việc vì danh sách công ty và kho khác nhau theo công ty. */
const keys = {
  companies: (companyId: string) => ['companies', companyId] as const,
  company: (companyId: string, id: string) => ['companies', companyId, id] as const,
  warehouses: (companyId: string) => ['warehouses', companyId] as const,
}

function useCompanyId(): string {
  return useAuth().workingCompany?.id ?? ''
}

/** Danh sách công ty: ở tổng công ty là mọi công ty, ở công ty thành viên chỉ công ty mình. */
export function useCompanies() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: keys.companies(companyId), queryFn: () => apiFetch<CompanyListItem[]>('/api/companies') })
}

/**
 * Chi tiết một công ty.
 * @param id Id công ty; null thì không tải (đang thêm mới).
 */
export function useCompany(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: keys.company(companyId, id ?? ''),
    queryFn: () => apiFetch<CompanyDetail>(`/api/companies/${id}`),
    enabled: id !== null,
  })
}

/**
 * Lưu công ty. Lưu xong tải lại danh sách và đọc lại danh sách công ty của người dùng: công ty mới (người thêm được gán vào)
 * hoặc công ty vừa ngừng dùng phải hiện/ẩn ngay ở ô chọn công ty trên Shellbar.
 */
export function useCompanyMutations() {
  const queryClient = useQueryClient()
  const { refreshUser } = useAuth()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['companies'] })
    void queryClient.invalidateQueries({ queryKey: ['users'] })
    void refreshUser().catch(() => {})
  }
  return {
    create: useMutation({
      mutationFn: (input: CreateCompanyInput) => apiFetch<CompanyDetail>('/api/companies', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: CompanyInfoInput & { isActive: boolean } }) =>
        apiFetch<CompanyDetail>(`/api/companies/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
  }
}

/** Kho của công ty đang làm việc trong phạm vi được phép, kho tổng trước. */
export function useWarehouses() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: keys.warehouses(companyId), queryFn: () => apiFetch<WarehouseDetail[]>('/api/warehouses') })
}

/** Lưu kho. Lưu xong tải lại danh sách kho và lựa chọn kho khi phân quyền người dùng. */
export function useWarehouseMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['warehouses'] })
    void queryClient.invalidateQueries({ queryKey: ['users'] })
  }
  return {
    create: useMutation({
      mutationFn: (input: WarehouseInput & { code: string }) =>
        apiFetch<WarehouseDetail>('/api/warehouses', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: WarehouseInput & { isActive: boolean } }) =>
        apiFetch<WarehouseDetail>(`/api/warehouses/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
  }
}
