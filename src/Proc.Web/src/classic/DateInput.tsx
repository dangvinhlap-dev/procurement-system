import { DatePicker } from 'antd'
import dayjs from 'dayjs'
import type { CSSProperties } from 'react'
import { DATE_INPUT_FORMATS } from '../utils/format'

/**
 * Ô ngày: gõ tay hoặc chọn trên lịch (docs/ui-style.md mục 5). Dùng DatePicker của antd thay cho input type="date"
 * vì ô ngày của trình duyệt hiển thị theo ngôn ngữ hệ điều hành (có máy ra mm/dd/yyyy).
 * @param value Ngày ISO "yyyy-MM-dd" hoặc null.
 * @param onChange Nhận ngày ISO mới hoặc null khi xóa trắng.
 * @param borderless Không viền – dùng trong ô của lưới nhập.
 */
export function DateInput({
  value,
  onChange,
  borderless,
  id,
  style,
  ...rest
}: {
  value: string | null
  onChange: (value: string | null) => void
  borderless?: boolean
  id?: string
  style?: CSSProperties
  'aria-label'?: string
  'data-row'?: number
  'data-col'?: number
}) {
  return (
    <DatePicker
      {...rest}
      id={id}
      style={style}
      className="erp-date"
      variant={borderless ? 'borderless' : 'outlined'}
      format={DATE_INPUT_FORMATS}
      placeholder="dd/mm/yyyy"
      value={value ? dayjs(value) : null}
      onChange={(d) => onChange(d ? d.format('YYYY-MM-DD') : null)}
    />
  )
}
