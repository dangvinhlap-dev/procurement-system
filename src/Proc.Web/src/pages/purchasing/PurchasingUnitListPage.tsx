import { useMemo, useState } from 'react'
import { usePurchasingUnits, type PurchasingUnit } from '../../api/purchasingUnits'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { Select } from '../../classic/Select'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { PurchasingUnitDrawer } from './PurchasingUnitDrawer'

const columns: GridColumn<PurchasingUnit>[] = [
  { key: 'code', title: 'Mã đơn vị', width: 120, value: (r) => r.code },
  { key: 'name', title: 'Tên đơn vị mua', width: 280, value: (r) => r.name },
  { key: 'note', title: 'Ghi chú', width: 300, value: (r) => r.note },
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
 * Màn hình Danh mục đơn vị mua – DVM (lát Y5a, design.md mục 19.3 – Q3): đơn vị được giao mua từng dòng trên bảng tổng hợp 2A/2B (ví dụ
 * VP NDD, Phòng CUVT). Người có quyền "Thêm, sửa đơn vị mua" tự quản lý như danh mục bộ phận. Không xóa; không dùng nữa thì ngừng dùng.
 */
export function PurchasingUnitListPage() {
  const { workingCompany, hasPermission } = useAuth()
  const units = usePurchasingUnits()
  const [status, setStatus] = useState('')
  // undefined = đóng; null = thêm mới; còn lại = đơn vị đang mở.
  const [open, setOpen] = useState<PurchasingUnit | null | undefined>(undefined)

  const rows = useMemo(
    () => (units.data ?? []).filter((u) => !status || (status === 'active') === u.isActive),
    [units.data, status],
  )

  return (
    <>
      <ListPage
        title={`Danh mục đơn vị mua – ${workingCompany?.code ?? ''}`}
        exportFileName="don-vi-mua"
        rows={rows}
        columns={columns}
        rowKey={(r) => r.id}
        auditEntity="PurchasingUnit"
        loading={units.isLoading}
        canCreate={hasPermission(Permission.UnitManage)}
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
      {open !== undefined && <PurchasingUnitDrawer unit={open} onClose={() => setOpen(undefined)} />}
    </>
  )
}
