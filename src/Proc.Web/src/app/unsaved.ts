import { createContext, useContext, useEffect, useLayoutEffect, useRef } from 'react'

/** Hàm đăng ký kiểm tra "còn thay đổi chưa lưu" của trang; trả hàm hủy đăng ký. Ngoài AppShell không làm gì. */
export const UnsavedContext = createContext<(check: () => boolean) => () => void>(() => () => {})

/**
 * Báo cho khung biết trang đang có thay đổi chưa lưu – để hỏi lại trước khi rời trang, đổi công ty, đăng xuất, tải lại trang (yêu cầu
 * người dùng 13/11/2026). Hàm kiểm tra gọi lúc cần hỏi, nên luôn đọc trạng thái mới nhất.
 * @param check Trả true khi còn thay đổi chưa lưu.
 * @param enabled false thì không đăng ký (ví dụ phiếu chỉ xem, ngăn kéo đang đóng).
 */
export function useUnsavedCheck(check: () => boolean, enabled = true): void {
  const register = useContext(UnsavedContext)
  const checkRef = useRef(check)
  useLayoutEffect(() => {
    checkRef.current = check
  })
  useEffect(() => {
    if (!enabled) return
    return register(() => checkRef.current())
  }, [register, enabled])
}
