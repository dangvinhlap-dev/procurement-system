import { App, Checkbox, Form, Input } from 'antd'
import { useState } from 'react'
import { Select } from '../../classic/Select'
import { useDivisionMutations, useOrganizationUserOptions, userSelectOptions, type Division } from '../../api/organization'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'

interface DivisionFormValues {
  code: string
  name: string
  directorUserId: string | null
  isActive: boolean
}

/**
 * Ngăn kéo chi tiết khối (lát O1). Mã không đổi được sau khi tạo; Giám đốc khối là tài khoản có vai trò tại công ty – người duyệt cấp
 * "GĐ khối của phiếu". Ngừng dùng: bộ phận đang gán giữ khối, bộ phận khác không gán mới được.
 * @param division Khối đang mở; null = thêm mới.
 * @param onClose Đóng ngăn kéo.
 */
export function DivisionDrawer({ division, onClose }: { division: Division | null; onClose: () => void }) {
  const { message } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<DivisionFormValues>()
  const mutations = useDivisionMutations()
  const editable = hasPermission(Permission.DepartmentManage)
  const users = useOrganizationUserOptions(editable)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const isNew = division === null

  const initialValues: DivisionFormValues = {
    code: division?.code ?? '',
    name: division?.name ?? '',
    directorUserId: division?.directorUserId ?? null,
    isActive: division?.isActive ?? true,
  }

  const save = async () => {
    let values: DivisionFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn trường chưa hợp lệ – xem các ô báo đỏ.')
      return
    }
    setError(null)
    setSaving(true)
    const input = { name: values.name.trim(), directorUserId: values.directorUserId ?? null }
    try {
      if (isNew) {
        await mutations.create.mutateAsync({ ...input, code: values.code })
        message.success(`Đã thêm khối ${values.code.toUpperCase()}.`)
      } else {
        await mutations.update.mutateAsync({ id: division.id, input: { ...input, isActive: values.isActive } })
        message.success(`Đã lưu khối ${division.code}.`)
      }
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được khối.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <DetailDrawer
      open
      title={isNew ? 'Thêm khối' : `Khối: ${division.code}`}
      audit={{ entityType: 'Division', id: division?.id }}
      onClose={onClose}
      onSave={editable ? save : undefined}
      saving={saving}
      error={error}
      form={form}
      initialValues={initialValues}
      readOnly={!editable}
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <>
              <Form.Item
                label="Mã khối"
                name="code"
                extra={isNew ? '2–20 chữ không dấu hoặc số, ví dụ HCNS. Không đổi được sau khi tạo.' : undefined}
                rules={[
                  { required: true, message: 'Hãy nhập mã khối.' },
                  { pattern: /^[A-Za-z0-9]{2,20}$/, message: 'Mã gồm 2–20 chữ không dấu hoặc số.' },
                ]}
                normalize={(v: string) => v.toUpperCase()}
              >
                <Input disabled={!isNew} autoFocus={isNew} autoComplete="off" />
              </Form.Item>
              <Form.Item label="Tên khối" name="name" rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên khối.' }, { max: 200 }]}>
                <Input />
              </Form.Item>
              <Form.Item
                label="Giám đốc khối"
                name="directorUserId"
                extra="Người duyệt cấp “GĐ khối của phiếu” ở Quy trình duyệt (APRL). Chỉ chọn được tài khoản có vai trò tại công ty."
              >
                <Select
                  allowClear
                  placeholder="Chọn tài khoản"
                  loading={users.isLoading}
                  options={userSelectOptions(users.data, { id: division?.directorUserId ?? null, name: division?.directorName ?? null })}
                />
              </Form.Item>
              {!isNew && (
                <Form.Item
                  name="isActive"
                  valuePropName="checked"
                  extra="Ngừng dùng: bộ phận đang gán giữ khối này; bộ phận khác không gán mới được."
                >
                  <Checkbox>Còn sử dụng</Checkbox>
                </Form.Item>
              )}
            </>
          ),
        },
      ]}
    />
  )
}
