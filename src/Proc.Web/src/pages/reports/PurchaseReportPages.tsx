import { Alert, Checkbox, InputNumber, Select, TreeSelect } from 'antd'
import dayjs from 'dayjs'
import { useMemo, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { useItemCategories } from '../../api/masterData'
import { priorityLabels } from '../../api/purchasing'
import {
  useContractCompliance,
  usePendingRequests,
  usePriceVariance,
  type OutOfContractLine,
  type PriceSourceKind,
  type PriceVarianceRow,
  usePurchaseSla,
  useReceiptQuality,
  useThreeWayMatch,
  type ReceiptQualityRow,
  type ThreeWayMatchIssue,
  type ThreeWayMatchRow,
  type ThreeWayMatchStatus,
  usePurchaseSpend,
  useSourcingSavings,
  useSupplierEvaluation,
  type PendingRequestLine,
  type PurchaseSlaRow,
  type SlaCategory,
  type SlaStatus,
  type PurchaseReportPeriod,
  type PurchaseSpendGroup,
  type PurchaseSpendRow,
  type SourcingSavingRow,
  type SupplierEvaluationRow,
} from '../../api/reports'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn, StatusTone } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatDate, formatMoney, formatQuantity, vnNumberInput } from '../../utils/format'
import { buildCategoryTree } from '../masterData/categoryTree'
import { LogDateRangeFilter } from '../system/logFilters'

const pendingColumns: GridColumn<PendingRequestLine>[] = [
  {
    key: 'status',
    title: 'Tình trạng',
    width: 120,
    type: 'status',
    value: (r) => (r.status === 'Submitted' ? 'Chờ duyệt' : 'Chờ đặt hàng'),
    tone: (r) => (r.overdueDays > 0 ? 'error' : 'warning'),
    groupable: true,
  },
  { key: 'no', title: 'Số PR', width: 140, value: (r) => r.requestNo },
  { key: 'date', title: 'Ngày PR', width: 90, type: 'date', value: (r) => r.date },
  { key: 'department', title: 'Bộ phận', width: 120, value: (r) => r.department, groupable: true },
  { key: 'priority', title: 'Ưu tiên', width: 70, value: (r) => priorityLabels[r.priority], groupable: true },
  { key: 'code', title: 'Mã hàng', width: 120, value: (r) => r.itemCode },
  { key: 'name', title: 'Tên hàng', width: 200, value: (r) => r.itemName },
  { key: 'uom', title: 'ĐVT', width: 60, value: (r) => r.uomCode },
  { key: 'qty', title: 'SL đề nghị', width: 90, type: 'quantity', value: (r) => r.qty },
  { key: 'ordered', title: 'Đã đặt', width: 80, type: 'quantity', value: (r) => r.orderedQty },
  { key: 'remaining', title: 'Còn phải đặt', width: 100, type: 'quantity', value: (r) => r.remainingQty },
  { key: 'amount', title: 'Giá trị ước tính', width: 120, type: 'money', value: (r) => r.estAmount },
  { key: 'needed', title: 'Ngày cần', width: 90, type: 'date', value: (r) => r.neededDate },
  { key: 'overdue', title: 'Trễ (ngày)', width: 80, type: 'quantity', value: (r) => (r.overdueDays > 0 ? r.overdueDays : null) },
]

/**
 * Báo cáo → PR chưa xử lý (design.md mục 11, 11.3): dòng PR đang chờ duyệt hoặc đã duyệt mà chưa lên PO đủ, ngày cần sớm nhất lên đầu;
 * dòng đã quá ngày cần được tô màu. Bấm đúp mở PR.
 */
export function PendingRequestsReportPage() {
  const navigate = useNavigate()
  const report = usePendingRequests()
  const rows = report.data ?? []
  const overdue = rows.filter((r) => r.overdueDays > 0).length
  return (
    <ListPage
      title="PR chưa xử lý"
      exportFileName="pr-chua-xu-ly"
      canCreate={false}
      rows={rows}
      columns={pendingColumns}
      rowKey={(r) => `${r.requestId}|${r.lineNo}`}
      loading={report.isLoading}
      isMarked={(r) => r.overdueDays > 0}
      onOpenRow={(r) => navigate(`/mua-hang/pr/${r.requestId}`)}
      notice={
        report.error ? (
          <Alert type="error" showIcon title={report.error.message} />
        ) : (
          <Alert
            type={overdue > 0 ? 'warning' : 'info'}
            showIcon
            title={`${overdue} dòng đã quá ngày cần (tô màu). Giá trị ước tính = SL còn phải đặt × giá tham khảo. Bấm đúp để mở PR.`}
          />
        )
      }
    />
  )
}

const slaCategoryLabels: Record<SlaCategory, string> = { Priced: 'Đã có HĐ/bảng giá', Sourcing: 'Chưa có NCC duyệt', Urgent: 'Khẩn' }
const slaStatusLabels: Record<SlaStatus, string> = { OnTime: 'Đúng hạn', Late: 'Trễ', InProgress: 'Đang xử lý', Overdue: 'Quá hạn' }
const slaStatusTone: Record<SlaStatus, StatusTone> = { OnTime: 'success', Late: 'error', InProgress: 'neutral', Overdue: 'error' }

