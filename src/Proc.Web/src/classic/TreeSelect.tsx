import { TreeSelect as AntTreeSelect, type TreeSelectProps } from 'antd'
import { useRef, useState, type KeyboardEvent, type ReactNode } from 'react'
import { hasWildcard, matchesKeyword } from '../utils/text'
import { LevelBadge } from './LevelBadge'

/** Chữ thuần của tiêu đề nút cây (chuỗi hoặc phần tử React có chữ). */
function titleText(node: ReactNode): string {
  if (node === null || node === undefined || typeof node === 'boolean') return ''
  if (typeof node === 'string' || typeof node === 'number') return String(node)
  if (Array.isArray(node)) return node.map(titleText).join(' ')
  if (typeof node === 'object' && 'props' in node) return titleText((node.props as { children?: ReactNode }).children)
  return ''
}

/**
 * Tiêu đề nút trong danh sách thả xuống: nút có `level` (nhóm hàng) thì thêm ô số cấp màu trước tên, `hint` (lý do khóa, ví dụ "cấp cuối")
 * mờ sau tên (design.md mục 5.7, N4-9; ui-style.md mục 5h). Tiêu đề gốc vẫn là chuỗi nên ô đã chọn, thuộc tính title và tìm kiếm không đổi.
 */
type TreeNodeData = Parameters<NonNullable<TreeSelectProps['treeTitleRender']>>[0]
function nodeTitle(node: TreeNodeData): ReactNode {
  // Phần mềm không dùng tiêu đề nút dạng hàm (chỉ chuỗi hoặc phần tử React); gặp thì không vẽ chữ thay vì gọi sai kiểu.
  const title = typeof node.title === 'function' ? null : node.title
  const { level, hint } = node as TreeNodeData & { level?: unknown; hint?: unknown }
  if (typeof level !== 'number') return title
  return (
    <>
      <LevelBadge level={level} />
      {title}
      {typeof hint === 'string' && <span className="erp-tree-node-hint">· {hint}</span>}
    </>
  )
}

/**
 * Ô chọn dạng cây dùng chung (nhóm hàng…) – thay `TreeSelect` của antd, cùng thuộc tính; cách tìm giống `Select` cổ điển
 * (docs/ui-style.md mục 5b, yêu cầu người dùng 07/11/2026 "mọi ô tìm kiếm gõ * phải lấy toàn bộ"):
 * - Mặc định bật tìm; gõ `*` liệt kê toàn bộ cây, `*` giữa từ khóa là ký tự đại diện (`vat*tu` khớp "Vật tư").
 * - Tìm không phân biệt có dấu/không dấu, nhiều từ không cần đúng thứ tự (CLAUDE.md mục 5).
 * - Từ khóa có `*` thì Enter chỉ liệt kê, không tự chọn nút đầu; mũi tên rồi Enter mới chọn.
 * @param props Như `TreeSelectProps` của antd; `showSearch={false}` để tắt tìm.
 */
export function TreeSelect<V = unknown>(props: TreeSelectProps<V>) {
  const { showSearch, onSearch, onKeyDown, ...rest } = props
  const [term, setTerm] = useState('')
  // Người dùng đã dùng mũi tên chọn nút sau lần gõ cuối – khi đó Enter là chọn nút đó, không chặn.
  const navigated = useRef(false)

  const treeTitleRender = props.treeTitleRender ?? nodeTitle
  if (showSearch === false) return <AntTreeSelect<V> {...props} treeTitleRender={treeTitleRender} />

  const onKeyDownCapture = (e: KeyboardEvent<HTMLElement>) => {
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') navigated.current = true
    if (e.key === 'Enter' && hasWildcard(term) && !navigated.current) {
      e.preventDefault()
      e.stopPropagation()
    }
  }

  return (
    <span style={{ display: 'contents' }} onKeyDownCapture={onKeyDownCapture}>
      <AntTreeSelect<V>
        {...rest}
        treeTitleRender={treeTitleRender}
        showSearch
        onKeyDown={onKeyDown}
        onSearch={(value: string) => {
          setTerm(value)
          navigated.current = false
          onSearch?.(value)
        }}
        filterTreeNode={(input, node) => matchesKeyword(titleText(node.title as ReactNode), input)}
        onOpenChange={(open) => {
          if (!open) setTerm('')
          rest.onOpenChange?.(open)
        }}
      />
    </span>
  )
}
