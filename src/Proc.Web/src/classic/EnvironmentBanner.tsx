import { useEffect } from 'react'
import { useEnvironmentStatus, type AppEnvironment } from '../api/operations'

/** Chữ đầu tiêu đề tab trình duyệt trên máy TEST (design.md mục 21.3). */
const TEST_TITLE_PREFIX = '[TEST] '

/** Chữ của dải theo môi trường; PROD không có dải (trừ khi có cảnh báo). */
const labels: Record<AppEnvironment, string | null> = {
  Dev: 'DEV – máy lập trình, dữ liệu mẫu',
  Test: 'MÔI TRƯỜNG TEST – dữ liệu thử nghiệm, email chuyển về hộp thư test',
  Prod: null,
  None: 'CHƯA KHAI MÔI TRƯỜNG MÁY CHỦ',
}

/** Tên ngắn đứng trước cảnh báo cấu hình. */
const shortLabels: Record<AppEnvironment, string | null> = { Dev: 'DEV', Test: 'MÔI TRƯỜNG TEST', Prod: null, None: null }

/**
 * Giữ "[TEST] " ở đầu tiêu đề tab trình duyệt khi máy là TEST – kể cả khi trang đổi tiêu đề sau đó (AppShell đặt lại theo màn hình).
 * @param active Đang là máy TEST.
 */
function useTestTitle(active: boolean) {
  useEffect(() => {
    if (!active) return
    const apply = () => {
      if (!document.title.startsWith(TEST_TITLE_PREFIX)) document.title = TEST_TITLE_PREFIX + document.title
    }
    apply()
    const title = document.querySelector('title')
    const observer = new MutationObserver(apply)
    if (title) observer.observe(title, { childList: true, characterData: true, subtree: true })
    return () => {
      observer.disconnect()
      if (document.title.startsWith(TEST_TITLE_PREFIX)) document.title = document.title.slice(TEST_TITLE_PREFIX.length)
    }
  }, [active])
}

/**
 * Dải nhận diện môi trường (design.md mục 21.3, lát QT1 – ui-style.md mục 5j): xám "DEV", cam "MÔI TRƯỜNG TEST", PROD không dải. Máy thiếu
 * cấu hình (chưa khai App:Environment, TEST chưa khai hộp thư test – người dùng chốt 14/11/2026) thì dải đỏ kèm cảnh báo, email không gửi.
 * Đặt đầu AppShell, trang đăng nhập, trang điện thoại `/duyet`, Portal NCC.
 * @param endpoint API môi trường – Portal NCC dùng `/api/portal/auth/environment`.
 * @param floating Dải nổi trên đầu trang (trang đăng nhập canh giữa màn hình).
 */
export function EnvironmentBanner({ endpoint, floating = false }: { endpoint?: string; floating?: boolean }) {
  const status = useEnvironmentStatus(endpoint).data
  useTestTitle(status?.environment === 'Test')
  if (!status) return null
  const label = labels[status.environment]
  if (!label && !status.warning) return null
  const tone = status.warning ? 'warning' : status.environment.toLowerCase()
  const text = status.warning ? [shortLabels[status.environment], status.warning].filter(Boolean).join(' – ') : (label ?? '')
  return (
    <div
      className={`erp-env-banner is-${tone}${floating ? ' is-floating' : ''}`}
      role={status.warning ? 'alert' : 'status'}
      data-testid="environment-banner"
      title={text}
    >
      {text}
    </div>
  )
}
