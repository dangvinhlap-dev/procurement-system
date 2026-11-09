import { FileExcelOutlined } from '@ant-design/icons'
import { Alert, App, Button, Checkbox, DatePicker } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import {
  downloadBoardMonthlyReport,
  useBoardMonthlyReport,
  type BoardMonthlyFilter,
  type BoardMonthlyReport,
  type BoardSection,
  type BoardSectionCode,
} from '../../api/boardReport'
import { anomalyLabels, type AnomalyCode } from '../../api/control'
import { screens } from '../../app/screens'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DataGrid } from '../../components/DataGrid/DataGrid'
import type { GridColumn, StatusTone } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { MONTH_INPUT_FORMATS, formatDate, formatMoney, formatQuantity } from '../../utils/format'
import { SupplierCoverageGrid } from '../pricing/SupplierCoveragePage'
import { SupplierOnboardingGrid } from '../pricing/SupplierOnboardingPages'
import { VolumeRebateTrackingGrid } from '../pricing/VolumeRebatePages'
import { ControlFindingGrid } from './ControlPages'
import { KpiGrid, OutOfContractGrid, PriceVarianceGrid } from './PurchaseReportPages'

/** Mục tiêu tỷ lệ mua theo giá đã duyệt của Ban (KPI P5, design.md mục 18.17) – dưới mức này tô đỏ. */
const COMPLIANCE_TARGET = 95

/** Đơn vị của một chỉ tiêu: tiền (VND), tỷ lệ (%) hoặc số đếm. */
type IndicatorUnit = 'VND' | '%' | 'Số'

/** Một dòng chỉ tiêu trên lưới chính: mỗi mục của báo cáo có một hoặc vài chỉ tiêu. */
interface Indicator {
  key: string
  section: BoardSectionCode
  sectionTitle: string
  label: string
  current: number | null
  previous: number | null
  unit: IndicatorUnit | null
  status: string
  tone: StatusTone
  note: string | null
  report: string | null
}

/** Tháng trước – Ban họp xem kết quả tháng đã qua. */
const defaultFilter = (allCompanies: boolean): BoardMonthlyFilter => {
  const m = dayjs().subtract(1, 'month')
  return { year: m.year(), month: m.month() + 1, allCompanies }
}

const columns: GridColumn<Indicator>[] = [
  { key: 'section', title: 'Mục', width: 170, value: (r) => r.sectionTitle, groupable: true, auditField: false },
  { key: 'label', title: 'Chỉ tiêu', width: 260, value: (r) => r.label, auditField: false },
  // Số canh phải, phông đơn cách (ui-style.md mục 3); đơn vị là cột riêng như cột tiền tệ.
  { key: 'current', title: 'Tháng này', width: 130, type: 'quantity', value: (r) => r.current, auditField: false },
  { key: 'previous', title: 'Tháng trước', width: 130, type: 'quantity', value: (r) => r.previous, auditField: false },
  { key: 'unit', title: 'Đơn vị', width: 60, value: (r) => r.unit, auditField: false },
  { key: 'status', title: 'Đánh giá', width: 150, type: 'status', value: (r) => r.status, tone: (r) => r.tone, auditField: false },
  { key: 'note', title: 'Ghi chú', width: 420, value: (r) => r.note, auditField: false },
  { key: 'report', title: 'Báo cáo chi tiết', width: 110, value: (r) => r.report, auditField: false },
]

/** Tạo dòng chỉ tiêu của một mục; trạng thái mặc định trung tính. */
const row = (
  section: BoardSection,
  key: string,
  label: string,
  current: number | null,
  unit: IndicatorUnit | null,
  extra: Partial<Pick<Indicator, 'previous' | 'status' | 'tone' | 'note'>> = {},
): Indicator => ({
  key: `${section.code}.${key}`,
  section: section.code,
  sectionTitle: section.title,
  label,
  current,
  previous: extra.previous ?? null,
  unit,
  status: extra.status ?? '',
  tone: extra.tone ?? 'neutral',
  note: extra.note ?? section.note,
  report: section.report,
})

