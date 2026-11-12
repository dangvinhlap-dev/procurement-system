import type { PortalInviteInput, PortalInviteResult, SupplierPortalUser } from '../api/supplierPortal'

/*
 * Gọi API Portal NCC (/api/portal – design.md 19.5, lát P1). Token Portal lưu riêng (khóa `proc.portal.session`), không dùng chung phiên và
 * hàm apiFetch của người dùng nội bộ: token NCC không bao giờ được gửi tới API nội bộ và ngược lại.
 */

/** Khóa lưu phiên Portal trong sessionStorage (mất khi đóng tab – máy dùng chung không giữ đăng nhập). */
const PORTAL_SESSION_KEY = 'proc.portal.session'

/** NCC của người dùng Portal. */
export interface PortalSupplier {
  id: string
  code: string
  name: string
  taxCode: string | null
  address: string | null
  phone: string | null
  email: string | null
  status: 'Potential' | 'Approved' | 'Suspended' | 'Blacklisted'
}

/** Người dùng Portal đang đăng nhập. */
export interface PortalMe {
  userId: string
  email: string
  fullName: string
  phone: string | null
  position: string | null
  isSupplierAdmin: boolean
  supplier: PortalSupplier
}

/** Phiên Portal: token + người dùng. */
export interface PortalSessionData {
  accessToken: string
  expiresAt: string
  me: PortalMe
}

/** Bước 2 đăng nhập: chờ nhập OTP. */
export interface PortalLoginChallenge {
  challengeId: string
  maskedEmail: string
  expiresAt: string
  /** Mã OTP – chỉ có ở máy dev / test tự động. */
  devCode: string | null
}

/** Thông tin link mời. */
export interface PortalInviteInfo {
  email: string
  fullName: string
  supplierName: string
}

/** Lỗi gọi API Portal, kèm mã HTTP. */
export class PortalApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

/**
 * Đọc phiên Portal đã lưu.
 * @returns Phiên còn hạn, hoặc null.
 */
export function loadPortalSession(): PortalSessionData | null {
  try {
    const raw = sessionStorage.getItem(PORTAL_SESSION_KEY)
    if (!raw) return null
    const s = JSON.parse(raw) as PortalSessionData
    return new Date(s.expiresAt).getTime() > Date.now() ? s : null
  } catch {
    return null
  }
}

/**
 * Lưu hoặc xóa phiên Portal.
 * @param session Phiên; null để xóa.
 */
export function savePortalSession(session: PortalSessionData | null): void {
  try {
    if (session) sessionStorage.setItem(PORTAL_SESSION_KEY, JSON.stringify(session))
    else sessionStorage.removeItem(PORTAL_SESSION_KEY)
  } catch {
    // Trình duyệt chặn lưu trữ: phiên chỉ sống trong bộ nhớ.
  }
}

/** Hàm gọi khi token Portal hết hiệu lực (PortalSessionProvider gắn vào để đăng xuất). */
let onPortalUnauthorized: () => void = () => {}

/**
 * Gắn hàm xử lý khi máy chủ trả 401 cho request có token Portal.
 * @param handler Hàm xử lý.
 */
export function setPortalUnauthorizedHandler(handler: () => void): void {
  onPortalUnauthorized = handler
}

/**
 * Gọi API Portal; tự gắn token Portal đang lưu.
 * @param path Đường dẫn bắt đầu bằng /api/portal.
 * @param init Phương thức và thân (đối tượng, tự chuyển JSON).
 * @returns Dữ liệu JSON (undefined khi 204).
 * @throws PortalApiError khi máy chủ báo lỗi hoặc không kết nối được.
 */
export async function portalFetch<T>(path: string, init: { method?: string; body?: unknown } = {}): Promise<T> {
  const headers = new Headers()
  const token = loadPortalSession()?.accessToken
  if (token) headers.set('Authorization', `Bearer ${token}`)
  if (init.body !== undefined) headers.set('Content-Type', 'application/json')
  let response: Response
  try {
    response = await fetch(path, {
      method: init.method ?? 'GET',
      headers,
      body: init.body === undefined ? undefined : JSON.stringify(init.body),
    })
  } catch {
    throw new PortalApiError(0, 'Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.')
  }
  if (!response.ok) {
    let message = `Lỗi HTTP ${response.status}.`
    try {
      const body = (await response.json()) as { title?: string; errors?: Record<string, string[]> }
      message = (body.errors ? Object.values(body.errors).flat()[0] : undefined) ?? body.title ?? message
    } catch {
      if (response.status >= 500) message = 'Máy chủ đang lỗi hoặc chưa chạy.'
    }
    if (response.status === 401 && token) onPortalUnauthorized()
    throw new PortalApiError(response.status, message)
  }
  return (response.status === 204 ? undefined : await response.json()) as T
}

