import { App, Checkbox, Form, Input, TreeSelect } from 'antd'
import { useMemo, useState } from 'react'
import { useItemCategories, useItemCategoryMutations, type ItemCategory } from '../../api/masterData'
import { Permission } from '../../auth/permissions'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { ListPage } from '../../components/ListPage/ListPage'
import { useSharedCatalogRights } from '../../auth/useSharedCatalogRights'
import { SharedCatalogNotice } from '../../components/ListPage/SharedCatalogNotice'
import { buildCategoryTree } from './categoryTree'

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
]

/**
 * Màn hình Nhóm hàng (design.md mục 5.2, 5.7): cây nhóm dùng chung, cũng là căn cứ phân quyền theo nhóm hàng.
 * Chỉ thấy nhóm trong phạm vi được phép; thêm ở tổng công ty, sửa thông tin (trừ mã) theo quyền.
 */
export function ItemCategoryListPage() {
  const categories = useItemCategories()
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
          canManage={open === null ? rights.canCreate : rights.canEdit}
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
 * @param categories Mọi nhóm đang thấy.
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
    () => buildCategoryTree(categories, { activeOnly: true, exclude: category?.id }),
    [categories, category?.id],
  )

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
                <TreeSelect treeData={tree} allowClear showSearch treeNodeFilterProp="title" treeDefaultExpandAll placeholder="(Nhóm gốc)" />
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
