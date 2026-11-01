import { Alert, Checkbox, InputNumber } from 'antd'
import dayjs from 'dayjs'
import { useState } from 'react'
import {
  usePurchaseAnalysis,
  type PurchaseAnalysis,
  type PurchaseAnalysisCompany,
  type PurchaseAnalysisFilter,
  type PurchaseAnalysisRow,
} from '../../api/purchaseHistory'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DataGrid } from '../../components/DataGrid/DataGrid'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatDate, formatMoney, formatQuantity, vnNumberInput } from '../../utils/format'
import { LogDateRangeFilter } from '../system/logFilters'

/** Ngưỡng tô màu chênh lệch giá giữa các pháp nhân mặc định – như màn hình So sánh giá (design.md mục 8.5). */
const defaultGapThreshold = 10

/** Điều kiện mặc định: 12 tháng gần nhất tính cả hai đầu, giảm mục tiêu 5% (ví dụ của đề xuất Ban Cung ứng). */
const defaultFilter = (allCompanies: boolean): PurchaseAnalysisFilter => ({
  from: dayjs().subtract(12, 'month').add(1, 'day').format('YYYY-MM-DD'),
  to: dayjs().format('YYYY-MM-DD'),
  targetDiscountPct: 5,
  allCompanies,
})

/** Cột của bảng phân tích; cột chênh lệch tô màu theo ngưỡng người dùng chọn. */
const columns = (gapThreshold: number): GridColumn<PurchaseAnalysisRow>[] => [
  { key: 'code', title: 'Mã hàng', width: 120, value: (r) => r.itemCode },
  { key: 'name', title: 'Tên hàng', width: 220, value: (r) => r.itemName },
  { key: 'uom', title: 'ĐVT', width: 60, value: (r) => r.uom, groupable: true },
  { key: 'companies', title: 'Số pháp nhân', width: 95, type: 'quantity', value: (r) => r.companyCount, groupable: true },
  { key: 'qty', title: 'Tổng SL', width: 100, type: 'quantity', value: (r) => r.quantity },
  { key: 'amount', title: 'Tổng giá trị', width: 130, type: 'money', value: (r) => r.amount },
  { key: 'avg', title: 'Giá BQ hợp nhất', width: 115, type: 'money', value: (r) => r.avgPrice },
  { key: 'min', title: 'Thấp nhất', width: 105, type: 'money', value: (r) => r.minPrice },
  { key: 'max', title: 'Cao nhất', width: 105, type: 'money', value: (r) => r.maxPrice },
  {
    key: 'gap',
    title: 'Chênh lệch',
    width: 100,
    type: 'status',
    value: (r) => (r.companyCount < 2 ? 'Một pháp nhân' : `${formatQuantity(r.priceGapPct)}%`),
    tone: (r) => (r.companyCount < 2 ? 'neutral' : r.priceGapPct > gapThreshold ? 'error' : r.priceGapPct > 0 ? 'warning' : 'success'),
  },
  { key: 'count', title: 'Số lần mua', width: 85, type: 'quantity', value: (r) => r.purchaseCount },
  { key: 'suppliers', title: 'Số NCC', width: 70, type: 'quantity', value: (r) => r.supplierCount },
  { key: 'best', title: 'NCC giá tốt nhất', width: 200, value: (r) => r.bestSupplierName },
  { key: 'bestPrice', title: 'Giá NCC tốt nhất', width: 115, type: 'money', value: (r) => r.bestSupplierPrice },
  { key: 'target', title: 'Giá mục tiêu', width: 110, type: 'money', value: (r) => r.targetPrice },
  { key: 'savings', title: 'Tiết kiệm dự kiến', width: 130, type: 'money', value: (r) => r.savings },
  { key: 'savingsPct', title: 'Tiết kiệm (%)', width: 95, type: 'quantity', value: (r) => r.savingsPct },
  { key: 'last', title: 'Mua gần nhất', width: 100, type: 'date', value: (r) => r.lastDate },
]

/** Dòng của bảng chi tiết trong ngăn kéo: từng pháp nhân, dòng cuối "Hợp nhất" như bảng ví dụ của đề xuất. */
type DetailRow = Omit<PurchaseAnalysisCompany, 'companyId' | 'lastPrice' | 'lastDate'> & { key: string; lastPrice: number | null; lastDate: string | null }

/** Khung cao cố định cho lưới trong ngăn kéo – lưới cuộn ảo cần chiều cao của khung chứa, như tab liên kết NCC. */
const gridBoxStyle = { height: 320, display: 'flex', flexDirection: 'column' } as const

