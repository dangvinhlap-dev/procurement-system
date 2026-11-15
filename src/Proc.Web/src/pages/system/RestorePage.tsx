import { CloseCircleOutlined, CloudDownloadOutlined, DiffOutlined, FieldTimeOutlined, ReloadOutlined, SwapOutlined } from '@ant-design/icons'
import { Alert, App, Button, Form, Input, Modal, TimePicker } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useState } from 'react'
import { defaultLogRange } from '../../api/logs'
import {
  RESTORE_CONFIRMATION,
  useQueueRestore,
  useRestoreAction,
  useRestoreOverview,
  useRestorePlan,
  useRestoreRun,
  useRestoreRuns,
  useSwapRestore,
  type RestoreCompareRow,
  type RestoreDumpSource,
  type RestoreOverview,
  type RestorePlan,
  type RestoreRun,
} from '../../api/restore'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DateInput } from '../../classic/DateInput'
import { DataGrid } from '../../components/DataGrid/DataGrid'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { ListPage } from '../../components/ListPage/ListPage'
import { formatDateTime, formatQuantity } from '../../utils/format'
import { LogDateRangeFilter, TruncatedNotice } from './logFilters'
import { formatBytes } from './opsFormat'
import { statusLabels } from './overviewColumns'

/** Cột tab Lịch sử. */
const runColumns: GridColumn<RestoreRun>[] = [
  { key: 'queuedAt', title: 'Lúc đặt', width: 125, type: 'datetime', value: (r) => r.queuedAt },
  { key: 'kind', title: 'Cách', width: 120, value: (r) => r.kindLabel, groupable: true },
  {
    key: 'status',
    title: 'Tình trạng',
    width: 110,
    type: 'status',
    value: (r) => r.statusLabel,
    tone: (r) => statusLabels[r.light]?.tone ?? 'neutral',
    groupable: true,
  },
  { key: 'source', title: 'Nguồn', width: 230, value: (r) => r.sourceLabel },
  { key: 'restore', title: 'Bản khôi phục', width: 260, value: (r) => (r.kind === 'PointInTime' ? `Máy chủ tạm cổng ${r.tempPort}` : r.restoreDatabase) },
  { key: 'old', title: 'CSDL / thư mục cũ', width: 240, value: (r) => r.oldDatabase },
  { key: 'reason', title: 'Lý do', width: 200, value: (r) => r.reason },
  { key: 'by', title: 'Người đặt', width: 110, value: (r) => r.requestedByName, groupable: true },
  { key: 'readyAt', title: 'Sẵn sàng lúc', width: 125, type: 'datetime', value: (r) => r.readyAt },
  { key: 'finishedAt', title: 'Kết thúc', width: 125, type: 'datetime', value: (r) => r.finishedAt },
  { key: 'note', title: 'Ghi chú', width: 480, value: (r) => [r.errorMessage, r.note].filter(Boolean).join(' · ') },
]

/** Cột tab Bản pg_dump. */
const dumpColumns: GridColumn<RestoreDumpSource>[] = [
  { key: 'finishedAt', title: 'Sao lưu xong', width: 130, type: 'datetime', value: (r) => r.finishedAt },
  { key: 'path', title: 'File', width: 380, value: (r) => r.relativePath },
  { key: 'size', title: 'Dung lượng', width: 100, type: 'quantity', value: (r) => r.sizeBytes, render: (r) => formatBytes(r.sizeBytes) },
  {
    key: 'file',
    title: 'Tình trạng',
    width: 120,
    type: 'status',
    value: (r) => (r.fileExists ? 'Còn file' : 'Không thấy file'),
    tone: (r) => (r.fileExists ? 'success' : 'error'),
  },
]