const slaColumns: GridColumn<PurchaseSlaRow>[] = [
  { key: 'pr', title: 'Số PR', width: 150, value: (r) => r.requestNo },
  { key: 'approved', title: 'Ngày duyệt PR', width: 100, type: 'date', value: (r) => r.approvedDate },
  { key: 'department', title: 'Bộ phận', width: 140, value: (r) => r.department, groupable: true },
  { key: 'line', title: 'Dòng', width: 55, type: 'quantity', value: (r) => r.lineNo },
  { key: 'item', title: 'Mặt hàng', width: 240, value: (r) => `${r.itemCode} – ${r.itemName}` },
  { key: 'category', title: 'Loại yêu cầu', width: 140, value: (r) => slaCategoryLabels[r.category], groupable: true },
  { key: 'sla', title: 'SLA (ngày)', width: 80, type: 'quantity', value: (r) => r.slaDays },
  { key: 'due', title: 'Hạn', width: 95, type: 'date', value: (r) => r.dueDate },
  { key: 'po', title: 'PO', width: 150, value: (r) => r.orderNo },
  { key: 'poDate', title: 'Ngày duyệt PO', width: 100, type: 'date', value: (r) => r.orderApprovedDate },
  { key: 'status', title: 'Trạng thái', width: 110, type: 'status', value: (r) => slaStatusLabels[r.status], tone: (r) => slaStatusTone[r.status], groupable: true },
]

/**
 * Báo cáo → SLA mua hàng (design.md mục 18.13 – V6): mỗi dòng PR được duyệt trong khoảng ngày, hạn = ngày duyệt PR + SLA ngày làm việc
 * theo loại yêu cầu (tham số ở SEC), so với ngày duyệt PO đầu tiên. Đầu trang: % đúng SLA (mục tiêu ≥ 95%). Bấm đúp mở PO hoặc PR.
 */
export function PurchaseSlaReportPage() {
  const navigate = useNavigate()
  const [range, setRange] = useState({ from: dayjs().startOf('month').format('YYYY-MM-DD'), to: dayjs().format('YYYY-MM-DD') })
  const report = usePurchaseSla(range)
  const data = report.data
  return (
    <ListPage
      title="SLA mua hàng"
      exportFileName="sla-mua-hang"
      exportSubtitle={`PR duyệt từ ${formatDate(range.from)} đến ${formatDate(range.to)}`}
      canCreate={false}
      rows={data?.rows ?? []}
      columns={slaColumns}
      rowKey={(r) => `${r.requestId}|${r.itemCode}|${r.lineNo}`}
      loading={report.isLoading}
      isMarked={(r) => r.status === 'Late' || r.status === 'Overdue'}
      onOpenRow={(r) => navigate(r.orderId ? `/mua-hang/po/${r.orderId}` : `/mua-hang/pr/${r.requestId}`)}
      notice={
        report.error ? (
          <Alert type="error" showIcon title={report.error.message} />
        ) : data ? (
          <Alert
            type={data.onTimePct == null || data.onTimePct >= 95 ? 'info' : 'warning'}
            showIcon
            title={`Đúng SLA ${data.onTimePct == null ? '—' : `${String(data.onTimePct).replace('.', ',')}%`} (mục tiêu ≥ 95%): đúng hạn ${data.onTime}, trễ ${data.late}, quá hạn chưa có PO ${data.overdue}, đang xử lý ${data.inProgress}. SLA tính ngày làm việc thứ 2–6 (chưa trừ ngày lễ), sửa ở Tham số hệ thống.`}
          />
        ) : null
      }
      onClearFilters={() => setRange({ from: dayjs().startOf('month').format('YYYY-MM-DD'), to: dayjs().format('YYYY-MM-DD') })}
      filters={<LogDateRangeFilter value={range} onChange={(r) => setRange({ ...range, ...r })} />}
    />
  )
}

const qcLabels: Record<ReceiptQualityRow['qcResult'], string> = { NotChecked: 'Chưa kiểm tra', Passed: 'Đạt', Failed: 'Không đạt' }

const qualityColumns: GridColumn<ReceiptQualityRow>[] = [
  { key: 'no', title: 'Phiếu nhập', width: 150, value: (r) => r.documentNo },
  { key: 'date', title: 'Ngày', width: 90, type: 'date', value: (r) => r.date },
  { key: 'supplier', title: 'Nhà cung cấp', width: 220, value: (r) => `${r.supplierCode} – ${r.supplierName}`, groupable: true },
  { key: 'po', title: 'PO', width: 150, value: (r) => r.purchaseOrderNo },
  { key: 'item', title: 'Mặt hàng', width: 230, value: (r) => `${r.itemCode} – ${r.itemName}` },
  { key: 'uom', title: 'ĐVT', width: 60, value: (r) => r.uomCode },
  { key: 'delivered', title: 'SL chứng từ', width: 90, type: 'quantity', value: (r) => r.deliveredQty },
  { key: 'qty', title: 'SL nhận đạt', width: 90, type: 'quantity', value: (r) => r.qty },
  { key: 'rejected', title: 'SL không đạt', width: 90, type: 'quantity', value: (r) => r.rejectedQty },
  { key: 'diff', title: 'Thiếu/thừa', width: 85, type: 'quantity', value: (r) => r.discrepancyQty },
  { key: 'qc', title: 'Kết quả KT', width: 100, value: (r) => qcLabels[r.qcResult], groupable: true },
  { key: 'note', title: 'Ghi chú kiểm tra', width: 240, value: (r) => r.qcNote },
]

