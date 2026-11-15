import { ClearOutlined, ReloadOutlined, StopOutlined, ToolOutlined } from '@ant-design/icons'
import { Alert, App, Button, Form, Input, Modal } from 'antd'
import dayjs from 'dayjs'
import { useState } from 'react'
import {
  TERMINATE_CONFIRMATION,
  useDbSessions,
  useDbTables,
  useMaintenanceRuns,
  useQueueMaintenance,
  useTerminateSession,
  type DbSession,
  type DbTableStat,
  type MaintenanceRun,
} from '../../api/dbMaintenance'
import { defaultLogRange } from '../../api/logs'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { ListPage } from '../../components/ListPage/ListPage'
import { formatDateTime, formatQuantity } from '../../utils/format'
import { LogDateRangeFilter, TruncatedNotice } from './logFilters'
import { formatBytes, formatDuration } from './opsFormat'
import { statusLabels } from './overviewColumns'

/** Lần dọn / thống kê gần nhất: lấy mốc mới hơn giữa chạy tay và tự động, ghi rõ nguồn. */
function lastOf(manual: string | null, auto: string | null): string {
  if (!manual && !auto) return 'Chưa có'
  if (manual && (!auto || manual >= auto)) return formatDateTime(manual)
  return `${formatDateTime(auto)} (tự động)`
}

/** Mốc mới hơn (để sắp xếp cột). */
function latest(a: string | null, b: string | null) {
  return a && (!b || a >= b) ? a : b
}

/** Đèn của bảng / phiên: Ổn (xanh) hoặc Cần xem (vàng), luôn kèm chữ. */
const lightColumn = <T extends { light: keyof typeof statusLabels }>(width = 90): GridColumn<T> => ({
  key: 'light',
  title: 'Tình trạng',
  width,
  type: 'status',
  value: (r) => statusLabels[r.light]?.label ?? r.light,
  tone: (r) => statusLabels[r.light]?.tone ?? 'neutral',
  groupable: true,
})

/** Cột tab Bảng: dung lượng, số dòng canh phải phông đơn cách (ui-style.md mục 3). */
const tableColumns: GridColumn<DbTableStat>[] = [
  { key: 'name', title: 'Bảng', width: 220, value: (r) => r.name },
  lightColumn<DbTableStat>(),
  { key: 'total', title: 'Dung lượng', width: 95, type: 'quantity', value: (r) => r.totalBytes, render: (r) => formatBytes(r.totalBytes) },
  { key: 'data', title: 'Dữ liệu', width: 90, type: 'quantity', value: (r) => r.tableBytes, render: (r) => formatBytes(r.tableBytes) },
  { key: 'index', title: 'Chỉ mục', width: 90, type: 'quantity', value: (r) => r.indexBytes, render: (r) => formatBytes(r.indexBytes) },
  { key: 'live', title: 'Dòng', width: 90, type: 'quantity', value: (r) => r.liveRows, render: (r) => formatQuantity(r.liveRows) },
  { key: 'dead', title: 'Dòng chết', width: 85, type: 'quantity', value: (r) => r.deadRows, render: (r) => formatQuantity(r.deadRows) },
  { key: 'deadPercent', title: '% chết', width: 65, type: 'quantity', value: (r) => r.deadPercent, render: (r) => formatQuantity(r.deadPercent) },
  {
    key: 'vacuum',
    title: 'VACUUM gần nhất',
    width: 155,
    value: (r) => latest(r.lastVacuum, r.lastAutoVacuum),
    render: (r) => lastOf(r.lastVacuum, r.lastAutoVacuum),
  },
  {
    key: 'analyze',
    title: 'ANALYZE gần nhất',
    width: 155,
    value: (r) => latest(r.lastAnalyze, r.lastAutoAnalyze),
    render: (r) => lastOf(r.lastAnalyze, r.lastAutoAnalyze),
  },
  { key: 'schema', title: 'Schema', width: 70, value: (r) => r.schema, groupable: true },
  { key: 'note', title: 'Ghi chú', width: 420, value: (r) => r.note },
]