const detailColumns: GridColumn<DetailRow>[] = [
  { key: 'company', title: 'Pháp nhân', width: 220, value: (r) => (r.companyCode ? `${r.companyCode} – ${r.companyName}` : r.companyName) },
  { key: 'qty', title: 'SL', width: 90, type: 'quantity', value: (r) => r.quantity },
  { key: 'amount', title: 'Giá trị', width: 125, type: 'money', value: (r) => r.amount },
  { key: 'avg', title: 'Giá BQ', width: 100, type: 'money', value: (r) => r.avgPrice },
  { key: 'min', title: 'Thấp nhất', width: 100, type: 'money', value: (r) => r.minPrice },
  { key: 'max', title: 'Cao nhất', width: 100, type: 'money', value: (r) => r.maxPrice },
  { key: 'last', title: 'Giá gần nhất', width: 105, type: 'money', value: (r) => r.lastPrice },
  { key: 'lastDate', title: 'Ngày', width: 90, type: 'date', value: (r) => r.lastDate },
  { key: 'count', title: 'Số lần mua', width: 85, type: 'quantity', value: (r) => r.purchaseCount },
  { key: 'suppliers', title: 'Số NCC', width: 70, type: 'quantity', value: (r) => r.supplierCount },
  { key: 'savings', title: 'Tiết kiệm dự kiến', width: 130, type: 'money', value: (r) => r.savings },
]

/** Các dòng chi tiết của một mặt hàng: từng pháp nhân rồi dòng hợp nhất. */
function detailRows(row: PurchaseAnalysisRow): DetailRow[] {
  return [
    ...row.companies.map((c) => ({ ...c, key: c.companyId })),
    {
      key: 'total',
      companyCode: '',
      companyName: 'Hợp nhất',
      quantity: row.quantity,
      amount: row.amount,
      avgPrice: row.avgPrice,
      minPrice: row.minPrice,
      maxPrice: row.maxPrice,
      lastPrice: null,
      lastDate: row.lastDate,
      purchaseCount: row.purchaseCount,
      supplierCount: row.supplierCount,
      savings: row.savings,
    },
  ]
}

/**
 * Ngăn kéo chi tiết (chỉ xem): số liệu từng pháp nhân của một mặt hàng, giá mục tiêu và chi phí sau đàm phán.
 * @param row Dòng phân tích đang mở.
 * @param discountPct % giảm mục tiêu đã dùng.
 * @param onClose Đóng ngăn kéo.
 */
function AnalysisDrawer({ row, discountPct, onClose }: { row: PurchaseAnalysisRow; discountPct: number; onClose: () => void }) {
  return (
    <DetailDrawer
      open
      readOnly
      title={`Phân tích giá: ${row.itemCode} – ${row.itemName} (${row.uom})`}
      onClose={onClose}
      tabs={[
        {
          key: 'companies',
          label: 'Theo pháp nhân',
          children: (
            <>
              <fieldset className="detail-section">
                <legend>Cơ hội tiết kiệm</legend>
                <div>
                  Giá mục tiêu <strong>{formatMoney(row.targetPrice)} đ</strong> (giảm {formatQuantity(discountPct)}% từ giá bình quân thấp nhất
                  giữa các pháp nhân) · tổng chi phí sau đàm phán <strong>{formatMoney(row.amount - row.savings)} đ</strong> · tiết kiệm{' '}
                  <strong>{formatMoney(row.savings)} đ</strong> ({formatQuantity(row.savingsPct)}% giá trị trước hợp nhất).
                </div>
                <div className="detail-hint">
                  NCC giá tốt nhất: {row.bestSupplierName} – bình quân {formatMoney(row.bestSupplierPrice)} đ. Chưa tính vận chuyển, thuế, tồn
                  kho và chi phí khác.
                </div>
              </fieldset>
              <div style={gridBoxStyle}>
                <DataGrid rows={detailRows(row)} columns={detailColumns} rowKey={(r) => r.key} groupBy={null} onGroupByChange={() => {}} />
              </div>
            </>
          ),
        },
      ]}
    />
  )
}

/** Dòng tóm tắt đầu trang: tổng giá trị, tiết kiệm của các dòng đang hiện, và phần dữ liệu chưa đưa vào phân tích. */
function summary(data: PurchaseAnalysis, rows: PurchaseAnalysisRow[]): string {
  const amount = rows.reduce((s, r) => s + r.amount, 0)
  const savings = rows.reduce((s, r) => s + r.savings, 0)
  const pct = amount === 0 ? 0 : Math.round((savings / amount) * 10000) / 100
  const skipped = [
    data.unmappedRecords > 0 && `${formatQuantity(data.unmappedRecords)} dòng chưa ánh xạ mặt hàng (${formatMoney(data.unmappedAmount)} đ – xử lý ở màn hình PHMAP)`,
    data.foreignCurrencyRecords > 0 && `${formatQuantity(data.foreignCurrencyRecords)} dòng ngoại tệ`,
    data.zeroPriceRecords > 0 && `${formatQuantity(data.zeroPriceRecords)} dòng đơn giá 0 (hàng tặng)`,
  ].filter(Boolean)
  return (
    `${data.allCompanies ? 'Toàn tập đoàn' : 'Công ty đang làm việc'}, ngày mua ${formatDate(data.from)} – ${formatDate(data.to)}: ` +
    `${formatQuantity(rows.length)} mặt hàng, giá trị ${formatMoney(amount)} đ (trước VAT), tiết kiệm dự kiến ${formatMoney(savings)} đ (${formatQuantity(pct)}%) ` +
    `ở giá mục tiêu giảm ${formatQuantity(data.targetDiscountPct)}% từ giá bình quân thấp nhất.` +
    (skipped.length > 0 ? ` Chưa đưa vào phân tích: ${skipped.join('; ')}.` : '')
  )
}

