import { CheckCircleOutlined, EditOutlined, LogoutOutlined, ReloadOutlined, ToolOutlined } from '@ant-design/icons'
import { Alert, App, Button, Checkbox, Form, Input, InputNumber, Modal } from 'antd'
import { useState } from 'react'
import {
  SIGN_OUT_ALL_CONFIRMATION,
  useAccessConfig,
  useSaveAccessConfig,
  useSignOutAll,
  type AccessConfig,
  type AccessPolicyValues,
} from '../../api/accessConfig'
import { useMaintenanceMode, type MaintenanceMode } from '../../api/agent'
import type { SystemOverviewRow } from '../../api/operations'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { ListPage } from '../../components/ListPage/ListPage'
import { formatDateTime, formatQuantity } from '../../utils/format'
import { MaintenanceModeModal } from './MaintenanceModeModal'
import { overviewColumns } from './overviewColumns'

type CfgTab = 'policy' | 'security' | 'maintenance'

const tabItems: { key: CfgTab; label: string }[] = [
  { key: 'policy', label: 'Chính sách' },
  { key: 'security', label: 'Bảo mật' },
  { key: 'maintenance', label: 'Bảo trì' },
]

/** Độ dài mật khẩu khuyên dùng cho TEST, PROD (ke-hoach-qt.md – bản 0.35.0 đặt khi cài). */
const RECOMMENDED_MIN_PASSWORD = 10

/**
 * Dòng tab "Chính sách" dựng từ cấu hình: cùng cột Nhóm – Hạng mục – Giá trị – Tình trạng – Ghi chú như SYS. Đèn vàng khi lỏng hơn mức khuyên
 * dùng cho TEST / PROD (design.md 21.3a: TEST bảo vệ như PROD).
 */
