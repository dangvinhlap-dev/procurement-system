import { Alert, Select } from 'antd'
import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router'
import { soleSourceStateLabel, useSoleSources, type SoleSourceListItem } from '../../api/soleSources'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { SoleSourceDrawer } from './SoleSourceDrawer'

/** Số ngày trước hạn đánh giá lại bắt đầu nhắc "sắp đến hạn" (giống cảnh báo bảng giá sắp hết hiệu lực). */
const reviewWarningDays = 30

/** Màu đèn theo trạng thái hiển thị; luôn kèm chữ (docs/ui-style.md mục 2). */
function tone(r: SoleSourceListItem): 'success' | 'warning' | 'error' | 'neutral' {
  if (r.isOverdue) return 'error'
  if (r.status === 'Submitted') return 'warning'
  if (r.isEffective) return r.daysToReview !== null && r.daysToReview <= reviewWarningDays ? 'warning' : 'success'
  return 'neutral'
}

const columns: GridColumn<SoleSourceListItem>[] = [
  { key: 'no', title: 'Số phiếu', width: 150, value: (r) => r.no },
  {
    key: 'status', title: 'Trạng thái', width: 170, type: 'status', value: (r) => soleSourceStateLabel(r), tone, groupable: true,
  },
  { key: 'supplierCode', title: 'Mã NCC', width: 110, value: (r) => r.supplierCode },
  { key: 'supplierName', title: 'Tên NCC', width: 220, value: (r) => r.supplierName, groupable: true },
  { key: 'itemCode', title: 'Mã hàng', width: 120, value: (r) => r.itemCode },
  { key: 'itemName', title: 'Tên hàng', width: 220, value: (r) => r.itemName },
  { key: 'from', title: 'Hiệu lực từ', width: 100, type: 'date', value: (r) => r.validFrom },
  { key: 'review', title: 'Hạn đánh giá lại', width: 120, type: 'date', value: (r) => r.reviewDate },
  { key: 'days', title: 'Còn (ngày)', width: 90, type: 'quantity', value: (r) => r.daysToReview },
  { key: 'creator', title: 'Người lập', width: 150, value: (r) => r.createdByName, groupable: true },
]

/**
 * Màn hình NCC độc quyền (NDQ – design.md mục 18.9, đề xuất Ban CUVT N6): phiếu giải trình mặt hàng chỉ mua được của một NCC tại công ty
 * đang làm việc, duyệt riêng, đánh giá lại định kỳ. Mở từ "Việc cần làm"/thông báo bằng `?mo=<id>`.
 */
export function SoleSourceListPage() {
  const { hasPermission } = useAuth()
  const list = useSoleSources()
  const [params, setParams] = useSearchParams()
  const [state, setState] = useState('')
  // "mo" = phiếu đang mở; "new" = đang lập mới.
  const openId = params.get('mo')
  const open = (id: string | null) => setParams(id ? { mo: id } : {}, { replace: true })

  const rows = useMemo(
    () => (list.data ?? []).filter((r) => !state || soleSourceStateLabel(r) === state),
    [list.data, state],
  )
  const dueSoon = (list.data ?? []).filter((r) => r.isOverdue || (r.isEffective && (r.daysToReview ?? 999) <= reviewWarningDays)).length

  return (
    <>
      <ListPage
        title="NCC độc quyền"
        exportFileName="ncc-doc-quyen"
        rows={rows}
        columns={columns}
        rowKey={(r) => r.id}
        auditEntity="SoleSourceApproval"
        loading={list.isLoading}
        canCreate={hasPermission(Permission.SoleSourceManage)}
        onCreate={() => open('new')}
        onOpenRow={(r) => open(r.id)}
        isMarked={(r) => r.status === 'Submitted'}
        onClearFilters={() => setState('')}
        notice={
          dueSoon > 0 ? (
            <Alert type="warning" showIcon
              title={`${dueSoon} phiếu quá hạn hoặc sắp đến hạn đánh giá lại (trong ${reviewWarningDays} ngày) – mở phiếu, bấm "Đánh giá lại".`} />
          ) : undefined
        }
        filters={
          <FilterField label="Trạng thái" width={170}>
            {(id) => (
              <Select
                id={id}
                value={state}
                onChange={setState}
                options={['', 'Nháp', 'Chờ duyệt', 'Đang hiệu lực', 'Chưa tới ngày hiệu lực', 'Quá hạn đánh giá lại', 'Đã kết thúc'].map((v) => ({
                  value: v,
                  label: v || 'Tất cả',
                }))}
              />
            )}
          </FilterField>
        }
      />
      {openId && <SoleSourceDrawer id={openId === 'new' ? null : openId} onClose={() => open(null)} onOpen={open} />}
    </>
  )
}
