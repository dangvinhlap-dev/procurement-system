import { CloseOutlined, MinusSquareOutlined, PlusSquareOutlined, PushpinFilled } from '@ant-design/icons'
import { Button, Table, Tag } from 'antd'
import type { TableColumnsType, TableProps, TableRef } from 'antd'
import type { SortOrder } from 'antd/es/table/interface'
import { useEffect, useLayoutEffect, useMemo, useRef, useState, type HTMLAttributes, type KeyboardEvent, type RefObject } from 'react'
import { COLUMN_DRAG_TYPE, GridHeaderCell, type GridHeaderCellProps } from './GridHeaderCell'
import { cellText, compareCellValues, type GridColumn } from './types'
import './DataGrid.css'

/** Trên ngưỡng này lưới bật cuộn ảo để không vẽ hết mọi dòng (ui-guide.md: "cuộn ảo khi trên 500 dòng"). */
const VIRTUAL_THRESHOLD = 500
/** Độ rộng cột đánh dấu/ghim đầu lưới. */
const MARK_COLUMN_WIDTH = 28
/** Chiều cao dòng tiêu đề cột, trừ ra khi tính chiều cao vùng cuộn. */
const HEADER_HEIGHT = 26

/** Thuộc tính của DataGrid. */
export interface DataGridProps<T> {
  /** Các dòng đã lọc (tìm kiếm, bộ lọc nghiệp vụ do ListPage/trang xử lý). */
  rows: T[]
  /** Các cột đang hiển thị, theo thứ tự khai báo. */
  columns: GridColumn<T>[]
  rowKey: (row: T) => string
  /** Khóa cột đang nhóm, null nếu không nhóm. */
  groupBy: string | null
  onGroupByChange: (key: string | null) => void
  /** Mở chi tiết dòng (bấm đúp hoặc Enter). */
  onOpenRow?: (row: T) => void
  /** Dòng được ghim/đánh dấu sẵn khi mở lưới. */
  isMarked?: (row: T) => boolean
  /** Tên cột đầy đủ (kể cả cột đang ẩn) để hiện nhãn "Nhóm theo". */
  allColumns?: GridColumn<T>[]
}

/**
 * Bản ghi đưa vào antd Table: dòng dữ liệu hoặc dòng tiêu đề nhóm.
 * Nhóm được trải phẳng (tiêu đề nhóm rồi tới các dòng con) thay vì dùng dữ liệu dạng cây của antd,
 * vì dạng cây không hiển thị được khi bật cuộn ảo.
 */
type GridRecord<T> =
  | { key: string; row: T; group?: undefined }
  | { key: string; row?: undefined; group: { label: string; count: number } }

type SortState = { key: string; order: 'ascend' | 'descend' } | null

/**
 * Lưới dữ liệu dùng chung cho mọi danh sách (CLAUDE.md mục 9, ui-guide.md mục Lưới dữ liệu):
 * dòng 24px, kẻ ô mảnh, dòng xen kẽ; bấm tiêu đề để sắp xếp, kéo mép để đổi độ rộng, kéo ô để đổi thứ tự;
 * kéo tiêu đề vào vùng nhóm để nhóm; cột trạng thái chấm màu + chữ; mũi tên lên/xuống chọn dòng,
 * Enter hoặc bấm đúp để mở; tự bật cuộn ảo khi trên 500 dòng.
 * Viết trên antd Table (miễn phí), không dùng AG Grid Enterprise.
 */
