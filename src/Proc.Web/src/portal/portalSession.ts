import { createContext, useContext } from 'react'
import type { PortalSessionData } from './portalApi'

/** Trạng thái phiên Portal dùng chung cho các trang /ncc. */
export interface PortalSessionValue {
  /** Phiên hiện tại; null = chưa đăng nhập. */
  session: PortalSessionData | null
  /** Lưu phiên sau khi qua OTP hoặc đổi mật khẩu (token mới). */
  signIn: (session: PortalSessionData) => void
  /** Đăng xuất (xóa token Portal đã lưu). */
  signOut: () => void
}

/** Context phiên Portal – khung `/ncc` cung cấp. */
export const PortalSessionContext = createContext<PortalSessionValue | null>(null)

/**
 * Phiên Portal của trang đang mở.
 * @returns Phiên và hàm đăng nhập / đăng xuất.
 * @throws Error khi dùng ngoài khung `/ncc`.
 */
export function usePortalSession(): PortalSessionValue {
  const value = useContext(PortalSessionContext)
  if (!value) throw new Error('usePortalSession phải dùng trong khung Portal (/ncc).')
  return value
}
