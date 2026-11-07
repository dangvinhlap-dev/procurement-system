import { ExportOutlined, FileAddOutlined } from '@ant-design/icons'
import { Alert, Button, Checkbox, Descriptions, InputNumber, Modal } from 'antd'
import { Select } from '../../classic/Select'
import dayjs from 'dayjs'
import { useMemo, useState } from 'react'
import { useLocation, useNavigate, useSearchParams } from 'react-router'
import {
  anomalyLabels,
  findingSeverityLabels,
  useAnomalies,
  useControlFindings,
  type Anomaly,
  type AnomalyQuery,
  type ControlFinding,
  type FindingPrefillState,
} from '../../api/control'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatDate, formatMoney, vnNumberInput } from '../../utils/format'
import { LogDateRangeFilter } from '../system/logFilters'
import { ControlFindingDrawer } from './ControlFindingDrawer'
import { controlRefPath } from './controlPaths'

/** Mặc định: từ đầu tháng trước đến hôm nay – KSNB rà định kỳ, không bỏ sót phát sinh của tháng đang chạy. */
const defaultQuery = (): AnomalyQuery => ({
  from: dayjs().subtract(1, 'month').startOf('month').format('YYYY-MM-DD'),
  to: dayjs().format('YYYY-MM-DD'),
  allCompanies: false,
  priceGapPct: 10,
  nearThresholdPct: 10,
  nearThresholdCount: 3,
  offContractPct: 5,
})

const severityText = (r: Anomaly) => (r.severity === 'High' ? 'Cao' : 'Trung bình')

const anomalyColumns: GridColumn<Anomaly>[] = [
  { key: 'severity', title: 'Mức', width: 100, type: 'status', value: severityText, tone: (r) => (r.severity === 'High' ? 'error' : 'warning'), groupable: true },
  { key: 'code', title: 'Dấu hiệu', width: 200, value: (r) => anomalyLabels[r.code], groupable: true },
  { key: 'date', title: 'Ngày', width: 90, type: 'date', value: (r) => r.date },
  { key: 'company', title: 'Công ty', width: 80, value: (r) => r.companyCode, groupable: true },
  { key: 'refNo', title: 'Chứng từ', width: 140, value: (r) => r.refNo },
  { key: 'supplier', title: 'Nhà cung cấp', width: 200, value: (r) => (r.supplierCode ? `${r.supplierCode} – ${r.supplierName ?? ''}` : null), groupable: true },
  { key: 'user', title: 'Người liên quan', width: 150, value: (r) => r.userName, groupable: true },
  { key: 'amount', title: 'Số tiền', width: 130, type: 'money', value: (r) => r.amount },
  { key: 'detail', title: 'Chi tiết', width: 480, value: (r) => r.detail },
]

/**
 * Báo cáo → Dấu hiệu bất thường (ANOM – KSNB, design.md mục 18.19): giá cao lặp lại một NCC, PO sát dưới ngưỡng duyệt, đổi tài khoản
 * ngân hàng NCC, mua ngoài hợp đồng cao, hóa đơn tính cả hàng thiếu, hàng/hóa đơn trước PO, xung đột lợi ích. Chỉ đọc; ngưỡng chỉnh trên
 * bộ lọc. Bấm đúp một dòng: xem chi tiết, mở chứng từ hoặc ghi nhận vi phạm (KSNB).
 */
