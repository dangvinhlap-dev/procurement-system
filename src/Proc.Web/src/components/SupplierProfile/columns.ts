import type { SupplierProfileBank, SupplierProfileContact, SupplierProfileRequestStatus } from '../../portal/portalApi'
import type { GridColumn, StatusTone } from '../DataGrid/types'

/** Màu đèn trạng thái đề nghị cập nhật hồ sơ NCC (luôn kèm chữ) – Portal và màn hình nội bộ. */
export const supplierProfileRequestTone: Record<SupplierProfileRequestStatus, StatusTone> = {
  Submitted: 'warning',
  Accepted: 'success',
  Rejected: 'error',
  Withdrawn: 'neutral',
}

/** Cột lưới người liên hệ NCC (chỉ đọc) – lát P2a. */
export const supplierProfileContactColumns: GridColumn<SupplierProfileContact>[] = [
  { key: 'name', title: 'Họ tên', width: 170, value: (r) => r.name, auditField: false },
  { key: 'position', title: 'Chức vụ', width: 120, value: (r) => r.position, auditField: false },
  { key: 'phone', title: 'Điện thoại', width: 110, value: (r) => r.phone, auditField: false },
  { key: 'email', title: 'Email', width: 180, value: (r) => r.email, auditField: false },
  { key: 'isPrimary', title: 'Liên hệ chính', width: 90, value: (r) => (r.isPrimary ? 'Có' : ''), auditField: false },
]

/** Cột lưới tài khoản ngân hàng NCC (chỉ đọc) – lát P2a. */
export const supplierProfileBankColumns: GridColumn<SupplierProfileBank>[] = [
  { key: 'bankName', title: 'Ngân hàng', width: 160, value: (r) => r.bankName, auditField: false },
  { key: 'branch', title: 'Chi nhánh', width: 130, value: (r) => r.branch, auditField: false },
  { key: 'accountNumber', title: 'Số tài khoản', width: 140, value: (r) => r.accountNumber, auditField: false },
  { key: 'accountName', title: 'Chủ tài khoản', width: 180, value: (r) => r.accountName, auditField: false },
  { key: 'isDefault', title: 'Mặc định', width: 70, value: (r) => (r.isDefault ? 'Có' : ''), auditField: false },
]
