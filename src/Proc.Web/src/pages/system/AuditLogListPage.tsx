import { Descriptions, Select, Table } from 'antd'
import { useMemo, useState } from 'react'
import { defaultLogRange, useAuditLog, useAuditLogs, type AuditLogItem } from '../../api/logs'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn, StatusTone } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatDateTime } from '../../utils/format'
import { LogDateRangeFilter, TruncatedNotice } from './logFilters'

/** Tên tiếng Việt của hành động (design.md mục 9.3); hành động mới chưa có tên thì hiện mã. */
const actionLabels: Record<string, { label: string; tone: StatusTone }> = {
  Create: { label: 'Tạo', tone: 'success' },
  Update: { label: 'Sửa', tone: 'warning' },
  Delete: { label: 'Xóa', tone: 'error' },
}

const actionLabel = (action: string) => actionLabels[action]?.label ?? action

/** Người thao tác; dòng không có người là do hệ thống tự làm (khởi tạo dữ liệu, job nền). */
const actorOf = (r: AuditLogItem) => r.userName ?? '(hệ thống)'

const columns: GridColumn<AuditLogItem>[] = [
  { key: 'occurredAt', title: 'Thời điểm', width: 125, type: 'datetime', value: (r) => r.occurredAt },
  { key: 'user', title: 'Người thao tác', width: 130, value: actorOf, groupable: true },
  {
    key: 'action',
    title: 'Hành động',
    width: 90,
    type: 'status',
    value: (r) => actionLabel(r.action),
    tone: (r) => actionLabels[r.action]?.tone ?? 'neutral',
    groupable: true,
  },
  { key: 'entity', title: 'Loại đối tượng', width: 140, value: (r) => r.entityName, groupable: true },
  { key: 'label', title: 'Đối tượng', width: 170, value: (r) => r.documentNumber ?? r.entityLabel },
  { key: 'fields', title: 'Trường thay đổi', width: 260, value: (r) => r.changedFields.join(', ') },
  { key: 'ip', title: 'IP', width: 110, value: (r) => r.ipAddress },
]

/**
 * Màn hình Nhật ký thao tác (design.md mục 9.3): mọi thay đổi dữ liệu tại công ty đang làm việc.
 * Lọc khoảng ngày ở server; lọc loại đối tượng, hành động, tìm theo người ở lưới. Bấm đúp xem so sánh trước/sau.
 */
export function AuditLogListPage() {
  const { workingCompany } = useAuth()
  const [range, setRange] = useState(defaultLogRange)
  const [entity, setEntity] = useState('')
  const [action, setAction] = useState('')
  const [openId, setOpenId] = useState<string | null>(null)
  const logs = useAuditLogs(range)

  const items = logs.data?.items
  const entityOptions = useMemo(
    () => [...new Map((items ?? []).map((r) => [r.entityType, r.entityName])).entries()].sort((a, b) => a[1].localeCompare(b[1], 'vi')),
    [items],
  )
  const rows = useMemo(
    () => (items ?? []).filter((r) => (!entity || r.entityType === entity) && (!action || r.action === action)),
    [items, entity, action],
  )

  return (
    <>
      <ListPage
        title={`Nhật ký thao tác – ${workingCompany?.code ?? ''}`}
        exportFileName="nhat-ky-thao-tac"
        rows={rows}
        columns={columns}
        rowKey={(r) => r.id}
        loading={logs.isLoading}
        canCreate={false}
        notice={<TruncatedNotice truncated={logs.data?.truncated} />}
        onOpenRow={(r) => setOpenId(r.id)}
        onClearFilters={() => {
          setRange(defaultLogRange())
          setEntity('')
          setAction('')
        }}
        filters={
          <>
            <LogDateRangeFilter value={range} onChange={setRange} />
            <FilterField label="Loại đối tượng" width={130}>
              {(id) => (
                <Select
                  id={id}
                  value={entity}
                  onChange={setEntity}
                  options={[{ value: '', label: 'Tất cả' }, ...entityOptions.map(([value, label]) => ({ value, label }))]}
                />
              )}
            </FilterField>
            <FilterField label="Hành động" width={95}>
              {(id) => (
                <Select
                  id={id}
                  value={action}
                  onChange={setAction}
                  options={[{ value: '', label: 'Tất cả' }, ...Object.entries(actionLabels).map(([value, a]) => ({ value, label: a.label }))]}
                />
              )}
            </FilterField>
          </>
        }
      />
      {openId && <AuditLogDrawer id={openId} onClose={() => setOpenId(null)} />}
    </>
  )
}

/** Ngăn kéo chi tiết một dòng nhật ký: so sánh trước/sau và thông tin request. Chỉ xem. */
function AuditLogDrawer({ id, onClose }: { id: string; onClose: () => void }) {
  const detail = useAuditLog(id)
  const log = detail.data
  return (
    <DetailDrawer
      open
      title={log ? `${actionLabel(log.item.action)} ${log.item.entityName.toLowerCase()}: ${log.item.documentNumber ?? log.item.entityLabel ?? log.item.entityId}` : 'Nhật ký thao tác'}
      onClose={onClose}
      loading={detail.isLoading}
      error={detail.error?.message}
      tabs={[
        {
          key: 'changes',
          label: 'Trước / sau',
          children: (
            <Table
              size="small"
              bordered
              pagination={false}
              rowKey="field"
              dataSource={log?.changes ?? []}
              columns={[
                { title: 'Trường', dataIndex: 'field', width: 170 },
                { title: 'Giá trị cũ', dataIndex: 'oldValue', render: (v: string | null) => v ?? '–' },
                { title: 'Giá trị mới', dataIndex: 'newValue', render: (v: string | null) => v ?? '–' },
              ]}
            />
          ),
        },
        {
          key: 'info',
          label: 'Thông tin chung',
          children: log && (
            <Descriptions
              size="small"
              bordered
              column={1}
              items={[
                { key: 'time', label: 'Thời điểm', children: formatDateTime(log.item.occurredAt) },
                { key: 'user', label: 'Người thao tác', children: actorOf(log.item) },
                { key: 'entity', label: 'Mã bản ghi', children: `${log.item.entityType} ${log.item.entityId}` },
                { key: 'ip', label: 'IP', children: log.item.ipAddress ?? '–' },
                { key: 'agent', label: 'Trình duyệt', children: log.userAgent ?? '–' },
                { key: 'correlation', label: 'Mã liên kết request', children: log.correlationId ?? '–' },
              ]}
            />
          ),
        },
      ]}
    />
  )
}
