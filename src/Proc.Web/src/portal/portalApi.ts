import type { PortalInviteInput, PortalInviteResult, SupplierPortalUser } from '../api/supplierPortal'

/*
 * Gọi API Portal NCC (/api/portal – design.md 19.5, lát P1). Token Portal lưu riêng (khóa `proc.portal.session`), không dùng chung phiên và
 * hàm apiFetch của người dùng nội bộ: token NCC không bao giờ được gửi tới API nội bộ và ngược lại.
 */

/** Khóa lưu phiên Portal trong sessionStorage (mất khi đóng tab – máy dùng chung không giữ đăng nhập). */
const PORTAL_SESSION_KEY = 'proc.portal.session'

/** NCC của người dùng Portal. */
export interface PortalSupplier {
  id: string
  code: string
  name: string
  taxCode: string | null
  address: string | null
  phone: string | null
  email: string | null
  status: 'Potential' | 'Approved' | 'Suspended' | 'Blacklisted'
}

/** Người dùng Portal đang đăng nhập. */
export interface PortalMe {
  userId: string
  email: string
  fullName: string
  phone: string | null
  position: string | null
  isSupplierAdmin: boolean
  supplier: PortalSupplier
}

/** Phiên Portal: token + người dùng. */
export interface PortalSessionData {
  accessToken: string
  expiresAt: string
  me: PortalMe
}

/** Bước 2 đăng nhập: chờ nhập OTP. */
export interface PortalLoginChallenge {
  challengeId: string
  maskedEmail: string
  expiresAt: string
  /** Mã OTP – chỉ có ở máy dev / test tự động. */
  devCode: string | null
}

/** Thông tin link mời. */
export interface PortalInviteInfo {
  email: string
  fullName: string
  supplierName: string
}

/** Lỗi gọi API Portal, kèm mã HTTP. */
export class PortalApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

/**
 * Đọc phiên Portal đã lưu.
 * @returns Phiên còn hạn, hoặc null.
 */
export function loadPortalSession(): PortalSessionData | null {
  try {
    const raw = sessionStorage.getItem(PORTAL_SESSION_KEY)
    if (!raw) return null
    const s = JSON.parse(raw) as PortalSessionData
    return new Date(s.expiresAt).getTime() > Date.now() ? s : null
  } catch {
    return null
  }
}

/**
 * Lưu hoặc xóa phiên Portal.
 * @param session Phiên; null để xóa.
 */
export function savePortalSession(session: PortalSessionData | null): void {
  try {
    if (session) sessionStorage.setItem(PORTAL_SESSION_KEY, JSON.stringify(session))
    else sessionStorage.removeItem(PORTAL_SESSION_KEY)
  } catch {
    // Trình duyệt chặn lưu trữ: phiên chỉ sống trong bộ nhớ.
  }
}

/** Hàm gọi khi token Portal hết hiệu lực (PortalSessionProvider gắn vào để đăng xuất). */
let onPortalUnauthorized: () => void = () => {}

/**
 * Gắn hàm xử lý khi máy chủ trả 401 cho request có token Portal.
 * @param handler Hàm xử lý.
 */
export function setPortalUnauthorizedHandler(handler: () => void): void {
  onPortalUnauthorized = handler
}

/**
 * Gọi API Portal; tự gắn token Portal đang lưu.
 * @param path Đường dẫn bắt đầu bằng /api/portal.
 * @param init Phương thức và thân (đối tượng, tự chuyển JSON).
 * @returns Dữ liệu JSON (undefined khi 204).
 * @throws PortalApiError khi máy chủ báo lỗi hoặc không kết nối được.
 */
export async function portalFetch<T>(path: string, init: { method?: string; body?: unknown } = {}): Promise<T> {
  const headers = new Headers()
  const token = loadPortalSession()?.accessToken
  if (token) headers.set('Authorization', `Bearer ${token}`)
  if (init.body !== undefined) headers.set('Content-Type', 'application/json')
  let response: Response
  try {
    response = await fetch(path, {
      method: init.method ?? 'GET',
      headers,
      body: init.body === undefined ? undefined : JSON.stringify(init.body),
    })
  } catch {
    throw new PortalApiError(0, 'Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.')
  }
  if (!response.ok) {
    let message = `Lỗi HTTP ${response.status}.`
    try {
      const body = (await response.json()) as { title?: string; errors?: Record<string, string[]> }
      message = (body.errors ? Object.values(body.errors).flat()[0] : undefined) ?? body.title ?? message
    } catch {
      if (response.status >= 500) message = 'Máy chủ đang lỗi hoặc chưa chạy.'
    }
    if (response.status === 401 && token) onPortalUnauthorized()
    throw new PortalApiError(response.status, message)
  }
  return (response.status === 204 ? undefined : await response.json()) as T
}

/** Các lệnh API Portal. */
export const portalApi = {
  /** Đăng nhập bước 1: email + mật khẩu → gửi OTP. */
  login: (email: string, password: string) =>
    portalFetch<PortalLoginChallenge>('/api/portal/auth/login', { method: 'POST', body: { email, password } }),
  /** Đăng nhập bước 2: OTP → token. */
  verifyOtp: (challengeId: string, code: string) =>
    portalFetch<PortalSessionData>('/api/portal/auth/verify-otp', { method: 'POST', body: { challengeId, code } }),
  /** Thông tin link mời. */
  invite: (token: string) => portalFetch<PortalInviteInfo>(`/api/portal/auth/invite?token=${encodeURIComponent(token)}`),
  /** Kích hoạt: đặt mật khẩu lần đầu. */
  activate: (token: string, password: string) =>
    portalFetch<void>('/api/portal/auth/activate', { method: 'POST', body: { token, password } }),
  /** Quên mật khẩu: gửi link đặt lại. */
  forgotPassword: (email: string) =>
    portalFetch<{ devToken: string | null }>('/api/portal/auth/forgot-password', { method: 'POST', body: { email } }),
  /** Đặt lại mật khẩu từ link. */
  resetPassword: (token: string, password: string) =>
    portalFetch<void>('/api/portal/auth/reset-password', { method: 'POST', body: { token, password } }),
  /** Người dùng đang đăng nhập. */
  me: () => portalFetch<PortalMe>('/api/portal/me'),
  /** Đổi mật khẩu – trả token mới. */
  changePassword: (currentPassword: string, newPassword: string) =>
    portalFetch<PortalSessionData>('/api/portal/change-password', { method: 'POST', body: { currentPassword, newPassword } }),
  /** Người dùng của NCC mình (quản trị NCC). */
  users: () => portalFetch<SupplierPortalUser[]>('/api/portal/users'),
  /** Mời người dùng con. */
  inviteUser: (input: PortalInviteInput) => portalFetch<PortalInviteResult>('/api/portal/users', { method: 'POST', body: input }),
  /** Gửi lại lời mời. */
  resendInvite: (id: string) => portalFetch<PortalInviteResult>(`/api/portal/users/${id}/resend-invite`, { method: 'POST' }),
  /** Khóa người dùng con. */
  disableUser: (id: string) => portalFetch<SupplierPortalUser>(`/api/portal/users/${id}/disable`, { method: 'POST' }),
  /** Mở khóa người dùng con. */
  enableUser: (id: string) => portalFetch<SupplierPortalUser>(`/api/portal/users/${id}/enable`, { method: 'POST' }),
}
