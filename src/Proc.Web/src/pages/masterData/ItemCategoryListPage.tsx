import { NodeCollapseOutlined, NodeExpandOutlined, SubnodeOutlined } from '@ant-design/icons'
import { Alert, App, Button, Checkbox, Form, Input } from 'antd'
import { Fragment, useMemo, useState } from 'react'
import { useItemCategoryCatalog, useItemCategoryMutations, type ItemCategory } from '../../api/masterData'
import { Permission } from '../../auth/permissions'
import { useSharedCatalogRights } from '../../auth/useSharedCatalogRights'
import { StatusLight, type StatusLevel } from '../../classic/ClassicScreen'
import { CATEGORY_LEVEL_NAMES, CATEGORY_MAX_LEVEL } from '../../classic/categoryLevels'
import { LevelBadge } from '../../classic/LevelBadge'
import { Select } from '../../classic/Select'
import { TreeSelect } from '../../classic/TreeSelect'
import { DataGrid } from '../../components/DataGrid/DataGrid'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { SharedCatalogNotice } from '../../components/ListPage/SharedCatalogNotice'
import { normalizeVi } from '../../utils/text'
import { CategoryDuplicateNotice } from './CategoryDuplicateNotice'
import { branchDepth, buildCategoryTree, sameNameCategories } from './categoryTree'

/** Quyền thêm / sửa danh mục dùng chung của người dùng. */
type SharedCatalogRights = ReturnType<typeof useSharedCatalogRights>

/** Nhóm đang mở trong ngăn kéo: `category` null = thêm mới, khi đó `parentId` là nhóm cha điền sẵn ("Thêm nhóm con"). */
interface OpenState {
  category: ItemCategory | null
  parentId?: string | null
}

/** Chữ "cấp n – tên cấp", ví dụ "3 – Loại hàng". */
function levelText(level: number) {
  return `${level} – ${CATEGORY_LEVEL_NAMES[level] ?? ''}`
}

/**
 * Lý do không thêm được nhóm con dưới nhóm đang chọn (design.md mục 5.7: N4-3 nhóm cấp 4 là cấp cuối; nhóm ngoài phạm vi chỉ xem; nhóm
 * ngừng dùng không làm nhóm cha). null = thêm được.
 */
function childBlockReason(category: ItemCategory): string | null {
  const label = `Nhóm ${category.code} – ${category.name}`
  if (category.level >= CATEGORY_MAX_LEVEL)
    return `${label} đã ở cấp ${CATEGORY_MAX_LEVEL} (cấp cuối), không thêm nhóm con được – nhóm hàng tối đa ${CATEGORY_MAX_LEVEL} cấp.`
  if (!category.isInScope) return `${label} ngoài phạm vi nhóm hàng của bạn – chỉ xem, không thêm nhóm con.`
  if (!category.isActive) return `${label} đã ngừng dùng – không thêm nhóm con.`
  return null
}

/**
 * Dòng gợi ý dưới thanh lệnh cho dòng đang chọn (bản vẽ N4 mục 3.1): đèn vàng + lý do khi không thêm được nhóm con, đèn xanh + cấp của nhóm
 * con sẽ tạo khi thêm được. Đèn luôn có chữ đi kèm (ui-style.md mục 2).
 */
function SelectionHint({ selected, canCreate }: { selected: ItemCategory | null; canCreate: boolean }) {
  if (!selected || !canCreate) return null
  const blocked = childBlockReason(selected)
  const level: StatusLevel = blocked ? 'warning' : 'success'
  const text =
    blocked ??
    `Đã chọn ${selected.code} – ${selected.name} (cấp ${levelText(selected.level)}). Nhóm con thêm vào sẽ ở cấp ${levelText(selected.level + 1)}.`
  return (
    <div className="erp-hint-line" data-testid="category-selection-hint">
      <StatusLight level={level} label={blocked ? 'Không thêm nhóm con được' : 'Thêm nhóm con được'} />
      <span>{text}</span>
    </div>
  )
}

