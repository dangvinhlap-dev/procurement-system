import { Alert, Checkbox } from 'antd'
import { useMemo, useState } from 'react'
import { useItemCategories } from '../../api/masterData'
import { useDemandSummary, type DemandSummary, type DemandSummaryCompany, type DemandSummaryRow } from '../../api/demandPlans'
import { Select } from '../../classic/Select'
import { TreeSelect } from '../../classic/TreeSelect'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DataGrid } from '../../components/DataGrid/DataGrid'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { buildCategoryTree } from '../masterData/categoryTree'

const thisYear = () => new Date().getFullYear()

/** Kỳ xem: cả năm, một quý hoặc một tháng – tách 12 tháng × N công ty thành một cột mỗi công ty cho dễ đọc. */
type Period = 'year' | `q${1 | 2 | 3 | 4}` | `m${number}`

const periodOptions: { value: Period; label: string }[] = [
  { value: 'year', label: 'Cả năm' },
  ...[1, 2, 3, 4].map((q) => ({ value: `q${q}` as Period, label: `Quý ${'I II III IV'.split(' ')[q - 1]}` })),
  ...Array.from({ length: 12 }, (_, i) => ({ value: `m${i + 1}` as Period, label: `Tháng ${i + 1}` })),
]

/** Các tháng (0–11) của kỳ. */
function periodMonths(period: Period): number[] {
  if (period === 'year') return Array.from({ length: 12 }, (_, i) => i)
  const n = Number(period.slice(1))
  return period.startsWith('q') ? [0, 1, 2].map((i) => (n - 1) * 3 + i) : [n - 1]
}

/** Cộng SL các tháng của kỳ, làm tròn 4 số lẻ như numeric(18,4) để tránh sai số dấu phẩy động khi cộng. */
const sumMonths = (months: number[], indexes: number[]) => Math.round(indexes.reduce((s, i) => s + (months[i] ?? 0), 0) * 10000) / 10000

/** Cột của bảng: thông tin mặt hàng, một cột mỗi công ty (SL của kỳ), tổng; công ty không có kế hoạch để trống. */
function columns(companies: DemandSummaryCompany[], period: Period): GridColumn<DemandSummaryRow>[] {
  const months = periodMonths(period)
  return [
    { key: 'code', title: 'Mã hàng', width: 120, value: (r) => r.itemCode, auditField: false },
    { key: 'name', title: 'Tên hàng', width: 220, value: (r) => r.itemName, auditField: false },
    { key: 'uom', title: 'ĐVT cơ sở', width: 75, value: (r) => r.baseUomCode, groupable: true, auditField: false },
    { key: 'category', title: 'Nhóm hàng', width: 160, value: (r) => `${r.categoryCode} – ${r.categoryName}`, groupable: true, auditField: false },
    ...companies.map<GridColumn<DemandSummaryRow>>((c) => ({
      key: `c-${c.companyId}`,
      title: c.code,
      width: 100,
      type: 'quantity',
      value: (r) => {
        const cell = r.companies.find((x) => x.companyId === c.companyId)
        return cell ? sumMonths(cell.months, months) : null
      },
      auditField: false,
    })),
    { key: 'total', title: 'Tổng', width: 110, type: 'quantity', value: (r) => sumMonths(r.months, months), auditField: false },
    { key: 'count', title: 'Số công ty', width: 80, type: 'quantity', value: (r) => r.companies.length, auditField: false },
  ]
}

/** Dòng của bảng chi tiết trong ngăn kéo: một tháng, một quý hoặc cả năm. */
interface DetailRow {
  key: string
  label: string
  months: number[]
}

