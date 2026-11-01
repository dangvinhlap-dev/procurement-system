import { DeleteOutlined } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Select } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useState } from 'react'
import {
  purchaseHistoryImportSource,
  useDeletePurchaseHistoryBatch,
  usePurchaseHistory,
  usePurchaseHistoryBatches,
  type PurchaseHistoryFilter,
  type PurchaseHistoryRow,
} from '../../api/purchaseHistory'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn, StatusTone } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatDate, formatDateTime, formatMoney } from '../../utils/format'

/** Có / Không / trống (chưa đánh giá). */
const yesNo = (value: boolean | null) => (value == null ? null : value ? 'Có' : 'Không')

/** Trạng thái khớp danh mục của dòng (lát E2 sẽ ánh xạ tiếp các dòng chưa khớp). */
function matchStatus(r: PurchaseHistoryRow): { text: string; tone: StatusTone } {
  if (r.itemId && r.supplierId) return { text: 'Đã khớp', tone: 'success' }
  if (r.itemId) return { text: 'Thiếu NCC', tone: 'warning' }
  if (r.supplierId) return { text: 'Thiếu mặt hàng', tone: 'warning' }
  return { text: 'Chưa khớp', tone: 'neutral' }
}

const columns: GridColumn<PurchaseHistoryRow>[] = [
  { key: 'date', title: 'Ngày mua', width: 90, type: 'date', value: (r) => r.purchaseDate },
  { key: 'dept', title: 'Bộ phận sử dụng', width: 140, value: (r) => r.department, groupable: true },
  { key: 'code', title: 'Mã vật tư', width: 110, value: (r) => r.itemCode },
  { key: 'name', title: 'Tên vật tư', width: 200, value: (r) => r.itemName, groupable: true },
  { key: 'spec', title: 'Quy cách', width: 120, value: (r) => r.specification },
  { key: 'brand', title: 'Thương hiệu', width: 100, value: (r) => r.brand, groupable: true },
  { key: 'origin', title: 'Xuất xứ', width: 90, value: (r) => r.origin, groupable: true },
  { key: 'standard', title: 'Tiêu chuẩn', width: 100, value: (r) => r.standard },
  { key: 'uom', title: 'ĐVT', width: 60, value: (r) => r.uom },
  { key: 'qty', title: 'Số lượng', width: 90, type: 'quantity', value: (r) => r.quantity },
  // Đơn giá có thể lẻ (numeric(19,6)) nên hiện như số lượng, không làm tròn về đồng.
  { key: 'price', title: 'Đơn giá trước VAT', width: 120, type: 'quantity', value: (r) => r.unitPrice },
  { key: 'vat', title: '% VAT', width: 60, type: 'quantity', value: (r) => r.vatPercent },
  { key: 'currency', title: 'Tiền tệ', width: 60, value: (r) => r.currency, groupable: true },
  { key: 'amount', title: 'Thành tiền', width: 120, type: 'money', value: (r) => r.amount },
  { key: 'cost', title: 'Chi phí liên quan', width: 110, type: 'money', value: (r) => r.relatedCost },
  { key: 'costNote', title: 'Ghi chú chi phí', width: 140, value: (r) => r.relatedCostNote },
  { key: 'supplier', title: 'Nhà cung cấp', width: 200, value: (r) => r.supplierName, groupable: true },
  { key: 'tax', title: 'MST NCC', width: 110, value: (r) => r.supplierTaxCode },
  { key: 'contact', title: 'Liên hệ NCC', width: 130, value: (r) => r.supplierContact },
  { key: 'payment', title: 'Điều kiện thanh toán', width: 130, value: (r) => r.paymentTerms },
  { key: 'discount', title: 'Chiết khấu', width: 90, value: (r) => r.discount },
  { key: 'sponsor', title: 'Tài trợ', width: 100, value: (r) => r.sponsorship },
  { key: 'onTime', title: 'Đúng hạn', width: 80, value: (r) => yesNo(r.onTime), groupable: true },
  { key: 'quality', title: 'Đạt chất lượng', width: 100, value: (r) => yesNo(r.qualityPassed), groupable: true },
  { key: 'complaint', title: 'Đổi trả / khiếu nại', width: 140, value: (r) => r.complaint },
  {
    key: 'match',
    title: 'Khớp danh mục',
    width: 120,
    type: 'status',
    value: (r) => matchStatus(r).text,
    tone: (r) => matchStatus(r).tone,
    groupable: true,
  },
  { key: 'item', title: 'Mã hàng đã khớp', width: 110, value: (r) => r.matchedItemCode },
  { key: 'supplierCode', title: 'Mã NCC đã khớp', width: 110, value: (r) => r.matchedSupplierCode },
  { key: 'request', title: 'Ngày yêu cầu', width: 90, type: 'date', value: (r) => r.requestDate },
  { key: 'row', title: 'Dòng trên file', width: 80, type: 'quantity', value: (r) => r.rowNumber },
]

const emptyFilter: PurchaseHistoryFilter = { batchId: '', from: '', to: '', match: '' }