/** Cột bảng so sánh số dòng (ngăn kéo). */
const compareColumns: GridColumn<RestoreCompareRow>[] = [
  { key: 'table', title: 'Bảng', width: 210, value: (r) => (r.schema === 'public' ? r.table : `${r.schema}.${r.table}`) },
  {
    key: 'light',
    title: 'So sánh',
    width: 110,
    type: 'status',
    value: (r) => (r.operational ? 'Bảng vận hành' : r.light === 'Ok' ? 'Bằng nhau' : 'Khác'),
    tone: (r) => statusLabels[r.light]?.tone ?? 'neutral',
    groupable: true,
  },
  { key: 'live', title: 'Đang chạy', width: 90, type: 'quantity', value: (r) => r.liveRows, render: (r) => (r.liveRows == null ? 'Không có' : formatQuantity(r.liveRows)) },
  {
    key: 'restore',
    title: 'Bản khôi phục',
    width: 95,
    type: 'quantity',
    value: (r) => r.restoreRows,
    render: (r) => (r.restoreRows == null ? 'Không có' : formatQuantity(r.restoreRows)),
  },
  {
    key: 'diff',
    title: 'Chênh lệch',
    width: 85,
    type: 'quantity',
    value: (r) => (r.restoreRows ?? 0) - (r.liveRows ?? 0),
    render: (r) => formatQuantity((r.restoreRows ?? 0) - (r.liveRows ?? 0)),
  },
]

type RstTab = 'history' | 'dumps'

const tabItems: { key: RstTab; label: string }[] = [
  { key: 'history', label: 'Lịch sử' },
  { key: 'dumps', label: 'Bản pg_dump' },
]

/** Ô chữ chỉ đọc trong ngăn kéo. */
function Info({ label, value }: { label: string; value: string | null | undefined }) {
  return (
    <Form.Item label={label}>
      <Input readOnly value={value ?? ''} title={value ?? ''} />
    </Form.Item>
  )
}

/** Khối kế hoạch chuỗi Full → Diff → WAL (dùng trong hộp thoại về mốc và ngăn kéo). */
function PlanBlock({ plan }: { plan: RestorePlan }) {
  return (
    <div data-testid="restore-plan">
      <Alert
        type={plan.feasible ? 'success' : 'error'}
        showIcon
        style={{ marginBottom: 4 }}
        title={plan.feasible ? `Khôi phục được về ${formatDateTime(plan.targetTime)}` : 'Không khôi phục được về mốc này'}
        description={
          plan.feasible ? (
            <>
              Full xong {formatDateTime(plan.fullFinishedAt)} ({plan.fullPath})
              {plan.diffPath ? ` + Diff xong ${formatDateTime(plan.diffFinishedAt)} (${plan.diffPath})` : ''} + {formatQuantity(plan.walCount)} file WAL
              ({plan.firstWal} → {plan.lastWal}).
            </>
          ) : (
            plan.problems.join(' ')
          )
        }
      />
      {plan.warnings.map((w) => (
        <Alert key={w} type="warning" showIcon style={{ marginBottom: 4 }} title={w} />
      ))}
    </div>
  )
}

/** Câu lý do bắt buộc ≥ 5 ký tự. */
const reasonRules = [
  { required: true, message: 'Nhập lý do.' },
  { min: 5, message: 'Lý do ít nhất 5 ký tự.' },
]

/**
 * Hộp thoại khôi phục từ một bản pg_dump sang CSDL mới (không đụng CSDL đang chạy).
 * @param dump Bản dump; null = đóng.
 * @param overview Thông tin chung (tên CSDL đang chạy).
 * @param onClose Đóng.
 * @param onDone Đặt xong.
 */
function DumpRestoreModal({
  dump,
  overview,
  onClose,
  onDone,
}: {
  dump: RestoreDumpSource | null
  overview: RestoreOverview | undefined
  onClose: () => void
  onDone: (run: RestoreRun) => void
}) {
  const [form] = Form.useForm<{ reason: string }>()
  const [error, setError] = useState<string | null>(null)
  const queue = useQueueRestore()
  const close = () => {
    form.resetFields()
    setError(null)
    onClose()
  }
  return (
    <Modal
      title="Khôi phục từ pg_dump"
      open={dump != null}
      onCancel={close}
      onOk={() => form.submit()}
      okText="Khôi phục"
      okButtonProps={{ icon: <CloudDownloadOutlined /> }}
      cancelText="Không khôi phục"
      confirmLoading={queue.isPending}
      destroyOnHidden
      width={560}
    >
      {dump && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 8 }}
          title={`Bản ${dump.relativePath} (${formatDateTime(dump.finishedAt)}, ${formatBytes(dump.sizeBytes)})`}
          description={`Khôi phục vào CSDL mới ${overview?.liveDatabase ?? ''}_restore_<giờ> trên cùng máy chủ – CSDL đang chạy không bị đụng. Xong thì so sánh số dòng, rồi hoán đổi hoặc bỏ.`}
        />
      )}
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form
        form={form}
        layout="vertical"
        onFinish={async (v) => {
          if (!dump) return
          setError(null)
          try {
            const run = await queue.mutateAsync({ kind: 'Dump', dumpRunId: dump.id, reason: v.reason })
            close()
            onDone(run)
          } catch (e) {
            setError(e instanceof Error ? e.message : 'Không đặt được lần khôi phục.')
          }
        }}
      >
        <Form.Item label="Lý do" name="reason" rules={reasonRules}>
          <Input.TextArea autoFocus maxLength={500} autoSize={{ minRows: 2, maxRows: 4 }} />
        </Form.Item>
      </Form>
    </Modal>
  )
}

