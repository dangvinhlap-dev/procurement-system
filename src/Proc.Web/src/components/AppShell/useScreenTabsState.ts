import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { useLocation, useNavigate, useNavigationType, type Location } from 'react-router'
import type { ReactNode } from 'react'
import { findScreenByPath } from '../../app/screens'
import { MAX_SCREEN_TABS, type OpenMode, type ScreenTabsApi } from '../../app/screenTabs'

/** Một thẻ màn hình đang mở. */
export interface ScreenTab {
  /** Khóa ổn định của thẻ – giữ nguyên khi thẻ đổi trang, nên trang trong thẻ không bị dựng lại khi chuyển thẻ. */
  id: string
  /** Vị trí (đường dẫn) thẻ đang hiện. */
  location: Location
  /** Lịch sử riêng của thẻ cho nút "Quay lại" (đường dẫn đầy đủ, cũ → mới, tối đa 30). */
  back: string[]
  /** Nhãn phụ do trang đặt (ví dụ số phiếu). */
  label?: string
}

interface TabsState {
  tabs: ScreenTab[]
  activeId: string
  /** Công ty đang làm việc mà các thẻ thuộc về – mọi thẻ cùng một công ty. */
  companyId: string
}

/**
 * Tên hiện trên thẻ: tên màn hình; trang con (phiếu) thêm nhãn phụ do trang đặt (số phiếu), chưa có thì "Lập mới"/"Phiếu".
 * @param tab Thẻ cần đặt tên.
 * @returns Tên ngắn (trên thẻ) và tên đầy đủ (chú thích khi rê chuột).
 */
export function screenTabTitle(tab: ScreenTab): { short: string; full: string; icon?: ReactNode } {
  const { pathname } = tab.location
  const screen = findScreenByPath(pathname)
  if (!screen) return { short: 'Không tìm thấy màn hình', full: pathname }
  if (screen.path === pathname) return { short: screen.navLabel, full: `${screen.title} (${screen.code})`, icon: screen.icon }
  const sub = tab.label ?? (pathname.endsWith('/moi') ? 'Lập mới' : 'Phiếu')
  return { short: `${screen.navLabel}: ${sub}`, full: `${screen.title} – ${sub}`, icon: screen.icon }
}

/** Lưu danh sách thẻ trong phiên trình duyệt: bấm F5 vẫn mở lại đủ thẻ (trang trong thẻ tải lại dữ liệu). */
const STORAGE_KEY = 'proc.screenTabs'
const MAX_BACK = 30

let seq = 0
const newId = () => `t${Date.now().toString(36)}${(seq++).toString(36)}`

/** Đường dẫn đầy đủ của một vị trí (gồm ?… và #…). */
export const pathOf = (l: Pick<Location, 'pathname' | 'search' | 'hash'>) => `${l.pathname}${l.search}${l.hash}`

/** Dựng đối tượng vị trí từ chuỗi đường dẫn cho thẻ chưa từng hiện (khôi phục sau F5, thẻ mới). */
function toLocation(path: string): Location {
  const url = new URL(path, 'http://app.local')
  return { pathname: url.pathname, search: url.search, hash: url.hash, state: null, key: newId() }
}

function loadState(current: Location, companyId: string): TabsState {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (raw) {
      const saved = JSON.parse(raw) as { tabs: { id: string; path: string }[]; activeId: string; companyId?: string }
      // Thẻ lưu của công ty khác thì bỏ – thẻ chỉ thuộc công ty đang làm việc.
      const tabs = (saved.companyId === companyId ? saved.tabs : []).slice(0, MAX_SCREEN_TABS).map((t) => ({ id: t.id, location: toLocation(t.path), back: [] }))
      if (tabs.length > 0) {
        // Địa chỉ trên thanh trình duyệt luôn thắng: trùng thẻ nào thì hiện thẻ đó, không thì nạp vào thẻ đang hiện lúc trước.
        const same = tabs.find((t) => pathOf(t.location) === pathOf(current))
        const activeId = same?.id ?? (tabs.some((t) => t.id === saved.activeId) ? saved.activeId : tabs[0].id)
        return { tabs: tabs.map((t) => (t.id === activeId ? { ...t, location: current } : t)), activeId, companyId }
      }
    }
  } catch {
    // Trình duyệt chặn sessionStorage hoặc dữ liệu hỏng – bắt đầu với một thẻ.
  }
  const id = newId()
  return { tabs: [{ id, location: current, back: [] }], activeId: id, companyId }
}

