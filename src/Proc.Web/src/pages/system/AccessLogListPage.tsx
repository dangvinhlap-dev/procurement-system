import { Select } from 'antd'
import { useMemo, useState } from 'react'
import { defaultLogRange, useAccessLogs, type AccessEvent, type AccessLogItem } from '../../api/logs'
import type { GridColumn, StatusTone } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { LogDateRangeFilter, TruncatedNotice } from './logFilters'

/** Tên tiếng Việt và màu của sự kiện truy cập: đỏ = đáng chú ý (đăng nhập sai, bị khóa). */
const eventLabels: Record<AccessEvent, { label: string; tone: StatusTone }> = {
  Login: { label: 'Đăng nhập', tone: 'success' },
  LoginFailed: { label: 'Đăng nhập sai', tone: 'error' },
  LockedOut: { label: 'Bị khóa tạm', tone: 'error' },
  Logout: { label: 'Đăng xuất', tone: 'neutral' },
  PasswordChanged: { label: 'Đổi mật khẩu', tone: 'success' },
  PasswordChangeFailed: { label: 'Đổi mật khẩu lỗi', tone: 'warning' },
  CompanySwitched: { label: 'Đổi công ty', tone: 'neutral' },
}

const columns: GridColumn<AccessLogItem>[] = [
  { key: 'occurredAt', title: 'Thời điểm', width: 125, type: 'datetime', value: (r) => r.occurredAt },
  { key: 'user', title: 'Tên đăng nhập', width: 140, value: (r) => r.userName, groupable: true },
  {
    key: 'event',
    title: 'Sự kiện',
    width: 130,
    type: 'status',
    value: (r) => eventLabels[r.event]?.label ?? r.event,
    tone: (r) => eventLabels[r.event]?.tone ?? 'neutral',
    groupable: true,
  },
  { key: 'company', title: 'Công ty', width: 80, value: (r) => r.companyCode, groupable: true },
  { key: 'detail', title: 'Ghi chú', width: 280, value: (r) => r.detail },
  { key: 'ip', title: 'IP', width: 110, value: (r) => r.ipAddress, groupable: true },
  { key: 'agent', title: 'Trình duyệt', width: 220, value: (r) => r.userAgent },
]

/**
 * Màn hình Nhật ký truy cập (design.md mục 9.3): đăng nhập/đăng xuất, đăng nhập sai, bị khóa, đổi mật khẩu,
 * đổi công ty. Công ty thành viên thấy người làm việc ở công ty mình; tổng công ty thấy toàn bộ.
 */
export function AccessLogListPage() {
  const [range, setRange] = useState(defaultLogRange)
  const [event, setEvent] = useState('')
  const logs = useAccessLogs(range)

  const items = logs.data?.items
  const rows = useMemo(() => (items ?? []).filter((r) => !event || r.event === event), [items, event])

  return (
    <ListPage
      title="Nhật ký truy cập"
      exportFileName="nhat-ky-truy-cap"
      rows={rows}
      columns={columns}
      rowKey={(r) => r.id}
      loading={logs.isLoading}
      canCreate={false}
      notice={<TruncatedNotice truncated={logs.data?.truncated} />}
      onClearFilters={() => {
        setRange(defaultLogRange())
        setEvent('')
      }}
      filters={
        <>
          <LogDateRangeFilter value={range} onChange={setRange} />
          <FilterField label="Sự kiện" width={150}>
            {(id) => (
              <Select
                id={id}
                value={event}
                onChange={setEvent}
                options={[{ value: '', label: 'Tất cả' }, ...Object.entries(eventLabels).map(([value, e]) => ({ value, label: e.label }))]}
              />
            )}
          </FilterField>
        </>
      }
    />
  )
}