/**
 * Báo cáo → Sai lệch nhận hàng (design.md mục 18.14 – V7): dòng phiếu nhập mua NCC đã ghi sổ có hàng không đạt hoặc thiếu/thừa so với
 * chứng từ giao; đầu trang % dòng đạt chất lượng trên các dòng đã kiểm tra. Bấm đúp mở phiếu nhập.
 */
export function ReceiptQualityReportPage() {
  const navigate = useNavigate()
  const [range, setRange] = useState({ from: dayjs().startOf('month').format('YYYY-MM-DD'), to: dayjs().format('YYYY-MM-DD') })
  const report = useReceiptQuality(range)
  const data = report.data
  return (
    <ListPage
      title="Sai lệch nhận hàng"
      exportFileName="sai-lech-nhan-hang"
      exportSubtitle={`Phiếu nhập từ ${formatDate(range.from)} đến ${formatDate(range.to)}`}
      canCreate={false}
      rows={data?.rows ?? []}
      columns={qualityColumns}
      rowKey={(r) => `${r.documentId}|${r.lineNo}`}
      loading={report.isLoading}
      onOpenRow={(r) => navigate(`/kho/grn/${r.documentId}`)}
      notice={
        report.error ? (
          <Alert type="error" showIcon title={report.error.message} />
        ) : data ? (
          <Alert
            type="info"
            showIcon
            title={`Đạt chất lượng ${data.passedPct == null ? '—' : `${String(data.passedPct).replace('.', ',')}%`}: ${data.passedLines}/${data.checkedLines} dòng đã kiểm tra (tổng ${data.totalLines} dòng nhận). Danh sách chỉ gồm dòng không đạt hoặc thiếu/thừa.`}
          />
        ) : null
      }
      onClearFilters={() => setRange({ from: dayjs().startOf('month').format('YYYY-MM-DD'), to: dayjs().format('YYYY-MM-DD') })}
      filters={<LogDateRangeFilter value={range} onChange={(r) => setRange({ ...range, ...r })} />}
    />
  )
}

const matchStatusLabels: Record<ThreeWayMatchStatus, string> = { Matched: 'Khớp', NoInvoice: 'Chưa có hóa đơn', Mismatch: 'Lệch tiền' }
const matchStatusTone: Record<ThreeWayMatchStatus, StatusTone> = { Matched: 'success', NoInvoice: 'warning', Mismatch: 'error' }

/** Chữ của mã cảnh báo đối chiếu (design.md mục 18.15). */
const matchIssueLabels: Record<ThreeWayMatchIssue, string> = {
  NoInvoice: 'Chưa có hóa đơn',
  AmountMismatch: 'Hóa đơn lệch tiền nhận',
  InvoiceIncludesShortage: 'Hóa đơn tính cả hàng thiếu/không đạt',
  NoPurchaseOrder: 'Nhập không theo PO',
  ReceivedBeforeOrder: 'Nhận hàng trước ngày PO',
  ReceivedBeforeApproval: 'Nhận hàng trước ngày duyệt PO',
  InvoiceBeforeOrder: 'Hóa đơn trước ngày PO',
}

const matchColumns: GridColumn<ThreeWayMatchRow>[] = [
  { key: 'no', title: 'Phiếu nhập', width: 150, value: (r) => r.documentNo },
  { key: 'date', title: 'Ngày nhập', width: 90, type: 'date', value: (r) => r.date },
  { key: 'supplier', title: 'Nhà cung cấp', width: 220, value: (r) => `${r.supplierCode} – ${r.supplierName}`, groupable: true },
  { key: 'po', title: 'PO', width: 140, value: (r) => r.purchaseOrderNo },
  { key: 'poDate', title: 'Ngày PO', width: 90, type: 'date', value: (r) => r.purchaseOrderDate },
  { key: 'poApproved', title: 'Ngày duyệt PO', width: 100, type: 'date', value: (r) => r.purchaseOrderApprovedDate },
  { key: 'poTotal', title: 'Tổng tiền PO', width: 130, type: 'money', value: (r) => r.orderTotal },
  { key: 'receipt', title: 'Tiền nhận', width: 130, type: 'money', value: (r) => r.receiptTotal },
  { key: 'invoiceNo', title: 'Số hóa đơn', width: 110, value: (r) => r.invoiceNo },
  { key: 'invoiceSeries', title: 'Ký hiệu', width: 90, value: (r) => r.invoiceSeries },
  { key: 'invoiceDate', title: 'Ngày hóa đơn', width: 100, type: 'date', value: (r) => r.invoiceDate },
  { key: 'invoice', title: 'Tiền hóa đơn', width: 130, type: 'money', value: (r) => r.invoiceAmount },
  { key: 'diff', title: 'Chênh lệch', width: 120, type: 'money', value: (r) => r.difference },
  {
    key: 'result',
    title: 'Kết quả',
    width: 120,
    type: 'status',
    value: (r) => matchStatusLabels[r.result],
    tone: (r) => matchStatusTone[r.result],
    groupable: true,
  },
  { key: 'issues', title: 'Cảnh báo', width: 320, value: (r) => r.issues.map((i) => matchIssueLabels[i]).join('; ') },
]

