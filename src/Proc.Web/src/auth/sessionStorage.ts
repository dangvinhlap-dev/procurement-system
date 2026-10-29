import type { Session } from './types'

/** Khóa lưu phiên trong sessionStorage. */
export const SESSION_KEY = 'proc.session'

/**
 * Đọc phiên đã lưu. Dùng sessionStorage (mất khi đóng tab/trình duyệt) thay vì localStorage
 * để máy dùng chung không giữ đăng nhập của người trước.
 * @returns Phiên còn hạn, hoặc null nếu chưa có, đã hết hạn hoặc trình duyệt chặn lưu trữ.
 */
export function loadSession(): Session | null {
  try {
    const raw = sessionStorage.getItem(SESSION_KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as Session
    return new Date(session.expiresAt).getTime() > Date.now() ? session : null
  } catch {
    return null
  }
}

/**
 * Lưu hoặc xóa phiên.
 * @param session Phiên cần lưu; null để xóa.
 */
export function saveSession(session: Session | null): void {
  try {
    if (session) sessionStorage.setItem(SESSION_KEY, JSON.stringify(session))
    else sessionStorage.removeItem(SESSION_KEY)
  } catch {
    // Trình duyệt chặn lưu trữ: phiên vẫn dùng được đến khi tải lại trang.
  }
}
