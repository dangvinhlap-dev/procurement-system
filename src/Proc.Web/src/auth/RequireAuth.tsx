import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import { LOGIN_PATH } from './paths'
import { useAuth } from './useAuth'

/**
 * Chặn trang cần đăng nhập: chưa đăng nhập thì chuyển sang màn hình đăng nhập,
 * nhớ trang đang mở để quay lại sau khi đăng nhập xong.
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { session } = useAuth()
  const location = useLocation()
  if (!session) return <Navigate to={LOGIN_PATH} replace state={{ from: location.pathname + location.search }} />
  return children
}
