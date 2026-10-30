import { Alert, App, Checkbox, Form, Input, Select } from 'antd'
import { useMemo, useState } from 'react'
import { useWarehouseMutations, type WarehouseDetail } from '../../api/organization'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'

interface WarehouseFormValues {
  code: string
  name: string
  parentWarehouseId: string | null
  address: string
  keeper: string
  allowNegativeStock: boolean
  isActive: boolean
}

/**
 * Các kho nằm dưới một kho (con, cháu…) – không chọn được làm kho cha, nếu không cây kho thành vòng.
 * @param all Mọi kho đang thấy.
 * @param rootId Kho đang sửa.
 * @returns Id của kho đó và mọi kho bên dưới.
 */
function descendantsOf(all: WarehouseDetail[], rootId: string): Set<string> {
  const result = new Set([rootId])
  let added = true
  while (added) {
    added = false
    for (const w of all) {
      if (w.parentWarehouseId && result.has(w.parentWarehouseId) && !result.has(w.id)) {
        result.add(w.id)
        added = true
      }
    }
  }
  return result
}

const orNull = (value: string | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Ngăn kéo chi tiết kho. Thêm mới luôn là kho phụ (kho tổng tạo cùng công ty); mã và loại kho không đổi được.
 * Kho tổng không ngừng dùng được – kênh duy nhất của giao dịch liên công ty (design.md mục 4).
 * @param warehouse Kho đang mở; null = thêm kho phụ.
 * @param warehouses Danh sách kho đang thấy, để chọn kho cha.
 * @param onClose Đóng ngăn kéo.
 */
export function WarehouseDrawer({
  warehouse,
  warehouses,
  onClose,
}: {
  warehouse: WarehouseDetail | null
  warehouses: WarehouseDetail[]
  onClose: () => void
}) {
  const { message } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<WarehouseFormValues>()
  const mutations = useWarehouseMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const isNew = warehouse === null
  const isMain = warehouse?.type === 'Main'
  const editable = hasPermission(Permission.WarehouseManage)

  const parentOptions = useMemo(() => {
    const blocked = warehouse ? descendantsOf(warehouses, warehouse.id) : new Set<string>()
    return warehouses
      .filter((w) => w.isActive && !blocked.has(w.id))
      .map((w) => ({ value: w.id, label: `${w.code} – ${w.name}` }))
  }, [warehouse, warehouses])

  const initialValues: WarehouseFormValues = warehouse
    ? {
        code: warehouse.code,
        name: warehouse.name,
        parentWarehouseId: warehouse.parentWarehouseId,
        address: warehouse.address ?? '',
        keeper: warehouse.keeper ?? '',
        allowNegativeStock: warehouse.allowNegativeStock,
        isActive: warehouse.isActive,
      }
    : {
        code: '',
        name: '',
        parentWarehouseId: warehouses.find((w) => w.type === 'Main')?.id ?? null,
        address: '',
        keeper: '',
        allowNegativeStock: false,
        isActive: true,
      }

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
    const input = {
      name: values.name.trim(),
      parentWarehouseId: isMain ? null : values.parentWarehouseId,
      address: orNull(values.address),
      keeper: orNull(values.keeper),
      allowNegativeStock: values.allowNegativeStock,
    }
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
      title={isNew ? 'Thêm kho phụ' : `Kho: ${warehouse.code}`}
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
              {isMain && (
                <Alert
                  type="info"
                  showIcon
                  className="detail-section"
                  title="Kho tổng: mỗi công ty có đúng một kho tổng, dùng cho nhập/xuất bán nội bộ với công ty khác; không ngừng dùng được."
                />
              )}
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
                <Form.Item label="Loại kho">
                  <Input value={isMain ? 'Kho tổng' : 'Kho phụ'} disabled />
                </Form.Item>
              </div>
              <Form.Item label="Tên kho" name="name" rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên kho.' }, { max: 200 }]}>
                <Input />
              </Form.Item>
              {!isMain && (
                <Form.Item
                  label="Kho cha"
                  name="parentWarehouseId"
                  extra="Kho cha cùng công ty. Kho phụ chỉ chuyển kho trong công ty; bán cho công ty khác phải qua kho tổng."
                  rules={[{ required: true, message: 'Hãy chọn kho cha.' }]}
                >
                  <Select options={parentOptions} showSearch={{ optionFilterProp: 'label' }} />
                </Form.Item>
              )}
              <div className="detail-grid">
                <Form.Item label="Thủ kho" name="keeper" rules={[{ max: 200 }]}>
                  <Input />
                </Form.Item>
                <Form.Item label="Địa chỉ" name="address" rules={[{ max: 500 }]}>
                  <Input />
                </Form.Item>
              </div>
              <Form.Item
                name="allowNegativeStock"
                valuePropName="checked"
                extra="Mặc định không cho xuất quá số tồn. Chỉ bật khi kho cần xuất trước, nhập chứng từ sau."
              >
                <Checkbox>Cho phép tồn âm</Checkbox>
              </Form.Item>
              {!isNew && (
                <Form.Item name="isActive" valuePropName="checked" extra="Kho còn kho con đang dùng thì phải ngừng dùng kho con trước.">
                  <Checkbox disabled={isMain}>Còn sử dụng</Checkbox>
                </Form.Item>
              )}
            </>
          ),
        },
      ]}
    />
  )
}