/** Cột tab Phiên và khóa: phiên chờ khóa / đang chặn / chạy lâu đèn vàng kèm lý do. */
const sessionColumns: GridColumn<DbSession>[] = [
  { key: 'pid', title: 'Pid', width: 65, type: 'quantity', value: (r) => r.pid, render: (r) => String(r.pid) },
  lightColumn<DbSession>(),
  { key: 'user', title: 'Tài khoản', width: 100, value: (r) => r.userName, groupable: true },
  { key: 'app', title: 'Ứng dụng', width: 150, value: (r) => (r.isSelf ? `${r.applicationName ?? ''} (phiên đang đọc)` : r.applicationName) },
  { key: 'client', title: 'Máy', width: 110, value: (r) => r.clientAddress ?? 'Nội bộ' },
  { key: 'state', title: 'Trạng thái', width: 140, value: (r) => r.stateLabel, groupable: true },
  { key: 'running', title: 'Đã chạy / chờ', width: 105, value: (r) => r.runningSeconds, render: (r) => formatDuration(r.runningSeconds) },
  { key: 'blockedBy', title: 'Chờ khóa của', width: 100, value: (r) => r.blockedBy.join(', ') },
  { key: 'blocking', title: 'Đang chặn', width: 80, type: 'quantity', value: (r) => r.blockingCount, render: (r) => formatQuantity(r.blockingCount) },
  { key: 'wait', title: 'Đang chờ', width: 140, value: (r) => [r.waitEventType, r.waitEvent].filter(Boolean).join(' · ') },
  { key: 'backendStart', title: 'Kết nối lúc', width: 125, type: 'datetime', value: (r) => r.backendStart },
  { key: 'query', title: 'Câu lệnh', width: 420, value: (r) => r.query },
  { key: 'note', title: 'Ghi chú', width: 360, value: (r) => r.note },
]

/** Cột tab Lịch sử. */
const runColumns: GridColumn<MaintenanceRun>[] = [
  { key: 'queuedAt', title: 'Vào hàng chờ', width: 125, type: 'datetime', value: (r) => r.queuedAt },
  { key: 'kind', title: 'Việc', width: 115, value: (r) => r.kindLabel, groupable: true },
  { key: 'target', title: 'Đối tượng', width: 260, value: (r) => r.target ?? 'Cả CSDL' },
  {
    key: 'status',
    title: 'Tình trạng',
    width: 95,
    type: 'status',
    value: (r) => r.statusLabel,
    tone: (r) => statusLabels[r.light]?.tone ?? 'neutral',
    groupable: true,
  },
  {
    key: 'trigger',
    title: 'Nguồn',
    width: 150,
    value: (r) => (r.trigger === 'Schedule' ? 'Theo lịch' : `Chạy tay – ${r.requestedByName ?? ''}`),
    groupable: true,
  },
  { key: 'reason', title: 'Lý do', width: 200, value: (r) => r.reason },
  { key: 'startedAt', title: 'Bắt đầu', width: 125, type: 'datetime', value: (r) => r.startedAt },
  { key: 'duration', title: 'Thời gian chạy', width: 110, value: (r) => r.durationSeconds, render: (r) => formatDuration(r.durationSeconds) },
  {
    key: 'before',
    title: 'Dung lượng trước',
    width: 115,
    type: 'quantity',
    value: (r) => r.sizeBeforeBytes,
    render: (r) => formatBytes(r.sizeBeforeBytes),
  },
  { key: 'after', title: 'Dung lượng sau', width: 110, type: 'quantity', value: (r) => r.sizeAfterBytes, render: (r) => formatBytes(r.sizeAfterBytes) },
  { key: 'note', title: 'Ghi chú', width: 420, value: (r) => [r.errorMessage, r.note].filter(Boolean).join(' · ') },
]

type DbmTab = 'tables' | 'sessions' | 'history'

const tabItems: { key: DbmTab; label: string }[] = [
  { key: 'tables', label: 'Bảng' },
  { key: 'sessions', label: 'Phiên và khóa' },
  { key: 'history', label: 'Lịch sử' },
]

/**
 * Hộp thoại ngắt phiên – xác nhận việc nguy hiểm Q6 (design.md mục 21.1): lý do (≥ 5 ký tự) + nhập lại mật khẩu + gõ "NGẮT PHIÊN". Máy chủ kiểm
 * tra lại cả ba; sai thì báo trong hộp thoại, không đóng.
 * @param session Phiên cần ngắt; null = đóng hộp thoại.
 * @param onClose Đóng hộp thoại.
 * @param onDone Ngắt xong (tình trạng có thể là Lỗi – ví dụ thiếu quyền ngắt phiên tài khoản khác).
 */
