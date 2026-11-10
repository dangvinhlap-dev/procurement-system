import { Select } from '../../classic/Select'
import { useMemo, useState } from 'react'
import { useDivisions, type Division } from '../../api/organization'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { DivisionDrawer } from './DivisionDrawer'

const columns: GridColumn<Division>[] = [
  { key: 'code', title: 'Mã khối', width: 110, value: (r) => r.code },
  { key: 'name', title: 'Tên khối', width: 260, value: (r) => r.name },
  { key: 'director', title: 'Giám đốc khối', width: 200, value: (r) => r.directorName },
  { key: 'departments', title: 'Số bộ phận', width: 100, type: 'quantity', value: (r) => r.departmentCount },
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
 * Màn hình Danh mục khối (lát O1, design.md mục 19.3): khối của công ty đang làm việc, mỗi khối có Giám đốc khối – người duyệt cấp
 * "GĐ khối của phiếu" trong quy trình duyệt. Bộ phận gán vào khối ở danh mục bộ phận. Không xóa; khối không dùng nữa thì ngừng dùng.
 */
export function DivisionListPage() {
  const { workingCompany, hasPermission } = useAuth()
  const divisions = useDivisions()
  const [status, setStatus] = useState('')
  // undefined = đóng; null = thêm mới; còn lại = khối đang mở.
  const [open, setOpen] = useState<Division | null | undefined>(undefined)

  const rows = useMemo(
    () => (divisions.data ?? []).filter((d) => !status || (status === 'active') === d.isActive),
    [divisions.data, status],
  )

  return (
    <>
      <ListPage
        title={`Danh mục khối – ${workingCompany?.code ?? ''}`}
        exportFileName="khoi"
        rows={rows}
        columns={columns}
        rowKey={(r) => r.id}
        auditEntity="Division"
        loading={divisions.isLoading}
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
      {open !== undefined && <DivisionDrawer division={open} onClose={() => setOpen(undefined)} />}
    </>
  )
}
