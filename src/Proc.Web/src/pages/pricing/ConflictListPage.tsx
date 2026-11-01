import { Select } from 'antd'
import { useMemo, useState } from 'react'
import { conflictRelationLabels, useConflictDeclarations, type ConflictDeclaration } from '../../api/conflicts'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { ConflictDrawer } from './ConflictDrawer'

const columns: GridColumn<ConflictDeclaration>[] = [
  { key: 'user', title: 'Người kê khai', width: 200, value: (r) => `${r.userFullName} (${r.userName})`, groupable: true },
  { key: 'supplierCode', title: 'Mã NCC', width: 110, value: (r) => r.supplierCode },
  { key: 'supplierName', title: 'Tên NCC', width: 240, value: (r) => r.supplierName, groupable: true },
  { key: 'relation', title: 'Loại quan hệ', width: 210, value: (r) => conflictRelationLabels[r.relation], groupable: true },
  { key: 'description', title: 'Mô tả', width: 260, value: (r) => r.description },
  { key: 'from', title: 'Hiệu lực từ', width: 100, type: 'date', value: (r) => r.validFrom },
  { key: 'to', title: 'Đến', width: 100, type: 'date', value: (r) => r.validTo },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 120,
    type: 'status',
    value: (r) => (r.isActive ? 'Đang hiệu lực' : r.validTo ? 'Đã kết thúc' : 'Chưa hiệu lực'),
    tone: (r) => (r.isActive ? 'warning' : 'neutral'),
    groupable: true,
  },
  { key: 'createdBy', title: 'Người ghi nhận', width: 160, value: (r) => r.createdByName },
  { key: 'createdAt', title: 'Ngày ghi nhận', width: 130, type: 'datetime', value: (r) => r.createdAt },
]

/**
 * Màn hình Kê khai xung đột lợi ích (COI – design.md mục 18.8, đề xuất Ban CUVT N4). Mọi người tự kê khai quan hệ với NCC và xem kê khai
 * của mình; KSNB/Trưởng ban (quyền xem mọi kê khai) thấy toàn bộ để kiểm tra. Kê khai đang hiệu lực chặn người đó duyệt phiếu, chấm thầu,
 * chọn thầu có NCC tương ứng.
 */
export function ConflictListPage() {
  const { hasPermission } = useAuth()
  const declarations = useConflictDeclarations()
  const [status, setStatus] = useState('')
  // undefined = đóng; null = kê khai mới; còn lại = kê khai đang mở.
  const [open, setOpen] = useState<ConflictDeclaration | null | undefined>(undefined)
  const seesAll = hasPermission(Permission.ConflictView) || hasPermission(Permission.ConflictManage)

  const rows = useMemo(
    () => (declarations.data ?? []).filter((d) => !status || (status === 'active') === d.isActive),
    [declarations.data, status],
  )

  return (
    <>
      <ListPage
        title={seesAll ? 'Kê khai xung đột lợi ích – toàn tập đoàn' : 'Kê khai xung đột lợi ích của tôi'}
        exportFileName="ke-khai-xung-dot-loi-ich"
        rows={rows}
        columns={columns}
        rowKey={(r) => r.id}
        loading={declarations.isLoading}
        canCreate
        onCreate={() => setOpen(null)}
        onOpenRow={setOpen}
        onClearFilters={() => setStatus('')}
        filters={
          <FilterField label="Trạng thái" width={140}>
            {(id) => (
              <Select
                id={id}
                value={status}
                onChange={setStatus}
                options={[
                  { value: '', label: 'Tất cả' },
                  { value: 'active', label: 'Đang hiệu lực' },
                  { value: 'inactive', label: 'Không hiệu lực' },
                ]}
              />
            )}
          </FilterField>
        }
      />
      {open !== undefined && <ConflictDrawer declaration={open} onClose={() => setOpen(undefined)} />}
    </>
  )
}
