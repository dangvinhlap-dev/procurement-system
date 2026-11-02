import type { FieldChange } from '../../api/changeHistory'
import { formatDateTime } from '../../utils/format'
import { ValueCell } from './HistoryParts'
import { actionLabel, changeValueText, userLabel } from './valueText'

/** Cột chung của bảng lịch sử: thời điểm, người, thao tác, giá trị cũ/mới. */
export const changeColumns = [
  { key: 'at', title: 'Ngày giờ', width: 130, render: (_: unknown, c: FieldChange) => formatDateTime(c.occurredAt) },
  { key: 'user', title: 'Người thao tác', width: 200, render: (_: unknown, c: FieldChange) => userLabel(c) },
  { key: 'action', title: 'Thao tác', width: 100, render: (_: unknown, c: FieldChange) => actionLabel(c) },
  { key: 'old', title: 'Giá trị cũ', width: 200, render: (_: unknown, c: FieldChange) => <ValueCell text={changeValueText(c, 'old')} /> },
  { key: 'new', title: 'Giá trị mới', width: 200, render: (_: unknown, c: FieldChange) => <ValueCell text={changeValueText(c, 'new')} /> },
]