function TerminateSessionModal({
  session,
  onClose,
  onDone,
}: {
  session: DbSession | null
  onClose: () => void
  onDone: (run: MaintenanceRun) => void
}) {
  const [form] = Form.useForm<{ reason: string; password: string; confirmation: string }>()
  const [error, setError] = useState<string | null>(null)
  const terminate = useTerminateSession()

  const close = () => {
    form.resetFields()
    setError(null)
    onClose()
  }

  return (
    <Modal
      title={session ? `Ngắt phiên ${session.pid}` : 'Ngắt phiên'}
      open={session != null}
      onCancel={close}
      onOk={() => form.submit()}
      okText="Ngắt phiên"
      okButtonProps={{ danger: true, icon: <StopOutlined /> }}
      cancelText="Không ngắt"
      confirmLoading={terminate.isPending}
      destroyOnHidden
      width={520}
    >
      {session && (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 8 }}
          title={`${session.userName ?? ''} · ${session.applicationName || 'không tên ứng dụng'} · ${session.clientAddress ?? 'nội bộ'} · ${session.stateLabel}`}
          description="Giao dịch đang mở của phiên bị hủy (dữ liệu chưa lưu của phiên đó mất); ứng dụng kết nối lại ở lần dùng sau. Việc này ghi vào nhật ký vận hành."
        />
      )}
      {session?.query && <Input.TextArea readOnly value={session.query} autoSize={{ minRows: 2, maxRows: 5 }} style={{ marginBottom: 8 }} />}
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form
        form={form}
        layout="vertical"
        onFinish={async (v) => {
          if (!session) return
          setError(null)
          try {
            const run = await terminate.mutateAsync({ pid: session.pid, ...v })
            close()
            onDone(run)
          } catch (e) {
            setError(e instanceof Error ? e.message : 'Không ngắt được phiên.')
          }
        }}
      >
        <Form.Item
          label="Lý do"
          name="reason"
          rules={[
            { required: true, message: 'Nhập lý do ngắt phiên.' },
            { min: 5, message: 'Lý do ít nhất 5 ký tự.' },
          ]}
        >
          <Input.TextArea autoFocus maxLength={500} autoSize={{ minRows: 2, maxRows: 4 }} />
        </Form.Item>
        <Form.Item label="Mật khẩu của bạn" name="password" rules={[{ required: true, message: 'Nhập lại mật khẩu để xác nhận.' }]}>
          <Input.Password autoComplete="current-password" />
        </Form.Item>
        <Form.Item
          label={`Gõ "${TERMINATE_CONFIRMATION}" để xác nhận`}
          name="confirmation"
          rules={[
            {
              validator: (_, value?: string) =>
                value?.trim().normalize().toUpperCase() === TERMINATE_CONFIRMATION
                  ? Promise.resolve()
                  : Promise.reject(new Error(`Gõ đúng "${TERMINATE_CONFIRMATION}".`)),
            },
          ]}
        >
          <Input autoComplete="off" />
        </Form.Item>
      </Form>
    </Modal>
  )
}

/**
 * Màn hình DBM – Bảo trì CSDL (design.md mục 21.4, lát QT4; ui-style.md mục 5m). Ba tab trên khuôn ListPage + DataGrid:
 * - **Bảng**: dung lượng (dữ liệu, chỉ mục), số dòng, dòng chết, lần VACUUM / ANALYZE gần nhất; nhiều dòng chết đèn vàng. Nút VACUUM ANALYZE
 *   bảng đang chọn / cả CSDL, REINDEX bảng đang chọn – đặt vào hàng chờ, chạy nền.
 * - **Phiên và khóa**: phiên kết nối CSDL ứng dụng, phiên chờ khóa / đang chặn / chạy lâu đèn vàng; nút "Ngắt phiên…" (icon đỏ) xác nhận Q6.
 * - **Lịch sử**: các lần bảo trì theo khoảng ngày, lịch hằng tuần; còn lần chờ / chạy thì tự làm mới 5 giây.
 * Chỉ ở tổng công ty, quyền System.OpsMaintenance.View; nút chạy cần System.OpsMaintenance.Run.
 */