export function AnomalyReportPage() {
  const navigate = useNavigate()
  const { hasPermission, workingCompany } = useAuth()
  const [query, setQuery] = useState(defaultQuery)
  const [selected, setSelected] = useState<Anomaly | null>(null)
  const report = useAnomalies(query)
  const data = report.data
  const high = (data?.items ?? []).filter((i) => i.severity === 'High').length
  const summary = (data?.summary ?? [])
    .map((s) => `${anomalyLabels[s.code]}: ${s.measured ? s.count : 'chưa đo'}`)
    .join('; ')
  const canRecord = hasPermission(Permission.ControlFindingManage)
  const selectedPath = selected ? controlRefPath(selected.screenCode, selected.targetId) : null

  const record = (a: Anomaly) => {
    const state: FindingPrefillState = {
      findingPrefill: {
        date: dayjs().format('YYYY-MM-DD'),
        severity: a.severity === 'High' ? 'Serious' : 'Normal',
        title: anomalyLabels[a.code] + (a.refNo ? ` – ${a.refNo}` : ''),
        description: [a.detail, a.userName ? `Người liên quan: ${a.userName}.` : null].filter(Boolean).join(' '),
        supplierId: null,
        anomalyCode: a.code,
        refScreen: a.screenCode,
        refId: a.targetId,
        refNo: a.refNo,
      },
    }
    navigate('/bao-cao/so-vi-pham', { state })
  }

  const number = (key: 'priceGapPct' | 'nearThresholdPct' | 'nearThresholdCount' | 'offContractPct', label: string, min: number, max: number) => (
    <FilterField label={label} width={130}>
      {(id) => (
        <InputNumber
          id={id}
          {...vnNumberInput}
          min={min}
          max={max}
          value={query[key]}
          onChange={(v) => v != null && setQuery({ ...query, [key]: v })}
          style={{ width: '100%' }}
        />
      )}
    </FilterField>
  )

  return (
    <>
      <ListPage
        title="Dấu hiệu bất thường (KSNB)"
        exportFileName="dau-hieu-bat-thuong"
        exportSubtitle={`Từ ${formatDate(query.from)} đến ${formatDate(query.to)}${query.allCompanies ? ' – toàn tập đoàn' : ''}`}
        canCreate={false}
        rows={data?.items ?? []}
        columns={anomalyColumns}
        rowKey={(r) => `${r.code}|${r.companyId}|${r.targetId ?? ''}|${r.userName ?? ''}|${r.detail}`}
        loading={report.isLoading}
        isMarked={(r) => r.severity === 'High'}
        onOpenRow={setSelected}
        notice={
          report.error ? (
            <Alert type="error" showIcon title={report.error.message} />
          ) : (
            <Alert
              type={high > 0 ? 'warning' : 'info'}
              showIcon
              title={`${data?.items.length ?? 0} dấu hiệu, ${high} mức cao. Tô màu: mức cao. Bấm đúp để xem, mở chứng từ hoặc ghi nhận vi phạm.`}
              description={summary || undefined}
            />
          )
        }
        onClearFilters={() => setQuery(defaultQuery())}
        filters={
          <>
            <LogDateRangeFilter value={query} onChange={(range) => setQuery({ ...query, ...range })} />
            {number('priceGapPct', 'Chênh giá từ (%)', 1, 1000)}
            {number('nearThresholdPct', 'Sát ngưỡng trong (%)', 1, 99)}
            {number('nearThresholdCount', 'Số PO sát ngưỡng từ', 2, 100)}
            {number('offContractPct', 'Ngoài HĐ trên (%)', 0, 99)}
            {hasPermission(Permission.PurchaseViewAllCompanies) && (
              <FilterField label="Phạm vi" width={140}>
                {(id) => (
                  <Checkbox id={id} checked={query.allCompanies} onChange={(e) => setQuery({ ...query, allCompanies: e.target.checked })}>
                    Toàn tập đoàn
                  </Checkbox>
                )}
              </FilterField>
            )}
          </>
        }
      />
      {selected && (
        <Modal
          open
          title={`${anomalyLabels[selected.code]} – mức ${severityText(selected).toLowerCase()}`}
          onCancel={() => setSelected(null)}
          width={640}
          footer={
            <>
              {selectedPath && (
                <Button icon={<ExportOutlined />} onClick={() => navigate(selectedPath)}>
                  Mở chứng từ
                </Button>
              )}
              {canRecord && (
                <Button
                  icon={<FileAddOutlined />}
                  disabled={selected.companyId !== workingCompany?.id}
                  title={
                    selected.companyId !== workingCompany?.id
                      ? `Dấu hiệu của công ty ${selected.companyCode} – chuyển sang công ty đó để ghi nhận vi phạm.`
                      : 'Ghi nhận vi phạm vào sổ, thông báo lãnh đạo'
                  }
                  onClick={() => record(selected)}
                >
                  Ghi nhận vi phạm
                </Button>
              )}
              <Button onClick={() => setSelected(null)}>Đóng</Button>
            </>
          }
        >
          <Descriptions
            column={1}
            size="small"
            bordered
            items={[
              { key: 'date', label: 'Ngày', children: formatDate(selected.date) },
              { key: 'company', label: 'Công ty', children: selected.companyCode },
              { key: 'ref', label: 'Chứng từ', children: selected.refNo ?? '–' },
              { key: 'supplier', label: 'Nhà cung cấp', children: selected.supplierCode ? `${selected.supplierCode} – ${selected.supplierName ?? ''}` : '–' },
              { key: 'user', label: 'Người liên quan', children: selected.userName ?? '–' },
              { key: 'amount', label: 'Số tiền', children: selected.amount == null ? '–' : `${formatMoney(selected.amount)} đ` },
              { key: 'detail', label: 'Chi tiết', children: selected.detail },
            ]}
          />
        </Modal>
      )}
    </>
  )
}

