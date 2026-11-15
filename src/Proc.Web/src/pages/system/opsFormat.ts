/* Định dạng dùng chung cho màn hình quản trị vận hành BAK, DBM (lát QT3b, QT4). */

/** Dung lượng dễ đọc kiểu Việt Nam (1.536 MB → "1,5 GB"), như SYS.
 * @param bytes Số byte; null = để trống.
 */
export function formatBytes(bytes: number | null): string {
  if (bytes == null) return ''
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let value = bytes
  let unit = 0
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }
  return `${value.toLocaleString('vi-VN', { maximumFractionDigits: unit === 0 ? 0 : 1, minimumFractionDigits: unit === 0 ? 0 : 1 })} ${units[unit]}`
}

/** Thời gian chạy: "45 giây", "12 phút 5 giây", "1 giờ 20 phút".
 * @param seconds Số giây; null = để trống.
 */
export function formatDuration(seconds: number | null): string {
  if (seconds == null) return ''
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  const s = seconds % 60
  if (h > 0) return `${h} giờ ${m} phút`
  return m > 0 ? `${m} phút ${s} giây` : `${s} giây`
}
