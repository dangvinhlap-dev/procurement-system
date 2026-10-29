import { useState } from 'react'
import { useRoles, type RoleListItem } from '../../api/administration'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { ListPage } from '../../components/ListPage/ListPage'
import { RoleDrawer } from './RoleDrawer'

const columns: GridColumn<RoleListItem>[] = [
  { key: 'code', title: 'Mã vai trò', width: 120, value: (r) => r.code },
  { key: 'name', title: 'Tên vai trò', width: 240, value: (r) => r.name },
  { key: 'permissions', title: 'Số quyền', width: 80, type: 'quantity', value: (r) => r.permissionCount },
  { key: 'users', title: 'Số người dùng', width: 100, type: 'quantity', value: (r) => r.userCount },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 110,
    type: 'status',
    value: (r) => (r.isActive ? 'Đang dùng' : 'Ngừng dùng'),
    tone: (r) => (r.isActive ? 'success' : 'neutral'),
    groupable: true,
  },
  { key: 'kind', title: 'Loại', width: 110, value: (r) => (r.isSystem ? 'Hệ thống' : 'Tự định nghĩa'), groupable: true },
]

/**
 * Màn hình Vai trò và phân quyền (design.md mục 9.1, 9.5): danh sách vai trò dùng chung cho mọi công ty;
 * bấm đúp mở ngăn kéo để sửa tên và tick quyền theo module.
 */
export function RoleListPage() {
  const { hasPermission } = useAuth()
  const roles = useRoles()
  // undefined = đóng; null = tạo mới; chuỗi = Id vai trò đang mở.
  const [openId, setOpenId] = useState<string | null | undefined>(undefined)

  return (
    <>
      <ListPage
        title="Vai trò và phân quyền"
        exportFileName="vai-tro"
        rows={roles.data ?? []}
        columns={columns}
        rowKey={(r) => r.id}
        loading={roles.isLoading}
        canCreate={hasPermission(Permission.RoleManage)}
        onCreate={() => setOpenId(null)}
        onOpenRow={(r) => setOpenId(r.id)}
      />
      {openId !== undefined && <RoleDrawer roleId={openId} onClose={() => setOpenId(undefined)} />}
    </>
  )
}