const defaultMatchFilter = () => ({
  from: dayjs().startOf('month').format('YYYY-MM-DD'),
  to: dayjs().format('YYYY-MM-DD'),
  tolerance: 0,
  onlyIssues: false,
})

/**
 * Báo cáo → Đối chiếu PO – nhận – hóa đơn (design.md mục 18.15, M5/V8 – báo cáo cho kế toán; thanh toán vẫn ở phần mềm kế toán):
 * mỗi phiếu nhập mua NCC đã ghi sổ trong khoảng ngày – tiền hóa đơn so với tiền nhận (theo dung sai), ngày nhận/hóa đơn so với ngày PO.
 * Bấm đúp mở phiếu nhập để cập nhật hóa đơn.
 */
export function ThreeWayMatchReportPage() {
  const navigate = useNavigate()
  const [filter, setFilter] = useState(defaultMatchFilter)
  const report = useThreeWayMatch(filter)
  const data = report.data
  return (
    <ListPage
      title="Đối chiếu PO – nhận – hóa đơn"
      exportFileName="doi-chieu-po-nhan-hoa-don"
      exportSubtitle={`Phiếu nhập từ ${formatDate(filter.from)} đến ${formatDate(filter.to)} – dung sai ${formatMoney(filter.tolerance)} đ`}
      canCreate={false}
      rows={data?.rows ?? []}
      columns={matchColumns}
      rowKey={(r) => r.documentId}
      loading={report.isLoading}
      onOpenRow={(r) => navigate(`/kho/grn/${r.documentId}`)}
      notice={
        report.error ? (
          <Alert type="error" showIcon title={report.error.message} />
        ) : data ? (
          <Alert
            type={data.mismatch > 0 ? 'warning' : 'info'}
            showIcon
            title={
              `Khớp ${data.matched} · Chưa có hóa đơn ${data.noInvoice} · Lệch tiền ${data.mismatch} · Có cảnh báo ${data.withIssues} phiếu. ` +
              `Tiền nhận ${formatMoney(data.receiptTotal)} – tiền hóa đơn đã nhập ${formatMoney(data.invoiceTotal)}.` +
              (data.hiddenDocuments > 0 ? ` Bỏ qua ${data.hiddenDocuments} phiếu có mặt hàng ngoài phạm vi nhóm hàng của bạn.` : '')
            }
          />
        ) : null
      }
      onClearFilters={() => setFilter(defaultMatchFilter())}
      filters={
        <>
          <LogDateRangeFilter value={filter} onChange={(r) => setFilter({ ...filter, ...r })} />
          <FilterField label="Dung sai (đ)" width={120}>
            {(id) => (
              <InputNumber
                {...vnNumberInput}
                id={id}
                min={0}
                precision={0}
                value={filter.tolerance}
                onChange={(v) => setFilter({ ...filter, tolerance: Number(v ?? 0) })}
                style={{ width: '100%' }}
              />
            )}
          </FilterField>
          <FilterField label="Hiển thị" width={170}>
            {(id) => (
              <Checkbox id={id} checked={filter.onlyIssues} onChange={(e) => setFilter({ ...filter, onlyIssues: e.target.checked })}>
                Chỉ phiếu có cảnh báo
              </Checkbox>
            )}
          </FilterField>
        </>
      }
    />
  )
}

/** Khoảng ngày mặc định của báo cáo mua hàng: từ đầu năm đến hôm nay. */
const defaultPeriod = (): PurchaseReportPeriod => ({
  from: dayjs().startOf('year').format('YYYY-MM-DD'),
  to: dayjs().format('YYYY-MM-DD'),
  allCompanies: false,
})

/** Dòng điều kiện ghi trên file Excel. */
const periodText = (p: PurchaseReportPeriod) => `Từ ${formatDate(p.from)} đến ${formatDate(p.to)}${p.allCompanies ? ' – toàn tập đoàn' : ''}`

/**
 * Ô lọc khoảng ngày và (khi có quyền) ô "Toàn tập đoàn" của báo cáo mua hàng.
 * @param value Điều kiện đang chọn.
 * @param onChange Đổi điều kiện.
 * @param extra Ô lọc riêng của từng báo cáo (đặt sau khoảng ngày).
 */
function PeriodFilters({ value, onChange, extra }: { value: PurchaseReportPeriod; onChange: (p: PurchaseReportPeriod) => void; extra?: ReactNode }) {
  const { hasPermission } = useAuth()
  return (
    <>
      <LogDateRangeFilter value={value} onChange={(range) => onChange({ ...value, ...range })} />
      {extra}
      {hasPermission(Permission.PurchaseViewAllCompanies) && (
        <FilterField label="Phạm vi" width={140}>
          {(id) => (
            <Checkbox id={id} checked={value.allCompanies} onChange={(e) => onChange({ ...value, allCompanies: e.target.checked })}>
              Toàn tập đoàn
            </Checkbox>
          )}
        </FilterField>
      )}
    </>
  )
}

