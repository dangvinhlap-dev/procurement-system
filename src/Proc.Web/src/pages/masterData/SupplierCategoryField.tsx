import { PlusOutlined } from '@ant-design/icons'
import { Alert, App, Button, Form, Input, Modal } from 'antd'
import { useMemo, useState } from 'react'
import { useItemCategoryCatalog, useItemCategoryMutations, type ItemCategory } from '../../api/masterData'
import { Permission } from '../../auth/permissions'
import { useSharedCatalogRights } from '../../auth/useSharedCatalogRights'
import { TreeSelect } from '../../classic/TreeSelect'
import { CategoryDuplicateNotice } from './CategoryDuplicateNotice'
import { buildCategoryTree, sameNameCategories, type CategoryNode } from './categoryTree'

/**
 * Cây ngành hàng: nhóm đang dùng, cộng nhóm đã ngừng dùng mà NCC vẫn giữ (ghi rõ để người dùng bỏ nếu muốn).
 * @param catalog Cả danh mục nhóm hàng.
 * @param selected Id đang chọn.
 */
function categoryTree(catalog: ItemCategory[], selected: string[]): CategoryNode[] {
  const keep = new Set(selected)
  const visible = catalog
    .filter((c) => c.isActive || keep.has(c.id))
    .map((c) => (c.isActive ? c : { ...c, name: `${c.name} (ngừng dùng)` }))
  return buildCategoryTree(visible)
}

/**
 * Ô "Ngành hàng cung cấp" của NCC (design.md mục 5.3, 5.8): chọn nhiều nhóm hàng từ cả danh mục Nhóm hàng (ai cũng xem đủ – người dùng
 * chốt 07/11/2026); thiếu nhóm thì nút "Thêm nhóm hàng" tạo ngay rồi tự chọn. Chữ tự do cũ (`supplyScope`, ngừng dùng) chỉ hiện để tham khảo.
 * Đặt trong `Form` của ngăn kéo NCC (trường `categoryIds`).
 * @param editable Người dùng sửa được NCC.
 * @param legacyText Ngành hàng ghi chữ tự do trước đây.
 */
export function SupplierCategoryField({ editable, legacyText }: { editable: boolean; legacyText: string | null | undefined }) {
  const form = Form.useFormInstance()
  const catalog = useItemCategoryCatalog()
  const rights = useSharedCatalogRights(Permission.ItemCategoryManage)
  const watched = Form.useWatch('categoryIds', form) as string[] | undefined
  const selected = useMemo(() => watched ?? [], [watched])
  const tree = useMemo(() => categoryTree(catalog.data ?? [], selected), [catalog.data, selected])
  const [adding, setAdding] = useState(false)

  return (
    <>
      <Form.Item
        label="Ngành hàng cung cấp"
        extra="Chọn từ danh mục Nhóm hàng (gõ * để xem tất cả); chọn nhóm cha là gồm mọi nhóm con. Không cần duyệt."
      >
        <div style={{ display: 'flex', gap: 4, alignItems: 'start' }}>
          <Form.Item name="categoryIds" noStyle>
            <TreeSelect
              aria-label="Ngành hàng cung cấp"
              multiple
              allowClear
              treeData={tree}
              loading={catalog.isLoading}
              placeholder="Chọn nhóm hàng"
              style={{ flex: 1 }}
            />
          </Form.Item>
          {editable && rights.canCreate && (
            <Button icon={<PlusOutlined />} onClick={() => setAdding(true)}>
              Thêm nhóm hàng
            </Button>
          )}
        </div>
      </Form.Item>
      {legacyText && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 8 }}
          title={`Ngành hàng ghi trước đây (chữ tự do, ngừng dùng – chỉ để tham khảo khi chọn nhóm hàng): ${legacyText}`}
        />
      )}
      {adding && (
        <NewCategoryDialog
          catalog={catalog.data ?? []}
          onClose={() => setAdding(false)}
          onCreated={(category) => {
            form.setFieldValue('categoryIds', [...new Set([...selected, category.id])])
            setAdding(false)
          }}
        />
      )}
    </>
  )
}

interface NewCategoryValues {
  name: string
  parentId: string | undefined
}

/**
 * Hộp thoại thêm nhóm hàng ngay từ ngăn kéo NCC. Cảnh báo trùng ngay khi gõ (so không dấu, như máy chủ): trùng trong cùng nhóm cha thì
 * không cho lưu (máy chủ cũng chặn), trùng tên ở nhánh khác thì nhắc để người dùng cân nhắc chọn nhóm có sẵn.
 * @param catalog Cả danh mục nhóm hàng.
 * @param onClose Đóng.
 * @param onCreated Đã thêm – trả nhóm mới.
 */
function NewCategoryDialog({
  catalog,
  onClose,
  onCreated,
}: {
  catalog: ItemCategory[]
  onClose: () => void
  onCreated: (category: ItemCategory) => void
}) {
  const { message } = App.useApp()
  const [form] = Form.useForm<NewCategoryValues>()
  const mutations = useItemCategoryMutations()
  const [error, setError] = useState<string | null>(null)
  const name = (Form.useWatch('name', form) as string | undefined) ?? ''
  const parentId = (Form.useWatch('parentId', form) as string | undefined) ?? null
  // Người bị giới hạn nhóm hàng chỉ thêm được nhóm con trong nhánh của mình (design.md mục 5.7).
  const allScopes = catalog.every((c) => c.isInScope)
  const parents = useMemo(() => buildCategoryTree(catalog.filter((c) => c.isInScope), { activeOnly: true }), [catalog])

  const { sameParent, elsewhere } = sameNameCategories(catalog, name, parentId)

  const submit = async () => {
    const values = await form.validateFields().catch(() => null)
    if (!values || sameParent) return
    setError(null)
    try {
      const created = await mutations.create.mutateAsync({ code: null, name: values.name.trim(), parentId: values.parentId ?? null, inventoryAccount: null })
      message.success(`Đã thêm nhóm hàng ${created.code} và chọn làm ngành hàng của NCC.`)
      onCreated(created)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không thêm được nhóm hàng.')
    }
  }

  return (
    <Modal
      open
      title="Thêm nhóm hàng"
      okText="Lưu"
      cancelText="Đóng"
      onOk={submit}
      onCancel={onClose}
      okButtonProps={{ disabled: !!sameParent }}
      confirmLoading={mutations.create.isPending}
      destroyOnHidden
    >
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      {/* Tên form riêng: id ô có tiền tố, không trùng ô "name" của ngăn kéo NCC phía sau (nhãn trỏ đúng ô). */}
      <Form form={form} name="newItemCategory" layout="vertical" initialValues={{ name: '', parentId: undefined }}>
        <Form.Item
          label="Nhóm cha"
          name="parentId"
          rules={allScopes ? [] : [{ required: true, message: 'Hãy chọn nhóm cha trong phạm vi của bạn.' }]}
          extra={allScopes ? 'Để trống = nhóm gốc.' : 'Bạn chỉ được làm việc với một số nhóm hàng nên chỉ thêm được nhóm con trong các nhóm đó.'}
        >
          <TreeSelect aria-label="Nhóm cha" treeData={parents} allowClear treeDefaultExpandAll placeholder="(Nhóm gốc)" />
        </Form.Item>
        <Form.Item label="Tên nhóm hàng" name="name" rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên nhóm hàng.' }, { max: 200 }]}>
          <Input autoComplete="off" />
        </Form.Item>
      </Form>
      <CategoryDuplicateNotice sameParent={sameParent} elsewhere={elsewhere} />
    </Modal>
  )
}
