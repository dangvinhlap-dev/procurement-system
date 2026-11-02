import { StopOutlined } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Flex, Form, Input, Select } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useState } from 'react'
import {
  conflictRelationLabels,
  useConflictDeclarationMutations,
  useConflictSupplierOptions,
  useConflictUserOptions,
  type ConflictDeclaration,
  type ConflictRelation,
} from '../../api/conflicts'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { renderRequiredMark } from '../../components/RequiredMark/requiredMark'
import { DATE_INPUT_FORMATS, formatDate, formatDateTime } from '../../utils/format'
import { matchesKeyword } from '../../utils/text'

interface ConflictFormValues {
  userId: string | null
  supplierId: string | null
  relation: ConflictRelation | null
  description: string
  validFrom: Dayjs | null
}

/** Lọc ô chọn theo nhãn, gõ có dấu hoặc không dấu đều tìm được (CLAUDE.md mục 5). */
const filterByLabel = (input: string, option?: { label?: unknown }) => matchesKeyword(String(option?.label ?? ''), input)

/**
 * Ngăn kéo kê khai xung đột lợi ích (design.md mục 18.8). Kê khai mới: tự kê khai, hoặc ghi thay người khác khi có quyền quản lý.
 * Kê khai đã ghi chỉ xem – không sửa, không xóa; hết quan hệ thì người có quyền quản lý bấm "Kết thúc kê khai" (bắt buộc lý do), để người
 * kê khai không tự gỡ chặn duyệt của mình.
 * @param declaration Kê khai đang mở; null = kê khai mới.
 * @param onClose Đóng ngăn kéo.
 */
