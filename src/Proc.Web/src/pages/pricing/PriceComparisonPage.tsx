import { Alert, Checkbox, InputNumber, Select } from 'antd'
import { useState, type ReactNode } from 'react'
import { useItemLookup } from '../../api/inventoryDocuments'
import {
  priceSourceLabels,
  usePriceComparison,
  type ComparePriceListRow,
  type ComparePurchaseRow,
  type PriceComparison,
  type PriceHistoryEntry,
} from '../../api/pricing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatMoney, formatQuantity, vnNumberInput } from '../../utils/format'

/** Ba góc nhìn của màn hình so sánh giá. */
type View = 'purchases' | 'price-lists' | 'history'

const viewLabels: Record<View, string> = {
  purchases: 'Giá thực mua theo công ty × NCC',
  'price-lists': 'Bảng giá NCC đang hiệu lực',
  history: 'Lịch sử giá 12 tháng',
}

const purchaseColumns: GridColumn<ComparePurchaseRow>[] = [
  { key: 'company', title: 'Công ty', width: 90, value: (r) => r.companyCode, groupable: true },
  { key: 'supplier', title: 'Nhà cung cấp', width: 230, value: (r) => `${r.supplierCode} – ${r.supplierName}`, groupable: true },
  { key: 'lastPrice', title: 'Giá gần nhất', width: 110, type: 'money', value: (r) => r.lastPrice },
  {
    key: 'above',
    title: 'So với thấp nhất',
    width: 110,
    type: 'status',
    value: (r) => (r.aboveGroupMinPct > 0 ? `+${formatQuantity(r.aboveGroupMinPct)}%` : 'Thấp nhất'),
    tone: (r) => (r.isAboveThreshold ? 'error' : r.aboveGroupMinPct > 0 ? 'warning' : 'success'),
  },
  { key: 'lastDate', title: 'Ngày', width: 90, type: 'date', value: (r) => r.lastDate },
  { key: 'lastDoc', title: 'Chứng từ', width: 150, value: (r) => `${r.lastSource === 'PO' ? 'PO' : 'Nhập'} ${r.lastDocumentNo}` },
  { key: 'min', title: 'Thấp nhất 12T', width: 110, type: 'money', value: (r) => r.minPrice },
  { key: 'max', title: 'Cao nhất 12T', width: 110, type: 'money', value: (r) => r.maxPrice },
  { key: 'avg', title: 'Bình quân 12T', width: 110, type: 'money', value: (r) => r.avgPrice },
  { key: 'qty', title: 'SL nhập 12T', width: 100, type: 'quantity', value: (r) => r.qty12Months },
]

const priceListColumns: GridColumn<ComparePriceListRow>[] = [
  { key: 'supplier', title: 'Nhà cung cấp', width: 230, value: (r) => `${r.supplierCode} – ${r.supplierName}`, groupable: true },
  { key: 'scope', title: 'Áp dụng', width: 110, value: (r) => (r.isGroupWide ? 'Toàn tập đoàn' : r.companyCode), groupable: true },
  { key: 'no', title: 'Số bảng giá', width: 150, value: (r) => r.priceListNo },
  { key: 'uom', title: 'ĐVT', width: 60, value: (r) => r.uomCode },
  { key: 'minQty', title: 'Từ SL', width: 80, type: 'quantity', value: (r) => r.minQty },
  { key: 'price', title: 'Đơn giá', width: 110, type: 'money', value: (r) => r.unitPrice },
  { key: 'discount', title: '% CK', width: 60, type: 'quantity', value: (r) => r.discountPct },
  { key: 'base', title: 'Giá quy đổi', width: 110, type: 'money', value: (r) => r.baseNetPrice },
  { key: 'from', title: 'Hiệu lực từ', width: 95, type: 'date', value: (r) => r.validFrom },
  { key: 'to', title: 'Đến', width: 95, type: 'date', value: (r) => r.validTo },
]

const historyColumns: GridColumn<PriceHistoryEntry>[] = [
  { key: 'date', title: 'Ngày', width: 90, type: 'date', value: (r) => r.date },
  { key: 'company', title: 'Công ty', width: 90, value: (r) => r.companyCode, groupable: true },
  { key: 'supplier', title: 'NCC', width: 110, value: (r) => r.supplierCode, groupable: true },
  { key: 'source', title: 'Nguồn', width: 130, value: (r) => priceSourceLabels[r.source], groupable: true },
  { key: 'no', title: 'Chứng từ', width: 150, value: (r) => r.documentNo },
  { key: 'qty', title: 'SL (ĐVT cơ sở)', width: 110, type: 'quantity', value: (r) => r.baseQty },
  { key: 'price', title: 'Giá quy đổi', width: 110, type: 'money', value: (r) => r.basePrice },
]

