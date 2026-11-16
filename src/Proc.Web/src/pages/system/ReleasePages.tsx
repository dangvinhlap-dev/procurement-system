import {
  CheckCircleOutlined,
  CheckOutlined,
  CloseCircleOutlined,
  CloudDownloadOutlined,
  CloudUploadOutlined,
  EditOutlined,
  PlusOutlined,
  ReloadOutlined,
  RollbackOutlined,
  SaveOutlined,
  StopOutlined,
} from '@ant-design/icons'
import { Alert, App, Button, Form, Input, Modal, Space, Upload } from 'antd'
import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import {
  defectStatusLabels,
  deployConfirmation,
  rollbackLevels,
  ROLLBACK_CONFIRMATION,
  testResultLabels,
  useRelease,
  useReleaseMutations,
  useReleaseOverview,
  useReleases,
  type ReleaseDefect,
  type ReleaseDefectStatus,
  type ReleaseInstall,
  type ReleaseListItem,
  type ReleaseOverview,
  type ReleaseTestItem,
  type ReleaseTicket,
  type RollbackLevel,
} from '../../api/release'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { Select } from '../../classic/Select'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { DataGrid } from '../../components/DataGrid/DataGrid'
import type { GridColumn } from '../../components/DataGrid/types'
import { DocumentForm } from '../../components/DocumentForm/DocumentForm'
import { ListPage } from '../../components/ListPage/ListPage'
import { formatDateTime } from '../../utils/format'

/** Đường dẫn màn hình REL. */
export const RELEASE_PATH = '/he-thong/phat-hanh'

const mb = (bytes: number) => `${(bytes / 1024 / 1024).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} MB`

/** Chuẩn hóa chữ xác nhận như máy chủ: bỏ khoảng trắng hai đầu, NFC, không phân biệt hoa thường. */
const sameConfirmation = (typed: string | undefined, expected: string) => typed?.trim().normalize().toUpperCase() === expected.normalize()

/** Dòng tóm tắt môi trường trên đầu REL: máy này + bộ thực thi đã khai ở AGT. */
function environmentNotice(o: ReleaseOverview) {
  const envs = o.environments.length === 0
    ? 'chưa khai bộ thực thi nào (AGT)'
    : o.environments.map((e) => `${e.code} ${e.appVersion ?? '?'} (${e.agentOnline ? 'đang liên lạc' : 'mất liên lạc'})`).join(' · ')
  return `Máy này: ${o.thisEnvironment} – phiên bản ${o.thisVersion} · Bộ thực thi: ${envs}${o.singleEnvironment ? ' · Chế độ một môi trường (chưa khai TEST)' : ''}`
}

/**
 * Hộp thoại nhận gói: tải lên file zip (kèm SHA256 từ file .sha256 nếu có) hoặc lấy từ GitHub Release theo phiên bản.
 * @param mode upload / github; null = đóng.
 * @param maxMb Dung lượng gói tối đa.
 * @param onClose Đóng.
 * @param onDone Nhận xong – mở phiếu.
 */