/**
 * Màn hình Nhóm hàng CAT (design.md mục 5.2, 5.7; bản vẽ N4 đã duyệt 14/11/2026): cây nhóm dùng chung tối đa 4 cấp – Ngành hàng, Nhóm hàng,
 * Loại hàng, Chủng loại – cũng là căn cứ phân quyền theo nhóm hàng. Lưới xếp theo đường dẫn, cột Cấp có ô số màu, cột tên mở/đóng nhánh;
 * nút "Thêm nhóm con" thao tác trên dòng đang chọn và tự khóa ở nhóm cấp 4.
 * Ai cũng xem đủ cả cây (người dùng chốt 07/11/2026 – để tránh tạo trùng, chọn ngành hàng NCC); chỉ sửa được nhóm trong phạm vi;
 * thêm ở tổng công ty, sửa thông tin (trừ mã) theo quyền.
 */
export function ItemCategoryListPage() {
  const categories = useItemCategoryCatalog()
  const all = useMemo(() => categories.data ?? [], [categories.data])
  // Thêm mới ở tổng công ty; sửa thông tin (trừ mã) theo quyền – design.md mục 5.7.
  const rights = useSharedCatalogRights(Permission.ItemCategoryManage)
  const [open, setOpen] = useState<OpenState | undefined>(undefined)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  // 0 = mọi cấp. Lọc theo cấp thì lưới thành danh sách phẳng (không mở/đóng nhánh).
  const [levelFilter, setLevelFilter] = useState(0)
  const [collapsed, setCollapsed] = useState<Set<string>>(() => new Set())

  const byId = useMemo(() => new Map(all.map((c) => [c.id, c])), [all])
  const selected = (selectedId && byId.get(selectedId)) || null

  const rows = useMemo(() => {
    if (levelFilter) return all.filter((c) => c.level === levelFilter)
    // Ẩn nhóm có một nhóm tổ tiên đang thu gọn (đi ngược theo nhóm cha, tối đa 4 cấp).
    const hidden = (c: ItemCategory) => {
      let parentId = c.parentId
      for (let i = 0; parentId && i < CATEGORY_MAX_LEVEL; i++) {
        if (collapsed.has(parentId)) return true
        parentId = byId.get(parentId)?.parentId ?? null
      }
      return false
    }
    return collapsed.size ? all.filter((c) => !hidden(c)) : all
  }, [all, byId, collapsed, levelFilter])

  const toggle = (id: string) =>
    setCollapsed((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const columns = useMemo<GridColumn<ItemCategory>[]>(
    () => [
      {
        key: 'level',
        title: 'Cấp',
        width: 120,
        value: (r) => levelText(r.level),
        render: (r) => (
          <>
            <LevelBadge level={r.level} />
            {r.levelName}
          </>
        ),
        groupable: true,
        auditField: false,
      },
      { key: 'code', title: 'Mã nhóm', width: 100, value: (r) => r.code },
      {
        key: 'name',
        title: 'Tên nhóm hàng',
        width: 280,
        value: (r) => r.name,
        // Thụt lề theo cấp + nút mở/đóng nhánh để danh sách (xếp theo đường dẫn) đọc như cây (ui-style.md mục 5h).
        render: (r) => {
          const tree = !levelFilter
          const isCollapsed = collapsed.has(r.id)
          return (
            <span className="erp-tree-cell" style={tree ? { paddingLeft: (r.level - 1) * 16 } : undefined}>
              {tree && r.childCount > 0 ? (
                <button
                  type="button"
                  className="erp-tree-toggle"
                  aria-label={isCollapsed ? `Mở nhánh ${r.code}` : `Thu nhánh ${r.code}`}
                  aria-expanded={!isCollapsed}
                  title={isCollapsed ? 'Mở nhánh' : 'Thu nhánh'}
                  onClick={(e) => {
                    e.stopPropagation()
                    toggle(r.id)
                  }}
                >
                  {isCollapsed ? '▸' : '▾'}
                </button>
              ) : (
                tree && <span className="erp-tree-toggle-space" />
              )}
              {r.name}
            </span>
          )
        },
      },
      { key: 'path', title: 'Đường dẫn', width: 200, value: (r) => r.path, auditField: false },
      { key: 'parent', title: 'Nhóm cha', width: 100, value: (r) => r.parentCode, groupable: true },
      { key: 'children', title: 'Số nhóm con', width: 100, type: 'quantity', value: (r) => r.childCount, auditField: false },
      { key: 'items', title: 'Số mặt hàng', width: 100, type: 'quantity', value: (r) => r.itemCount, auditField: false },
      { key: 'account', title: 'TK kho', width: 80, value: (r) => r.inventoryAccount, groupable: true },
      {
        key: 'status',
        title: 'Trạng thái',
        width: 110,
        type: 'status',
        value: (r) => (r.isActive ? 'Đang dùng' : 'Ngừng dùng'),
        tone: (r) => (r.isActive ? 'success' : 'neutral'),
        groupable: true,
      },
      // Danh mục ai cũng xem đủ; nhóm ngoài phạm vi nhóm hàng của người xem chỉ xem (design.md mục 9.2).
      { key: 'scope', title: 'Phạm vi của bạn', width: 120, value: (r) => (r.isInScope ? 'Trong phạm vi' : 'Chỉ xem'), groupable: true, auditField: false },
    ],
    [collapsed, levelFilter],
  )

  const blocked = selected ? childBlockReason(selected) : 'Chọn một dòng trong lưới làm nhóm cha.'
  const parentsWithChildren = useMemo(() => all.filter((c) => c.childCount > 0).map((c) => c.id), [all])

  return (
    <>
      <ListPage
        title="Nhóm hàng"
        exportFileName="nhom-hang"
        excel={{ catalog: 'item-categories', title: 'nhóm hàng', canImport: rights.canCreate }}
        rows={rows}
        columns={columns}
        rowKey={(r) => r.id}
        auditEntity="ItemCategory"
        loading={categories.isLoading}
        canCreate={rights.canCreate}
        onCreate={() => setOpen({ category: null, parentId: null })}
        onOpenRow={(r) => setOpen({ category: r })}
        onSelectRow={(r) => setSelectedId(r?.id ?? null)}
        actions={
          <>
            {rights.canCreate && (
              <Button
                icon={<SubnodeOutlined />}
                disabled={!!blocked}
                title={blocked ?? 'Thêm nhóm con dưới nhóm đang chọn'}
                onClick={() => selected && setOpen({ category: null, parentId: selected.id })}
              >
                Thêm nhóm con
              </Button>
            )}
            <Button
              icon={<NodeExpandOutlined />}
              disabled={!!levelFilter || collapsed.size === 0}
              title="Mở mọi nhánh"
              onClick={() => setCollapsed(new Set())}
            >
              Mở hết nhánh
            </Button>
            <Button
              icon={<NodeCollapseOutlined />}
              disabled={!!levelFilter || parentsWithChildren.length === 0}
              title="Thu gọn mọi nhánh, chỉ còn nhóm cấp 1"
              onClick={() => setCollapsed(new Set(parentsWithChildren))}
            >
              Thu gọn nhánh
            </Button>
          </>
        }
        filters={
          <FilterField label="Cấp" width={150}>
            {(id) => (
              <Select
                id={id}
                value={levelFilter}
                onChange={(v) => setLevelFilter(Number(v))}
                options={[
                  { value: 0, label: 'Tất cả' },
                  ...[1, 2, 3, 4].map((lv) => ({ value: lv, label: levelText(lv) })),
                ]}
              />
            )}
          </FilterField>
        }
        onClearFilters={() => setLevelFilter(0)}
        notice={
          <>
            <SharedCatalogNotice rights={rights} />
            <SelectionHint selected={selected} canCreate={rights.canCreate} />
          </>
        }
      />
      {open !== undefined && (
        <ItemCategoryDrawer
          // Đổi nhóm (bấm đường dẫn, mở nhóm con) thì dựng lại form với giá trị của nhóm mới.
          key={open.category?.id ?? `new:${open.parentId ?? ''}`}
          category={open.category}
          presetParentId={open.parentId ?? null}
          categories={all}
          rights={rights}
          onOpen={setOpen}
          onClose={() => setOpen(undefined)}
        />
      )}
    </>
  )
}

interface CategoryFormValues {
  code: string
  name: string
  parentId: string | null
  inventoryAccount: string
  isActive: boolean
}

/**
 * Ngăn kéo chi tiết nhóm hàng (bản vẽ N4 mục 3.2, 3.3): tiêu đề có ô "Cấp n/4", đường dẫn nhóm cha bấm được, ô Cấp chỉ đọc tự đổi theo
 * nhóm cha, tab NHÓM CON có nút "Thêm nhóm con". Ô nhóm cha không chọn được chính nhóm này, nhóm con cháu (cây không thành vòng), nhóm cấp 4
 * và nhóm làm nhánh vượt 4 cấp (design.md mục 5.7, N4-3, N4-4).
 * @param category Nhóm đang mở; null = thêm mới.
 * @param presetParentId Thêm mới: nhóm cha điền sẵn ("Thêm nhóm con"); null = nhóm gốc.
 * @param categories Cả danh mục (nhóm cha chỉ chọn trong phạm vi; kiểm tra trùng tên, số cấp nhánh trên cả cây).
 * @param rights Quyền thêm / sửa danh mục dùng chung.
 * @param onOpen Mở nhóm khác trong ngăn kéo (nhóm cha trên đường dẫn, nhóm con) hoặc thêm nhóm con.
 * @param onClose Đóng.
 */
function ItemCategoryDrawer({
  category,
  presetParentId,
  categories,
  rights,
  onOpen,
  onClose,
}: {
  category: ItemCategory | null
  presetParentId: string | null
  categories: ItemCategory[]
  rights: SharedCatalogRights
  onOpen: (state: OpenState) => void
  onClose: () => void
}) {
  const { message } = App.useApp()
  const [form] = Form.useForm<CategoryFormValues>()
  const mutations = useItemCategoryMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const canManage = category ? rights.canEdit && category.isInScope : rights.canCreate
  const byId = useMemo(() => new Map(categories.map((c) => [c.id, c])), [categories])
  const tree = useMemo(
    () =>
      buildCategoryTree(
        categories.filter((c) => c.isInScope),
        { activeOnly: true, exclude: category?.id, parentForDepth: category ? branchDepth(categories, category.id) : 1 },
      ),
    [categories, category],
  )
  const name = (Form.useWatch('name', form) as string | undefined) ?? ''
  const watchedParent = Form.useWatch('parentId', form) as string | null | undefined
  // Trước lần vẽ đầu useWatch trả undefined – lấy giá trị ban đầu để ô Cấp, cảnh báo trùng không nháy.
  const parentId = watchedParent === undefined ? (category ? category.parentId : presetParentId) : watchedParent
  const parent = parentId ? byId.get(parentId) : undefined
  const level = parent ? parent.level + 1 : 1
  // Chỉ cảnh báo khi tên/nhóm cha khác bản đang lưu (nhóm trùng có từ trước vẫn sửa được thông tin khác).
  const changed = !category || normalizeVi(name) !== normalizeVi(category.name) || parentId !== (category.parentId ?? null)
  const duplicate = changed ? sameNameCategories(categories, name, parentId, category?.id) : { sameParent: undefined, elsewhere: [] }

  // Đường dẫn từ nhóm cấp 1 đến nhóm cha (nhóm đang mở đứng cuối).
  const ancestors = useMemo(() => {
    const chain: ItemCategory[] = []
    let id = category ? category.parentId : presetParentId
    for (let i = 0; id && i < CATEGORY_MAX_LEVEL; i++) {
      const c = byId.get(id)
      if (!c) break
      chain.unshift(c)
      id = c.parentId
    }
    return chain
  }, [byId, category, presetParentId])
  const children = useMemo(() => (category ? categories.filter((c) => c.parentId === category.id) : []), [categories, category])

  const save = async () => {
    const values = await form.validateFields().catch(() => null)
    if (!values) return
    setError(null)
    setSaving(true)
    const input = {
      name: values.name.trim(),
      parentId: values.parentId ?? null,
      inventoryAccount: values.inventoryAccount?.trim() || null,
    }
    try {
      // Thêm mới: mã do người dùng gõ (người dùng chốt 14/11/2026 – design.md mục 5.7, N4-7); máy chủ báo trùng, vượt cấp.
      const saved = category
        ? await mutations.update.mutateAsync({ id: category.id, input: { ...input, isActive: values.isActive } })
        : await mutations.create.mutateAsync({ ...input, code: values.code.trim().toUpperCase() })
      message.success(`Đã lưu nhóm hàng ${saved.code}.`)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được nhóm hàng.')
    } finally {
      setSaving(false)
    }
  }

  const blocked = category ? childBlockReason(category) : null
  const title = category
    ? `Nhóm hàng: ${category.code} – ${category.name}`
    : parent
      ? `Thêm nhóm con của ${parent.code}`
      : 'Thêm nhóm hàng cấp 1'

  return (
    <DetailDrawer
      open
      title={title}
      titleExtra={<LevelBadge level={category?.level ?? level} text={`Cấp ${category?.level ?? level}/${CATEGORY_MAX_LEVEL}`} />}
      audit={{ entityType: 'ItemCategory', id: category?.id }}
      onClose={onClose}
      onSave={canManage ? save : undefined}
      saving={saving}
      error={error}
      form={form}
      initialValues={
        category
          ? { code: category.code, name: category.name, parentId: category.parentId, inventoryAccount: category.inventoryAccount ?? '', isActive: category.isActive }
          : { code: '', name: '', parentId: presetParentId, inventoryAccount: parent?.inventoryAccount ?? '', isActive: true }
      }
      readOnly={!canManage}
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <>
              {ancestors.length > 0 && (
                <nav className="erp-crumb" aria-label="Đường dẫn nhóm hàng">
                  <span>Đường dẫn:</span>
                  {ancestors.map((a) => (
                    <Fragment key={a.id}>
                      <button type="button" className="erp-crumb-link" title={`Mở nhóm ${a.code}`} onClick={() => onOpen({ category: a })}>
                        {a.code} – {a.name}
                      </button>
                      <span className="erp-crumb-sep">›</span>
                    </Fragment>
                  ))}
                  <span className="erp-crumb-current">{category ? `${category.code} – ${category.name}` : '(nhóm mới)'}</span>
                </nav>
              )}
              {category && !category.isInScope && (
                <Alert type="info" showIcon style={{ marginBottom: 8 }} title="Nhóm này ngoài phạm vi nhóm hàng của bạn – chỉ xem được." />
              )}
              {canManage && <CategoryDuplicateNotice {...duplicate} />}
              <div className="detail-grid">
                <Form.Item
                  label="Mã nhóm"
                  name="code"
                  extra={category ? undefined : '2–20 chữ không dấu hoặc số, không đổi được sau khi lưu.'}
                  rules={
                    category
                      ? []
                      : [
                          { required: true, whitespace: true, message: 'Hãy nhập mã nhóm.' },
                          { pattern: /^\s*[A-Za-z0-9]{2,20}\s*$/, message: 'Mã gồm 2–20 chữ không dấu hoặc số.' },
                        ]
                  }
                  normalize={(v: string) => v?.toUpperCase()}
                >
                  <Input disabled={!!category} autoComplete="off" maxLength={20} placeholder="Ví dụ VT, VTD01" />
                </Form.Item>
                {/* Cấp tính từ nhóm cha (N4-1), không nhập – đổi nhóm cha thì máy chủ tính lại cấp cả nhánh (N4-2). */}
                <Form.Item label="Cấp" htmlFor="category-level">
                  <Input id="category-level" disabled value={levelText(level)} />
                </Form.Item>
              </div>
              <Form.Item label="Tên nhóm hàng" name="name" rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên nhóm.' }, { max: 200 }]}>
                <Input />
              </Form.Item>
              <Form.Item
                label="Nhóm cha"
                name="parentId"
                extra={`Để trống là nhóm cấp 1 (Ngành hàng). Nhóm cấp ${CATEGORY_MAX_LEVEL} và nhóm làm nhánh vượt ${CATEGORY_MAX_LEVEL} cấp bị khóa. Được phân quyền một nhóm thì được mọi nhóm con của nó.`}
              >
                <TreeSelect treeData={tree} allowClear showSearch treeDefaultExpandAll placeholder="(Nhóm cấp 1)" />
              </Form.Item>
              <div className="detail-grid">
                <Form.Item
                  label="Tài khoản kho mặc định"
                  name="inventoryAccount"
                  rules={[{ pattern: /^\s*\d{3,10}\s*$/, message: 'Tài khoản gồm 3–10 chữ số, ví dụ 152.' }]}
                >
                  <Input autoComplete="off" placeholder="Ví dụ 152, 153, 156" />
                </Form.Item>
                {category && (
                  <Form.Item label="Trạng thái" name="isActive" valuePropName="checked" extra="Ngừng dùng nhóm con và hàng trong nhóm trước.">
                    <Checkbox>Còn sử dụng</Checkbox>
                  </Form.Item>
                )}
              </div>
            </>
          ),
        },
        ...(category
          ? [
              {
                key: 'children',
                label: `Nhóm con (${children.length})`,
                // Tab không có ô form – chỉ vẽ khi mở (ui-style.md mục 5f).
                lazy: true,
                children: (
                  <ChildCategoriesTab
                    category={category}
                    rows={children}
                    blocked={blocked}
                    canCreate={rights.canCreate}
                    onOpen={onOpen}
                  />
                ),
              },
            ]
          : []),
      ]}
    />
  )
}

