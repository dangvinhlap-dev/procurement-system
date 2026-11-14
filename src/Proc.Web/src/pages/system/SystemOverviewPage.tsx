import { ReloadOutlined } from '@ant-design/icons'
import { Alert, Button } from 'antd'
import { useSystemOverview, type OverviewStatus, type SystemOverviewRow } from '../../api/operations'
import type { GridColumn, StatusTone } from '../../components/DataGrid/types'
import { ListPage } from '../../components/ListPage/ListPage'
import { formatDateTime } from '../../utils/format'

/** Đèn và chữ của tình trạng – màu không là thông tin duy nhất (ui-style.md mục 2). */
const statusLabels: Record<OverviewStatus, { label: string; tone: StatusTone }> = {
  Ok: { label: 'Ổn', tone: 'success' },
  Warning: { label: 'Cần xem', tone: 'warning' },
  Error: { label: 'Lỗi', tone: 'error' },
  Neutral: { label: 'Thông tin', tone: 'neutral' },
}

/** Tên môi trường trên tiêu đề thông báo. */
const environmentLabels: Record<string, string> = { Dev: 'DEV', Test: 'TEST', Prod: 'PROD', None: 'Chưa khai' }

const columns: GridColumn<SystemOverviewRow>[] = [
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

/**
 * Màn hình SYS – Tổng quan hệ thống (design.md mục 21.4, lát QT1): số liệu của môi trường đang chạy – phiên bản, migration đã chạy / đang chờ,
 * dung lượng CSDL, số kết nối, chỗ trống ổ đĩa, nhịp dịch vụ nền, cách gửi email; dòng đỏ / vàng ghi lý do và cách xử lý. Chỉ ở tổng công ty,
 * quyền System.Ops.View. Bảng ba môi trường (TEST / PROD) thêm ở lát QT8.
 */
export function SystemOverviewPage() {
  const overview = useSystemOverview()
  const data = overview.data
  const problems = data?.rows.filter((r) => r.status === 'Error' || r.status === 'Warning').length ?? 0
  return (
    <ListPage
      title="Tổng quan hệ thống"
      exportFileName="tong-quan-he-thong"
      rows={data?.rows ?? []}
      columns={columns}
      rowKey={(r) => `${r.group}|${r.item}`}
      loading={overview.isLoading}
      canCreate={false}
      actions={
        <Button icon={<ReloadOutlined />} loading={overview.isFetching} onClick={() => void overview.refetch()} title="Lấy lại số liệu">
          Làm mới
        </Button>
      }
      notice={
        overview.error ? (
          <Alert type="error" showIcon title={overview.error.message} />
        ) : data ? (
          <Alert
            type={problems > 0 ? 'warning' : 'info'}
            showIcon
            title={`Môi trường ${environmentLabels[data.environment] ?? data.environment} · phiên bản ${data.version} · số liệu lúc ${formatDateTime(data.checkedAt)}${
              problems > 0 ? ` · ${problems} hạng mục cần xem` : ' · mọi hạng mục ổn'
            }`}
          />
        ) : undefined
      }
    />
  )
}