/**
 * Báo cáo → Dữ liệu mua lịch sử (mã PHIS, design.md mục 18.1, lát E1): dữ liệu mua 12–24 tháng của pháp nhân đang làm việc, nhập
 * từ Excel theo lô để phân tích; không ghi sổ kho. Lọc theo lô, ngày mua, khớp danh mục; nhập nhầm thì xóa cả lô.
 */
export function PurchaseHistoryPage() {
  const { message, modal } = App.useApp()
  const { hasPermission } = useAuth()
  const canImport = hasPermission(Permission.PurchaseHistoryImport)
  const [filter, setFilter] = useState(emptyFilter)
  const history = usePurchaseHistory(filter)
  const batches = usePurchaseHistoryBatches()
  const deleteBatch = useDeletePurchaseHistoryBatch()
  const rows = history.data?.rows ?? []
  const selectedBatch = batches.data?.find((b) => b.id === filter.batchId)
  const unmatched = rows.filter((r) => !r.itemId || !r.supplierId).length

  const confirmDelete = () => {
    if (!selectedBatch) return
    modal.confirm({
      title: `Xóa lô "${selectedBatch.fileName}"?`,
      content: `Xóa cả ${selectedBatch.recordCount.toLocaleString('vi-VN')} dòng của lô nhập lúc ${formatDateTime(selectedBatch.importedAt)}. Thao tác ghi nhật ký, không khôi phục được – muốn có lại thì nhập lại file.`,
      okText: 'Xóa lô',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        try {
          await deleteBatch.mutateAsync(selectedBatch.id)
          setFilter({ ...filter, batchId: '' })
          message.success(`Đã xóa lô "${selectedBatch.fileName}".`)
        } catch (e) {
          message.error(e instanceof Error ? e.message : 'Không xóa được lô.')
        }
      },
    })
  }

  return (
    <ListPage
      title="Dữ liệu mua lịch sử"
      exportFileName="du-lieu-mua-lich-su"
      exportSubtitle={selectedBatch ? `Lô: ${selectedBatch.fileName}` : undefined}
      canCreate={false}
      rows={rows}
      columns={columns}
      rowKey={(r) => r.id}
      loading={history.isLoading}
      excelImport={{ source: purchaseHistoryImportSource, title: 'dữ liệu mua lịch sử', canImport }}
      notice={
        history.error ? (
          <Alert type="error" showIcon title={history.error.message} />
        ) : (
          <>
            {history.data?.truncated && (
              <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 6 }}
                title={`Chỉ hiện ${history.data.limit.toLocaleString('vi-VN')} dòng có ngày mua mới nhất – chọn lô hoặc thu hẹp khoảng ngày để xem hết.`}
              />
            )}
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 6 }}
              title={`${rows.length.toLocaleString('vi-VN')} dòng, thành tiền ${formatMoney(rows.reduce((s, r) => s + r.amount, 0))} đ; ${unmatched.toLocaleString('vi-VN')} dòng chưa khớp đủ mặt hàng và NCC. Dữ liệu chỉ để phân tích – không ghi sổ kho.`}
            />
          </>
        )
      }
      onClearFilters={() => setFilter(emptyFilter)}
      filters={
        <>
          <FilterField label="Lô nhập" width={230}>
            {(id) => (
              <Select
                id={id}
                allowClear
                placeholder="Tất cả các lô"
                loading={batches.isLoading}
                value={filter.batchId || undefined}
                onChange={(batchId) => setFilter({ ...filter, batchId: batchId ?? '' })}
                options={(batches.data ?? []).map((b) => ({
                  value: b.id,
                  label: `${b.fileName} – ${formatDateTime(b.importedAt)} (${b.recordCount.toLocaleString('vi-VN')} dòng)`,
                  title: `${b.fileName}: ngày mua ${formatDate(b.fromDate)} – ${formatDate(b.toDate)}, người nhập ${b.importedBy ?? ''}`,
                }))}
              />
            )}
          </FilterField>
          <FilterField label="Ngày mua" width={240}>
            {(id) => (
              <DatePicker.RangePicker
                id={id}
                format="DD/MM/YYYY"
                value={filter.from && filter.to ? [dayjs(filter.from), dayjs(filter.to)] : null}
                onChange={(dates) => {
                  const [from, to] = (dates ?? []) as [Dayjs | null, Dayjs | null]
                  setFilter({ ...filter, from: from?.format('YYYY-MM-DD') ?? '', to: to?.format('YYYY-MM-DD') ?? '' })
                }}
              />
            )}
          </FilterField>
          <FilterField label="Khớp danh mục" width={150}>
            {(id) => (
              <Select
                id={id}
                value={filter.match}
                onChange={(match) => setFilter({ ...filter, match })}
                options={[
                  { value: '', label: 'Tất cả' },
                  { value: 'matched', label: 'Đã khớp đủ' },
                  { value: 'unmatched', label: 'Chưa khớp đủ' },
                ]}
              />
            )}
          </FilterField>
          {canImport && selectedBatch && (
            <Button danger icon={<DeleteOutlined />} onClick={confirmDelete} loading={deleteBatch.isPending}>
              Xóa lô
            </Button>
          )}
        </>
      }
    />
  )
}