/**
 * Hộp thoại khôi phục về mốc thời gian: chọn ngày + giờ → kế hoạch (Full → Diff → WAL, cảnh báo CSDL khác cùng máy chủ) → lý do → giao bộ
 * thực thi dựng máy chủ CSDL tạm.
 * @param open Mở.
 * @param onClose Đóng.
 * @param onDone Đặt xong.
 */
function PointInTimeModal({ open, onClose, onDone }: { open: boolean; onClose: () => void; onDone: (run: RestoreRun) => void }) {
  const [date, setDate] = useState<string | null>(dayjs().format('YYYY-MM-DD'))
  const [time, setTime] = useState<Dayjs | null>(dayjs().subtract(1, 'hour').startOf('minute'))
  const [target, setTarget] = useState<string | null>(null)
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | null>(null)
  const plan = useRestorePlan(target)
  const queue = useQueueRestore()
  const close = () => {
    setTarget(null)
    setReason('')
    setError(null)
    onClose()
  }
  // Mốc là giờ Việt Nam (CLAUDE.md mục 5) – gửi kèm múi giờ +07:00.
  const pick = date && time ? `${date}T${time.format('HH:mm')}:00+07:00` : null
  return (
    <Modal
      title="Khôi phục về mốc thời gian"
      open={open}
      onCancel={close}
      onOk={async () => {
        if (!target || !plan.data?.feasible) return
        if (reason.trim().length < 5) {
          setError('Nhập lý do (ít nhất 5 ký tự).')
          return
        }
        setError(null)
        try {
          const run = await queue.mutateAsync({ kind: 'PointInTime', targetTime: target, reason: reason.trim() })
          close()
          onDone(run)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không đặt được lần khôi phục.')
        }
      }}
      okText="Khôi phục"
      okButtonProps={{ icon: <FieldTimeOutlined />, disabled: !target || target !== pick || !plan.data?.feasible }}
      cancelText="Không khôi phục"
      confirmLoading={queue.isPending}
      destroyOnHidden
      width={640}
    >
      <Form layout="vertical">
        <div style={{ display: 'flex', gap: 8, alignItems: 'flex-end' }}>
          <Form.Item label="Ngày" style={{ marginBottom: 4 }}>
            <DateInput value={date} onChange={setDate} aria-label="Ngày của mốc" />
          </Form.Item>
          <Form.Item label="Giờ" style={{ marginBottom: 4 }}>
            <TimePicker format="HH:mm" value={time} onChange={setTime} aria-label="Giờ của mốc" needConfirm={false} style={{ width: 90 }} />
          </Form.Item>
          <Form.Item style={{ marginBottom: 4 }}>
            <Button icon={<DiffOutlined />} disabled={!pick} loading={plan.isFetching} onClick={() => setTarget(pick)}>
              Lập kế hoạch
            </Button>
          </Form.Item>
        </div>
        {plan.error && <Alert type="error" showIcon title={plan.error.message} style={{ marginBottom: 4 }} />}
        {plan.data && target === pick && <PlanBlock plan={plan.data} />}
        {plan.data?.feasible && target === pick && (
          <Form.Item label="Lý do" required style={{ marginTop: 8 }}>
            <Input.TextArea value={reason} onChange={(e) => setReason(e.target.value)} maxLength={500} autoSize={{ minRows: 2, maxRows: 4 }} />
          </Form.Item>
        )}
      </Form>
      {error && <Alert type="error" showIcon title={error} />}
    </Modal>
  )
}

