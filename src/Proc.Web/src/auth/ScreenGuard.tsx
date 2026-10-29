import { StopOutlined } from '@ant-design/icons'
import { Result } from 'antd'
import type { ReactNode } from 'react'
import type { Screen } from '../app/screens'
import { useAuth } from './useAuth'

/**
 * Chặn mở thẳng đường dẫn của màn hình không có quyền (ví dụ gõ URL hoặc dùng dấu trang cũ).
 * Chỉ là lớp bảo vệ ở giao diện; dữ liệu vẫn do API kiểm tra quyền.
 */
export function ScreenGuard({ screen, children }: { screen: Screen; children: ReactNode }) {
  const { hasPermission, workingCompany } = useAuth()
  if (screen.permission && !hasPermission(screen.permission)) {
    return (
      <Result
        icon={<StopOutlined />}
        title="Bạn không có quyền mở màn hình này"
        subTitle={`${screen.title} – tại ${workingCompany?.code ?? ''} ${workingCompany?.name ?? ''}. Liên hệ quản trị hệ thống nếu cần cấp quyền.`}
      />
    )
  }
  return children
}
