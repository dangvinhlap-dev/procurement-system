import { FileExcelOutlined } from '@ant-design/icons'
import { Alert, App, Button, Select } from 'antd'
import dayjs from 'dayjs'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { apiDownload } from '../../api/client'
import { accountingExportQuery, useAccountingDocuments, type AccountingDocument, type AccountingExportFilter } from '../../api/reports'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatMoney } from '../../utils/format'
import { LogDateRangeFilter } from '../system/logFilters'

/** Tên loại chứng từ xuất cho kế toán. */
const docTypeNames: Record<AccountingDocument['docType'], string> = {
  GRN: 'Nhập mua NCC',
  RTV: 'Xuất trả NCC',
  TRF: 'Chuyển kho nội bộ',
  ICS: 'Xuất bán nội bộ',
  ICR: 'Nhập mua nội bộ',
}

/** Đường dẫn mở phiếu theo loại chứng từ. */
const docPaths: Record<AccountingDocument['docType'], string> = { GRN: '/kho/grn', RTV: '/kho/rtv', TRF: '/kho/trf', ICS: '/kho/ics', ICR: '/kho/icr' }

const columns: GridColumn<AccountingDocument>[] = [
  { key: 'date', title: 'Ngày CT', width: 90, type: 'date', value: (r) => r.date },
  { key: 'no', title: 'Số CT', width: 150, value: (r) => r.no },
  { key: 'type', title: 'Loại', width: 130, value: (r) => docTypeNames[r.docType], groupable: true },
  { key: 'partner', title: 'Đối tượng', width: 230, value: (r) => (r.partnerCode ? `${r.partnerCode} – ${r.partnerName}` : null), groupable: true },
  { key: 'local', title: 'Mã NCC tại công ty', width: 120, value: (r) => r.partnerLocalCode },
  { key: 'tax', title: 'MST', width: 110, value: (r) => r.partnerTaxCode },
  { key: 'invoice', title: 'Hóa đơn', width: 130, value: (r) => (r.invoiceNo ? `${r.invoiceSeries ?? ''} ${r.invoiceNo}`.trim() : null) },
  { key: 'lines', title: 'Số dòng', width: 70, type: 'quantity', value: (r) => r.lineCount },
  { key: 'amount', title: 'Tiền hàng', width: 120, type: 'money', value: (r) => r.amount },
  { key: 'vat', title: 'Tiền thuế', width: 110, type: 'money', value: (r) => r.vatAmount },
  { key: 'cost', title: 'Tiền vốn', width: 120, type: 'money', value: (r) => r.costAmount },
  {
    key: 'final',
    title: 'Giá vốn',
    width: 100,
    type: 'status',
    value: (r) => (r.isCostFinal ? 'Chính thức' : 'Tạm tính'),
    tone: (r) => (r.isCostFinal ? 'success' : 'warning'),
    groupable: true,
  },
]

/** Điều kiện mặc định: từ đầu tháng đến hôm nay, mọi loại chứng từ. */
const defaultFilter = (): AccountingExportFilter => ({
  from: dayjs().startOf('month').format('YYYY-MM-DD'),
  to: dayjs().format('YYYY-MM-DD'),
  docTypes: [],
})

/**
 * Báo cáo → Xuất cho kế toán (design.md mục 11.5): xem trước các chứng từ kho đã ghi sổ trong khoảng ngày, rồi tải file Excel cho phần mềm
 * kế toán (sheet Chứng từ – mỗi dòng hàng một dòng, Tổng hợp, Hướng dẫn). Chứng từ giá vốn tạm tính được tô màu: nên xuất lại sau khi đóng kỳ.
 */
export function AccountingExportPage() {
  const navigate = useNavigate()
  const { message } = App.useApp()
  const [filter, setFilter] = useState(defaultFilter)
  const [downloading, setDownloading] = useState(false)
  const documents = useAccountingDocuments(filter)
  const rows = documents.data ?? []
  const provisional = rows.filter((r) => !r.isCostFinal).length

  const download = async () => {
    setDownloading(true)
    try {
      await apiDownload(`/api/accounting-export/file?${accountingExportQuery(filter)}`, 'chung-tu-ke-toan.xlsx')
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Không xuất được file.')
    } finally {
      setDownloading(false)
    }
  }

  return (
    <ListPage
      title="Xuất chứng từ cho kế toán"
      exportFileName="danh-sach-chung-tu-ke-toan"
      canCreate={false}
      rows={rows}
      columns={columns}
      rowKey={(r) => r.documentId}
      loading={documents.isLoading}
      isMarked={(r) => !r.isCostFinal}
      onOpenRow={(r) => navigate(`${docPaths[r.docType]}/${r.documentId}`)}
      notice={
        documents.error ? (
          <Alert type="error" showIcon title={documents.error.message} />
        ) : (
          <Alert
            type="info"
            showIcon
            title={`${rows.length} chứng từ đã ghi sổ, tiền hàng ${formatMoney(rows.reduce((s, r) => s + r.amount, 0))} đ. ${provisional} chứng từ giá vốn tạm tính (tô màu) – nên xuất lại sau khi đóng kỳ.`}
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