/**
 * Hộp thoại hoán đổi – việc nguy hiểm Q6 (design.md mục 21.1; bố cục ui-style.md mục 5m): cảnh báo việc sẽ làm, lý do, mật khẩu, gõ "KHÔI
 * PHỤC". Lỗi máy chủ hiện trong hộp thoại, không đóng.
 * @param run Lần phục hồi; null = đóng.
 * @param overview Thông tin chung (CSDL khác cùng máy chủ).
 * @param onClose Đóng.
 * @param onDone Đặt hoán đổi xong.
 */
function SwapModal({
  run,
  overview,
  onClose,
  onDone,
}: {
  run: RestoreRun | null
  overview: RestoreOverview | undefined
  onClose: () => void
  onDone: (run: RestoreRun) => void
}) {
  const [form] = Form.useForm<{ reason: string; password: string; confirmation: string }>()
  const [error, setError] = useState<string | null>(null)
  const swap = useSwapRestore()
  const close = () => {
    form.resetFields()
    setError(null)
    onClose()
  }
  const pitr = run?.kind === 'PointInTime'
  return (
    <Modal
      title="Hoán đổi bản khôi phục"
      open={run != null}
      onCancel={close}
      onOk={() => form.submit()}
      okText="Hoán đổi"
      okButtonProps={{ danger: true, icon: <SwapOutlined /> }}
      cancelText="Không hoán đổi"
      confirmLoading={swap.isPending}
      destroyOnHidden
      width={600}
    >
      {run && (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 8 }}
          title={
            pitr
              ? `Đưa cả máy chủ PostgreSQL về mốc ${formatDateTime(run.targetTime)}`
              : `${run.restoreDatabase} thành CSDL đang chạy ${run.liveDatabase}`
          }
          description={
            <>
              Bộ thực thi của máy này sẽ bật chế độ bảo trì, dừng site, {pitr ? 'dừng PostgreSQL, cất thư mục dữ liệu cũ, đưa bản khôi phục vào' : 'đổi tên CSDL'}
              , chạy lại và kiểm tra. Dữ liệu nhập sau thời điểm của bản khôi phục sẽ không còn trong CSDL đang chạy (bản cũ giữ lại để tra cứu).
              Bảng vận hành (sao lưu, bộ thực thi, nhật ký vận hành, lịch sử phục hồi) giữ như hiện tại.
              {pitr && overview && overview.otherDatabases.length > 0 && (
                <strong>
                  {' '}
                  Các CSDL khác trên cùng máy chủ ({overview.otherDatabases.join(', ')}) cũng quay về mốc này.
                </strong>
              )}
            </>
          }
        />
      )}
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form
        form={form}
        layout="vertical"
        onFinish={async (v) => {
          if (!run) return
          setError(null)
          try {
            const result = await swap.mutateAsync({ id: run.id, ...v })
            close()
            onDone(result)
          } catch (e) {
            setError(e instanceof Error ? e.message : 'Không hoán đổi được.')
          }
        }}
      >
        <Form.Item label="Lý do" name="reason" rules={reasonRules}>
          <Input.TextArea autoFocus maxLength={500} autoSize={{ minRows: 2, maxRows: 4 }} />
        </Form.Item>
        <Form.Item label="Mật khẩu của bạn" name="password" rules={[{ required: true, message: 'Nhập lại mật khẩu để xác nhận.' }]}>
          <Input.Password autoComplete="current-password" />
        </Form.Item>
        <Form.Item
          label={`Gõ "${RESTORE_CONFIRMATION}" để xác nhận`}
          name="confirmation"
          rules={[
            {
              validator: (_, value?: string) =>
                value?.trim().normalize().toUpperCase() === RESTORE_CONFIRMATION
                  ? Promise.resolve()
                  : Promise.reject(new Error(`Gõ đúng "${RESTORE_CONFIRMATION}".`)),
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
 * Ngăn kéo xem một lần phục hồi: thông tin, bảng so sánh số dòng, kế hoạch (về mốc).
 * @param id Lần phục hồi.
 * @param onClose Đóng.
 */
function RestoreDrawer({ id, onClose }: { id: string; onClose: () => void }) {
  const detail = useRestoreRun(id)
  const [groupBy, setGroupBy] = useState<string | null>(null)
  const r = detail.data
  return (
    <DetailDrawer
      open
      title={r ? `Phục hồi: ${r.kindLabel} – ${r.statusLabel}` : 'Lần phục hồi'}
      onClose={onClose}
      loading={detail.isLoading}
      error={detail.error?.message ?? null}
      readOnly
      tabs={[
        {
          key: 'info',
          label: 'Thông tin',
          children: r && (
            <>
              <Info label="Cách" value={r.kindLabel} />
              <Info label="Tình trạng" value={r.statusLabel} />
              <Info label="Nguồn" value={r.sourceLabel} />
              <Info label="CSDL đang chạy" value={r.liveDatabase} />
              <Info label="Bản khôi phục" value={r.kind === 'PointInTime' ? `${r.restoreDatabase} trên máy chủ tạm cổng ${r.tempPort}` : r.restoreDatabase} />
              <Info label="CSDL / thư mục cũ" value={r.oldDatabase} />
              <Info label="Người đặt" value={`${r.requestedByName ?? ''} · ${formatDateTime(r.queuedAt)}`} />
              <Info label="Lý do" value={r.reason} />
              <Info label="Hoán đổi" value={r.swapRequestedAt ? `${r.swapRequestedByName ?? ''} · ${formatDateTime(r.swapRequestedAt)} · ${r.swapReason ?? ''}` : ''} />
              <Form.Item label="Ghi chú">
                <Input.TextArea readOnly value={[r.errorMessage, r.note].filter(Boolean).join('\n')} autoSize={{ minRows: 2, maxRows: 6 }} />
              </Form.Item>
            </>
          ),
        },
        {
          key: 'compare',
          label: 'So sánh số dòng',
          lazy: true,
          children: r?.comparison ? (
            <>
              <Alert
                type={r.canSwap ? 'info' : 'warning'}
                showIcon
                style={{ marginBottom: 4 }}
                title={`So sánh lúc ${formatDateTime(r.comparison.comparedAt)} · phiên bản CSDL đang chạy ${r.comparison.liveMigration ?? '?'} · bản khôi phục ${r.comparison.restoreMigration ?? '?'}`}
              />
              <DataGrid<RestoreCompareRow>
                rows={r.comparison.rows}
                columns={compareColumns}
                rowKey={(x) => `${x.schema}.${x.table}`}
                groupBy={groupBy}
                onGroupByChange={setGroupBy}
                isMarked={(x) => x.light === 'Warning'}
              />
            </>
          ) : (
            <Alert type="info" showIcon title="Chưa có bảng so sánh (bản khôi phục chưa xong)." />
          ),
        },
        ...(r?.plan
          ? [{ key: 'plan', label: 'Kế hoạch', lazy: true, children: <PlanBlock plan={r.plan} /> }]
          : []),
      ]}
    />
  )
}

/**
 * Màn hình RST – Phục hồi (design.md mục 21.4, 21.15, lát QT6; ui-style.md mục 5o). Hai tab trên khuôn ListPage + DataGrid:
 * - **Lịch sử**: các lần phục hồi (tự làm mới 5 giây khi còn lần đang chạy); nút Về mốc thời gian…, So sánh lại, Hoán đổi… (Q6 "KHÔI PHỤC"), Bỏ
 *   bản khôi phục – thao tác trên dòng đang chọn; bấm đúp mở ngăn kéo có bảng so sánh số dòng.
 * - **Bản pg_dump**: bản dump dùng được; nút Khôi phục từ bản này… (CSDL mới, không đụng CSDL đang chạy).
 * Chỉ ở tổng công ty, quyền System.OpsRestore.View; nút cần System.OpsRestore.Run.
 */
export function RestorePage() {
  const { message, modal } = App.useApp()
  const { hasPermission } = useAuth()
  const canRun = hasPermission(Permission.RestoreRun)
  const [tab, setTab] = useState<RstTab>('history')
  const [range, setRange] = useState(defaultLogRange)
  const [selected, setSelected] = useState<RestoreRun | null>(null)
  const [dump, setDump] = useState<RestoreDumpSource | null>(null)
  const [restoringDump, setRestoringDump] = useState<RestoreDumpSource | null>(null)
  const [pitrOpen, setPitrOpen] = useState(false)
  const [swapping, setSwapping] = useState<RestoreRun | null>(null)
  const [openId, setOpenId] = useState<string | null>(null)
  const overview = useRestoreOverview()
  const runs = useRestoreRuns(range)
  const action = useRestoreAction()
  const viewTabs = { items: tabItems, activeKey: tab, onChange: (k: string) => setTab(k as RstTab) }
  // Dòng đang chọn theo số liệu mới nhất (lưới tự làm mới khi đang chạy).
  const current = selected ? (runs.data?.items.find((r) => r.id === selected.id) ?? selected) : null

  const refreshButton = (
    <Button
      icon={<ReloadOutlined />}
      loading={runs.isFetching || overview.isFetching}
      onClick={() => {
        void runs.refetch()
        void overview.refetch()
      }}
      title="Lấy lại số liệu"
    >
      Làm mới
    </Button>
  )

  const done = (run: RestoreRun, what: string) => {
    message.success(`${what} – theo dõi ở tab Lịch sử.`)
    setTab('history')
    setSelected(run)
  }

  const discard = (run: RestoreRun) =>
    modal.confirm({
      title: 'Bỏ bản khôi phục?',
      content:
        run.kind === 'Dump'
          ? `Xóa CSDL ${run.restoreDatabase}. CSDL đang chạy không bị đụng.`
          : 'Bộ thực thi dừng máy chủ CSDL tạm và xóa thư mục khôi phục. CSDL đang chạy không bị đụng.',
      okText: 'Bỏ bản khôi phục',
      okButtonProps: { danger: true },
      cancelText: 'Không bỏ',
      onOk: async () => {
        try {
          const r = await action.mutateAsync({ id: run.id, action: 'discard' })
          message.success(r.status === 'Discarded' ? 'Đã bỏ bản khôi phục.' : 'Đã giao bộ thực thi dừng máy chủ tạm, xóa thư mục.')
        } catch (e) {
          message.error(e instanceof Error ? e.message : 'Không bỏ được bản khôi phục.')
        }
      },
    })

  const ov = overview.data
  const notice = overview.error ? (
    <Alert type="error" showIcon title={overview.error.message} />
  ) : ov ? (
    <>
      <Alert
        type={ov.agentReady ? 'info' : 'warning'}
        showIcon
        title={`CSDL đang chạy: ${ov.liveDatabase} · bộ thực thi máy này (${ov.agentEnvironment}): ${ov.agentReady ? 'sẵn sàng' : 'chưa sẵn sàng'}`}
        description={
          ov.agentReady
            ? 'Khôi phục không ghi đè CSDL đang chạy: khôi phục ra bản riêng → so sánh số dòng → hoán đổi (bộ thực thi) hoặc bỏ.'
            : `${ov.agentNote ?? ''} Khôi phục từ pg_dump vẫn làm được (ứng dụng tự làm); hoán đổi và về mốc thời gian cần bộ thực thi.`
        }
      />
      {ov.otherDatabases.length > 0 && (
        <Alert
          type="warning"
          showIcon
          style={{ marginTop: 4 }}
          title={`Máy chủ PostgreSQL này còn ${formatQuantity(ov.otherDatabases.length)} CSDL khác: ${ov.otherDatabases.join(', ')}`}
          description="Khôi phục về mốc thời gian dùng bản Full (cả thư mục dữ liệu PostgreSQL) nên hoán đổi đưa CẢ các CSDL này về mốc. Chỉ cần lấy lại dữ liệu phần mềm này thì dùng Khôi phục từ pg_dump."
        />
      )}
    </>
  ) : undefined

  const modals = (
    <>
      <DumpRestoreModal dump={restoringDump} overview={ov} onClose={() => setRestoringDump(null)} onDone={(r) => done(r, 'Đã đặt khôi phục pg_dump vào hàng chờ')} />
      <PointInTimeModal open={pitrOpen} onClose={() => setPitrOpen(false)} onDone={(r) => done(r, 'Đã giao bộ thực thi khôi phục về mốc')} />
      <SwapModal run={swapping} overview={ov} onClose={() => setSwapping(null)} onDone={(r) => done(r, 'Đã giao bộ thực thi hoán đổi')} />
      {openId && <RestoreDrawer id={openId} onClose={() => setOpenId(null)} />}
    </>
  )

  if (tab === 'dumps') {
    return (
      <>
        <ListPage<RestoreDumpSource>
          key="dumps"
          title="Phục hồi"
          viewTabs={viewTabs}
          exportFileName="phuc-hoi-ban-pg-dump"
          rows={ov?.dumps ?? []}
          columns={dumpColumns}
          rowKey={(r) => r.id}
          loading={overview.isLoading}
          canCreate={false}
          onSelectRow={setDump}
          isMarked={(r) => !r.fileExists}
          actions={
            <>
              {canRun && (
                <Button
                  icon={<CloudDownloadOutlined />}
                  disabled={!dump?.fileExists}
                  onClick={() => setRestoringDump(dump)}
                  title={!dump ? 'Chọn một bản pg_dump trong lưới' : !dump.fileExists ? 'Không thấy file trong thư mục sao lưu' : `Khôi phục ${dump.relativePath}`}
                >
                  Khôi phục từ bản này…
                </Button>
              )}
              {refreshButton}
            </>
          }
          notice={notice}
        />
        {modals}
      </>
    )
  }

  const noRow = 'Chọn một lần phục hồi trong lưới'
  const ready = current?.status === 'Ready'
  return (
    <>
      <ListPage<RestoreRun>
        key="history"
        title="Phục hồi"
        viewTabs={viewTabs}
        exportFileName="phuc-hoi-lich-su"
        exportSubtitle={`Lịch sử phục hồi từ ${range.from} đến ${range.to}`}
        rows={runs.data?.items ?? []}
        columns={runColumns}
        rowKey={(r) => r.id}
        loading={runs.isLoading}
        canCreate={false}
        onSelectRow={setSelected}
        onOpenRow={(r) => setOpenId(r.id)}
        isMarked={(r) => r.light === 'Error' || r.status === 'Ready'}
        filters={<LogDateRangeFilter value={range} onChange={setRange} />}
        onClearFilters={() => setRange(defaultLogRange())}
        actions={
          <>
            {canRun && (
              <>
                <Button icon={<FieldTimeOutlined />} onClick={() => setPitrOpen(true)} title="Chọn mốc thời gian, xem kế hoạch Full → Diff → WAL rồi khôi phục ra máy chủ CSDL tạm">
                  Về mốc thời gian…
                </Button>
                <Button
                  icon={<DiffOutlined />}
                  disabled={!ready}
                  loading={action.isPending}
                  onClick={async () => {
                    if (!current) return
                    try {
                      await action.mutateAsync({ id: current.id, action: 'compare' })
                      message.success('Đã so sánh lại số dòng.')
                      setOpenId(current.id)
                    } catch (e) {
                      message.error(e instanceof Error ? e.message : 'Không so sánh được.')
                    }
                  }}
                  title={!current ? noRow : ready ? 'Đếm lại số dòng CSDL đang chạy và bản khôi phục' : 'Chỉ khi bản khôi phục đang chờ kiểm tra'}
                >
                  So sánh lại
                </Button>
                <Button
                  icon={<SwapOutlined />}
                  disabled={!ready || current?.canSwap !== true || !ov?.agentReady}
                  onClick={() => setSwapping(current)}
                  title={
                    !current
                      ? noRow
                      : !ready
                        ? 'Chỉ hoán đổi bản khôi phục đang chờ kiểm tra'
                        : current.canSwap !== true
                          ? 'Bản khôi phục khác phiên bản CSDL – không hoán đổi được'
                          : !ov?.agentReady
                            ? (ov?.agentNote ?? 'Bộ thực thi máy này chưa sẵn sàng')
                            : 'Hoán đổi bản khôi phục thành CSDL đang chạy (xác nhận mật khẩu)'
                  }
                >
                  Hoán đổi…
                </Button>
                <Button
                  icon={<CloseCircleOutlined />}
                  disabled={!current || !(current.status === 'Ready' || current.status === 'Failed')}
                  onClick={() => current && discard(current)}
                  title={!current ? noRow : 'Xóa bản khôi phục (CSDL / máy chủ tạm), không hoán đổi'}
                >
                  Bỏ bản khôi phục
                </Button>
              </>
            )}
            {refreshButton}
          </>
        }
        notice={
          <>
            {notice}
            {runs.error && <Alert type="error" showIcon title={runs.error.message} />}
            <TruncatedNotice truncated={runs.data?.truncated} />
          </>
        }
      />
      {modals}
    </>
  )
}
