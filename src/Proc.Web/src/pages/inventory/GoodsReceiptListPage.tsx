import { Select } from 'antd'
import dayjs from 'dayjs'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import {
  documentStatusLabels,
  useInventoryDocuments,
  type DocumentStatus,
  type InventoryDocumentListItem,
} from '../../api/inventoryDocuments'
import type { DateRange } from '../../api/logs'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { LogDateRangeFilter, TruncatedNotice } from '../system/logFilters'
import { documentStatusTone } from './documentStatus'

const columns: GridColumn<InventoryDocumentListItem>[] = [
  { key: 'no', title: 'Số phiếu', width: 150, value: (r) => r.no },
  { key: 'date', title: 'Ngày nhập', width: 90, type: 'date', value: (r) => r.date },
  { key: 'warehouse', title: 'Kho nhập', width: 150, value: (r) => (r.warehouseCode ? `${r.warehouseCode} – ${r.warehouseName}` : ''), groupable: true },
  { key: 'supplier', title: 'Nhà cung cấp', width: 220, value: (r) => (r.supplierCode ? `${r.supplierCode} – ${r.supplierName}` : ''), groupable: true },
  { key: 'invoice', title: 'Số hóa đơn', width: 100, value: (r) => r.invoiceNo },
  { key: 'amount', title: 'Tiền hàng', width: 120, type: 'money', value: (r) => r.totalAmount },
  { key: 'vat', title: 'Tiền VAT', width: 100, type: 'money', value: (r) => r.totalVat },
  { key: 'total', title: 'Tổng thanh toán', width: 120, type: 'money', value: (r) => r.totalAmount + r.totalVat },
  { key: 'lines', title: 'Số dòng', width: 70, type: 'quantity', value: (r) => r.lineCount },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 110,
    type: 'status',
    value: (r) => documentStatusLabels[r.status],
    tone: (r) => documentStatusTone[r.status],
    groupable: true,
  },
]

/**
 * Màn hình Phiếu nhập mua từ NCC (GRN – design.md mục 10.4): danh sách phiếu của kho được phép trong khoảng ngày
 * (mặc định 30 ngày gần nhất, lọc ở server); "Tạo mới" và bấm đúp mở trang phiếu.
 */
export function GoodsReceiptListPage() {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const [range, setRange] = useState<DateRange>(() => ({
    from: dayjs().subtract(29, 'day').format('YYYY-MM-DD'),
    to: dayjs().format('YYYY-MM-DD'),
  }))
  const [status, setStatus] = useState<DocumentStatus | undefined>()
  const documents = useInventoryDocuments({ docType: 'GRN', from: range.from, to: range.to, status })

  return (
    <ListPage
      title="Phiếu nhập mua từ NCC (GRN)"
      exportFileName="phieu-nhap-mua"
      rows={documents.data?.items ?? []}
      columns={columns}
      rowKey={(r) => r.id}
      loading={documents.isLoading}
      canCreate={hasPermission(Permission.InventoryDocumentCreate)}
      onCreate={() => navigate('/kho/grn/moi')}
      onOpenRow={(r) => navigate(`/kho/grn/${r.id}`)}
      notice={<TruncatedNotice truncated={documents.data?.truncated} />}
      onClearFilters={() => setStatus(undefined)}
      filters={
        <>
          <LogDateRangeFilter value={range} onChange={setRange} />
          <FilterField label="Trạng thái" width={130}>
            {(id) => (
              <Select
                id={id}
                value={status ?? ''}
                onChange={(v) => setStatus(v === '' ? undefined : (v as DocumentStatus))}
                options={[{ value: '', label: 'Tất cả' }, ...Object.entries(documentStatusLabels).map(([value, label]) => ({ value, label }))]}
              />
            )}
          </FilterField>
        </>
      }
    />
  )
}
