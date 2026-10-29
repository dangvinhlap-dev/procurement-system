import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { apiFetch, setAuthBridge } from '../api/client'
import { loadSession, saveSession } from './sessionStorage'
import type { CurrentUserInfo, LoginResult, Session } from './types'
import { AuthContext, type AuthContextValue } from './useAuth'

/**
 * Chọn công ty làm việc hợp lệ: giữ công ty đang chọn nếu người dùng vẫn được làm việc ở đó,
 * nếu không thì lấy công ty mặc định.
 */
function pickCompany(user: CurrentUserInfo, preferred?: string): string {
  return user.companies.some((c) => c.id === preferred) ? preferred! : user.defaultCompanyId
}

/**
 * Quản lý phiên đăng nhập: lưu token, công ty đang làm việc; tự đăng xuất khi token hết hạn
 * hoặc API trả 401; khi tải lại trang thì gọi /api/auth/me để lấy danh sách công ty mới nhất.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [session, setSession] = useState<Session | null>(loadSession)
  const [logoutReason, setLogoutReason] = useState<string | null>(null)
  // apiFetch đọc qua ref để luôn lấy giá trị mới nhất mà không phải gắn lại bridge.
  // Mọi thay đổi phiên đều đi qua update() nên ref luôn khớp với state.
  const sessionRef = useRef(session)

  const update = useCallback((next: Session | null) => {
    sessionRef.current = next
    saveSession(next)
    setSession(next)
  }, [])

  const logout = useCallback(
    (reason?: string) => {
      // Người dùng tự đăng xuất: báo server để ghi nhật ký truy cập (design.md mục 9.3). Không chờ kết quả;
      // hết phiên (có reason) thì token đã vô hiệu nên không gọi.
      if (!reason && sessionRef.current) void apiFetch<void>('/api/auth/logout', { method: 'POST' }).catch(() => {})
      update(null)
      setLogoutReason(reason ?? null)
      // Xóa dữ liệu đã tải để người đăng nhập sau không thấy dữ liệu của người trước.
      queryClient.clear()
    },
    [update, queryClient],
  )

  useEffect(() => {
    setAuthBridge({
      getAccessToken: () => sessionRef.current?.accessToken ?? null,
      getCompanyId: () => sessionRef.current?.workingCompanyId ?? null,
      onUnauthorized: () => logout('Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại.'),
    })
  }, [logout])

  // Tải lại trang: làm mới thông tin người dùng (quản trị có thể vừa đổi công ty được gán).
  useEffect(() => {
    if (!sessionRef.current) return
    apiFetch<CurrentUserInfo>('/api/auth/me')
      .then((user) => {
        const current = sessionRef.current
        if (current) update({ ...current, user, workingCompanyId: pickCompany(user, current.workingCompanyId) })
      })
      .catch(() => {
        // 401 đã được apiFetch xử lý (đăng xuất); lỗi mạng thì giữ phiên cũ.
      })
  }, [update])

  // Tự đăng xuất đúng lúc token hết hạn.
  useEffect(() => {
    if (!session) return
    const ms = new Date(session.expiresAt).getTime() - Date.now()
    const timer = setTimeout(() => logout('Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại.'), Math.max(0, ms))
    return () => clearTimeout(timer)
  }, [session, logout])

  const value = useMemo<AuthContextValue>(() => {
    const workingCompany = session?.user.companies.find((c) => c.id === session.workingCompanyId) ?? null
    return {
      session,
      logoutReason,
      workingCompany,
      hasPermission: (permission) => workingCompany?.permissions.includes(permission) ?? false,
      login: async (userName, password) => {
        const result = await apiFetch<LoginResult>('/api/auth/login', { method: 'POST', body: { userName, password } })
        setLogoutReason(null)
        queryClient.clear()
        update({
          accessToken: result.accessToken,
          expiresAt: result.expiresAt,
          user: result.user,
          workingCompanyId: result.user.defaultCompanyId,
        })
      },
      logout,
      setWorkingCompany: (companyId) => {
        const current = sessionRef.current
        if (!current || !current.user.companies.some((c) => c.id === companyId)) return
        update({ ...current, workingCompanyId: companyId })
        // Ghi nhật ký truy cập "đổi công ty"; phạm vi dữ liệu vẫn do header X-Company-Id của từng request quyết định.
        void apiFetch<void>('/api/auth/working-company', { method: 'POST', body: { companyId } }).catch(() => {})
        // Dữ liệu đã tải thuộc công ty cũ – tải lại theo công ty mới (mỗi công ty là pháp nhân độc lập).
        void queryClient.invalidateQueries()
      },
    }
  }, [session, logoutReason, logout, update, queryClient])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
