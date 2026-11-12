import { CheckCircleOutlined, CopyOutlined, MailOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import { Alert, App, Button, Form, Input, Modal } from 'antd'
import { useState } from 'react'
import {
  portalUserStatusLabels,
  type PortalInviteInput,
  type PortalInviteResult,
  type SupplierPortalUser,
} from '../../api/supplierPortal'
import { copyText } from '../../utils/clipboard'
import { formatDateTime } from '../../utils/format'
import { DataGrid } from '../DataGrid/DataGrid'
import type { GridColumn } from '../DataGrid/types'

/** Thuộc tính bảng tài khoản Portal. */
interface PortalUserPanelProps {
  /** Tài khoản của NCC. */
  rows: SupplierPortalUser[]
  loading: boolean
  /** Được mời / khóa / mở khóa. */
  canManage: boolean
  /** Người đang thao tác là quản trị NCC (trên Portal): không tự khóa mình, không khóa / mở tài khoản quản trị. */
  portalSelfId?: string
  /** Nhãn nút mời, ví dụ "Mời quản trị NCC…" hoặc "Mời người dùng…". */
  inviteLabel: string
  /** Câu giải thích trên hộp thoại mời. */
  inviteHint: string
  /** Mã loại đối tượng trong nhật ký để F1 xem lịch sử ô – chỉ phía nội bộ (Portal không đọc nhật ký nội bộ). */
  auditEntity?: string
  onInvite: (input: PortalInviteInput) => Promise<PortalInviteResult>
  onResend: (userId: string) => Promise<PortalInviteResult>
  onDisable: (userId: string) => Promise<unknown>
  onEnable: (userId: string) => Promise<unknown>
}

const columns: GridColumn<SupplierPortalUser>[] = [
  { key: 'email', title: 'Email (tên đăng nhập)', width: 200, value: (r) => r.email },
  { key: 'fullName', title: 'Họ tên', width: 160, value: (r) => r.fullName },
  { key: 'position', title: 'Chức vụ', width: 110, value: (r) => r.position },
  { key: 'role', title: 'Vai trò', width: 110, auditField: false, value: (r) => (r.isSupplierAdmin ? 'Quản trị NCC' : 'Người dùng') },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 170,
    type: 'status',
    value: (r) => portalUserStatusLabels[r.status],
    tone: (r) => (r.status === 'Active' ? 'success' : r.status === 'Invited' ? 'warning' : 'error'),
  },
  { key: 'invitedAt', title: 'Ngày mời', width: 120, type: 'datetime', auditField: 'CreatedAt', value: (r) => r.invitedAt },
  { key: 'lastLoginAt', title: 'Đăng nhập gần nhất', width: 130, type: 'datetime', value: (r) => r.lastLoginAt },
]

/**
 * Bảng tài khoản Portal của một NCC + nút Mời, Gửi lại lời mời, Khóa, Mở khóa (design.md 19.5, lát P1). Dùng ở tab "Tài khoản Portal" của
 * ngăn kéo NCC (Phòng CUVT – mời quản trị NCC) và trang "Người dùng" của Portal (quản trị NCC – mời người dùng con). Mời xong hiện link
 * kích hoạt để người mời tự chuyển khi máy chủ chưa gửi được email.
 */
