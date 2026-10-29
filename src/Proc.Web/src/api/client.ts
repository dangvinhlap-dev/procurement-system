/** Lỗi khi gọi API, mang thông điệp tiếng Việt từ ProblemDetails của server. */
export class ApiError extends Error {
  readonly status: number

  /**
   * @param status Mã HTTP.
   * @param message Thông điệp cho người dùng.
   */
  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

/** Nguồn token và công ty làm việc – AuthProvider gắn vào khi khởi động. */
interface AuthBridge {
  getAccessToken: () => string | null
  getCompanyId: () => string | null
  onUnauthorized: () => void
}

let bridge: AuthBridge = { getAccessToken: () => null, getCompanyId: () => null, onUnauthorized: () => {} }

/**
 * Gắn nguồn token cho apiFetch. Tách ra để apiFetch không phụ thuộc React context.
 * @param value Các hàm lấy token, công ty và xử lý khi hết phiên.
 */
export function setAuthBridge(value: AuthBridge): void {
  bridge = value
}

/**
 * Đọc thông điệp lỗi từ ProblemDetails (title) hoặc ValidationProblemDetails (errors).
 */
async function readErrorMessage(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { title?: string; errors?: Record<string, string[]> }
    const firstFieldError = body.errors ? Object.values(body.errors).flat()[0] : undefined
    return firstFieldError ?? body.title ?? `Lỗi HTTP ${response.status}.`
  } catch {
    return response.status >= 500 ? 'Máy chủ đang lỗi hoặc chưa chạy.' : `Lỗi HTTP ${response.status}.`
  }
}

/**
 * Gọi API: tự gắn JWT (Authorization: Bearer) và công ty đang làm việc (X-Company-Id – design.md mục 9.2).
 * Nhận 401 ở API cần đăng nhập thì báo hết phiên để quay về màn hình đăng nhập.
 * @param path Đường dẫn, ví dụ "/api/auth/me".
 * @param init Tùy chọn fetch; body là object sẽ được chuyển thành JSON.
 * @returns Dữ liệu JSON đã parse.
 * @throws ApiError khi server trả lỗi hoặc không kết nối được.
 */
export async function apiFetch<T>(path: string, init: Omit<RequestInit, 'body'> & { body?: unknown } = {}): Promise<T> {
  const headers = new Headers(init.headers)
  const token = bridge.getAccessToken()
  const companyId = bridge.getCompanyId()
  if (token) headers.set('Authorization', `Bearer ${token}`)
  if (companyId) headers.set('X-Company-Id', companyId)
  if (init.body !== undefined) headers.set('Content-Type', 'application/json')

  let response: Response
  try {
    response = await fetch(path, { ...init, headers, body: init.body === undefined ? undefined : JSON.stringify(init.body) })
  } catch {
    throw new ApiError(0, 'Không kết nối được máy chủ. Kiểm tra mạng hoặc báo quản trị hệ thống.')
  }

  if (!response.ok) {
    const message = await readErrorMessage(response)
    if (response.status === 401 && token) bridge.onUnauthorized()
    throw new ApiError(response.status, message)
  }
  return (response.status === 204 ? undefined : await response.json()) as T
}
