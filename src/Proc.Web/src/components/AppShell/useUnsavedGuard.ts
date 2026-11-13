import { App } from 'antd'
import { useCallback, useEffect, useRef } from 'react'
import { useBlocker } from 'react-router'

/** Câu hỏi khi rời trang trong thẻ (bấm cây điều hướng, Quay lại, nút Đóng, nút Back của trình duyệt). */
const LEAVE_DIALOG = {
  title: 'Rời trang khi phiếu chưa lưu',
  content: 'Phiếu đang sửa có thay đổi chưa lưu. Rời trang sẽ mất các thay đổi này.',
  okText: 'Rời trang',
  cancelText: 'Ở lại',
  okButtonProps: { danger: true },
} as const

interface UnsavedGuardOptions {
  /** Id thẻ đang xem. */
  activeId: string
  /** Khung đang tự chuyển/mở/đóng thẻ (không phải người dùng rời trang trong thẻ) – không chặn. */
  isTabNavigating: () => boolean
  /** Tên thẻ để nêu trong câu hỏi. */
  tabName: (id: string) => string
}

/**
 * Hỏi lại khi rời trang còn phiếu chưa lưu (yêu cầu người dùng 13/11/2026). Trang báo thay đổi chưa lưu qua `useUnsavedCheck`
 * (DocumentForm, DetailDrawer – dấu theo `utils/formDirty.ts`); khung hỏi ở mọi lối ra:
 * - rời trang trong thẻ đang xem: chặn bằng `useBlocker` (một bộ chặn cho cả khung – router chỉ nhận một), chỉ khi đổi đường dẫn
 *   (đổi `?…` như mở ngăn kéo phụ lục vẫn ở trang cũ); chuyển thẻ không chặn vì trang cũ vẫn còn trong thẻ;
 * - đóng thẻ, đổi công ty, đăng xuất: `confirmDiscard` trước khi làm;
 * - tải lại / đóng tab trình duyệt: `beforeunload` (trình duyệt tự hiện câu hỏi của nó, không đổi chữ được).
 *
 * @returns `register` (đăng ký kiểm tra theo thẻ), `dirtyTabs`, `confirmDiscard`.
 */
export function useUnsavedGuard({ activeId, isTabNavigating, tabName }: UnsavedGuardOptions) {
  const { modal } = App.useApp()
  const checks = useRef(new Map<string, Set<() => boolean>>())
  // Đã đồng ý bỏ thay đổi (ví dụ đăng xuất) – không hỏi lại ở bộ chặn điều hướng.
  const bypass = useRef(false)
  const activeIdRef = useRef(activeId)
  useEffect(() => {
    activeIdRef.current = activeId
  }, [activeId])

  const register = useCallback((tabId: string, check: () => boolean) => {
    const set = checks.current.get(tabId) ?? new Set<() => boolean>()
    set.add(check)
    checks.current.set(tabId, set)
    return () => {
      set.delete(check)
      if (set.size === 0) checks.current.delete(tabId)
    }
  }, [])

  const isTabDirty = useCallback((tabId: string) => [...(checks.current.get(tabId) ?? [])].some((check) => check()), [])

  /**
   * Các thẻ còn thay đổi chưa lưu.
   * @param tabIds Thẻ cần xét; bỏ trống = mọi thẻ.
   */
  const dirtyTabs = useCallback(
    (tabIds?: string[]) => (tabIds ?? [...checks.current.keys()]).filter(isTabDirty),
    [isTabDirty],
  )

  /**
   * Còn thẻ chưa lưu thì hỏi "Bỏ thay đổi / Ở lại" rồi mới làm; không còn thì làm ngay.
   * @param tabIds Thẻ sẽ bị đóng/mất dữ liệu.
   * @param action Việc cần làm khi đồng ý.
   * @param leaving true khi rời hẳn khung (đăng xuất) – bộ chặn điều hướng không hỏi lại.
   */
  const confirmDiscard = useCallback(
    (tabIds: string[], action: () => void, leaving = false) => {
      const dirty = dirtyTabs(tabIds)
      const run = () => {
        if (leaving) bypass.current = true
        action()
      }
      if (dirty.length === 0) {
        run()
        return
      }
      modal.confirm({
        title: 'Còn phiếu chưa lưu',
        content: `Có thay đổi chưa lưu ở: ${dirty.map(tabName).join('; ')}. Tiếp tục sẽ mất các thay đổi này.`,
        okText: 'Bỏ thay đổi',
        cancelText: 'Ở lại',
        okButtonProps: { danger: true },
        onOk: run,
      })
    },
    [dirtyTabs, modal, tabName],
  )

  const blocker = useBlocker(
    useCallback(
      ({ currentLocation, nextLocation }: { currentLocation: { pathname: string }; nextLocation: { pathname: string } }) =>
        !bypass.current &&
        !isTabNavigating() &&
        currentLocation.pathname !== nextLocation.pathname &&
        isTabDirty(activeIdRef.current),
      [isTabNavigating, isTabDirty],
    ),
  )
  useEffect(() => {
    if (blocker.state !== 'blocked') return
    modal.confirm({ ...LEAVE_DIALOG, onOk: () => blocker.proceed(), onCancel: () => blocker.reset() })
  }, [blocker, modal])

  useEffect(() => {
    const onBeforeUnload = (e: BeforeUnloadEvent) => {
      if (bypass.current || dirtyTabs().length === 0) return
      e.preventDefault()
      // Trình duyệt cũ cần gán returnValue mới hiện câu hỏi.
      e.returnValue = ''
    }
    window.addEventListener('beforeunload', onBeforeUnload)
    return () => window.removeEventListener('beforeunload', onBeforeUnload)
  }, [dirtyTabs])

  return { register, dirtyTabs, confirmDiscard }
}
