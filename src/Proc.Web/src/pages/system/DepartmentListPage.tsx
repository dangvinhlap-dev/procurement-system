import { Select } from 'antd'
import { useMemo, useState } from 'react'
import { useDepartments, type Department } from '../../api/organization'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { DepartmentDrawer } from './DepartmentDrawer'

const columns: GridColumn<Department>[] = [
  { key: 'code', title: 'Mã bộ phận', width: 110, value: (r) => r.code },
  { key: 'name', title: 'Tên bộ phận', width: 260, value: (r) => r.name },
  { key: 'manager', title: 'Trưởng bộ phận', width: 180, value: (r) => r.managerName },
  { key: 'requests', title: 'Số PR', width: 90, type: 'quantity', value: (r) => r.requestCount },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 110,
    type: 'status',
    value: (r) => (r.isActive ? 'Đang dùng' : 'Ngừng dùng'),
    tone: (r) => (r.isActive ? 'success' : 'neutral'),
    groupable: true,
  },
]

/**
 * Màn hình Danh mục bộ phận (design.md mục 5.1, 18.5): phòng ban, phân xưởng của công ty đang làm việc – đơn vị đề nghị
 * mua hàng trên PR. Mỗi công ty tự quản lý danh mục của mình; không xóa, bộ phận không dùng nữa thì ngừng dùng.
 */
export function DepartmentListPage() {
  const { workingCompany, hasPermission } = useAuth()
  const departments = useDepartments()
  const [status, setStatus] = useState('')
  // undefined = đóng; null = thêm mới; còn lại = bộ phận đang mở.
  const [open, setOpen] = useState<Department | null | undefined>(undefined)

  const rows = useMemo(
    () => (departments.data ?? []).filter((d) => !status || (status === 'active') === d.isActive),
    [departments.data, status],
  )

  return (
    <>
      <ListPage
        title={`Danh mục bộ phận – ${workingCompany?.code ?? ''}`}
        exportFileName="bo-phan"
        rows={rows}
        columns={columns}
        rowKey={(r) => r.id}
        loading={departments.isLoading}
        canCreate={hasPermission(Permission.DepartmentManage)}
        onCreate={() => setOpen(null)}
        onOpenRow={setOpen}
        onClearFilters={() => setStatus('')}
        filters={
          <FilterField label="Trạng thái" width={130}>
            {(id) => (
              <Select
                id={id}
                value={status}
                onChange={setStatus}
                options={[
                  { value: '', label: 'Tất cả' },
                  { value: 'active', label: 'Đang dùng' },
                  { value: 'inactive', label: 'Ngừng dùng' },
                ]}
              />
            )}
          </FilterField>
        }
      />
      {open !== undefined && <DepartmentDrawer department={open} onClose={() => setOpen(undefined)} />}
    </>
  )
}
