import { CheckOutlined, CloseCircleOutlined } from '@ant-design/icons'
import { Alert, App, Button, Descriptions, Input } from 'antd'
import dayjs from 'dayjs'
import { useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import {
  internalSaleStatusLabels,
  internalSaleStatusTones,
  useInternalSale,
  useInternalSaleMutations,
  useInternalSales,
  type InternalSaleLine,
  type InternalSaleListItem,
  type InternalSaleStatus,
} from '../../api/internalSales'
import type { DateRange } from '../../api/logs'
import { DocNo } from '../../classic/DocNo'
import { Select } from '../../classic/Select'
import { DataGrid } from '../../components/DataGrid/DataGrid'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatDate, formatDateTime, formatMoney } from '../../utils/format'
import { LogDateRangeFilter } from '../system/logFilters'

/** Đường dẫn màn hình Đơn bán nội bộ (DBN); `/:id` mở sẵn ngăn kéo một đơn (từ thông báo). */
export const INTERNAL_SALE_PATH = '/mua-hang/don-ban-noi-bo'

/** Nhãn trạng thái PO của công ty mua. */
const orderStatusLabels: Record<string, string> = { Approved: 'Đã duyệt', Closed: 'Đã đóng', Cancelled: 'Đã hủy' }

const listColumns: GridColumn<InternalSaleListItem>[] = [
  // PO thuộc công ty mua: ô số chỉ sao chép, không mở phiếu gốc (người dùng công ty bán không xem được PO của công ty khác).
  { key: 'no', title: 'Số đơn (PO bên mua)', width: 160, value: (r) => r.orderNo },
  { key: 'date', title: 'Ngày đặt', width: 90, type: 'date', value: (r) => r.orderDate },
  { key: 'buyer', title: 'Công ty mua', width: 240, value: (r) => `${r.buyerCompanyCode} – ${r.buyerCompanyName}`, groupable: true },
  { key: 'warehouse', title: 'Nơi nhận', width: 160, value: (r) => r.warehouseName, groupable: true },
  { key: 'expected', title: 'Ngày giao dự kiến', width: 110, type: 'date', value: (r) => r.expectedDate },
  { key: 'lines', title: 'Số dòng', width: 70, type: 'quantity', value: (r) => r.lineCount },
  { key: 'amount', title: 'Tổng tiền (gồm VAT)', width: 140, type: 'money', value: (r) => r.amount },
  {
    key: 'status', title: 'Bên bán', width: 120, type: 'status', groupable: true,
    value: (r) => internalSaleStatusLabels[r.status], tone: (r) => internalSaleStatusTones[r.status],
  },
  { key: 'orderStatus', title: 'Trạng thái PO', width: 100, value: (r) => orderStatusLabels[r.orderStatus] ?? r.orderStatus, groupable: true },
  { key: 'decidedBy', title: 'Người xác nhận', width: 150, value: (r) => r.decidedByName, groupable: true },
  { key: 'decidedAt', title: 'Giờ xác nhận', width: 120, value: (r) => formatDateTime(r.decidedAt) },
]

const lineColumns: GridColumn<InternalSaleLine>[] = [
  { key: 'lineNo', title: 'STT', width: 50, type: 'quantity', value: (r) => r.lineNo },
  { key: 'code', title: 'Mã hàng', width: 110, value: (r) => r.itemCode },
  { key: 'name', title: 'Tên hàng', width: 220, value: (r) => r.itemName },
  { key: 'uom', title: 'ĐVT', width: 60, value: (r) => r.uom },
  { key: 'qty', title: 'SL đặt', width: 90, type: 'quantity', value: (r) => r.qty },
  { key: 'price', title: 'Đơn giá', width: 110, type: 'money', value: (r) => r.unitPrice },
  { key: 'discount', title: '% CK', width: 60, type: 'quantity', value: (r) => r.discountPct },
  { key: 'amount', title: 'Thành tiền', width: 120, type: 'money', value: (r) => r.amount },
  { key: 'vat', title: 'Thuế suất', width: 70, value: (r) => r.vatCode },
  { key: 'delivery', title: 'Ngày giao', width: 90, type: 'date', value: (r) => r.deliveryDate },
  { key: 'received', title: 'SL bên mua đã nhận', width: 110, type: 'quantity', value: (r) => r.receivedQty },
  { key: 'note', title: 'Ghi chú', width: 180, value: (r) => r.note },
]