/** Tháng 1–3, Quý I, tháng 4–6, Quý II… rồi Cả năm – quý, năm cộng từ tháng như phiếu kế hoạch. */
const detailRows: DetailRow[] = [
  ...[1, 2, 3, 4].flatMap((q) => [
    ...[0, 1, 2].map((i) => {
      const m = (q - 1) * 3 + i
      return { key: `m${m + 1}`, label: `Tháng ${m + 1}`, months: [m] }
    }),
    { key: `q${q}`, label: `Quý ${'I II III IV'.split(' ')[q - 1]}`, months: periodMonths(`q${q}` as Period) },
  ]),
  { key: 'year', label: 'Cả năm', months: periodMonths('year') },
]

/** Khung cao cố định cho lưới trong ngăn kéo – lưới cuộn ảo cần chiều cao của khung chứa. */
const gridBoxStyle = { height: 460, display: 'flex', flexDirection: 'column' } as const

/**
 * Ngăn kéo chi tiết một mặt hàng (chỉ xem): SL từng tháng, quý, cả năm của từng công ty và tổng, kèm số phiếu đã chốt.
 * @param row Dòng đang mở.
 * @param companies Các công ty của bảng tổng hợp.
 * @param onClose Đóng ngăn kéo.
 */
function DemandSummaryDrawer({ row, companies, onClose }: { row: DemandSummaryRow; companies: DemandSummaryCompany[]; onClose: () => void }) {
  const cells = row.companies.map((cell) => ({ cell, company: companies.find((c) => c.companyId === cell.companyId) }))
  const detailColumns: GridColumn<DetailRow>[] = [
    { key: 'period', title: 'Kỳ', width: 90, value: (r) => r.label, auditField: false },
    ...cells.map<GridColumn<DetailRow>>(({ cell, company }) => ({
      key: `c-${cell.companyId}`,
      title: company?.code ?? '',
      width: 100,
      type: 'quantity',
      value: (r) => sumMonths(cell.months, r.months),
      auditField: false,
    })),
    { key: 'total', title: 'Tổng', width: 110, type: 'quantity', value: (r) => sumMonths(row.months, r.months), auditField: false },
  ]
  return (
    <DetailDrawer
      open
      readOnly
      title={`Nhu cầu: ${row.itemCode} – ${row.itemName} (${row.baseUomCode})`}
      onClose={onClose}
      tabs={[
        {
          key: 'months',
          label: 'Theo tháng',
          children: (
            <>
              <fieldset className="detail-section">
                <legend>Phiếu kế hoạch đã chốt</legend>
                {cells.map(({ cell, company }) => (
                  <div key={cell.companyId}>
                    <strong>{company ? `${company.code} – ${company.name}` : ''}</strong>: {cell.planNos.join(', ')}
                  </div>
                ))}
                <div className="detail-hint">Số lượng quy về ĐVT cơ sở ({row.baseUomCode}); quý, năm cộng từ tháng.</div>
              </fieldset>
              <div style={gridBoxStyle}>
                <DataGrid
                  rows={detailRows}
                  columns={detailColumns}
                  rowKey={(r) => r.key}
                  groupBy={null}
                  onGroupByChange={() => {}}
                  isMarked={(r) => !r.key.startsWith('m')}
                />
              </div>
            </>
          ),
        },
      ]}
    />
  )
}

/** Dòng tóm tắt đầu trang: phạm vi, số mặt hàng, công ty còn phiếu nháp hoặc chưa có kế hoạch đã chốt. */
function summaryText(data: DemandSummary, rows: number): { text: string; warn: boolean } {
  const drafts = data.companies.filter((c) => c.draftPlans > 0).map((c) => `${c.code} (${c.draftPlans})`)
  const missing = data.companies.filter((c) => c.confirmedPlans === 0).map((c) => c.code)
  const parts = [
    `Năm ${data.year} – ${data.allCompanies ? `toàn tập đoàn, gộp ${data.companies.length} công ty` : 'công ty đang làm việc'}: ${rows} mặt hàng, chỉ tính phiếu đã chốt, số lượng theo ĐVT cơ sở.`,
    drafts.length > 0 && `Còn phiếu nháp chưa chốt (chưa tính): ${drafts.join(', ')}.`,
    data.allCompanies && missing.length > 0 && `Chưa có kế hoạch đã chốt: ${missing.join(', ')}.`,
  ].filter(Boolean)
  return { text: parts.join(' '), warn: drafts.length > 0 }
}