function policyRows(c: AccessConfig): SystemOverviewRow[] {
  const blockedCount = c.extraBlockedPasswords?.split('\n').filter((s) => s.trim()).length
  const ranges = c.opsAllowedIpRanges?.split('\n').filter((s) => s.trim()) ?? []
  const hours = c.sessionMinutes / 60
  return [
    {
      group: 'Mật khẩu',
      item: 'Độ dài tối thiểu',
      value: `${c.minPasswordLength} ký tự`,
      status: c.minPasswordLength >= RECOMMENDED_MIN_PASSWORD ? 'Ok' : 'Warning',
      note:
        c.minPasswordLength >= RECOMMENDED_MIN_PASSWORD
          ? 'Áp khi tạo người dùng, đặt lại, đổi mật khẩu; mật khẩu đang dùng không bị bắt đổi.'
          : `TEST, PROD nên đặt từ ${RECOMMENDED_MIN_PASSWORD} ký tự. Áp khi đặt mật khẩu mới, mật khẩu đang dùng không bị bắt đổi.`,
    },
    {
      group: 'Mật khẩu',
      item: 'Chặn mật khẩu phổ biến',
      value: c.blockCommonPasswords ? `Bật (${formatQuantity(c.commonPasswordCount)} mật khẩu)` : 'Tắt',
      status: c.blockCommonPasswords ? 'Ok' : 'Warning',
      note: 'Danh sách nhúng trong phần mềm (password123, 12345678, qwerty123…), so không phân biệt hoa thường.',
    },
    {
      group: 'Mật khẩu',
      item: 'Mật khẩu cấm thêm',
      value: blockedCount == null ? 'Chỉ người có quyền quản lý CFG xem được' : blockedCount === 0 ? 'Không có' : `${blockedCount} mục`,
      status: 'Neutral',
      note: 'Mẫu mật khẩu nội bộ hay dùng (tên công ty + năm…) – luôn chặn, kể cả khi tắt chặn mật khẩu phổ biến.',
    },
    {
      group: 'Đăng nhập',
      item: 'Sai mật khẩu liên tiếp',
      value: `${c.maxFailedLogins} lần thì tạm khóa ${c.lockoutMinutes} phút`,
      status: 'Ok',
      note: 'Hết thời gian tự đăng nhập được, không cần quản trị mở; quản trị đặt lại mật khẩu thì mở ngay. Áp cả cho mật khẩu xác nhận việc nguy hiểm.',
    },
    {
      group: 'Đăng nhập',
      item: 'Hạn phiên đăng nhập',
      value: `${c.sessionMinutes} phút${Number.isInteger(hours) ? ` (${hours} giờ)` : ''}`,
      status: 'Ok',
      note: 'Áp cho lần đăng nhập sau khi lưu. Đổi / đặt lại mật khẩu thì phiên cũ của người đó hết hiệu lực ngay.',
    },
    {
      group: 'Đăng nhập',
      item: 'Đăng xuất tất cả gần nhất',
      value: c.sessionsRevokedAt ? formatDateTime(c.sessionsRevokedAt) : 'Chưa dùng',
      status: 'Neutral',
      note: 'Mọi phiên cấp trước mốc này phải đăng nhập lại (dùng khi nghi lộ mật khẩu, máy bị nhiễm virus).',
    },
    {
      group: 'Quản trị vận hành',
      item: 'Dải IP được dùng',
      value: ranges.length === 0 ? 'Mọi IP' : ranges.join(', '),
      status: ranges.length === 0 ? 'Warning' : 'Ok',
      note:
        ranges.length === 0
          ? 'Chưa giới hạn – TEST, PROD nên khai dải IP máy quản trị.'
          : 'Ngoài dải thì mọi chức năng quản trị vận hành (SYS, BAK, RST, DBM, AGT, CFG…) bị chặn. Lỡ khóa mình: đăng nhập ngay trên máy chủ, hoặc đặt Security:IgnoreOpsIpRanges = true trong file cấu hình máy chủ.',
    },
    {
      group: 'Quản trị vận hành',
      item: 'IP của bạn',
      value: c.yourIp ?? 'Không rõ',
      status: 'Neutral',
      note: 'Dải IP khai phải gồm địa chỉ này – phần mềm không cho lưu dải tự khóa mình.',
    },
    {
      group: 'Giới hạn tần suất',
      item: 'Đăng nhập',
      value: `${formatQuantity(c.loginPerMinutePerIp)} lần / phút / IP`,
      status: 'Ok',
      note: 'Chống dò mật khẩu; máy chủ tự gọi (127.0.0.1) không giới hạn.',
    },
    {
      group: 'Giới hạn tần suất',
      item: 'API',
      value: `${formatQuantity(c.apiPerMinutePerUser)} lần / phút / phiên`,
      status: 'Ok',
      note: 'Vượt thì báo "Quá nhiều yêu cầu" kèm số giây chờ.',
    },
    {
      group: 'Giới hạn tần suất',
      item: 'Việc nặng cùng lúc',
      value: `${formatQuantity(c.heavyConcurrentPerUser)} việc / phiên`,
      status: 'Ok',
      note: 'Nhập / xuất Excel, báo cáo, in phiếu PDF.',
    },
  ]
}

/** Dòng tab "Bảo trì": trạng thái chế độ bảo trì. */
function maintenanceRows(m: MaintenanceMode): SystemOverviewRow[] {
  return [
    {
      group: 'Chế độ bảo trì',
      item: 'Trạng thái',
      value: m.isOn ? `Đang bật từ ${m.startedAt ? formatDateTime(m.startedAt) : '?'}` : 'Đang tắt',
      status: m.isOn ? 'Error' : 'Ok',
      note: m.isOn
        ? 'Người dùng thường (kể cả NCC trên Portal) nhận thông báo bảo trì; người có quyền quản trị vận hành vẫn vào được.'
        : 'Người dùng làm việc bình thường.',
    },
    { group: 'Chế độ bảo trì', item: 'Câu thông báo', value: m.message ?? '—', status: 'Neutral', note: 'Hiện trên dải đỏ ở mọi màn hình khi bật.' },
    {
      group: 'Chế độ bảo trì',
      item: 'Lần bật / tắt gần nhất',
      value: m.changedAt ? `${formatDateTime(m.changedAt)} – ${m.changedByName ?? ''}` : 'Chưa dùng',
      status: 'Neutral',
      note: m.reason ? `Lý do: ${m.reason}` : null,
    },
  ]
}

