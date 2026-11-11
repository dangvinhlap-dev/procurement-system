import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Kiểu dữ liệu và hàm gọi API quản lý người dùng, vai trò (khớp Proc.Application.Administration).
 * Viết tay tạm thời; sẽ sinh tự động từ OpenAPI khi số API nhiều lên (design.md mục 3.2).
 */

/** Một dòng trên danh sách người dùng. */
export interface UserListItem {
  id: string
  userName: string
  fullName: string
  email: string | null
  isActive: boolean
  isLockedOut: boolean
  /** Tên vai trò tại công ty đang làm việc; rỗng = không làm việc ở công ty này. */
  roleNames: string[]
  companyCodes: string[]
  createdAt: string
}

/** Vai trò, kho, nhóm hàng tại công ty đang làm việc; danh sách kho/nhóm rỗng = tất cả. */
export interface UserAccess {
  roleIds: string[]
  warehouseIds: string[]
  itemCategoryIds: string[]
  /** Bộ phận tại công ty (lát O1) – điền sẵn khi lập đề nghị mua; null = chưa gán. */
  departmentId?: string | null
}

/** Chi tiết người dùng. */
export interface UserDetail {
  id: string
  userName: string
  fullName: string
  email: string | null
  isActive: boolean
  lockoutEnd: string | null
  createdAt: string
  canManageAccount: boolean
  isSelf: boolean
  access: UserAccess
  companyCodes: string[]
}

/** Lựa chọn khi phân quyền tại công ty đang làm việc. */
export interface UserAccessOptions {
  roles: { id: string; code: string; name: string; canAssign: boolean }[]
  warehouses: { id: string; code: string; name: string; type: 'Main' | 'Sub'; parentWarehouseId: string | null; isActive: boolean }[]
  itemCategories: { id: string; code: string; name: string; parentId: string | null; isActive: boolean }[]
  mustLimitWarehouses: boolean
  mustLimitItemCategories: boolean
  /** Bộ phận còn sử dụng của công ty (lát O1). */
  departments?: { id: string; code: string; name: string }[]
}

/** Dữ liệu tạo tài khoản. */
export interface CreateUserInput {
  userName: string
  fullName: string
  email: string | null
  password: string
  access: UserAccess
}

/** Dữ liệu sửa tài khoản. */
export interface UpdateUserInput {
  fullName: string
  email: string | null
  isActive: boolean
}

/** Một dòng trên danh sách vai trò. */
export interface RoleListItem {
  id: string
  code: string
  name: string
  isActive: boolean
  isSystem: boolean
  permissionCount: number
  userCount: number
}

/** Bộ vai trò mẫu: tất cả, Ban cung ứng theo sơ đồ (mục 18.6, G10), vị trí ký biểu mẫu CUVT (lát O3, mục 19.3). */
export type RoleTemplateSet = 'ALL' | 'BAN' | 'CUVT'

/** Tên các bộ vai trò mẫu để chọn trên hộp thoại. */
export const ROLE_TEMPLATE_SETS: { value: RoleTemplateSet; label: string }[] = [
  { value: 'ALL', label: 'Tất cả vai trò mẫu' },
  { value: 'BAN', label: 'Ban cung ứng theo sơ đồ phê duyệt' },
  { value: 'CUVT', label: 'Vị trí ký biểu mẫu CUVT (A, B, DV)' },
]

/** Tham số `set` của API vai trò mẫu (trống = mọi vai trò mẫu). */
const roleSetQuery = (set: RoleTemplateSet) => (set === 'ALL' ? '' : `?set=${set}`)

/** Một vai trò mẫu Ban Cung ứng (design.md mục 18.6) hoặc biểu mẫu CUVT (lát O3). */
export interface RoleTemplate {
  code: string
  name: string
  description: string
  permissionCodes: string[]
  /** Đã có vai trò cùng mã – tạo bộ mẫu sẽ bỏ qua, không sửa. */
  exists: boolean
}

/** Kết quả tạo vai trò từ bộ mẫu. */
export interface ApplyRoleTemplatesResult {
  created: string[]
  skipped: string[]
}

/** Chi tiết vai trò. */
export interface RoleDetail {
  id: string
  code: string
  name: string
  isActive: boolean
  isSystem: boolean
  canEdit: boolean
  permissionCodes: string[]
}

/** Quyền gom theo module. */
export interface PermissionGroup {
  module: string
  name: string
  permissions: { code: string; name: string }[]
}

/**
 * Khóa cache của TanStack Query. Có công ty đang làm việc trong khóa vì dữ liệu (vai trò tại công ty, kho được phép)
 * khác nhau theo công ty – đổi công ty là tải lại.
 */
