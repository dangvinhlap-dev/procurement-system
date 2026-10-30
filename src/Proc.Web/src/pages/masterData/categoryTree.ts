import type { ItemCategory } from '../../api/masterData'

/** Một nút của TreeSelect nhóm hàng. */
export interface CategoryNode {
  value: string
  title: string
  disabled?: boolean
  children: CategoryNode[]
}

/**
 * Dựng cây nhóm hàng cho TreeSelect từ danh sách phẳng. Nhóm có nhóm cha ngoài phạm vi người xem thì đứng ở gốc.
 * @param categories Danh sách nhóm (API đã lọc theo phạm vi).
 * @param options.activeOnly Chỉ nhóm đang dùng (chọn nhóm cho hàng mới, nhóm cha).
 * @param options.exclude Nhóm không được chọn cùng mọi nhóm con của nó (chọn nhóm cha: không chọn chính nó hay con cháu).
 * @returns Các nút gốc.
 */
export function buildCategoryTree(
  categories: ItemCategory[],
  options: { activeOnly?: boolean; exclude?: string } = {},
): CategoryNode[] {
  const visible = categories.filter((c) => !options.activeOnly || c.isActive)
  const ids = new Set(visible.map((c) => c.id))
  const nodes = new Map<string, CategoryNode>(
    visible.map((c) => [c.id, { value: c.id, title: `${c.code} – ${c.name}`, children: [] }]),
  )
  const roots: CategoryNode[] = []
  for (const c of visible) {
    const node = nodes.get(c.id)!
    if (c.parentId && ids.has(c.parentId)) nodes.get(c.parentId)!.children.push(node)
    else roots.push(node)
  }
  if (options.exclude) {
    const disable = (node: CategoryNode) => {
      node.disabled = true
      node.children.forEach(disable)
    }
    const excluded = nodes.get(options.exclude)
    if (excluded) disable(excluded)
  }
  return roots
}