/**
 * Hộp thoại sửa cấu hình truy cập: mỗi tham số một ô có khoảng cho phép (máy chủ kiểm tra lại), lý do bắt buộc. Lỗi máy chủ (ví dụ dải IP không
 * gồm IP người lưu) hiện trong hộp thoại, không đóng.
 * @param config Cấu hình hiện tại; null = đóng.
 * @param onClose Đóng hộp thoại.
 */
function EditPolicyModal({ config, onClose }: { config: AccessConfig | null; onClose: () => void }) {
  const { message } = App.useApp()
  const [form] = Form.useForm<AccessPolicyValues & { reason: string }>()
  const [error, setError] = useState<string | null>(null)
  const save = useSaveAccessConfig()
  const close = () => {
    setError(null)
    onClose()
  }
  const number = (name: keyof AccessPolicyValues, label: string, min: number, max: number, unit: string) => (
    <Form.Item label={label} name={name} rules={[{ required: true, message: `Nhập ${label.toLowerCase()}.` }]} extra={`Từ ${min} đến ${formatQuantity(max)} ${unit}.`}>
      <InputNumber min={min} max={max} precision={0} style={{ width: 140 }} />
    </Form.Item>
  )
  return (
    <Modal
      title="Sửa cấu hình truy cập"
      open={config != null}
      onCancel={close}
      onOk={() => form.submit()}
      okText="Lưu"
      okButtonProps={{ icon: <CheckCircleOutlined /> }}
      cancelText="Bỏ qua"
      confirmLoading={save.isPending}
      destroyOnHidden
      width={640}
    >
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      {config && (
        <Form
          form={form}
          layout="vertical"
          preserve={false}
          initialValues={{ ...config, extraBlockedPasswords: config.extraBlockedPasswords ?? '', opsAllowedIpRanges: config.opsAllowedIpRanges ?? '', reason: '' }}
          onFinish={async (v) => {
            setError(null)
            try {
              await save.mutateAsync({
                ...v,
                extraBlockedPasswords: v.extraBlockedPasswords?.trim() ? v.extraBlockedPasswords : null,
                opsAllowedIpRanges: v.opsAllowedIpRanges?.trim() ? v.opsAllowedIpRanges : null,
              })
              message.success('Đã lưu cấu hình truy cập.')
              close()
            } catch (e) {
              setError(e instanceof Error ? e.message : 'Không lưu được cấu hình truy cập.')
            }
          }}
        >
          <div className="erp-cfg-form-grid">
            {number('minPasswordLength', 'Độ dài mật khẩu tối thiểu', 8, 64, 'ký tự')}
            {number('maxFailedLogins', 'Số lần sai mật khẩu thì khóa', 3, 20, 'lần')}
            {number('lockoutMinutes', 'Số phút tạm khóa', 1, 1440, 'phút')}
            {number('sessionMinutes', 'Hạn phiên đăng nhập', 15, 1440, 'phút')}
            {number('loginPerMinutePerIp', 'Đăng nhập / phút / IP', 5, 1000, 'lần')}
            {number('apiPerMinutePerUser', 'API / phút / phiên', 60, 10000, 'lần')}
            {number('heavyConcurrentPerUser', 'Việc nặng cùng lúc / phiên', 1, 20, 'việc')}
          </div>
          <Form.Item name="blockCommonPasswords" valuePropName="checked">
            <Checkbox>Chặn mật khẩu phổ biến ({formatQuantity(config.commonPasswordCount)} mật khẩu nhúng sẵn)</Checkbox>
          </Form.Item>
          <Form.Item label="Mật khẩu cấm thêm" name="extraBlockedPasswords" extra="Mỗi dòng một mật khẩu, ví dụ tên công ty + năm.">
            <Input.TextArea maxLength={4000} autoSize={{ minRows: 2, maxRows: 5 }} />
          </Form.Item>
          <Form.Item
            label="Dải IP được dùng chức năng quản trị vận hành"
            name="opsAllowedIpRanges"
            extra={`Mỗi dòng một dải (10.0.0.0/8) hoặc một địa chỉ; để trống = mọi IP. IP của bạn: ${config.yourIp ?? 'không rõ'} – phải nằm trong dải.`}
          >
            <Input.TextArea maxLength={2000} autoSize={{ minRows: 2, maxRows: 5 }} placeholder="Để trống = mọi IP" />
          </Form.Item>
          <Form.Item
            label="Lý do"
            name="reason"
            rules={[
              { required: true, message: 'Nhập lý do.' },
              { min: 5, message: 'Lý do ít nhất 5 ký tự.' },
            ]}
          >
            <Input maxLength={500} placeholder="Ví dụ: Áp chính sách mật khẩu cho PROD" />
          </Form.Item>
        </Form>
      )}
    </Modal>
  )
}

