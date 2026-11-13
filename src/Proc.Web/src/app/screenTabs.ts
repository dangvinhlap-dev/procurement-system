import { createContext, useContext, useEffect, useLayoutEffect, useRef } from 'react'
import { useNavigate } from 'react-router'

/** Số màn hình mở cùng lúc tối đa trong khung ứng dụng (người dùng chốt 13/11/2026: 6 thẻ, bản demo 4 thẻ). */
export const MAX_SCREEN_TABS = 6

/**
 * Cách mở một màn hình:
 * - `auto`: đã có thẻ cùng đường dẫn thì chuyển sang thẻ đó, chưa có thì mở thẻ mới (đủ 6 thẻ thì báo, không mở);
 * - `current`: mở thay nội dung của thẻ đang xem (như trước khi có thẻ).
 */
export type OpenMode = 'auto' | 'current'

/** Thao tác trên các thẻ màn hình, do AppShell cung cấp. */
export interface ScreenTabsApi {
  /**
   * Mở màn hình theo đường dẫn.
   * @param path Đường dẫn trong ứng dụng, ví dụ `/mua-hang/po` hoặc `/mua-hang/po/12`.
   * @param mode Cách mở (mặc định `auto`).
   */
  open: (path: string, mode?: OpenMode) => void
  /** Quay lại trang trước của chính thẻ đang xem (mỗi thẻ có lịch sử riêng). */
  back: () => void
}

/** Ngữ cảnh thẻ màn hình; null khi trang nằm ngoài AppShell (Portal, trang điện thoại, màn hình mẫu). */
export const ScreenTabsContext = createContext<ScreenTabsApi | null>(null)

/** Trang đang nằm trong thẻ được hiện hay không; ngoài AppShell luôn là true. */
export const TabActiveContext = createContext(true)

/** Hàm đặt nhãn phụ cho thẻ chứa trang (ví dụ số phiếu); ngoài AppShell không làm gì. */
export const TabLabelContext = createContext<(label: string | undefined) => void>(() => {})

/**
 * Trang có nằm trong thẻ đang hiện không. Phím tắt cấp cửa sổ (Ctrl+S, F2, Esc…) chỉ chạy ở thẻ đang hiện – thẻ ẩn vẫn còn trong trang để
 * giữ dữ liệu đang nhập, nếu không chặn thì Ctrl+S lưu cùng lúc mọi phiếu đang mở.
 * @returns true nếu là thẻ đang hiện hoặc trang không nằm trong thẻ.
 */
export function useIsActiveTab(): boolean {
  return useContext(TabActiveContext)
}

/**
 * Hàm mở màn hình theo quy tắc thẻ; ngoài AppShell thì điều hướng thường.
 * @returns Hàm `(path, mode?) => void`.
 */
export function useOpenScreen(): (path: string, mode?: OpenMode) => void {
  const api = useContext(ScreenTabsContext)
  const navigate = useNavigate()
  return api ? api.open : (path) => void navigate(path)
}

/**
 * Hiện nhãn phụ trên thẻ chứa trang (ví dụ "PO-2026-0012" thay vì chỉ tên màn hình); trang đóng thì nhãn tự bỏ.
 * @param label Nhãn; undefined = chỉ hiện tên màn hình.
 */
export function useScreenTabLabel(label: string | undefined): void {
  const setLabel = useContext(TabLabelContext)
  useEffect(() => {
    setLabel(label)
    return () => setLabel(undefined)
  }, [label, setLabel])
}

/**
 * Phần tử có nằm trong một thẻ đang ẩn không – để tìm ô tìm kiếm, lớp phủ… chỉ trong thẻ đang hiện.
 * @param el Phần tử cần xét.
 * @returns true nếu thuộc thẻ đang ẩn.
 */
export function isInHiddenTab(el: Element): boolean {
  return el.closest('.app-tab-panel:not(.is-active)') !== null
}

/**
 * Thao tác thẻ màn hình cho khung (thanh công cụ, menu); ngoài AppShell thì điều hướng thường.
 * @returns `open` và `back`.
 */
export function useScreenTabs(): ScreenTabsApi {
  const api = useContext(ScreenTabsContext)
  const navigate = useNavigate()
  return api ?? { open: (path) => void navigate(path), back: () => void navigate(-1) }
}

/** Hàm đăng ký kiểm tra "còn thay đổi chưa lưu" cho thẻ chứa trang; trả hàm hủy đăng ký. Ngoài AppShell không làm gì. */
export const UnsavedContext = createContext<(check: () => boolean) => () => void>(() => () => {})

/**
 * Báo cho khung biết trang đang có thay đổi chưa lưu – để hỏi lại trước khi rời trang, đóng thẻ, đổi công ty, đăng xuất, tải lại trang
 * (người dùng chốt 13/11/2026: 6 thẻ, bản demo 4 thẻ). Hàm kiểm tra gọi lúc cần hỏi, nên luôn đọc trạng thái mới nhất.
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
