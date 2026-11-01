import { ConfigProvider } from 'antd'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { classicTheme } from './classicTheme'
import './classic.css'

/** Phím tắt chung của màn hình cổ điển (docs/ui-style.md mục 6). Hàm nào không truyền thì phím đó không làm gì. */
export interface ClassicShortcuts {
  /** Ctrl+S. */
  save?: () => void
  /** Ctrl+P. */
  print?: () => void
  /** F2. */
  addRow?: () => void
  /** F3. */
  back?: () => void
}

/**
 * Khung màn hình phong cách ERP desktop cổ điển (docs/ui-style.md mục 1).
 * Gắn lớp .erp-classic (biến CSS --erp-*), lồng theme antd riêng và bắt phím tắt toàn màn hình.
 * @param children Các vùng theo thứ tự: MenuBar, Toolbar, ScreenTitle, CommandBar, nội dung (ClassicBody), StatusBar.
 * @param shortcuts Hàm xử lý phím tắt.
 */
export function ClassicScreen({ children, shortcuts }: { children: ReactNode; shortcuts?: ClassicShortcuts }) {
  // Giữ bản mới nhất trong ref để chỉ gắn trình nghe phím một lần mà vẫn gọi đúng hàm theo state hiện tại.
  const shortcutsRef = useRef(shortcuts)
  useEffect(() => {
    shortcutsRef.current = shortcuts
  })

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      const s = shortcutsRef.current
      if (!s) return
      const key = e.key.toLowerCase()
      const handler =
        e.ctrlKey && key === 's' ? s.save
        : e.ctrlKey && key === 'p' ? s.print
        : e.key === 'F2' ? s.addRow
        : e.key === 'F3' ? s.back
        : undefined
      if (!handler) return
      // Chặn hành vi mặc định của trình duyệt (lưu trang, in trang, tìm kiếm).
      e.preventDefault()
      handler()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])

  return (
    <ConfigProvider theme={classicTheme}>
      <div className="erp-classic">{children}</div>
    </ConfigProvider>
  )
}

/** Vùng nội dung cuộn được giữa thanh lệnh phụ và thanh trạng thái. */
export function ClassicBody({ children }: { children: ReactNode }) {
  return <main className="erp-body">{children}</main>
}

/** Một mục của thanh menu chữ. */
export interface MenuBarItem {
  /** Nhãn; ký tự đứng sau dấu & là phím Alt và được gạch chân, ví dụ "&Phiếu". */
  label: string
  onClick?: () => void
}

/**
 * Thanh menu chữ trên cùng (Phiếu, Sửa, Đi tới, Hệ thống, Trợ giúp).
 * Alt + chữ gạch chân đưa con trỏ tới mục đó (giữ thói quen thao tác bàn phím – ui-guide.md mục 8).
 * @param items Các mục menu.
 */
export function MenuBar({ items }: { items: MenuBarItem[] }) {
  const refs = useRef<(HTMLButtonElement | null)[]>([])

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (!e.altKey || e.ctrlKey) return
      const index = items.findIndex((it) => accessKey(it.label) === e.key.toLowerCase())
      if (index < 0) return
      e.preventDefault()
      refs.current[index]?.focus()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [items])

  return (
    <nav className="erp-menubar" aria-label="Menu chính">
      {items.map((it, i) => (
        <button
          key={it.label}
          ref={(el) => {
            refs.current[i] = el
          }}
          type="button"
          className="erp-menubar__item"
          onClick={it.onClick}
        >
          {renderAccessLabel(it.label)}
        </button>
      ))}
    </nav>
  )
}

/** Lấy phím Alt (chữ sau dấu &) của nhãn, chữ thường; rỗng nếu không có. */
function accessKey(label: string): string {
  const i = label.indexOf('&')
  return i >= 0 ? label.charAt(i + 1).toLowerCase() : ''
}

/** Vẽ nhãn có chữ gạch chân ở vị trí sau dấu &. */
function renderAccessLabel(label: string): ReactNode {
  const i = label.indexOf('&')
  if (i < 0) return label
  return (
    <>
      {label.slice(0, i)}
      <u>{label.charAt(i + 1)}</u>
      {label.slice(i + 2)}
    </>
  )
}

/**
 * Màu icon theo mức quan trọng của thao tác (docs/ui-style.md mục 2):
 * - 'danger' (đỏ): không hoàn tác được hoặc làm mất dữ liệu đang nhập – Hủy, Thoát, Xóa dòng.
 * - 'success' (xanh lá): ghi hoặc tạo dữ liệu – Lưu, Thêm dòng.
 * - 'warning' (vàng): thao tác thường, không đổi dữ liệu – Quay lại, In, Tìm, Xem trước in…
 */
export type IconTone = 'danger' | 'success' | 'warning'

