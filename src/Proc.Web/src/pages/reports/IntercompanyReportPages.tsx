import { Alert } from 'antd'
import dayjs from 'dayjs'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { useGoodsInTransit, useReconciliation, type GoodsInTransit, type Reconciliation } from '../../api/intercompany'
import type { DateRange } from '../../api/logs'
import type { GridColumn } from '../../components/DataGrid/types'
import { ListPage } from '../../components/ListPage/ListPage'
import { LogDateRangeFilter } from '../system/logFilters'

const transitColumns: GridColumn<GoodsInTransit>[] = [
  {
    key: 'direction',
    title: 'Chiều',
    width: 90,
    type: 'status',
    value: (r) => (r.direction === 'Out' ? 'Bán ra' : 'Mua vào'),
    tone: (r) => (r.direction === 'Out' ? 'warning' : 'success'),
    groupable: true,
  },
  { key: 'date', title: 'Ngày xuất', width: 90, type: 'date', value: (r) => r.saleDate },
  { key: 'sale', title: 'Phiếu xuất bán', width: 150, value: (r) => r.saleNo },
  { key: 'seller', title: 'Công ty bán', width: 90, value: (r) => r.sellerCode, groupable: true },
  { key: 'buyer', title: 'Công ty mua', width: 90, value: (r) => r.buyerCode, groupable: true },
  { key: 'receipt', title: 'Phiếu nhập chờ nhận', width: 150, value: (r) => r.receiptNo },
  { key: 'amount', title: 'Tiền hàng', width: 120, type: 'money', value: (r) => r.totalAmount },
  { key: 'total', title: 'Tổng thanh toán', width: 130, type: 'money', value: (r) => r.totalAmount + r.totalVat },
]

/**
 * Báo cáo → Hàng đi đường nội bộ (design.md mục 11, 6.6): phiếu xuất bán nội bộ đã ghi sổ mà bên mua chưa xác nhận nhận hàng, cả
 * chiều bán ra và mua vào của công ty đang làm việc. Bấm đúp mở phiếu của công ty mình (bán ra: phiếu xuất; mua vào: phiếu nhập).
 */
export function GoodsInTransitPage() {
  const navigate = useNavigate()
  const transit = useGoodsInTransit()
  return (
    <ListPage
      title="Hàng đi đường nội bộ"
      exportFileName="hang-di-duong"
      canCreate={false}
      rows={transit.data ?? []}
      columns={transitColumns}
      rowKey={(r) => r.saleId}
      loading={transit.isLoading}
      onOpenRow={(r) => (r.direction === 'Out' ? navigate(`/kho/ics/${r.saleId}`) : r.receiptId && navigate(`/kho/icr/${r.receiptId}`))}
      notice={<Alert type="info" showIcon title="Hàng đã xuất ở công ty bán, công ty mua chưa xác nhận nhận hàng. Bấm đúp để mở phiếu." />}
    />
  )
}

const reconciliationColumns: GridColumn<Reconciliation>[] = [
  { key: 'company', title: 'Công ty đối ứng', width: 220, value: (r) => `${r.counterCompanyCode} – ${r.counterCompanyName}` },
  { key: 'sold', title: 'Mình bán', width: 130, type: 'money', value: (r) => r.soldAmount },
  { key: 'received', title: 'Bên kia đã nhận', width: 130, type: 'money', value: (r) => r.receivedByCounterAmount },
  { key: 'diffOut', title: 'Chênh lệch bán', width: 130, type: 'money', value: (r) => r.soldAmount - r.receivedByCounterAmount },
  { key: 'soldByCounter', title: 'Bên kia bán', width: 130, type: 'money', value: (r) => r.soldByCounterAmount },
  { key: 'purchased', title: 'Mình đã nhận', width: 130, type: 'money', value: (r) => r.purchasedAmount },
  { key: 'diffIn', title: 'Chênh lệch mua', width: 130, type: 'money', value: (r) => r.soldByCounterAmount - r.purchasedAmount },
]

/**
 * Báo cáo → Đối chiếu nội bộ (design.md mục 11 – nhóm Nội bộ, 6.6): theo từng công ty đối ứng, giá trị trước VAT bên bán xuất và
 * bên mua đã nhận, hai chiều, theo ngày phiếu xuất bán. Chênh lệch = hàng đi đường + nhận thiếu.
 */
export function IntercompanyReconciliationPage() {
  const [range, setRange] = useState<DateRange>(() => ({
    from: dayjs().startOf('month').format('YYYY-MM-DD'),
    to: dayjs().format('YYYY-MM-DD'),
  }))
  const data = useReconciliation(range)
  return (
    <ListPage
      title="Đối chiếu mua bán nội bộ"
      exportFileName="doi-chieu-noi-bo"
      canCreate={false}
      rows={data.data ?? []}
      columns={reconciliationColumns}
      rowKey={(r) => r.counterCompanyId}
      loading={data.isLoading}
      isMarked={(r) => r.soldAmount !== r.receivedByCounterAmount || r.soldByCounterAmount !== r.purchasedAmount}
      notice={<Alert type="info" showIcon title="Giá trị trước VAT theo ngày phiếu xuất bán. Chênh lệch là hàng đi đường hoặc nhận thiếu." />}
      filters={<LogDateRangeFilter value={range} onChange={setRange} />}
    />
  )
}