const childColumns: GridColumn<ItemCategory>[] = [
  { key: 'code', title: 'Mã nhóm', width: 100, value: (r) => r.code },
  { key: 'name', title: 'Tên nhóm', width: 240, value: (r) => r.name },
  { key: 'children', title: 'Số nhóm con', width: 100, type: 'quantity', value: (r) => r.childCount },
  { key: 'items', title: 'Số mặt hàng', width: 100, type: 'quantity', value: (r) => r.itemCount },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 110,
    type: 'status',
    value: (r) => (r.isActive ? 'Đang dùng' : 'Ngừng dùng'),
    tone: (r) => (r.isActive ? 'success' : 'neutral'),
  },
]

/**
 * Tab NHÓM CON của ngăn kéo nhóm hàng (bản vẽ N4 mục 3.2): lưới nhóm con trực tiếp (bấm đúp mở nhóm con) và nút "Thêm nhóm con"; nhóm cấp 4
 * thì thay nút bằng dòng giải thích (N4-3).
 */
function ChildCategoriesTab({
  category,
  rows,
  blocked,
  canCreate,
  onOpen,
}: {
  category: ItemCategory
  rows: ItemCategory[]
  blocked: string | null
  canCreate: boolean
  onOpen: (state: OpenState) => void
}) {
  const childLevel = category.level + 1
  return (
    <div className="category-children">
      <div className="erp-hint-line">
        {blocked ? (
          <>
            <StatusLight level="warning" label="Không thêm nhóm con được" />
            <span>{blocked}</span>
          </>
        ) : (
          <>
            <span>
              Nhóm con ở cấp {levelText(childLevel)}. Bấm đúp một dòng để mở nhóm con.
            </span>
            {canCreate && (
              <Button icon={<SubnodeOutlined />} onClick={() => onOpen({ category: null, parentId: category.id })}>
                Thêm nhóm con
              </Button>
            )}
          </>
        )}
      </div>
      <div style={{ height: 260 }}>
        <DataGrid
          rows={rows}
          columns={childColumns}
          rowKey={(r) => r.id}
          groupBy={null}
          onGroupByChange={() => undefined}
          onOpenRow={(r) => onOpen({ category: r })}
        />
      </div>
    </div>
  )
}
