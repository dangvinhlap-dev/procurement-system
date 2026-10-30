import { Alert, App, Checkbox, Form, Input } from 'antd'
import { useState } from 'react'
import { useSupplierGroupMutations, useSupplierGroups, type SupplierGroup } from '../../api/masterData'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
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
  const { workingCompany, hasPermission } = useAuth()
  const groups = useSupplierGroups()
  const canManage = workingCompany?.isHeadOffice === true && hasPermission(Permission.SupplierManage)
  // undefined = đóng; null = thêm mới; còn lại = nhóm đang mở.
  const [open, setOpen] = useState<SupplierGroup | null | undefined>(undefined)

  return (
    <>
      <ListPage
        title="Nhóm nhà cung cấp"
        exportFileName="nhom-ncc"
        excel={{ catalog: 'supplier-groups', title: 'nhóm NCC', canImport: canManage }}
        rows={groups.data ?? []}
        columns={columns}
        rowKey={(r) => r.id}
        loading={groups.isLoading}
        canCreate={canManage}
        onCreate={() => setOpen(null)}
        onOpenRow={setOpen}
        notice={
          !workingCompany?.isHeadOffice && (
            <Alert type="info" showIcon title="Danh mục dùng chung toàn tập đoàn: chỉ thêm, sửa khi làm việc ở tổng công ty." />
          )
        }
      />
      {open !== undefined && <SupplierGroupDrawer group={open} canManage={canManage} onClose={() => setOpen(undefined)} />}
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
      if (group) await mutations.update.mutateAsync({ id: group.id, input: { name: values.name.trim(), isActive: values.isActive } })
      else await mutations.create.mutateAsync({ code: values.code, name: values.name.trim() })
      message.success(`Đã lưu nhóm NCC ${(group?.code ?? values.code).toUpperCase()}.`)
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
                  extra={group ? undefined : '2–20 chữ không dấu hoặc số. Không đổi được sau khi tạo.'}
                  rules={[
                    { required: true, message: 'Hãy nhập mã nhóm.' },
                    { pattern: /^[A-Za-z0-9]{2,20}$/, message: 'Mã gồm 2–20 chữ không dấu hoặc số.' },
                  ]}
                  normalize={(v: string) => v.toUpperCase()}
                >
                  <Input disabled={group !== null} autoFocus={group === null} autoComplete="off" />
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