/** Bọc icon trong lớp màu theo mức quan trọng. */
function toned(icon: ReactNode, tone: IconTone | undefined): ReactNode {
  return tone ? <span className={`erp-tone erp-tone--${tone}`}>{icon}</span> : icon
}

/** Một nút icon trên thanh công cụ; `separator` để chèn vạch dọc chia nhóm. */
export type ToolbarItem =
  | { key: string; icon: ReactNode; title: string; tone?: IconTone; onClick?: () => void; disabled?: boolean }
  | { key: string; separator: true }

/**
 * Thanh công cụ icon nhỏ: ô lệnh (gõ mã màn hình rồi Enter) + các nút Lưu, Quay lại, Thoát, Hủy, In, Tìm.
 * @param items Các nút theo thứ tự hiển thị.
 * @param onCommand Gọi khi Enter ở ô lệnh, nhận mã đã gõ (đã cắt khoảng trắng, viết hoa).
 */
export function Toolbar({ items, onCommand }: { items: ToolbarItem[]; onCommand?: (code: string) => void }) {
  const [command, setCommand] = useState('')
  return (
    <div className="erp-toolbar" role="toolbar" aria-label="Thanh công cụ">
      <input
        className="erp-input erp-toolbar__command"
        aria-label="Ô lệnh"
        placeholder="Mã màn hình…"
        value={command}
        onChange={(e) => setCommand(e.target.value)}
        onKeyDown={(e) => {
          if (e.key !== 'Enter' || !command.trim()) return
          onCommand?.(command.trim().toUpperCase())
          setCommand('')
        }}
      />
      {items.map((it) =>
        'separator' in it ? (
          <span key={it.key} className="erp-toolbar__sep" aria-hidden />
        ) : (
          <button
            key={it.key}
            type="button"
            className="erp-iconbtn"
            title={it.title}
            aria-label={it.title}
            disabled={it.disabled}
            onClick={it.onClick}
          >
            {toned(it.icon, it.tone)}
          </button>
        ),
      )}
    </div>
  )
}

/** Tiêu đề màn hình in đậm, xanh đậm, cỡ lớn (ví dụ "Tạo đơn mua hàng"). */
export function ScreenTitle({ children }: { children: ReactNode }) {
  return <h1 className="erp-title">{children}</h1>
}

/** Thanh nút lệnh phụ dưới tiêu đề (Tổng quan chứng từ, Giữ, Xem trước in, Thông báo…). */
export function CommandBar({ children }: { children: ReactNode }) {
  return (
    <div className="erp-commandbar" role="toolbar" aria-label="Lệnh phụ">
      {children}
    </div>
  )
}

/**
 * Nút lệnh nền be nhạt, viền nâu xám (docs/ui-style.md mục 2).
 * @param icon Biểu tượng tùy chọn; không có `children` thì nút vuông chỉ icon và bắt buộc có `title`.
 * @param title Chú thích khi rê chuột, đồng thời là tên đọc màn hình của nút chỉ icon.
 * @param tone Màu icon theo mức quan trọng.
 */
export function CommandButton({
  icon,
  tone,
  children,
  title,
  onClick,
  disabled,
}: {
  icon?: ReactNode
  tone?: IconTone
  children?: ReactNode
  title?: string
  onClick?: () => void
  disabled?: boolean
}) {
  return (
    <button
      type="button"
      className={children ? 'erp-cmdbtn' : 'erp-cmdbtn erp-cmdbtn--icon'}
      title={title}
      aria-label={children ? undefined : title}
      onClick={onClick}
      disabled={disabled}
    >
      {toned(icon, tone)}
      {children}
    </button>
  )
}

/** Mức của thông báo trên thanh trạng thái, dùng màu đèn tương ứng. */
export type StatusLevel = 'success' | 'warning' | 'error'

/**
 * Đèn trạng thái ô vuông xanh lá / vàng / đỏ. Luôn có `label` để màu không là thông tin duy nhất
 * (hiện khi rê chuột và cho trình đọc màn hình).
 */
export function StatusLight({ level, label }: { level: StatusLevel; label: string }) {
  const color = level === 'success' ? 'green' : level === 'warning' ? 'yellow' : 'red'
  return <span className={`erp-light erp-light--${color}`} role="img" aria-label={label} title={label} />
}

/**
 * Thanh trạng thái dưới cùng: thông báo sau thao tác (đèn + chữ) và thông tin phụ bên phải.
 * @param message Thông báo hiện tại; null thì để trống.
 * @param right Nội dung bên phải (ví dụ người dùng, phiên bản).
 */
export function StatusBar({ message, right }: { message: { level: StatusLevel; text: string } | null; right?: ReactNode }) {
  return (
    <footer className="erp-statusbar" role="status" aria-live="polite">
      {message && (
        <>
          <StatusLight level={message.level} label={message.text} />
          <span>{message.text}</span>
        </>
      )}
      <span className="erp-statusbar__right">{right}</span>
    </footer>
  )
}
