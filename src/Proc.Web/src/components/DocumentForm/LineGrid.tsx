import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Button, Form, type FormListFieldData } from 'antd'
import { useEffect, type KeyboardEvent, type MutableRefObject, type ReactNode } from 'react'
import { hasOpenPopup } from '../../utils/popup'

/** Một cột của lưới dòng chứng từ. */
export interface LineColumn {
  key: string
  title: string
  width: number
  align?: 'left' | 'right' | 'center'
  /** Ô của một dòng: thường là Form.Item name={[field.name, '…']} noStyle bọc ô nhập; ô tính toán thì là chữ chỉ đọc. */
  render: (field: FormListFieldData, index: number) => ReactNode
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
}

/**
 * Lưới dòng chứng từ nhập như Excel (design.md mục 10.1): mỗi ô là một ô nhập không viền, Enter sang ô tiếp theo
 * (hết dòng thì sang dòng dưới), F2 thêm dòng và đặt con trỏ vào ô đầu của dòng mới. Cột STT và nút xóa dòng có sẵn.
 */
export function LineGrid({ name, columns, newLine, readOnly = false, addRef }: LineGridProps) {
  return (
    <Form.List name={name}>
      {(fields, { add, remove }) => (
        <LineGridBody
          fields={fields}
          columns={columns}
          readOnly={readOnly}
          onAdd={() => add(newLine())}
          onRemove={remove}
          addRef={addRef}
        />
      )}
    </Form.List>
  )
}

function LineGridBody({
  fields,
  columns,
  readOnly,
  onAdd,
  onRemove,
  addRef,
}: {
  fields: FormListFieldData[]
  columns: LineColumn[]
  readOnly: boolean
  onAdd: () => void
  onRemove: (index: number) => void
  addRef?: MutableRefObject<(() => void) | null>
}) {
  // Thêm dòng rồi đặt con trỏ vào ô nhập đầu tiên của dòng mới (chờ React vẽ xong dòng).
  useEffect(() => {
    if (!addRef) return
    addRef.current = () => {
      onAdd()
      setTimeout(() => {
        const rows = document.querySelectorAll<HTMLTableRowElement>('.line-grid tbody tr.line-grid-row')
        rows[rows.length - 1]?.querySelector<HTMLElement>('input:not([disabled])')?.focus()
      }, 50)
    }
    return () => {
      addRef.current = null
    }
  }, [addRef, onAdd])

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
    <div className="line-grid" onKeyDown={onKeyDown}>
      <table>
        <thead>
          <tr>
            <th style={{ width: 36 }}>STT</th>
            {columns.map((c) => (
              <th key={c.key} style={{ width: c.width, textAlign: c.align ?? 'left' }}>
                {c.title}
              </th>
            ))}
            {!readOnly && <th style={{ width: 32 }} />}
          </tr>
        </thead>
        <tbody>
          {fields.map((field, index) => (
            <tr key={field.key} className="line-grid-row">
              <td className="line-grid-stt">{index + 1}</td>
              {columns.map((c) => (
                <td key={c.key} style={{ textAlign: c.align ?? 'left' }}>
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
