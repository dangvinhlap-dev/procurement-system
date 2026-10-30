import { Select } from 'antd'
import { useMemo, useState } from 'react'
import { useWarehouses, type WarehouseDetail } from '../../api/organization'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { WarehouseDrawer } from './WarehouseDrawer'

const columns: GridColumn<WarehouseDetail>[] = [
  { key: 'code', title: 'Mã kho', width: 90, value: (r) => r.code },
  { key: 'name', title: 'Tên kho', width: 220, value: (r) => r.name },
  { key: 'type', title: 'Loại kho', width: 90, value: (r) => (r.type === 'Main' ? 'Kho tổng' : 'Kho phụ'), groupable: true },
  { key: 'parent', title: 'Kho cha', width: 80, value: (r) => r.parentCode, groupable: true },
  { key: 'keeper', title: 'Thủ kho', width: 150, value: (r) => r.keeper },
  { key: 'address', title: 'Địa chỉ', width: 220, value: (r) => r.address },
  { key: 'negative', title: 'Tồn âm', width: 80, value: (r) => (r.allowNegativeStock ? 'Cho phép' : 'Không'), groupable: true },
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
 * Màn hình Danh mục kho (design.md mục 4, 5.1): kho của công ty đang làm việc trong phạm vi được phép, kho tổng đầu tiên.
 * Kho tổng có sẵn khi thêm công ty; ở đây thêm kho phụ, sửa thông tin, đổi kho cha, bật cho phép tồn âm.
 */
export function WarehouseListPage() {
  const { workingCompany, hasPermission } = useAuth()
  const warehouses = useWarehouses()
  const [status, setStatus] = useState('')
  // undefined = đóng; null = thêm mới; còn lại = kho đang mở.
  const [open, setOpen] = useState<WarehouseDetail | null | undefined>(undefined)

  const rows = useMemo(
    () => (warehouses.data ?? []).filter((w) => !status || (status === 'active') === w.isActive),
    [warehouses.data, status],
  )

  return (
    <>
      <ListPage
        title={`Danh mục kho – ${workingCompany?.code ?? ''}`}
        exportFileName="kho"
        rows={rows}
        columns={columns}
        rowKey={(r) => r.id}
        loading={warehouses.isLoading}
        canCreate={hasPermission(Permission.WarehouseManage)}
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
      {open !== undefined && (
        <WarehouseDrawer warehouse={open} warehouses={warehouses.data ?? []} onClose={() => setOpen(undefined)} />
      )}
    </>
  )
}
