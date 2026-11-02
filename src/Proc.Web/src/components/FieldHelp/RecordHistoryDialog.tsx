import { Alert, DatePicker, Modal, Select, Spin, Table } from 'antd'
import type { Dayjs } from 'dayjs'
import dayjs from 'dayjs'
import { useMemo, useState } from 'react'
import { useRecordHistory, type FieldChange } from '../../api/changeHistory'
import { DATE_INPUT_FORMATS } from '../../utils/format'
import { changeColumns } from './historyColumns'
import { RecordStampLine } from './HistoryParts'
import { fieldLabel, userLabel } from './valueText'
import './FieldHelp.css'

/** Thuộc tính của RecordHistoryDialog. */
export interface RecordHistoryDialogProps {
  open: boolean
  onClose: () => void
  /** Mã loại đối tượng của bản ghi, ví dụ PurchaseOrder. */
  entityType: string
  entityId: string | null
  /** Nhãn ô trên màn hình theo tên ô viết thường (collectFieldLabels) – để ghi tên ô như trên phiếu. */
  screenLabels: Record<string, string>
}

/**
 * Hộp thoại "Lịch sử thay đổi" của cả bản ghi – như "Hiển thị thay đổi" của ERP (design.md mục 9.3a): mọi lần tạo, sửa, xóa của đầu phiếu
 * và các dòng (kể cả dòng đã xóa), lọc theo ô, người thao tác, khoảng ngày.
 */
export function RecordHistoryDialog({ open, onClose, entityType, entityId, screenLabels }: RecordHistoryDialogProps) {
  const history = useRecordHistory(entityType, open ? entityId : null)
  const [field, setField] = useState<string | null>(null)
  const [user, setUser] = useState<string | null>(null)
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null)
  const changes = useMemo(() => history.data?.changes ?? [], [history.data])

  // Dòng con đã xóa: có dòng nhật ký Delete – ghi chú "(đã xóa)" cạnh số dòng.
  const deleted = useMemo(() => new Set(changes.filter((c) => c.action === 'Delete').map((c) => c.entityId)), [changes])

  const fieldOptions = useMemo(
    () =>
      [...new Map(changes.map((c) => [c.field, fieldLabel(c.field, screenLabels)])).entries()]
        .map(([value, label]) => ({ value, label }))
        .sort((a, b) => a.label.localeCompare(b.label, 'vi')),
    [changes, screenLabels],
  )
  const userOptions = useMemo(
    () => [...new Map(changes.map((c) => [c.userName ?? '', userLabel(c)])).entries()].map(([value, label]) => ({ value, label })),
    [changes],
  )

  const rows = changes.filter((c) => {
    if (field && c.field !== field) return false
    if (user !== null && (c.userName ?? '') !== user) return false
    const day = dayjs(c.occurredAt)
    if (range?.[0] && day.isBefore(range[0].startOf('day'))) return false
    if (range?.[1] && day.isAfter(range[1].endOf('day'))) return false
    return true
  })

  const columns = [
    changeColumns[0],
    changeColumns[1],
    changeColumns[2],
    {
      key: 'object',
      title: 'Đối tượng',
      width: 170,
      render: (_: unknown, c: FieldChange) =>
        c.entityType === entityType
          ? 'Đầu phiếu'
          : `${c.entityName}${c.lineNo ? ` – dòng ${c.lineNo}` : ''}${deleted.has(c.entityId) ? ' (đã xóa)' : ''}`,
    },
    { key: 'field', title: 'Ô', width: 160, render: (_: unknown, c: FieldChange) => fieldLabel(c.field, screenLabels) },
    changeColumns[3],
    changeColumns[4],
  ]

  return (
    <Modal
      open={open}
      title={`Lịch sử thay đổi${history.data?.record.label ? `: ${history.data.record.label}` : ''}`}
      onCancel={onClose}
      footer={null}
      width={1150}
      destroyOnHidden
      className="field-help-dialog"
    >
      {history.error ? (
        <Alert type="error" showIcon title={history.error.message} />
      ) : (
        <Spin spinning={history.isLoading}>
          {history.data && <RecordStampLine record={history.data.record} />}
          <div className="field-help-filters">
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder="Ô"
              aria-label="Lọc theo ô"
              style={{ width: 220 }}
              options={fieldOptions}
              value={field}
              onChange={(v) => setField(v ?? null)}
            />
            <Select
              allowClear
              placeholder="Người thao tác"
              aria-label="Lọc theo người thao tác"
              style={{ width: 240 }}
              options={userOptions}
              value={user}
              onChange={(v) => setUser(v ?? null)}
            />
            <DatePicker.RangePicker
              format={DATE_INPUT_FORMATS}
              placeholder={['Từ ngày', 'Đến ngày']}
              value={range}
              onChange={(v) => setRange(v)}
            />
            <span className="field-help-count">{rows.length} thay đổi</span>
          </div>
          <Table<FieldChange>
            size="small"
            bordered
            pagination={false}
            rowKey={(c) => `${c.logId}-${c.field}`}
            dataSource={rows}
            columns={columns}
            scroll={{ y: 420 }}
            aria-label="Lịch sử thay đổi của phiếu"
          />
          {history.data?.truncated && <Alert type="warning" showIcon className="field-help-alert" title="Chỉ hiện các lần thay đổi gần nhất." />}
        </Spin>
      )}
    </Modal>
  )
}