/**
 * Dựng các dòng chỉ tiêu từ báo cáo: mục có số liệu ra một hoặc vài chỉ tiêu chính; mục thiếu quyền ra một dòng "Không có quyền xem".
 * Các số lấy nguyên từ máy chủ (đã khớp báo cáo chi tiết), không tính lại ở giao diện.
 * @param report Báo cáo tháng.
 * @returns Dòng theo thứ tự mục.
 */
function indicators(report: BoardMonthlyReport): Indicator[] {
  return report.sections.flatMap((s): Indicator[] => {
    if (!s.available) return [row(s, 'na', s.title, null, null, { status: 'Không có quyền xem', note: s.note })]
    switch (s.code) {
      case 'Purchase': {
        const p = report.purchase!
        const met = p.compliancePct == null ? null : p.compliancePct >= COMPLIANCE_TARGET
        return [
          row(s, 'orders', 'Số PO đã duyệt', p.orderCount, 'Số', { previous: p.previousOrderCount }),
          row(s, 'amount', 'Giá trị mua (trước VAT)', p.totalAmount, 'VND', { previous: p.previousTotalAmount }),
          row(s, 'compliance', `Tỷ lệ mua theo giá đã duyệt (mục tiêu ≥ ${COMPLIANCE_TARGET}%)`, p.compliancePct, '%', {
            previous: p.previousCompliancePct,
            status: met == null ? 'Chưa có dữ liệu' : met ? 'Đạt' : 'Không đạt',
            tone: met == null ? 'neutral' : met ? 'success' : 'error',
          }),
          row(s, 'out', 'Giá trị mua ngoài giá đã duyệt', p.outOfContractAmount, 'VND', {
            tone: p.outOfContractAmount > 0 ? 'warning' : 'success',
            status: p.outOfContractAmount > 0 ? 'Cần xem' : 'Không có',
          }),
        ]
      }
      case 'Kpi': {
        const k = report.kpi!
        const all = k.measuredCount > 0 && k.metCount === k.measuredCount
        return [
          row(s, 'met', 'Số KPI đạt mục tiêu', k.metCount, 'Số', {
            status: k.measuredCount === 0 ? 'Chưa có dữ liệu' : `Đạt ${k.metCount}/${k.measuredCount}`,
            tone: k.measuredCount === 0 ? 'neutral' : all ? 'success' : 'warning',
            note: `${s.note ?? ''} Trên ${k.measuredCount} chỉ số có kết quả; bấm đúp xem từng chỉ số.`.trim(),
          }),
        ]
      }
      case 'Savings': {
        const v = report.savings!
        return [
          row(s, 'sourcing', 'Tiết kiệm đấu thầu (so giá chào bình quân)', v.sourcingSaving, 'VND', {
            status: v.sourcingSavingPct == null ? 'Không có thầu' : `${formatQuantity(v.sourcingSavingPct)}%`,
            tone: v.sourcingSavingPct == null ? 'neutral' : 'success',
            note: `${v.rfqCount} RFQ chọn thầu, giá trị trúng ${formatMoney(v.awardedAmount)} đ.`,
          }),
          row(s, 'standard', 'Tiết kiệm so giá chuẩn', v.standardSaving, 'VND', {
            status: v.standardSavingPct == null ? 'Không có giá chuẩn' : `${formatQuantity(v.standardSavingPct)}%`,
            tone: v.standardSavingPct == null ? 'neutral' : v.standardSaving < 0 ? 'error' : 'success',
            note: `Âm = mua cao hơn giá chuẩn. Giá trị mua có giá chuẩn ${formatMoney(v.standardCoveredAmount)} đ` +
              (v.standardCoveragePct == null ? '.' : ` (${formatQuantity(v.standardCoveragePct)}%).`),
          }),
        ]
      }
      case 'OutOfContract':
        return [
          row(s, 'lines', 'Số dòng mua ngoài giá đã duyệt', report.outOfContractCount, 'Số', {
            status: report.outOfContractCount > 0 ? 'Cần xem' : 'Không có',
            tone: report.outOfContractCount > 0 ? 'warning' : 'success',
          }),
        ]
      case 'PriceVariance': {
        const v = report.priceVariance!
        return [
          row(s, 'impact', 'Ảnh hưởng biến động giá', v.impactAmount, 'VND', {
            status: v.impactAmount > 0 ? 'Tăng chi phí' : v.impactAmount < 0 ? 'Giảm chi phí' : 'Không đổi',
            tone: v.impactAmount > 0 ? 'error' : 'success',
          }),
          row(s, 'up', 'Số mặt hàng tăng giá', v.increasedCount, 'Số', { note: `${v.decreasedCount} mặt hàng giảm giá. ${s.note ?? ''}`.trim() }),
        ]
      }
      case 'VolumeRebates': {
        const rows = report.volumeRebates!
        const alerts = rows.filter(
          (r) => r.period.isNearNextTier || r.period.targetStatus === 'AtRisk' || r.period.targetStatus === 'Missed' || r.unreceivedPeriods > 0,
        ).length
        return [
          row(s, 'count', 'Thỏa thuận chiết khấu đang theo dõi', rows.length, 'Số', {
            status: alerts > 0 ? `${alerts} cần chú ý` : 'Không có cảnh báo',
            tone: alerts > 0 ? 'warning' : 'success',
          }),
          row(s, 'rebate', 'Chiết khấu phải nhận kỳ đang chạy', rows.reduce((sum, r) => sum + r.period.rebateAmount, 0), 'VND'),
        ]
      }
      case 'SupplierCoverage': {
        const c = report.supplierCoverage!
        return [
          row(s, 'short', `Nhóm hàng chưa đủ ${c.minSuppliers} NCC đã duyệt`, c.shortCount, 'Số', {
            status: c.shortCount > 0 ? 'Thiếu' : 'Đủ',
            tone: c.shortCount > 0 ? 'error' : 'success',
            note: `Trên ${c.categoryCount} nhóm hàng đang dùng. ${s.note ?? ''}`.trim(),
          }),
        ]
      }
      case 'Onboarding': {
        const o = report.onboarding!
        return [
          row(s, 'count', 'Hồ sơ đánh giá NCC mới', o.count, 'Số', {
            note: `${o.approvedCount} đã duyệt, ${o.inProgressCount} đang xử lý, ${o.closedCount} không đạt. ${s.note ?? ''}`.trim(),
          }),
        ]
      }
      case 'Control': {
        const c = report.control!
        return [
          ...(c.highCount == null
            ? []
            : [
                row(s, 'high', 'Dấu hiệu bất thường mức Cao', c.highCount, 'Số', {
                  status: c.highCount > 0 ? 'Cần xác minh' : 'Không có',
                  tone: c.highCount > 0 ? 'error' : 'success',
                  note: `${c.mediumCount ?? 0} dấu hiệu mức Trung bình. ${s.note ?? ''}`.trim(),
                }),
              ]),
          row(s, 'findings', 'Vi phạm ghi nhận trong tháng', c.findingCount, 'Số', {
            status: c.seriousConcludedCount > 0 ? `${c.seriousConcludedCount} nghiêm trọng` : 'Không có vụ nghiêm trọng',
            tone: c.seriousConcludedCount > 0 ? 'error' : 'success',
          }),
        ]
      }
    }
  })
}

