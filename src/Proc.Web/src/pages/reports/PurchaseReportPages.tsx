import { Alert, Checkbox, Select } from 'antd'
import dayjs from 'dayjs'
import { useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { priorityLabels } from '../../api/purchasing'
import {
  usePendingRequests,
  usePurchaseSpend,
  useSourcingSavings,
  useSupplierEvaluation,
  type PendingRequestLine,
  type PurchaseReportPeriod,
  type PurchaseSpendGroup,
  type PurchaseSpendRow,
  type SourcingSavingRow,
  type SupplierEvaluationRow,
} from '../../api/reports'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatDate, formatMoney } from '../../utils/format'
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
            title="Đúng hạn: ngày phiếu nhập ≤ ngày giao của dòng PO (không có thì ngày giao dự kiến của PO). Tô màu: có giao trễ hoặc trả hàng."
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