const findingColumns: GridColumn<ControlFinding>[] = [
  { key: 'no', title: 'Số', width: 140, value: (r) => r.no },
  { key: 'date', title: 'Ngày phát hiện', width: 100, type: 'date', value: (r) => r.date },
  {
    key: 'severity',
    title: 'Mức',
    width: 110,
    type: 'status',
    value: (r) => findingSeverityLabels[r.severity],
    tone: (r) => (r.severity === 'Serious' ? 'error' : 'warning'),
    groupable: true,
  },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 120,
    type: 'status',
    value: (r) => (r.status === 'Open' ? 'Đang xác minh' : 'Đã kết luận'),
    tone: (r) => (r.status === 'Open' ? 'warning' : 'success'),
    groupable: true,
  },
  { key: 'title', title: 'Tiêu đề', width: 280, value: (r) => r.title },
  { key: 'anomalyCode', title: 'Từ dấu hiệu', width: 180, value: (r) => (r.anomalyCode ? anomalyLabels[r.anomalyCode] : null), groupable: true },
  { key: 'refNo', title: 'Chứng từ', width: 140, value: (r) => r.refNo },
  { key: 'supplier', title: 'Nhà cung cấp', width: 200, value: (r) => (r.supplierCode ? `${r.supplierCode} – ${r.supplierName ?? ''}` : null), groupable: true },
  { key: 'conclusion', title: 'Kết luận', width: 280, value: (r) => r.conclusion },
  { key: 'createdBy', title: 'Người ghi nhận', width: 150, value: (r) => r.createdByName, auditField: false },
]

/**
 * Báo cáo → Sổ vi phạm (VIOL – KSNB, design.md mục 18.19): KSNB ghi nhận, sửa khi đang xác minh, kết luận; lãnh đạo nhận thông báo và xem.
 * Mở từ thông báo bằng `?mo=<id>`; từ màn hình dấu hiệu bất thường thì mở sẵn phiếu ghi nhận mới đã điền nội dung.
 */
export function ControlFindingListPage() {
  const { hasPermission } = useAuth()
  const location = useLocation()
  const findings = useControlFindings()
  const [params, setParams] = useSearchParams()
  const [status, setStatus] = useState('')
  const prefill = (location.state as FindingPrefillState | null)?.findingPrefill
  // "mo" = phiếu đang mở; "new" = đang ghi nhận mới; có nội dung điền sẵn thì mở phiếu mới ngay.
  const openId = params.get('mo') ?? (prefill ? 'new' : null)
  // Đổi tham số tìm kiếm là điều hướng mới không mang state, nên nội dung điền sẵn tự mất – đóng ngăn kéo không mở lại phiếu mới.
  const open = (id: string | null) => setParams(id ? { mo: id } : {}, { replace: true })
  const rows = useMemo(() => (findings.data ?? []).filter((r) => !status || r.status === status), [findings.data, status])
  const seriousOpen = (findings.data ?? []).filter((r) => r.severity === 'Serious' && r.status === 'Open').length

  return (
    <>
      <ListPage
        title="Sổ vi phạm (KSNB)"
        exportFileName="so-vi-pham"
        rows={rows}
        columns={findingColumns}
        rowKey={(r) => r.id}
        auditEntity="ControlFinding"
        loading={findings.isLoading}
        canCreate={hasPermission(Permission.ControlFindingManage)}
        onCreate={() => open('new')}
        onOpenRow={(r) => open(r.id)}
        isMarked={(r) => r.severity === 'Serious' && r.status === 'Open'}
        notice={
          findings.error ? (
            <Alert type="error" showIcon title={findings.error.message} />
          ) : seriousOpen > 0 ? (
            <Alert type="warning" showIcon title={`${seriousOpen} vi phạm nghiêm trọng đang xác minh.`} />
          ) : undefined
        }
        onClearFilters={() => setStatus('')}
        filters={
          <FilterField label="Trạng thái" width={150}>
            {(id) => (
              <Select
                id={id}
                value={status}
                onChange={setStatus}
                options={[
                  { value: '', label: 'Tất cả' },
                  { value: 'Open', label: 'Đang xác minh' },
                  { value: 'Concluded', label: 'Đã kết luận' },
                ]}
              />
            )}
          </FilterField>
        }
      />
      {openId && (
        <ControlFindingDrawer
          id={openId === 'new' ? null : openId}
          prefill={openId === 'new' ? prefill : undefined}
          onClose={() => open(null)}
          onOpen={open}
        />
      )}
    </>
  )
}