/** Khung cao cố định cho lưới trong ngăn kéo – lưới cuộn ảo cần chiều cao của khung chứa. */
const gridBoxStyle = { height: 360, display: 'flex', flexDirection: 'column' } as const

/** Một dòng tổng hợp dấu hiệu bất thường theo loại. */
interface AnomalyCountRow {
  code: string
  name: string
  count: number
  note: string
}

const anomalyCountColumns: GridColumn<AnomalyCountRow>[] = [
  { key: 'name', title: 'Loại dấu hiệu', width: 260, value: (r) => r.name, auditField: false },
  { key: 'count', title: 'Số dấu hiệu', width: 100, type: 'quantity', value: (r) => r.count, auditField: false },
  { key: 'note', title: 'Ghi chú', width: 420, value: (r) => r.note, auditField: false },
]

/** Một dòng cơ cấu theo nguồn giá. */
interface SourceRow {
  source: string
  lineCount: number
  amount: number
  sharePct: number
}

/** Tên nguồn giá – cùng nhãn báo cáo PRCW. */
const sourceLabels: Record<string, string> = {
  MonthlyApproval: 'Duyệt tháng',
  Rfq: 'Chọn thầu',
  Contract: 'Hợp đồng',
  PriceList: 'Bảng giá',
  Deviation: 'Lệch giá đã duyệt',
  None: 'Không có giá đã duyệt',
  Unknown: 'Không xác định',
}