/** Trạng thái thư mời NCC thấy (lát P3a). */
export type PortalRfqStatus = 'Open' | 'Closed' | 'Reviewing' | 'Awarded' | 'Cancelled'

/** Nhãn tiếng Việt của trạng thái thư mời. */
export const portalRfqStatusLabels: Record<PortalRfqStatus, string> = {
  Open: 'Đang nhận báo giá',
  Closed: 'Hết hạn nộp – chờ mở',
  Reviewing: 'Đang xét chọn',
  Awarded: 'Đã có kết quả',
  Cancelled: 'Đã hủy',
}

/** Một thư mời báo giá trong danh sách Portal. deadlineAt là giờ Việt Nam (không kèm múi giờ). */
export interface PortalRfqListItem {
  id: string
  no: string
  companyName: string
  title: string
  date: string
  deadlineAt: string
  status: PortalRfqStatus
  lineCount: number
  submissionCount: number
  lastSubmittedAt: string | null
}

/** Dòng hàng của RFQ trên Portal. */
export interface PortalRfqLine {
  id: string
  lineNo: number
  itemCode: string
  itemName: string
  supplierItemCode: string | null
  uomCode: string
  qty: number
  note: string | null
}

/** Giá một dòng NCC đã nộp. */
export interface PortalQuoteLine {
  rfqLineId: string
  unitPrice: number
  discountPct: number
  vatCode: string | null
  leadTimeDays: number | null
  note: string | null
  amount: number
}

/** Biên nhận một lần nộp. */
export interface PortalSubmission {
  sequence: number
  submittedAt: string
  submittedByEmail: string
  contentHash: string
  totalAmount: number
}

/** Chi tiết thư mời kèm bản nộp cuối và lịch sử nộp. */
export interface PortalRfq {
  id: string
  no: string
  companyName: string
  title: string
  date: string
  deadlineAt: string
  note: string | null
  status: PortalRfqStatus
  canSubmit: boolean
  closedReason: string | null
  vatCodes: string[]
  lines: PortalRfqLine[]
  latest: {
    sequence: number
    submittedAt: string
    validUntil: string | null
    paymentTerms: string | null
    note: string | null
    contentHash: string
    lines: PortalQuoteLine[]
  } | null
  submissions: PortalSubmission[]
  /** Dung lượng tối đa một file báo giá (MB). */
  maxFileSizeMb: number
  /** Còn gửi câu hỏi làm rõ được (trước giờ hạn, chưa mở – lát P3b-2). */
  canAsk: boolean
  /** Câu hỏi – trả lời của chính NCC (riêng) và thông báo làm rõ công khai của bên mời, cũ trước. */
  clarifications: PortalClarification[]
}

/** Một mục hỏi đáp NCC thấy (lát P3b-2): câu hỏi của mình hoặc thông báo làm rõ của bên mời. */
export interface PortalClarification {
  id: string
  isNotice: boolean
  question: string | null
  askedAt: string | null
  askedByEmail: string | null
  /** Câu trả lời / nội dung thông báo; null = chưa trả lời. */
  answer: string | null
  answeredAt: string | null
}

/** File báo giá NCC đã tải lên (lát P3b-1). */
export interface PortalAttachment {
  id: string
  fileName: string
  sizeBytes: number
  note: string | null
  uploadedAt: string
  uploadedByName: string | null
  canDelete: boolean
  lockReason: string | null
}

/**
 * Gửi request Portal có token, trả Response đã kiểm lỗi (dùng cho tải file lên / xuống – thân không phải JSON).
 * @param path Đường dẫn.
 * @param init Phương thức và thân.
 * @returns Response thành công.
 */
async function portalRaw(path: string, init: { method?: string; body?: BodyInit } = {}): Promise<Response> {
  const headers = new Headers()
  const token = loadPortalSession()?.accessToken
  if (token) headers.set('Authorization', `Bearer ${token}`)
  let response: Response
  try {
    response = await fetch(path, { method: init.method ?? 'GET', headers, body: init.body })
  } catch {
    throw new PortalApiError(0, 'Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.')
  }
  if (!response.ok) {
    let message = `Lỗi HTTP ${response.status}.`
    try {
      const body = (await response.json()) as { title?: string }
      message = body.title ?? message
    } catch {
      // Thân không phải JSON – giữ câu mặc định.
    }
    if (response.status === 401 && token) onPortalUnauthorized()
    throw new PortalApiError(response.status, message)
  }
  return response
}

/** Báo giá NCC gửi; dòng không gửi = không chào dòng đó. */
export interface PortalSubmitQuote {
  validUntil: string | null
  paymentTerms: string | null
  note: string | null
  lines: { rfqLineId: string; unitPrice: number; discountPct: number; vatCode: string | null; leadTimeDays: number | null; note: string | null }[]
}