/**
 * Màn hình Mua hàng → Đơn bán nội bộ – DBN (lát X7, design.md mục 19.3): PO đã duyệt mà công ty khác trong tập đoàn đặt cho NCC nội bộ đại
 * diện công ty đang làm việc. Công ty bán xem đơn rồi Xác nhận (ghi chú tùy chọn) hoặc Từ chối (bắt buộc lý do); mỗi đơn một lần, không đổi.
 * Đường dẫn `/mua-hang/don-ban-noi-bo/:id` (từ thông báo) mở sẵn đơn đó.
 */
export function InternalSaleListPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [range, setRange] = useState<DateRange>(() => ({ from: dayjs().subtract(89, 'day').format('YYYY-MM-DD'), to: dayjs().format('YYYY-MM-DD') }))
  const [status, setStatus] = useState('')
  const list = useInternalSales({ ...range, status })
  return (
    <>
      <ListPage
        title="Đơn bán nội bộ"
        exportFileName="don-ban-noi-bo"
        rows={list.data?.items ?? []}
        columns={listColumns}
        rowKey={(r) => r.orderId}
        auditEntity="InternalSaleConfirmation"
        loading={list.isLoading}
        onOpenRow={(r) => navigate(`${INTERNAL_SALE_PATH}/${r.orderId}`)}
        onClearFilters={() => setStatus('')}
        isMarked={(r) => r.status === 'Pending'}
        notice={list.data?.truncated && <Alert type="warning" showIcon title="Còn đơn chưa hiện – thu hẹp khoảng ngày." />}
        filters={
          <>
            <LogDateRangeFilter value={range} onChange={setRange} />
            <FilterField label="Bên bán" width={140}>
              {(fieldId) => (
                <Select id={fieldId} value={status} onChange={setStatus}
                  options={[{ value: '', label: 'Tất cả' },
                    ...(['Pending', 'Confirmed', 'Rejected', 'Cancelled'] as InternalSaleStatus[]).map((s) => ({ value: s, label: internalSaleStatusLabels[s] }))]} />
              )}
            </FilterField>
          </>
        }
      />
      {id && <InternalSaleDrawer key={id} orderId={id} onClose={() => navigate(INTERNAL_SALE_PATH)} />}
    </>
  )
}

/**
 * Ngăn kéo một đơn bán nội bộ: đầu đơn và dòng hàng chỉ xem (PO thuộc công ty mua, đã khóa); ô "Ghi chú / lý do" cùng nút Xác nhận (icon
 * xanh) / Từ chối (icon đỏ, bắt buộc lý do) khi được quyết định; đã quyết định thì hiện kết quả, người, giờ.
 * @param orderId Id PO của công ty mua.
 * @param onClose Đóng ngăn kéo.
 */