export function DataGrid<T>({ rows, columns, rowKey, groupBy, onGroupByChange, onOpenRow, isMarked, allColumns }: DataGridProps<T>) {
  const [order, setOrder] = useState<string[]>(() => columns.map((c) => c.key))
  const [widths, setWidths] = useState<Record<string, number>>({})
  const [sort, setSort] = useState<SortState>(null)
  const [selectedKey, setSelectedKey] = useState<string | null>(null)
  const [marked, setMarked] = useState<Set<string>>(
    () => new Set(isMarked ? rows.filter(isMarked).map(rowKey) : []),
  )
  const [collapsedGroups, setCollapsedGroups] = useState<Set<string>>(new Set())
  const [dragOverZone, setDragOverZone] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const bodyHeight = useBodyHeight(containerRef)

  // Cột mới xuất hiện (ví dụ vừa bỏ ẩn) mà chưa có trong thứ tự thì thêm vào cuối.
  const orderedColumns = useMemo(() => {
    const byKey = new Map(columns.map((c) => [c.key, c]))
    const known = order.filter((k) => byKey.has(k)).map((k) => byKey.get(k)!)
    const extra = columns.filter((c) => !order.includes(c.key))
    return [...known, ...extra]
  }, [columns, order])

  const sortedRows = useMemo(() => {
    const column = sort && columns.find((c) => c.key === sort.key)
    if (!column || !sort) return rows
    const factor = sort.order === 'ascend' ? 1 : -1
    // Array.prototype.sort ổn định: dòng bằng nhau giữ thứ tự gốc.
    return [...rows].sort((a, b) => factor * compareCellValues(column.value(a), column.value(b)))
  }, [rows, columns, sort])

  const groupColumn = groupBy ? (allColumns ?? columns).find((c) => c.key === groupBy) : undefined

  const records = useMemo<GridRecord<T>[]>(() => {
    if (!groupColumn) return sortedRows.map((row) => ({ key: rowKey(row), row }))
    const groups = new Map<string, T[]>()
    for (const row of sortedRows) {
      const label = cellText(groupColumn, row) || '(Trống)'
      const items = groups.get(label)
      if (items) items.push(row)
      else groups.set(label, [row])
    }
    return [...groups.entries()]
      .sort(([a], [b]) => a.localeCompare(b, 'vi'))
      .flatMap(([label, items]) => {
        const key = `__group__${label}`
        const header: GridRecord<T> = { key, group: { label, count: items.length } }
        return collapsedGroups.has(key) ? [header] : [header, ...items.map((row) => ({ key: rowKey(row), row }))]
      })
  }, [sortedRows, groupColumn, rowKey, collapsedGroups])

  // Thứ tự dòng dữ liệu đang nhìn thấy (bỏ tiêu đề nhóm) – dùng cho phím mũi tên và tô dòng xen kẽ.
  const visibleKeys = useMemo(() => records.filter((r) => !r.group).map((r) => r.key), [records])
  const visibleIndex = useMemo(() => new Map(visibleKeys.map((k, i) => [k, i])), [visibleKeys])
  const rowByKey = useMemo(() => new Map(rows.map((r) => [rowKey(r), r])), [rows, rowKey])

  const virtual = rows.length > VIRTUAL_THRESHOLD
  const tableRef = useRef<TableRef>(null)

  const totalColumns = orderedColumns.length + 1
  const widthOf = (c: GridColumn<T>) => widths[c.key] ?? c.width

  const antColumns: TableColumnsType<GridRecord<T>> = [
    {
      key: '__mark',
      width: MARK_COLUMN_WIDTH,
      className: 'data-grid-mark',
      onCell: (rec) => (rec.group ? { colSpan: totalColumns } : {}),
      render: (_, rec) => {
        if (rec.group) {
          const collapsed = collapsedGroups.has(rec.key)
          return (
            <span className="data-grid-group-label">
              <button
                type="button"
                className="data-grid-group-toggle"
                aria-label={collapsed ? 'Mở nhóm' : 'Thu nhóm'}
                onClick={() =>
                  setCollapsedGroups((prev) => {
                    const next = new Set(prev)
                    if (collapsed) next.delete(rec.key)
                    else next.add(rec.key)
                    return next
                  })
                }
              >
                {collapsed ? <PlusSquareOutlined /> : <MinusSquareOutlined />}
              </button>
              {groupColumn?.title}: <b>{rec.group.label}</b> ({rec.group.count})
            </span>
          )
        }
        const isOn = marked.has(rec.key)
        return (
          <button
            type="button"
            className={`data-grid-mark-toggle${isOn ? ' is-on' : ''}`}
            aria-label={isOn ? 'Bỏ ghim dòng' : 'Ghim dòng'}
            aria-pressed={isOn}
            onClick={(e) => {
              e.stopPropagation()
              setMarked((prev) => {
                const next = new Set(prev)
                if (isOn) next.delete(rec.key)
                else next.add(rec.key)
                return next
              })
            }}
          >
            {isOn ? <PushpinFilled /> : '—'}
          </button>
        )
      },
    },
    ...orderedColumns.map((c) => ({
      key: c.key,
      title: c.title,
      width: widthOf(c),
      align: c.type === 'money' || c.type === 'quantity' ? ('right' as const) : undefined,
      className: `data-grid-col-${c.type ?? 'text'}`,
      ellipsis: true,
      sorter: true,
      showSorterTooltip: false,
      sortOrder: (sort?.key === c.key ? sort.order : null) as SortOrder,
      onCell: (rec: GridRecord<T>) => (rec.group ? { colSpan: 0 } : {}),
      onHeaderCell: () =>
        ({
          columnKey: c.key,
          columnWidth: widthOf(c),
          onResizeWidth: (key: string, width: number) => setWidths((prev) => ({ ...prev, [key]: width })),
          onReorder: (from: string, to: string) =>
            setOrder(() => {
              const keys = orderedColumns.map((x) => x.key).filter((k) => k !== from)
              keys.splice(keys.indexOf(to), 0, from)
              return keys
            }),
        }) as GridHeaderCellProps as HTMLAttributes<HTMLElement>,
      render: (_: unknown, rec: GridRecord<T>) => (rec.row === undefined ? null : renderCell(c, rec.row)),
    })),
  ]

  const handleChange: TableProps<GridRecord<T>>['onChange'] = (_p, _f, sorter) => {
    const s = Array.isArray(sorter) ? sorter[0] : sorter
    setSort(s?.order && s.columnKey ? { key: String(s.columnKey), order: s.order } : null)
  }

  const selectKey = (key: string) => {
    setSelectedKey(key)
    if (virtual) tableRef.current?.scrollTo({ key })
    else containerRef.current?.querySelector(`tr[data-row-key="${CSS.escape(key)}"]`)?.scrollIntoView({ block: 'nearest' })
  }

  const handleKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (visibleKeys.length === 0) return
    const current = selectedKey ? (visibleIndex.get(selectedKey) ?? -1) : -1
    const pageSize = Math.max(1, Math.floor(bodyHeight / 24) - 1)
    const moves: Record<string, number> = {
      ArrowDown: current + 1,
      ArrowUp: current - 1,
      PageDown: current + pageSize,
      PageUp: current - pageSize,
      Home: 0,
      End: visibleKeys.length - 1,
    }
    if (e.key in moves) {
      e.preventDefault()
      const next = Math.min(visibleKeys.length - 1, Math.max(0, moves[e.key]))
      selectKey(visibleKeys[next])
    } else if (e.key === 'Enter' && selectedKey) {
      e.preventDefault()
      const row = rowByKey.get(selectedKey)
      if (row) onOpenRow?.(row)
    }
  }

  const scrollX = MARK_COLUMN_WIDTH + orderedColumns.reduce((sum, c) => sum + widthOf(c), 0)

  return (
    <div className="data-grid-wrap">
      <div
        data-testid="group-zone"
        className={`data-grid-group-zone${dragOverZone ? ' is-drag-over' : ''}`}
        onDragOver={(e) => {
          if (e.dataTransfer.types.includes(COLUMN_DRAG_TYPE)) {
            e.preventDefault()
            setDragOverZone(true)
          }
        }}
        onDragLeave={() => setDragOverZone(false)}
        onDrop={(e) => {
          setDragOverZone(false)
          const key = e.dataTransfer.getData(COLUMN_DRAG_TYPE)
          const column = columns.find((c) => c.key === key)
          if (column?.groupable) {
            setCollapsedGroups(new Set())
            onGroupByChange(key)
          }
        }}
      >
        {groupColumn ? (
          <>
            <span>Nhóm theo:</span>
            <Tag>{groupColumn.title}</Tag>
            <Button size="small" type="text" icon={<CloseOutlined />} onClick={() => onGroupByChange(null)}>
              Bỏ nhóm
            </Button>
          </>
        ) : (
          'Kéo tiêu đề cột vào đây để nhóm'
        )}
      </div>
      <div ref={containerRef} className="data-grid" tabIndex={0} onKeyDown={handleKeyDown} aria-label="Lưới dữ liệu">
        <Table<GridRecord<T>>
          ref={tableRef}
          size="small"
          bordered
          virtual={virtual}
          pagination={false}
          tableLayout="fixed"
          columns={antColumns}
          dataSource={records}
          scroll={{ x: scrollX, y: Math.max(48, bodyHeight - HEADER_HEIGHT) }}
          components={{ header: { cell: GridHeaderCell } }}
          onChange={handleChange}
          rowClassName={(rec) => {
            if (rec.group) return 'data-grid-group-row'
            const index = visibleIndex.get(rec.key) ?? 0
            return [
              'data-grid-row',
              index % 2 === 1 ? 'is-alt' : '',
              rec.key === selectedKey ? 'is-selected' : '',
            ].join(' ')
          }}
          onRow={(rec) =>
            rec.group
              ? {}
              : {
                  onClick: () => setSelectedKey(rec.key),
                  onDoubleClick: () => onOpenRow?.(rec.row),
                }
          }
        />
      </div>
    </div>
  )
}

