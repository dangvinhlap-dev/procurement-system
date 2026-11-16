import { ReloadOutlined, SyncOutlined } from '@ant-design/icons'
import { Alert, App, Button, Form, Modal } from 'antd'
import dayjs from 'dayjs'
import { useState } from 'react'
import type { AgentCommand } from '../../api/agent'
import type { DateRange } from '../../api/logs'
import type { SystemOverviewRow } from '../../api/operations'
import {
  TEST_REFRESH_CONFIRMATION,
  useRunTestRefresh,
  useTestRefreshOverview,
  useTestRefreshRuns,
  type TestRefreshOverview,
} from '../../api/testRefresh'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { ListPage } from '../../components/ListPage/ListPage'
import { formatDateTime } from '../../utils/format'
import { DangerFields } from './DangerFields'
import { LogDateRangeFilter } from './logFilters'
import { formatBytes, formatDuration } from './opsFormat'
import { overviewColumns, statusLabels } from './overviewColumns'

type TstTab = 'status' | 'history'

const tabItems: { key: TstTab; label: string }[] = [
  { key: 'status', label: 'Tình trạng' },
  { key: 'history', label: 'Lịch sử' },
]

/** Làm mới thưa (trước mỗi đợt UAT) nên lịch sử mặc định 90 ngày. */
const defaultRange = (): DateRange => ({ from: dayjs().subtract(89, 'day').format('YYYY-MM-DD'), to: dayjs().format('YYYY-MM-DD') })

/** Cột tab Lịch sử: mỗi lần làm mới là một lệnh bộ thực thi RefreshTest. */
const historyColumns: GridColumn<AgentCommand>[] = [
  { key: 'createdAt', title: 'Ra lệnh lúc', width: 125, type: 'datetime', value: (r) => r.createdAt },
  {
    key: 'status',
    title: 'Tình trạng',
    width: 95,
    type: 'status',
    value: (r) => r.statusLabel,
    tone: (r) => statusLabels[r.light]?.tone ?? 'neutral',
  },
  { key: 'createdBy', title: 'Người ra lệnh', width: 110, value: (r) => r.createdByName },
  { key: 'reason', title: 'Lý do', width: 220, value: (r) => r.reason },
  { key: 'startedAt', title: 'Bộ thực thi nhận', width: 125, type: 'datetime', value: (r) => r.startedAt },
  { key: 'duration', title: 'Thời gian chạy', width: 105, value: (r) => r.durationSeconds, render: (r) => formatDuration(r.durationSeconds) },
  { key: 'result', title: 'Kết quả', width: 480, value: (r) => r.resultMessage },
]

/**
 * Dòng tab "Tình trạng": máy nguồn, TEST (liên lạc, phiên bản, khóa dịch vụ), bản pg_dump, bảng giữ theo TEST – cùng cột với SYS.
 * @param o Tổng quan máy chủ trả về.
 */
function statusRows(o: TestRefreshOverview): SystemOverviewRow[] {
  const t = o.test
  const sameVersion = t?.appVersion != null && t.appVersion === o.version
  return [
    {
      group: 'Nguồn dữ liệu',
      item: 'Máy này',
      value: `${o.machineEnvironment} – bản ${o.version}`,
      status: o.readOnlyReason ? 'Warning' : 'Neutral',
      note: o.readOnlyReason ?? 'Dữ liệu TEST sẽ là bản sao nguyên trạng của máy này (không làm mờ – design.md 21.1 E6).',
    },
    {
      group: 'Nguồn dữ liệu',
      item: 'Bản pg_dump',
      value: o.dump
        ? `${o.dump.finishedAt ? formatDateTime(o.dump.finishedAt) : '?'} · ${o.dump.relativePath ?? ''} · ${formatBytes(o.dump.sizeBytes)}`
        : 'Chưa có',
      status: !o.dump ? 'Error' : o.dump.stale ? 'Warning' : 'Ok',
      note: !o.dump
        ? 'BAK → Sao lưu ngay → pg_dump.'
        : o.dump.stale
          ? `Đã ${Math.floor(o.dump.ageHours)} giờ – muốn dữ liệu mới hơn thì BAK → Sao lưu ngay → pg_dump rồi làm mới.`
          : `SHA256 ${o.dump.sha256 ?? ''}`,
    },
    {
      group: 'Máy TEST',
      item: 'Bộ thực thi',
      value: t ? `${t.name} – ${t.contactLabel}${t.lastSeenAt ? ` (liên lạc ${formatDateTime(t.lastSeenAt)})` : ''}` : 'Chưa khai',
      status: t ? t.contactLight : 'Error',
      note: t ? `Bộ thực thi ${t.agentVersion ?? '?'} – làm mới cần bản 1.3.0 trở lên.` : 'Khai môi trường TEST, tạo khóa ở màn hình AGT.',
    },
    {
      group: 'Máy TEST',
      item: 'Phiên bản phần mềm',
      value: t?.appVersion ?? 'Chưa biết',
      status: sameVersion ? 'Ok' : 'Error',
      note: sameVersion
        ? 'Cùng bản với máy này – cấu trúc CSDL khớp.'
        : `Phải cùng bản ${o.version}: cài TEST đúng bản hoặc quay về bản cũ trên TEST ở REL.`,
    },
    {
      group: 'Máy TEST',
      item: 'Khóa dịch vụ',
      value: t?.hasServiceKey ? 'Đã tạo' : 'Chưa tạo',
      status: t?.hasServiceKey ? 'Ok' : 'Warning',
      note: 'Để nút "Báo lỗi / góp ý" trên TEST gửi lỗi về phiếu phát hành ở đây (AGT → Tạo khóa dịch vụ…).',
    },
    {
      group: 'Sau khi làm mới',
      item: 'Giữ theo TEST',
      value: o.keepTables.join(', '),
      status: 'Neutral',
      note: 'Lịch sử sao lưu, bộ thực thi, bảo trì, phục hồi, nhật ký vận hành / lỗi của chính TEST; còn lại (kể cả người dùng, mật khẩu, quyền) như PROD.',
    },
    {
      group: 'Sau khi làm mới',
      item: 'Đặt lại',
      value: 'Thư chờ gửi bỏ gửi · mọi phiên đăng nhập hết hiệu lực',
      status: 'Neutral',
      note: 'Quyền admin cấp thêm trên TEST mất (E8). Cấu hình máy (App:Environment, email chuyển hướng, khóa) nằm trong file nên giữ nguyên.',
    },
  ]
}