/**
 * Mua hàng → Tổng hợp nhu cầu (mã KHNTH, design.md mục 18.25, lát G5b): nhu cầu gộp của các công ty từ phiếu kế hoạch nhu cầu Đã chốt –
 * mỗi mặt hàng một dòng, một cột mỗi công ty và cột tổng cho kỳ chọn (cả năm, quý, tháng), ĐVT cơ sở. Có quyền "Xem kế hoạch nhu cầu toàn
 * tập đoàn" thì mặc định gộp mọi công ty (Văn phòng Ban dự báo sản lượng gộp để thương lượng); không thì chỉ công ty đang làm việc.
 * Mở dòng để xem từng tháng và số phiếu.
 */
export function DemandSummaryPage() {
  const { hasPermission } = useAuth()
  const canViewAll = hasPermission(Permission.DemandPlanViewAllCompanies)
  const [allCompanies, setAllCompanies] = useState(canViewAll)
  const [year, setYear] = useState(() => thisYear() + 1)
  const [categoryId, setCategoryId] = useState<string>()
  const [period, setPeriod] = useState<Period>('year')
  const [opened, setOpened] = useState<DemandSummaryRow | null>(null)
  const categories = useItemCategories()
  const tree = useMemo(() => buildCategoryTree(categories.data ?? []), [categories.data])
  const summary = useDemandSummary(year, categoryId, allCompanies && canViewAll)
  const data = summary.data
  const companies = useMemo(() => data?.companies ?? [], [data])
  const gridColumns = useMemo(() => columns(companies, period), [companies, period])
  // Năm đã qua vẫn xem được (đối chiếu kế hoạch cũ); năm tới tối đa như khi lập phiếu.
  const years = Array.from({ length: 5 }, (_, i) => thisYear() - 2 + i)
  const info = data && summaryText(data, data.rows.length)
  const periodLabel = periodOptions.find((p) => p.value === period)?.label

  return (
    <>
      <ListPage
        title="Tổng hợp nhu cầu"
        exportFileName="tong-hop-nhu-cau"
        exportSubtitle={`Năm ${year} – ${periodLabel}${data?.allCompanies ? ' – toàn tập đoàn' : ''}`}
        canCreate={false}
        rows={data?.rows ?? []}
        columns={gridColumns}
        rowKey={(r) => r.itemId}
        loading={summary.isLoading}
        onOpenRow={setOpened}
        notice={
          summary.error ? (
            <Alert type="error" showIcon title={summary.error.message} />
          ) : (
            info && <Alert type={info.warn ? 'warning' : 'info'} showIcon title={info.text} />
          )
        }
        onClearFilters={() => {
          setYear(thisYear() + 1)
          setCategoryId(undefined)
          setPeriod('year')
          setAllCompanies(canViewAll)
        }}
        filters={
          <>
            <FilterField label="Năm kế hoạch" width={110}>
              {(id) => <Select id={id} value={year} onChange={setYear} options={years.map((y) => ({ value: y, label: String(y) }))} />}
            </FilterField>
            <FilterField label="Kỳ" width={120}>
              {(id) => <Select id={id} value={period} onChange={setPeriod} options={periodOptions} />}
            </FilterField>
            <FilterField label="Nhóm hàng (gồm nhóm con)" width={220}>
              {(id) => (
                <TreeSelect id={id} value={categoryId} onChange={setCategoryId} treeData={tree} allowClear showSearch placeholder="Tất cả" />
              )}
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
        }
      />
      {opened && <DemandSummaryDrawer row={opened} companies={companies} onClose={() => setOpened(null)} />}
    </>
  )
}
