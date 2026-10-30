import { Alert, App, Checkbox, Form, Input } from 'antd'
import { useState } from 'react'
import { useUomMutations, useUoms, type Uom } from '../../api/masterData'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { ListPage } from '../../components/ListPage/ListPage'

const columns: GridColumn<Uom>[] = [
  { key: 'code', title: 'Mã ĐVT', width: 100, value: (r) => r.code },
  { key: 'name', title: 'Tên đơn vị tính', width: 200, value: (r) => r.name },
  { key: 'items', title: 'Số mặt hàng dùng làm ĐVT cơ sở', width: 200, type: 'quantity', value: (r) => r.itemCount },
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

/** Danh mục dùng chung chỉ sửa ở tổng công ty (design.md mục 5.7). */
function useCanManage(): boolean {
  const { workingCompany, hasPermission } = useAuth()
  return workingCompany?.isHeadOffice === true && hasPermission(Permission.ItemManage)
}

/**
 * Màn hình Đơn vị tính (design.md mục 5.2): cái, hộp, thùng, kg… dùng chung toàn tập đoàn.
 */
export function UomListPage() {
  const uoms = useUoms()
  const canManage = useCanManage()
  const { workingCompany } = useAuth()
  // undefined = đóng; null = thêm mới; còn lại = ĐVT đang mở.
  const [open, setOpen] = useState<Uom | null | undefined>(undefined)

  return (
    <>
      <ListPage
        title="Đơn vị tính"
        exportFileName="don-vi-tinh"
        rows={uoms.data ?? []}
        columns={columns}
        rowKey={(r) => r.id}
        loading={uoms.isLoading}
        canCreate={canManage}
        onCreate={() => setOpen(null)}
        onOpenRow={setOpen}
        notice={
          !workingCompany?.isHeadOffice && (
            <Alert type="info" showIcon title="Danh mục dùng chung toàn tập đoàn: chỉ thêm, sửa khi làm việc ở tổng công ty." />
          )
        }
      />
      {open !== undefined && <UomDrawer uom={open} canManage={canManage} onClose={() => setOpen(undefined)} />}
    </>
  )
}

/**
 * Ngăn kéo chi tiết đơn vị tính.
 * @param uom ĐVT đang mở; null = thêm mới.
 * @param canManage Được sửa.
 * @param onClose Đóng.
 */
function UomDrawer({ uom, canManage, onClose }: { uom: Uom | null; canManage: boolean; onClose: () => void }) {
  const { message } = App.useApp()
  const [form] = Form.useForm<{ code: string; name: string; isActive: boolean }>()
  const mutations = useUomMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const save = async () => {
    const values = await form.validateFields().catch(() => null)
    if (!values) return
    setError(null)
    setSaving(true)
    try {
      if (uom) await mutations.update.mutateAsync({ id: uom.id, input: { name: values.name.trim(), isActive: values.isActive } })
      else await mutations.create.mutateAsync({ code: values.code, name: values.name.trim() })
      message.success(`Đã lưu đơn vị tính ${(uom?.code ?? values.code).toUpperCase()}.`)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được đơn vị tính.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <DetailDrawer
      open
      title={uom ? `Đơn vị tính: ${uom.code}` : 'Thêm đơn vị tính'}
      onClose={onClose}
      onSave={canManage ? save : undefined}
      saving={saving}
      error={error}
      form={form}
      initialValues={uom ? { code: uom.code, name: uom.name, isActive: uom.isActive } : { code: '', name: '', isActive: true }}
      readOnly={!canManage}
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <>
              <div className="detail-grid">
                <Form.Item
                  label="Mã ĐVT"
                  name="code"
                  extra={uom ? undefined : '1–10 chữ không dấu hoặc số, ví dụ THUNG. Không đổi được sau khi tạo.'}
                  rules={[
                    { required: true, message: 'Hãy nhập mã đơn vị tính.' },
                    { pattern: /^[A-Za-z0-9]{1,10}$/, message: 'Mã gồm 1–10 chữ không dấu hoặc số.' },
                  ]}
                  normalize={(v: string) => v.toUpperCase()}
                >
                  <Input disabled={uom !== null} autoFocus={uom === null} autoComplete="off" />
                </Form.Item>
                <Form.Item label="Tên đơn vị tính" name="name" rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên.' }, { max: 50 }]}>
                  <Input />
                </Form.Item>
              </div>
              {uom && (
                <Form.Item name="isActive" valuePropName="checked" extra="Không ngừng dùng được khi còn hàng đang dùng ĐVT này.">
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
