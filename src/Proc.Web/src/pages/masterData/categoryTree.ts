import type { ItemCategory } from '../../api/masterData'
import { CATEGORY_MAX_LEVEL } from '../../classic/categoryLevels'
import { normalizeVi } from '../../utils/text'

/** Một nút của TreeSelect nhóm hàng. */
export interface CategoryNode {
  value: string
  title: string
  /** Cấp 1–4 – TreeSelect cổ điển vẽ ô số cấp trước tên (design.md mục 5.7, N4-9). */
  level: number
  /** Lý do nút bị khóa, hiện mờ sau tên (ví dụ "cấp cuối"). */
  hint?: string
  disabled?: boolean
  children: CategoryNode[]
}

/**
 * Số cấp của nhánh tính từ nhóm `id` (1 = nhóm không có nhóm con). Dùng khi chọn nhóm cha mới: cấp nhóm cha + số cấp nhánh ≤ 4
 * (design.md mục 5.7, N4-4). Ví dụ nhóm có con và cháu → 3: chỉ làm nhóm cấp 1 (gốc) hoặc vào nhóm cấp 1.
 * @param categories Cả danh mục (kể cả ngoài phạm vi, ngừng dùng – máy chủ cũng tính trên cả cây).
 * @param id Nhóm gốc của nhánh.
 */
export function branchDepth(categories: ItemCategory[], id: string): number {
  const children = new Map<string, string[]>()
  for (const c of categories) {
    if (!c.parentId) continue
    children.set(c.parentId, [...(children.get(c.parentId) ?? []), c.id])
  }
  const depth = (nodeId: string, guard: number): number =>
    guard > CATEGORY_MAX_LEVEL + 1 ? guard : 1 + Math.max(0, ...(children.get(nodeId) ?? []).map((k) => depth(k, guard + 1)))
  return depth(id, 1)
}

/**
 * Dựng cây nhóm hàng cho TreeSelect từ danh sách phẳng. Nhóm có nhóm cha ngoài phạm vi người xem thì đứng ở gốc.
 * @param categories Danh sách nhóm (API đã lọc theo phạm vi).
 * @param options.activeOnly Chỉ nhóm đang dùng (chọn nhóm cho hàng mới, nhóm cha).
 * @param options.exclude Nhóm không được chọn cùng mọi nhóm con của nó (chọn nhóm cha: không chọn chính nó hay con cháu).
 * @param options.itemLevelOnly Ô chọn nhóm của mặt hàng: chỉ chọn được nhóm cấp 4 – Chủng loại; nhóm cấp 1–3 vẫn hiện để mở nhánh
 *   nhưng bị khóa (design.md mục 5.7, N4-5). Nhóm đang gắn của mặt hàng cũ (cấp thấp hơn) vẫn hiện được trong ô.
 * @param options.parentForDepth Ô chọn nhóm cha của một nhánh sâu n cấp (thêm mới: 1): khóa nhóm mà cấp + n > 4 – nhóm cấp 4 là cấp
 *   cuối, nhóm làm nhánh vượt 4 cấp (design.md mục 5.7, N4-3, N4-4, N4-9).
 * @returns Các nút gốc.
 */
export function buildCategoryTree(
  categories: ItemCategory[],
  options: { activeOnly?: boolean; exclude?: string; parentForDepth?: number; itemLevelOnly?: boolean } = {},
): CategoryNode[] {
  const visible = categories.filter((c) => !options.activeOnly || c.isActive)
  const ids = new Set(visible.map((c) => c.id))
  const nodes = new Map<string, CategoryNode>(
    visible.map((c) => [c.id, { value: c.id, title: `${c.code} – ${c.name}`, level: c.level, children: [] }]),
  )
  const roots: CategoryNode[] = []
  for (const c of visible) {
    const node = nodes.get(c.id)!
    if (c.parentId && ids.has(c.parentId)) nodes.get(c.parentId)!.children.push(node)
    else roots.push(node)
  }
  if (options.parentForDepth) {
    const depth = options.parentForDepth
    for (const node of nodes.values()) {
      if (node.level + depth <= CATEGORY_MAX_LEVEL) continue
      node.disabled = true
      node.hint = node.level >= CATEGORY_MAX_LEVEL ? 'cấp cuối' : `nhánh sẽ vượt ${CATEGORY_MAX_LEVEL} cấp`
    }
  }
  if (options.itemLevelOnly) {
    for (const node of nodes.values()) {
      if (node.level >= CATEGORY_MAX_LEVEL) continue
      node.disabled = true
      node.hint = `chọn nhóm cấp ${CATEGORY_MAX_LEVEL}`
    }
  }
  if (options.exclude) {
    const disable = (node: CategoryNode) => {
      node.disabled = true
      node.hint = undefined
      node.children.forEach(disable)
    }
    const excluded = nodes.get(options.exclude)
    if (excluded) disable(excluded)
  }
  return roots
}

/**
 * Nhóm cùng tên (so không dấu, như máy chủ) với tên đang gõ: trong cùng nhóm cha (máy chủ chặn) và ở nhánh khác (chỉ nhắc) – tránh tạo
 * trùng nhóm hàng (yêu cầu người dùng 07/11/2026).
 * @param catalog Cả danh mục nhóm hàng (kể cả ngoài phạm vi, ngừng dùng).
 * @param name Tên đang gõ.
 * @param parentId Nhóm cha đang chọn; null = gốc.
 * @param excludeId Nhóm đang sửa (không so với chính nó).
 */
export function sameNameCategories(catalog: ItemCategory[], name: string, parentId: string | null, excludeId?: string) {
  const key = normalizeVi(name)
  const same = key ? catalog.filter((c) => c.id !== excludeId && normalizeVi(c.name) === key) : []
  const sameParent = same.find((c) => (c.parentId ?? null) === parentId)
  return { sameParent, elsewhere: same.filter((c) => c !== sameParent) }
}
