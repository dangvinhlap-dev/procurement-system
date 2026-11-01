import { useEffect, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'
import { formatQuantity } from '../utils/format'
import { CommandButton } from './ClassicScreen'
import { DateInput } from './DateInput'

/** Kiểu ô nhập trong lưới; không khai báo `kind` thì cột chỉ hiển thị (nền ô chỉ đọc). */
export type EntryCellKind = 'text' | 'number' | 'date' | 'select'

/** Khai báo một cột của lưới nhập. */
export interface EntryColumn<T> {
  key: string
  title: string
  /** Độ rộng cột (px) – lưới dày đặc nên khai báo cố định. */
  width: number
  kind?: EntryCellKind
  /** Cột số: canh phải, phông đơn cách (docs/ui-style.md mục 3). */
  numeric?: boolean
  /** Tiêu đề có dấu * màu lỗi. */
  required?: boolean
  /** Lựa chọn cho kind = 'select'. */
  options?: { value: string; label: string }[]
  /** Giá trị đang có của ô nhập. */
  value?: (row: T) => string | number | null
  /** Nội dung hiển thị của cột chỉ đọc. */
  display?: (row: T) => ReactNode
  /** Định dạng số khi không đang gõ; mặc định kiểu số lượng vi-VN (1.234,5). */
  format?: (value: number) => string
  /** Dòng nào được sửa ô này; mặc định mọi dòng. */
  editable?: (row: T) => boolean
}

/** Thuộc tính của EntryGrid. */
export interface EntryGridProps<T> {
  /** Tên lưới cho trình đọc màn hình. */
  label: string
  /** Dòng tiêu đề nhỏ phía trên lưới (ví dụ "Các yếu tố giá"). */
  caption?: string
  columns: EntryColumn<T>[]
  rows: T[]
  getRowKey: (row: T) => string
  onCellChange?: (rowIndex: number, key: string, value: string | number | null) => void
  selectedKeys?: ReadonlySet<string>
  onSelectionChange?: (keys: Set<string>) => void
  /** Đèn trạng thái / tam giác cảnh báo ở đầu dòng. */
  rowMarker?: (row: T) => ReactNode
  rowClassName?: (row: T) => string | undefined
  /** Thêm một dòng trống; gọi khi Enter ở dòng cuối. */
  onAppendRow?: () => void
  /** Các dòng tổng vẽ cuối lưới (dùng <SummaryRow>). */
  summary?: ReactNode
  /** Chiều cao tối đa vùng cuộn (px). */
  maxHeight?: number
}

/**
 * Lưới nhập dòng kiểu ERP desktop: ô nhập ngay trong lưới, đường kẻ 1px, dòng 20px, tiêu đề cột dính trên.
 * Enter ở một ô chuyển xuống ô cùng cột dòng dưới; ở dòng cuối thì thêm dòng mới (docs/ui-style.md mục 5–6).
 */
export function EntryGrid<T>({
  label,
  caption,
  columns,
  rows,
  getRowKey,
  onCellChange,
  selectedKeys,
  onSelectionChange,
  rowMarker,
  rowClassName,
  onAppendRow,
  summary,
  maxHeight,
}: EntryGridProps<T>) {
  const tableRef = useRef<HTMLTableElement>(null)
  // Ô cần đưa con trỏ tới sau khi thêm dòng (dòng mới chỉ có sau lần vẽ kế tiếp).
  const pendingFocus = useRef<{ row: number; col: number } | null>(null)

  useEffect(() => {
    const target = pendingFocus.current
    if (!target) return
    pendingFocus.current = null
    focusCell(tableRef.current, target.row, target.col)
  }, [rows.length])

  const onKeyDown = (e: KeyboardEvent<HTMLTableElement>) => {
    if (e.key !== 'Enter' || e.ctrlKey || e.altKey) return
    const cell = (e.target as HTMLElement).closest<HTMLElement>('[data-row]')
    if (!cell) return
    e.preventDefault()
    const row = Number(cell.dataset.row) + 1
    const col = Number(cell.dataset.col)
    if (row < rows.length) {
      focusCell(tableRef.current, row, col)
    } else if (onAppendRow) {
      pendingFocus.current = { row, col }
      onAppendRow()
    }
  }

  const toggleRow = (key: string) => {
    if (!onSelectionChange) return
    const next = new Set(selectedKeys)
    if (next.has(key)) next.delete(key)
    else next.add(key)
    onSelectionChange(next)
  }

  const totalWidth = 18 + (rowMarker ? 22 : 0) + columns.reduce((s, c) => s + c.width, 0)

  return (
    <div className="erp-grid" style={maxHeight ? { maxHeight } : undefined}>
      {caption && <div className="erp-grid__caption">{caption}</div>}
      <table ref={tableRef} aria-label={label} style={{ width: totalWidth }} onKeyDown={onKeyDown}>
        <colgroup>
          <col style={{ width: 18 }} />
          {rowMarker && <col style={{ width: 22 }} />}
          {columns.map((c) => (
            <col key={c.key} style={{ width: c.width }} />
          ))}
        </colgroup>
        <thead>
          <tr>
            <th className="erp-grid__selector" aria-label="Chọn dòng" />
            {rowMarker && <th className="erp-grid__marker" aria-label="Trạng thái" />}
            {columns.map((c) => (
              <th key={c.key} className={c.numeric ? 'erp-grid__num' : undefined}>
                <span className={c.required ? 'erp-field__label--required' : undefined}>{c.title}</span>
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, r) => {
            const key = getRowKey(row)
            const selected = selectedKeys?.has(key) ?? false
            return (
              <tr key={key} aria-selected={selected} className={rowClassName?.(row)}>
                <td
                  className="erp-grid__selector"
                  title={selected ? 'Bỏ chọn dòng' : 'Chọn dòng'}
                  onClick={() => toggleRow(key)}
                />
                {rowMarker && <td className="erp-grid__marker">{rowMarker(row)}</td>}
                {columns.map((c, ci) => (
                  <GridCell key={c.key} column={c} row={row} rowIndex={r} colIndex={ci} onChange={onCellChange} />
                ))}
              </tr>
            )
          })}
          {summary}
        </tbody>
      </table>
    </div>
  )
}

/** Đưa con trỏ tới ô nhập ở (dòng, cột); bỏ qua nếu ô đó chỉ đọc. */
function focusCell(table: HTMLTableElement | null, row: number, col: number) {
  const el = table?.querySelector<HTMLElement>(`[data-row="${row}"][data-col="${col}"]`)
  el?.focus()
  if (el instanceof HTMLInputElement) el.select()
}

/** Vẽ một ô: ô nhập theo `kind` hoặc ô chỉ đọc. */
function GridCell<T>({
  column: c,
  row,
  rowIndex,
  colIndex,
  onChange,
}: {
  column: EntryColumn<T>
  row: T
  rowIndex: number
  colIndex: number
  onChange?: (rowIndex: number, key: string, value: string | number | null) => void
}) {
  const numClass = c.numeric ? ' erp-grid__num' : ''
  const editable = c.kind && onChange && (c.editable?.(row) ?? true)
  if (!editable) {
    const content = c.display ? c.display(row) : formatPlain(c, c.value?.(row) ?? null)
    return <td className={`erp-grid__readonly${numClass}`}>{content}</td>
  }

  const value = c.value?.(row) ?? null
  const dataAttrs = { 'data-row': rowIndex, 'data-col': colIndex, 'aria-label': `${c.title} dòng ${rowIndex + 1}` }
  const set = (v: string | number | null) => onChange(rowIndex, c.key, v)

  return (
    <td className={`erp-grid__cell-input${numClass}`}>
      {c.kind === 'number' ? (
        <NumberInput value={typeof value === 'number' ? value : null} format={c.format} onCommit={set} {...dataAttrs} />
      ) : c.kind === 'select' ? (
        <select value={value == null ? '' : String(value)} onChange={(e) => set(e.target.value || null)} {...dataAttrs}>
          <option value="" />
          {c.options?.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      ) : c.kind === 'date' ? (
        <DateInput value={value == null ? null : String(value)} onChange={set} {...dataAttrs} />
      ) : (
        <input
          value={value == null ? '' : String(value)}
          onChange={(e) => set(e.target.value || null)}
          {...dataAttrs}
        />
      )}
    </td>
  )
}

/** Hiển thị giá trị của cột chỉ đọc không có `display`. */
function formatPlain<T>(c: EntryColumn<T>, value: string | number | null): string {
  if (value == null) return ''
  if (typeof value === 'number') return (c.format ?? formatQuantity)(value)
  return value
}

/**
 * Chuyển chữ gõ kiểu Việt Nam sang số: dấu chấm phân cách nghìn, dấu phẩy thập phân.
 * Ví dụ "1.912,5" → 1912.5; chuỗi rỗng hoặc không phải số → null.
 */
function parseVnNumber(text: string): number | null {
  const normalized = text.trim().replace(/\./g, '').replace(',', '.')
  if (!normalized) return null
  const n = Number(normalized)
  return Number.isFinite(n) ? n : null
}

/**
 * Ô số trong lưới: khi rời ô hiển thị định dạng vi-VN; khi đang gõ giữ nguyên chữ người dùng gõ.
 * Giá trị chỉ được ghi khi rời ô (blur) – Enter chuyển dòng cũng là rời ô.
 */
function NumberInput({
  value,
  format = formatQuantity,
  onCommit,
  ...rest
}: {
  value: number | null
  format?: (value: number) => string
  onCommit: (value: number | null) => void
  'data-row': number
  'data-col': number
  'aria-label': string
}) {
  const [text, setText] = useState<string | null>(null)
  return (
    <input
      inputMode="decimal"
      value={text ?? (value == null ? '' : format(value))}
      onFocus={() => setText(value == null ? '' : String(value).replace('.', ','))}
      onChange={(e) => setText(e.target.value)}
      onBlur={() => {
        if (text !== null) onCommit(parseVnNumber(text))
        setText(null)
      }}
      {...rest}
    />
  )
}

/**
 * Dòng tổng / tạm tính cuối lưới, chữ xanh dương đậm (docs/ui-style.md mục 2).
 * @param cells Nội dung theo đúng thứ tự cột của lưới (kể cả cột chọn và cột đèn nếu có).
 */
export function SummaryRow({ cells }: { cells: { content?: ReactNode; numeric?: boolean; colSpan?: number }[] }) {
  return (
    <tr className="erp-grid__subtotal">
      {cells.map((c, i) => (
        <td key={i} colSpan={c.colSpan} className={c.numeric ? 'erp-grid__num' : undefined}>
          {c.content}
        </td>
      ))}
    </tr>
  )
}

/**
 * Thanh nút nhỏ dưới lưới: thêm dòng, xóa dòng, chọn tất cả, bỏ chọn.
 * Nút không có hàm xử lý thì ẩn; xóa dòng khóa khi chưa chọn dòng nào.
 */
export function GridButtons({
  onAdd,
  onDelete,
  onSelectAll,
  onClearSelection,
  hasSelection,
  icons,
}: {
  onAdd?: () => void
  onDelete?: () => void
  onSelectAll?: () => void
  onClearSelection?: () => void
  hasSelection: boolean
  icons: { add: ReactNode; delete: ReactNode; selectAll: ReactNode; clear: ReactNode }
}) {
  return (
    <div className="erp-gridbuttons">
      {onAdd && <CommandButton icon={icons.add} title="Thêm dòng (F2)" onClick={onAdd} />}
      {onDelete && <CommandButton icon={icons.delete} title="Xóa dòng đã chọn" onClick={onDelete} disabled={!hasSelection} />}
      {onSelectAll && <CommandButton icon={icons.selectAll} title="Chọn tất cả" onClick={onSelectAll} />}
      {onClearSelection && (
        <CommandButton icon={icons.clear} title="Bỏ chọn" onClick={onClearSelection} disabled={!hasSelection} />
      )}
    </div>
  )
}
