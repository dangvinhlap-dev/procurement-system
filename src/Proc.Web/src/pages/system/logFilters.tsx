import { Alert, DatePicker } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import type { DateRange } from '../../api/logs'
import { FilterField } from '../../components/ListPage/ListPage'

/**
 * Ô lọc "Từ ngày – Đến ngày" (dd/MM/yyyy) của màn hình nhật ký; lọc ở server vì nhật ký rất nhiều dòng.
 * @param value Khoảng ngày đang chọn.
 * @param onChange Đổi khoảng ngày.
 */
export function LogDateRangeFilter({ value, onChange }: { value: DateRange; onChange: (range: DateRange) => void }) {
  return (
    <FilterField label="Từ ngày – Đến ngày" width={210}>
      {(id) => (
        <DatePicker.RangePicker
          id={id}
          format="DD/MM/YYYY"
          allowClear={false}
          value={[dayjs(value.from), dayjs(value.to)]}
          onChange={(dates) => {
            const [from, to] = (dates ?? []) as [Dayjs | null, Dayjs | null]
            if (from && to) onChange({ from: from.format('YYYY-MM-DD'), to: to.format('YYYY-MM-DD') })
          }}
        />
      )}
    </FilterField>
  )
}

/** Báo kết quả bị cắt bớt (server trả tối đa 2.000 dòng mỗi lần). */
export function TruncatedNotice({ truncated }: { truncated?: boolean }) {
  return truncated ? (
    <Alert
      type="warning"
      showIcon
      style={{ marginBottom: 6 }}
      title="Chỉ hiện 2.000 dòng mới nhất. Hãy thu hẹp khoảng ngày để xem đủ."
    />
  ) : null
}
