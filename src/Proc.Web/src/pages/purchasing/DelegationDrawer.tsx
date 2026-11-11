import { StopOutlined } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Form, Input } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useState } from 'react'
import {
  delegationStatusLabels,
  useApprovalDelegationMutations,
  useDelegationDocTypes,
  useDelegationUserOptions,
  type ApprovalDelegation,
} from '../../api/delegations'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { Select } from '../../classic/Select'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { DATE_INPUT_FORMATS, formatDate, formatDateTime } from '../../utils/format'
import { matchesKeyword } from '../../utils/text'

interface DelegationFormValues {
  delegatorUserId: string | null
  delegateUserId: string | null
  docType: string
  validFrom: Dayjs | null
  validTo: Dayjs | null
  reason: string
  note: string
}

/** Thời hạn tối đa một lần ủy quyền (ngày) – khớp ApprovalDelegation.MaxDays. */
const MAX_DAYS = 90

/** Lọc ô chọn theo nhãn, gõ có dấu hoặc không dấu đều tìm được (CLAUDE.md mục 5). */
const filterByLabel = (input: string, option?: { label?: unknown }) => matchesKeyword(String(option?.label ?? ''), input)

/**
 * Ngăn kéo ủy quyền duyệt (lát O2, design.md mục 19.3). Lập mới: tự ủy quyền, hoặc lập thay người khác khi có quyền quản lý ủy quyền.
 * Ủy quyền đã lập chỉ xem – không sửa; sai hoặc về sớm thì người ủy quyền (hoặc người quản lý) bấm "Thu hồi".
 * @param delegation Ủy quyền đang mở; null = lập mới.
 * @param onClose Đóng ngăn kéo.
 */
