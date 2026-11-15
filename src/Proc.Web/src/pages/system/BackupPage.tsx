import { CloudUploadOutlined, DownOutlined, ReloadOutlined } from '@ant-design/icons'
import { Alert, App, Button, Dropdown } from 'antd'
import { useState } from 'react'
import {
  backupKindLabels,
  useBackupOverview,
  useBackupRuns,
  useQueueBackup,
  type BackupKind,
  type BackupRun,
} from '../../api/backup'
import { defaultLogRange } from '../../api/logs'
import { useWalArchiveStatus, type SystemOverviewRow, type WalArchiveStatus } from '../../api/operations'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { ListPage } from '../../components/ListPage/ListPage'
import { formatDateTime, formatQuantity } from '../../utils/format'
import { LogDateRangeFilter, TruncatedNotice } from './logFilters'
import { overviewColumns, statusLabels } from './overviewColumns'

/** Kiểu thông báo đầu trang theo đèn của trạng thái lưu WAL. */
function alertType(status: WalArchiveStatus['status']) {
  return status === 'Error' ? 'error' : status === 'Warning' ? 'warning' : status === 'Ok' ? 'success' : 'info'
}

/** Dung lượng dễ đọc kiểu Việt Nam (1.536 MB → "1,5 GB"), như SYS. */
function formatBytes(bytes: number | null): string {
  if (bytes == null) return ''
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let value = bytes
  let unit = 0
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }
  return `${value.toLocaleString('vi-VN', { maximumFractionDigits: unit === 0 ? 0 : 1, minimumFractionDigits: unit === 0 ? 0 : 1 })} ${units[unit]}`
}

/** Thời gian chạy: "45 giây", "12 phút 5 giây", "1 giờ 20 phút". */
function formatDuration(seconds: number | null): string {
  if (seconds == null) return ''
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  const s = seconds % 60
  if (h > 0) return `${h} giờ ${m} phút`
  return m > 0 ? `${m} phút ${s} giây` : `${s} giây`
}

/** Chữ kết quả kiểm tra / chép nơi thứ hai (null = chưa kiểm tra / không khai). */
function yesNo(value: boolean | null, yes: string, no: string, none: string) {
  return value == null ? none : value ? yes : no
}

/** Cột tab Lịch sử: đèn kèm chữ; dung lượng, số file canh phải phông đơn cách (ui-style.md mục 3). */
const runColumns: GridColumn<BackupRun>[] = [
  { key: 'queuedAt', title: 'Vào hàng chờ', width: 125, type: 'datetime', value: (r) => r.queuedAt },
  { key: 'kind', title: 'Kiểu', width: 80, value: (r) => r.kindLabel, groupable: true },
  {
    key: 'status',
    title: 'Tình trạng',
    width: 190,
    type: 'status',
    value: (r) => r.statusLabel,
    tone: (r) => statusLabels[r.light]?.tone ?? 'neutral',
    groupable: true,
  },
  {
    key: 'trigger',
    title: 'Nguồn',
    width: 150,
    value: (r) => (r.trigger === 'Schedule' ? 'Theo lịch' : `Sao lưu ngay – ${r.requestedByName ?? ''}`),
    groupable: true,
  },
  { key: 'startedAt', title: 'Bắt đầu', width: 125, type: 'datetime', value: (r) => r.startedAt },
  { key: 'duration', title: 'Thời gian chạy', width: 110, value: (r) => formatDuration(r.durationSeconds) },
  { key: 'size', title: 'Dung lượng', width: 95, type: 'quantity', value: (r) => r.sizeBytes, render: (r) => formatBytes(r.sizeBytes) },
  { key: 'files', title: 'Số file', width: 75, type: 'quantity', value: (r) => r.fileCount, render: (r) => formatQuantity(r.fileCount) },
  { key: 'verified', title: 'Kiểm tra', width: 90, value: (r) => yesNo(r.verified, 'Đạt', 'Không đạt', '') },
  { key: 'secondary', title: 'Nơi thứ hai', width: 100, value: (r) => yesNo(r.secondaryCopied, 'Đã chép', 'Chưa chép', 'Không khai') },
  { key: 'path', title: 'Đường dẫn', width: 240, value: (r) => r.relativePath },
  {
    key: 'note',
    title: 'Ghi chú',
    width: 420,
    value: (r) => [r.errorMessage, r.verifyMessage, r.secondaryMessage, r.note].filter(Boolean).join(' · '),
  },
  { key: 'wal', title: 'File WAL đầu', width: 190, value: (r) => r.startWalFile },
  { key: 'sha', title: 'SHA256', width: 200, value: (r) => r.sha256 },
  { key: 'purgedAt', title: 'Đã dọn lúc', width: 125, type: 'datetime', value: (r) => r.purgedAt },
]

type BackupTab = 'history' | 'wal' | 'schedule' | 'storage'

const tabItems: { key: BackupTab; label: string }[] = [
  { key: 'history', label: 'Lịch sử' },
  { key: 'wal', label: 'Lưu WAL' },
  { key: 'schedule', label: 'Lịch và thời hạn giữ' },
  { key: 'storage', label: 'Nơi lưu' },
]

const runKinds: BackupKind[] = ['Full', 'Diff', 'Dump', 'Attachments']

