import { Alert, InputNumber, Select, TreeSelect } from 'antd'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router'
import { useItemLookup } from '../../api/inventoryDocuments'
import { useItemCategories } from '../../api/masterData'
import { useWarehouses } from '../../api/organization'
import {
  useExpiring,
  useGroupStock,
  useSlowMoving,
  type ExpiringRow,
  type GroupStockResult,
  type GroupStockRow,
  type SlowMovingRow,
  type StockReportFilter,
} from '../../api/reports'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatDate, formatMoney, formatQuantity, vnNumberInput } from '../../utils/format'
import { buildCategoryTree } from '../masterData/categoryTree'

/**
 * Ô lọc chung của báo cáo kho: kho, nhóm hàng (gồm nhóm con), số ngày.
 * @param value Điều kiện đang chọn.
 * @param onChange Đổi điều kiện.
 * @param daysLabel Nhãn ô số ngày.
 * @param minDays Số ngày nhỏ nhất cho phép.
 */
function StockReportFilters({
  value,
  onChange,
  daysLabel,
  minDays,
}: {
  value: StockReportFilter
  onChange: (value: StockReportFilter) => void
  daysLabel: string
  minDays: number
}) {
  const warehouses = useWarehouses()
  const categories = useItemCategories()
  const tree = useMemo(() => buildCategoryTree(categories.data ?? []), [categories.data])
  return (
    <>
      <FilterField label="Kho" width={200}>
        {(id) => (
          <Select
            id={id}
            value={value.warehouseId ?? ''}
            onChange={(v) => onChange({ ...value, warehouseId: v === '' ? undefined : v })}
            options={[{ value: '', label: 'Tất cả kho được phép' }, ...(warehouses.data ?? []).map((w) => ({ value: w.id, label: `${w.code} – ${w.name}` }))]}
          />
        )}
      </FilterField>
      <FilterField label="Nhóm hàng (gồm nhóm con)" width={220}>
        {(id) => (
          <TreeSelect
            id={id}
            value={value.categoryId}
            onChange={(v) => onChange({ ...value, categoryId: v })}
            treeData={tree}
            allowClear
            showSearch
            treeNodeFilterProp="title"
            placeholder="Tất cả"
          />
        )}
      </FilterField>
      <FilterField label={daysLabel} width={130}>
        {(id) => (
          <InputNumber
            {...vnNumberInput}
            id={id}
            value={value.days}
            min={minDays}
            max={3650}
            precision={0}
            // Ô trống thì giữ số cũ, không gửi số ngày rỗng lên server.
            onChange={(v) => v != null && onChange({ ...value, days: Number(v) })}
            style={{ width: '100%' }}
          />
        )}
      </FilterField>
    </>
  )
}

const slowColumns: GridColumn<SlowMovingRow>[] = [
  { key: 'warehouse', title: 'Kho', width: 150, value: (r) => `${r.warehouseCode} – ${r.warehouseName}`, groupable: true },
  { key: 'code', title: 'Mã hàng', width: 130, value: (r) => r.itemCode },
  { key: 'name', title: 'Tên hàng', width: 230, value: (r) => r.itemName },
  { key: 'category', title: 'Nhóm hàng', width: 150, value: (r) => `${r.categoryCode} – ${r.categoryName}`, groupable: true },
  { key: 'uom', title: 'ĐVT', width: 60, value: (r) => r.uomCode },
  { key: 'qty', title: 'Tồn', width: 90, type: 'quantity', value: (r) => r.qty },
  { key: 'amount', title: 'Giá trị tồn', width: 120, type: 'money', value: (r) => r.amount },
  { key: 'firstIn', title: 'Nhập đầu', width: 90, type: 'date', value: (r) => r.firstInDate },
  { key: 'lastIn', title: 'Nhập cuối', width: 90, type: 'date', value: (r) => r.lastInDate },
  { key: 'lastOut', title: 'Xuất cuối', width: 90, type: 'date', value: (r) => r.lastOutDate },
  { key: 'idle', title: 'Số ngày không xuất', width: 120, type: 'quantity', value: (r) => r.idleDays },
]

