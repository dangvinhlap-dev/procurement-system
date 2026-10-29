import { LockOutlined, UserOutlined } from '@ant-design/icons'
import { Alert, Button, Form, Input, Typography } from 'antd'
import { useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { useAuth } from '../auth/useAuth'
import './LoginPage.css'

/** Tên ứng dụng hiển thị trên màn hình đăng nhập. */
const APP_NAME = 'Quản lý mua hàng và kho'

/**
 * Màn hình đăng nhập – ngoại lệ duy nhất không nằm trong AppShell vì chưa có người dùng và công ty
 * (ui-guide.md mục "Màn hình đăng nhập"). Vẽ như hộp thoại Windows: thanh tiêu đề, nhãn bên trái ô nhập.
 * Enter để đăng nhập; sai thì hiện thông báo tiếng Việt từ server.
 */
export function LoginPage() {
  const { session, login, logoutReason } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const from = (location.state as { from?: string } | null)?.from ?? '/'

  if (session) return <Navigate to={from} replace />

  const submit = async (values: { userName: string; password: string }) => {
    setSubmitting(true)
    setError(null)
    try {
      await login(values.userName, values.password)
      navigate(from, { replace: true })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Đăng nhập không thành công.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="login-page">
      <div className="login-dialog" role="dialog" aria-labelledby="login-title">
        <div className="login-title-bar" id="login-title">
          Đăng nhập – {APP_NAME}
        </div>
        <div className="login-body">
          <Typography.Title level={4} className="login-app-name">
            {APP_NAME}
          </Typography.Title>
          {(error ?? logoutReason) && (
            <Alert className="login-alert" type={error ? 'error' : 'warning'} showIcon title={error ?? logoutReason} />
          )}
          <Form layout="horizontal" labelCol={{ flex: '110px' }} labelAlign="left" colon={false} onFinish={submit} requiredMark={false}>
            <Form.Item
              label="Tên đăng nhập"
              name="userName"
              rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên đăng nhập.' }]}
            >
              <Input prefix={<UserOutlined />} autoFocus autoComplete="username" />
            </Form.Item>
            <Form.Item label="Mật khẩu" name="password" rules={[{ required: true, message: 'Hãy nhập mật khẩu.' }]}>
              <Input.Password prefix={<LockOutlined />} autoComplete="current-password" />
            </Form.Item>
            <div className="login-actions">
              <Button type="primary" htmlType="submit" loading={submitting}>
                Đăng nhập
              </Button>
            </div>
          </Form>
        </div>
        <div className="login-footer">Phiên bản {__APP_VERSION__}</div>
      </div>
    </div>
  )
}