/**
 * Hộp thoại "Đăng xuất tất cả" – việc nguy hiểm Q6 (ui-style.md mục 5m): cảnh báo vàng, lý do, mật khẩu của bạn, gõ chữ xác nhận; lỗi máy chủ là
 * khối đỏ trong hộp thoại. Xong thì phiên đang dùng nhận token mới (không bị đăng xuất).
 * @param open Mở hộp thoại.
 * @param onClose Đóng hộp thoại.
 */
function SignOutAllModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { message } = App.useApp()
  const { renewToken } = useAuth()
  const [form] = Form.useForm<{ reason: string; password: string; confirmation: string }>()
  const [error, setError] = useState<string | null>(null)
  const signOutAll = useSignOutAll()
  const close = () => {
    form.resetFields()
    setError(null)
    onClose()
  }
  return (
    <Modal
      title="Đăng xuất tất cả"
      open={open}
      onCancel={close}
      onOk={() => form.submit()}
      okText="Đăng xuất tất cả"
      okButtonProps={{ danger: true, icon: <LogoutOutlined /> }}
      cancelText="Không đăng xuất"
      confirmLoading={signOutAll.isPending}
      destroyOnHidden
      width={520}
    >
      <Alert
        type="warning"
        showIcon
        style={{ marginBottom: 8 }}
        title="Mọi người đang đăng nhập (kể cả các phiên khác của bạn) phải đăng nhập lại; phiếu đang nhập dở chưa lưu sẽ mất."
        description="Dùng khi nghi lộ mật khẩu hoặc máy bị nhiễm virus. Phiên bạn đang dùng được giữ lại. Việc này ghi vào nhật ký vận hành."
      />
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form
        form={form}
        layout="vertical"
        onFinish={async (v) => {
          setError(null)
          try {
            const result = await signOutAll.mutateAsync(v)
            renewToken(result.accessToken, result.expiresAt)
            message.success('Đã đăng xuất mọi phiên đăng nhập khác.')
            close()
          } catch (e) {
            setError(e instanceof Error ? e.message : 'Không đăng xuất được.')
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
          <Input.TextArea autoFocus maxLength={500} autoSize={{ minRows: 2, maxRows: 4 }} />
        </Form.Item>
        <Form.Item label="Mật khẩu của bạn" name="password" rules={[{ required: true, message: 'Nhập lại mật khẩu để xác nhận.' }]}>
          <Input.Password autoComplete="current-password" />
        </Form.Item>
        <Form.Item
          label={`Gõ "${SIGN_OUT_ALL_CONFIRMATION}" để xác nhận`}
          name="confirmation"
          rules={[
            {
              validator: (_, value?: string) =>
                value?.trim().normalize().toUpperCase() === SIGN_OUT_ALL_CONFIRMATION
                  ? Promise.resolve()
                  : Promise.reject(new Error(`Gõ đúng "${SIGN_OUT_ALL_CONFIRMATION}".`)),
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
 * Màn hình CFG – Cấu hình truy cập (design.md mục 21.4, 21.16 – lát QT7; ui-style.md mục 5p). Ba tab trên khuôn ListPage + DataGrid (cùng cột với
 * SYS):
 * - **Chính sách**: mật khẩu (độ dài, phổ biến, cấm thêm), khóa khi sai, hạn phiên, dải IP quản trị, giới hạn tần suất; đèn vàng khi lỏng hơn mức
 *   khuyên dùng cho TEST / PROD. Nút "Sửa cấu hình…", "Đăng xuất tất cả…" (Q6, icon đỏ).
 * - **Bảo mật**: header bảo mật, HTTPS, proxy tin cậy, giới hạn Portal (chỉ đọc – từ máy chủ).
 * - **Bảo trì**: chế độ bảo trì; nút "Bật / Tắt chế độ bảo trì…" (chuyển từ SYS).
 * Chỉ ở tổng công ty; xem cần System.Ops.View, sửa cần System.OpsConfig.Manage, bảo trì cần System.OpsMaintenanceMode.Manage.
 */
export function AccessConfigPage() {
  const config = useAccessConfig()
  const maintenance = useMaintenanceMode()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(Permission.ConfigManage)
  const canToggle = hasPermission(Permission.MaintenanceModeManage)
  const [tab, setTab] = useState<CfgTab>('policy')
  const [editing, setEditing] = useState<AccessConfig | null>(null)
  const [signingOut, setSigningOut] = useState(false)
  const [toggling, setToggling] = useState<MaintenanceMode | null>(null)
  const data = config.data
  const mode = maintenance.data

  const rows = tab === 'policy' ? (data ? policyRows(data) : []) : tab === 'security' ? (data?.security ?? []) : mode ? maintenanceRows(mode) : []
  const problems = rows.filter((r) => r.status === 'Error' || r.status === 'Warning').length
  const error = tab === 'maintenance' ? maintenance.error : config.error

  return (
    <>
      <ListPage<SystemOverviewRow>
        title="Cấu hình truy cập"
        exportFileName="cau-hinh-truy-cap"
        rows={rows}
        columns={overviewColumns}
        rowKey={(r) => `${r.group}|${r.item}`}
        loading={tab === 'maintenance' ? maintenance.isLoading : config.isLoading}
        canCreate={false}
        viewTabs={{ items: tabItems, activeKey: tab, onChange: (k: string) => setTab(k as CfgTab) }}
        actions={
          <>
            {tab === 'policy' && canManage && (
              <>
                <Button icon={<EditOutlined />} disabled={!data} onClick={() => setEditing(data ?? null)} title="Sửa chính sách mật khẩu, khóa, phiên, dải IP, giới hạn">
                  Sửa cấu hình…
                </Button>
                <Button icon={<LogoutOutlined />} onClick={() => setSigningOut(true)} title="Buộc mọi người đăng nhập lại (giữ phiên của bạn)">
                  Đăng xuất tất cả…
                </Button>
              </>
            )}
            {tab === 'maintenance' && canToggle && mode && (
              <Button
                icon={mode.isOn ? <CheckCircleOutlined /> : <ToolOutlined />}
                onClick={() => setToggling(mode)}
                title={mode.isOn ? 'Cho người dùng vào lại' : 'Chặn người dùng thường trong lúc cài đặt / khôi phục'}
              >
                {mode.isOn ? 'Tắt chế độ bảo trì…' : 'Bật chế độ bảo trì…'}
              </Button>
            )}
            <Button
              icon={<ReloadOutlined />}
              loading={config.isFetching || maintenance.isFetching}
              onClick={() => {
                void config.refetch()
                void maintenance.refetch()
              }}
              title="Lấy lại số liệu"
            >
              Làm mới
            </Button>
          </>
        }
        notice={
          error ? (
            <Alert type="error" showIcon title={error.message} />
          ) : tab === 'maintenance' ? (
            mode?.isOn ? (
              <Alert type="error" showIcon data-testid="cfg-maintenance-notice" title={`Đang bảo trì: ${mode.message ?? ''}`} />
            ) : undefined
          ) : data ? (
            <Alert
              type={problems > 0 ? 'warning' : 'info'}
              showIcon
              title={`${data.updatedAt ? `Lưu lần cuối ${formatDateTime(data.updatedAt)} – ${data.updatedByName ?? ''}` : 'Đang dùng giá trị mặc định'}${
                problems > 0 ? ` · ${problems} hạng mục cần xem` : ' · mọi hạng mục ổn'
              }`}
            />
          ) : undefined
        }
      />
      <EditPolicyModal config={editing} onClose={() => setEditing(null)} />
      <SignOutAllModal open={signingOut} onClose={() => setSigningOut(false)} />
      <MaintenanceModeModal mode={toggling} onClose={() => setToggling(null)} />
    </>
  )
}
