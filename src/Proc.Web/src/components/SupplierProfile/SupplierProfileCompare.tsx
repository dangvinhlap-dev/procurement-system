import { useState } from 'react'
import { DataGrid } from '../DataGrid/DataGrid'
import type { GridColumn } from '../DataGrid/types'
import { supplierProfileBankColumns as bankColumns, supplierProfileContactColumns as contactColumns } from './columns'
import type {
  SupplierProfileBank,
  SupplierProfileContact,
  SupplierProfileField,
  SupplierProfileRequest,
} from '../../portal/portalApi'

/** Cột so sánh thông tin chung: lúc NCC gửi → đề nghị; nội bộ thêm cột "Hiện tại" để thấy chỗ Phòng CUVT đã sửa trong lúc chờ. */
function fieldColumns(showCurrent: boolean): GridColumn<SupplierProfileField>[] {
  return [
    { key: 'label', title: 'Thông tin', width: 140, value: (r) => r.label, auditField: false },
    { key: 'old', title: 'Lúc gửi', width: 240, value: (r) => r.old ?? '(trống)', auditField: false },
    { key: 'new', title: 'Đề nghị', width: 240, value: (r) => r.new ?? '(xóa trắng)', auditField: false },
    ...(showCurrent
      ? [
          {
            key: 'current',
            title: 'Hiện tại',
            width: 240,
            value: (r: SupplierProfileField) => (r.current === r.old ? '= lúc gửi' : `${r.current ?? '(trống)'} – đã sửa trong lúc chờ`),
            auditField: false as const,
          },
        ]
      : []),
  ]
}

/** Lưới chỉ đọc cao cố định (kiểu khối file Portal). */
function Grid<T>({ rows, columns, rowKey, height, testId }: {
  rows: T[]
  columns: GridColumn<T>[]
  rowKey: (row: T) => string
  height: number
  testId: string
}) {
  const [groupBy, setGroupBy] = useState<string | null>(null)
  return (
    <div style={{ height, display: 'flex', flexDirection: 'column', marginBottom: 8 }} data-testid={testId}>
      <DataGrid rows={rows} columns={columns} rowKey={rowKey} groupBy={groupBy} onGroupByChange={setGroupBy} />
    </div>
  )
}

/**
 * So sánh đề nghị cập nhật hồ sơ NCC với hồ sơ (lát P2a, design.md 19.5 P2-4): bảng thông tin chung đổi, danh sách người liên hệ và tài khoản
 * đề nghị (hiện danh sách hiện tại bên dưới để đối chiếu). Dùng chung cho Portal và màn hình nội bộ.
 * @param request Đề nghị.
 * @param showCurrent Hiện cột / danh sách "hiện tại" (nội bộ) – Portal chỉ thấy lúc gửi và đề nghị.
 */
export function SupplierProfileCompare({ request, showCurrent }: { request: SupplierProfileRequest; showCurrent: boolean }) {
  const contactKey = (r: SupplierProfileContact) => `${r.id ?? 'moi'}-${r.name}-${r.email ?? ''}`
  const bankKey = (r: SupplierProfileBank) => `${r.bankName}-${r.accountNumber}`
  return (
    <div data-testid="profile-compare">
      <p className="erp-profile-compare-title">
        <strong>Thông tin chung</strong> {request.fields.length === 0 && '– không đổi'}
      </p>
      {request.fields.length > 0 && (
        <Grid
          rows={request.fields}
          columns={fieldColumns(showCurrent)}
          rowKey={(r) => r.field}
          // Thanh nhóm cột (~26px) + tiêu đề (~24px) + mỗi dòng 22px + viền: đủ hiện mọi dòng, không cuộn.
          height={56 + 22 * request.fields.length}
          testId="profile-compare-fields"
        />
      )}
      <p className="erp-profile-compare-title">
        <strong>Người liên hệ</strong> {request.proposedContacts ? '– đề nghị thay bằng danh sách dưới' : '– không đổi'}
      </p>
      {request.proposedContacts && (
        <Grid rows={request.proposedContacts} columns={contactColumns} rowKey={contactKey} height={120} testId="profile-compare-contacts" />
      )}
      {request.proposedContacts && showCurrent && (
        <>
          <p className="erp-profile-compare-title">Người liên hệ hiện tại</p>
          <Grid rows={request.currentContacts} columns={contactColumns} rowKey={contactKey} height={100} testId="profile-current-contacts" />
        </>
      )}
      <p className="erp-profile-compare-title">
        <strong>Tài khoản ngân hàng</strong>{' '}
        {request.proposedBankAccounts
          ? '– đề nghị thay bằng danh sách dưới (khi chấp nhận thành yêu cầu duyệt NCC, duyệt xong mới áp dụng)'
          : '– không đổi'}
      </p>
      {request.proposedBankAccounts && (
        <Grid rows={request.proposedBankAccounts} columns={bankColumns} rowKey={bankKey} height={100} testId="profile-compare-banks" />
      )}
      {request.proposedBankAccounts && showCurrent && (
        <>
          <p className="erp-profile-compare-title">Tài khoản hiện tại</p>
          <Grid rows={request.currentBankAccounts} columns={bankColumns} rowKey={bankKey} height={100} testId="profile-current-banks" />
        </>
      )}
    </div>
  )
}