export const adminKeys = {
  users: (companyId: string) => ['users', companyId] as const,
  user: (companyId: string, id: string) => ['users', companyId, id] as const,
  accessOptions: (companyId: string) => ['users', companyId, 'access-options'] as const,
  roles: (companyId: string) => ['roles', companyId] as const,
  role: (companyId: string, id: string) => ['roles', companyId, id] as const,
  permissions: ['permissions'] as const,
}

/** Id công ty đang làm việc – đưa vào khóa truy vấn để đổi công ty thì tải lại dữ liệu. */
function useCompanyId(): string {
  return useAuth().workingCompany?.id ?? ''
}

/** Danh sách người dùng. */
export function useUsers() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: adminKeys.users(companyId), queryFn: () => apiFetch<UserListItem[]>('/api/users') })
}

/**
 * Chi tiết một người dùng.
 * @param id Id người dùng; null thì không tải (đang tạo mới).
 */
export function useUser(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: adminKeys.user(companyId, id ?? ''),
    queryFn: () => apiFetch<UserDetail>(`/api/users/${id}`),
    enabled: id !== null,
  })
}

/**
 * Lựa chọn vai trò, kho, nhóm hàng khi phân quyền.
 * @param enabled Chỉ tải khi người dùng có quyền quản lý người dùng.
 */
export function useAccessOptions(enabled: boolean) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: adminKeys.accessOptions(companyId),
    queryFn: () => apiFetch<UserAccessOptions>('/api/users/access-options'),
    enabled,
  })
}

/** Danh sách vai trò. */
export function useRoles() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: adminKeys.roles(companyId), queryFn: () => apiFetch<RoleListItem[]>('/api/roles') })
}

/**
 * Chi tiết vai trò.
 * @param id Id vai trò; null thì không tải (đang tạo mới).
 */
export function useRole(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: adminKeys.role(companyId, id ?? ''),
    queryFn: () => apiFetch<RoleDetail>(`/api/roles/${id}`),
    enabled: id !== null,
  })
}

/** Danh mục quyền (không đổi trong phiên nên giữ cache mãi). */
export function usePermissionCatalog() {
  return useQuery({
    queryKey: adminKeys.permissions,
    queryFn: () => apiFetch<PermissionGroup[]>('/api/permissions'),
    staleTime: Infinity,
  })
}

/** Lưu người dùng: tạo mới, sửa thông tin, gán quyền, đặt lại mật khẩu. Lưu xong tải lại danh sách người dùng và vai trò. */
export function useUserMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['users'] })
    // Số người dùng của vai trò thay đổi theo việc gán quyền.
    void queryClient.invalidateQueries({ queryKey: ['roles'] })
  }
  return {
    create: useMutation({
      mutationFn: (input: CreateUserInput) => apiFetch<UserDetail>('/api/users', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: UpdateUserInput }) =>
        apiFetch<UserDetail>(`/api/users/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    setAccess: useMutation({
      mutationFn: ({ id, access }: { id: string; access: UserAccess }) =>
        apiFetch<UserDetail>(`/api/users/${id}/access`, { method: 'PUT', body: access }),
      onSuccess,
    }),
    resetPassword: useMutation({
      mutationFn: ({ id, newPassword }: { id: string; newPassword: string }) =>
        apiFetch<void>(`/api/users/${id}/reset-password`, { method: 'POST', body: { newPassword } }),
      onSuccess,
    }),
  }
}

/** Lưu vai trò: tạo mới, sửa. Lưu xong tải lại danh sách vai trò và lựa chọn vai trò khi phân quyền. */
export function useRoleMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['roles'] })
    void queryClient.invalidateQueries({ queryKey: ['users'] })
  }
  return {
    create: useMutation({
      mutationFn: (input: { code: string; name: string; permissionCodes: string[] }) =>
        apiFetch<RoleDetail>('/api/roles', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: { name: string; isActive: boolean; permissionCodes: string[] } }) =>
        apiFetch<RoleDetail>(`/api/roles/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    createFromTemplates: useMutation({
      mutationFn: (set: RoleTemplateSet) => apiFetch<ApplyRoleTemplatesResult>(`/api/roles/templates${roleSetQuery(set)}`, { method: 'POST' }),
      onSuccess: () => {
        onSuccess()
        void queryClient.invalidateQueries({ queryKey: ['role-templates'] })
      },
    }),
  }
}

/**
 * Các vai trò mẫu của một bộ kèm cờ đã có.
 * @param enabled Chỉ tải khi mở hộp thoại.
 * @param set Bộ vai trò mẫu.
 */
export function useRoleTemplates(enabled: boolean, set: RoleTemplateSet) {
  return useQuery({
    queryKey: ['role-templates', set],
    queryFn: () => apiFetch<RoleTemplate[]>(`/api/roles/templates${roleSetQuery(set)}`),
    enabled,
  })
}