/**
 * Báo cáo → Hàng chậm luân chuyển (design.md mục 11, 11.2): kho × mặt hàng còn tồn mà không xuất từ N ngày (mặc định 90) – tính từ
 * ngày xuất gần nhất, chưa xuất bao giờ thì từ ngày nhập đầu tiên. Dòng chưa xuất lần nào được tô màu. Bấm đúp mở thẻ kho.
 */
export function SlowMovingReportPage() {
  const navigate = useNavigate()
  const [filter, setFilter] = useState<StockReportFilter>({ days: 90 })
  const report = useSlowMoving(filter)
  const total = (report.data?.items ?? []).reduce((sum, r) => sum + r.amount, 0)
  return (
    <ListPage
      title="Hàng chậm luân chuyển"
      exportFileName="hang-cham-luan-chuyen"
      exportSubtitle={`Không xuất từ ${filter.days} ngày, tính đến ${report.data ? formatDate(report.data.asOf) : ''}`}
      canCreate={false}
      rows={report.data?.items ?? []}
      columns={slowColumns}
      rowKey={(r) => `${r.warehouseId}|${r.itemId}`}
      loading={report.isLoading}
      isMarked={(r) => r.lastOutDate == null}
      onOpenRow={(r) => navigate(`/kho/the-kho?itemId=${r.itemId}&warehouseId=${r.warehouseId}`)}
      notice={
        report.error ? (
          <Alert type="error" showIcon title={report.error.message} />
        ) : (
          <Alert
            type="info"
            showIcon
            title={`Tổng giá trị tồn chậm luân chuyển: ${formatMoney(total)} đ (giá vốn tạm tính khi kỳ còn mở). Dòng tô màu: chưa xuất lần nào. Bấm đúp để xem thẻ kho.`}
          />
        )
      }
      onClearFilters={() => setFilter({ days: 90 })}
      filters={<StockReportFilters value={filter} onChange={setFilter} daysLabel="Không xuất từ (ngày)" minDays={1} />}
    />
  )
}

const expiringColumns: GridColumn<ExpiringRow>[] = [
  {
    key: 'status',
    title: 'Tình trạng',
    width: 110,
    type: 'status',
    value: (r) => (r.daysLeft < 0 ? 'Đã hết hạn' : 'Sắp hết hạn'),
    tone: (r) => (r.daysLeft < 0 ? 'error' : 'warning'),
    groupable: true,
  },
  { key: 'warehouse', title: 'Kho', width: 150, value: (r) => `${r.warehouseCode} – ${r.warehouseName}`, groupable: true },
  { key: 'code', title: 'Mã hàng', width: 130, value: (r) => r.itemCode },
  { key: 'name', title: 'Tên hàng', width: 230, value: (r) => r.itemName },
  { key: 'lot', title: 'Số lô', width: 100, value: (r) => r.lotNo },
  { key: 'expiry', title: 'Hạn dùng', width: 90, type: 'date', value: (r) => r.expiryDate },
  { key: 'daysLeft', title: 'Còn (ngày)', width: 90, type: 'quantity', value: (r) => r.daysLeft },
  { key: 'uom', title: 'ĐVT', width: 60, value: (r) => r.uomCode },
  { key: 'qty', title: 'Tồn', width: 90, type: 'quantity', value: (r) => r.qty },
  { key: 'amount', title: 'Giá trị tồn', width: 120, type: 'money', value: (r) => r.amount },
]

/**
 * Báo cáo → Hàng sắp hết hạn (design.md mục 11, 11.2): lô còn tồn có hạn dùng trong N ngày tới (mặc định 30), luôn gồm lô đã hết
 * hạn (tô màu). Hạn dùng lấy từ phiếu đã ghi sổ của lô. Bấm đúp mở thẻ kho.
 */
