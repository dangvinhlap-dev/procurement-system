import { useRef, type DragEvent, type MouseEvent as ReactMouseEvent, type ThHTMLAttributes } from 'react'

/** Kiểu dữ liệu kéo thả riêng của lưới, để không nhận nhầm khi người dùng kéo chữ hoặc file vào. */
export const COLUMN_DRAG_TYPE = 'application/x-data-grid-column'

/** Độ rộng tối thiểu của cột khi kéo thu nhỏ. */
const MIN_WIDTH = 40

/** Thuộc tính thêm mà DataGrid truyền vào ô tiêu đề qua onHeaderCell. */
export interface GridHeaderCellProps extends ThHTMLAttributes<HTMLTableCellElement> {
  columnKey?: string
  columnWidth?: number
  onResizeWidth?: (key: string, width: number) => void
  onReorder?: (fromKey: string, toKey: string) => void
}

/**
 * Ô tiêu đề cột của DataGrid: kéo mép phải để đổi độ rộng, kéo cả ô để đổi thứ tự
 * hoặc thả vào vùng "Kéo tiêu đề cột vào đây để nhóm" (ui-guide.md mục Lưới dữ liệu).
 * Ô không có columnKey (cột đánh dấu, ô đệm thanh cuộn của antd) hiển thị như th bình thường.
 */
export function GridHeaderCell({ columnKey, columnWidth, onResizeWidth, onReorder, children, ...rest }: GridHeaderCellProps) {
  // Sau khi kéo đổi độ rộng, trình duyệt vẫn phát sự kiện click lên th → antd sẽ đổi chiều sắp xếp.
  // Cờ này chặn đúng một lần click đó.
  const justResized = useRef(false)

  if (!columnKey) return <th {...rest}>{children}</th>

  const startResize = (e: ReactMouseEvent) => {
    // preventDefault để không khởi động kéo thả HTML5 của cả ô tiêu đề.
    e.preventDefault()
    e.stopPropagation()
    const startX = e.clientX
    const startWidth = columnWidth ?? MIN_WIDTH
    const onMove = (ev: MouseEvent) => {
      justResized.current = true
      onResizeWidth?.(columnKey, Math.max(MIN_WIDTH, startWidth + ev.clientX - startX))
    }
    const onUp = () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('mouseup', onUp)
      setTimeout(() => (justResized.current = false), 0)
    }
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup', onUp)
  }

  const acceptsDrag = (e: DragEvent) => e.dataTransfer.types.includes(COLUMN_DRAG_TYPE)

  return (
    <th
      {...rest}
      draggable
      onDragStart={(e) => {
        e.dataTransfer.setData(COLUMN_DRAG_TYPE, columnKey)
        e.dataTransfer.effectAllowed = 'move'
      }}
      onDragOver={(e) => {
        if (acceptsDrag(e)) e.preventDefault()
      }}
      onDrop={(e) => {
        const from = e.dataTransfer.getData(COLUMN_DRAG_TYPE)
        if (from && from !== columnKey) onReorder?.(from, columnKey)
      }}
      onClickCapture={(e) => {
        if (justResized.current) {
          e.stopPropagation()
          justResized.current = false
        }
      }}
    >
      {children}
      <span
        className="data-grid-resize-handle"
        aria-hidden
        onMouseDown={startResize}
        onClick={(e) => e.stopPropagation()}
      />
    </th>
  )
}