function ReceiveModal({ mode, maxMb, onClose, onDone }: {
  mode: 'upload' | 'github' | null
  maxMb: number
  onClose: () => void
  onDone: (t: ReleaseTicket) => void
}) {
  const [form] = Form.useForm<{ version: string; sha256: string; note: string }>()
  const [file, setFile] = useState<File | null>(null)
  const [error, setError] = useState<string | null>(null)
  const m = useReleaseMutations()
  const close = () => {
    setFile(null)
    setError(null)
    onClose()
  }
  const pending = m.upload.isPending || m.github.isPending
  return (
    <Modal
      title={mode === 'github' ? 'Nhận gói từ GitHub Release' : 'Nhận gói tải lên'}
      open={mode != null}
      onCancel={close}
      onOk={() => form.submit()}
      okText="Nhận gói"
      okButtonProps={{ icon: <CloudDownloadOutlined /> }}
      cancelText="Bỏ qua"
      confirmLoading={pending}
      destroyOnHidden
      width={560}
    >
      <Alert type="info" showIcon style={{ marginBottom: 8 }}
        title={mode === 'github'
          ? 'Lấy procurement-x.y.z.zip và file .sha256 của tag vx.y.z; SHA256 phải khớp.'
          : `File procurement-x.y.z.zip do publish.ps1 tạo (tối đa ${maxMb} MB). Dán SHA256 trong file .sha256 để kiểm gói không hỏng.`} />
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        onFinish={async (v) => {
          setError(null)
          try {
            const t = mode === 'github'
              ? await m.github.mutateAsync({ version: v.version, note: v.note || null })
              : await m.upload.mutateAsync({ file: file!, sha256: v.sha256 || null, note: v.note || null })
            close()
            onDone(t)
          } catch (e) {
            setError(e instanceof Error ? e.message : 'Không nhận được gói.')
          }
        }}
      >
        {mode === 'github' ? (
          <Form.Item label="Phiên bản" name="version" rules={[{ required: true, message: 'Nhập phiên bản, ví dụ 0.36.0.' }]}>
            <Input autoFocus placeholder="0.36.0" />
          </Form.Item>
        ) : (
          <>
            <Form.Item label="File gói" required validateStatus={file ? undefined : 'error'} help={file ? `${file.name} – ${mb(file.size)}` : 'Chọn file .zip'}>
              <Upload accept=".zip" maxCount={1} showUploadList={false} beforeUpload={(f) => { setFile(f); return false }}>
                <Button icon={<CloudUploadOutlined />}>Chọn file…</Button>
              </Upload>
            </Form.Item>
            <Form.Item label="SHA256 (không bắt buộc)" name="sha256" rules={[{ pattern: /^\s*[0-9a-fA-F]{64}\s*$/, message: 'SHA256 gồm 64 ký tự 0–9, a–f.' }]}>
              <Input placeholder="Dán từ file procurement-x.y.z.zip.sha256" />
            </Form.Item>
          </>
        )}
        <Form.Item label="Ghi chú" name="note">
          <Input.TextArea maxLength={4000} autoSize={{ minRows: 2, maxRows: 4 }} />
        </Form.Item>
      </Form>
    </Modal>
  )
}

/**
 * Màn hình REL – Trung tâm phát hành (design.md mục 21.4, 21.6 – lát QT8; ui-style.md mục 5q): ListPage + DataGrid các phiếu phát hành, thông báo
 * đầu trang là môi trường máy này và bộ thực thi; nút "Nhận gói…" (tải lên / GitHub). Bấm đúp mở phiếu. Máy TEST chỉ xem.
 */
export function ReleaseListPage() {
  const navigate = useNavigate()
  const overview = useReleaseOverview()
  const list = useReleases()
  const { hasPermission } = useAuth()
  const [receiving, setReceiving] = useState<'upload' | 'github' | null>(null)
  const o = overview.data
  const canReceive = hasPermission(Permission.ReleaseManage) && !!o?.canWrite
  const columns: GridColumn<ReleaseListItem>[] = [
    { key: 'version', title: 'Phiên bản', width: 100, value: (r) => r.version },
    { key: 'status', title: 'Trạng thái', width: 120, type: 'status', value: (r) => r.statusLabel, tone: (r) => r.tone, groupable: true },
    { key: 'tests', title: 'Kiểm thử', width: 90, value: (r) => `${r.testedItems}/${r.totalItems}` },
    { key: 'defects', title: 'Lỗi chưa xử lý', width: 110, type: 'quantity', value: (r) => r.openDefects },
    { key: 'source', title: 'Nguồn', width: 120, value: (r) => r.source, groupable: true },
    { key: 'size', title: 'Dung lượng', width: 100, value: (r) => mb(r.packageSize) },
    { key: 'receivedAt', title: 'Nhận lúc', width: 140, type: 'datetime', value: (r) => r.receivedAt },
    { key: 'receivedBy', title: 'Người nhận', width: 120, value: (r) => r.receivedByName },
    { key: 'prod', title: 'Cài PROD lúc', width: 140, type: 'datetime', value: (r) => r.prodInstalledAt },
  ]
  return (
    <>
      <ListPage<ReleaseListItem>
        title="Trung tâm phát hành"
        exportFileName="phieu-phat-hanh"
        rows={list.data ?? []}
        columns={columns}
        rowKey={(r) => r.id}
        loading={list.isLoading}
        canCreate={false}
        onOpenRow={(r) => navigate(`${RELEASE_PATH}/${r.id}`)}
        actions={
          <>
            {canReceive && (
              <>
                <Button icon={<CloudUploadOutlined />} onClick={() => setReceiving('upload')} title="Tải lên file procurement-x.y.z.zip">
                  Nhận gói tải lên…
                </Button>
                {o?.gitHubEnabled && (
                  <Button icon={<CloudDownloadOutlined />} onClick={() => setReceiving('github')} title="Lấy gói từ GitHub Release theo phiên bản">
                    Nhận từ GitHub…
                  </Button>
                )}
              </>
            )}
            <Button icon={<ReloadOutlined />} loading={list.isFetching} onClick={() => { void list.refetch(); void overview.refetch() }} title="Lấy lại danh sách">
              Làm mới
            </Button>
          </>
        }
        notice={
          overview.error ? <Alert type="error" showIcon title={overview.error.message} />
            : o ? (
              <Alert type={o.canWrite ? 'info' : 'warning'} showIcon data-testid="release-notice" title={environmentNotice(o)}
                description={o.readOnlyReason ?? undefined} />
            ) : undefined
        }
      />
      <ReceiveModal mode={receiving} maxMb={o?.maxPackageSizeMb ?? 200} onClose={() => setReceiving(null)}
        onDone={(t) => navigate(`${RELEASE_PATH}/${t.id}`)} />
    </>
  )
}

