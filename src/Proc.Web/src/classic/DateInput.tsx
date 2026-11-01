import { useState, type InputHTMLAttributes } from 'react'
import dayjs from 'dayjs'

/**
 * Đổi chữ người dùng gõ thành ngày ISO "yyyy-MM-dd".
 * Nhận "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "ddMMyyyy" và "dd/MM" (lấy năm hiện tại) – gõ nhanh bằng bàn phím.
 * Ví dụ "15/11/2026" → "2026-11-15"; "1511" → "2026-11-15" nếu đang ở năm 2026; sai ngày → null.
 */
function parseVnDate(text: string): string | null {
  const t = text.trim()
  if (!t) return null
  const parts = /^\d{4}$|^\d{8}$/.test(t) ? [t.slice(0, 2), t.slice(2, 4), t.slice(4) || String(dayjs().year())] : t.split(/[/.-]/)
  if (parts.length < 2 || parts.length > 3) return null
  const [d, m, y = String(dayjs().year())] = parts.map(Number).map(String)
  const iso = `${y.padStart(4, '0')}-${m.padStart(2, '0')}-${d.padStart(2, '0')}`
  // Kiểm tra ngày có thật (31/02 không hợp lệ) bằng cách định dạng ngược lại.
  return dayjs(iso, 'YYYY-MM-DD').format('YYYY-MM-DD') === iso ? iso : null
}

/**
 * Ô ngày gõ tay theo dd/MM/yyyy (CLAUDE.md mục 5). Không dùng input type="date" vì trình duyệt hiển thị theo ngôn ngữ hệ điều hành
 * (có máy ra mm/dd/yyyy). Giá trị chỉ ghi khi rời ô; gõ sai thì giữ ngày cũ.
 * @param value Ngày ISO "yyyy-MM-dd" hoặc null.
 * @param onChange Nhận ngày ISO mới hoặc null khi xóa trắng.
 */
export function DateInput({
  value,
  onChange,
  ...rest
}: { value: string | null; onChange: (value: string | null) => void } & Omit<
  InputHTMLAttributes<HTMLInputElement>,
  'value' | 'onChange'
>) {
  const [text, setText] = useState<string | null>(null)
  const shown = value ? dayjs(value).format('DD/MM/YYYY') : ''
  return (
    <input
      {...rest}
      placeholder="dd/mm/yyyy"
      value={text ?? shown}
      onFocus={() => setText(shown)}
      onChange={(e) => setText(e.target.value)}
      onBlur={() => {
        if (text !== null && text !== shown) {
          const parsed = parseVnDate(text)
          if (parsed || !text.trim()) onChange(parsed)
        }
        setText(null)
      }}
    />
  )
}
