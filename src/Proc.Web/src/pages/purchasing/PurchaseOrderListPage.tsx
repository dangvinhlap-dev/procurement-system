import { App, Select } from 'antd'
import { useMemo, useState } from 'react'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import type { GridColumn, StatusTone } from '../../components/DataGrid/types'
import { demoCompanies, mockPurchaseOrders, type PurchaseOrderRow, type PurchaseOrderStatus } from '../../mocks/purchaseOrders'

/**
 * Tông màu của từng trạng thái PO theo quy ước ui-guide.md:
 * xanh lá = đã xử lý/hoàn tất, cam = chờ duyệt, đỏ = quá hạn, xám = còn lại.
 */
const statusTone: Record<PurchaseOrderStatus, StatusTone> = {
  'Nháp': 'neutral',
  'Chờ duyệt': 'warning',
  'Đã duyệt': 'success',
  'Đang giao': 'neutral',
  'Quá hạn giao': 'error',
  'Đã nhận đủ': 'success',
  'Đã hủy': 'neutral',
}

const columns: GridColumn<PurchaseOrderRow>[] = [
  { key: 'number', title: 'Số PO', width: 140, value: (r) => r.number },
  { key: 'date', title: 'Ngày', width: 85, type: 'date', value: (r) => r.date },
  { key: 'company', title: 'Công ty', width: 170, value: (r) => r.companyName, groupable: true },
  { key: 'supplier', title: 'Nhà cung cấp', width: 250, value: (r) => r.supplierName, groupable: true },
  { key: 'total', title: 'Tổng tiền', width: 120, type: 'money', value: (r) => r.totalAmount },
  { key: 'status', title: 'Trạng thái', width: 120, type: 'status', value: (r) => r.status, tone: (r) => statusTone[r.status], groupable: true },
  { key: 'createdBy', title: 'Người tạo', width: 130, value: (r) => r.createdBy, groupable: true },
]

const allStatuses = Object.keys(statusTone) as PurchaseOrderStatus[]

/**
 * Danh sách đơn mua hàng dữ liệu mẫu (30 dòng giả) ở /mua-hang/po/mau – chỉ để test giao diện chung AppShell/ListPage/DataGrid so với
 * ảnh mẫu docs/mau-man-hinh-danh-sach.png (e2e/po-list.spec.ts). Danh sách PO thật là PurchaseListPage ở /mua-hang/po.
 */
export function PurchaseOrderListPage() {
  const { message } = App.useApp()
  const [company, setCompany] = useState('')
  const [status, setStatus] = useState('')

  const rows = useMemo(
    () => mockPurchaseOrders.filter((r) => (!company || r.companyCode === company) && (!status || r.status === status)),
    [company, status],
  )

  return (
    <ListPage
      title="Danh sách đơn mua hàng (PO)"
      exportFileName="don-mua-hang"
      rows={rows}
      columns={columns}
      rowKey={(r) => r.id}
      isMarked={(r) => r.pinned}
      onOpenRow={(r) => message.info(`Mở đơn mua hàng ${r.number} – form PO sẽ làm ở giai đoạn 5.`)}
      onClearFilters={() => {
        setCompany('')
        setStatus('')
      }}
      filters={
        <>
          <FilterField label="Công ty" width={200}>
            {(id) => (
              <Select
                id={id}
                value={company}
                onChange={setCompany}
                options={[{ value: '', label: 'Tất cả' }, ...demoCompanies.slice(1).map((c) => ({ value: c.code, label: c.name }))]}
              />
            )}
          </FilterField>
          <FilterField label="Trạng thái" width={140}>
            {(id) => (
              <Select
                id={id}
                value={status}
                onChange={setStatus}
                options={[{ value: '', label: 'Tất cả' }, ...allStatuses.map((s) => ({ value: s, label: s }))]}
              />
            )}
          </FilterField>
        </>
      }
    />
  )
}