export function DbMaintenancePage() {
  const { message, modal } = App.useApp()
  const { hasPermission } = useAuth()
  const canRun = hasPermission(Permission.MaintenanceRun)
  const [tab, setTab] = useState<DbmTab>('tables')
  const [range, setRange] = useState(defaultLogRange)
  const [table, setTable] = useState<DbTableStat | null>(null)
  const [session, setSession] = useState<DbSession | null>(null)
  const [terminating, setTerminating] = useState<DbSession | null>(null)
  const tables = useDbTables()
  const sessions = useDbSessions(tab === 'sessions')
  const runs = useMaintenanceRuns(range)
  const queue = useQueueMaintenance()

  const queueRun = (kind: 'Vacuum' | 'Reindex', target: DbTableStat | null) => {
    const what = `${kind === 'Vacuum' ? 'VACUUM ANALYZE' : 'REINDEX'} ${target ? `bảng ${target.name}` : 'cả CSDL'}`
    modal.confirm({
      title: `Chạy ${what}?`,
      content:
        kind === 'Reindex'
          ? 'REINDEX CONCURRENTLY dựng lại chỉ mục mà không khóa ghi bảng, nhưng tốn thêm chỗ trống bằng cỡ chỉ mục trong lúc chạy.'
          : 'VACUUM ANALYZE dọn dòng chết, cập nhật thống kê; không khóa đọc / ghi bảng.',
      okText: 'Chạy',
      cancelText: 'Không chạy',
      onOk: async () => {
        try {
          await queue.mutateAsync({ kind, schema: target?.schema, table: target?.name })
          message.success(`Đã đặt ${what} vào hàng chờ – kết quả hiện ở tab Lịch sử.`)
          setTab('history')
        } catch (e) {
          message.error(e instanceof Error ? e.message : 'Không đặt được việc bảo trì.')
        }
      },
    })
  }

  const refresh = () => {
    if (tab === 'tables') void tables.refetch()
    else if (tab === 'sessions') void sessions.refetch()
    else void runs.refetch()
  }

  const refreshButton = (
    <Button
      icon={<ReloadOutlined />}
      loading={tables.isFetching || sessions.isFetching || runs.isFetching}
      onClick={refresh}
      title="Lấy lại số liệu của tab đang xem"
    >
      Làm mới
    </Button>
  )
  const viewTabs = { items: tabItems, activeKey: tab, onChange: (k: string) => setTab(k as DbmTab) }

  if (tab === 'tables') {
    const noTable = 'Chọn một bảng trong lưới'
    return (
      <ListPage<DbTableStat>
        key="tables"
        title="Bảo trì CSDL"
        viewTabs={viewTabs}
        exportFileName="bao-tri-csdl-bang"
        exportSubtitle={tables.data ? `CSDL ${tables.data.databaseName} lúc ${formatDateTime(tables.data.checkedAt)}` : undefined}
        rows={tables.data?.tables ?? []}
        columns={tableColumns}
        rowKey={(r) => `${r.schema}.${r.name}`}
        loading={tables.isLoading}
        canCreate={false}
        onSelectRow={setTable}
        isMarked={(r) => r.light !== 'Ok'}
        actions={
          <>
            {canRun && (
              <>
                <Button
                  icon={<ClearOutlined />}
                  disabled={!table}
                  onClick={() => queueRun('Vacuum', table)}
                  title={table ? `VACUUM ANALYZE bảng ${table.name}` : noTable}
                >
                  VACUUM ANALYZE
                </Button>
                <Button icon={<ClearOutlined />} onClick={() => queueRun('Vacuum', null)} title="VACUUM ANALYZE mọi bảng của CSDL ứng dụng">
                  VACUUM cả CSDL
                </Button>
                <Button
                  icon={<ToolOutlined />}
                  disabled={!table}
                  onClick={() => queueRun('Reindex', table)}
                  title={table ? `REINDEX CONCURRENTLY bảng ${table.name}` : noTable}
                >
                  REINDEX
                </Button>
              </>
            )}
            {refreshButton}
          </>
        }
        notice={
          tables.error ? (
            <Alert type="error" showIcon title={tables.error.message} />
          ) : tables.data ? (
            <Alert
              type={tables.data.tables.some((t) => t.light !== 'Ok') ? 'warning' : 'info'}
              showIcon
              title={`CSDL ${tables.data.databaseName}: ${formatBytes(tables.data.databaseBytes)} · ${formatQuantity(tables.data.tables.length)} bảng · số liệu lúc ${formatDateTime(tables.data.checkedAt)}`}
              description={
                tables.data.tables.some((t) => t.light !== 'Ok')
                  ? 'Bảng đèn vàng: nhiều dòng chết hoặc chưa ANALYZE – chọn bảng rồi bấm VACUUM ANALYZE.'
                  : 'PostgreSQL tự dọn (autovacuum); lịch hằng tuần chạy thêm VACUUM ANALYZE cả CSDL – xem tab Lịch sử.'
              }
            />
          ) : undefined
        }
      />
    )
  }

  if (tab === 'sessions') {
    const data = sessions.data
    return (
      <>
        <ListPage<DbSession>
          key="sessions"
          title="Bảo trì CSDL"
          viewTabs={viewTabs}
          exportFileName="bao-tri-csdl-phien"
          exportSubtitle={data ? `Phiên lúc ${formatDateTime(data.checkedAt)}` : undefined}
          rows={data?.sessions ?? []}
          columns={sessionColumns}
          rowKey={(r) => String(r.pid)}
          loading={sessions.isLoading}
          canCreate={false}
          onSelectRow={setSession}
          isMarked={(r) => r.light !== 'Ok'}
          actions={
            <>
              {canRun && (
                <Button
                  icon={<StopOutlined />}
                  disabled={!session || session.isSelf}
                  onClick={() => setTerminating(session)}
                  title={
                    !session ? 'Chọn một phiên trong lưới' : session.isSelf ? 'Không ngắt được phiên đang đọc số liệu' : `Ngắt phiên ${session.pid}`
                  }
                >
                  Ngắt phiên…
                </Button>
              )}
              {refreshButton}
            </>
          }
          notice={
            sessions.error ? (
              <Alert type="error" showIcon title={sessions.error.message} />
            ) : data ? (
              <Alert
                type={data.waitingCount > 0 ? 'warning' : 'info'}
                showIcon
                title={`${formatQuantity(data.sessions.length)} phiên · ${formatQuantity(data.waitingCount)} phiên chờ khóa · số liệu lúc ${formatDateTime(data.checkedAt)}`}
                description={
                  data.usesMaintenanceAccount
                    ? 'Đọc bằng tài khoản bảo trì – thấy câu lệnh của mọi tài khoản.'
                    : 'Đọc bằng tài khoản ứng dụng – câu lệnh của tài khoản khác bị PostgreSQL ẩn (khai ConnectionStrings:Maintenance để xem đủ).'
                }
              />
            ) : undefined
          }
        />
        <TerminateSessionModal
          session={terminating}
          onClose={() => setTerminating(null)}
          onDone={(run) => {
            if (run.status === 'Succeeded') message.success(run.note ?? `Đã ngắt phiên ${run.sessionPid}.`)
            else message.error(run.errorMessage ?? 'Không ngắt được phiên.')
            void sessions.refetch()
          }}
        />
      </>
    )
  }

  const page = runs.data
  return (
    <ListPage<MaintenanceRun>
      key="history"
      title="Bảo trì CSDL"
      viewTabs={viewTabs}
      exportFileName="bao-tri-csdl-lich-su"
      exportSubtitle={`Lịch sử bảo trì từ ${range.from} đến ${range.to}`}
      rows={page?.items ?? []}
      columns={runColumns}
      rowKey={(r) => r.id}
      loading={runs.isLoading}
      canCreate={false}
      actions={refreshButton}
      filters={<LogDateRangeFilter value={range} onChange={setRange} />}
      onClearFilters={() => setRange(defaultLogRange())}
      isMarked={(r) => r.light === 'Error'}
      notice={
        runs.error ? (
          <Alert type="error" showIcon title={runs.error.message} />
        ) : (
          <>
            <TruncatedNotice truncated={page?.truncated} />
            {page && (
              <Alert
                type="info"
                showIcon
                title={
                  page.scheduleEnabled && page.nextScheduledAt
                    ? `Lịch hằng tuần: VACUUM ANALYZE cả CSDL ${page.scheduleLabel} · lượt kế tiếp ${dayjs(page.nextScheduledAt).format('DD/MM/YYYY HH:mm')}`
                    : `Lịch hằng tuần đang tắt trên máy này (DbMaintenance:ScheduleEnabled) – tham số đặt ${page.scheduleLabel}.`
                }
              />
            )}
          </>
        )
      }
    />
  )
}
