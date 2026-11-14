import { useState } from 'react'
import { defaultLogRange } from '../../api/logs'
import { useOperationLogs, type OperationLogItem, type OperationResult } from '../../api/operations'
import type { GridColumn, StatusTone } from '../../components/DataGrid/types'
import { ListPage } from '../../components/ListPage/ListPage'
import { LogDateRangeFilter, TruncatedNotice } from './logFilters'

/** Tên tiếng Việt của loại việc vận hành; loại chưa có tên hiện nguyên mã. */
const kindLabels: Record<string, string> = {
  AppStarted: 'Khởi động ứng dụng',
}

const resultLabels: Record<OperationResult, { label: string; tone: StatusTone }> = {
  Succeeded: { label: 'Thành công', tone: 'success' },
  Failed: { label: 'Lỗi', tone: 'error' },
  Info: { label: 'Ghi nhận', tone: 'neutral' },
}

const columns: GridColumn<OperationLogItem>[] = [
  { key: 'occurredAt', title: 'Thời điểm', width: 125, type: 'datetime', value: (r) => r.occurredAt },
  { key: 'environment', title: 'Môi trường', width: 90, value: (r) => r.environment, groupable: true },
  { key: 'user', title: 'Người làm', width: 130, value: (r) => r.userName ?? 'Hệ thống', groupable: true },
  { key: 'kind', title: 'Việc', width: 170, value: (r) => kindLabels[r.kind] ?? r.kind, groupable: true },
  { key: 'target', title: 'Đối tượng', width: 140, value: (r) => r.target },
  {
    key: 'result',
    title: 'Kết quả',
    width: 100,
    type: 'status',
    value: (r) => resultLabels[r.result]?.label ?? r.result,
    tone: (r) => resultLabels[r.result]?.tone ?? 'neutral',
    groupable: true,
  },
  { key: 'reason', title: 'Lý do', width: 200, value: (r) => r.reason },
  { key: 'detail', title: 'Chi tiết', width: 320, value: (r) => r.detail },
]

/**
 * Màn hình Nhật ký vận hành (design.md mục 21.2 – N3, lát QT1): mọi thao tác quản trị vận hành và mỗi lần khởi động ứng dụng – ai, lúc nào,
 * môi trường, việc, lý do, kết quả. Bảng chỉ thêm; bản ngoài CSDL nằm ở file ngày trong thư mục Operations:LogDirectory trên máy chủ.
 */
export function OperationLogListPage() {
  const [range, setRange] = useState(defaultLogRange)
  const logs = useOperationLogs(range)
  return (
    <ListPage
      title="Nhật ký vận hành"
      exportFileName="nhat-ky-van-hanh"
      rows={logs.data?.items ?? []}
      columns={columns}
      rowKey={(r) => r.id}
      loading={logs.isLoading}
      canCreate={false}
      notice={<TruncatedNotice truncated={logs.data?.truncated} />}
      onClearFilters={() => setRange(defaultLogRange())}
      filters={<LogDateRangeFilter value={range} onChange={setRange} />}
    />
  )
}