/**
 * Hộp thoại "Làm mới TEST từ PROD" – việc nguy hiểm Q6 (ui-style.md mục 5m, 5r): cảnh báo vàng việc bộ thực thi sẽ làm và dữ liệu TEST mất, lý do,
 * mật khẩu, gõ "LÀM MỚI TEST"; nút đỏ. Lỗi máy chủ là khối đỏ trong hộp thoại.
 * @param overview Tổng quan; null = đóng.
 * @param onClose Đóng hộp thoại.
 * @param onQueued Đã giao lệnh.
 */
function RefreshModal({ overview, onClose, onQueued }: { overview: TestRefreshOverview | null; onClose: () => void; onQueued: () => void }) {
  const [form] = Form.useForm<{ reason: string; password: string; confirmation: string }>()
  const [error, setError] = useState<string | null>(null)
  const run = useRunTestRefresh()
  const close = () => {
    setError(null)
    onClose()
  }
  const dumpAt = overview?.dump?.finishedAt ? formatDateTime(overview.dump.finishedAt) : '?'
  return (
    <Modal
      title="Làm mới TEST từ PROD"
      open={overview != null}
      onCancel={close}
      onOk={() => form.submit()}
      okText="Làm mới TEST"
      okButtonProps={{ danger: true, icon: <SyncOutlined /> }}
      cancelText="Không làm mới"
      confirmLoading={run.isPending}
      destroyOnHidden
      width={560}
    >
      <Alert
        type="warning"
        showIcon
        style={{ marginBottom: 8 }}
        title={`Bộ thực thi TEST sẽ: tải bản pg_dump lúc ${dumpAt}, tắt site TEST vài phút, thay toàn bộ dữ liệu TEST bằng dữ liệu PROD, bỏ thư chờ gửi, đăng xuất mọi người, chép bổ sung file đính kèm.`}
        description="Phiếu nhập thử và quyền cấp thêm trên TEST mất; đăng nhập TEST bằng mật khẩu PROD. CSDL TEST trước đó giữ một bản (_prerefresh_) để xem lại."
      />
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        onFinish={async (v) => {
          setError(null)
          try {
            await run.mutateAsync(v)
            onQueued()
            close()
          } catch (e) {
            setError(e instanceof Error ? e.message : 'Không giao được lệnh làm mới.')
          }
        }}
      >
        <DangerFields confirmation={TEST_REFRESH_CONFIRMATION} />
      </Form>
    </Modal>
  )
}

/**
 * Màn hình TST – Làm mới TEST từ PROD (design.md 21.4, 21.5, 21.18 – lát QT9; ui-style.md mục 5r). Khuôn ListPage + DataGrid, hai tab:
 * - **Tình trạng**: nguồn dữ liệu (máy này, bản pg_dump), máy TEST (bộ thực thi, phiên bản, khóa dịch vụ), phần giữ / đặt lại; nút "Làm mới TEST…"
 *   (Q6) khóa khi còn điều chặn, chú thích nêu lý do.
 * - **Lịch sử**: các lần làm mới (lệnh RefreshTest), tự làm mới 5 giây khi còn lệnh chờ / chạy.
 * Chạy trên PROD (DEV thử); máy TEST chỉ xem. Xem cần System.Ops.View hoặc quyền làm mới; làm mới cần System.OpsTestRefresh.Run.
 */
