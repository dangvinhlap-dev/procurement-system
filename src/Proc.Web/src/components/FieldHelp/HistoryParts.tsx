import type { RecordStamp } from '../../api/changeHistory'
import { formatDateTime } from '../../utils/format'

/** Ô trống trong lịch sử hiện chữ mờ "(trống)" để phân biệt với chuỗi rỗng không thấy được. */
export function ValueCell({ text }: { text: string }) {
  return text ? <span>{text}</span> : <span className="field-help-empty">(trống)</span>
}

/**
 * Dòng "Tạo bởi … lúc …; Sửa gần nhất bởi … lúc …" của bản ghi.
 * @param props.record Người, thời điểm tạo/sửa.
 */
export function RecordStampLine({ record }: { record: RecordStamp }) {
  return (
    <div className="field-help-stamp">
      <span>
        Tạo: <b>{record.createdBy ?? 'Hệ thống'}</b>
        {record.createdAt && ` lúc ${formatDateTime(record.createdAt)}`}
      </span>
      {record.updatedAt && (
        <span>
          Sửa gần nhất: <b>{record.updatedBy ?? 'Hệ thống'}</b> lúc {formatDateTime(record.updatedAt)}
        </span>
      )}
    </div>
  )
}
