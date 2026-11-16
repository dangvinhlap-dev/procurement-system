import { Select } from '../../classic/Select'
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
  { key: 'keeper', title: 'Thủ kho', width: 150, value: (r) => r.keeper },
  { key: 'address', title: 'Địa chỉ', width: 220, value: (r) => r.address },
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
 * Màn hình Danh mục kho (design.md mục 5.1, 19.4): kho – nơi nhận hàng – của công ty đang làm việc trong phạm vi được phép, theo mã.
 * Danh sách phẳng (không kho tổng / kho phụ, không tồn âm); thêm kho, sửa thông tin, ngừng / dùng lại.
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
        auditEntity="Warehouse"
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
        <WarehouseDrawer warehouse={open} onClose={() => setOpen(undefined)} />
      )}
    </>
  )
}