const groupLabels: Record<PurchaseSpendGroup, string> = {
  supplier: 'Nhà cung cấp',
  category: 'Nhóm hàng',
  item: 'Mặt hàng',
  company: 'Công ty',
}

/** Cột của báo cáo mua hàng; cột đầu đổi tiêu đề theo cách nhóm. */
const spendColumns = (groupBy: PurchaseSpendGroup): GridColumn<PurchaseSpendRow>[] => [
  { key: 'code', title: `Mã ${groupLabels[groupBy].toLowerCase()}`, width: 130, value: (r) => r.code },
  { key: 'name', title: groupLabels[groupBy], width: 280, value: (r) => r.name },
  { key: 'count', title: 'Số phiếu nhập', width: 100, type: 'quantity', value: (r) => r.receiptCount },
  { key: 'receipt', title: 'Giá trị nhập', width: 140, type: 'money', value: (r) => r.receiptAmount },
  { key: 'return', title: 'Giá trị trả', width: 130, type: 'money', value: (r) => r.returnAmount },
  { key: 'net', title: 'Mua thuần', width: 140, type: 'money', value: (r) => r.netAmount },
  { key: 'share', title: 'Tỷ trọng (%)', width: 100, type: 'quantity', value: (r) => r.sharePct },
]

/**
 * Báo cáo → Mua hàng theo NCC (design.md mục 11, 11.3): giá trị trước VAT của phiếu nhập mua NCC và phiếu trả NCC đã ghi sổ trong khoảng
 * ngày, nhóm theo NCC, nhóm hàng, mặt hàng hoặc công ty; mua thuần = nhập − trả. Không gồm mua nội bộ.
 */
export function PurchaseSpendReportPage() {
  const [period, setPeriod] = useState(defaultPeriod)
  const [groupBy, setGroupBy] = useState<PurchaseSpendGroup>('supplier')
  const report = usePurchaseSpend(period, groupBy)
  return (
    <ListPage
      key={groupBy}
      title="Mua hàng theo NCC"
      exportFileName="mua-hang-theo-nhom"
      exportSubtitle={`${periodText(period)} – theo ${groupLabels[groupBy].toLowerCase()}`}
      canCreate={false}
      rows={report.data?.items ?? []}
      columns={spendColumns(groupBy)}
      rowKey={(r) => r.key}
      loading={report.isLoading}
      notice={
        report.error ? (
          <Alert type="error" showIcon title={report.error.message} />
        ) : (
          <Alert
            type="info"
            showIcon
            title={`Tổng mua thuần: ${formatMoney(report.data?.totalNet ?? 0)} đ (trước VAT, nhập mua NCC − trả NCC đã ghi sổ; không gồm mua nội bộ).`}
          />
        )
      }
      onClearFilters={() => {
        setPeriod(defaultPeriod())
        setGroupBy('supplier')
      }}
      filters={
        <PeriodFilters
          value={period}
          onChange={setPeriod}
          extra={
            <FilterField label="Tổng hợp theo" width={150}>
              {(id) => (
                <Select id={id} value={groupBy} onChange={setGroupBy} options={Object.entries(groupLabels).map(([value, label]) => ({ value, label }))} />
              )}
            </FilterField>
          }
        />
      }
    />
  )
}

const evaluationColumns: GridColumn<SupplierEvaluationRow>[] = [
  { key: 'code', title: 'Mã NCC', width: 120, value: (r) => r.supplierCode },
  { key: 'name', title: 'Nhà cung cấp', width: 260, value: (r) => r.supplierName },
  { key: 'count', title: 'Số phiếu nhập', width: 100, type: 'quantity', value: (r) => r.receiptCount },
  { key: 'receipt', title: 'Giá trị nhập', width: 130, type: 'money', value: (r) => r.receiptAmount },
  { key: 'return', title: 'Giá trị trả', width: 120, type: 'money', value: (r) => r.returnAmount },
  { key: 'returnPct', title: 'Tỷ lệ trả (%)', width: 100, type: 'quantity', value: (r) => r.returnPct },
  { key: 'lines', title: 'Dòng có hẹn giao', width: 120, type: 'quantity', value: (r) => r.deliveryLines },
  { key: 'onTime', title: 'Đúng hạn', width: 90, type: 'quantity', value: (r) => r.onTimeLines },
  { key: 'onTimePct', title: 'Đúng hạn (%)', width: 100, type: 'quantity', value: (r) => r.onTimePct },
  { key: 'late', title: 'Trễ bình quân (ngày)', width: 130, type: 'quantity', value: (r) => r.avgLateDays },
  {
    key: 'rating',
    title: 'Xếp loại',
    width: 100,
    type: 'status',
    value: (r) => r.rating,
    tone: (r) => (r.rating === 'Không đạt' ? 'error' : r.rating == null ? 'neutral' : 'success'),
    groupable: true,
  },
]