export function ConflictDrawer({ declaration, onClose }: { declaration: ConflictDeclaration | null; onClose: () => void }) {
  const { message, modal } = App.useApp()
  const { hasPermission, session } = useAuth()
  const [form] = Form.useForm<ConflictFormValues>()
  const mutations = useConflictDeclarationMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const isNew = declaration === null
  const canManage = hasPermission(Permission.ConflictManage)
  const suppliers = useConflictSupplierOptions(isNew)
  const users = useConflictUserOptions(isNew && canManage)

  const initialValues: ConflictFormValues = isNew
    ? { userId: null, supplierId: null, relation: null, description: '', validFrom: dayjs() }
    : {
        userId: declaration.userId,
        supplierId: declaration.supplierId,
        relation: declaration.relation,
        description: declaration.description,
        validFrom: dayjs(declaration.validFrom),
      }

  const save = async () => {
    let values: ConflictFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn trường chưa hợp lệ – xem các ô báo đỏ.')
      return
    }
    setError(null)
    setSaving(true)
    try {
      const saved = await mutations.create.mutateAsync({
        userId: values.userId ?? null,
        supplierId: values.supplierId!,
        relation: values.relation!,
        description: values.description.trim(),
        validFrom: values.validFrom ? values.validFrom.format('YYYY-MM-DD') : null,
      })
      message.success(`Đã ghi kê khai quan hệ với NCC ${saved.supplierCode}.`)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không ghi được kê khai.')
    } finally {
      setSaving(false)
    }
  }

  const end = () => {
    if (!declaration) return
    let date = dayjs().subtract(1, 'day')
    let reason = ''
    modal.confirm({
      title: `Kết thúc kê khai quan hệ với NCC ${declaration.supplierCode}?`,
      content: (
        <Flex vertical gap={6}>
          <label htmlFor="conflict-end-date">{renderRequiredMark('Ngày cuối còn quan hệ', { required: true })}</label>
          <DatePicker id="conflict-end-date" format={DATE_INPUT_FORMATS} defaultValue={date} onChange={(d) => (date = d ?? dayjs())} />
          <label htmlFor="conflict-end-reason">{renderRequiredMark('Lý do kết thúc', { required: true })}</label>
          <Input.TextArea id="conflict-end-reason" rows={3} maxLength={500} onChange={(e) => (reason = e.target.value)} />
        </Flex>
      ),
      okText: 'Kết thúc',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        if (!reason.trim()) {
          message.error('Hãy nhập lý do kết thúc kê khai.')
          throw new Error('missing reason')
        }
        try {
          await mutations.end.mutateAsync({ id: declaration.id, validTo: date.format('YYYY-MM-DD'), reason: reason.trim() })
          message.success('Đã kết thúc kê khai.')
          onClose()
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không kết thúc được kê khai.')
        }
      },
    })
  }

  const supplierOptions = isNew
    ? (suppliers.data ?? []).map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))
    : [{ value: declaration.supplierId, label: `${declaration.supplierCode} – ${declaration.supplierName}` }]
  const userOptions = isNew
    ? (users.data ?? []).map((u) => ({ value: u.id, label: `${u.fullName} (${u.userName})` }))
    : [{ value: declaration.userId, label: `${declaration.userFullName} (${declaration.userName})` }]

  return (
    <DetailDrawer
      open
      title={isNew ? 'Kê khai xung đột lợi ích' : `Kê khai: ${declaration.userName} – ${declaration.supplierCode}`}
      audit={{ entityType: 'ConflictDeclaration', id: declaration?.id }}
      onClose={onClose}
      onSave={isNew ? save : undefined}
      saving={saving}
      error={error}
      form={form}
      formName="conflict"
      initialValues={initialValues}
      readOnly={!isNew}
      extraActions={
        !isNew && canManage && !declaration.validTo ? (
          <Button danger icon={<StopOutlined />} onClick={end} loading={mutations.end.isPending}>
            Kết thúc kê khai
          </Button>
        ) : undefined
      }
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <>
              {isNew ? (
                <Alert
                  type="info"
                  showIcon
                  style={{ marginBottom: 8 }}
                  title="Khi kê khai còn hiệu lực, bạn sẽ không duyệt, chấm thầu, chọn thầu các phiếu có NCC này. Kê khai đã ghi không sửa, không xóa được."
                />
              ) : (
                <Alert
                  type={declaration.isActive ? 'warning' : 'info'}
                  showIcon
                  style={{ marginBottom: 8 }}
                  title={
                    declaration.isActive
                      ? 'Đang hiệu lực – người này không duyệt, chấm thầu, chọn thầu phiếu có NCC này.'
                      : declaration.validTo
                        ? `Đã kết thúc ngày ${formatDate(declaration.validTo)}: ${declaration.endReason ?? ''}`
                        : `Chưa hiệu lực – bắt đầu từ ${formatDate(declaration.validFrom)}.`
                  }
                />
              )}
              {(canManage || !isNew) && (
                <Form.Item
                  label="Người có quan hệ"
                  name="userId"
                  extra={isNew ? `Để trống = tự kê khai (${session?.user.fullName ?? ''}).` : undefined}
                >
                  <Select allowClear options={userOptions} showSearch={{ filterOption: filterByLabel }} placeholder="Chính tôi" />
                </Form.Item>
              )}
              <Form.Item label="Nhà cung cấp" name="supplierId" rules={[{ required: true, message: 'Hãy chọn nhà cung cấp.' }]}>
                <Select
                  options={supplierOptions}
                  loading={suppliers.isLoading}
                  showSearch={{ filterOption: filterByLabel }}
                  placeholder="Gõ mã hoặc tên NCC"
                  autoFocus={isNew}
                />
              </Form.Item>
              <Form.Item label="Loại quan hệ" name="relation" rules={[{ required: true, message: 'Hãy chọn loại quan hệ.' }]}>
                <Select options={Object.entries(conflictRelationLabels).map(([value, label]) => ({ value, label }))} />
              </Form.Item>
              <Form.Item
                label="Mô tả quan hệ"
                name="description"
                rules={[{ required: true, whitespace: true, message: 'Hãy mô tả quan hệ lợi ích.' }, { max: 500 }]}
                extra="Ví dụ: Em trai là giám đốc kinh doanh của NCC."
              >
                <Input.TextArea rows={3} maxLength={500} />
              </Form.Item>
              <Form.Item label="Hiệu lực từ ngày" name="validFrom" rules={[{ required: true, message: 'Chọn ngày bắt đầu.' }]}>
                <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
              </Form.Item>
              {!isNew && (
                <div className="detail-grid">
                  <Form.Item label="Người ghi nhận">
                    <Input value={declaration.createdByName ?? ''} readOnly />
                  </Form.Item>
                  <Form.Item label="Thời điểm ghi nhận">
                    <Input value={formatDateTime(declaration.createdAt)} readOnly />
                  </Form.Item>
                </div>
              )}
            </>
          ),
        },
      ]}
    />
  )
}
