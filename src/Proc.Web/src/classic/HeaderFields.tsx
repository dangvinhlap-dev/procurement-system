import { useId, useState, type ReactNode } from 'react'

/**
 * Khối có nút thu gọn nhỏ ở mép trái (như vùng header, tab, lưới dòng hàng trong ảnh mẫu – docs/ui-style.md mục 1).
 * @param title Tên khối, hiện khi đã thu gọn và dùng làm nhãn cho nút.
 */
export function Section({ title, children }: { title: string; children: ReactNode }) {
  const [open, setOpen] = useState(true)
  return (
    <section className="erp-section" aria-label={title}>
      <button
        type="button"
        className="erp-section__toggle"
        aria-expanded={open}
        title={open ? `Thu gọn: ${title}` : `Mở: ${title}`}
        onClick={() => setOpen(!open)}
      >
        {open ? '▾' : '▸'}
      </button>
      <div className="erp-section__content">
        {open ? children : <div className="erp-section__collapsed">{title}</div>}
      </div>
    </section>
  )
}

/** Hàng các ô của phần header chứng từ, trên một dòng (tự xuống dòng khi màn hình hẹp). */
export function HeaderFields({ children }: { children: ReactNode }) {
  return <div className="erp-header-fields">{children}</div>
}

/**
 * Một ô có nhãn bên trái. `children` là hàm nhận id để gắn vào ô nhập (nhãn bấm được, trình đọc màn hình đọc đúng tên).
 * @param label Nhãn ô.
 * @param required Ô bắt buộc: vẽ dấu * màu lỗi sau nhãn (giữ quy ước ui-guide.md mục 6).
 */
export function Field({
  label,
  required,
  children,
}: {
  label: string
  required?: boolean
  children: (id: string) => ReactNode
}) {
  const id = useId()
  return (
    <div className="erp-field">
      <label htmlFor={id} className={required ? 'erp-field__label--required' : undefined}>
        {label}
      </label>
      {children(id)}
    </div>
  )
}
