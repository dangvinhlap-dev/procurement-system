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

type Column = GridColumn<InventoryDocumentListItem>

const noColumn: Column = { key: 'no', title: 'Số phiếu', width: 150, value: (r) => r.no }
const supplierColumn: Column = {
  key: 'supplier', title: 'Nhà cung cấp', width: 220, value: (r) => (r.supplierCode ? `${r.supplierCode} – ${r.supplierName}` : ''), groupable: true,
}
const moneyColumns: Column[] = [
  { key: 'amount', title: 'Tiền hàng', width: 120, type: 'money', value: (r) => r.totalAmount },
  { key: 'vat', title: 'Tiền VAT', width: 100, type: 'money', value: (r) => r.totalVat },
  { key: 'total', title: 'Tổng thanh toán', width: 120, type: 'money', value: (r) => r.totalAmount + r.totalVat },
]
const tailColumns: Column[] = [
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

/** Cột theo loại phiếu: phiếu nhập/trả có NCC và tiền; phiếu chuyển có kho đi, kho đến, không có tiền (giá vốn chốt khi đóng kỳ). */
const columnsByType: Record<string, Column[]> = {
  GRN: [
    noColumn,
    { key: 'date', title: 'Ngày nhập', width: 90, type: 'date', value: (r) => r.date },
    { key: 'warehouse', title: 'Kho nhập', width: 150, value: (r) => (r.warehouseCode ? `${r.warehouseCode} – ${r.warehouseName}` : ''), groupable: true },
    supplierColumn,
    { key: 'invoice', title: 'Số hóa đơn', width: 100, value: (r) => r.invoiceNo },
    ...moneyColumns,
    ...tailColumns,
  ],
  RTV: [
    noColumn,
    { key: 'date', title: 'Ngày trả', width: 90, type: 'date', value: (r) => r.date },
    { key: 'warehouse', title: 'Kho xuất', width: 150, value: (r) => (r.warehouseCode ? `${r.warehouseCode} – ${r.warehouseName}` : ''), groupable: true },
    supplierColumn,
    { key: 'invoice', title: 'Số HĐ điều chỉnh', width: 120, value: (r) => r.invoiceNo },
    ...moneyColumns,
    ...tailColumns,
  ],
  TRF: [
    noColumn,
    { key: 'date', title: 'Ngày chuyển', width: 90, type: 'date', value: (r) => r.date },
    { key: 'from', title: 'Kho xuất', width: 120, value: (r) => r.fromWarehouseCode, groupable: true },
    { key: 'to', title: 'Kho nhập', width: 120, value: (r) => r.toWarehouseCode, groupable: true },
    ...tailColumns,
  ],
}

/**
 * Danh sách chứng từ kho một loại (GRN, RTV, TRF – design.md mục 10) của kho được phép trong khoảng ngày (mặc định 30 ngày gần
 * nhất, lọc ở server); "Tạo mới" và bấm đúp mở trang phiếu dưới cùng đường dẫn.
 * @param docType Loại chứng từ.
 * @param title Tiêu đề màn hình.
 * @param path Đường dẫn màn hình, ví dụ "/kho/grn".
 * @param exportFileName Tên file khi xuất CSV.
 */
export function InventoryDocumentListPage({ docType, title, path, exportFileName }: { docType: string; title: string; path: string; exportFileName: string }) {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const [range, setRange] = useState<DateRange>(() => ({
    from: dayjs().subtract(29, 'day').format('YYYY-MM-DD'),
    to: dayjs().format('YYYY-MM-DD'),
  }))
  const [status, setStatus] = useState<DocumentStatus | undefined>()
  const documents = useInventoryDocuments({ docType, from: range.from, to: range.to, status })

  return (
    <ListPage
      title={title}
      exportFileName={exportFileName}
      rows={documents.data?.items ?? []}
      columns={columnsByType[docType]}
      rowKey={(r) => r.id}
      loading={documents.isLoading}
      canCreate={hasPermission(Permission.InventoryDocumentCreate)}
      onCreate={() => navigate(`${path}/moi`)}
      onOpenRow={(r) => navigate(`${path}/${r.id}`)}
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