export function TestRefreshPage() {
  const { message } = App.useApp()
  const { hasPermission } = useAuth()
  const canRun = hasPermission(Permission.TestRefreshRun)
  const [tab, setTab] = useState<TstTab>('status')
  const [range, setRange] = useState(defaultRange)
  const [confirming, setConfirming] = useState<TestRefreshOverview | null>(null)
  const overview = useTestRefreshOverview()
  const runs = useTestRefreshRuns(range)
  const o = overview.data
  const viewTabs = { items: tabItems, activeKey: tab, onChange: (k: string) => setTab(k as TstTab) }
  const blocked = o ? (o.blockers.length > 0 ? o.blockers.join(' ') : null) : 'Đang tải tình trạng'

  const actions = (
    <>
      {canRun && !o?.readOnlyReason && (
        <Button icon={<SyncOutlined />} disabled={blocked != null} title={blocked ?? 'Giao bộ thực thi TEST làm mới dữ liệu từ máy này'} onClick={() => setConfirming(o ?? null)}>
          Làm mới TEST…
        </Button>
      )}
      <Button
        icon={<ReloadOutlined />}
        loading={overview.isFetching || runs.isFetching}
        onClick={() => {
          void overview.refetch()
          void runs.refetch()
        }}
        title="Lấy lại số liệu"
      >
        Làm mới
      </Button>
    </>
  )

  const notice = overview.error ? (
    <Alert type="error" showIcon title={overview.error.message} />
  ) : o ? (
    o.openCommandId ? (
      <Alert type="info" showIcon data-testid="tst-notice" title="Đang chờ bộ thực thi TEST làm mới – kết quả hiện ở tab Lịch sử (tự làm mới 5 giây)." />
    ) : o.blockers.length > 0 ? (
      <Alert
        type={o.readOnlyReason ? 'warning' : 'error'}
        showIcon
        data-testid="tst-notice"
        title={o.readOnlyReason ? 'Máy TEST chỉ xem' : 'Chưa làm mới được'}
        description={
          <ul className="erp-tst-list">
            {o.blockers.map((b) => (
              <li key={b}>{b}</li>
            ))}
          </ul>
        }
      />
    ) : (
      <Alert
        type={o.warnings.length > 0 ? 'warning' : 'success'}
        showIcon
        data-testid="tst-notice"
        title={`Đủ điều kiện làm mới – TEST sẽ có dữ liệu ${o.machineEnvironment} lúc ${o.dump?.finishedAt ? formatDateTime(o.dump.finishedAt) : '?'}.`}
        description={
          o.warnings.length > 0 ? (
            <ul className="erp-tst-list">
              {o.warnings.map((w) => (
                <li key={w}>{w}</li>
              ))}
            </ul>
          ) : undefined
        }
      />
    )
  ) : undefined

  return (
    <>
      {tab === 'status' ? (
        <ListPage<SystemOverviewRow>
          key="status"
          title="Làm mới TEST"
          viewTabs={viewTabs}
          exportFileName="lam-moi-test"
          rows={o ? statusRows(o) : []}
          columns={overviewColumns}
          rowKey={(r) => `${r.group}|${r.item}`}
          loading={overview.isLoading}
          canCreate={false}
          isMarked={(r) => r.status === 'Error' || r.status === 'Warning'}
          actions={actions}
          notice={notice}
        />
      ) : (
        <ListPage<AgentCommand>
          key="history"
          title="Làm mới TEST"
          viewTabs={viewTabs}
          exportFileName="lam-moi-test-lich-su"
          exportSubtitle={`Lần làm mới TEST từ ${range.from} đến ${range.to}`}
          rows={runs.data ?? []}
          columns={historyColumns}
          rowKey={(r) => r.id}
          loading={runs.isLoading}
          canCreate={false}
          isMarked={(r) => r.light === 'Error'}
          filters={<LogDateRangeFilter value={range} onChange={setRange} />}
          onClearFilters={() => setRange(defaultRange())}
          actions={actions}
          notice={runs.error ? <Alert type="error" showIcon title={runs.error.message} /> : notice}
        />
      )}
      <RefreshModal
        overview={confirming}
        onClose={() => setConfirming(null)}
        onQueued={() => {
          message.success('Đã giao lệnh làm mới – bộ thực thi TEST nhận ở lần chạy kế tiếp; kết quả ở tab Lịch sử.')
          setTab('history')
        }}
      />
    </>
  )
}
