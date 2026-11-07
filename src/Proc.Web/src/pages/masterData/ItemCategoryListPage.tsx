import { Alert, App, Checkbox, Form, Input } from 'antd'
import { TreeSelect } from '../../classic/TreeSelect'
import { useMemo, useState } from 'react'
import { useItemCategoryCatalog, useItemCategoryMutations, type ItemCategory } from '../../api/masterData'
import { Permission } from '../../auth/permissions'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { ListPage } from '../../components/ListPage/ListPage'
import { useSharedCatalogRights } from '../../auth/useSharedCatalogRights'
import { SharedCatalogNotice } from '../../components/ListPage/SharedCatalogNotice'
import { normalizeVi } from '../../utils/text'
import { CategoryDuplicateNotice } from './CategoryDuplicateNotice'
import { buildCategoryTree, sameNameCategories } from './categoryTree'

const columns: GridColumn<ItemCategory>[] = [
  { key: 'path', title: 'Đường dẫn', width: 200, value: (r) => r.path },
  { key: 'code', title: 'Mã nhóm', width: 100, value: (r) => r.code },
  // Thụt lề theo cấp để danh sách (xếp theo đường dẫn) đọc như cây.
  { key: 'name', title: 'Tên nhóm hàng', width: 260, value: (r) => `${'   '.repeat(r.level - 1)}${r.name}` },
  { key: 'parent', title: 'Nhóm cha', width: 100, value: (r) => r.parentCode, groupable: true },
  { key: 'account', title: 'TK kho', width: 80, value: (r) => r.inventoryAccount, groupable: true },
  { key: 'items', title: 'Số mặt hàng', width: 100, type: 'quantity', value: (r) => r.itemCount },
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
  { key: 'scope', title: 'Phạm vi của bạn', width: 120, value: (r) => (r.isInScope ? 'Trong phạm vi' : 'Chỉ xem'), groupable: true },
]

/**
 * Màn hình Nhóm hàng (design.md mục 5.2, 5.7): cây nhóm dùng chung, cũng là căn cứ phân quyền theo nhóm hàng.
 * Ai cũng xem đủ cả cây (người dùng chốt 07/11/2026 – để tránh tạo trùng, chọn ngành hàng NCC); chỉ sửa được nhóm trong phạm vi;
 * thêm ở tổng công ty, sửa thông tin (trừ mã) theo quyền.
 */