function InternalSaleDrawer({ orderId, onClose }: { orderId: string; onClose: () => void }) {
  const { message, modal } = App.useApp()
  const sale = useInternalSale(orderId)
  const mutations = useInternalSaleMutations()
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const data = sale.data

  const decide = (kind: 'confirm' | 'reject') => {
    if (!data) return
    const text = note.trim()
    if (kind === 'reject' && !text) {
      setError('Nhập lý do từ chối để công ty mua biết xử lý.')
      return
    }
    setError(null)
    modal.confirm({
      title: kind === 'confirm' ? `Xác nhận nhận đơn ${data.orderNo}?` : `Từ chối đơn ${data.orderNo}?`,
      content: 'Mỗi đơn chỉ quyết định một lần, không đổi được. Người lập PO ở công ty mua sẽ nhận thông báo.',
      okText: kind === 'confirm' ? 'Xác nhận' : 'Từ chối',
      cancelText: 'Không',
      onOk: async () => {
        try {
          if (kind === 'confirm') await mutations.confirm.mutateAsync({ orderId, note: text || null })
          else await mutations.reject.mutateAsync({ orderId, note: text })
          message.success(kind === 'confirm' ? `Đã xác nhận đơn ${data.orderNo}.` : `Đã từ chối đơn ${data.orderNo}.`)
          setNote('')
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không ghi được quyết định.')
        }
      },
    })
  }

  const buttons = data?.canDecide && (
    <>
      <Button icon={<CheckOutlined />} loading={mutations.confirm.isPending} onClick={() => decide('confirm')}>Xác nhận đơn</Button>
      <Button icon={<CloseCircleOutlined />} loading={mutations.reject.isPending} onClick={() => decide('reject')}>Từ chối</Button>
    </>
  )

  return (
    <DetailDrawer
      open
      title={data ? `Đơn bán nội bộ: ${data.orderNo}` : 'Đơn bán nội bộ'}
      loading={sale.isLoading}
      error={error ?? (sale.error instanceof Error ? sale.error.message : null)}
      onClose={onClose}
      readOnly={!data?.canDecide}
      extraActions={buttons}
      tabs={[
        {
          key: 'general',
          label: 'Thông tin đơn',
          children: data && (
            <>
              {data.status !== 'Pending' ? (
                <Alert
                  type={data.status === 'Confirmed' ? 'success' : data.status === 'Rejected' ? 'error' : 'info'}
                  showIcon
                  style={{ marginBottom: 8 }}
                  title={`${internalSaleStatusLabels[data.status]}${data.decidedByName ? ` – ${data.decidedByName}` : ''}${data.decidedAt ? `, ${formatDateTime(data.decidedAt)}` : ''}`}
                  description={data.note ?? undefined}
                />
              ) : data.readOnlyReason ? (
                <Alert type="info" showIcon style={{ marginBottom: 8 }} title={data.readOnlyReason} />
              ) : null}
              <Descriptions size="small" column={2} bordered style={{ marginBottom: 8 }}
                items={[
                  { key: 'no', label: 'Số đơn (PO bên mua)', children: <DocNo no={data.orderNo} /> },
                  { key: 'date', label: 'Ngày đặt', children: formatDate(data.orderDate) },
                  { key: 'buyer', label: 'Công ty mua', children: `${data.buyerCompanyCode} – ${data.buyerCompanyName}` },
                  { key: 'status', label: 'Trạng thái PO', children: orderStatusLabels[data.orderStatus] ?? data.orderStatus },
                  { key: 'warehouse', label: 'Nơi nhận', children: data.warehouseName ?? '' },
                  { key: 'expected', label: 'Ngày giao dự kiến', children: formatDate(data.expectedDate) },
                  { key: 'address', label: 'Địa chỉ giao', children: data.deliveryAddress ?? '' },
                  { key: 'payment', label: 'Thanh toán', children: data.paymentTerms ?? '' },
                  { key: 'deliveryTerms', label: 'Điều khoản giao', children: data.deliveryTerms ?? '' },
                  { key: 'orderedBy', label: 'Người lập PO', children: data.orderedByName ?? '' },
                  { key: 'approvedAt', label: 'PO duyệt lúc', children: formatDateTime(data.approvedAt) },
                  {
                    key: 'total', label: 'Tổng tiền',
                    children: `${formatMoney(data.totalAmount)} + VAT ${formatMoney(data.totalVat)} = ${formatMoney(data.totalAmount + data.totalVat)} VND`,
                  },
                  { key: 'orderNote', label: 'Ghi chú PO', span: 2, children: data.orderNote ?? '' },
                ]}
              />
              {data.canDecide && (
                <div style={{ marginBottom: 8 }}>
                  <label htmlFor="internal-sale-note">Ghi chú xác nhận / lý do từ chối</label>
                  <Input.TextArea id="internal-sale-note" rows={2} maxLength={500} value={note} onChange={(e) => setNote(e.target.value)} />
                </div>
              )}
              <DataGrid rows={data.lines} rowKey={(r) => String(r.lineNo)} columns={lineColumns} groupBy={null} onGroupByChange={() => {}} />
            </>
          ),
        },
      ]}
    />
  )
}