/**
 * Báo cáo → Phân tích giá hợp nhất (mã PHAN, design.md mục 18.3, lát E3): trên dữ liệu mua lịch sử đã ánh xạ, mỗi mặt hàng chung × ĐVT
 * một dòng – SL, giá trị, giá bình quân, thấp/cao nhất của các pháp nhân, chênh lệch, NCC giá tốt nhất, giá mục tiêu và tiết kiệm dự kiến
 * nếu mua tập trung. Nhiều tiết kiệm trước; tô màu dòng chênh lệch giá quá ngưỡng. Mở dòng để xem từng pháp nhân.
 */
export function PurchaseAnalysisPage() {
  const { hasPermission } = useAuth()
  const canViewAll = hasPermission(Permission.PurchaseHistoryViewAllCompanies)
  const [filter, setFilter] = useState(() => defaultFilter(canViewAll))
  const [gapThreshold, setGapThreshold] = useState(defaultGapThreshold)
  const [multiOnly, setMultiOnly] = useState(false)
  const [opened, setOpened] = useState<PurchaseAnalysisRow | null>(null)
  const analysis = usePurchaseAnalysis({ ...filter, allCompanies: filter.allCompanies && canViewAll })
  const data = analysis.data
  const rows = (data?.rows ?? []).filter((r) => !multiOnly || r.companyCount > 1)

  return (
    <>
      <ListPage
        title="Phân tích giá hợp nhất"
        exportFileName="phan-tich-gia-hop-nhat"
        exportSubtitle={data && `Ngày mua ${formatDate(data.from)} – ${formatDate(data.to)}${data.allCompanies ? ' – toàn tập đoàn' : ''}, giảm mục tiêu ${formatQuantity(data.targetDiscountPct)}%`}
        canCreate={false}
        rows={rows}
        columns={columns(gapThreshold)}
        rowKey={(r) => `${r.itemId}|${r.uom}`}
        loading={analysis.isLoading}
        isMarked={(r) => r.companyCount > 1 && r.priceGapPct > gapThreshold}
        onOpenRow={setOpened}
        notice={
          analysis.error ? (
            <Alert type="error" showIcon title={analysis.error.message} />
          ) : (
            data && <Alert type={data.unmappedRecords > 0 ? 'warning' : 'info'} showIcon title={summary(data, rows)} />
          )
        }
        onClearFilters={() => {
          setFilter(defaultFilter(canViewAll))
          setGapThreshold(defaultGapThreshold)
          setMultiOnly(false)
        }}
        filters={
          <>
            <LogDateRangeFilter value={filter} onChange={(range) => setFilter({ ...filter, ...range })} />
            <FilterField label="Giảm mục tiêu (%)" width={120}>
              {(id) => (
                <InputNumber
                  {...vnNumberInput}
                  id={id}
                  value={filter.targetDiscountPct}
                  min={0}
                  max={99}
                  onChange={(v) => setFilter({ ...filter, targetDiscountPct: Number(v ?? 0) })}
                  style={{ width: '100%' }}
                />
              )}
            </FilterField>
            <FilterField label="Ngưỡng tô màu (%)" width={120}>
              {(id) => (
                <InputNumber {...vnNumberInput} id={id} value={gapThreshold} min={0} max={1000} onChange={(v) => setGapThreshold(Number(v ?? 0))} style={{ width: '100%' }} />
              )}
            </FilterField>
            <FilterField label="Mặt hàng" width={200}>
              {(id) => (
                <Checkbox id={id} checked={multiOnly} onChange={(e) => setMultiOnly(e.target.checked)}>
                  Nhiều pháp nhân cùng mua
                </Checkbox>
              )}
            </FilterField>
            {canViewAll && (
              <FilterField label="Phạm vi" width={140}>
                {(id) => (
                  <Checkbox id={id} checked={filter.allCompanies} onChange={(e) => setFilter({ ...filter, allCompanies: e.target.checked })}>
                    Toàn tập đoàn
                  </Checkbox>
                )}
              </FilterField>
            )}
          </>
        }
      />
      {opened && data && <AnalysisDrawer row={opened} discountPct={data.targetDiscountPct} onClose={() => setOpened(null)} />}
    </>
  )
}
