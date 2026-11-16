import { CopyOutlined, EditOutlined, KeyOutlined, PlusOutlined, ReloadOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import { Alert, App, Button, Checkbox, Form, Input, Modal } from 'antd'
import { useState } from 'react'
import {
  CREATE_KEY_CONFIRMATION,
  useAgentCommand,
  useAgentCommands,
  useAgentEnvironments,
  useCancelAgentCommand,
  useCreateAgentKey,
  useQueueAgentCommand,
  useSaveAgentEnvironment,
  type AgentCommand,
  type AgentEnvironment,
  type AgentKeyCreated,
  type AgentKeyKind,
} from '../../api/agent'
import { defaultLogRange } from '../../api/logs'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { Select } from '../../classic/Select'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { ListPage } from '../../components/ListPage/ListPage'
import { copyText } from '../../utils/clipboard'
import { formatDateTime } from '../../utils/format'
import { LogDateRangeFilter, TruncatedNotice } from './logFilters'
import { formatDuration } from './opsFormat'
import { statusLabels } from './overviewColumns'

/** Tên môi trường. */
const environmentLabels: Record<string, string> = { Dev: 'DEV – lập trình', Test: 'TEST – dùng thử', Prod: 'PROD – dùng thật' }

/** Cột tab Môi trường: đèn liên lạc kèm chữ (ui-style.md mục 2 – màu không là thông tin duy nhất). */
const environmentColumns: GridColumn<AgentEnvironment>[] = [
  { key: 'code', title: 'Môi trường', width: 90, value: (r) => r.code },
  { key: 'name', title: 'Tên', width: 200, value: (r) => (r.isCurrent ? `${r.name} (máy này)` : r.name) },
  {
    key: 'status',
    title: 'Tình trạng',
    width: 115,
    type: 'status',
    value: (r) => r.statusLabel,
    tone: (r) => statusLabels[r.light]?.tone ?? 'neutral',
  },
  { key: 'lastSeenAt', title: 'Liên lạc cuối', width: 125, type: 'datetime', value: (r) => r.lastSeenAt },
  { key: 'agentVersion', title: 'Bộ thực thi', width: 85, value: (r) => r.agentVersion },
  { key: 'appVersion', title: 'Phần mềm', width: 80, value: (r) => r.appVersion },
  { key: 'hostName', title: 'Máy', width: 110, value: (r) => r.hostName },
  { key: 'key', title: 'Khóa', width: 75, value: (r) => (r.hasKey ? `…${r.keyHint ?? ''}` : 'Chưa có') },
  {
    key: 'keyCreated',
    title: 'Tạo khóa',
    width: 170,
    value: (r) => r.keyCreatedAt,
    render: (r) => (r.keyCreatedAt ? `${formatDateTime(r.keyCreatedAt)} · ${r.keyCreatedByName ?? ''}` : ''),
  },
  {
    key: 'serviceKey',
    title: 'Khóa dịch vụ',
    width: 95,
    value: (r) => (r.hasServiceKey ? `…${r.serviceKeyHint ?? ''}` : ''),
  },
  { key: 'queued', title: 'Lệnh chờ', width: 70, type: 'quantity', value: (r) => r.queuedCount, render: (r) => String(r.queuedCount) },
  { key: 'address', title: 'Địa chỉ', width: 200, value: (r) => r.address },
  { key: 'note', title: 'Ghi chú', width: 420, value: (r) => r.note },
]

/** Cột tab Lệnh. */
const commandColumns: GridColumn<AgentCommand>[] = [
  { key: 'createdAt', title: 'Ra lệnh lúc', width: 125, type: 'datetime', value: (r) => r.createdAt },
  { key: 'environment', title: 'Môi trường', width: 90, value: (r) => r.environmentCode, groupable: true },
  { key: 'kind', title: 'Lệnh', width: 120, value: (r) => r.kindLabel, groupable: true },
  {
    key: 'status',
    title: 'Tình trạng',
    width: 95,
    type: 'status',
    value: (r) => r.statusLabel,
    tone: (r) => statusLabels[r.light]?.tone ?? 'neutral',
    groupable: true,
  },
  { key: 'createdBy', title: 'Người ra lệnh', width: 110, value: (r) => r.createdByName, groupable: true },
  { key: 'reason', title: 'Lý do', width: 220, value: (r) => r.reason },
  { key: 'startedAt', title: 'Bộ thực thi nhận', width: 125, type: 'datetime', value: (r) => r.startedAt },
  { key: 'duration', title: 'Thời gian chạy', width: 105, value: (r) => r.durationSeconds, render: (r) => formatDuration(r.durationSeconds) },
  { key: 'expiresAt', title: 'Hạn nhận', width: 125, type: 'datetime', value: (r) => r.expiresAt },
  { key: 'result', title: 'Kết quả', width: 420, value: (r) => r.resultMessage },
]

type AgtTab = 'environments' | 'commands'

const tabItems: { key: AgtTab; label: string }[] = [
  { key: 'environments', label: 'Môi trường' },
  { key: 'commands', label: 'Lệnh' },
]

/**
 * Hộp thoại khai / sửa môi trường. Sửa thì không đổi mã (Dev / Test / Prod).
 * @param editing Môi trường đang sửa; undefined = khai mới; null = đóng.
 * @param onClose Đóng hộp thoại.
 */
function EnvironmentModal({ editing, onClose }: { editing: AgentEnvironment | null | undefined; onClose: () => void }) {
  const { message } = App.useApp()
  const [form] = Form.useForm<{ code: string; name: string; address: string; isActive: boolean }>()
  const [error, setError] = useState<string | null>(null)
  const save = useSaveAgentEnvironment()
  const open = editing !== null
  const close = () => {
    setError(null)
    onClose()
  }

  return (
    <Modal
      title={editing ? `Sửa môi trường ${editing.code}` : 'Khai môi trường'}
      open={open}
      onCancel={close}
      onOk={() => form.submit()}
      okText="Lưu"
      okButtonProps={{ icon: <PlusOutlined /> }}
      cancelText="Bỏ qua"
      confirmLoading={save.isPending}
      destroyOnHidden
      width={480}
    >
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        initialValues={editing ? { ...editing, address: editing.address ?? '' } : { code: 'Test', name: '', address: '', isActive: true }}
        onFinish={async (v) => {
          setError(null)
          try {
            await save.mutateAsync({ id: editing?.id, code: v.code, name: v.name, address: v.address?.trim() || null, isActive: v.isActive })
            message.success(editing ? `Đã lưu môi trường ${editing.code}.` : `Đã khai môi trường ${v.code} – bấm "Tạo khóa…" để lấy khóa cho máy đó.`)
            close()
          } catch (e) {
            setError(e instanceof Error ? e.message : 'Không lưu được môi trường.')
          }
        }}
      >
        <Form.Item label="Môi trường" name="code" rules={[{ required: true, message: 'Chọn môi trường.' }]}>
          <Select
            disabled={!!editing}
            showSearch={false}
            options={Object.entries(environmentLabels).map(([value, label]) => ({ value, label }))}
          />
        </Form.Item>
        <Form.Item label="Tên" name="name" rules={[{ required: true, message: 'Nhập tên môi trường.' }]}>
          <Input maxLength={100} placeholder="Ví dụ: Máy TEST – phòng máy tầng 3" />
        </Form.Item>
        <Form.Item label="Địa chỉ web" name="address" extra="Không bắt buộc, ví dụ https://test.congty.local – để mở nhanh, đọc trạng thái ở lát sau.">
          <Input maxLength={300} />
        </Form.Item>
        <Form.Item name="isActive" valuePropName="checked" extra="Ngừng dùng thì bộ thực thi của máy đó bị từ chối.">
          <Checkbox>Đang dùng</Checkbox>
        </Form.Item>
      </Form>
    </Modal>
  )
}

/** Chữ của hai loại khóa trên hộp thoại. */
const keyTexts: Record<AgentKeyKind, { title: string; name: string; purpose: string; paste: string }> = {
  agent: {
    title: 'Khóa bộ thực thi',
    name: 'khóa bộ thực thi',
    purpose: 'Khóa cho phép ra lệnh quyền cao trên máy chủ đó.',
    paste: 'Dán khóa khi chạy qt-agent-install.ps1 trên máy đó (deploy-windows.md mục 4d). Không gửi khóa qua email, chat.',
  },
  service: {
    title: 'Khóa dịch vụ',
    name: 'khóa dịch vụ',
    purpose: 'Khóa cho máy chủ đó gọi máy này – máy TEST chuyển báo lỗi / góp ý của người dùng về trung tâm phát hành.',
    paste:
      'Dán vào Operations:Upstream:ServiceKey trong appsettings.Production.json của máy đó rồi khởi động lại site (deploy-windows.md mục 4h). Không gửi khóa qua email, chat.',
  },
}

/**
 * Hộp thoại tạo khóa – xác nhận việc nguy hiểm Q6 (mật khẩu + gõ "TẠO KHÓA"): khóa bộ thực thi (ra lệnh quyền cao trên máy chủ) hoặc khóa dịch vụ
 * (lát QT9 – máy đó gọi máy này); khóa cũ cùng loại hết hiệu lực ngay. Tạo xong hiện khóa **một lần** kèm nút sao chép.
 * @param environment Môi trường; null = đóng.
 * @param kind Loại khóa.
 * @param onClose Đóng hộp thoại.
 */
function CreateKeyModal({ environment, kind, onClose }: { environment: AgentEnvironment | null; kind: AgentKeyKind; onClose: () => void }) {
  const { message } = App.useApp()
  const [form] = Form.useForm<{ password: string; confirmation: string }>()
  const [error, setError] = useState<string | null>(null)
  const [created, setCreated] = useState<AgentKeyCreated | null>(null)
  const createKey = useCreateAgentKey()
  const text = keyTexts[kind]
  const hasOld = kind === 'service' ? environment?.hasServiceKey : environment?.hasKey
  const oldHint = kind === 'service' ? environment?.serviceKeyHint : environment?.keyHint
  const close = () => {
    setError(null)
    setCreated(null)
    onClose()
  }

  if (created) {
    return (
      <Modal
        title={`${text.title} ${environment?.code ?? ''}`}
        open
        onCancel={close}
        footer={
          <Button type="primary" onClick={close}>
            Đã chép khóa – đóng
          </Button>
        }
        width={560}
      >
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 8 }}
          title="Khóa chỉ hiện một lần – máy chủ chỉ lưu băm."
          description={text.paste}
        />
        <Input.TextArea readOnly value={created.key} autoSize data-testid="agent-key" style={{ fontFamily: 'Consolas, monospace' }} />
        <Button
          icon={<CopyOutlined />}
          style={{ marginTop: 8 }}
          onClick={async () => {
            if (await copyText(created.key)) message.success('Đã sao chép khóa.')
            else message.warning('Trình duyệt không cho sao chép – chọn chữ rồi Ctrl+C.')
          }}
        >
          Sao chép khóa
        </Button>
      </Modal>
    )
  }

  return (
    <Modal
      title={environment ? `Tạo ${text.name} ${environment.code}` : 'Tạo khóa'}
      open={environment != null}
      onCancel={close}
      onOk={() => form.submit()}
      okText="Tạo khóa"
      okButtonProps={{ icon: <KeyOutlined /> }}
      cancelText="Không tạo"
      confirmLoading={createKey.isPending}
      destroyOnHidden
      width={520}
    >
      <Alert
        type="warning"
        showIcon
        style={{ marginBottom: 8 }}
        title={hasOld ? `Khóa cũ (…${oldHint ?? ''}) hết hiệu lực ngay.` : text.purpose}
        description={`${kind === 'service' ? 'Máy đó' : 'Bộ thực thi trên máy đó'} phải dán khóa mới mới liên lạc lại được. Việc này ghi vào nhật ký vận hành.`}
      />
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        onFinish={async (v) => {
          if (!environment) return
          setError(null)
          try {
            setCreated(await createKey.mutateAsync({ environmentId: environment.id, kind, ...v }))
          } catch (e) {
            setError(e instanceof Error ? e.message : 'Không tạo được khóa.')
          }
        }}
      >
        <Form.Item label="Mật khẩu của bạn" name="password" rules={[{ required: true, message: 'Nhập lại mật khẩu để xác nhận.' }]}>
          <Input.Password autoComplete="current-password" autoFocus />
        </Form.Item>
        <Form.Item
          label={`Gõ "${CREATE_KEY_CONFIRMATION}" để xác nhận`}
          name="confirmation"
          rules={[
            {
              validator: (_, value?: string) =>
                value?.trim().normalize().toUpperCase() === CREATE_KEY_CONFIRMATION
                  ? Promise.resolve()
                  : Promise.reject(new Error(`Gõ đúng "${CREATE_KEY_CONFIRMATION}".`)),
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
 * Hộp thoại "Lệnh thử" (Ping): bộ thực thi báo phiên bản, tên máy, tài khoản chạy – không đổi gì trên máy (design.md 21.8 GĐ2).
 * @param environment Môi trường; null = đóng.
 * @param onClose Đóng hộp thoại.
 * @param onQueued Đã ra lệnh.
 */
function PingModal({
  environment,
  onClose,
  onQueued,
}: {
  environment: AgentEnvironment | null
  onClose: () => void
  onQueued: () => void
}) {
  const [form] = Form.useForm<{ reason: string }>()
  const [error, setError] = useState<string | null>(null)
  const queue = useQueueAgentCommand()
  const close = () => {
    setError(null)
    onClose()
  }
  return (
    <Modal
      title={environment ? `Lệnh thử – ${environment.code}` : 'Lệnh thử'}
      open={environment != null}
      onCancel={close}
      onOk={() => form.submit()}
      okText="Gửi lệnh"
      okButtonProps={{ icon: <SendOutlined /> }}
      cancelText="Bỏ qua"
      confirmLoading={queue.isPending}
      destroyOnHidden
      width={480}
    >
      <div style={{ marginBottom: 8 }}>
        Bộ thực thi nhận lệnh ở lần chạy kế tiếp (mỗi phút), báo phiên bản, tên máy, tài khoản chạy. Quá 1 giờ chưa nhận thì lệnh hết hạn.
      </div>
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        initialValues={{ reason: 'Kiểm tra bộ thực thi sau khi cài' }}
        onFinish={async (v) => {
          if (!environment) return
          setError(null)
          try {
            await queue.mutateAsync({ environmentId: environment.id, kind: 'Ping', reason: v.reason })
            close()
            onQueued()
          } catch (e) {
            setError(e instanceof Error ? e.message : 'Không ra lệnh được.')
          }
        }}
      >
        <Form.Item
          label="Lý do"
          name="reason"
          rules={[
            { required: true, message: 'Nhập lý do.' },
            { min: 5, message: 'Lý do ít nhất 5 ký tự.' },
          ]}
        >
          <Input maxLength={500} />
        </Form.Item>
      </Form>
    </Modal>
  )
}

/**
 * Ngăn kéo xem một lệnh: thông tin và nhật ký bộ thực thi gửi về (phông đơn cách, cuộn – khung `.erp-error-detail`).
 * @param id Lệnh.
 * @param onClose Đóng.
 */
function CommandDrawer({ id, onClose }: { id: string; onClose: () => void }) {
  const detail = useAgentCommand(id)
  const c = detail.data
  const info = (label: string, value: string | null | undefined) => (
    <Form.Item label={label}>
      <Input readOnly value={value ?? ''} title={value ?? ''} />
    </Form.Item>
  )
  return (
    <DetailDrawer
      open
      title={c ? `Lệnh: ${c.kindLabel} – ${c.environmentCode}` : 'Lệnh bộ thực thi'}
      onClose={onClose}
      loading={detail.isLoading}
      error={detail.error?.message ?? null}
      readOnly
      tabs={[
        {
          key: 'info',
          label: 'Thông tin',
          children: c && (
            <>
              {info('Môi trường', `${c.environmentCode} – ${c.environmentName}`)}
              {info('Lệnh', c.kindLabel)}
              {info('Tình trạng', c.statusLabel)}
              {info('Người ra lệnh', `${c.createdByName ?? ''} · ${formatDateTime(c.createdAt)}`)}
              {info('Lý do', c.reason)}
              {info('Hạn nhận', formatDateTime(c.expiresAt))}
              {info('Bộ thực thi nhận', c.startedAt ? formatDateTime(c.startedAt) : '')}
              {info('Kết thúc', c.finishedAt ? `${formatDateTime(c.finishedAt)} · chạy ${formatDuration(c.durationSeconds)}` : '')}
              {c.cancelledByName && info('Người hủy', c.cancelledByName)}
              <Form.Item label="Kết quả">
                <Input.TextArea readOnly value={c.resultMessage ?? ''} autoSize={{ minRows: 2, maxRows: 5 }} />
              </Form.Item>
            </>
          ),
        },
        {
          key: 'journal',
          label: 'Nhật ký bộ thực thi',
          lazy: true,
          children: (
            <pre className="erp-error-detail" data-testid="agent-journal">
              {c?.journal ?? 'Bộ thực thi chưa gửi nhật ký.'}
            </pre>
          ),
        },
      ]}
    />
  )
}

/**
 * Màn hình AGT – Bộ thực thi (lát QT5, docs/ke-hoach-qt.md QT5; ui-style.md mục 5n). Hai tab trên khuôn ListPage + DataGrid:
 * - **Môi trường**: DEV / TEST / PROD đã khai, tình trạng liên lạc (đèn kèm chữ), phiên bản bộ thực thi / phần mềm, khóa (4 ký tự cuối). Nút
 *   Khai môi trường, Sửa, Tạo khóa… (Q6), Lệnh thử – thao tác trên dòng đang chọn.
 * - **Lệnh**: lịch sử lệnh theo khoảng ngày, tự làm mới 5 giây khi còn lệnh chờ / chạy; bấm đúp mở ngăn kéo có nhật ký; Hủy lệnh còn chờ.
 * Chỉ ở tổng công ty, quyền System.OpsAgent.View; nút cần System.OpsAgent.Manage.
 */
export function AgentPage() {
  const { message, modal } = App.useApp()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(Permission.AgentManage)
  const [tab, setTab] = useState<AgtTab>('environments')
  const [range, setRange] = useState(defaultLogRange)
  const [environment, setEnvironment] = useState<AgentEnvironment | null>(null)
  const [command, setCommand] = useState<AgentCommand | null>(null)
  const [editing, setEditing] = useState<AgentEnvironment | null | undefined>(null)
  const [keyFor, setKeyFor] = useState<{ environment: AgentEnvironment; kind: AgentKeyKind } | null>(null)
  const [pingFor, setPingFor] = useState<AgentEnvironment | null>(null)
  const [openId, setOpenId] = useState<string | null>(null)
  const environments = useAgentEnvironments()
  const commands = useAgentCommands(range)
  const cancel = useCancelAgentCommand()
  const viewTabs = { items: tabItems, activeKey: tab, onChange: (k: string) => setTab(k as AgtTab) }

  const refreshButton = (
    <Button
      icon={<ReloadOutlined />}
      loading={environments.isFetching || commands.isFetching}
      onClick={() => void (tab === 'environments' ? environments.refetch() : commands.refetch())}
      title="Lấy lại số liệu của tab đang xem"
    >
      Làm mới
    </Button>
  )

  if (tab === 'environments') {
    const noRow = 'Chọn một môi trường trong lưới'
    const pingBlocked = !environment ? noRow : !environment.isActive ? 'Môi trường đang ngừng dùng' : !environment.hasKey ? 'Chưa có khóa – tạo khóa trước' : null
    const rows = environments.data ?? []
    return (
      <>
        <ListPage<AgentEnvironment>
          key="environments"
          title="Bộ thực thi"
          viewTabs={viewTabs}
          exportFileName="bo-thuc-thi-moi-truong"
          rows={rows}
          columns={environmentColumns}
          rowKey={(r) => r.id}
          loading={environments.isLoading}
          canCreate={false}
          onSelectRow={setEnvironment}
          onOpenRow={canManage ? setEditing : undefined}
          isMarked={(r) => r.light === 'Warning' || r.light === 'Error'}
          actions={
            <>
              {canManage && (
                <>
                  <Button icon={<PlusOutlined />} onClick={() => setEditing(undefined)} title="Khai môi trường DEV / TEST / PROD">
                    Khai môi trường…
                  </Button>
                  <Button icon={<EditOutlined />} disabled={!environment} onClick={() => setEditing(environment)} title={environment ? `Sửa ${environment.code}` : noRow}>
                    Sửa…
                  </Button>
                  <Button
                    icon={<KeyOutlined />}
                    disabled={!environment?.isActive}
                    onClick={() => environment && setKeyFor({ environment, kind: 'agent' })}
                    title={!environment ? noRow : !environment.isActive ? 'Môi trường đang ngừng dùng' : `Tạo khóa bộ thực thi ${environment.code}`}
                  >
                    Tạo khóa…
                  </Button>
                  <Button
                    icon={<KeyOutlined />}
                    disabled={!environment?.isActive}
                    onClick={() => environment && setKeyFor({ environment, kind: 'service' })}
                    title={
                      !environment
                        ? noRow
                        : !environment.isActive
                          ? 'Môi trường đang ngừng dùng'
                          : `Khóa cho máy ${environment.code} gọi máy này (máy TEST chuyển báo lỗi về PROD)`
                    }
                  >
                    Tạo khóa dịch vụ…
                  </Button>
                  <Button
                    icon={<SendOutlined />}
                    disabled={pingBlocked != null}
                    onClick={() => setPingFor(environment)}
                    title={pingBlocked ?? `Gửi lệnh thử cho bộ thực thi ${environment?.code}`}
                  >
                    Lệnh thử
                  </Button>
                </>
              )}
              {refreshButton}
            </>
          }
          notice={
            environments.error ? (
              <Alert type="error" showIcon title={environments.error.message} />
            ) : environments.data ? (
              <Alert
                type={rows.some((r) => r.light === 'Warning' || r.light === 'Error') ? 'warning' : 'info'}
                showIcon
                title={
                  rows.length === 0
                    ? 'Chưa khai môi trường nào – bấm "Khai môi trường…", tạo khóa, rồi cài bộ thực thi trên máy đó (deploy-windows.md mục 4d).'
                    : `${rows.length} môi trường · ${rows.filter((r) => r.light === 'Ok').length} đang liên lạc`
                }
                description="Bộ thực thi trên mỗi máy tự hỏi lệnh mỗi phút bằng khóa riêng (không mở cổng vào máy); liên lạc trong 3 phút thì đèn xanh."
              />
            ) : undefined
          }
        />
        <EnvironmentModal editing={editing} onClose={() => setEditing(null)} />
        <CreateKeyModal environment={keyFor?.environment ?? null} kind={keyFor?.kind ?? 'agent'} onClose={() => setKeyFor(null)} />
        <PingModal
          environment={pingFor}
          onClose={() => setPingFor(null)}
          onQueued={() => {
            message.success('Đã gửi lệnh thử – bộ thực thi nhận ở lần chạy kế tiếp; kết quả ở tab Lệnh.')
            setTab('commands')
          }}
        />
      </>
    )
  }

  const page = commands.data
  const cancelBlocked = !command ? 'Chọn một lệnh trong lưới' : command.status !== 'Queued' ? 'Chỉ hủy được lệnh đang chờ' : null
  return (
    <>
      <ListPage<AgentCommand>
        key="commands"
        title="Bộ thực thi"
        viewTabs={viewTabs}
        exportFileName="bo-thuc-thi-lenh"
        exportSubtitle={`Lệnh bộ thực thi từ ${range.from} đến ${range.to}`}
        rows={page?.items ?? []}
        columns={commandColumns}
        rowKey={(r) => r.id}
        loading={commands.isLoading}
        canCreate={false}
        onSelectRow={setCommand}
        onOpenRow={(r) => setOpenId(r.id)}
        isMarked={(r) => r.light === 'Error'}
        filters={<LogDateRangeFilter value={range} onChange={setRange} />}
        onClearFilters={() => setRange(defaultLogRange())}
        actions={
          <>
            {canManage && (
              <Button
                icon={<StopOutlined />}
                disabled={cancelBlocked != null}
                title={cancelBlocked ?? 'Hủy lệnh trước khi bộ thực thi nhận'}
                onClick={() =>
                  command &&
                  modal.confirm({
                    title: `Hủy lệnh ${command.kindLabel} – ${command.environmentCode}?`,
                    content: 'Bộ thực thi sẽ không nhận lệnh này.',
                    okText: 'Hủy lệnh',
                    okButtonProps: { danger: true, icon: <StopOutlined /> },
                    cancelText: 'Không hủy',
                    onOk: async () => {
                      try {
                        await cancel.mutateAsync(command.id)
                        message.success('Đã hủy lệnh.')
                      } catch (e) {
                        message.error(e instanceof Error ? e.message : 'Không hủy được lệnh.')
                      }
                    },
                  })
                }
              >
                Hủy lệnh
              </Button>
            )}
            {refreshButton}
          </>
        }
        notice={
          commands.error ? <Alert type="error" showIcon title={commands.error.message} /> : <TruncatedNotice truncated={page?.truncated} />
        }
      />
      {openId && <CommandDrawer id={openId} onClose={() => setOpenId(null)} />}
    </>
  )
}