/**
 * Báo cáo → Đánh giá NCC (design.md mục 5.3, 11.3): theo NCC trong khoảng ngày – giá trị nhập, trả, tỷ lệ trả; tỷ lệ giao đúng hạn so với
 * ngày giao trên PO (dòng nhập không theo PO hoặc PO không hẹn ngày thì không chấm). Dòng có giao trễ hoặc trả hàng được tô màu.
 */
export function SupplierEvaluationReportPage() {
  const [period, setPeriod] = useState(defaultPeriod)
  const report = useSupplierEvaluation(period)
  return (
    <ListPage
      title="Đánh giá nhà cung cấp"
      exportFileName="danh-gia-ncc"
      exportSubtitle={periodText(period)}
      canCreate={false}
      rows={report.data ?? []}
      columns={evaluationColumns}
      rowKey={(r) => r.supplierId}
      loading={report.isLoading}
      isMarked={(r) => (r.onTimePct != null && r.onTimePct < 100) || r.returnAmount > 0}
      notice={
        report.error ? (
          <Alert type="error" showIcon title={report.error.message} />
        ) : (
          <Alert
            type="info"
            showIcon
            title="Đúng hạn: ngày phiếu nhập ≤ ngày giao của dòng PO (không có thì ngày giao dự kiến của PO). Tô màu: có giao trễ hoặc trả hàng. Xếp loại theo ngưỡng ở Hệ thống → Xếp loại NCC."
          />
        )
      }
      onClearFilters={() => setPeriod(defaultPeriod())}
      filters={<PeriodFilters value={period} onChange={setPeriod} />}
    />
  )
}

const savingColumns: GridColumn<SourcingSavingRow>[] = [
  { key: 'date', title: 'Ngày chọn thầu', width: 100, type: 'date', value: (r) => r.awardDate },
  { key: 'no', title: 'Số RFQ', width: 140, value: (r) => r.rfqNo, groupable: true },
  { key: 'company', title: 'Công ty', width: 80, value: (r) => r.companyCode, groupable: true },
  { key: 'item', title: 'Mặt hàng', width: 220, value: (r) => `${r.itemCode} – ${r.itemName}` },
  { key: 'uom', title: 'ĐVT', width: 60, value: (r) => r.uomCode },
  { key: 'qty', title: 'SL', width: 80, type: 'quantity', value: (r) => r.qty },
  { key: 'supplier', title: 'NCC trúng thầu', width: 200, value: (r) => `${r.supplierCode} – ${r.supplierName}`, groupable: true },
  { key: 'quotes', title: 'Số báo giá', width: 80, type: 'quantity', value: (r) => r.quoteCount },
  { key: 'price', title: 'Giá trúng', width: 110, type: 'money', value: (r) => r.awardedPrice },
  { key: 'min', title: 'Giá thấp nhất', width: 110, type: 'money', value: (r) => r.minPrice },
  { key: 'avg', title: 'Giá bình quân', width: 110, type: 'money', value: (r) => r.avgPrice },
  { key: 'max', title: 'Giá cao nhất', width: 110, type: 'money', value: (r) => r.maxPrice },
  { key: 'amount', title: 'Thành tiền', width: 130, type: 'money', value: (r) => r.awardedAmount },
  { key: 'savingAvg', title: 'Tiết kiệm so bình quân', width: 150, type: 'money', value: (r) => r.savingVsAvg },
  { key: 'savingMax', title: 'Tiết kiệm so cao nhất', width: 150, type: 'money', value: (r) => r.savingVsMax },
  { key: 'reason', title: 'Lý do không chọn giá thấp nhất', width: 220, value: (r) => r.awardReason },
]

/**
 * Báo cáo → Tiết kiệm từ đấu thầu (design.md mục 8.4, 11.3): mỗi dòng RFQ đã chọn thầu trong khoảng ngày, giá trúng so với giá chào thấp
 * nhất, bình quân, cao nhất (sau chiết khấu, trước VAT). Dòng không chọn giá thấp nhất được tô màu. Bấm đúp mở RFQ.
 */
export function SourcingSavingsReportPage() {
  const navigate = useNavigate()
  const [period, setPeriod] = useState(defaultPeriod)
  const report = useSourcingSavings(period)
  const rows = report.data ?? []
  const saving = rows.reduce((sum, r) => sum + r.savingVsAvg, 0)
  const amount = rows.reduce((sum, r) => sum + r.awardedAmount, 0)
  return (
    <ListPage
      title="Tiết kiệm từ đấu thầu"
      exportFileName="tiet-kiem-dau-thau"
      exportSubtitle={periodText(period)}
      canCreate={false}
      rows={rows}
      columns={savingColumns}
      rowKey={(r) => `${r.rfqId}|${r.itemCode}|${r.uomCode}|${r.qty}`}
      loading={report.isLoading}
      isMarked={(r) => !r.isLowest}
      onOpenRow={(r) => navigate(`/dau-thau/rfq/${r.rfqId}`)}
      notice={
        report.error ? (
          <Alert type="error" showIcon title={report.error.message} />
        ) : (
          <Alert
            type="info"
            showIcon
            title={`Giá trị trúng thầu ${formatMoney(amount)} đ, tiết kiệm so với giá chào bình quân ${formatMoney(saving)} đ. Tô màu: không chọn giá thấp nhất. Bấm đúp để mở RFQ.`}
          />
        )
      }
      onClearFilters={() => setPeriod(defaultPeriod())}
      filters={<PeriodFilters value={period} onChange={setPeriod} />}
    />
  )
}

