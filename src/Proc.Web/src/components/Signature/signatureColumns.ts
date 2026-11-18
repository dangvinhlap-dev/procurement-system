import type { SignatureVersion, UserSignature } from '../../api/signatures'
import type { GridColumn } from '../DataGrid/types'

/** Cột lịch sử phiên bản chữ ký (màn hình CKY, tab CHỮ KÝ của ngăn kéo người dùng). */
export const signatureColumns: GridColumn<SignatureVersion>[] = [
  { key: 'versionNo', title: 'Phiên bản', width: 80, type: 'quantity', value: (r) => r.versionNo },
  {
    key: 'state',
    title: 'Tình trạng',
    width: 110,
    type: 'status',
    value: (r) => (r.validTo ? 'Hết hiệu lực' : 'Đang dùng'),
    tone: (r) => (r.validTo ? 'neutral' : 'success'),
  },
  { key: 'validFrom', title: 'Hiệu lực từ', width: 130, type: 'datetime', value: (r) => r.validFrom },
  { key: 'validTo', title: 'Đến', width: 130, type: 'datetime', value: (r) => r.validTo },
  { key: 'uploadedBy', title: 'Người tải lên', width: 150, value: (r) => r.uploadedByName },
  { key: 'how', title: 'Cách tải', width: 110, value: (r) => (r.byAdmin ? 'Quản trị tải thay' : 'Tự tải') },
  { key: 'reason', title: 'Lý do', width: 200, value: (r) => r.reason },
  { key: 'retire', title: 'Ngừng dùng', width: 220, value: (r) => (r.retireReason ? `${r.retiredByName ?? ''}: ${r.retireReason}` : null) },
]

/** Họ tên in dưới chữ ký (danh xưng + họ tên). */
export const signatureDisplayName = (s: UserSignature | undefined) =>
  s ? (s.honorific ? `${s.honorific} ${s.fullName}` : s.fullName) : ''
