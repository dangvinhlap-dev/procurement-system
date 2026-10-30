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

/**
 * Gắn token và công ty làm việc vào header, giống apiFetch.
 */
function authHeaders(): Headers {
  const headers = new Headers()
  const token = bridge.getAccessToken()
  const companyId = bridge.getCompanyId()
  if (token) headers.set('Authorization', `Bearer ${token}`)
  if (companyId) headers.set('X-Company-Id', companyId)
  return headers
}

/**
 * Tải file từ API (ví dụ Excel) và mở hộp lưu file của trình duyệt. Dùng fetch thay cho link trực tiếp vì API cần
 * header Authorization và X-Company-Id.
 * @param path Đường dẫn API.
 * @param fallbackName Tên file nếu server không gửi Content-Disposition.
 * @throws ApiError khi server trả lỗi hoặc không kết nối được.
 */
export async function apiDownload(path: string, fallbackName: string): Promise<void> {
  let response: Response
  try {
    response = await fetch(path, { headers: authHeaders() })
  } catch {
    throw new ApiError(0, 'Không kết nối được máy chủ. Kiểm tra mạng hoặc báo quản trị hệ thống.')
  }
  if (!response.ok) throw new ApiError(response.status, await readErrorMessage(response))

  // Tên file tiếng Việt nằm ở filename*=UTF-8''… (RFC 5987).
  const disposition = response.headers.get('Content-Disposition') ?? ''
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1]
  const plain = /filename="?([^";]+)"?/i.exec(disposition)?.[1]
  const name = encoded ? decodeURIComponent(encoded) : (plain ?? fallbackName)

  const url = URL.createObjectURL(await response.blob())
  const a = Object.assign(document.createElement('a'), { href: url, download: name })
  a.click()
  URL.revokeObjectURL(url)
}

/**
 * Gửi file lên API dạng multipart/form-data (trường "file").
 * @param path Đường dẫn API.
 * @param file File người dùng chọn.
 * @returns Dữ liệu JSON server trả về.
 * @throws ApiError khi server trả lỗi hoặc không kết nối được.
 */
export async function apiUpload<T>(path: string, file: File): Promise<T> {
  const form = new FormData()
  form.append('file', file)
  let response: Response
  try {
    response = await fetch(path, { method: 'POST', headers: authHeaders(), body: form })
  } catch {
    throw new ApiError(0, 'Không kết nối được máy chủ. Kiểm tra mạng hoặc báo quản trị hệ thống.')
  }
  if (!response.ok) {
    if (response.status === 401) bridge.onUnauthorized()
    throw new ApiError(response.status, await readErrorMessage(response))
  }
  return (await response.json()) as T
}