/** Tên nguồn giá trên báo cáo giá mua (design.md mục 18.17). */
const priceSourceLabels: Record<PriceSourceKind, string> = {
  Rfq: 'Chọn thầu',
  Contract: 'Hợp đồng',
  PriceList: 'Bảng giá',
  Deviation: 'Lệch giá đã duyệt',
  None: 'Không có giá đã duyệt',
  Unknown: 'Không xác định',
}

/** Mục tiêu tỷ lệ mua theo giá đã duyệt của Ban CUVT (P5). */
const COMPLIANCE_TARGET = 95

/** Tuần trước, thứ Hai đến Chủ nhật – kỳ mặc định của báo cáo tuần. */
const lastWeek = (): PurchaseReportPeriod => {
  // dayjs().day(): 0 = Chủ nhật; quy về 0 = thứ Hai để không phụ thuộc ngôn ngữ của dayjs.
  const monday = dayjs().subtract((dayjs().day() + 6) % 7, 'day')
  return {
    from: monday.subtract(7, 'day').format('YYYY-MM-DD'),
    to: monday.subtract(1, 'day').format('YYYY-MM-DD'),
    allCompanies: false,
  }
}

/** Tháng trước – kỳ mặc định của báo cáo biến động giá. */
const lastMonth = (): PurchaseReportPeriod => {
  const month = dayjs().subtract(1, 'month')
  return { from: month.startOf('month').format('YYYY-MM-DD'), to: month.endOf('month').format('YYYY-MM-DD'), allCompanies: false }
}

const outOfContractColumns: GridColumn<OutOfContractLine>[] = [
  { key: 'date', title: 'Ngày PO', width: 90, type: 'date', value: (r) => r.date },
  { key: 'no', title: 'Số PO', width: 130, value: (r) => r.orderNo, groupable: true },
  { key: 'company', title: 'Công ty', width: 80, value: (r) => r.companyCode, groupable: true },
  { key: 'department', title: 'Bộ phận', width: 130, value: (r) => r.department, groupable: true },
  { key: 'supplier', title: 'Nhà cung cấp', width: 200, value: (r) => `${r.supplierCode} – ${r.supplierName}`, groupable: true },
  { key: 'item', title: 'Mặt hàng', width: 220, value: (r) => `${r.itemCode} – ${r.itemName}` },
  { key: 'uom', title: 'ĐVT', width: 60, value: (r) => r.uomCode },
  { key: 'qty', title: 'SL', width: 80, type: 'quantity', value: (r) => r.qty },
  { key: 'price', title: 'Giá mua (sau CK)', width: 120, type: 'money', value: (r) => r.netUnitPrice },
  { key: 'approved', title: 'Giá đã duyệt (sau CK)', width: 140, type: 'money', value: (r) => r.approvedNetPrice },
  { key: 'pct', title: 'Lệch (%)', width: 80, type: 'quantity', value: (r) => r.deviationPct },
  { key: 'amount', title: 'Thành tiền', width: 130, type: 'money', value: (r) => r.amount },
  { key: 'deviation', title: 'Tiền chênh', width: 120, type: 'money', value: (r) => r.deviationAmount },
  {
    key: 'kind',
    title: 'Loại',
    width: 160,
    type: 'status',
    value: (r) => priceSourceLabels[r.kind],
    tone: (r) => (r.kind === 'Deviation' ? 'warning' : 'error'),
    groupable: true,
  },
  { key: 'reason', title: 'Lý do lệch giá', width: 220, value: (r) => r.reason },
]

/**
 * Báo cáo → Giá mua & mua ngoài hợp đồng (báo cáo tuần – design.md mục 18.17): dòng PO đã duyệt trong kỳ theo nguồn giá, tỷ lệ mua theo
 * giá đã duyệt (KPI ≥ 95%), danh sách dòng lệch giá hoặc không có giá đã duyệt. Mặc định tuần trước. Bấm đúp mở PO.
 */
