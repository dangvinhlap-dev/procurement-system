import { useMemo, useState } from 'react'
import {
  constructionStatusLabels,
  useConstructionProjects,
  type ConstructionProject,
  type ConstructionProjectStatus,
} from '../../api/constructionProjects'
import { useDepartments } from '../../api/organization'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { Select } from '../../classic/Select'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { ConstructionProjectDrawer } from './ConstructionProjectDrawer'

/** Màu đèn trạng thái: đang thực hiện xanh, tạm ngừng vàng, hoàn thành xám. */
const statusTone = (s: ConstructionProjectStatus) => (s === 'InProgress' ? 'success' : s === 'Suspended' ? 'warning' : 'neutral')

const columns: GridColumn<ConstructionProject>[] = [
  { key: 'code', title: 'Mã công trình', width: 120, value: (r) => r.code },
  { key: 'name', title: 'Tên công trình', width: 260, value: (r) => r.name },
  { key: 'department', title: 'Phòng ban đề xuất', width: 180, value: (r) => `${r.departmentCode} – ${r.departmentName}`, groupable: true },
  { key: 'location', title: 'Địa điểm', width: 150, value: (r) => r.location },
  { key: 'estimate', title: 'Tổng dự toán', width: 130, type: 'money', value: (r) => r.estimateAmount },
  { key: 'spent', title: 'Lũy kế đã mua', width: 130, type: 'money', value: (r) => r.spentAmount },
  { key: 'spentVat', title: 'Lũy kế gồm VAT', width: 130, type: 'money', value: (r) => r.spentAmountWithVat },
  { key: 'remaining', title: 'Còn lại', width: 130, type: 'money', value: (r) => r.remainingAmount },
  { key: 'currency', title: 'Tiền tệ', width: 70, value: () => 'VND' },
  { key: 'initial', title: 'Dự toán ban đầu', width: 130, type: 'money', value: (r) => r.initialEstimateAmount },
  { key: 'revisions', title: 'Số lần điều chỉnh', width: 90, type: 'quantity', value: (r) => r.revisionCount },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 120,
    type: 'status',
    value: (r) => constructionStatusLabels[r.status],
    tone: (r) => statusTone(r.status),
    groupable: true,
  },
  { key: 'start', title: 'Khởi công', width: 95, type: 'date', value: (r) => r.startDate },
  { key: 'end', title: 'Dự kiến hoàn thành', width: 95, type: 'date', value: (r) => r.expectedEndDate },
  { key: 'note', title: 'Ghi chú', width: 200, value: (r) => r.note },
  { key: 'createdBy', title: 'Người tạo', width: 150, value: (r) => r.createdByName },
  { key: 'createdAt', title: 'Ngày tạo', width: 130, type: 'datetime', value: (r) => r.createdAt },
]

/**
 * Màn hình Công trình – dự toán (CTDT – lát Y2, design.md mục 19.2). Danh mục công trình của công ty đang làm việc với tổng dự toán chưa VAT –
 * khung chi phí của đề nghị mua vật tư công trình (loại A, biểu mẫu 1A). Dự toán chỉ đổi qua "Điều chỉnh dự toán" (có lý do, lưu lịch sử).
 */
export function ConstructionProjectListPage() {
  const { hasPermission } = useAuth()
  const projects = useConstructionProjects()
  const departments = useDepartments()
  const [status, setStatus] = useState('')
  const [departmentId, setDepartmentId] = useState('')
  // undefined = đóng; null = tạo mới; còn lại = công trình đang mở.
  const [open, setOpen] = useState<ConstructionProject | null | undefined>(undefined)
  const canManage = hasPermission(Permission.ProjectManage)

  const rows = useMemo(
    () => (projects.data ?? []).filter((p) => (!status || p.status === status) && (!departmentId || p.departmentId === departmentId)),
    [projects.data, status, departmentId],
  )

  return (
    <>
      <ListPage
        title="Công trình – dự toán"
        exportFileName="cong-trinh-du-toan"
        rows={rows}
        columns={columns}
        rowKey={(r) => r.id}
        auditEntity="ConstructionProject"
        loading={projects.isLoading}
        canCreate={canManage}
        onCreate={() => setOpen(null)}
        onOpenRow={setOpen}
        onClearFilters={() => {
          setStatus('')
          setDepartmentId('')
        }}
        filters={
          <>
            <FilterField label="Trạng thái" width={140}>
              {(id) => (
                <Select
                  id={id}
                  value={status}
                  onChange={setStatus}
                  options={[{ value: '', label: 'Tất cả' }, ...Object.entries(constructionStatusLabels).map(([value, label]) => ({ value, label }))]}
                />
              )}
            </FilterField>
            <FilterField label="Phòng ban" width={200}>
              {(id) => (
                <Select
                  id={id}
                  value={departmentId}
                  onChange={setDepartmentId}
                  options={[
                    { value: '', label: 'Tất cả' },
                    ...(departments.data ?? []).map((d) => ({ value: d.id, label: `${d.code} – ${d.name}` })),
                  ]}
                />
              )}
            </FilterField>
          </>
        }
      />
      {open !== undefined && <ConstructionProjectDrawer project={open} onClose={() => setOpen(undefined)} />}
    </>
  )
}
