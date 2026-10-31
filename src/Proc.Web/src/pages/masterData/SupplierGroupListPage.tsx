import { App, Checkbox, Form, Input } from 'antd'
import { useState } from 'react'
import { useSupplierGroupMutations, useSupplierGroups, type SupplierGroup } from '../../api/masterData'
import { Permission } from '../../auth/permissions'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { useSharedCatalogRights } from '../../auth/useSharedCatalogRights'
import { SharedCatalogNotice } from '../../components/ListPage/SharedCatalogNotice'
import { ListPage } from '../../components/ListPage/ListPage'

const columns: GridColumn<SupplierGroup>[] = [
  { key: 'code', title: 'Mã nhóm', width: 100, value: (r) => r.code },
  { key: 'name', title: 'Tên nhóm NCC', width: 240, value: (r) => r.name },
  { key: 'suppliers', title: 'Số NCC', width: 90, type: 'quantity', value: (r) => r.supplierCount },
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
 * Màn hình Nhóm NCC (design.md mục 5.3): trong nước, nhập khẩu, nội bộ… dùng chung toàn tập đoàn.
 */
export function SupplierGroupListPage() {
  const groups = useSupplierGroups()
  // Thêm mới ở tổng công ty; sửa thông tin (trừ mã) theo quyền – design.md mục 5.7.
  const rights = useSharedCatalogRights(Permission.SupplierManage)
  // undefined = đóng; null = thêm mới; còn lại = nhóm đang mở.
  const [open, setOpen] = useState<SupplierGroup | null | undefined>(undefined)

  return (
    <>
      <ListPage
        title="Nhóm nhà cung cấp"
        exportFileName="nhom-ncc"
        excel={{ catalog: 'supplier-groups', title: 'nhóm NCC', canImport: rights.canCreate }}
        rows={groups.data ?? []}
        columns={columns}
        rowKey={(r) => r.id}
        loading={groups.isLoading}
        canCreate={rights.canCreate}
        onCreate={() => setOpen(null)}
        onOpenRow={setOpen}
        notice={
          <SharedCatalogNotice rights={rights} />
        }
      />
      {open !== undefined && <SupplierGroupDrawer group={open} canManage={open === null ? rights.canCreate : rights.canEdit} onClose={() => setOpen(undefined)} />}
    </>
  )
}

/**
 * Ngăn kéo chi tiết nhóm NCC.
 * @param group Nhóm đang mở; null = thêm mới.
 * @param canManage Được sửa.
 * @param onClose Đóng.
 */
function SupplierGroupDrawer({ group, canManage, onClose }: { group: SupplierGroup | null; canManage: boolean; onClose: () => void }) {
  const { message } = App.useApp()
  const [form] = Form.useForm<{ code: string; name: string; isActive: boolean }>()
  const mutations = useSupplierGroupMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const save = async () => {
    const values = await form.validateFields().catch(() => null)
    if (!values) return
    setError(null)
    setSaving(true)
    try {
      // Thêm mới: mã do hệ thống cấp theo dải đánh số (design.md mục 5.11).
      const saved = group
        ? await mutations.update.mutateAsync({ id: group.id, input: { name: values.name.trim(), isActive: values.isActive } })
        : await mutations.create.mutateAsync({ code: null, name: values.name.trim() })
      message.success(`Đã lưu nhóm NCC ${saved.code}.`)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được nhóm NCC.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <DetailDrawer
      open
      title={group ? `Nhóm NCC: ${group.code}` : 'Thêm nhóm NCC'}
      onClose={onClose}
      onSave={canManage ? save : undefined}
      saving={saving}
      error={error}
      form={form}
      initialValues={group ? { code: group.code, name: group.name, isActive: group.isActive } : { code: '', name: '', isActive: true }}
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
                  extra={group ? undefined : 'Mã do hệ thống tự cấp khi lưu, theo dải ở Hệ thống → Đánh số danh mục.'}
                >
                  <Input disabled placeholder="(tự cấp khi lưu)" />
                </Form.Item>
                <Form.Item label="Tên nhóm NCC" name="name" rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên nhóm.' }, { max: 200 }]}>
                  <Input />
                </Form.Item>
              </div>
              {group && (
                <Form.Item name="isActive" valuePropName="checked" extra="Chuyển các NCC còn giao dịch sang nhóm khác trước khi ngừng dùng.">
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