export function ItemCategoryListPage() {
  const categories = useItemCategoryCatalog()
  // Thêm mới ở tổng công ty; sửa thông tin (trừ mã) theo quyền – design.md mục 5.7.
  const rights = useSharedCatalogRights(Permission.ItemCategoryManage)
  // undefined = đóng; null = thêm mới; còn lại = nhóm đang mở.
  const [open, setOpen] = useState<ItemCategory | null | undefined>(undefined)

  return (
    <>
      <ListPage
        title="Nhóm hàng"
        exportFileName="nhom-hang"
        excel={{ catalog: 'item-categories', title: 'nhóm hàng', canImport: rights.canCreate }}
        rows={categories.data ?? []}
        columns={columns}
        rowKey={(r) => r.id}
        auditEntity="ItemCategory"
        loading={categories.isLoading}
        canCreate={rights.canCreate}
        onCreate={() => setOpen(null)}
        onOpenRow={setOpen}
        notice={
          <SharedCatalogNotice rights={rights} />
        }
      />
      {open !== undefined && (
        <ItemCategoryDrawer
          category={open}
          categories={categories.data ?? []}
          canManage={open === null ? rights.canCreate : rights.canEdit && open.isInScope}
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
 * Ngăn kéo chi tiết nhóm hàng. Nhóm cha không chọn được chính nhóm này hay nhóm con cháu của nó (cây không thành vòng).
 * @param category Nhóm đang mở; null = thêm mới.
 * @param categories Cả danh mục (nhóm cha chỉ chọn trong phạm vi; kiểm tra trùng tên trên cả cây).
 * @param canManage Được sửa.
 * @param onClose Đóng.
 */
function ItemCategoryDrawer({
  category,
  categories,
  canManage,
  onClose,
}: {
  category: ItemCategory | null
  categories: ItemCategory[]
  canManage: boolean
  onClose: () => void
}) {
  const { message } = App.useApp()
  const [form] = Form.useForm<CategoryFormValues>()
  const mutations = useItemCategoryMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const tree = useMemo(
    () => buildCategoryTree(categories.filter((c) => c.isInScope), { activeOnly: true, exclude: category?.id }),
    [categories, category?.id],
  )
  const name = (Form.useWatch('name', form) as string | undefined) ?? ''
  const parentId = (Form.useWatch('parentId', form) as string | null | undefined) ?? null
  // Chỉ cảnh báo khi tên/nhóm cha khác bản đang lưu (nhóm trùng có từ trước vẫn sửa được thông tin khác).
  const changed = !category || normalizeVi(name) !== normalizeVi(category.name) || parentId !== (category.parentId ?? null)
  const duplicate = changed ? sameNameCategories(categories, name, parentId, category?.id) : { sameParent: undefined, elsewhere: [] }

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
      // Thêm mới: mã do hệ thống cấp theo dải đánh số (design.md mục 5.11).
      const saved = category
        ? await mutations.update.mutateAsync({ id: category.id, input: { ...input, isActive: values.isActive } })
        : await mutations.create.mutateAsync({ ...input, code: null })
      message.success(`Đã lưu nhóm hàng ${saved.code}.`)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được nhóm hàng.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <DetailDrawer
      open
      title={category ? `Nhóm hàng: ${category.code}` : 'Thêm nhóm hàng'}
      audit={{ entityType: 'ItemCategory', id: category?.id }}
      onClose={onClose}
      onSave={canManage ? save : undefined}
      saving={saving}
      error={error}
      form={form}
      initialValues={
        category
          ? { code: category.code, name: category.name, parentId: category.parentId, inventoryAccount: category.inventoryAccount ?? '', isActive: category.isActive }
          : { code: '', name: '', parentId: null, inventoryAccount: '', isActive: true }
      }
      readOnly={!canManage}
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <>
              {category && !category.isInScope && (
                <Alert type="info" showIcon style={{ marginBottom: 8 }} title="Nhóm này ngoài phạm vi nhóm hàng của bạn – chỉ xem được." />
              )}
              {canManage && <CategoryDuplicateNotice {...duplicate} />}
              <div className="detail-grid">
                <Form.Item
                  label="Mã nhóm"
                  name="code"
                  extra={category ? undefined : 'Mã do hệ thống tự cấp khi lưu, theo dải ở Hệ thống → Đánh số danh mục.'}
                >
                  <Input disabled placeholder="(tự cấp khi lưu)" />
                </Form.Item>
                <Form.Item
                  label="Tài khoản kho mặc định"
                  name="inventoryAccount"
                  rules={[{ pattern: /^\s*\d{3,10}\s*$/, message: 'Tài khoản gồm 3–10 chữ số, ví dụ 152.' }]}
                >
                  <Input autoComplete="off" placeholder="Ví dụ 152, 153, 156" />
                </Form.Item>
              </div>
              <Form.Item label="Tên nhóm hàng" name="name" rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên nhóm.' }, { max: 200 }]}>
                <Input />
              </Form.Item>
              <Form.Item
                label="Nhóm cha"
                name="parentId"
                extra="Để trống là nhóm gốc. Được phân quyền một nhóm thì được mọi nhóm con của nó."
              >
                <TreeSelect treeData={tree} allowClear showSearch treeDefaultExpandAll placeholder="(Nhóm gốc)" />
              </Form.Item>
              {category && (
                <Form.Item name="isActive" valuePropName="checked" extra="Ngừng dùng nhóm con và hàng trong nhóm trước.">
                  <Checkbox>Còn sử dụng</Checkbox>
                </Form.Item>
              )}
            </>
          ),
        },
      ]}
    />
  )
}
