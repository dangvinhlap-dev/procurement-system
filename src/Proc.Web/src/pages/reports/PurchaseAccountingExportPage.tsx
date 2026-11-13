import { FileExcelOutlined } from '@ant-design/icons'
import { Alert, App, Button } from 'antd'
import { Select } from '../../classic/Select'
import dayjs from 'dayjs'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { apiDownload } from '../../api/client'
import {
  accountingExportQuery,
  usePurchaseAccountingDocuments,
  type AccountingExportFilter,
  type PurchaseAccountingDocument,
} from '../../api/reports'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatMoney } from '../../utils/format'
import { LogDateRangeFilter } from '../system/logFilters'

/** Tên loại chứng từ mua xuất cho kế toán. */
const docTypeNames: Record<PurchaseAccountingDocument['docType'], string> = {
  PO: 'Đơn mua hàng',
  XNH: 'Xác nhận nhận hàng',
  DNT: 'Đề nghị thanh toán',
}

/** Đường dẫn mở phiếu theo loại chứng từ. */
const docPaths: Record<PurchaseAccountingDocument['docType'], string> = {
  PO: '/mua-hang/po',
  XNH: '/mua-hang/xac-nhan-nhan-hang',
  DNT: '/mua-hang/de-nghi-thanh-toan',
}

const columns: GridColumn<PurchaseAccountingDocument>[] = [
  { key: 'type', title: 'Loại', width: 140, value: (r) => docTypeNames[r.docType], groupable: true },
  { key: 'effective', title: 'Ngày hiệu lực', width: 100, type: 'date', value: (r) => r.effectiveDate },
  { key: 'no', title: 'Số CT', width: 150, value: (r) => r.no, docRef: (r) => ({ docType: r.docType, id: r.documentId }) },
  { key: 'date', title: 'Ngày CT', width: 90, type: 'date', value: (r) => r.date },
  { key: 'po', title: 'Số PO', width: 150, value: (r) => r.purchaseOrderNo },
  { key: 'supplier', title: 'Nhà cung cấp', width: 230, value: (r) => `${r.supplierCode} – ${r.supplierName}`, groupable: true },
  { key: 'local', title: 'Mã NCC tại công ty', width: 120, value: (r) => r.supplierLocalCode },
  { key: 'tax', title: 'MST', width: 110, value: (r) => r.supplierTaxCode },
  { key: 'invoice', title: 'Hóa đơn', width: 140, value: (r) => (r.invoiceNo ? `${r.invoiceSeries ?? ''} ${r.invoiceNo}`.trim() : null) },
  { key: 'lines', title: 'Số dòng', width: 70, type: 'quantity', value: (r) => r.lineCount },
  { key: 'amount', title: 'Trước thuế / tiền HĐ', width: 130, type: 'money', value: (r) => r.amount },
  { key: 'vat', title: 'Tiền thuế', width: 110, type: 'money', value: (r) => r.vatAmount },
  { key: 'total', title: 'Tổng tiền', width: 120, type: 'money', value: (r) => r.totalAmount },
  { key: 'status', title: 'Tình trạng', width: 140, value: (r) => r.status, groupable: true },
]

/** Điều kiện mặc định: từ đầu tháng đến hôm nay, mọi loại chứng từ. */
const defaultFilter = (): AccountingExportFilter => ({
  from: dayjs().startOf('month').format('YYYY-MM-DD'),
  to: dayjs().format('YYYY-MM-DD'),
  docTypes: [],
})

/**
 * Báo cáo → Xuất chứng từ mua (lát Y9b, design.md mục 19.3 "Lát Y9" – thay ACCX từ khi bỏ phân hệ kho): xem trước PO đã duyệt (theo ngày
 * duyệt), phiếu xác nhận nhận hàng kèm hóa đơn (theo ngày nhận), đề nghị thanh toán đã duyệt (theo ngày duyệt), rồi tải file Excel cho phần
 * mềm kế toán. Phiếu nhận hàng chưa có hóa đơn được tô màu.
 */
export function PurchaseAccountingExportPage() {
  const navigate = useNavigate()
  const { message } = App.useApp()
  const [filter, setFilter] = useState(defaultFilter)
  const [downloading, setDownloading] = useState(false)
  const documents = usePurchaseAccountingDocuments(filter)
  const rows = documents.data ?? []
  const count = (type: PurchaseAccountingDocument['docType']) => rows.filter((r) => r.docType === type).length
  const noInvoice = rows.filter((r) => r.docType === 'XNH' && !r.invoiceNo).length
  const requested = rows.filter((r) => r.docType === 'DNT').reduce((s, r) => s + r.totalAmount, 0)

  const download = async () => {
    setDownloading(true)
    try {
      await apiDownload(`/api/purchase-accounting-export/file?${accountingExportQuery(filter)}`, 'chung-tu-mua-ke-toan.xlsx')
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Không xuất được file.')
    } finally {
      setDownloading(false)
    }
  }

  return (
    <ListPage
      title="Xuất chứng từ mua cho kế toán"
      exportFileName="danh-sach-chung-tu-mua-ke-toan"
      canCreate={false}
      rows={rows}
      columns={columns}
      rowKey={(r) => r.documentId}
      loading={documents.isLoading}
      isMarked={(r) => r.docType === 'XNH' && !r.invoiceNo}
      onOpenRow={(r) => navigate(`${docPaths[r.docType]}/${r.documentId}`)}
      notice={
        documents.error ? (
          <Alert type="error" showIcon title={documents.error.message} />
        ) : (
          <Alert
            type="info"
            showIcon
            title={`${count('PO')} đơn mua hàng, ${count('XNH')} phiếu nhận hàng (${noInvoice} chưa có hóa đơn – tô màu), ${count('DNT')} đề nghị thanh toán đã duyệt, tổng đề nghị ${formatMoney(requested)} đ.`}
            action={
              <Button type="primary" icon={<FileExcelOutlined />} loading={downloading} disabled={rows.length === 0} onClick={() => void download()}>
                Tải file Excel cho kế toán
              </Button>
            }
          />
        )
      }
      onClearFilters={() => setFilter(defaultFilter())}
      filters={
        <>
          <LogDateRangeFilter value={filter} onChange={(range) => setFilter({ ...filter, ...range })} />
          <FilterField label="Loại chứng từ" width={300}>
            {(id) => (
              <Select
                id={id}
                mode="multiple"
                allowClear
                placeholder="Tất cả"
                value={filter.docTypes}
                onChange={(docTypes) => setFilter({ ...filter, docTypes })}
                options={Object.entries(docTypeNames).map(([value, label]) => ({ value, label: `${value} – ${label}` }))}
                maxTagCount="responsive"
              />
            )}
          </FilterField>
        </>
      }
    />
  )
}
