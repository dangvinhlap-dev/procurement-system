import { Select } from 'antd'
import { useMemo, useState } from 'react'
import { useBudgetYear, type DepartmentBudgetYear } from '../../api/purchasing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { BudgetDrawer } from './BudgetDrawer'

/** Số liệu hiện ở 12 cột tháng. */
type BudgetView = 'budget' | 'used' | 'remaining'

const VIEW_LABELS: Record<BudgetView, string> = { budget: 'Ngân sách', used: 'Đã dùng', remaining: 'Còn lại' }

/**
 * Cột của lưới: mã, tên bộ phận, tình trạng (số tháng vượt), cả năm, rồi 12 tháng theo số liệu đang chọn – tình trạng đặt trước để
 * thấy ngay mà không phải cuộn ngang.
 * @param view Ngân sách / Đã dùng / Còn lại.
 */
function buildColumns(view: BudgetView): GridColumn<DepartmentBudgetYear>[] {
  const months: GridColumn<DepartmentBudgetYear>[] = Array.from({ length: 12 }, (_, i) => ({
    key: `m${i + 1}`,
    title: `T${String(i + 1).padStart(2, '0')}`,
    width: 96,
    type: 'money' as const,
    auditField: false as const,
    value: (r: DepartmentBudgetYear) => r.months[i][view],
  }))
  return [
    { key: 'code', title: 'Mã bộ phận', width: 100, value: (r) => r.departmentCode, auditField: false },
    { key: 'name', title: 'Tên bộ phận', width: 180, value: (r) => r.departmentName, auditField: false },
    {
      key: 'total',
      title: view === 'used' ? 'Đã dùng cả năm' : 'Ngân sách cả năm',
      width: 130,
      type: 'money',
      auditField: false,
      value: (r) => (view === 'used' ? r.totalUsed : r.totalBudget),
    },
    {
      key: 'over',
      title: 'Tình trạng',
      width: 140,
      type: 'status',
      auditField: false,
      groupable: true,
      value: (r) => (r.overMonths > 0 ? `Vượt ${r.overMonths} tháng` : r.totalBudget > 0 ? 'Trong ngân sách' : 'Chưa khai'),
      tone: (r) => (r.overMonths > 0 ? 'error' : r.totalBudget > 0 ? 'success' : 'neutral'),
    },
    ...months,
  ]
}

/**
 * Màn hình Ngân sách bộ phận (BUDG – lát C3, design.md mục 18.24): ngân sách mua hàng theo công ty × bộ phận × tháng của công ty đang
 * làm việc, kèm số đã dùng (PO + phần PR chưa lên PO) và còn lại. Bấm đúp một bộ phận để khai 12 tháng. Tháng bỏ trống = không kiểm
 * soát; PR/PO vượt ngân sách không bị chặn mà qua thêm chặng duyệt "Vượt ngân sách" (APRL loại PRB, POB).
 */
export function BudgetListPage() {
  const { workingCompany, hasPermission } = useAuth()
  const [thisYear] = useState(() => new Date().getFullYear())
  const [year, setYear] = useState(thisYear)
  const [view, setView] = useState<BudgetView>('budget')
  const budgets = useBudgetYear(year)
  const [openId, setOpenId] = useState<string | null>(null)
  const columns = useMemo(() => buildColumns(view), [view])
  const rows = budgets.data ?? []
  const open = rows.find((r) => r.departmentId === openId) ?? null

  return (
    <>
      <ListPage
        title={`Ngân sách bộ phận ${year} – ${workingCompany?.code ?? ''}`}
        exportFileName={`ngan-sach-${year}`}
        exportSubtitle={VIEW_LABELS[view]}
        rows={rows}
        columns={columns}
        rowKey={(r) => r.departmentId}
        canCreate={false}
        loading={budgets.isLoading}
        onOpenRow={(r) => setOpenId(r.departmentId)}
        isMarked={(r) => r.overMonths > 0}
        onClearFilters={() => {
          setYear(thisYear)
          setView('budget')
        }}
        notice={
          hasPermission(Permission.BudgetManage)
            ? 'Bấm đúp một bộ phận để khai ngân sách 12 tháng. Tháng bỏ trống là không kiểm soát ngân sách.'
            : undefined
        }
        filters={
          <>
            <FilterField label="Năm" width={90}>
              {(id) => (
                <Select
                  id={id}
                  value={year}
                  onChange={setYear}
                  options={Array.from({ length: 6 }, (_, i) => thisYear - 3 + i).map((y) => ({ value: y, label: String(y) }))}
                />
              )}
            </FilterField>
            <FilterField label="Hiển thị" width={120}>
              {(id) => (
                <Select
                  id={id}
                  value={view}
                  onChange={setView}
                  options={(Object.keys(VIEW_LABELS) as BudgetView[]).map((v) => ({ value: v, label: VIEW_LABELS[v] }))}
                />
              )}
            </FilterField>
          </>
        }
      />
      {open && <BudgetDrawer year={year} row={open} onClose={() => setOpenId(null)} />}
    </>
  )
}
