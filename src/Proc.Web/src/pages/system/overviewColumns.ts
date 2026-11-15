import type { OverviewStatus, SystemOverviewRow } from '../../api/operations'
import type { GridColumn, StatusTone } from '../../components/DataGrid/types'

/** Đèn và chữ của tình trạng – màu không là thông tin duy nhất (ui-style.md mục 2). */
export const statusLabels: Record<OverviewStatus, { label: string; tone: StatusTone }> = {
  Ok: { label: 'Ổn', tone: 'success' },
  Warning: { label: 'Cần xem', tone: 'warning' },
  Error: { label: 'Lỗi', tone: 'error' },
  Neutral: { label: 'Thông tin', tone: 'neutral' },
}

/** Cột bảng "nhóm – hạng mục – giá trị – đèn – ghi chú" dùng chung cho SYS và BAK (tab Lưu WAL). */
export const overviewColumns: GridColumn<SystemOverviewRow>[] = [
  { key: 'group', title: 'Nhóm', width: 120, value: (r) => r.group, groupable: true },
  { key: 'item', title: 'Hạng mục', width: 200, value: (r) => r.item },
  { key: 'value', title: 'Giá trị', width: 280, value: (r) => r.value },
  {
    key: 'status',
    title: 'Tình trạng',
    width: 100,
    type: 'status',
    value: (r) => statusLabels[r.status]?.label ?? r.status,
    tone: (r) => statusLabels[r.status]?.tone ?? 'neutral',
    groupable: true,
  },
  { key: 'note', title: 'Ghi chú', width: 420, value: (r) => r.note },
]
