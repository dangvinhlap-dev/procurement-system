import { useRef, type KeyboardEvent, type ReactNode } from 'react'

/** Một tab: khóa, nhãn và nội dung (chỉ vẽ nội dung tab đang chọn). */
export interface TabStripItem {
  key: string
  label: string
  content: ReactNode
}

/**
 * Tab ngang kiểu ERP desktop: tab thường nền xanh nhạt, tab đang chọn đậm hơn chữ trắng (docs/ui-style.md mục 2).
 * Theo mẫu WAI-ARIA tabs: mũi tên trái/phải chuyển tab, Tab đi thẳng vào nội dung.
 * @param items Danh sách tab.
 * @param activeKey Tab đang chọn.
 * @param onChange Gọi khi chọn tab khác.
 */
export function TabStrip({
  items,
  activeKey,
  onChange,
  label,
}: {
  items: TabStripItem[]
  activeKey: string
  onChange: (key: string) => void
  label: string
}) {
  const refs = useRef<(HTMLButtonElement | null)[]>([])
  const activeIndex = Math.max(0, items.findIndex((t) => t.key === activeKey))

  const onKeyDown = (e: KeyboardEvent) => {
    if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') return
    e.preventDefault()
    const next = (activeIndex + (e.key === 'ArrowRight' ? 1 : -1) + items.length) % items.length
    onChange(items[next].key)
    refs.current[next]?.focus()
  }

  return (
    <div className="erp-tabs">
      <div className="erp-tabs__list" role="tablist" aria-label={label} onKeyDown={onKeyDown}>
        {items.map((t, i) => (
          <button
            key={t.key}
            ref={(el) => {
              refs.current[i] = el
            }}
            type="button"
            role="tab"
            id={`tab-${t.key}`}
            aria-selected={i === activeIndex}
            aria-controls={`tabpanel-${t.key}`}
            tabIndex={i === activeIndex ? 0 : -1}
            className="erp-tabs__tab"
            onClick={() => onChange(t.key)}
          >
            {t.label}
          </button>
        ))}
      </div>
      <div className="erp-tabs__panel" role="tabpanel" id={`tabpanel-${items[activeIndex].key}`} aria-labelledby={`tab-${items[activeIndex].key}`}>
        {items[activeIndex].content}
      </div>
    </div>
  )
}