function saveState(state: TabsState) {
  try {
    sessionStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        tabs: state.tabs.map((t) => ({ id: t.id, path: pathOf(t.location) })),
        activeId: state.activeId,
        companyId: state.companyId,
      }),
    )
  } catch {
    // Không lưu được thì F5 chỉ mở lại thẻ đang xem – không ảnh hưởng thao tác.
  }
}

/**
 * Quản lý tối đa 4 thẻ màn hình trong AppShell.
 *
 * Cách làm: thanh địa chỉ trình duyệt luôn là đường dẫn của **thẻ đang hiện**; mỗi thẻ tự vẽ trang của mình theo vị trí riêng
 * (`useRoutes(routes, tab.location)`), thẻ ẩn vẫn nằm trong trang nên giữ nguyên dữ liệu đang nhập, vị trí cuộn, tab đang chọn.
 * Trang trong thẻ điều hướng như cũ (`useNavigate`) – địa chỉ đổi thì thẻ đang hiện đi theo. Chuyển thẻ = thay địa chỉ bằng đường dẫn
 * của thẻ đích (replace, để nút Back của trình duyệt không lẫn trang giữa các thẻ); nút "Quay lại" dùng lịch sử riêng của thẻ.
 *
 * Các thẻ thuộc **công ty đang làm việc** (người dùng chốt 13/11/2026): mọi dữ liệu lọc theo công ty đang làm việc, thẻ ẩn mở dưới công ty
 * khác mà tải lại theo công ty mới thì dễ lưu nhầm – nên đổi công ty là đóng các thẻ khác, thẻ đang xem dựng lại dưới công ty mới.
 *
 * @param onFull Gọi khi muốn mở thẻ thứ 5 – thông báo cho người dùng.
 * @param companyId Công ty đang làm việc.
 * @returns Danh sách thẻ, thẻ đang hiện, các thao tác mở/chuyển/đóng/quay lại, giữ một thẻ, đặt nhãn.
 */
