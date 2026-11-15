import { ReloadOutlined } from '@ant-design/icons'
import { Alert, Button } from 'antd'
import { useWalArchiveStatus, type WalArchiveStatus } from '../../api/operations'
import { ListPage } from '../../components/ListPage/ListPage'
import { formatDateTime } from '../../utils/format'
import { overviewColumns } from './overviewColumns'

/** Kiểu thông báo đầu trang theo đèn của trạng thái lưu WAL. */
function alertType(status: WalArchiveStatus['status']) {
  return status === 'Error' ? 'error' : status === 'Warning' ? 'warning' : status === 'Ok' ? 'success' : 'info'
}

/**
 * Màn hình BAK – Sao lưu (design.md mục 21.4, 21.5). Lát QT3a: tình trạng lưu WAL liên tục của máy chủ CSDL – trạng thái (Chưa bật / Chờ khởi
 * động lại / Đang chạy / Trễ > 15 phút / Có lỗi) kèm đèn và chữ, tham số PostgreSQL cần cho lưu WAL và sao lưu Diff, thống kê chép, chỗ trống ổ
 * thư mục WAL, tài khoản bảo trì; mỗi dòng đỏ / vàng ghi cách xử lý. Lát QT3b thêm lịch sử sao lưu, lịch, nơi lưu, nút "Sao lưu ngay".
 * Chỉ ở tổng công ty, quyền System.OpsBackup.View.
 */
export function BackupPage() {
  const wal = useWalArchiveStatus()
  const data = wal.data
  return (
    <ListPage
      title="Sao lưu"
      exportFileName="sao-luu-luu-wal"
      exportSubtitle="Tình trạng lưu WAL"
      rows={data?.rows ?? []}
      columns={overviewColumns}
      rowKey={(r) => `${r.group}|${r.item}`}
      loading={wal.isLoading}
      canCreate={false}
      actions={
        <Button icon={<ReloadOutlined />} loading={wal.isFetching} onClick={() => void wal.refetch()} title="Lấy lại tình trạng lưu WAL">
          Làm mới
        </Button>
      }
      notice={
        wal.error ? (
          <Alert type="error" showIcon title={wal.error.message} />
        ) : data ? (
          <Alert
            type={alertType(data.status)}
            showIcon
            title={`Lưu WAL: ${data.stateLabel} · số liệu lúc ${formatDateTime(data.checkedAt)}`}
            description={data.summary}
          />
        ) : undefined
      }
    />
  )
}
