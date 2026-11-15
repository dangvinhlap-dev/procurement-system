import { ApiError, apiPostQuiet } from '../api/client'

/**
 * Gửi lỗi giao diện về nhật ký lỗi của máy chủ (màn hình ERR – lát QT2, `POST /api/ops/v1/client-errors`).
 *
 * Chống gửi trùng: cùng một lỗi (tên + nội dung + dòng đầu ngăn xếp) trong 60 giây chỉ gửi một lần, lần sau dùng lại mã theo dõi cũ; mỗi lần mở
 * trang gửi tối đa 20 lỗi (trang lỗi lặp vô hạn không làm đầy nhật ký – máy chủ còn giới hạn 30 lần/phút/phiên). Không gửi:
 * - `ApiError`: lỗi do máy chủ trả về – lỗi 500 máy chủ đã tự ghi, lỗi 4xx là lỗi nghiệp vụ người dùng đã thấy câu báo;
 * - lỗi vô hại của trình duyệt ("ResizeObserver loop…"), "Script error." (script khác nguồn, không có thông tin).
 */

/** Loại báo lỗi – khớp ClientErrorRequest.Kind của máy chủ. */
export type ClientErrorKind = 'render' | 'error' | 'rejection' | 'manual'

/** Cỡ tối đa mỗi trường gửi lên (máy chủ nhận ≤ 8 KB cả gói). */
const FIELD_LIMIT = { message: 1000, stack: 4000, componentStack: 2000, url: 500 }

const DEDUPE_MS = 60_000
const MAX_PER_PAGE = 20

const recent = new Map<string, { at: number; code: Promise<string | null> }>()
let sent = 0

/** Lỗi không cần gửi. */
function ignored(error: unknown, message: string): boolean {
  if (error instanceof ApiError) return true
  return /ResizeObserver loop|^Script error\.?$/i.test(message)
}

/** Cắt chữ theo giới hạn. */
function cut(value: string | null | undefined, max: number): string | null {
  if (!value) return null
  return value.length > max ? value.slice(0, max) : value
}

/**
 * Gửi một lỗi giao diện.
 * @param error Lỗi bắt được (Error hoặc giá trị bất kỳ bị throw / reject).
 * @param kind Nguồn bắt lỗi.
 * @param componentStack Cây component React (ErrorBoundary).
 * @returns Mã theo dõi (ERR-xxxxxxxx), hoặc null khi không gửi / gửi hỏng.
 */
export function reportClientError(error: unknown, kind: ClientErrorKind, componentStack?: string | null): Promise<string | null> {
  const name = error instanceof Error ? error.name : 'Error'
  const message = error instanceof Error ? error.message : typeof error === 'string' ? error : JSON.stringify(error) ?? String(error)
  if (ignored(error, message)) return Promise.resolve(null)
  const stack = error instanceof Error ? (error.stack ?? null) : null
  const key = `${name}|${message}|${stack?.split('\n')[1]?.trim() ?? ''}`
  const now = Date.now()
  const previous = recent.get(key)
  if (previous && now - previous.at < DEDUPE_MS) return previous.code
  if (sent >= MAX_PER_PAGE) return Promise.resolve(null)
  sent++
  const code = apiPostQuiet<{ trackingCode: string }>('/api/ops/v1/client-errors', {
    kind,
    name,
    message: cut(message, FIELD_LIMIT.message) ?? '(không có nội dung)',
    stack: cut(stack, FIELD_LIMIT.stack),
    componentStack: cut(componentStack, FIELD_LIMIT.componentStack),
    url: cut(window.location.href, FIELD_LIMIT.url),
  }).then((r) => r?.trackingCode ?? null)
  recent.set(key, { at: now, code })
  return code
}

let installed = false

/**
 * Bắt lỗi JavaScript không ai bắt (`window.onerror`) và promise bị từ chối không ai xử lý (`unhandledrejection`), gửi về nhật ký lỗi. Gọi một
 * lần khi khởi động (main.tsx).
 */
export function installClientErrorReporting(): void {
  if (installed) return
  installed = true
  window.addEventListener('error', (e) => void reportClientError(e.error ?? e.message, 'error'))
  window.addEventListener('unhandledrejection', (e) => void reportClientError(e.reason, 'rejection'))
}
