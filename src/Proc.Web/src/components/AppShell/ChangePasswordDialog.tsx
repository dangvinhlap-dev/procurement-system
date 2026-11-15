import { Alert, App, Form, Input, Modal } from 'antd'
import { useState } from 'react'
import { apiFetch } from '../../api/client'
import type { LoginResult } from '../../auth/types'
import { useAuth } from '../../auth/useAuth'

interface FormValues {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

/**
 * Hộp thoại đổi mật khẩu của chính người đang đăng nhập (mở từ menu người dùng trên Shellbar).
 * Quy tắc mật khẩu (design.md mục 9.4) do server kiểm tra; ở đây chỉ báo sớm lỗi bỏ trống và nhập lại không khớp.
 * Esc hoặc nút Hủy để đóng.
 */
export function ChangePasswordDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [form] = Form.useForm<FormValues>()
  const { message } = App.useApp()
  const { renewToken } = useAuth()
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
      // Đổi xong mọi phiên khác của mình hết hiệu lực; máy chủ trả token mới cho phiên này (lát QT7).
      const result = await apiFetch<LoginResult | null>('/api/auth/change-password', {
        method: 'POST',
        body: { currentPassword: values.currentPassword, newPassword: values.newPassword },
      })
      if (result) renewToken(result.accessToken, result.expiresAt)
      message.success('Đã đổi mật khẩu. Các phiên đăng nhập khác của bạn đã bị đăng xuất.')
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
      <Form form={form} layout="vertical" onFinish={submit}>
        <Form.Item label="Mật khẩu hiện tại" name="currentPassword" rules={[{ required: true, message: 'Hãy nhập mật khẩu hiện tại.' }]}>
          <Input.Password autoFocus autoComplete="current-password" />
        </Form.Item>
        <Form.Item
          label="Mật khẩu mới"
          name="newPassword"
          extra="Có chữ thường và chữ số, đủ độ dài theo cấu hình truy cập (mặc định 8 ký tự), không dùng mật khẩu quá phổ biến."
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