/** Các lệnh API Portal. */
export const portalApi = {
  /** Đăng nhập bước 1: email + mật khẩu → gửi OTP. */
  login: (email: string, password: string) =>
    portalFetch<PortalLoginChallenge>('/api/portal/auth/login', { method: 'POST', body: { email, password } }),
  /** Đăng nhập bước 2: OTP → token. */
  verifyOtp: (challengeId: string, code: string) =>
    portalFetch<PortalSessionData>('/api/portal/auth/verify-otp', { method: 'POST', body: { challengeId, code } }),
  /** Thông tin link mời. */
  invite: (token: string) => portalFetch<PortalInviteInfo>(`/api/portal/auth/invite?token=${encodeURIComponent(token)}`),
  /** Kích hoạt: đặt mật khẩu lần đầu. */
  activate: (token: string, password: string) =>
    portalFetch<void>('/api/portal/auth/activate', { method: 'POST', body: { token, password } }),
  /** Quên mật khẩu: gửi link đặt lại. */
  forgotPassword: (email: string) =>
    portalFetch<{ devToken: string | null }>('/api/portal/auth/forgot-password', { method: 'POST', body: { email } }),
  /** Đặt lại mật khẩu từ link. */
  resetPassword: (token: string, password: string) =>
    portalFetch<void>('/api/portal/auth/reset-password', { method: 'POST', body: { token, password } }),
  /** Người dùng đang đăng nhập. */
  me: () => portalFetch<PortalMe>('/api/portal/me'),
  /** Đổi mật khẩu – trả token mới. */
  changePassword: (currentPassword: string, newPassword: string) =>
    portalFetch<PortalSessionData>('/api/portal/change-password', { method: 'POST', body: { currentPassword, newPassword } }),
  /** Người dùng của NCC mình (quản trị NCC). */
  users: () => portalFetch<SupplierPortalUser[]>('/api/portal/users'),
  /** Mời người dùng con. */
  inviteUser: (input: PortalInviteInput) => portalFetch<PortalInviteResult>('/api/portal/users', { method: 'POST', body: input }),
  /** Gửi lại lời mời. */
  resendInvite: (id: string) => portalFetch<PortalInviteResult>(`/api/portal/users/${id}/resend-invite`, { method: 'POST' }),
  /** Khóa người dùng con. */
  disableUser: (id: string) => portalFetch<SupplierPortalUser>(`/api/portal/users/${id}/disable`, { method: 'POST' }),
  /** Mở khóa người dùng con. */
  enableUser: (id: string) => portalFetch<SupplierPortalUser>(`/api/portal/users/${id}/enable`, { method: 'POST' }),
  /** Thư mời báo giá của NCC (lát P3a). */
  rfqs: () => portalFetch<PortalRfqListItem[]>('/api/portal/rfqs'),
  /** Chi tiết thư mời. */
  rfq: (id: string) => portalFetch<PortalRfq>(`/api/portal/rfqs/${id}`),
  /** Nộp / nộp lại báo giá – niêm phong đến giờ mở. */
  submitQuote: (id: string, input: PortalSubmitQuote) =>
    portalFetch<PortalRfq>(`/api/portal/rfqs/${id}/quotes`, { method: 'POST', body: input }),
  /** Gửi câu hỏi làm rõ (lát P3b-2) – chỉ NCC mình thấy câu trả lời. */
  ask: (id: string, question: string) =>
    portalFetch<PortalRfq>(`/api/portal/rfqs/${id}/questions`, { method: 'POST', body: { question } }),
  /** File báo giá đã tải lên (lát P3b-1). */
  attachments: (id: string) => portalFetch<PortalAttachment[]>(`/api/portal/rfqs/${id}/attachments`),
  /** Tải file báo giá lên. */
  uploadAttachment: async (id: string, file: File, note: string | null) => {
    const form = new FormData()
    form.append('file', file)
    if (note) form.append('note', note)
    return (await (await portalRaw(`/api/portal/rfqs/${id}/attachments`, { method: 'POST', body: form })).json()) as PortalAttachment
  },
  /** Xóa file báo giá (còn trong hạn nộp). */
  deleteAttachment: (id: string, attachmentId: string) =>
    portalFetch<void>(`/api/portal/rfqs/${id}/attachments/${attachmentId}`, { method: 'DELETE' }),
  /** Tải xuống file báo giá của mình. */
  downloadAttachment: async (id: string, item: PortalAttachment) => {
    const response = await portalRaw(`/api/portal/rfqs/${id}/attachments/${item.id}/content`)
    const url = URL.createObjectURL(await response.blob())
    const a = Object.assign(document.createElement('a'), { href: url, download: item.fileName })
    a.click()
    URL.revokeObjectURL(url)
  },
}
