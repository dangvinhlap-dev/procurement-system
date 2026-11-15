import { App } from 'antd'
import { useCallback, useEffect, useRef } from 'react'
import { useBlocker } from 'react-router'

/** Câu hỏi khi rời trang (bấm cây điều hướng, menu, ô lệnh, Quay lại, nút Đóng, nút Back của trình duyệt). */
const LEAVE_DIALOG = {
  title: 'Rời trang khi phiếu chưa lưu',
  content: 'Phiếu đang sửa có thay đổi chưa lưu. Rời trang sẽ mất các thay đổi này.',
  okText: 'Rời trang',
  cancelText: 'Ở lại',
  okButtonProps: { danger: true },
} as const

/**
 * Hỏi lại khi rời trang còn phiếu chưa lưu (yêu cầu người dùng 13/11/2026). Trang báo thay đổi chưa lưu qua `useUnsavedCheck`
 * (DocumentForm, DetailDrawer – dấu theo `utils/formDirty.ts`); khung hỏi ở mọi lối ra:
 * - rời trang: chặn bằng `useBlocker` (một bộ chặn cho cả khung – router chỉ nhận một), chỉ khi đổi đường dẫn (đổi `?…` như mở ngăn kéo
 *   phụ lục vẫn ở trang cũ);
 * - đổi công ty, đăng xuất: `confirmDiscard` trước khi làm;
 * - tải lại / đóng tab trình duyệt: `beforeunload` (trình duyệt tự hiện câu hỏi của nó, không đổi chữ được).
 *
 * @returns `register` (đăng ký kiểm tra của trang), `isDirty`, `confirmDiscard`.
 */
export function useUnsavedGuard() {
  const { modal } = App.useApp()
  const checks = useRef(new Set<() => boolean>())
  // Đã đồng ý bỏ thay đổi (ví dụ đăng xuất) – không hỏi lại ở bộ chặn điều hướng.
  const bypass = useRef(false)

  const register = useCallback((check: () => boolean) => {
    checks.current.add(check)
    return () => {
      checks.current.delete(check)
    }
  }, [])

  /** Trang đang mở còn thay đổi chưa lưu. */
  const isDirty = useCallback(() => [...checks.current].some((check) => check()), [])

  /**
   * Còn thay đổi chưa lưu thì hỏi "Bỏ thay đổi / Ở lại" rồi mới làm; không còn thì làm ngay.
   * @param action Việc cần làm khi đồng ý.
   * @param leaving true khi rời hẳn khung (đăng xuất) – bộ chặn điều hướng không hỏi lại.
   */
  const confirmDiscard = useCallback(
    (action: () => void, leaving = false) => {
      const run = () => {
        if (leaving) bypass.current = true
        action()
      }
      if (!isDirty()) {
        run()
        return
      }
      modal.confirm({
        title: 'Còn phiếu chưa lưu',
        content: 'Phiếu đang sửa có thay đổi chưa lưu. Tiếp tục sẽ mất các thay đổi này.',
        okText: 'Bỏ thay đổi',
        cancelText: 'Ở lại',
        okButtonProps: { danger: true },
        onOk: run,
      })
    },
    [isDirty, modal],
  )

  const blocker = useBlocker(
    useCallback(
      ({ currentLocation, nextLocation }: { currentLocation: { pathname: string }; nextLocation: { pathname: string } }) =>
        !bypass.current && currentLocation.pathname !== nextLocation.pathname && isDirty(),
      [isDirty],
    ),
  )
  useEffect(() => {
    if (blocker.state !== 'blocked') return
    modal.confirm({ ...LEAVE_DIALOG, onOk: () => blocker.proceed(), onCancel: () => blocker.reset() })
  }, [blocker, modal])

  useEffect(() => {
    const onBeforeUnload = (e: BeforeUnloadEvent) => {
      if (bypass.current || !isDirty()) return
      e.preventDefault()
      // Trình duyệt cũ cần gán returnValue mới hiện câu hỏi.
      e.returnValue = ''
    }
    window.addEventListener('beforeunload', onBeforeUnload)
    return () => window.removeEventListener('beforeunload', onBeforeUnload)
  }, [isDirty])

  return { register, isDirty, confirmDiscard }
}