export function DelegationDrawer({ delegation, onClose }: { delegation: ApprovalDelegation | null; onClose: () => void }) {
  const { message, modal } = App.useApp()
  const { hasPermission, session } = useAuth()
  const [form] = Form.useForm<DelegationFormValues>()
  const mutations = useApprovalDelegationMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const isNew = delegation === null
  const canManage = hasPermission(Permission.DelegationManage)
  const users = useDelegationUserOptions(isNew)
  const docTypes = useDelegationDocTypes(isNew)
  const myId = session?.user.userId
  // Giấy ủy quyền có chữ ký: người ủy quyền hoặc người quản lý đính kèm (máy chủ kiểm tra lại).
  const attachmentTab = useAttachmentTab('ApprovalDelegation', delegation?.id, isNew || canManage || delegation.delegatorUserId === myId)

  const initialValues: DelegationFormValues = isNew
    ? { delegatorUserId: null, delegateUserId: null, docType: '', validFrom: dayjs(), validTo: dayjs().add(6, 'day'), reason: '', note: '' }
    : {
        delegatorUserId: delegation.delegatorUserId,
        delegateUserId: delegation.delegateUserId,
        docType: delegation.docType ?? '',
        validFrom: dayjs(delegation.validFrom),
        validTo: dayjs(delegation.validTo),
        reason: delegation.reason,
        note: delegation.note ?? '',
      }

  const save = async () => {
    let values: DelegationFormValues
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
        delegatorUserId: values.delegatorUserId ?? null,
        delegateUserId: values.delegateUserId!,
        docType: values.docType || null,
        validFrom: values.validFrom!.format('YYYY-MM-DD'),
        validTo: values.validTo!.format('YYYY-MM-DD'),
        reason: values.reason.trim(),
        note: values.note.trim() || null,
      })
      message.success(`Đã ủy quyền cho ${saved.delegateName} từ ${formatDate(saved.validFrom)} đến ${formatDate(saved.validTo)}.`)
      // Ngăn kéo đóng sau khi lưu nên tải file chọn lúc lập ngay tại đây.
      await attachmentTab.uploadTo(saved.id)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lập được ủy quyền.')
    } finally {
      setSaving(false)
    }
  }

  const revoke = () => {
    if (!delegation) return
    modal.confirm({
      title: `Thu hồi ủy quyền cho ${delegation.delegateName}?`,
      content: 'Từ lúc thu hồi, người này không duyệt thay được nữa. Các bước đã duyệt thay giữ nguyên.',
      okText: 'Thu hồi',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        try {
          await mutations.revoke.mutateAsync(delegation.id)
          message.success('Đã thu hồi ủy quyền.')
          onClose()
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không thu hồi được ủy quyền.')
        }
      },
    })
  }

  const userOptions = isNew
    ? (users.data ?? []).map((u) => ({ value: u.id, label: `${u.fullName} (${u.userName})` }))
    : [
        { value: delegation.delegatorUserId, label: delegation.delegatorName },
        { value: delegation.delegateUserId, label: delegation.delegateName },
      ]
  const docTypeOptions = [
    { value: '', label: 'Mọi loại phiếu' },
    ...(isNew ? (docTypes.data ?? []).map((t) => ({ value: t.code, label: t.name })) : delegation.docType ? [{ value: delegation.docType, label: delegation.docTypeName }] : []),
  ]

  return (
    <DetailDrawer
      open
      title={isNew ? 'Lập ủy quyền duyệt' : `Ủy quyền: ${delegation.delegatorName} → ${delegation.delegateName}`}
      audit={{ entityType: 'ApprovalDelegation', id: delegation?.id }}
      onClose={onClose}
      onSave={isNew ? save : undefined}
      saving={saving}
      error={error}
      form={form}
      formName="delegation"
      initialValues={initialValues}
      readOnly={!isNew}
      extraActions={
        !isNew && delegation.canRevoke ? (
          <Button danger icon={<StopOutlined />} onClick={revoke} loading={mutations.revoke.isPending}>
            Thu hồi
          </Button>
        ) : undefined
      }
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <>
              <Alert
                type={!isNew && delegation.status === 'Active' ? 'warning' : 'info'}
                showIcon
                style={{ marginBottom: 8 }}
                title={
                  isNew
                    ? 'Trong thời hạn, người được ủy quyền duyệt/từ chối thay bạn ở các cấp bạn đang giữ (vai trò, Trưởng phòng ban, GĐ khối). Người đó vẫn phải có quyền duyệt loại phiếu. Ủy quyền đã lập không sửa được – sai thì thu hồi rồi lập lại.'
                    : delegation.status === 'Revoked'
                      ? `Đã thu hồi lúc ${formatDateTime(delegation.revokedAt)}${delegation.revokedByName ? ` (${delegation.revokedByName})` : ''}.`
                      : `${delegationStatusLabels[delegation.status]}: ${delegation.delegateName} duyệt thay ${delegation.delegatorName} từ ${formatDate(delegation.validFrom)} đến hết ${formatDate(delegation.validTo)}.`
                }
              />
              {(canManage || !isNew) && (
                <Form.Item
                  label="Người ủy quyền"
                  name="delegatorUserId"
                  extra={isNew ? `Để trống = chính tôi (${session?.user.fullName ?? ''}).` : undefined}
                >
                  <Select allowClear options={userOptions} showSearch={{ filterOption: filterByLabel }} placeholder="Chính tôi" />
                </Form.Item>
              )}
              <Form.Item label="Người được ủy quyền" name="delegateUserId" rules={[{ required: true, message: 'Hãy chọn người được ủy quyền.' }]}>
                <Select
                  options={userOptions.filter((o) => isNew ? o.value !== myId || canManage : true)}
                  loading={users.isLoading}
                  showSearch={{ filterOption: filterByLabel }}
                  placeholder="Gõ tên hoặc tên đăng nhập"
                  autoFocus={isNew}
                />
              </Form.Item>
              <Form.Item label="Loại phiếu" name="docType">
                <Select options={docTypeOptions} />
              </Form.Item>
              <div className="detail-grid">
                <Form.Item label="Từ ngày" name="validFrom" rules={[{ required: true, message: 'Chọn ngày bắt đầu.' }]}>
                  <DatePicker
                    format={DATE_INPUT_FORMATS}
                    style={{ width: '100%' }}
                    disabledDate={isNew ? (d) => d.isBefore(dayjs(), 'day') : undefined}
                  />
                </Form.Item>
                <Form.Item
                  label="Đến hết ngày"
                  name="validTo"
                  dependencies={['validFrom']}
                  rules={[
                    { required: true, message: 'Chọn ngày kết thúc.' },
                    ({ getFieldValue }) => ({
                      validator: (_, value: Dayjs | null) => {
                        const from = getFieldValue('validFrom') as Dayjs | null
                        if (!isNew || !value || !from) return Promise.resolve()
                        if (value.isBefore(from, 'day')) return Promise.reject(new Error('Ngày kết thúc phải từ ngày bắt đầu trở đi.'))
                        if (value.diff(from, 'day') + 1 > MAX_DAYS) return Promise.reject(new Error(`Mỗi lần ủy quyền tối đa ${MAX_DAYS} ngày.`))
                        return Promise.resolve()
                      },
                    }),
                  ]}
                >
                  <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
                </Form.Item>
              </div>
              <Form.Item
                label="Lý do"
                name="reason"
                rules={[{ required: true, whitespace: true, message: 'Hãy nhập lý do ủy quyền.' }, { max: 500 }]}
                extra="Ví dụ: Đi công tác Hà Nội 15–20/11."
              >
                <Input maxLength={500} />
              </Form.Item>
              <Form.Item label="Ghi chú" name="note" rules={[{ max: 500 }]}>
                <Input.TextArea rows={2} maxLength={500} />
              </Form.Item>
              {!isNew && (
                <div className="detail-grid">
                  <Form.Item label="Người lập">
                    <Input value={delegation.createdByName ?? ''} readOnly />
                  </Form.Item>
                  <Form.Item label="Thời điểm lập">
                    <Input value={formatDateTime(delegation.createdAt)} readOnly />
                  </Form.Item>
                </div>
              )}
            </>
          ),
        },
        attachmentTab,
      ]}
    />
  )
}
