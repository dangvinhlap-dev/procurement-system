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

/** Id công ty đang làm việc – đưa vào khóa truy vấn để đổi công ty thì tải lại dữ liệu. */
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

/** Bộ phận của công ty đang làm việc – khớp Proc.Application.Organization.DepartmentDto (design.md mục 18.5). */
export interface Department {
  id: string
  code: string
  name: string
  managerName: string | null
  isActive: boolean
  /** Số PR đã ghi bộ phận này. */
  requestCount: number
  /** Khối (lát O1, design.md mục 19.3). */
  divisionId: string | null
  /** "Mã – tên" khối. */
  divisionName: string | null
  /** Trưởng phòng ban (tài khoản) – người duyệt cấp "Trưởng phòng ban của phiếu". */
  managerUserId: string | null
  /** Họ tên Trưởng phòng ban. */
  managerUserName: string | null
  /** Họ tên Giám đốc khối của khối đã gán. */
  directorName: string | null
  /** Số người dùng thuộc bộ phận. */
  memberCount: number
}

/** Thông tin bộ phận khi thêm/sửa. */
export interface DepartmentInput {
  name: string
  managerName: string | null
  divisionId: string | null
  managerUserId: string | null
}

/** Khối của công ty đang làm việc – khớp Proc.Application.Organization.DivisionDto (lát O1). */
export interface Division {
  id: string
  code: string
  name: string
  /** Giám đốc khối (tài khoản) – người duyệt cấp "GĐ khối của phiếu". */
  directorUserId: string | null
  directorName: string | null
  isActive: boolean
  /** Số bộ phận thuộc khối. */
  departmentCount: number
}

/** Thông tin khối khi thêm/sửa. */
export interface DivisionInput {
  name: string
  directorUserId: string | null
}

/** Tài khoản chọn được làm Trưởng phòng ban / Giám đốc khối (có vai trò tại công ty đang làm việc). */
export interface OrganizationUserOption {
  id: string
  userName: string
  fullName: string
}

/** Bộ phận của công ty đang làm việc (cả bộ phận ngừng dùng), theo mã. Người lập/xem PR cũng đọc được để chọn bộ phận. */
export function useDepartments(enabled = true) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['departments', companyId],
    queryFn: () => apiFetch<Department[]>('/api/departments'),
    enabled,
  })
}

/** Thêm, sửa bộ phận. Lưu xong tải lại danh sách bộ phận. */
export function useDepartmentMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['departments'] })
  return {
    create: useMutation({
      mutationFn: (input: DepartmentInput & { code: string }) =>
        apiFetch<Department>('/api/departments', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: DepartmentInput & { isActive: boolean } }) =>
        apiFetch<Department>(`/api/departments/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
  }
}

/** Khối của công ty đang làm việc (cả khối ngừng dùng), theo mã – lát O1. */
export function useDivisions(enabled = true) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['divisions', companyId],
    queryFn: () => apiFetch<Division[]>('/api/divisions'),
    enabled,
  })
}

/** Thêm, sửa khối. Lưu xong tải lại khối và bộ phận (bộ phận hiện tên khối, GĐ khối). */
export function useDivisionMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['divisions'] })
    void queryClient.invalidateQueries({ queryKey: ['departments'] })
  }
  return {
    create: useMutation({
      mutationFn: (input: DivisionInput & { code: string }) => apiFetch<Division>('/api/divisions', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: DivisionInput & { isActive: boolean } }) =>
        apiFetch<Division>(`/api/divisions/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
  }
}

/** Tài khoản chọn làm Trưởng phòng ban / Giám đốc khối (chỉ người sửa danh mục bộ phận gọi được). */
export function useOrganizationUserOptions(enabled = true) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['department-user-options', companyId],
    queryFn: () => apiFetch<OrganizationUserOption[]>('/api/departments/user-options'),
    enabled,
  })
}

/** Bộ phận của chính người dùng tại công ty đang làm việc – điền sẵn khi lập đề nghị mua (null = chưa gán). */
export function useMyDepartment(enabled = true) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['my-department', companyId],
    queryFn: async () => (await apiFetch<{ departmentId: string | null }>('/api/departments/mine')).departmentId,
    enabled,
  })
}

/**
 * Lựa chọn tài khoản cho ô Trưởng phòng ban / Giám đốc khối: "Họ tên (tên đăng nhập)". Tài khoản đang gán mà không còn trong danh sách
 * (mất quyền tại công ty) vẫn hiện tên để người sửa thấy và đổi.
 * @param options Tài khoản có vai trò tại công ty.
 * @param current Id và họ tên đang gán.
 */
export function userSelectOptions(
  options: OrganizationUserOption[] | undefined,
  current?: { id: string | null; name: string | null },
) {
  const list = (options ?? []).map((u) => ({ value: u.id, label: `${u.fullName} (${u.userName})` }))
  if (current?.id && !list.some((o) => o.value === current.id)) list.unshift({ value: current.id, label: current.name ?? current.id })
  return list
}
