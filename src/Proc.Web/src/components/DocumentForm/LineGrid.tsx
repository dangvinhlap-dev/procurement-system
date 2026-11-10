import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Button, Form, type FormListFieldData } from 'antd'
import { useEffect, useLayoutEffect, useRef, useState, type KeyboardEvent, type MouseEvent as ReactMouseEvent, type MutableRefObject, type ReactNode } from 'react'
import { layoutTokens } from '../../theme'
import { hasOpenPopup } from '../../utils/popup'

/** Một cột của lưới dòng chứng từ. */
export interface LineColumn {
  key: string
  title: string
  width: number
  align?: 'left' | 'right' | 'center'
  /** Ô bắt buộc nhập: tiêu đề cột có dấu * đỏ (ẩn khi lưới chỉ xem) – docs/ui-style.md mục 3. */
  required?: boolean
  /** Ô của một dòng: thường là Form.Item name={[field.name, '…']} noStyle bọc ô nhập; ô tính toán thì là chữ chỉ đọc. */
  render: (field: FormListFieldData, index: number) => ReactNode
  /**
   * Trường của dòng mà ô này hiển thị khi ô không phải ô nhập (ví dụ cột "Tên hàng" chỉ đọc → itemId), để F1 xem được lịch sử
   * (design.md mục 9.3a). Ô nhập thì tự lấy tên từ Form.Item, không cần khai báo.
   */
  auditField?: string
}

/** Thuộc tính của LineGrid. */
export interface LineGridProps {
  /** Tên mảng dòng trong form, ví dụ "lines". */
  name: string
  columns: LineColumn[]
  /** Giá trị dòng mới khi bấm "Thêm dòng" hoặc F2. */
  newLine: () => object
  readOnly?: boolean
  /** DocumentForm gọi hàm này khi bấm F2. */
  addRef?: MutableRefObject<(() => void) | null>
  /** Sửa được ô nhưng không thêm/xóa dòng (phiếu sinh từ phiếu khác, ví dụ phiếu nhập mua nội bộ). */
  fixedRows?: boolean
  /** Mã loại đối tượng của dòng trong nhật ký (ví dụ PurchaseOrderLine): bấm F1 ở ô của dòng đã lưu xem lịch sử ô đó. */
  auditEntity?: string
  /**
   * Giữ đúng độ rộng cột và cuộn ngang khi tổng độ rộng vượt khung (ví dụ lưới 12 tháng của kế hoạch nhu cầu). Mặc định tắt: lưới co cột
   * cho vừa khung như các phiếu hiện có.
   */
  scrollX?: boolean
}

/**
 * Lưới dòng chứng từ nhập như Excel (design.md mục 10.1): mỗi ô là một ô nhập không viền, Enter sang ô tiếp theo
 * (hết dòng thì sang dòng dưới), F2 thêm dòng và đặt con trỏ vào ô đầu của dòng mới. Cột STT và nút xóa dòng có sẵn.
 * Kéo mép phải tiêu đề cột để đổi độ rộng, bấm đúp mép để trả về độ rộng gốc (docs/ui-style.md mục 5).
 */
export function LineGrid({ name, columns, newLine, readOnly = false, addRef, fixedRows = false, auditEntity, scrollX = false }: LineGridProps) {
  return (
    <Form.List name={name}>
      {(fields, { add, remove }) => (
        <LineGridBody
          fields={fields}
          columns={columns}
          readOnly={readOnly || fixedRows}
          onAdd={() => add(newLine())}
          onRemove={remove}
          addRef={fixedRows ? undefined : addRef}
          listName={name}
          auditEntity={auditEntity}
          scrollX={scrollX}
        />
      )}
    </Form.List>
  )
}

