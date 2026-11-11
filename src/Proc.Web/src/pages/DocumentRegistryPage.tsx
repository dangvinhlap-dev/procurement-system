import dayjs from 'dayjs'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router'
import {
  docTypeLabels,
  documentPath,
  progressLabels,
  useDocumentRegistry,
  type DocumentProgress,
  type DocumentRegistryRow,
} from '../api/documentRegistry'
import type { DateRange } from '../api/logs'
import { nextApproversText } from '../api/purchasing'
import { Select } from '../classic/Select'
import type { GridColumn } from '../components/DataGrid/types'
import { FilterField, ListPage } from '../components/ListPage/ListPage'
import { LogDateRangeFilter, TruncatedNotice } from './system/logFilters'

const progressTone = { Open: 'warning', Done: 'success', Cancelled: 'error' } as const

const columns: GridColumn<DocumentRegistryRow>[] = [
  { key: 'docType', title: 'Loại chứng từ', width: 150, value: (r) => docTypeLabels[r.docType] ?? r.docType, groupable: true },
  { key: 'no', title: 'Số chứng từ', width: 150, value: (r) => r.no, docRef: (r) => ({ docType: r.docType, id: r.id }) },
  { key: 'date', title: 'Ngày', width: 90, type: 'date', value: (r) => r.date },
  {
    key: 'progress',
    title: 'Tình trạng',
    width: 130,
    type: 'status',
    value: (r) => progressLabels[r.progress],
    tone: (r) => progressTone[r.progress],
    groupable: true,
  },
  { key: 'status', title: 'Trạng thái', width: 130, value: (r) => r.statusName, groupable: true },
  { key: 'pending', title: 'Đang chờ duyệt', width: 110, value: (r) => (r.pendingLevel ? `Cấp ${r.pendingLevel}` : null) },
  { key: 'nextApprovers', title: 'Người duyệt tiếp theo', width: 220, value: (r) => nextApproversText(r.nextApprovers) },
  { key: 'partner', title: 'Đối tác / kho', width: 260, value: (r) => r.partner, groupable: true },
  { key: 'description', title: 'Nội dung', width: 260, value: (r) => r.description },
  { key: 'amount', title: 'Giá trị', width: 130, type: 'money', value: (r) => r.amount },
  { key: 'currency', title: 'Tiền tệ', width: 60, value: (r) => (r.amount === null ? null : 'VND') },
  { key: 'createdBy', title: 'Người lập', width: 160, value: (r) => r.createdByName, groupable: true },
  { key: 'createdAt', title: 'Thời điểm lập', width: 120, type: 'datetime', value: (r) => r.createdAt },
]

/** Khoảng ngày mặc định: đầu tháng này → hôm nay. */
function defaultRange(): DateRange {
  return { from: dayjs().startOf('month').format('YYYY-MM-DD'), to: dayjs().format('YYYY-MM-DD') }
}

/**
 * Hằng ngày → Tra cứu chứng từ (yêu cầu người dùng 02/11/2026, design.md mục 18.22): mọi loại chứng từ của công ty đang làm việc – đề nghị,
 * đơn mua, phiếu nhập/xuất/chuyển kho, mua bán nội bộ, bảng giá, tìm nguồn, RFQ, hợp đồng, phụ lục, duyệt NCC, độc quyền, đánh giá NCC,
 * vi phạm – đã hoàn thành hay chưa. Lọc theo loại, tình trạng, "tôi lập" / "tôi đã duyệt"; bấm đúp mở trang đầy đủ của chứng từ.
 * Chỉ hiện loại người dùng có quyền xem, trong phạm vi công ty/kho (máy chủ lọc).
 */
export function DocumentRegistryPage() {
  const navigate = useNavigate()
  const [range, setRange] = useState<DateRange>(defaultRange)
  const [docTypes, setDocTypes] = useState<string[]>([])
  const [progress, setProgress] = useState<DocumentProgress | ''>('')
  const [mine, setMine] = useState<'' | 'created' | 'acted'>('')
  const registry = useDocumentRegistry({
    ...range,
    docTypes,
    progress: progress || undefined,
    mine: mine || undefined,
  })
  const data = registry.data
  const typeOptions = useMemo(
    () => (data?.docTypes ?? []).map((t) => ({ value: t, label: `${t} – ${docTypeLabels[t] ?? t}` })),
    [data?.docTypes],
  )

  return (
    <ListPage
      title="Tra cứu chứng từ"
      exportFileName="tra-cuu-chung-tu"
      rows={data?.rows ?? []}
      columns={columns}
      rowKey={(r) => `${r.docType}|${r.id}`}
      loading={registry.isLoading}
      canCreate={false}
      onOpenRow={(r) => {
        const path = documentPath(r.docType, r.id)
        if (path) navigate(path)
      }}
      notice={<TruncatedNotice truncated={data?.truncated} />}
      onClearFilters={() => {
        setRange(defaultRange())
        setDocTypes([])
        setProgress('')
        setMine('')
      }}
      filters={
        <>
          <LogDateRangeFilter value={range} onChange={setRange} />
          <FilterField label="Loại chứng từ" width={320}>
            {(id) => (
              <Select
                id={id}
                mode="multiple"
                allowClear
                maxTagCount="responsive"
                placeholder="Tất cả loại được xem"
                value={docTypes}
                onChange={setDocTypes}
                showSearch={{ optionFilterProp: 'label' }}
                options={typeOptions}
              />
            )}
          </FilterField>
          <FilterField label="Tình trạng" width={170}>
            {(id) => (
              <Select
                id={id}
                value={progress}
                onChange={setProgress}
                options={[
                  { value: '', label: 'Tất cả' },
                  { value: 'Open', label: progressLabels.Open },
                  { value: 'Done', label: progressLabels.Done },
                  { value: 'Cancelled', label: progressLabels.Cancelled },
                ]}
              />
            )}
          </FilterField>
          <FilterField label="Người thực hiện" width={170}>
            {(id) => (
              <Select
                id={id}
                value={mine}
                onChange={setMine}
                options={[
                  { value: '', label: 'Mọi người' },
                  { value: 'created', label: 'Tôi lập' },
                  { value: 'acted', label: 'Tôi đã duyệt / ghi sổ' },
                ]}
              />
            )}
          </FilterField>
        </>
      }
    />
  )
}