/** Ô nhập lý do + mật khẩu + chữ xác nhận (việc nguy hiểm Q6 – ui-style.md mục 5m). */
function DangerFields({ confirmation }: { confirmation: string }) {
  return (
    <>
      <Form.Item label="Lý do" name="reason" rules={[{ required: true, message: 'Nhập lý do.' }, { min: 5, message: 'Lý do ít nhất 5 ký tự.' }]}>
        <Input.TextArea autoFocus maxLength={500} autoSize={{ minRows: 2, maxRows: 4 }} />
      </Form.Item>
      <Form.Item label="Mật khẩu của bạn" name="password" rules={[{ required: true, message: 'Nhập lại mật khẩu để xác nhận.' }]}>
        <Input.Password autoComplete="current-password" />
      </Form.Item>
      <Form.Item label={`Gõ "${confirmation}" để xác nhận`} name="confirmation"
        rules={[{ validator: (_, v?: string) => (sameConfirmation(v, confirmation) ? Promise.resolve() : Promise.reject(new Error(`Gõ đúng "${confirmation}".`))) }]}>
        <Input autoComplete="off" />
      </Form.Item>
    </>
  )
}

/** Kiểu hộp thoại đang mở trên form phiếu. */
type Dialog =
  | { kind: 'deploy'; env: 'Test' | 'Prod' }
  | { kind: 'rollback'; env: 'Test' | 'Prod' }
  | { kind: 'signOff' }
  | { kind: 'cancel' }
  | { kind: 'fail'; item: ReleaseTestItem }
  | { kind: 'defect' }
  | { kind: 'resolve'; defect: ReleaseDefect }
  | { kind: 'note' }

/**
 * Hộp thoại các bước của phiếu: cài (Q6 "CÀI x.y.z"), quay về (Q6 "QUAY VỀ", chọn mức không thấp hơn mức tối thiểu), xác nhận UAT, hủy, mục Không
 * đạt, thêm / xử lý lỗi, thêm ghi chú. Lỗi máy chủ hiện trong hộp thoại, không đóng.
 */