/** Dòng tóm tắt: mặt hàng, phạm vi, thấp nhất/cao nhất/bình quân 12 tháng, số dòng mua cao hơn ngưỡng. */
function summary(data: PriceComparison): string {
  const above = data.purchases.filter((p) => p.isAboveThreshold).length
  const range =
    data.groupMinPrice == null
      ? 'chưa có phiếu nhập trong 12 tháng'
      : `thấp nhất ${formatMoney(data.groupMinPrice)} · cao nhất ${formatMoney(data.groupMaxPrice)} · bình quân ${formatMoney(data.groupAvgPrice)}`
  return (
    `${data.itemCode} – ${data.itemName} · giá quy về ${data.baseUomCode}, trước VAT · ${data.allCompanies ? 'Toàn tập đoàn' : 'Công ty đang làm việc'}: ` +
    `${range} · ${above} dòng mua cao hơn thấp nhất quá ${formatQuantity(data.thresholdPct)}%`
  )
}

/**
 * Màn hình Giá và NCC → So sánh giá (design.md mục 8.2): chọn mặt hàng, xem công ty nào mua của NCC nào với giá bao nhiêu (tô màu
 * dòng cao hơn giá thấp nhất quá X%), giá bảng giá đang hiệu lực và lịch sử giá 12 tháng. Mọi giá quy về ĐVT cơ sở, trước VAT.
 * Người có quyền xem toàn tập đoàn bật ô "Toàn tập đoàn" để so giữa các công ty.
 */
export function PriceComparisonPage() {
  const { hasPermission } = useAuth()
  const canViewAll = hasPermission(Permission.PriceViewAllCompanies)
  const [itemId, setItemId] = useState<string | undefined>()
  const [allCompanies, setAllCompanies] = useState(canViewAll)
  const [threshold, setThreshold] = useState(10)
  const [view, setView] = useState<View>('purchases')
  const [search, setSearch] = useState('')
  const lookup = useItemLookup(search, itemId ? [itemId] : [])
  const comparison = usePriceComparison(itemId, allCompanies && canViewAll, threshold)
  const data = itemId ? comparison.data : undefined

  const notice = comparison.error ? (
    <Alert type="error" showIcon title={comparison.error.message} />
  ) : !itemId ? (
    <Alert type="info" showIcon title="Chọn mặt hàng để so sánh giá." />
  ) : (
    data && <Alert type="info" showIcon title={summary(data)} />
  )

  const filters: ReactNode = (
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
      <FilterField label="Xem" width={240}>
        {(id) => <Select id={id} value={view} onChange={setView} options={Object.entries(viewLabels).map(([value, label]) => ({ value, label }))} />}
      </FilterField>
      <FilterField label="Ngưỡng tô màu (%)" width={120}>
        {(id) => <InputNumber {...vnNumberInput} id={id} value={threshold} min={0} max={1000} onChange={(v) => setThreshold(Number(v ?? 0))} style={{ width: '100%' }} />}
      </FilterField>
      {canViewAll && (
        <FilterField label="Phạm vi" width={140}>
          {(id) => (
            <Checkbox id={id} checked={allCompanies} onChange={(e) => setAllCompanies(e.target.checked)}>
              Toàn tập đoàn
            </Checkbox>
          )}
        </FilterField>
      )}
    </>
  )

  const common = {
    title: 'So sánh giá',
    exportFileName: 'so-sanh-gia',
    canCreate: false,
    loading: comparison.isLoading && !!itemId,
    notice,
    filters,
    onClearFilters: () => setThreshold(10),
  }

  // Mỗi góc nhìn một ListPage riêng (kiểu dòng khác nhau); key để bộ lọc cột, nhóm của lưới không lẫn giữa các góc nhìn.
  if (view === 'price-lists')
    return <ListPage key={view} {...common} rows={data?.priceLists ?? []} columns={priceListColumns} rowKey={(r) => `${r.priceListNo}|${r.uomCode}|${r.minQty}`} />
  if (view === 'history')
    return <ListPage key={view} {...common} rows={data?.history ?? []} columns={historyColumns} rowKey={(r) => `${r.source}|${r.documentNo}|${r.date}|${r.basePrice}|${r.baseQty}`} />
  return (
    <ListPage
      key={view}
      {...common}
      rows={data?.purchases ?? []}
      columns={purchaseColumns}
      rowKey={(r) => `${r.companyCode}|${r.supplierCode}`}
      isMarked={(r) => r.isAboveThreshold}
    />
  )
}
