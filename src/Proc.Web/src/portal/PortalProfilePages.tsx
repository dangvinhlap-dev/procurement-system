import { CloseOutlined, DeleteOutlined, EditOutlined, PaperClipOutlined, SendOutlined } from '@ant-design/icons'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, App, Button, Checkbox, Form, Input, Spin, Upload } from 'antd'
import { useMemo, useState } from 'react'
import { supplierStatusLabels } from '../api/masterData'
import { DataGrid } from '../components/DataGrid/DataGrid'
import type { GridColumn } from '../components/DataGrid/types'
import { LineGrid, type LineColumn } from '../components/DocumentForm/LineGrid'
import {
  supplierProfileBankColumns,
  supplierProfileContactColumns,
  supplierProfileRequestTone,
} from '../components/SupplierProfile/columns'
import { SupplierProfileCompare } from '../components/SupplierProfile/SupplierProfileCompare'
import {
  portalApi,
  portalFiles,
  supplierProfileRequestStatusLabels,
  type PortalProfile,
  type SupplierProfileBank,
  type SupplierProfileContact,
  type SupplierProfileRequest,
} from './portalApi'
import { PortalFiles, Section } from './PortalParts'

const requestColumns: GridColumn<SupplierProfileRequest>[] = [
  { key: 'no', title: 'Số đề nghị', width: 120, value: (r) => r.no, auditField: false },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 150,
    type: 'status',
    value: (r) => supplierProfileRequestStatusLabels[r.status],
    tone: (r) => supplierProfileRequestTone[r.status],
    auditField: false,
  },
  { key: 'summary', title: 'Nội dung đề nghị đổi', width: 260, value: (r) => r.summary, auditField: false },
  { key: 'submittedAt', title: 'Thời điểm gửi', width: 130, type: 'datetime', value: (r) => r.submittedAt, auditField: false },
  { key: 'submittedByEmail', title: 'Người gửi', width: 170, value: (r) => r.submittedByEmail, auditField: false },
  { key: 'closedAt', title: 'Thời điểm xử lý', width: 130, type: 'datetime', value: (r) => r.closedAt, auditField: false },
  { key: 'reviewComment', title: 'Ý kiến / lý do', width: 240, value: (r) => r.reviewComment, auditField: false },
]

/** Giá trị form đề nghị (thông tin chung + hai lưới + ghi chú). */
interface ProfileFormValues {
  name: string
  shortName?: string | null
  address?: string | null
  phone?: string | null
  email?: string | null
  website?: string | null
  supplyScope?: string | null
  contacts: Partial<SupplierProfileContact>[]
  bankAccounts: Partial<SupplierProfileBank>[]
  note?: string | null
}

const trimOrNull = (v: string | null | undefined) => (v?.trim() ? v.trim() : null)

/** Ô chữ trong lưới dòng (không viền như Excel). */
function cellText(listName: string, key: string, label: string, maxLength: number): LineColumn['render'] {
  return (field) => (
    <Form.Item name={[field.name, key]} noStyle>
      <Input variant="borderless" maxLength={maxLength} aria-label={`${label} dòng ${field.name + 1}`} data-list={listName} />
    </Form.Item>
  )
}

/** Ô đánh dấu trong lưới dòng. */
function cellCheck(key: string, label: string): LineColumn['render'] {
  return (field) => (
    <Form.Item name={[field.name, key]} valuePropName="checked" noStyle>
      <Checkbox aria-label={`${label} dòng ${field.name + 1}`} />
    </Form.Item>
  )
}

const contactEditColumns: LineColumn[] = [
  { key: 'name', title: 'Họ tên *', width: 170, render: cellText('contacts', 'name', 'Họ tên', 200) },
  { key: 'position', title: 'Chức vụ', width: 120, render: cellText('contacts', 'position', 'Chức vụ', 100) },
  { key: 'phone', title: 'Điện thoại', width: 110, render: cellText('contacts', 'phone', 'Điện thoại', 50) },
  { key: 'email', title: 'Email', width: 180, render: cellText('contacts', 'email', 'Email', 256) },
  { key: 'isPrimary', title: 'Liên hệ chính', width: 90, align: 'center', render: cellCheck('isPrimary', 'Liên hệ chính') },
]

