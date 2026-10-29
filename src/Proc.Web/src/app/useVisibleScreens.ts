import { useMemo } from 'react'
import { useAuth } from '../auth/useAuth'
import { screens, type Screen } from './screens'

/**
 * Các màn hình người dùng được mở tại công ty đang làm việc – dùng cho thanh menu, cây điều hướng, ô lệnh.
 * Đổi công ty thì danh sách đổi theo, vì quyền gán theo từng công ty (design.md mục 9.1).
 * @returns Danh sách màn hình được phép, giữ thứ tự khai báo.
 */
export function useVisibleScreens(): Screen[] {
  const { hasPermission } = useAuth()
  return useMemo(() => screens.filter((s) => !s.permission || hasPermission(s.permission)), [hasPermission])
}