/** Thân lưới dòng: vẽ tiêu đề cột co giãn được, các dòng nhập và dòng tổng. */
function LineGridBody({
  fields,
  columns,
  readOnly,
  onAdd,
  onRemove,
  addRef,
  listName,
  auditEntity,
  scrollX,
}: {
  listName: string
  auditEntity?: string
  scrollX: boolean
  fields: FormListFieldData[]
  columns: LineColumn[]
  readOnly: boolean
  onAdd: () => void
  onRemove: (index: number) => void
  addRef?: MutableRefObject<(() => void) | null>
}) {
  const form = Form.useFormInstance()
  // Độ rộng cột người dùng đã kéo (theo key); cột chưa kéo dùng độ rộng khai báo.
  const [widths, setWidths] = useState<Record<string, number>>({})
  const widthOf = (c: LineColumn) => widths[c.key] ?? Math.round(c.width * layoutTokens.columnWidthScale)

  /** Kéo mép phải tiêu đề cột; tối thiểu 32px để còn đọc được tiêu đề. */
  const startResize = (e: ReactMouseEvent, c: LineColumn) => {
    e.preventDefault()
    const startX = e.clientX
    const startWidth = widthOf(c)
    const onMove = (ev: MouseEvent) => setWidths((w) => ({ ...w, [c.key]: Math.max(32, startWidth + ev.clientX - startX) }))
    const onUp = () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('mouseup', onUp)
      document.body.classList.remove('erp-col-resizing')
    }
    document.body.classList.add('erp-col-resizing')
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup', onUp)
  }
  const resetWidth = (c: LineColumn) =>
    setWidths((w) => {
      const next = { ...w }
      delete next[c.key]
      return next
    })

  const rootRef = useRef<HTMLDivElement>(null)
  // Cờ "vừa thêm dòng, cần đặt con trỏ vào dòng mới" – xử lý ở useLayoutEffect bên dưới.
  const focusNewRow = useRef(false)

  // Thêm dòng rồi đặt con trỏ vào ô nhập đầu tiên của dòng mới.
  useEffect(() => {
    if (!addRef) return
    addRef.current = () => {
      focusNewRow.current = true
      onAdd()
    }
    return () => {
      addRef.current = null
    }
  }, [addRef, onAdd])

  // Đặt con trỏ ngay khi React vẽ xong dòng mới (trước khi trình duyệt nhận phím tiếp theo). Trước đây chờ setTimeout 50 ms: máy chậm
  // thì con trỏ nhảy về dòng mới giữa lúc người dùng đã gõ sang ô khác – chữ số rơi nhầm ô (lỗi e2e volume-rebate 09/11/2026).
  // Tìm dòng trong chính lưới này, không tìm toàn trang: form có hai lưới (dòng hàng + bậc chiết khấu) thì không đặt nhầm lưới.
  useLayoutEffect(() => {
    if (!focusNewRow.current) return
    focusNewRow.current = false
    const rows = rootRef.current?.querySelectorAll<HTMLTableRowElement>('tbody tr.line-grid-row')
    rows?.[rows.length - 1]?.querySelector<HTMLElement>('input:not([disabled])')?.focus()
  }, [fields.length])

  // Enter sang ô nhập kế tiếp, như Excel. Ô đang mở danh sách thả xuống thì để Enter chọn mục.
  const onKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key !== 'Enter' || e.shiftKey || e.ctrlKey || hasOpenPopup()) return
    const target = e.target as HTMLElement
    if (target.tagName !== 'INPUT') return
    const inputs = [...e.currentTarget.querySelectorAll<HTMLInputElement>('tbody input:not([disabled]):not([type=hidden])')]
    const next = inputs[inputs.indexOf(target as HTMLInputElement) + 1]
    if (next) {
      e.preventDefault()
      next.focus()
      next.select?.()
    }
  }

  return (
    <div className="line-grid" ref={rootRef} onKeyDown={onKeyDown}>
      {/* scrollX: bảng rộng bằng tổng độ rộng cột (tối thiểu 100% – CSS), nhiều cột thì cuộn ngang thay vì ép cột hẹp (docs/ui-style.md mục 5). */}
      <table style={scrollX ? { width: 44 + columns.reduce((sum, c) => sum + widthOf(c), 0) + (readOnly ? 0 : 32) } : undefined}>
        <thead>
          <tr>
            <th style={{ width: 44 }}>STT</th>
            {columns.map((c) => (
              <th key={c.key} style={{ width: widthOf(c), textAlign: c.align ?? 'left' }}>
                {c.required && !readOnly ? <span className="required-mark">{c.title}</span> : c.title}
                <span
                  className="line-grid-resize-handle"
                  role="separator"
                  aria-orientation="vertical"
                  aria-label={`Đổi độ rộng cột ${c.title}`}
                  title="Kéo để đổi độ rộng; bấm đúp để trả về mặc định"
                  onMouseDown={(e) => startResize(e, c)}
                  onDoubleClick={() => resetWidth(c)}
                />
              </th>
            ))}
            {!readOnly && <th style={{ width: 32 }} />}
          </tr>
        </thead>
        <tbody>
          {fields.map((field, index) => (
            <tr
              key={field.key}
              className="line-grid-row"
              // Mỗi dòng là một bản ghi trong nhật ký: khóa dòng lấy từ ô ẩn "id" (dòng mới chưa lưu thì rỗng).
              {...(auditEntity ? { 'data-audit-entity': auditEntity, 'data-audit-id': form.getFieldValue([listName, field.name, 'id']) ?? '' } : {})}
            >
              <td className="line-grid-stt">{index + 1}</td>
              {columns.map((c) => (
                <td key={c.key} style={{ textAlign: c.align ?? 'left' }} data-audit-field={c.auditField}>
                  {c.render(field, index)}
                </td>
              ))}
              {!readOnly && (
                <td>
                  <Button type="text" size="small" icon={<DeleteOutlined />} aria-label={`Xóa dòng ${index + 1}`} onClick={() => onRemove(field.name)} />
                </td>
              )}
            </tr>
          ))}
          {fields.length === 0 && (
            <tr>
              <td colSpan={columns.length + 2} className="line-grid-empty">
                Chưa có dòng nào. Bấm "Thêm dòng" hoặc F2.
              </td>
            </tr>
          )}
        </tbody>
      </table>
      {!readOnly && (
        <Button type="text" icon={<PlusOutlined />} onClick={() => (addRef?.current ? addRef.current() : onAdd())} className="line-grid-add">
          Thêm dòng (F2)
        </Button>
      )}
    </div>
  )
}
