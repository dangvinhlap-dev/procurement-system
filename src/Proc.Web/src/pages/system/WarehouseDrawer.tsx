import { App, Checkbox, Form, Input } from 'antd'
import { useState } from 'react'
import { useWarehouseMutations, type WarehouseDetail } from '../../api/organization'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'

interface WarehouseFormValues {
  code: string
  name: string
  address: string
  keeper: string
  isActive: boolean
}

/** Chuỗi rỗng hoặc toàn khoảng trắng gửi lên API là null; còn lại bỏ khoảng trắng hai đầu. */
const orNull = (value: string | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Ngăn kéo chi tiết kho – nơi nhận hàng (design.md mục 19.4): danh sách phẳng, không kho cha, không tồn âm; mã không đổi được,
 * kho nào cũng ngừng / dùng lại được.
 * @param warehouse Kho đang mở; null = thêm kho.
 * @param onClose Đóng ngăn kéo.
 */
export function WarehouseDrawer({ warehouse, onClose }: { warehouse: WarehouseDetail | null; onClose: () => void }) {
  const { message } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<WarehouseFormValues>()
  const mutations = useWarehouseMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const isNew = warehouse === null
  const editable = hasPermission(Permission.WarehouseManage)

  const initialValues: WarehouseFormValues = warehouse
    ? {
        code: warehouse.code,
        name: warehouse.name,
        address: warehouse.address ?? '',
        keeper: warehouse.keeper ?? '',
        isActive: warehouse.isActive,
      }
    : { code: '', name: '', address: '', keeper: '', isActive: true }

  const save = async () => {
    let values: WarehouseFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn trường chưa hợp lệ – xem các ô báo đỏ.')
      return
    }
    setError(null)
    setSaving(true)
    const input = { name: values.name.trim(), address: orNull(values.address), keeper: orNull(values.keeper) }
    try {
      if (isNew) {
        await mutations.create.mutateAsync({ ...input, code: values.code })
        message.success(`Đã thêm kho ${values.code.toUpperCase()}.`)
      } else {
        await mutations.update.mutateAsync({ id: warehouse.id, input: { ...input, isActive: values.isActive } })
        message.success(`Đã lưu kho ${warehouse.code}.`)
      }
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được kho.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <DetailDrawer
      open
      title={isNew ? 'Thêm kho' : `Kho: ${warehouse.code}`}
      audit={{ entityType: 'Warehouse', id: warehouse?.id }}
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
              <div className="detail-grid">
                <Form.Item
                  label="Mã kho"
                  name="code"
                  extra={isNew ? '2–20 chữ không dấu hoặc số, ví dụ KP3. Không đổi được sau khi tạo.' : undefined}
                  rules={[
                    { required: true, message: 'Hãy nhập mã kho.' },
                    { pattern: /^[A-Za-z0-9]{2,20}$/, message: 'Mã gồm 2–20 chữ không dấu hoặc số.' },
                  ]}
                  normalize={(v: string) => v.toUpperCase()}
                >
                  <Input disabled={!isNew} autoFocus={isNew} autoComplete="off" />
                </Form.Item>
                <Form.Item label="Tên kho" name="name" rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên kho.' }, { max: 200 }]}>
                  <Input />
                </Form.Item>
              </div>
              <div className="detail-grid">
                <Form.Item label="Thủ kho" name="keeper" rules={[{ max: 200 }]}>
                  <Input />
                </Form.Item>
                <Form.Item label="Địa chỉ" name="address" rules={[{ max: 500 }]}>
                  <Input />
                </Form.Item>
              </div>
              {!isNew && (
                <Form.Item name="isActive" valuePropName="checked" extra="Kho ngừng dùng không chọn được làm nơi nhận trên phiếu mới.">
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
