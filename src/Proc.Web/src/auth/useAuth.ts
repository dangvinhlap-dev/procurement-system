import { createContext, useContext } from 'react'
import type { Session, UserCompanyInfo } from './types'

/** Những gì AuthProvider cung cấp cho các component. */
export interface AuthContextValue {
  session: Session | null
  /** Công ty đang làm việc (thuộc danh sách công ty của người dùng). */
  workingCompany: UserCompanyInfo | null
  /**
   * Đăng nhập.
   * @throws ApiError với thông điệp tiếng Việt khi sai thông tin, bị khóa, ngừng dùng…
   */
  login: (userName: string, password: string) => Promise<void>
  /** Đăng xuất; reason hiện cho người dùng nếu có (ví dụ "phiên đã hết hạn"). */
  logout: (reason?: string) => void
  /** Đổi công ty đang làm việc. */
  setWorkingCompany: (companyId: string) => void
  /** Thông báo lý do đăng xuất gần nhất, hiện trên màn hình đăng nhập. */
  logoutReason: string | null
  /** Người dùng có quyền tại công ty đang làm việc không (dùng để ẩn menu, nút). */
  hasPermission: (permission: string) => boolean
}

export const AuthContext = createContext<AuthContextValue | null>(null)

/**
 * Lấy phiên đăng nhập hiện tại.
 * @returns Giá trị của AuthProvider.
 * @throws Error khi dùng ngoài AuthProvider.
 */
export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext)
  if (!value) throw new Error('useAuth phải dùng bên trong AuthProvider')
  return value
}