function StepDialog({ ticket, dialog, onClose }: { ticket: ReleaseTicket; dialog: Dialog | null; onClose: () => void }) {
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const [error, setError] = useState<string | null>(null)
  const m = useReleaseMutations()
  const close = () => {
    setError(null)
    onClose()
  }
  if (!dialog) return null
  const id = ticket.id
  const minNo = rollbackLevels[ticket.minRollbackLevel].no
  const spec: { title: string; ok: string; danger?: boolean; icon: React.ReactNode; body: React.ReactNode; run: (v: Record<string, string>) => Promise<unknown>; done: string } = (() => {
    switch (dialog.kind) {
      case 'deploy':
        return {
          title: `Cài bản ${ticket.version} lên ${dialog.env === 'Prod' ? 'PROD' : 'TEST'}`,
          ok: dialog.env === 'Prod' ? 'Cài PROD' : 'Cài TEST',
          danger: dialog.env === 'Prod',
          icon: <CloudUploadOutlined />,
          body: (
            <>
              <Alert type="warning" showIcon style={{ marginBottom: 8 }}
                title={`Bộ thực thi ${dialog.env === 'Prod' ? 'PROD' : 'TEST'} sẽ: kiểm SHA256, sao lưu CSDL, tắt site vài phút, cài bản ${ticket.version}, chạy script CSDL, kiểm tra /health và kiểm thử nhanh; lỗi thì tự trả về bản cũ.`}
                description={dialog.env === 'Prod' ? 'Báo người dùng trước giờ cài – người đang nhập phiếu sẽ không lưu được trong lúc site tắt.' : undefined} />
              <DangerFields confirmation={deployConfirmation(ticket.version)} />
            </>
          ),
          run: (v) => m.deploy.mutateAsync({ id, environment: dialog.env, reason: v.reason, password: v.password, confirmation: v.confirmation }),
          done: 'Đã giao lệnh cài cho bộ thực thi – kết quả hiện ở tab Lịch sử cài.',
        }
      case 'rollback':
        return {
          title: `Quay về bản trước ${ticket.version} trên ${dialog.env === 'Prod' ? 'PROD' : 'TEST'}`,
          ok: 'Quay về',
          danger: true,
          icon: <RollbackOutlined />,
          body: (
            <>
              <Alert type="warning" showIcon style={{ marginBottom: 8 }}
                title={`Gói khai mức quay về tối thiểu ${minNo}. Bộ thực thi ${dialog.env === 'Prod' ? 'PROD' : 'TEST'} tắt site, quay về theo mức chọn, kiểm tra /health.`}
                description={dialog.env === 'Test'
                  ? 'Quay về xong phiếu trở lại Đã nhận, bỏ xác nhận UAT – sửa xong thì cài TEST và UAT lại (hoặc hủy phiếu, nhận bản vá).'
                  : undefined} />
              <Form.Item label="Mức quay về" name="level" rules={[{ required: true, message: 'Chọn mức.' }]}>
                <Select showSearch={false} options={(Object.keys(rollbackLevels) as RollbackLevel[])
                  .filter((k) => rollbackLevels[k].no >= minNo)
                  .map((k) => ({ value: k, label: `${rollbackLevels[k].label} – ${rollbackLevels[k].note}` }))} />
              </Form.Item>
              <DangerFields confirmation={ROLLBACK_CONFIRMATION} />
            </>
          ),
          run: (v) => m.rollback.mutateAsync({ id, environment: dialog.env, level: v.level as RollbackLevel, reason: v.reason, password: v.password, confirmation: v.confirmation }),
          done: `Đã giao lệnh quay về cho bộ thực thi ${dialog.env === 'Prod' ? 'PROD' : 'TEST'}.`,
        }
      case 'signOff':
        return {
          title: `Xác nhận UAT đạt – bản ${ticket.version}`,
          ok: 'Xác nhận UAT đạt',
          icon: <CheckCircleOutlined />,
          body: (
            <>
              {ticket.openIssues.length > 0 && (
                <Alert type="warning" showIcon style={{ marginBottom: 8 }} title={`Còn ${ticket.openIssues.join(', ')} – ghi lý do vẫn xác nhận.`} />
              )}
              <Form.Item label="Ý kiến" name="comment" rules={ticket.openIssues.length > 0 ? [{ required: true, min: 5, message: 'Ghi lý do (ít nhất 5 ký tự).' }] : []}>
                <Input.TextArea autoFocus maxLength={4000} autoSize={{ minRows: 2, maxRows: 4 }} />
              </Form.Item>
            </>
          ),
          run: (v) => m.signOff.mutateAsync({ id, comment: v.comment || null }),
          done: 'Đã xác nhận UAT đạt – cài PROD được.',
        }
      case 'cancel':
        return {
          title: `Hủy phiếu phát hành ${ticket.version}`,
          ok: 'Hủy phiếu',
          danger: true,
          icon: <CloseCircleOutlined />,
          body: (
            <Form.Item label="Lý do" name="reason" rules={[{ required: true, min: 5, message: 'Nhập lý do (ít nhất 5 ký tự).' }]}>
              <Input.TextArea autoFocus maxLength={4000} autoSize={{ minRows: 2, maxRows: 4 }} />
            </Form.Item>
          ),
          run: (v) => m.cancel.mutateAsync({ id, reason: v.reason }),
          done: 'Đã hủy phiếu.',
        }
      case 'fail':
        return {
          title: `Không đạt – mục ${dialog.item.seq}`,
          ok: 'Ghi Không đạt',
          icon: <SaveOutlined />,
          body: (
            <>
              <p>{dialog.item.content}</p>
              <Form.Item label="Ý kiến (lỗi gì, ở màn hình nào)" name="comment" rules={[{ required: true, message: 'Ghi ý kiến.' }]}>
                <Input.TextArea autoFocus maxLength={4000} autoSize={{ minRows: 2, maxRows: 4 }} />
              </Form.Item>
            </>
          ),
          run: (v) => m.test.mutateAsync({ id, itemId: dialog.item.id, result: 'Failed', comment: v.comment }),
          done: 'Đã ghi Không đạt.',
        }
      case 'defect':
        return {
          title: 'Thêm lỗi phát hiện',
          ok: 'Thêm lỗi',
          icon: <PlusOutlined />,
          body: (
            <>
              <Form.Item label="Tiêu đề" name="title" rules={[{ required: true, message: 'Nhập tiêu đề.' }]}>
                <Input autoFocus maxLength={300} />
              </Form.Item>
              <Form.Item label="Màn hình" name="screen"><Input maxLength={200} placeholder="Ví dụ PO, REL" /></Form.Item>
              <Form.Item label="Mô tả" name="description"><Input.TextArea maxLength={4000} autoSize={{ minRows: 2, maxRows: 5 }} /></Form.Item>
            </>
          ),
          run: (v) => m.addDefect.mutateAsync({ id, title: v.title, description: v.description || null, screen: v.screen || null }),
          done: 'Đã thêm lỗi.',
        }
      case 'resolve':
        return {
          title: `Xử lý lỗi – ${dialog.defect.title}`,
          ok: 'Lưu',
          icon: <SaveOutlined />,
          body: (
            <>
              <Form.Item label="Trạng thái" name="status" initialValue={dialog.defect.status} rules={[{ required: true }]}>
                <Select showSearch={false} options={(Object.keys(defectStatusLabels) as ReleaseDefectStatus[]).map((k) => ({ value: k, label: defectStatusLabels[k].label }))} />
              </Form.Item>
              <Form.Item label="Cách xử lý" name="resolution" initialValue={dialog.defect.resolution ?? ''}>
                <Input.TextArea maxLength={4000} autoSize={{ minRows: 2, maxRows: 4 }} />
              </Form.Item>
            </>
          ),
          run: (v) => m.resolveDefect.mutateAsync({ id, defectId: dialog.defect.id, status: v.status as ReleaseDefectStatus, resolution: v.resolution || null }),
          done: 'Đã cập nhật lỗi.',
        }
      case 'note':
        return {
          title: 'Thêm ghi chú',
          ok: 'Thêm ghi chú',
          icon: <PlusOutlined />,
          body: (
            <Form.Item label="Nội dung" name="text" rules={[{ required: true, message: 'Nhập nội dung.' }]}>
              <Input.TextArea autoFocus maxLength={1000} autoSize={{ minRows: 2, maxRows: 5 }} />
            </Form.Item>
          ),
          run: (v) => m.note.mutateAsync({ id, text: v.text }),
          done: 'Đã thêm ghi chú.',
        }
    }
  })()
  const pending = Object.values(m).some((x) => x.isPending)
  return (
    <Modal title={spec.title} open onCancel={close} onOk={() => form.submit()} okText={spec.ok} cancelText="Bỏ qua" confirmLoading={pending}
      okButtonProps={{ danger: spec.danger, icon: spec.icon }} destroyOnHidden width={560}>
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form form={form} layout="vertical" preserve={false}
        onFinish={async (v: Record<string, string>) => {
          setError(null)
          try {
            await spec.run(v)
            message.success(spec.done)
            close()
          } catch (e) {
            setError(e instanceof Error ? e.message : 'Không thực hiện được.')
          }
        }}>
        {spec.body}
      </Form>
    </Modal>
  )
}

