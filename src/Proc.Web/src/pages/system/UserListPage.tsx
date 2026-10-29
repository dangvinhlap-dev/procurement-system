import { Select } from 'antd'
import { useMemo, useState } from 'react'
import { useUsers, type UserListItem } from '../../api/administration'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn, StatusTone } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { UserDrawer } from './UserDrawer'

type UserStatus = 'Đang dùng' | 'Tạm khóa' | 'Ngừng dùng'

/** Trạng thái hiển thị: ngừng dùng được ưu tiên vì tạm khóa không còn ý nghĩa khi tài khoản đã ngừng. */
function statusOf(u: UserListItem): UserStatus {
  if (!u.isActive) return 'Ngừng dùng'
  return u.isLockedOut ? 'Tạm khóa' : 'Đang dùng'
}

const statusTone: Record<UserStatus, StatusTone> = {
  'Đang dùng': 'success',
  'Tạm khóa': 'warning',
  'Ngừng dùng': 'neutral',
}

/**
 * Màn hình Người dùng (design.md mục 9.1, 9.2, 9.5): danh sách tài khoản kèm vai trò tại công ty đang làm việc;
 * bấm đúp mở ngăn kéo chi tiết để sửa thông tin, gán vai trò, kho, nhóm hàng tại công ty này.
 * Mặc định chỉ hiện người làm việc ở công ty đang chọn; chọn "Tất cả tài khoản" để gán thêm người từ công ty khác.
 */
export function UserListPage() {
  const { workingCompany, hasPermission } = useAuth()
  const users = useUsers()
  const [scope, setScope] = useState<'company' | 'all'>('company')
  const [status, setStatus] = useState('')
  // undefined = đóng; null = tạo mới; chuỗi = Id người dùng đang mở.
  const [openId, setOpenId] = useState<string | null | undefined>(undefined)

  const columns = useMemo<GridColumn<UserListItem>[]>(
    () => [
      { key: 'userName', title: 'Tên đăng nhập', width: 140, value: (r) => r.userName },
      { key: 'fullName', title: 'Họ tên', width: 180, value: (r) => r.fullName },
      { key: 'roles', title: `Vai trò tại ${workingCompany?.code ?? ''}`, width: 200, value: (r) => r.roleNames.join(', '), groupable: true },
      { key: 'companies', title: 'Công ty được gán', width: 140, value: (r) => r.companyCodes.join(', '), groupable: true },
      { key: 'email', title: 'Email', width: 180, value: (r) => r.email },
      { key: 'status', title: 'Trạng thái', width: 100, type: 'status', value: statusOf, tone: (r) => statusTone[statusOf(r)], groupable: true },
      { key: 'createdAt', title: 'Ngày tạo', width: 85, type: 'date', value: (r) => r.createdAt.slice(0, 10) },
    ],
    [workingCompany?.code],
  )

  const rows = useMemo(
    () =>
      (users.data ?? []).filter(
        (u) => (scope === 'all' || u.roleNames.length > 0) && (!status || statusOf(u) === status),
      ),
    [users.data, scope, status],
  )

  return (
    <>
      <ListPage
        title="Người dùng"
        exportFileName="nguoi-dung"
        rows={rows}
        columns={columns}
        rowKey={(r) => r.id}
        loading={users.isLoading}
        canCreate={hasPermission(Permission.UserManage)}
        onCreate={() => setOpenId(null)}
        onOpenRow={(r) => setOpenId(r.id)}
        onClearFilters={() => {
          setScope('company')
          setStatus('')
        }}
        filters={
          <>
            <FilterField label="Phạm vi" width={200}>
              {(id) => (
                <Select
                  id={id}
                  value={scope}
                  onChange={setScope}
                  options={[
                    { value: 'company', label: `Người dùng của ${workingCompany?.code ?? ''}` },
                    { value: 'all', label: 'Tất cả tài khoản' },
                  ]}
                />
              )}
            </FilterField>
            <FilterField label="Trạng thái" width={130}>
              {(id) => (
                <Select
                  id={id}
                  value={status}
                  onChange={setStatus}
                  options={[{ value: '', label: 'Tất cả' }, ...Object.keys(statusTone).map((s) => ({ value: s, label: s }))]}
                />
              )}
            </FilterField>
          </>
        }
      />
      {openId !== undefined && <UserDrawer userId={openId} onClose={() => setOpenId(undefined)} />}
    </>
  )
}