const sourceColumns: GridColumn<SourceRow>[] = [
  { key: 'source', title: 'Nguồn giá', width: 200, value: (r) => sourceLabels[r.source] ?? r.source, auditField: false },
  { key: 'lines', title: 'Số dòng PO', width: 100, type: 'quantity', value: (r) => r.lineCount, auditField: false },
  { key: 'amount', title: 'Giá trị', width: 140, type: 'money', value: (r) => r.amount, auditField: false },
  { key: 'currency', title: 'Tiền tệ', width: 70, value: () => 'VND', auditField: false },
  { key: 'share', title: 'Tỷ trọng (%)', width: 100, type: 'quantity', value: (r) => r.sharePct, auditField: false },
]

/**
 * Lưới chi tiết của một mục trong ngăn kéo; null khi mục không có danh sách (Tiết kiệm – mở thẳng báo cáo chi tiết).
 * @param code Mục.
 * @param report Báo cáo tháng.
 */
function sectionDetail(code: BoardSectionCode, report: BoardMonthlyReport): ReactNode {
  switch (code) {
    case 'Purchase':
      return <DataGrid rows={report.purchase!.sources} columns={sourceColumns} rowKey={(r) => r.source} groupBy={null} onGroupByChange={() => {}} />
    case 'Kpi':
      return <KpiGrid rows={report.kpi!.items} />
    case 'OutOfContract':
      return <OutOfContractGrid rows={report.outOfContract!} />
    case 'PriceVariance':
      return <PriceVarianceGrid rows={report.priceVariance!.topIncreases} />
    case 'VolumeRebates':
      return <VolumeRebateTrackingGrid rows={report.volumeRebates!} />
    case 'SupplierCoverage':
      return <SupplierCoverageGrid rows={report.supplierCoverage!.categories} />
    case 'Onboarding':
      return <SupplierOnboardingGrid rows={report.onboarding!.items} />
    case 'Control': {
      const c = report.control!
      return (
        <>
          {c.highCount != null && (
            <div style={{ ...gridBoxStyle, height: 200 }}>
              <DataGrid
                rows={c.summary.map((a) => ({ code: a.code, name: anomalyLabels[a.code as AnomalyCode] ?? a.code, count: a.count, note: a.note }))}
                columns={anomalyCountColumns}
                rowKey={(r) => r.code}
                groupBy={null}
                onGroupByChange={() => {}}
              />
            </div>
          )}
          <ControlFindingGrid rows={c.findings} />
        </>
      )
    }
    default:
      return null
  }
}

/**
 * Ngăn kéo chi tiết một mục (chỉ xem): lưới cùng cột với báo cáo chi tiết, nút mở báo cáo đó.
 * @param section Mục đang mở.
 * @param report Báo cáo tháng.
 * @param onClose Đóng ngăn kéo.
 */
function SectionDrawer({ section, report, onClose }: { section: BoardSection; report: BoardMonthlyReport; onClose: () => void }) {
  const navigate = useNavigate()
  const path = screens.find((s) => s.code === section.report)?.path
  return (
    <DetailDrawer
      open
      readOnly
      title={`${section.title} – tháng ${String(report.month).padStart(2, '0')}/${report.year}`}
      onClose={onClose}
      extraActions={
        path && (
          <Button onClick={() => navigate(path)}>
            Mở báo cáo {section.report}
          </Button>
        )
      }
      tabs={[
        {
          key: 'detail',
          label: 'Chi tiết',
          children: (
            <>
              {section.note && <div className="detail-hint">{section.note}</div>}
              <div style={gridBoxStyle}>{sectionDetail(section.code, report)}</div>
            </>
          ),
        },
      ]}
    />
  )
}

