import { App, Checkbox, Form, Input } from 'antd'
import { useState } from 'react'
import { usePurchasingUnitMutations, type PurchasingUnit } from '../../api/purchasingUnits'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'

interface PurchasingUnitFormValues {
  code: string
  name: string
  note: string | null
  isActive: boolean
}

/**
 * Ngăn kéo chi tiết đơn vị mua (lát Y5a). Mã không đổi được sau khi tạo; ngừng dùng thì không chọn mới được trên bảng tổng hợp, dòng đã gán
 * giữ nguyên.
 * @param unit Đơn vị đang mở; null = thêm mới.
 * @param onClose Đóng ngăn kéo.
 */
export function PurchasingUnitDrawer({ unit, onClose }: { unit: PurchasingUnit | null; onClose: () => void }) {
  const { message } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<PurchasingUnitFormValues>()
  const mutations = usePurchasingUnitMutations()
  const editable = hasPermission(Permission.UnitManage)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const isNew = unit === null

  const initialValues: PurchasingUnitFormValues = {
    code: unit?.code ?? '',
    name: unit?.name ?? '',
    note: unit?.note ?? null,
    isActive: unit?.isActive ?? true,
  }

  const save = async () => {
    let values: PurchasingUnitFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn trường chưa hợp lệ – xem các ô báo đỏ.')
      return
    }
    setError(null)
    setSaving(true)
    const input = { name: values.name.trim(), note: values.note?.trim() || null }
    try {
      if (isNew) {
        await mutations.create.mutateAsync({ ...input, code: values.code })
        message.success(`Đã thêm đơn vị mua ${values.code.toUpperCase()}.`)
      } else {
        await mutations.update.mutateAsync({ id: unit.id, input: { ...input, isActive: values.isActive } })
        message.success(`Đã lưu đơn vị mua ${unit.code}.`)
      }
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được đơn vị mua.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <DetailDrawer
      open
      title={isNew ? 'Thêm đơn vị mua' : `Đơn vị mua: ${unit.code}`}
      audit={{ entityType: 'PurchasingUnit', id: unit?.id }}
      onClose={onClose}
      onSave={editable ? save : undefined}
      saving={saving}
      error={error}
      form={form}
      initialValues={initialValues}
      readOnly={!editable}
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <>
              <Form.Item
                label="Mã đơn vị"
                name="code"
                extra={isNew ? '2–20 chữ không dấu hoặc số, ví dụ VPNDD. Không đổi được sau khi tạo.' : undefined}
                rules={[
                  { required: true, message: 'Hãy nhập mã đơn vị mua.' },
                  { pattern: /^[A-Za-z0-9]{2,20}$/, message: 'Mã gồm 2–20 chữ không dấu hoặc số.' },
                ]}
                normalize={(v: string) => v.toUpperCase()}
              >
                <Input disabled={!isNew} autoFocus={isNew} autoComplete="off" />
              </Form.Item>
              <Form.Item
                label="Tên đơn vị mua"
                name="name"
                rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên đơn vị mua.' }, { max: 200 }]}
              >
                <Input />
              </Form.Item>
              <Form.Item label="Ghi chú" name="note" rules={[{ max: 500 }]}>
                <Input.TextArea rows={3} />
              </Form.Item>
              {!isNew && (
                <Form.Item
                  name="isActive"
                  valuePropName="checked"
                  extra="Ngừng dùng: không chọn mới được trên bảng tổng hợp 2A/2B; dòng đã gán giữ nguyên."
                >
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
