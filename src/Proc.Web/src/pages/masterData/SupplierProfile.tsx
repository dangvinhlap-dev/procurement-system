import { Descriptions, Table } from 'antd'
import { supplierStatusLabels, useSupplier, type SupplierBankAccount, type SupplierContact } from '../../api/masterData'

/**
 * Hồ sơ NCC chỉ xem – đặt trong trang/ngăn kéo duyệt NCC để người duyệt thấy đủ thông tin trước khi duyệt mà không phải mở màn hình
 * NCC (yêu cầu sau test 0.14.0, design.md mục 18.6). Cùng quyền `MasterData.Supplier.View` với màn hình duyệt nên không mở thêm dữ liệu.
 * @param supplierId Id NCC.
 */
export function SupplierProfile({ supplierId }: { supplierId: string }) {
  const supplier = useSupplier(supplierId)
  const s = supplier.data
  if (!s) return null
  const text = (v: string | null | undefined) => (v?.trim() ? v : '—')
  return (
    <div data-testid="supplier-profile">
      <Descriptions column={2} size="small" bordered>
        <Descriptions.Item label="Mã NCC">{s.code}</Descriptions.Item>
        <Descriptions.Item label="Trạng thái">
          {supplierStatusLabels[s.status]}
          {s.statusNote ? ` – ${s.statusNote}` : ''}
        </Descriptions.Item>
        <Descriptions.Item label="Tên NCC" span={2}>{s.name}</Descriptions.Item>
        <Descriptions.Item label="Tên viết tắt">{text(s.shortName)}</Descriptions.Item>
        <Descriptions.Item label="Mã số thuế">{s.isForeign ? `${text(s.taxCode)} (NCC nước ngoài)` : text(s.taxCode)}</Descriptions.Item>
        <Descriptions.Item label="Địa chỉ" span={2}>{text(s.address)}</Descriptions.Item>
        <Descriptions.Item label="Điện thoại">{text(s.phone)}</Descriptions.Item>
        <Descriptions.Item label="Email">{text(s.email)}</Descriptions.Item>
        <Descriptions.Item label="Website">{text(s.website)}</Descriptions.Item>
        <Descriptions.Item label="Xếp hạng">{s.rating ? `${s.rating}/5` : '—'}</Descriptions.Item>
        <Descriptions.Item label="Điều khoản thanh toán" span={2}>{text(s.paymentTerms)}</Descriptions.Item>
        <Descriptions.Item label="Phạm vi cung cấp" span={2}>{text(s.supplyScope)}</Descriptions.Item>
      </Descriptions>
      <h4 className="supplier-profile-heading">Người liên hệ</h4>
      <Table<SupplierContact>
        size="small"
        bordered
        pagination={false}
        rowKey={(r) => r.id ?? r.name}
        dataSource={s.contacts}
        locale={{ emptyText: 'Chưa có người liên hệ' }}
        columns={[
          { key: 'name', title: 'Họ tên', render: (_, r) => `${r.name}${r.isPrimary ? ' (chính)' : ''}` },
          { key: 'position', title: 'Chức vụ', render: (_, r) => text(r.position) },
          { key: 'phone', title: 'Điện thoại', render: (_, r) => text(r.phone) },
          { key: 'email', title: 'Email', render: (_, r) => text(r.email) },
        ]}
      />
      <h4 className="supplier-profile-heading">Tài khoản ngân hàng</h4>
      <Table<SupplierBankAccount>
        size="small"
        bordered
        pagination={false}
        rowKey={(r) => r.id ?? r.accountNumber}
        dataSource={s.bankAccounts}
        locale={{ emptyText: 'Chưa có tài khoản' }}
        columns={[
          { key: 'bank', title: 'Ngân hàng', render: (_, r) => `${r.bankName}${r.isDefault ? ' (mặc định)' : ''}` },
          { key: 'branch', title: 'Chi nhánh', render: (_, r) => text(r.branch) },
          { key: 'number', title: 'Số tài khoản', render: (_, r) => r.accountNumber },
          { key: 'holder', title: 'Chủ tài khoản', render: (_, r) => text(r.accountName) },
        ]}
      />
    </div>
  )
}
