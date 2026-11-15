import { ReloadOutlined } from '@ant-design/icons'
import { Alert, Button } from 'antd'
import { useSystemOverview } from '../../api/operations'
import { ListPage } from '../../components/ListPage/ListPage'
import { formatDateTime } from '../../utils/format'
import { overviewColumns } from './overviewColumns'

/** Tên môi trường trên tiêu đề thông báo. */
const environmentLabels: Record<string, string> = { Dev: 'DEV', Test: 'TEST', Prod: 'PROD', None: 'Chưa khai' }

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
      columns={overviewColumns}
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