/**
 * Form phiếu phát hành (DocumentForm – design.md 21.6): đầu phiếu thông tin gói (SHA256, mức quay về, nguồn, UAT), ô Rủi ro / Cách quay lại / Ghi
 * chú sửa được tới khi khóa; tab Mục kiểm thử (Đạt / Không đạt / Chưa kiểm thử trên dòng đang chọn), Lỗi phát hiện, Lịch sử cài (tự làm mới khi còn
 * lần cài chờ), CHANGELOG, Đính kèm. Nút bước theo trạng thái và quyền: Cài TEST…, Xác nhận UAT…, Cài PROD…, Quay về…, Đóng phiếu, Hủy phiếu….
 */
export function ReleasePage() {
  const { id } = useParams()
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const query = useRelease(id)
  const overview = useReleaseOverview().data
  const m = useReleaseMutations()
  const { hasPermission } = useAuth()
  const [dialog, setDialog] = useState<Dialog | null>(null)
  const [item, setItem] = useState<ReleaseTestItem | null>(null)
  const [defect, setDefect] = useState<ReleaseDefect | null>(null)
  const [error, setError] = useState<string | null>(null)
  const t = query.data
  const writable = !!overview?.canWrite
  const canManage = hasPermission(Permission.ReleaseManage) && writable
  const canTest = (canManage || hasPermission(Permission.ReleaseUatSignOff)) && writable
  const editable = !!t && canManage && !t.isLocked
  const pendingInstall = t?.installs.some((i) => i.status === 'Pending') ?? false
  const attachmentTab = useAttachmentTab('ReleaseTicket', t?.id, editable)

  useEffect(() => {
    if (t) form.setFieldsValue({ risks: t.risks ?? '', rollbackPlan: t.rollbackPlan ?? '', note: t.note ?? '' })
  }, [t, form])

  if (!t) return <DocumentForm title="Phiếu phát hành" number={null} status={{ label: '…', tone: 'neutral' }} actions={null} form={form}
    loading={query.isLoading} error={query.error?.message} header={null} lines={null} readOnly />

  const save = async () => {
    if (!editable) return
    const v = form.getFieldsValue() as { risks: string; rollbackPlan: string; note: string }
    setError(null)
    try {
      await m.info.mutateAsync({ id: t.id, risks: v.risks || null, rollbackPlan: v.rollbackPlan || null, note: v.note || null })
      message.success('Đã lưu phiếu phát hành.')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được.')
    }
  }
  const record = async (result: 'Passed' | 'NotTested') => {
    if (!item) return
    setError(null)
    try {
      await m.test.mutateAsync({ id: t.id, itemId: item.id, result, comment: null })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không ghi được kết quả.')
    }
  }
  const close = () => Modal.confirm({
    title: `Đóng phiếu phát hành ${t.version}?`,
    content: 'Đóng khi đã theo dõi màn hình ERR sau khi cài PROD. Phiếu đóng vẫn quay về bản cũ được.',
    okText: 'Đóng phiếu',
    cancelText: 'Bỏ qua',
    onOk: async () => {
      try {
        await m.close.mutateAsync(t.id)
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Không đóng được phiếu.')
      }
    },
  })

  const testing = t.status === 'Received' || t.status === 'UatInProgress'
  const hasTest = !overview?.singleEnvironment
  const actions = writable && (
    <Space wrap size={4}>
      {editable && <Button icon={<SaveOutlined />} onClick={() => void save()}>Lưu</Button>}
      {testing && hasTest && hasPermission(Permission.ReleaseDeployTest) && (
        <Button icon={<CloudUploadOutlined />} disabled={pendingInstall} onClick={() => setDialog({ kind: 'deploy', env: 'Test' })}>Cài TEST…</Button>
      )}
      {testing && hasPermission(Permission.ReleaseUatSignOff) && (t.status === 'UatInProgress' || !hasTest) && (
        <Button icon={<CheckCircleOutlined />} disabled={pendingInstall} onClick={() => setDialog({ kind: 'signOff' })}>Xác nhận UAT đạt…</Button>
      )}
      {t.status === 'UatPassed' && hasPermission(Permission.ReleaseDeployProd) && (
        <Button icon={<CloudUploadOutlined />} disabled={pendingInstall} onClick={() => setDialog({ kind: 'deploy', env: 'Prod' })}>Cài PROD…</Button>
      )}
      {(t.status === 'UatInProgress' || t.status === 'UatPassed') && t.installedOnTest && hasPermission(Permission.ReleaseDeployTest) && (
        <Button icon={<RollbackOutlined />} disabled={pendingInstall} onClick={() => setDialog({ kind: 'rollback', env: 'Test' })}>Quay về bản cũ trên TEST…</Button>
      )}
      {(t.status === 'ProdInstalled' || t.status === 'Closed') && hasPermission(Permission.ReleaseDeployProd) && (
        <Button icon={<RollbackOutlined />} disabled={pendingInstall} onClick={() => setDialog({ kind: 'rollback', env: 'Prod' })}>Quay về bản cũ…</Button>
      )}
      {t.status === 'ProdInstalled' && canManage && <Button icon={<CheckOutlined />} disabled={pendingInstall} onClick={close}>Đóng phiếu</Button>}
      {canManage && <Button icon={<EditOutlined />} onClick={() => setDialog({ kind: 'note' })}>Thêm ghi chú…</Button>}
      {(testing || t.status === 'UatPassed') && canManage && (
        <Button icon={<CloseCircleOutlined />} disabled={pendingInstall} onClick={() => setDialog({ kind: 'cancel' })}>Hủy phiếu…</Button>
      )}
    </Space>
  )

  const itemColumns: GridColumn<ReleaseTestItem>[] = [
    { key: 'seq', title: 'STT', width: 50, type: 'quantity', value: (r) => r.seq },
    { key: 'section', title: 'Nhóm', width: 100, value: (r) => r.section, groupable: true },
    { key: 'content', title: 'Nội dung kiểm thử', width: 420, value: (r) => r.content },
    { key: 'result', title: 'Kết quả', width: 120, type: 'status', value: (r) => testResultLabels[r.result].label, tone: (r) => testResultLabels[r.result].tone, groupable: true },
    { key: 'tester', title: 'Người kiểm thử', width: 120, value: (r) => r.testerName },
    { key: 'testedAt', title: 'Lúc', width: 130, type: 'datetime', value: (r) => r.testedAt },
    { key: 'comment', title: 'Ý kiến', width: 260, value: (r) => r.comment },
  ]
  const defectColumns: GridColumn<ReleaseDefect>[] = [
    { key: 'title', title: 'Tiêu đề', width: 260, value: (r) => r.title },
    { key: 'status', title: 'Trạng thái', width: 110, type: 'status', value: (r) => defectStatusLabels[r.status].label, tone: (r) => defectStatusLabels[r.status].tone },
    { key: 'screen', title: 'Màn hình', width: 90, value: (r) => r.screen },
    { key: 'description', title: 'Mô tả', width: 280, value: (r) => r.description },
    { key: 'reporter', title: 'Người báo', width: 110, value: (r) => r.reporterName },
    { key: 'reportedAt', title: 'Lúc báo', width: 130, type: 'datetime', value: (r) => r.reportedAt },
    { key: 'resolution', title: 'Cách xử lý', width: 220, value: (r) => r.resolution },
  ]
  const installStatus = (i: ReleaseInstall) => i.status === 'Pending'
    ? { label: 'Đang chờ bộ thực thi', tone: 'neutral' as const }
    : i.status === 'Succeeded' ? { label: 'Xong', tone: 'success' as const } : { label: 'Lỗi', tone: 'error' as const }
  const installColumns: GridColumn<ReleaseInstall>[] = [
    { key: 'startedAt', title: 'Lúc giao', width: 130, type: 'datetime', value: (r) => r.startedAt },
    { key: 'kind', title: 'Việc', width: 110, value: (r) => (r.kind === 'Rollback' ? `Quay về mức ${r.level ? rollbackLevels[r.level].no : '?'}` : 'Cài') },
    { key: 'env', title: 'Môi trường', width: 90, value: (r) => r.environment.toUpperCase() },
    { key: 'status', title: 'Kết quả', width: 150, type: 'status', value: (r) => installStatus(r).label, tone: (r) => installStatus(r).tone },
    { key: 'versions', title: 'Bản', width: 130, value: (r) => (r.kind === 'Rollback' ? `${t.version} → ${r.targetVersion ?? '?'}` : `${r.previousVersion ?? '?'} → ${t.version}`) },
    { key: 'message', title: 'Thông báo', width: 320, value: (r) => r.message },
    { key: 'by', title: 'Người giao', width: 110, value: (r) => r.byName },
    { key: 'reason', title: 'Lý do', width: 200, value: (r) => r.reason },
  ]

  return (
    <>
      <DocumentForm
        title="Phiếu phát hành"
        number={t.version}
        status={{ label: t.statusLabel, tone: t.tone }}
        meta={`Nhận ${formatDateTime(t.receivedAt)} – ${t.receivedByName ?? ''} · ${t.source}`}
        audit={{ entityType: 'ReleaseTicket', id: t.id }}
        loading={query.isLoading}
        error={error}
        notice={
          <>
            {!writable && overview?.readOnlyReason && <Alert type="warning" showIcon title={overview.readOnlyReason} style={{ marginBottom: 4 }} />}
            {pendingInstall && <Alert type="info" showIcon data-testid="release-pending" title="Đang chờ bộ thực thi báo kết quả – trang tự làm mới 5 giây." style={{ marginBottom: 4 }} />}
            {t.isLocked && <Alert type="info" showIcon title="Phiếu đã khóa – chỉ thêm ghi chú, lỗi phát hiện." />}
          </>
        }
        form={form}
        readOnly={!editable}
        onSave={() => void save()}
        actions={actions}
        linesLabel="Mục kiểm thử"
        header={
          <>
            <Form.Item label="Gói"><Input readOnly value={`${t.packageFileName} – ${mb(t.packageSize)}`} /></Form.Item>
            <Form.Item label="Mức quay về tối thiểu"><Input readOnly value={rollbackLevels[t.minRollbackLevel].label} /></Form.Item>
            <Form.Item label="UAT">
              <Input readOnly value={t.uatSignedAt ? `${formatDateTime(t.uatSignedAt)} – ${t.uatSignedByName ?? ''}${t.uatWithoutTest ? ' (không qua TEST)' : ''}` : 'Chưa xác nhận'} />
            </Form.Item>
            <Form.Item label="SHA256" className="document-form-wide"><Input readOnly value={t.sha256} className="erp-mono" /></Form.Item>
            <Form.Item label="Thay đổi CSDL" className="document-form-wide"><Input.TextArea readOnly value={t.databaseChanges ?? ''} autoSize={{ minRows: 1, maxRows: 4 }} /></Form.Item>
            <Form.Item label="Rủi ro" name="risks" className="document-form-wide"><Input.TextArea maxLength={4000} autoSize={{ minRows: 1, maxRows: 4 }} /></Form.Item>
            <Form.Item label="Cách quay lại" name="rollbackPlan" className="document-form-wide"><Input.TextArea maxLength={4000} autoSize={{ minRows: 1, maxRows: 4 }} /></Form.Item>
            <Form.Item label="Ghi chú" name="note" className="document-form-wide"><Input.TextArea maxLength={4000} autoSize={{ minRows: 1, maxRows: 6 }} /></Form.Item>
            {t.endReason && <Form.Item label="Lý do hủy / quay về" className="document-form-wide"><Input readOnly value={t.endReason} /></Form.Item>}
          </>
        }
        lines={
          <>
            <div className="erp-rel-grid"><DataGrid<ReleaseTestItem> rows={t.items} columns={itemColumns} rowKey={(r) => r.id} groupBy={null} onGroupByChange={() => {}}
              onSelectRow={setItem} auditEntity="ReleaseTestItem" /></div>
            {canTest && testing && (
              <div className="erp-rel-grid-buttons">
                <Button size="small" icon={<CheckOutlined />} disabled={!item} onClick={() => void record('Passed')}>Đạt</Button>
                <Button size="small" icon={<StopOutlined />} disabled={!item} onClick={() => item && setDialog({ kind: 'fail', item })}>Không đạt…</Button>
                <Button size="small" icon={<ReloadOutlined />} disabled={!item} onClick={() => void record('NotTested')}>Chưa kiểm thử</Button>
                {!item && <span className="erp-rel-hint">Chọn một mục trong lưới.</span>}
              </div>
            )}
          </>
        }
        extraTabs={[
          {
            key: 'defects',
            label: `Lỗi phát hiện (${t.defects.length})`,
            children: (
              <>
                <div className="erp-rel-grid"><DataGrid<ReleaseDefect> rows={t.defects} columns={defectColumns} rowKey={(r) => r.id} groupBy={null} onGroupByChange={() => {}}
                  onSelectRow={setDefect} auditEntity="ReleaseDefect" /></div>
                {canTest && t.status !== 'Cancelled' && (
                  <div className="erp-rel-grid-buttons">
                    <Button size="small" icon={<PlusOutlined />} onClick={() => setDialog({ kind: 'defect' })}>Thêm lỗi…</Button>
                    {canManage && <Button size="small" icon={<EditOutlined />} disabled={!defect} onClick={() => defect && setDialog({ kind: 'resolve', defect })}>Xử lý lỗi…</Button>}
                  </div>
                )}
              </>
            ),
          },
          {
            key: 'installs',
            label: `Lịch sử cài (${t.installs.length})`,
            children: <div className="erp-rel-grid"><DataGrid<ReleaseInstall> rows={t.installs} columns={installColumns} rowKey={(r) => r.id} groupBy={null} onGroupByChange={() => {}} /></div>,
          },
          { key: 'changelog', label: 'CHANGELOG', children: <pre className="erp-error-detail" data-testid="release-changelog">{t.changelog ?? 'Gói không có CHANGELOG.md.'}</pre> },
          attachmentTab,
        ]}
      />
      <StepDialog key={dialog ? JSON.stringify(dialog) : 'none'} ticket={t} dialog={dialog} onClose={() => setDialog(null)} />
    </>
  )
}