const bankEditColumns: LineColumn[] = [
  { key: 'bankName', title: 'Ngân hàng *', width: 160, render: cellText('banks', 'bankName', 'Ngân hàng', 200) },
  { key: 'branch', title: 'Chi nhánh', width: 130, render: cellText('banks', 'branch', 'Chi nhánh', 200) },
  { key: 'accountNumber', title: 'Số tài khoản *', width: 140, render: cellText('banks', 'accountNumber', 'Số tài khoản', 30) },
  { key: 'accountName', title: 'Chủ tài khoản', width: 180, render: cellText('banks', 'accountName', 'Chủ tài khoản', 200) },
  { key: 'isDefault', title: 'Mặc định', width: 70, align: 'center', render: cellCheck('isDefault', 'Mặc định') },
]

/**
 * Form đề nghị cập nhật hồ sơ (lát P2a – P2-2, P2-3): điền sẵn hồ sơ hiện tại, NCC sửa phần cần đổi; mã số thuế chỉ xem (Phòng CUVT sửa trong
 * phần mềm – người dùng chốt 14/11/2026). File kèm chọn trước, tải lên ngay sau khi gửi (CLAUDE.md mục 4).
 */
function ProfileRequestForm({ profile, onClose }: { profile: PortalProfile; onClose: () => void }) {
  const { message, modal } = App.useApp()
  const queryClient = useQueryClient()
  const [form] = Form.useForm<ProfileFormValues>()
  const [pending, setPending] = useState<File[]>([])
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const initial = useMemo<ProfileFormValues>(
    () => ({
      name: profile.name,
      shortName: profile.shortName,
      address: profile.address,
      phone: profile.phone,
      email: profile.email,
      website: profile.website,
      supplyScope: profile.supplyScope,
      contacts: profile.contacts,
      bankAccounts: profile.bankAccounts,
      note: '',
    }),
    [profile],
  )

  const submit = () => {
    const v = form.getFieldsValue(true) as ProfileFormValues
    // Dòng lưới để trống hoàn toàn (dòng gõ dở) bỏ qua; dòng thiếu ô bắt buộc để máy chủ báo lỗi rõ ràng.
    const contacts = (v.contacts ?? [])
      .filter((c) => c.name?.trim() || c.phone?.trim() || c.email?.trim() || c.position?.trim())
      .map((c) => ({
        id: c.id ?? null,
        name: c.name?.trim() ?? '',
        position: trimOrNull(c.position),
        phone: trimOrNull(c.phone),
        email: trimOrNull(c.email),
        isPrimary: !!c.isPrimary,
      }))
    const banks = (v.bankAccounts ?? [])
      .filter((b) => b.bankName?.trim() || b.accountNumber?.trim())
      .map((b) => ({
        id: b.id ?? null,
        bankName: b.bankName?.trim() ?? '',
        branch: trimOrNull(b.branch),
        accountNumber: b.accountNumber?.trim() ?? '',
        accountName: trimOrNull(b.accountName),
        isDefault: !!b.isDefault,
      }))
    if (!v.name?.trim()) return setError('Nhập tên công ty.')
    setError(null)
    modal.confirm({
      title: 'Gửi đề nghị cập nhật hồ sơ',
      content:
        'Gửi đề nghị cho Phòng Cung ứng vật tư? Hồ sơ chỉ đổi khi Phòng Cung ứng vật tư chấp nhận; đổi tài khoản ngân hàng còn phải duyệt thêm. ' +
        'Gửi rồi không sửa được – cần sửa thì rút đề nghị và gửi lại.',
      okText: 'Gửi',
      cancelText: 'Không',
      onOk: async () => {
        setBusy(true)
        try {
          const sent = await portalApi.submitProfile({
            name: v.name.trim(),
            shortName: trimOrNull(v.shortName),
            address: trimOrNull(v.address),
            phone: trimOrNull(v.phone),
            email: trimOrNull(v.email),
            website: trimOrNull(v.website),
            supplyScope: trimOrNull(v.supplyScope),
            contacts,
            bankAccounts: banks,
            note: trimOrNull(v.note),
          })
          // Đề nghị đã gửi: tải file trong hàng chờ; file lỗi báo riêng, đề nghị vẫn giữ (NCC tải lại ở khối file của đề nghị).
          const files = portalFiles('profile-requests')
          for (const file of pending) {
            try {
              await files.upload(sent.id, file, null)
            } catch (e) {
              void message.error(`Không tải lên được "${file.name}": ${e instanceof Error ? e.message : String(e)}`)
            }
          }
          await queryClient.invalidateQueries({ queryKey: ['portal', 'profile'] })
          void message.success(`Đã gửi đề nghị ${sent.no}.`)
          onClose()
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không gửi được đề nghị.')
        } finally {
          setBusy(false)
        }
      },
    })
  }

  return (
    <Section title="Đề nghị cập nhật hồ sơ">
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form form={form} layout="vertical" requiredMark={false} initialValues={initial} data-testid="portal-profile-form">
        <div className="erp-portal-profile-head">
          <Form.Item name="name" label="Tên công ty *" className="is-wide">
            <Input maxLength={300} aria-label="Tên công ty" />
          </Form.Item>
          <Form.Item name="shortName" label="Tên viết tắt">
            <Input maxLength={100} aria-label="Tên viết tắt" />
          </Form.Item>
          <Form.Item label="Mã số thuế (chỉ Phòng CUVT sửa)">
            <Input value={profile.taxCode ?? ''} readOnly aria-label="Mã số thuế" />
          </Form.Item>
          <Form.Item name="address" label="Địa chỉ" className="is-wide">
            <Input maxLength={500} aria-label="Địa chỉ" />
          </Form.Item>
          <Form.Item name="phone" label="Điện thoại">
            <Input maxLength={50} aria-label="Điện thoại" />
          </Form.Item>
          <Form.Item name="email" label="Email">
            <Input maxLength={256} aria-label="Email công ty" />
          </Form.Item>
          <Form.Item name="website" label="Website">
            <Input maxLength={200} aria-label="Website" />
          </Form.Item>
          <Form.Item name="note" label="Ghi chú">
            <Input maxLength={1000} aria-label="Ghi chú đề nghị" />
          </Form.Item>
          <Form.Item name="supplyScope" label="Phạm vi cung cấp" className="is-wide">
            <Input maxLength={500} aria-label="Phạm vi cung cấp" />
          </Form.Item>
        </div>
        <p className="erp-portal-hint">
          <strong>Người liên hệ</strong>
        </p>
        <div data-testid="portal-profile-contacts">
          <LineGrid name="contacts" columns={contactEditColumns} newLine={() => ({ isPrimary: false })} />
        </div>
        <p className="erp-portal-hint">
          <strong>Tài khoản ngân hàng</strong> – đổi tài khoản phải được bên mua duyệt rồi mới dùng để thanh toán.
        </p>
        <div data-testid="portal-profile-banks">
          <LineGrid name="bankAccounts" columns={bankEditColumns} newLine={() => ({ isDefault: false })} />
        </div>
      </Form>
      <div className="erp-portal-file-bar">
        <Upload
          showUploadList={false}
          multiple
          beforeUpload={(file) => {
            if (file.size > profile.maxFileSizeMb * 1024 * 1024) {
              void message.error(`File "${file.name}" quá ${profile.maxFileSizeMb} MB. Hãy nén hoặc chia nhỏ file.`)
            } else {
              setPending((list) => [...list, file])
            }
            return false
          }}
        >
          <Button icon={<PaperClipOutlined />}>Chọn file kèm…</Button>
        </Upload>
        <span className="erp-portal-hint" data-testid="portal-profile-pending">
          {pending.length === 0
            ? `Giấy phép kinh doanh, xác nhận tài khoản ngân hàng… tối đa ${profile.maxFileSizeMb} MB/file – tải lên khi gửi đề nghị.`
            : ''}
        </span>
        {pending.map((file, index) => (
          <span key={`${file.name}-${index}`} className="erp-portal-pending-file">
            {file.name}
            <Button
              type="text"
              size="small"
              icon={<DeleteOutlined />}
              title={`Bỏ file ${file.name}`}
              aria-label={`Bỏ file ${file.name}`}
              onClick={() => setPending((list) => list.filter((_, i) => i !== index))}
            />
          </span>
        ))}
      </div>
      <div className="erp-portal-actions" style={{ marginTop: 8 }}>
        <Button icon={<CloseOutlined />} onClick={onClose} disabled={busy}>
          Bỏ qua
        </Button>
        <Button type="primary" icon={<SendOutlined />} loading={busy} onClick={submit}>
          Gửi đề nghị
        </Button>
      </div>
    </Section>
  )
}