/**
 * Vẽ nội dung một ô theo kiểu cột. Cột trạng thái: chấm màu + chữ – màu không là thông tin duy nhất (ui-guide.md).
 */
function renderCell<T>(column: GridColumn<T>, row: T) {
  const text = cellText(column, row)
  if (column.type === 'status') {
    const tone = column.tone?.(row) ?? 'neutral'
    return (
      <span className={`data-grid-status tone-${tone}`} title={text}>
        <span className="data-grid-status-dot" aria-hidden />
        {text}
      </span>
    )
  }
  return text
}

/**
 * Theo dõi chiều cao vùng chứa lưới để đặt chiều cao vùng cuộn: tiêu đề cột đứng yên, chỉ thân lưới cuộn.
 * @param ref Phần tử bao lưới.
 * @returns Chiều cao (px) hiện tại.
 */
function useBodyHeight(ref: RefObject<HTMLDivElement | null>): number {
  const [height, setHeight] = useState(400)
  useLayoutEffect(() => {
    if (ref.current) setHeight(ref.current.clientHeight)
  }, [ref])
  useEffect(() => {
    const el = ref.current
    if (!el) return
    const observer = new ResizeObserver(([entry]) => setHeight(Math.floor(entry.contentRect.height)))
    observer.observe(el)
    return () => observer.disconnect()
  }, [ref])
  return height
}