/**
 * Báo cáo → Báo cáo tháng cho Ban (mã BCBAN, lát G12, design.md mục 18.25): mỗi dòng một chỉ tiêu chính của các mục – mua hàng và tỷ lệ mua
 * theo giá đã duyệt, KPI, tiết kiệm, mua ngoài giá đã duyệt, biến động giá, chiết khấu sản lượng gộp, NCC đủ điều kiện, hồ sơ NCC mới, kiểm
 * soát nội bộ (chỉ người có quyền KSNB). Bấm đúp mở chi tiết mục (cùng cột báo cáo chi tiết). "Xuất gói Excel" tải đủ các mục, mỗi mục một
 * sheet. Mặc định tháng trước.
 */
export function BoardMonthlyReportPage() {
  const { message } = App.useApp()
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const canViewAll = hasPermission(Permission.PurchaseViewAllCompanies)
  const [filter, setFilter] = useState(() => defaultFilter(false))
  const [opened, setOpened] = useState<BoardSection | null>(null)
  const [exporting, setExporting] = useState(false)
  const report = useBoardMonthlyReport(filter)
  const data = report.data
  const rows = data ? indicators(data) : []
  const monthText = `${String(filter.month).padStart(2, '0')}/${filter.year}`

  const exportPackage = async () => {
    setExporting(true)
    try {
      await downloadBoardMonthlyReport(filter)
    } catch (e) {
      message.error((e as Error).message)
    } finally {
      setExporting(false)
    }
  }

  const open = (r: Indicator) => {
    const section = data?.sections.find((s) => s.code === r.section)
    if (!section?.available) return
    // Tiết kiệm không có danh sách riêng ở báo cáo tháng: mở thẳng báo cáo chi tiết.
    if (section.code === 'Savings') {
      const path = screens.find((s) => s.code === section.report)?.path
      if (path) navigate(path)
      return
    }
    setOpened(section)
  }

  return (
    <>
      <ListPage
        title="Báo cáo tháng cho Ban"
        exportFileName={`bao-cao-thang-ban-${filter.year}-${String(filter.month).padStart(2, '0')}`}
        exportSubtitle={data && `Tháng ${monthText} – ${data.allCompanies ? 'toàn tập đoàn' : `công ty ${data.companyCode}`}`}
        canCreate={false}
        actions={
          <Button icon={<FileExcelOutlined />} onClick={exportPackage} loading={exporting} disabled={!data}>
            Xuất gói Excel
          </Button>
        }
        rows={rows}
        columns={columns}
        rowKey={(r) => r.key}
        loading={report.isLoading}
        isMarked={(r) => r.tone === 'error'}
        onOpenRow={open}
        notice={
          report.error ? (
            <Alert type="error" showIcon title={report.error.message} />
          ) : (
            data && (
              <Alert
                type="info"
                showIcon
                title={
                  `Tháng ${monthText} (${formatDate(data.from)} – ${formatDate(data.to)}), ` +
                  `${data.allCompanies ? 'toàn tập đoàn' : `công ty ${data.companyCode}`}; so tháng trước ${formatDate(data.previousFrom)} – ` +
                  `${formatDate(data.previousTo)}.`
                }
                description="Số liệu lấy đúng từ các báo cáo chi tiết. Bấm đúp một dòng để xem chi tiết mục; tô màu: chỉ tiêu không đạt. Xuất gói Excel: mỗi mục một sheet."
              />
            )
          )
        }
        onClearFilters={() => setFilter(defaultFilter(false))}
        filters={
          <>
            <FilterField label="Tháng" width={120}>
              {(id) => (
                <DatePicker
                  id={id}
                  picker="month"
                  format={MONTH_INPUT_FORMATS}
                  allowClear={false}
                  value={dayjs(new Date(filter.year, filter.month - 1, 1))}
                  onChange={(v: Dayjs | null) => v && setFilter({ ...filter, year: v.year(), month: v.month() + 1 })}
                  style={{ width: '100%' }}
                />
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
      {opened && data && <SectionDrawer section={opened} report={data} onClose={() => setOpened(null)} />}
    </>
  )
}
