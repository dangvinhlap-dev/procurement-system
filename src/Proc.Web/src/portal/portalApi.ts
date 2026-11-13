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

/** Kết quả chọn thầu của chính NCC (lát P4b): trúng mọi dòng đã chào, trúng một phần, không trúng, không báo giá. */
export type PortalRfqOutcome = 'Won' | 'PartlyWon' | 'Lost' | 'NoQuote'

/** Nhãn tiếng Việt của kết quả chọn thầu. */
export const portalRfqOutcomeLabels: Record<PortalRfqOutcome, string> = {
  Won: 'Trúng thầu',
  PartlyWon: 'Trúng một phần',
  Lost: 'Không trúng thầu',
  NoQuote: 'Không nộp báo giá',
}

/** Một dòng NCC trúng thầu – giá theo báo giá của chính NCC. */
export interface PortalWonLine {
  rfqLineId: string
  unitPrice: number
  discountPct: number
  vatCode: string | null
  amount: number
}

/** Kết quả chọn thầu NCC thấy khi kết quả đã duyệt xong (lát P4b) – không có NCC khác, giá khác, lý do chọn. */
export interface PortalRfqResult {
  outcome: PortalRfqOutcome
  awardedAt: string | null
  quotedLineCount: number
  wonAmount: number
  wonLines: PortalWonLine[]
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
  /** Kết quả chọn thầu của chính NCC; null khi chưa có kết quả (lát P4b). */
  outcome: PortalRfqOutcome | null
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
  /** Kết quả chọn thầu của chính NCC; null khi chưa duyệt xong kết quả hoặc đã hủy (lát P4b). */
  result: PortalRfqResult | null
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

/**
 * Lệnh file NCC trên Portal theo loại chứng từ: file báo giá của thư mời (lát P3b-1) hoặc file gửi kèm phản hồi đơn hàng (lát P4a).
 * @param kind "rfqs" hoặc "orders".
 * @returns Các lệnh danh sách, tải lên, xóa, tải xuống.
 */
export function portalFiles(kind: 'rfqs' | 'orders' | 'delivery-notices') {
  // Phiếu báo giao (lát P5) nằm dưới đơn: id = "Id đơn/Id phiếu báo giao".
  const base = (id: string) =>
    kind === 'delivery-notices'
      ? `/api/portal/orders/${id.split('/')[0]}/delivery-notices/${id.split('/')[1]}/attachments`
      : `/api/portal/${kind}/${id}/attachments`
  return {
    list: (id: string) => portalFetch<PortalAttachment[]>(base(id)),
    upload: async (id: string, file: File, note: string | null) => {
      const form = new FormData()
      form.append('file', file)
      if (note) form.append('note', note)
      return (await (await portalRaw(base(id), { method: 'POST', body: form })).json()) as PortalAttachment
    },
    remove: (id: string, attachmentId: string) => portalFetch<void>(`${base(id)}/${attachmentId}`, { method: 'DELETE' }),
    download: async (id: string, item: PortalAttachment) => {
      const response = await portalRaw(`${base(id)}/${item.id}/content`)
      const url = URL.createObjectURL(await response.blob())
      const a = Object.assign(document.createElement('a'), { href: url, download: item.fileName })
      a.click()
      URL.revokeObjectURL(url)
    },
  }
}

/** Trạng thái đơn NCC thấy (lát P4a). */
export type PortalOrderStatus = 'Pending' | 'Confirmed' | 'Rejected' | 'Received' | 'Cancelled'

/** Nhãn trạng thái đơn. */
export const portalOrderStatusLabels: Record<PortalOrderStatus, string> = {
  Pending: 'Chờ xác nhận',
  Confirmed: 'Đã xác nhận',
  Rejected: 'Đã từ chối',
  Received: 'Đã nhận đủ hàng',
  Cancelled: 'Bên đặt đã hủy',
}

/** Một dòng danh sách đơn đặt hàng. */
export interface PortalOrderListItem {
  id: string
  no: string
  companyName: string
  date: string
  expectedDate: string | null
  status: PortalOrderStatus
  lineCount: number
  totalPayable: number
  currencyCode: string
  promisedDate: string | null
  respondedAt: string | null
}

/** Dòng hàng của đơn. */
export interface PortalOrderLine {
  lineNo: number
  itemCode: string
  itemName: string
  supplierItemCode: string | null
  uomCode: string
  qty: number
  unitPrice: number
  discountPct: number
  vatCode: string | null
  amount: number
  vatAmount: number
  deliveryDate: string | null
  note: string | null
  /** Id dòng đơn – chọn dòng khi báo giao (lát P5). */
  purchaseOrderLineId: string
  /** SL bên đặt đã xác nhận nhận. */
  receivedQty: number
  /** SL còn báo giao được = SL đặt × (1 + dung sai) − đã nhận − đang báo giao chưa nhận. */
  deliverableQty: number
}

/** Trạng thái phiếu báo giao (lát P5): đã gửi – chờ nhận, bên đặt đang kiểm nhận, đã nhận, NCC đã hủy. */
export type PortalDeliveryNoticeStatus = 'Submitted' | 'Receiving' | 'Received' | 'Cancelled'

/** Nhãn trạng thái phiếu báo giao. */
export const portalDeliveryNoticeStatusLabels: Record<PortalDeliveryNoticeStatus, string> = {
  Submitted: 'Đã gửi – chờ nhận',
  Receiving: 'Bên đặt đang kiểm nhận',
  Received: 'Bên đặt đã nhận',
  Cancelled: 'Đã hủy',
}

/** Dòng phiếu báo giao; receivedQty / rejectedQty có khi bên đặt đã xác nhận nhận hàng. */
export interface PortalDeliveryNoticeLine {
  lineNo: number
  poLineNo: number
  itemCode: string
  itemName: string
  uomCode: string
  qty: number
  receivedQty: number | null
  rejectedQty: number | null
  note: string | null
}

/** Phiếu báo giao hàng NCC gửi (lát P5). */
export interface PortalDeliveryNotice {
  id: string
  no: string
  expectedDate: string
  deliveryNoteNo: string
  deliverer: string | null
  note: string | null
  status: PortalDeliveryNoticeStatus
  submittedAt: string
  submittedByEmail: string
  cancelledAt: string | null
  cancelReason: string | null
  receiptNo: string | null
  receivedDate: string | null
  canCancel: boolean
  lines: PortalDeliveryNoticeLine[]
}

/** NCC gửi phiếu báo giao (dòng SL 0 bỏ trước khi gửi). */
export interface PortalDeliveryNoticeInput {
  expectedDate: string
  deliveryNoteNo: string | null
  deliverer: string | null
  note: string | null
  lines: { purchaseOrderLineId: string; qty: number; note: string | null }[]
}

/** Một lần NCC phản hồi đơn (reopenedAt khác null = bên đặt đã mở lại, phản hồi này là lịch sử). */
export interface PortalOrderResponse {
  decision: 'Confirmed' | 'Rejected'
  promisedDate: string | null
  note: string | null
  respondedAt: string
  respondedByEmail: string
  reopenedAt: string | null
  reopenReason: string | null
}

/** Chi tiết đơn đặt hàng. */
export interface PortalOrder {
  id: string
  no: string
  companyName: string
  date: string
  approvedAt: string | null
  expectedDate: string | null
  deliveryPlace: string | null
  deliveryAddress: string | null
  paymentTerms: string | null
  deliveryTerms: string | null
  note: string | null
  currencyCode: string
  status: PortalOrderStatus
  canRespond: boolean
  grossAmount: number
  totalDiscount: number
  totalAmount: number
  totalVat: number
  lines: PortalOrderLine[]
  responses: PortalOrderResponse[]
  maxFileSizeMb: number
  /** Còn báo giao được (lát P5). */
  canNotifyDelivery: boolean
  /** Lý do chưa báo giao được. */
  notifyDeliveryBlockedReason: string | null
  /** Các phiếu báo giao, mới trước. */
  deliveryNotices: PortalDeliveryNotice[]
}

/** NCC xác nhận (kèm ngày giao cam kết) hoặc từ chối (kèm lý do). */
export interface PortalOrderRespond {
  decision: 'Confirmed' | 'Rejected'
  promisedDate: string | null
  note: string | null
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
  attachments: (id: string) => portalFiles('rfqs').list(id),
  /** Tải file báo giá lên. */
  uploadAttachment: (id: string, file: File, note: string | null) => portalFiles('rfqs').upload(id, file, note),
  /** Xóa file báo giá (còn trong hạn nộp). */
  deleteAttachment: (id: string, attachmentId: string) => portalFiles('rfqs').remove(id, attachmentId),
  /** Tải xuống file báo giá của mình. */
  downloadAttachment: (id: string, item: PortalAttachment) => portalFiles('rfqs').download(id, item),
  /** Đơn đặt hàng của NCC (lát P4a). */
  orders: () => portalFetch<PortalOrderListItem[]>('/api/portal/orders'),
  /** Chi tiết đơn. */
  order: (id: string) => portalFetch<PortalOrder>(`/api/portal/orders/${id}`),
  /** Xác nhận / từ chối đơn. */
  respondOrder: (id: string, input: PortalOrderRespond) =>
    portalFetch<PortalOrder>(`/api/portal/orders/${id}/respond`, { method: 'POST', body: input }),
  /** Gửi phiếu báo giao hàng (lát P5) – trả phiếu vừa gửi để tải file kèm. */
  createDeliveryNotice: (orderId: string, input: PortalDeliveryNoticeInput) =>
    portalFetch<PortalDeliveryNotice>(`/api/portal/orders/${orderId}/delivery-notices`, { method: 'POST', body: input }),
  /** Hủy phiếu báo giao (bắt buộc lý do). */
  cancelDeliveryNotice: (orderId: string, noticeId: string, reason: string) =>
    portalFetch<PortalOrder>(`/api/portal/orders/${orderId}/delivery-notices/${noticeId}/cancel`, { method: 'POST', body: { reason } }),
}