export function useScreenTabsState(onFull: () => void, companyId: string) {
  const location = useLocation()
  const navType = useNavigationType()
  const navigate = useNavigate()
  const [state, setState] = useState<TabsState>(() => loadState(location, companyId))
  // Bản mới nhất của danh sách thẻ cho các thao tác (gọi từ sự kiện) – không phải đăng ký lại hàm mỗi lần thẻ đổi.
  const stateRef = useRef(state)
  useLayoutEffect(() => {
    stateRef.current = state
  }, [state])
  // Đường dẫn đang chờ tới khi chuyển/mở thẻ – tới nơi chỉ cập nhật vị trí thẻ đích, không tính là điều hướng trong thẻ.
  const pending = useRef<string | null>(null)
  const backing = useRef(false)

  // useLayoutEffect: cập nhật vị trí thẻ trước khi trình duyệt vẽ, không thấy trang cũ nháy lại một khung hình.
  useLayoutEffect(() => {
    const path = pathOf(location)
    // Vị trí cũ tới muộn (REPLACE) trong lúc chờ thì bỏ qua; điều hướng mới (PUSH) trong thẻ thì không chờ nữa.
    if (pending.current !== null && (path === pending.current || navType !== 'PUSH')) {
      if (path !== pending.current) return
      pending.current = null
      setState((s) => ({ ...s, tabs: s.tabs.map((t) => (t.id === s.activeId ? { ...t, location } : t)) }))
      return
    }
    const isBack = backing.current
    backing.current = false
    setState((s) => ({
      ...s,
      tabs: s.tabs.map((t) => {
        if (t.id !== s.activeId) return t
        // Trang trước = trang thẻ đang hiện trước lần điều hướng này (lịch sử riêng của thẻ, không theo thanh địa chỉ).
        const prev = pathOf(t.location)
        const back = isBack ? t.back.slice(0, -1) : navType === 'PUSH' && prev !== path ? [...t.back, prev].slice(-MAX_BACK) : t.back
        return { ...t, location, back }
      }),
    }))
  }, [location, navType])

  useEffect(() => saveState(state), [state])

  // flushSync: địa chỉ và khung cập nhật ngay trong lần bấm. Không có thì bấm liên tiếp nhanh (mở thẻ rồi chuyển thẻ ngay), react-router
  // còn đang vẽ lần chuyển trước nên khung chậm nhận vị trí mới – nút "Quay lại", tên màn hình trên tab trình duyệt bị lệch.
  const go = useCallback(
    (path: string) => {
      pending.current = path
      void navigate(path, { replace: true, flushSync: true })
    },
    [navigate],
  )

  // Đổi công ty (từ ô chọn công ty hoặc link email tự đổi công ty): chỉ giữ thẻ đang xem, dựng lại (Id mới) để tải dữ liệu theo công ty mới.
  // Không điều hướng ở đây – trang mở link email đang tự chuyển tới phiếu cần mở. Cập nhật ngay trong lúc vẽ (không chờ effect) để thẻ
  // của công ty cũ không kịp vẽ lại dưới công ty mới.
  if (state.companyId !== companyId) {
    const active = state.tabs.find((t) => t.id === state.activeId) ?? state.tabs[0]
    const tab: ScreenTab = { id: newId(), location: active.location, back: [] }
    setState({ tabs: [tab], activeId: tab.id, companyId })
  }

  const switchTo = useCallback(
    (id: string) => {
      const s = stateRef.current
      const tab = s.tabs.find((t) => t.id === id)
      if (!tab || id === s.activeId) return
      setState({ ...s, activeId: id })
      go(pathOf(tab.location))
    },
    [go],
  )

  const open = useCallback(
    (path: string, mode: OpenMode = 'auto') => {
      const s = stateRef.current
      const active = s.tabs.find((t) => t.id === s.activeId)
      if (mode === 'current') {
        if (!active || pathOf(active.location) !== path) {
          pending.current = null
          void navigate(path, { flushSync: true })
        }
        return
      }
      const pathname = toLocation(path).pathname
      const same = s.tabs.find((t) => t.location.pathname === pathname)
      if (same) {
        if (same.id === s.activeId) {
          // Cùng màn hình nhưng khác tham số (?…) thì nạp lại đúng đường dẫn trong thẻ.
          if (pathOf(same.location) !== path) void navigate(path)
          return
        }
        setState({ ...s, activeId: same.id })
        go(path)
        return
      }
      if (s.tabs.length >= MAX_SCREEN_TABS) {
        onFull()
        return
      }
      const tab: ScreenTab = { id: newId(), location: toLocation(path), back: [] }
      setState({ ...s, tabs: [...s.tabs, tab], activeId: tab.id })
      go(path)
    },
    [go, navigate, onFull],
  )

  const close = useCallback(
    (id: string) => {
      const s = stateRef.current
      const index = s.tabs.findIndex((t) => t.id === id)
      if (index < 0) return
      if (s.tabs.length === 1) {
        // Đóng thẻ cuối cùng: còn lại trang chủ (thẻ mới, trang cũ bỏ hẳn).
        const home: ScreenTab = { id: newId(), location: toLocation('/'), back: [] }
        setState({ ...s, tabs: [home], activeId: home.id })
        go('/')
        return
      }
      const tabs = s.tabs.filter((t) => t.id !== id)
      if (id !== s.activeId) {
        setState({ ...s, tabs })
        return
      }
      // Đóng thẻ đang hiện: hiện thẻ bên phải (hết thì bên trái) – như thanh thẻ của trình duyệt.
      const next = tabs[Math.min(index, tabs.length - 1)]
      setState({ ...s, tabs, activeId: next.id })
      go(pathOf(next.location))
    },
    [go],
  )

  const back = useCallback(() => {
    const s = stateRef.current
    const tab = s.tabs.find((t) => t.id === s.activeId)
    const prev = tab?.back.at(-1)
    if (!prev) return
    backing.current = true
    pending.current = null
    void navigate(prev, { replace: true, flushSync: true })
  }, [navigate])

  /**
   * Đóng mọi thẻ, chỉ còn một thẻ mở đường dẫn cho trước (dùng trước khi đổi công ty).
   * @param path Đường dẫn của thẻ còn lại.
   */
  const keepOnly = useCallback(
    (path: string) => {
      const s = stateRef.current
      const tab: ScreenTab = { id: newId(), location: toLocation(path), back: [] }
      setState({ ...s, tabs: [tab], activeId: tab.id })
      go(path)
    },
    [go],
  )

  const setLabel = useCallback((id: string, label: string | undefined) => {
    setState((s) => {
      const tab = s.tabs.find((t) => t.id === id)
      if (!tab || tab.label === label) return s
      return { ...s, tabs: s.tabs.map((t) => (t.id === id ? { ...t, label } : t)) }
    })
  }, [])

  const api = useMemo<ScreenTabsApi>(() => ({ open, back }), [open, back])
  const activeTab = state.tabs.find((t) => t.id === state.activeId) ?? state.tabs[0]

  /** Khung đang tự chuyển/mở/đóng thẻ (chờ địa chỉ tới đích) – bộ chặn "phiếu chưa lưu" không chặn lượt này. */
  const isTabNavigating = useCallback(() => pending.current !== null, [])

  return { tabs: state.tabs, activeTab, api, switchTo, close, keepOnly, setLabel, isTabNavigating }
}