export function ContractComplianceReportPage() {
  const navigate = useNavigate()
  const [period, setPeriod] = useState(lastWeek)
  const report = useContractCompliance(period)
  const data = report.data
  const pct = data?.compliancePct
  const sources = (data?.sources ?? [])
    .map((s) => `${priceSourceLabels[s.source]} ${formatMoney(s.amount)} đ (${formatQuantity(s.sharePct)}%)`)
    .join('; ')
  return (
    <ListPage
      title="Giá mua & mua ngoài hợp đồng"
      exportFileName="gia-mua-ngoai-hop-dong"
      exportSubtitle={periodText(period)}
      canCreate={false}
      rows={data?.lines ?? []}
      columns={outOfContractColumns}
      rowKey={(r) => `${r.orderId}|${r.lineNo}`}
      loading={report.isLoading}
      isMarked={(r) => (r.deviationAmount ?? 0) > 0}
      onOpenRow={(r) => navigate(`/mua-hang/po/${r.orderId}`)}
      notice={
        report.error ? (
          <Alert type="error" showIcon title={report.error.message} />
        ) : (
          <Alert
            type={pct != null && pct < COMPLIANCE_TARGET ? 'warning' : 'info'}
            showIcon
            title={
              `${data?.orderCount ?? 0} PO đã duyệt, ${formatMoney(data?.totalAmount ?? 0)} đ (trước VAT). ` +
              `Mua theo giá đã duyệt: ${pct == null ? '–' : `${formatQuantity(pct)}%`} (mục tiêu ≥ ${COMPLIANCE_TARGET}%).`
            }
            description={sources ? `${sources}. Tô màu: mua cao hơn giá đã duyệt. Bấm đúp để mở PO.` : undefined}
          />
        )
      }
      onClearFilters={() => setPeriod(lastWeek())}
      filters={<PeriodFilters value={period} onChange={setPeriod} />}
    />
  )
}

const varianceColumns: GridColumn<PriceVarianceRow>[] = [
  { key: 'code', title: 'Mã hàng', width: 120, value: (r) => r.itemCode },
  { key: 'name', title: 'Tên hàng', width: 240, value: (r) => r.itemName },
  { key: 'uom', title: 'ĐVT cơ sở', width: 80, value: (r) => r.baseUomCode },
  { key: 'prevQty', title: 'SL kỳ trước', width: 100, type: 'quantity', value: (r) => r.previousQty },
  { key: 'prevPrice', title: 'Giá BQ kỳ trước', width: 120, type: 'money', value: (r) => r.previousAvgPrice },
  { key: 'qty', title: 'SL kỳ này', width: 100, type: 'quantity', value: (r) => r.currentQty },
  { key: 'price', title: 'Giá BQ kỳ này', width: 120, type: 'money', value: (r) => r.currentAvgPrice },
  { key: 'change', title: 'Thay đổi (%)', width: 100, type: 'quantity', value: (r) => r.changePct },
  { key: 'impact', title: 'Ảnh hưởng', width: 130, type: 'money', value: (r) => r.impactAmount },
  { key: 'min', title: 'Giá thấp nhất', width: 110, type: 'money', value: (r) => r.minPrice },
  { key: 'max', title: 'Giá cao nhất', width: 110, type: 'money', value: (r) => r.maxPrice },
  { key: 'orders', title: 'Số PO', width: 70, type: 'quantity', value: (r) => r.orderCount },
  { key: 'suppliers', title: 'Số NCC', width: 70, type: 'quantity', value: (r) => r.supplierCount },
]

/**
 * Báo cáo → Biến động giá mua (design.md mục 18.17): giá bình quân gia quyền theo ĐVT cơ sở (sau CK, trước VAT) của dòng PO đã duyệt
 * kỳ này so với kỳ trước liền trước (trọn tháng thì lùi theo tháng, còn lại cùng số ngày); ảnh hưởng = chênh giá × SL kỳ này. Mặc định tháng trước. Dòng tăng giá được tô màu.
 */
export function PriceVarianceReportPage() {
  const [period, setPeriod] = useState(lastMonth)
  const [categoryId, setCategoryId] = useState<string>()
  const categories = useItemCategories()
  const tree = useMemo(() => buildCategoryTree(categories.data ?? []), [categories.data])
  const report = usePriceVariance(period, categoryId)
  const data = report.data
  const previous = data ? `kỳ trước từ ${formatDate(data.previousFrom)} đến ${formatDate(data.previousTo)}` : 'kỳ trước'
  return (
    <ListPage
      title="Biến động giá mua"
      exportFileName="bien-dong-gia-mua"
      exportSubtitle={`${periodText(period)} – so ${previous}`}
      canCreate={false}
      rows={data?.items ?? []}
      columns={varianceColumns}
      rowKey={(r) => r.itemId}
      loading={report.isLoading}
      isMarked={(r) => (r.changePct ?? 0) > 0}
      notice={
        report.error ? (
          <Alert type="error" showIcon title={report.error.message} />
        ) : (
          <Alert
            type="info"
            showIcon
            title={`So với ${previous}: ảnh hưởng ${formatMoney(data?.impactAmount ?? 0)} đ (dương là mua đắt hơn). Giá theo PO đã duyệt, ĐVT cơ sở, sau chiết khấu, trước VAT. Tô màu: tăng giá.`}
          />
        )
      }
      onClearFilters={() => {
        setPeriod(lastMonth())
        setCategoryId(undefined)
      }}
      filters={
        <PeriodFilters
          value={period}
          onChange={setPeriod}
          extra={
            <FilterField label="Nhóm hàng (gồm nhóm con)" width={220}>
              {(id) => (
                <TreeSelect
                  id={id}
                  value={categoryId}
                  onChange={setCategoryId}
                  treeData={tree}
                  allowClear
                  showSearch
                  treeNodeFilterProp="title"
                  placeholder="Tất cả"
                />
              )}
            </FilterField>
          }
        />
      }
    />
  )
}
