import { Alert, App, Form, Input, Modal } from 'antd'
import { useState } from 'react'
import { apiFetch } from '../../api/client'

interface FormValues {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

/**
 * Hộp thoại đổi mật khẩu của chính người đang đăng nhập (mở từ menu người dùng trên thanh menu).
 * Quy tắc mật khẩu (design.md mục 9.4) do server kiểm tra; ở đây chỉ báo sớm lỗi bỏ trống và nhập lại không khớp.
 * Esc hoặc nút Hủy để đóng.
 */
export function ChangePasswordDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [form] = Form.useForm<FormValues>()
  const { message } = App.useApp()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const close = () => {
    form.resetFields()
    setError(null)
    onClose()
  }

  const submit = async (values: FormValues) => {
    setSaving(true)
    setError(null)
    try {
      await apiFetch<void>('/api/auth/change-password', {
        method: 'POST',
        body: { currentPassword: values.currentPassword, newPassword: values.newPassword },
      })
      message.success('Đã đổi mật khẩu.')
      close()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không đổi được mật khẩu.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal
      title="Đổi mật khẩu"
      open={open}
      onCancel={close}
      onOk={() => form.submit()}
      okText="Đổi mật khẩu"
      cancelText="Hủy"
      confirmLoading={saving}
      destroyOnHidden
      width={420}
    >
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 12 }} />}
      <Form form={form} layout="vertical" onFinish={submit} requiredMark={false}>
        <Form.Item label="Mật khẩu hiện tại" name="currentPassword" rules={[{ required: true, message: 'Hãy nhập mật khẩu hiện tại.' }]}>
          <Input.Password autoFocus autoComplete="current-password" />
        </Form.Item>
        <Form.Item
          label="Mật khẩu mới"
          name="newPassword"
          extra="Tối thiểu 8 ký tự, có chữ thường và chữ số."
          rules={[{ required: true, message: 'Hãy nhập mật khẩu mới.' }]}
        >
          <Input.Password autoComplete="new-password" />
        </Form.Item>
        <Form.Item
          label="Nhập lại mật khẩu mới"
          name="confirmPassword"
          dependencies={['newPassword']}
          rules={[
            { required: true, message: 'Hãy nhập lại mật khẩu mới.' },
            ({ getFieldValue }) => ({
              validator: (_, value) =>
                !value || value === getFieldValue('newPassword')
                  ? Promise.resolve()
                  : Promise.reject(new Error('Mật khẩu nhập lại không khớp.')),
            }),
          ]}
        >
          <Input.Password autoComplete="new-password" />
        </Form.Item>
      </Form>
    </Modal>
  )
}