/** Lưới chỉ đọc cao cố định của trang hồ sơ. */
function ReadGrid<T>({ rows, columns, rowKey, testId }: { rows: T[]; columns: GridColumn<T>[]; rowKey: (r: T) => string; testId: string }) {
  const [groupBy, setGroupBy] = useState<string | null>(null)
  return (
    <div style={{ height: 110, display: 'flex', flexDirection: 'column', marginBottom: 8 }} data-testid={testId}>
      <DataGrid rows={rows} columns={columns} rowKey={rowKey} groupBy={groupBy} onGroupByChange={setGroupBy} />
    </div>
  )
}

/**
 * Trang "Hồ sơ công ty" của Portal (lát P2a, design.md 19.5 P2-1…P2-6): hồ sơ hiện tại; quản trị NCC gửi đề nghị cập nhật; các đề nghị đã
 * gửi (chọn một đề nghị xem so sánh, file kèm, rút khi còn chờ).
 */
export function PortalProfilePage() {
  const { message, modal } = App.useApp()
  const queryClient = useQueryClient()
  const query = useQuery({ queryKey: ['portal', 'profile'], queryFn: portalApi.profile })
  const [editing, setEditing] = useState(false)
  const [groupBy, setGroupBy] = useState<string | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const p = query.data
  if (query.isLoading) return <Spin />
  if (!p) return query.error ? <Alert type="error" showIcon title={query.error.message} /> : null
  const selected = p.requests.find((r) => r.id === selectedId) ?? null

  const withdraw = (r: SupplierProfileRequest) => {
    let reason = ''
    modal.confirm({
      title: `Rút đề nghị ${r.no}`,
      content: (
        <Input.TextArea rows={3} maxLength={1000} placeholder="Lý do rút (không bắt buộc)" onChange={(e) => (reason = e.target.value)} />
      ),
      okText: 'Rút đề nghị',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        try {
          await portalApi.withdrawProfile(r.id, reason.trim() || null)
          await queryClient.invalidateQueries({ queryKey: ['portal', 'profile'] })
          void message.success(`Đã rút đề nghị ${r.no}.`)
        } catch (e) {
          void message.error(e instanceof Error ? e.message : 'Không rút được đề nghị.')
        }
      },
    })
  }

  const blocked = p.pendingRequestNo
    ? `Đề nghị ${p.pendingRequestNo} đang chờ Phòng Cung ứng vật tư xử lý – rút đề nghị đó nếu muốn gửi lại.`
    : 'Chỉ quản trị NCC gửi được đề nghị cập nhật hồ sơ.'
  return (
    <>
      <h1 className="erp-portal-title">Hồ sơ công ty</h1>
      <Section title="Hồ sơ hiện tại">
        <dl className="erp-portal-fields erp-portal-profile-fields" data-testid="portal-profile">
          <dt>Mã NCC</dt>
          <dd>{p.code}</dd>
          <dt>Tình trạng</dt>
          <dd>{supplierStatusLabels[p.status as keyof typeof supplierStatusLabels] ?? p.status}</dd>
          <dt>Tên công ty</dt>
          <dd>{p.name}</dd>
          <dt>Tên viết tắt</dt>
          <dd>{p.shortName ?? '–'}</dd>
          <dt>Mã số thuế</dt>
          <dd>{p.taxCode ?? '–'}</dd>
          <dt>Điện thoại</dt>
          <dd>{p.phone ?? '–'}</dd>
          <dt>Địa chỉ</dt>
          <dd>{p.address ?? '–'}</dd>
          <dt>Email</dt>
          <dd>{p.email ?? '–'}</dd>
          <dt>Website</dt>
          <dd>{p.website ?? '–'}</dd>
          <dt>Điều khoản thanh toán</dt>
          <dd>{p.paymentTerms ?? '–'}</dd>
          <dt>Phạm vi cung cấp</dt>
          <dd>{p.supplyScope ?? '–'}</dd>
        </dl>
        <p className="erp-portal-hint">
          <strong>Người liên hệ</strong>
        </p>
        <ReadGrid
          rows={p.contacts}
          columns={supplierProfileContactColumns}
          rowKey={(r) => r.id ?? r.name}
          testId="portal-profile-current-contacts"
        />
        <p className="erp-portal-hint">
          <strong>Tài khoản ngân hàng</strong>
        </p>
        <ReadGrid
          rows={p.bankAccounts}
          columns={supplierProfileBankColumns}
          rowKey={(r) => r.id ?? r.accountNumber}
          testId="portal-profile-current-banks"
        />
        {!editing && (
          <div className="erp-portal-actions">
            <span className="erp-portal-hint">
              {p.canSubmit
                ? 'Mã số thuế, điều khoản thanh toán do Phòng Cung ứng vật tư quản lý – cần đổi xin liên hệ trực tiếp.'
                : blocked}
            </span>
            <Button icon={<EditOutlined />} disabled={!p.canSubmit} onClick={() => setEditing(true)}>
              Đề nghị cập nhật hồ sơ…
            </Button>
          </div>
        )}
      </Section>
      {editing && <ProfileRequestForm profile={p} onClose={() => setEditing(false)} />}
      <Section title={`Các đề nghị đã gửi (${p.requests.length})`}>
        <div style={{ height: 160, display: 'flex', flexDirection: 'column' }} data-testid="portal-profile-requests">
          <DataGrid
            rows={p.requests}
            columns={requestColumns}
            rowKey={(r) => r.id}
            groupBy={groupBy}
            onGroupByChange={setGroupBy}
            loading={query.isFetching}
            onSelectRow={(r) => setSelectedId(r?.id ?? null)}
          />
        </div>
        <p className="erp-portal-hint">Chọn một đề nghị để xem nội dung, file kèm.</p>
      </Section>
      {selected && (
        <>
          <Section title={`Đề nghị ${selected.no} – ${supplierProfileRequestStatusLabels[selected.status]}`}>
            {selected.note && <p className="erp-portal-hint">Ghi chú: {selected.note}</p>}
            {selected.status === 'Rejected' && (
              <Alert type="error" showIcon title={`Lý do từ chối: ${selected.reviewComment}`} style={{ marginBottom: 8 }} />
            )}
            {selected.status === 'Accepted' && selected.bankChangeRequestNo && (
              <Alert
                type="info"
                showIcon
                title="Thay đổi tài khoản ngân hàng đang chờ bên mua duyệt – duyệt xong hồ sơ hiện tại mới đổi tài khoản."
                style={{ marginBottom: 8 }}
              />
            )}
            <SupplierProfileCompare request={selected} showCurrent={false} />
            {selected.canWithdraw && (
              <div className="erp-portal-actions">
                <Button icon={<CloseOutlined />} onClick={() => withdraw(selected)}>
                  Rút đề nghị…
                </Button>
              </div>
            )}
          </Section>
          <PortalFiles
            kind="profile-requests"
            id={selected.id}
            title={`File kèm đề nghị ${selected.no}`}
            canEdit={selected.canWithdraw}
            maxFileSizeMb={p.maxFileSizeMb}
            hint={
              selected.canWithdraw
                ? `Giấy phép, xác nhận tài khoản… tối đa ${p.maxFileSizeMb} MB/file. Thêm, xóa được đến khi Phòng Cung ứng vật tư xử lý.`
                : 'Đề nghị đã xử lý hoặc đã rút – file chỉ xem, tải xuống.'
            }
            testId="portal-profile-files"
          />
        </>
      )}
    </>
  )
}