/** Mô tả từng kiểu trong menu "Sao lưu ngay". */
const runKindHints: Record<BackupKind, string> = {
  Full: 'Full – chụp cả máy chủ CSDL',
  Diff: 'Diff – phần đổi từ bản Full gần nhất',
  Dump: 'pg_dump – bản logic của CSDL ứng dụng',
  Attachments: 'Đính kèm – chép bổ sung file mới',
}

/**
 * Màn hình BAK – Sao lưu (design.md mục 21.4, 21.5; ui-style.md mục 5l). Bốn tab trên khuôn ListPage + DataGrid:
 * - **Lịch sử**: các lần sao lưu (Full / Diff / pg_dump / đính kèm) theo khoảng ngày – tình trạng đèn kèm chữ, dung lượng, kiểm tra, nơi thứ hai;
 *   có lần đang chờ / đang chạy thì tự làm mới mỗi 5 giây.
 * - **Lưu WAL** (lát QT3a): tình trạng lưu WAL liên tục, tham số PostgreSQL, mỗi dòng đỏ / vàng ghi cách xử lý.
 * - **Lịch và thời hạn giữ**, **Nơi lưu** (lát QT3b): lịch B1, lượt kế tiếp, số bản đang giữ; thư mục, chỗ trống ổ, công cụ PostgreSQL.
 * Nút "Sao lưu ngay" (icon xanh, quyền System.OpsBackup.Run) chọn kiểu rồi đặt vào hàng chờ. Không có nút tải file sao lưu (Q5).
 * Chỉ ở tổng công ty, quyền System.OpsBackup.View.
 */
export function BackupPage() {
  const { message } = App.useApp()
  const { hasPermission } = useAuth()
  const canRun = hasPermission(Permission.BackupRun)
  const [tab, setTab] = useState<BackupTab>('history')
  const [range, setRange] = useState(defaultLogRange)
  const runs = useBackupRuns(range)
  const wal = useWalArchiveStatus()
  const overview = useBackupOverview()
  const queue = useQueueBackup()

  const runNow = async (kind: BackupKind) => {
    try {
      await queue.mutateAsync(kind)
      message.success(`Đã đặt sao lưu ${backupKindLabels[kind]} vào hàng chờ – kết quả hiện ở tab Lịch sử.`)
      setTab('history')
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Không đặt được sao lưu.')
    }
  }

  const refresh = () => {
    if (tab === 'history') void runs.refetch()
    else if (tab === 'wal') void wal.refetch()
    else void overview.refetch()
  }

  const viewTabs = { items: tabItems, activeKey: tab, onChange: (k: string) => setTab(k as BackupTab) }
  const actions = (
    <>
      {canRun && (
        <Dropdown
          trigger={['click']}
          menu={{ items: runKinds.map((k) => ({ key: k, label: runKindHints[k] })), onClick: ({ key }) => void runNow(key as BackupKind) }}
        >
          <Button icon={<CloudUploadOutlined />} loading={queue.isPending} title="Đặt một lần sao lưu vào hàng chờ, chạy lần lượt">
            Sao lưu ngay <DownOutlined />
          </Button>
        </Dropdown>
      )}
      <Button
        icon={<ReloadOutlined />}
        loading={runs.isFetching || wal.isFetching || overview.isFetching}
        onClick={refresh}
        title="Lấy lại số liệu của tab đang xem"
      >
        Làm mới
      </Button>
    </>
  )

  if (tab === 'history') {
    return (
      <ListPage<BackupRun>
        key="history"
        title="Sao lưu"
        viewTabs={viewTabs}
        exportFileName="sao-luu-lich-su"
        exportSubtitle={`Lịch sử sao lưu từ ${range.from} đến ${range.to}`}
        rows={runs.data?.items ?? []}
        columns={runColumns}
        rowKey={(r) => r.id}
        loading={runs.isLoading}
        canCreate={false}
        actions={actions}
        filters={<LogDateRangeFilter value={range} onChange={setRange} />}
        onClearFilters={() => setRange(defaultLogRange())}
        isMarked={(r) => r.light === 'Error'}
        notice={
          runs.error ? <Alert type="error" showIcon title={runs.error.message} /> : <TruncatedNotice truncated={runs.data?.truncated} />
        }
      />
    )
  }

  const rows: SystemOverviewRow[] =
    tab === 'wal' ? (wal.data?.rows ?? []) : tab === 'schedule' ? (overview.data?.scheduleRows ?? []) : (overview.data?.storageRows ?? [])
  const error = tab === 'wal' ? wal.error : overview.error
  return (
    <ListPage<SystemOverviewRow>
      key={tab}
      title="Sao lưu"
      viewTabs={viewTabs}
      exportFileName={tab === 'wal' ? 'sao-luu-luu-wal' : tab === 'schedule' ? 'sao-luu-lich' : 'sao-luu-noi-luu'}
      exportSubtitle={tabItems.find((t) => t.key === tab)?.label}
      rows={rows}
      columns={overviewColumns}
      rowKey={(r) => `${r.group}|${r.item}`}
      loading={tab === 'wal' ? wal.isLoading : overview.isLoading}
      canCreate={false}
      actions={actions}
      notice={
        error ? (
          <Alert type="error" showIcon title={error.message} />
        ) : tab === 'wal' && wal.data ? (
          <Alert
            type={alertType(wal.data.status)}
            showIcon
            title={`Lưu WAL: ${wal.data.stateLabel} · số liệu lúc ${formatDateTime(wal.data.checkedAt)}`}
            description={wal.data.summary}
          />
        ) : undefined
      }
    />
  )
}