export function ExpiringReportPage() {
  const navigate = useNavigate()
  const [filter, setFilter] = useState<StockReportFilter>({ days: 30 })
  const report = useExpiring(filter)
  const expired = (report.data?.items ?? []).filter((r) => r.daysLeft < 0).length
  return (
    <ListPage
      title="Hàng sắp hết hạn"
      exportFileName="hang-sap-het-han"
      exportSubtitle={`Hết hạn trong ${filter.days} ngày tới, tính đến ${report.data ? formatDate(report.data.asOf) : ''}`}
      canCreate={false}
      rows={report.data?.items ?? []}
      columns={expiringColumns}
      rowKey={(r) => `${r.warehouseId}|${r.itemId}|${r.lotNo}`}
      loading={report.isLoading}
      isMarked={(r) => r.daysLeft < 0}
      onOpenRow={(r) => navigate(`/kho/the-kho?itemId=${r.itemId}&warehouseId=${r.warehouseId}`)}
      notice={
        report.error ? (
          <Alert type="error" showIcon title={report.error.message} />
        ) : (
          <Alert
            type={expired > 0 ? 'warning' : 'info'}
            showIcon
            title={`${expired} lô đã hết hạn (tô màu) cần xử lý. Hạn dùng lấy từ phiếu nhập đã ghi sổ của lô. Bấm đúp để xem thẻ kho.`}
          />
        )
      }
      onClearFilters={() => setFilter({ days: 30 })}
      filters={<StockReportFilters value={filter} onChange={setFilter} daysLabel="Trong (ngày tới)" minDays={0} />}
    />
  )
}

const groupColumns: GridColumn<GroupStockRow>[] = [
  { key: 'company', title: 'Công ty', width: 260, value: (r) => `${r.companyCode} – ${r.companyName}` },
  { key: 'main', title: 'Tồn kho Main', width: 120, type: 'quantity', value: (r) => r.mainQty },
  { key: 'available', title: 'Kho Main chưa giữ chỗ', width: 150, type: 'quantity', value: (r) => r.mainAvailable },
  { key: 'sub', title: 'Tồn kho Sub', width: 110, type: 'quantity', value: (r) => r.subQty },
  { key: 'total', title: 'Tổng tồn', width: 110, type: 'quantity', value: (r) => r.totalQty },
  {
    key: 'note',
    title: 'Gợi ý',
    width: 200,
    type: 'status',
    value: (r) => (r.isCurrent ? 'Công ty đang làm việc' : r.mainAvailable > 0 ? 'Có thể mua nội bộ' : 'Chỉ tồn kho Sub'),
    tone: (r) => (r.isCurrent ? 'neutral' : r.mainAvailable > 0 ? 'success' : 'warning'),
  },
]

/** Dòng tóm tắt tồn tập đoàn. */
function groupSummary(data: GroupStockResult) {
  const others = data.items.filter((r) => !r.isCurrent).reduce((sum, r) => sum + r.mainAvailable, 0)
  const min = data.minStock != null ? ` Tồn tối thiểu: ${formatQuantity(data.minStock)}.` : ''
  return `${data.itemCode} – ${data.itemName} (${data.uomCode}): công ty khác còn ${formatQuantity(others)} ở kho Main có thể bán nội bộ.${min}`
}

/**
 * Báo cáo → Tồn tập đoàn (design.md mục 11, 11.2): tồn một mặt hàng ở mọi công ty – chỉ số lượng, không có giá trị – để gợi ý mua
 * nội bộ thay vì mua mới. Liên công ty chỉ đi kho Main → kho Main nên tách tồn kho Main (phần chưa giữ chỗ) và kho Sub.
 */
export function GroupStockReportPage() {
  const [itemId, setItemId] = useState<string | undefined>()
  const [search, setSearch] = useState('')
  const lookup = useItemLookup(search, itemId ? [itemId] : [])
  const report = useGroupStock(itemId)
  const data = itemId ? report.data : undefined
  return (
    <ListPage
      title="Tồn tập đoàn"
      exportFileName="ton-tap-doan"
      exportSubtitle={data ? `${data.itemCode} – ${data.itemName} (${data.uomCode})` : undefined}
      canCreate={false}
      rows={data?.items ?? []}
      columns={groupColumns}
      rowKey={(r) => r.companyId}
      loading={report.isLoading && !!itemId}
      isMarked={(r) => r.isCurrent}
      notice={
        report.error ? (
          <Alert type="error" showIcon title={report.error.message} />
        ) : !itemId ? (
          <Alert type="info" showIcon title="Chọn mặt hàng để xem tồn ở mọi công ty trong tập đoàn." />
        ) : (
          data && <Alert type="info" showIcon title={groupSummary(data)} />
        )
      }
      onClearFilters={() => setItemId(undefined)}
      filters={
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
      }
    />
  )
}
