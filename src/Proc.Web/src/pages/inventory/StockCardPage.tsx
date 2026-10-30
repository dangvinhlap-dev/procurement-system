import { Alert, Select } from 'antd'
import dayjs from 'dayjs'
import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { useItemLookup } from '../../api/inventoryDocuments'
import type { DateRange } from '../../api/logs'
import { useWarehouses } from '../../api/organization'
import { useStockCard, type StockCardEntry } from '../../api/stock'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatMoney, formatQuantity } from '../../utils/format'
import { LogDateRangeFilter } from '../system/logFilters'

/** Đường dẫn trang phiếu theo loại chứng từ – bấm đúp một phát sinh để mở phiếu gốc. */
const documentPaths: Record<string, string> = { GRN: '/kho/grn', RTV: '/kho/rtv', TRF: '/kho/trf' }

const columns: GridColumn<StockCardEntry>[] = [
  { key: 'date', title: 'Ngày', width: 90, type: 'date', value: (r) => r.date },
  { key: 'no', title: 'Số phiếu', width: 150, value: (r) => r.documentNo },
  { key: 'type', title: 'Loại', width: 60, value: (r) => r.docType, groupable: true },
  { key: 'warehouse', title: 'Kho', width: 70, value: (r) => r.warehouseCode, groupable: true },
  { key: 'description', title: 'Diễn giải', width: 240, value: (r) => r.description },
  { key: 'lot', title: 'Số lô', width: 90, value: (r) => r.lotNo },
  { key: 'in', title: 'Nhập', width: 90, type: 'quantity', value: (r) => (r.inQty ? r.inQty : null) },
  { key: 'out', title: 'Xuất', width: 90, type: 'quantity', value: (r) => (r.outQty ? r.outQty : null) },
  { key: 'balance', title: 'Tồn', width: 100, type: 'quantity', value: (r) => r.balanceQty },
  { key: 'unitCost', title: 'Đơn giá', width: 110, type: 'money', value: (r) => r.unitCost },
  { key: 'amount', title: 'Giá trị', width: 120, type: 'money', value: (r) => r.amount },
  {
    key: 'final',
    title: 'Giá',
    width: 90,
    type: 'status',
    value: (r) => (r.isCostFinal ? 'Đã chốt' : 'Tạm tính'),
    tone: (r) => (r.isCostFinal ? 'success' : 'warning'),
    groupable: true,
  },
]

/**
 * Màn hình Kho → Thẻ kho (design.md mục 11): mọi phát sinh của một mặt hàng trong khoảng ngày theo thứ tự ghi sổ, với tồn đầu,
 * tồn lũy kế và tồn cuối. Mở từ Tồn kho hiện tại thì nhận sẵn mặt hàng, kho qua tham số ?itemId=&warehouseId=.
 */
export function StockCardPage() {
  const [params] = useSearchParams()
  // Tab thẻ kho có thể đang mở sẵn: mở từ dòng tồn khác thì dựng lại trang theo tham số mới.
  return <StockCardView key={params.toString()} initialItemId={params.get('itemId')} initialWarehouseId={params.get('warehouseId')} />
}

/** Nội dung thẻ kho; giá trị lọc ban đầu lấy từ tham số URL. */
function StockCardView({ initialItemId, initialWarehouseId }: { initialItemId: string | null; initialWarehouseId: string | null }) {
  const navigate = useNavigate()
  const [itemId, setItemId] = useState<string | undefined>(initialItemId ?? undefined)
  const [warehouseId, setWarehouseId] = useState<string | undefined>(initialWarehouseId ?? undefined)
  // Mặc định từ đầu tháng tới hôm nay – trùng kỳ kho đang làm.
  const [range, setRange] = useState<DateRange>(() => ({
    from: dayjs().startOf('month').format('YYYY-MM-DD'),
    to: dayjs().format('YYYY-MM-DD'),
  }))
  const [search, setSearch] = useState('')
  const lookup = useItemLookup(search, itemId ? [itemId] : [])
  const warehouses = useWarehouses()
  const card = useStockCard({ itemId, warehouseId, ...range })
  const data = card.data

  const notice = card.error ? (
    <Alert type="error" showIcon title={card.error.message} />
  ) : !itemId ? (
    <Alert type="info" showIcon title="Chọn mặt hàng để xem thẻ kho." />
  ) : (
    data && (
      <Alert
        type="info"
        showIcon
        className="stock-card-summary"
        title={
          `${data.itemCode} – ${data.itemName} (${data.uomCode}) · Tồn đầu ${formatQuantity(data.openingQty)} (${formatMoney(data.openingAmount)})` +
          ` · Nhập ${formatQuantity(data.inQty)} · Xuất ${formatQuantity(data.outQty)}` +
          ` · Tồn cuối ${formatQuantity(data.closingQty)} (${formatMoney(data.closingAmount)})`
        }
      />
    )
  )

  return (
    <ListPage
      title="Thẻ kho"
      exportFileName="the-kho"
      canCreate={false}
      rows={itemId ? (data?.entries ?? []) : []}
      columns={columns}
      rowKey={(r) => `${r.documentId}|${r.warehouseCode}|${r.lotNo ?? ''}|${r.balanceQty}|${r.date}`}
      loading={card.isLoading && !!itemId}
      onOpenRow={(r) => documentPaths[r.docType] && navigate(`${documentPaths[r.docType]}/${r.documentId}`)}
      notice={notice}
      onClearFilters={() => setWarehouseId(undefined)}
      filters={
        <>
          <FilterField label="Mặt hàng" width={280}>
            {(id) => (
              <Select
                id={id}
                value={itemId}
                onChange={setItemId}
                showSearch={{ filterOption: false, onSearch: setSearch }}
                placeholder="Gõ mã hoặc tên hàng"
                options={(lookup.data ?? []).map((i) => ({ value: i.id, label: `${i.code} – ${i.name}` }))}
                popupMatchSelectWidth={420}
              />
            )}
          </FilterField>
          <FilterField label="Kho" width={200}>
            {(id) => (
              <Select
                id={id}
                value={warehouseId ?? ''}
                onChange={(v) => setWarehouseId(v === '' ? undefined : v)}
                options={[{ value: '', label: 'Tất cả kho được phép' }, ...(warehouses.data ?? []).map((w) => ({ value: w.id, label: `${w.code} – ${w.name}` }))]}
              />
            )}
          </FilterField>
          <LogDateRangeFilter value={range} onChange={setRange} />
        </>
      }
    />
  )
}