export function PortalUserPanel({
  rows,
  loading,
  canManage,
  portalSelfId,
  inviteLabel,
  inviteHint,
  auditEntity,
  onInvite,
  onResend,
  onDisable,
  onEnable,
}: PortalUserPanelProps) {
  const { message, modal } = App.useApp()
  const [groupBy, setGroupBy] = useState<string | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [inviting, setInviting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [result, setResult] = useState<PortalInviteResult | null>(null)
  const [form] = Form.useForm<PortalInviteInput>()
  const selected = rows.find((r) => r.id === selectedId) ?? null
  // Quản trị NCC không thao tác trên chính mình và trên tài khoản quản trị (chỉ Phòng CUVT khóa / mở).
  const lockedForPortal = portalSelfId !== undefined && selected !== null && (selected.id === portalSelfId || selected.isSupplierAdmin)

  const run = async (action: () => Promise<unknown>, done: string) => {
    setBusy(true)
    try {
      await action()
      void message.success(done)
    } catch (e) {
      void message.error(e instanceof Error ? e.message : 'Không thực hiện được.')
    } finally {
      setBusy(false)
    }
  }

  const submitInvite = async () => {
    const values = await form.validateFields()
    setBusy(true)
    setError(null)
    try {
      const r = await onInvite({
        email: values.email.trim(),
        fullName: values.fullName.trim(),
        phone: values.phone?.trim() || null,
        position: values.position?.trim() || null,
      })
      setInviting(false)
      setResult(r)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không mời được.')
    } finally {
      setBusy(false)
    }
  }

  const resend = async () => {
    if (!selected) return
    setBusy(true)
    try {
      setResult(await onResend(selected.id))
    } catch (e) {
      void message.error(e instanceof Error ? e.message : 'Không gửi lại được lời mời.')
    } finally {
      setBusy(false)
    }
  }

  const disable = () => {
    if (!selected) return
    modal.confirm({
      title: `Khóa tài khoản Portal ${selected.email}?`,
      content: 'Người dùng bị đăng xuất ngay và không đăng nhập được cho đến khi mở khóa.',
      okText: 'Khóa',
      cancelText: 'Không',
      okButtonProps: { danger: true, icon: <StopOutlined /> },
      onOk: () => run(() => onDisable(selected.id), `Đã khóa tài khoản ${selected.email}.`),
    })
  }

  const copyLink = async () => {
    if (!result) return
    if (await copyText(result.inviteUrl)) void message.success('Đã sao chép link kích hoạt.')
    else void message.warning('Trình duyệt không cho sao chép – bôi đen link rồi bấm Ctrl+C.')
  }

  return (
    <div data-testid="portal-users">
      <div style={{ height: 220, display: 'flex', flexDirection: 'column' }}>
        <DataGrid
          rows={rows}
          columns={columns}
          rowKey={(r) => r.id}
          groupBy={groupBy}
          onGroupByChange={setGroupBy}
          loading={loading}
          auditEntity={auditEntity}
          onSelectRow={(r) => setSelectedId(r?.id ?? null)}
        />
      </div>
      {canManage && (
        <div style={{ display: 'flex', gap: 8, marginTop: 8, flexWrap: 'wrap' }}>
          <Button
            icon={<SendOutlined />}
            onClick={() => {
              // Form nằm trong hộp thoại hủy khi đóng (destroyOnHidden + preserve=false) nên mở lại là ô trống.
              setError(null)
              setInviting(true)
            }}
          >
            {inviteLabel}
          </Button>
          <Button icon={<MailOutlined />} disabled={busy || selected?.status !== 'Invited' || lockedForPortal} onClick={() => void resend()}>
            Gửi lại lời mời
          </Button>
          <Button icon={<StopOutlined />} disabled={busy || !selected || selected.status === 'Disabled' || lockedForPortal} onClick={disable}>
            Khóa
          </Button>
          <Button
            icon={<CheckCircleOutlined />}
            disabled={busy || selected?.status !== 'Disabled' || lockedForPortal}
            onClick={() => selected && void run(() => onEnable(selected.id), `Đã mở khóa tài khoản ${selected.email}.`)}
          >
            Mở khóa
          </Button>
        </div>
      )}

      <Modal
        open={inviting}
        title={inviteLabel.replace('…', '')}
        okText="Mời"
        cancelText="Đóng"
        okButtonProps={{ icon: <SendOutlined />, loading: busy }}
        onOk={() => void submitInvite()}
        onCancel={() => setInviting(false)}
        destroyOnHidden
      >
        <Alert type="info" showIcon title={inviteHint} style={{ marginBottom: 8 }} />
        {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
        {/* Tên form riêng: id ô có tiền tố, không trùng ô "email" của form NCC bên dưới (nhãn trỏ đúng ô). */}
        <Form form={form} name="portalInvite" layout="vertical" preserve={false}>
          <Form.Item name="email" label="Email (tên đăng nhập)" rules={[{ required: true, type: 'email', message: 'Nhập email đúng định dạng.' }]}>
            <Input maxLength={256} autoFocus />
          </Form.Item>
          <Form.Item name="fullName" label="Họ tên" rules={[{ required: true, whitespace: true, message: 'Nhập họ tên.' }]}>
            <Input maxLength={200} />
          </Form.Item>
          <Form.Item name="position" label="Chức vụ">
            <Input maxLength={100} />
          </Form.Item>
          <Form.Item name="phone" label="Điện thoại">
            <Input maxLength={50} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        open={result !== null}
        title="Đã tạo lời mời"
        okText="Sao chép link"
        cancelText="Đóng"
        okButtonProps={{ icon: <CopyOutlined /> }}
        onOk={() => void copyLink()}
        onCancel={() => setResult(null)}
      >
        {result && (
          <>
            <Alert
              type={result.emailSent ? 'success' : 'warning'}
              showIcon
              style={{ marginBottom: 8 }}
              title={
                result.emailSent
                  ? `Đã gửi email mời tới ${result.user.email}.`
                  : `Máy chủ chưa gửi được email – hãy chuyển link dưới đây cho ${result.user.email}.`
              }
            />
            <Input.TextArea value={result.inviteUrl} readOnly autoSize data-testid="portal-invite-url" />
            <div style={{ marginTop: 4 }}>Link dùng một lần, hiệu lực đến {formatDateTime(result.expiresAt)}.</div>
          </>
        )}
      </Modal>
    </div>
  )
}
