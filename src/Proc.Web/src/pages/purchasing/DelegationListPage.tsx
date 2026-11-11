import { useMemo, useState } from 'react'
import { delegationStatusLabels, useApprovalDelegations, type ApprovalDelegation, type DelegationStatus } from '../../api/delegations'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { Select } from '../../classic/Select'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { DelegationDrawer } from './DelegationDrawer'

/** Màu đèn trạng thái: đang hiệu lực xanh, chưa hiệu lực vàng, còn lại xám. */
const statusTone = (s: DelegationStatus) => (s === 'Active' ? 'success' : s === 'Upcoming' ? 'warning' : 'neutral')

const columns: GridColumn<ApprovalDelegation>[] = [
  { key: 'delegator', title: 'Người ủy quyền', width: 200, value: (r) => r.delegatorName, groupable: true },
  { key: 'delegate', title: 'Người được ủy quyền', width: 200, value: (r) => r.delegateName, groupable: true },
  { key: 'docType', title: 'Loại phiếu', width: 170, value: (r) => r.docTypeName, groupable: true },
  { key: 'from', title: 'Từ ngày', width: 95, type: 'date', value: (r) => r.validFrom },
  { key: 'to', title: 'Đến ngày', width: 95, type: 'date', value: (r) => r.validTo },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 120,
    type: 'status',
    value: (r) => delegationStatusLabels[r.status],
    tone: (r) => statusTone(r.status),
    groupable: true,
  },
  { key: 'reason', title: 'Lý do', width: 240, value: (r) => r.reason },
  { key: 'note', title: 'Ghi chú', width: 200, value: (r) => r.note },
  { key: 'createdBy', title: 'Người lập', width: 150, value: (r) => r.createdByName },
  { key: 'createdAt', title: 'Ngày lập', width: 130, type: 'datetime', value: (r) => r.createdAt },
]

/**
 * Màn hình Ủy quyền duyệt (UQD – lát O2, design.md mục 19.3). Người giữ cấp duyệt tự ủy quyền cho người khác duyệt thay trong thời gian vắng
 * (tối đa 90 ngày một lần); người có quyền quản lý ủy quyền xem toàn công ty, lập thay, thu hồi. Người được ủy quyền thấy phiếu chờ duyệt
 * thay ở Việc cần làm.
 */
export function DelegationListPage() {
  const { hasPermission } = useAuth()
  const delegations = useApprovalDelegations()
  const [status, setStatus] = useState('')
  // undefined = đóng; null = lập mới; còn lại = ủy quyền đang mở.
  const [open, setOpen] = useState<ApprovalDelegation | null | undefined>(undefined)
  const seesAll = hasPermission(Permission.DelegationManage)

  const rows = useMemo(() => (delegations.data ?? []).filter((d) => !status || d.status === status), [delegations.data, status])

  return (
    <>
      <ListPage
        title={seesAll ? 'Ủy quyền duyệt – toàn công ty' : 'Ủy quyền duyệt của tôi'}
        exportFileName="uy-quyen-duyet"
        rows={rows}
        columns={columns}
        rowKey={(r) => r.id}
        auditEntity="ApprovalDelegation"
        loading={delegations.isLoading}
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
                options={[{ value: '', label: 'Tất cả' }, ...Object.entries(delegationStatusLabels).map(([value, label]) => ({ value, label }))]}
              />
            )}
          </FilterField>
        }
      />
      {open !== undefined && <DelegationDrawer delegation={open} onClose={() => setOpen(undefined)} />}
    </>
  )
}
