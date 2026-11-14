import { CATEGORY_LEVEL_NAMES } from './categoryLevels'

/**
 * Ô số cấp màu của nhóm hàng (docs/ui-style.md mục 5h): ô vuông bo 2px, 4 sắc xanh đậm → nhạt theo cấp 1 → 4.
 * Luôn có chú thích tên cấp (title/aria-label) – màu không là thông tin duy nhất.
 * @param level Cấp 1–4.
 * @param text Chữ trong ô; mặc định là số cấp (ví dụ "Cấp 3/4" ở tiêu đề ngăn kéo).
 */
export function LevelBadge({ level, text }: { level: number; text?: string }) {
  const name = CATEGORY_LEVEL_NAMES[level] ?? ''
  const label = `Cấp ${level}${name ? ` – ${name}` : ''}`
  return (
    <span className={`erp-level-badge is-${level}${text ? ' is-wide' : ''}`} title={label} aria-label={label} role="img">
      {text ?? level}
    </span>
  )
}
