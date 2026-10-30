import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { App, Button, Checkbox, Form, Input, InputNumber, Select, TreeSelect } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import {
  itemTypeLabels,
  useItem,
  useItemMutations,
  useUoms,
  vatOptions,
  type ItemCategory,
  type ItemInput,
  type ItemType,
} from '../../api/masterData'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { buildCategoryTree } from './categoryTree'
import { ItemSuppliersTab } from './SupplierLinkTabs'

interface ItemFormValues {
  code: string
  name: string
  englishName: string
  type: ItemType
  categoryId: string | undefined
  baseUomId: string | undefined
  specification: string
  brand: string
  origin: string
  barcode: string
  vatCode: string | null
  technicalSpecs: string
  trackLot: boolean
  trackExpiry: boolean
  minStock: number | null
  maxStock: number | null
  conversions: { uomId: string | undefined; factor: number | null }[]
  isActive: boolean
}

const emptyValues: ItemFormValues = {
  code: '',
  name: '',
  englishName: '',
  type: 'Inventory',
  categoryId: undefined,
  baseUomId: undefined,
  specification: '',
  brand: '',
  origin: '',
  barcode: '',
  vatCode: '10',
  technicalSpecs: '',
  trackLot: false,
  trackExpiry: false,
  minStock: null,
  maxStock: null,
  conversions: [],
  isActive: true,
}

const orNull = (value: string | undefined) => (value?.trim() ? value.trim() : null)

/** Ô số kiểu Việt Nam: dấu phẩy thập phân (CLAUDE.md mục 5). */
const numberProps = { decimalSeparator: ',', style: { width: '100%' } } as const

/**
 * Ngăn kéo chi tiết hàng hóa: tab Thông tin chung, Quản lý kho, Quy đổi ĐVT, Thông số kỹ thuật, Nhà cung cấp (chỉ xem).
 * Chỉ hàng tồn kho mới quản lý lô/hạn dùng/tồn tối thiểu (design.md mục 5.2); đổi loại khác thì các ô đó bị khóa và xóa.
 * @param itemId Id hàng; null = thêm mới.
 * @param categories Nhóm hàng trong phạm vi.
 * @param canManage Được sửa (ở tổng công ty, có quyền).
 * @param onClose Đóng.
 */
export function ItemDrawer({
  itemId,
  categories,
  canManage,
  onClose,
}: {
  itemId: string | null
  categories: ItemCategory[]
  canManage: boolean
  onClose: () => void
}) {
  const { message } = App.useApp()
  const [form] = Form.useForm<ItemFormValues>()
  const item = useItem(itemId)
  const uoms = useUoms()
  const mutations = useItemMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const isNew = itemId === null
  const detail = item.data
  const editable = isNew ? canManage : detail?.canEdit === true
  const type = Form.useWatch('type', form)
  const baseUomId = Form.useWatch('baseUomId', form)
  const isInventory = (type ?? 'Inventory') === 'Inventory'
  const tree = useMemo(() => buildCategoryTree(categories, { activeOnly: true }), [categories])
  // ĐVT đã ngừng chỉ còn hiện nếu mặt hàng đang dùng nó, để không chọn mới được.
  const uomOptions = (uoms.data ?? [])
    .filter((u) => u.isActive || u.id === detail?.baseUomId || detail?.conversions.some((c) => c.uomId === u.id))
    .map((u) => ({ value: u.id, label: `${u.code} – ${u.name}` }))
  const baseUomCode = uoms.data?.find((u) => u.id === baseUomId)?.code ?? 'ĐVT cơ sở'

  useEffect(() => {
    if (!detail) return
    form.setFieldsValue({
      code: detail.code,
      name: detail.name,
      englishName: detail.englishName ?? '',
      type: detail.type,
      categoryId: detail.categoryId,
      baseUomId: detail.baseUomId,
      specification: detail.specification ?? '',
      brand: detail.brand ?? '',
      origin: detail.origin ?? '',
      barcode: detail.barcode ?? '',
      vatCode: detail.vatCode,
      technicalSpecs: detail.technicalSpecs ?? '',
      trackLot: detail.trackLot,
      trackExpiry: detail.trackExpiry,
      minStock: detail.minStock,
      maxStock: detail.maxStock,
      conversions: detail.conversions.map((c) => ({ uomId: c.uomId, factor: c.factor })),
      isActive: detail.isActive,
    })
  }, [detail, form])

  // Hàng không phải tồn kho thì không quản lý lô/hạn dùng/tồn tối thiểu – xóa luôn để khỏi bị API từ chối khi lưu.
  useEffect(() => {
    if (type && type !== 'Inventory') form.setFieldsValue({ trackLot: false, trackExpiry: false, minStock: null, maxStock: null })
  }, [type, form])

  const save = async () => {
    let values: ItemFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn trường chưa hợp lệ – xem các ô báo đỏ ở từng tab.')
      return
    }
    setError(null)
    setSaving(true)
    const input: ItemInput = {
      name: values.name.trim(),
      englishName: orNull(values.englishName),
      type: values.type,
      categoryId: values.categoryId!,
      baseUomId: values.baseUomId!,
      specification: orNull(values.specification),
      brand: orNull(values.brand),
      origin: orNull(values.origin),
      barcode: orNull(values.barcode),
      vatCode: values.vatCode ?? null,
      technicalSpecs: orNull(values.technicalSpecs),
      trackLot: values.trackLot,
      trackExpiry: values.trackExpiry,
      minStock: values.minStock ?? null,
      maxStock: values.maxStock ?? null,
      conversions: (values.conversions ?? []).map((c) => ({ uomId: c.uomId!, factor: c.factor! })),
    }
    try {
      if (isNew) {
        await mutations.create.mutateAsync({ ...input, code: values.code })
        message.success(`Đã thêm hàng ${values.code.toUpperCase()}.`)
      } else if (detail) {
        await mutations.update.mutateAsync({ id: detail.id, input: { ...input, isActive: values.isActive } })
        message.success(`Đã lưu hàng ${detail.code}.`)
      }
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được hàng hóa.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <DetailDrawer
      open
      title={isNew ? 'Thêm hàng hóa' : `Hàng hóa: ${detail?.code ?? ''}`}
      onClose={onClose}
      onSave={editable ? save : undefined}
      saving={saving}
      loading={item.isLoading}
      error={error ?? (item.error ? item.error.message : null)}
      form={form}
      initialValues={emptyValues}
      readOnly={!editable}
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <>
              <div className="detail-grid">
                <Form.Item
                  label="Mã hàng"
                  name="code"
                  extra={isNew ? '2–30 ký tự chữ không dấu, số, - . _ /, ví dụ VT-0001. Không đổi được sau khi tạo.' : undefined}
                  rules={[
                    { required: true, message: 'Hãy nhập mã hàng.' },
                    { pattern: /^[A-Za-z0-9][A-Za-z0-9._/-]{1,29}$/, message: 'Mã gồm 2–30 ký tự chữ không dấu, số, - . _ /.' },
                  ]}
                  normalize={(v: string) => v.toUpperCase()}
                >
                  <Input disabled={!isNew} autoFocus={isNew} autoComplete="off" />
                </Form.Item>
                <Form.Item label="Loại hàng" name="type" extra="Chỉ hàng tồn kho mới đi qua kho.">
                  <Select options={Object.entries(itemTypeLabels).map(([value, label]) => ({ value, label }))} />
                </Form.Item>
              </div>
              <Form.Item label="Tên hàng" name="name" rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên hàng.' }, { max: 300 }]}>
                <Input />
              </Form.Item>
              <Form.Item label="Tên tiếng Anh" name="englishName" rules={[{ max: 300 }]}>
                <Input />
              </Form.Item>
              <div className="detail-grid">
                <Form.Item label="Nhóm hàng" name="categoryId" rules={[{ required: true, message: 'Hãy chọn nhóm hàng.' }]}>
                  <TreeSelect treeData={tree} showSearch treeNodeFilterProp="title" treeDefaultExpandAll />
                </Form.Item>
                <Form.Item
                  label="ĐVT cơ sở"
                  name="baseUomId"
                  extra="Đơn vị lưu tồn kho và tính giá vốn."
                  rules={[{ required: true, message: 'Hãy chọn ĐVT cơ sở.' }]}
                >
                  <Select options={uomOptions} showSearch={{ optionFilterProp: 'label' }} />
                </Form.Item>
                <Form.Item label="Quy cách" name="specification" rules={[{ max: 500 }]}>
                  <Input />
                </Form.Item>
                <Form.Item label="Thuế suất GTGT mặc định" name="vatCode">
                  <Select options={vatOptions} allowClear placeholder="Chưa khai báo" />
                </Form.Item>
                <Form.Item label="Thương hiệu" name="brand" rules={[{ max: 100 }]}>
                  <Input />
                </Form.Item>
                <Form.Item label="Xuất xứ" name="origin" rules={[{ max: 100 }]}>
                  <Input />
                </Form.Item>
                <Form.Item label="Mã vạch" name="barcode" rules={[{ max: 50 }]}>
                  <Input autoComplete="off" />
                </Form.Item>
              </div>
              {!isNew && (
                <Form.Item name="isActive" valuePropName="checked" extra="Ngừng dùng thì không chọn được trên chứng từ mới; chứng từ cũ vẫn giữ.">
                  <Checkbox>Còn sử dụng</Checkbox>
                </Form.Item>
              )}
            </>
          ),
        },
        {
          key: 'stock',
          label: 'Quản lý kho',
          children: (
            <>
              {!isInventory && <div className="detail-hint" style={{ marginBottom: 8 }}>Chỉ hàng tồn kho mới quản lý lô, hạn dùng và tồn tối thiểu/tối đa.</div>}
              <Form.Item name="trackLot" valuePropName="checked">
                <Checkbox disabled={!isInventory}>Quản lý theo lô</Checkbox>
              </Form.Item>
              <Form.Item name="trackExpiry" valuePropName="checked">
                <Checkbox disabled={!isInventory}>Quản lý hạn dùng</Checkbox>
              </Form.Item>
              <div className="detail-grid">
                <Form.Item label={`Tồn tối thiểu (${baseUomCode})`} name="minStock">
                  <InputNumber {...numberProps} min={0} precision={4} disabled={!isInventory} />
                </Form.Item>
                <Form.Item
                  label={`Tồn tối đa (${baseUomCode})`}
                  name="maxStock"
                  dependencies={['minStock']}
                  rules={[
                    ({ getFieldValue }) => ({
                      validator: (_, v: number | null) =>
                        v == null || getFieldValue('minStock') == null || v >= getFieldValue('minStock')
                          ? Promise.resolve()
                          : Promise.reject(new Error('Tồn tối đa phải ≥ tồn tối thiểu.')),
                    }),
                  ]}
                >
                  <InputNumber {...numberProps} min={0} precision={4} disabled={!isInventory} />
                </Form.Item>
              </div>
            </>
          ),
        },
        {
          key: 'uom',
          label: 'Quy đổi ĐVT',
          children: (
            <>
              <div className="detail-hint" style={{ marginBottom: 8 }}>
                Mỗi dòng: 1 ĐVT quy đổi = hệ số × {baseUomCode}. Ví dụ ĐVT cơ sở Hộp, thêm Thùng hệ số 24 nghĩa là 1 thùng = 24 hộp.
              </div>
              <Form.List name="conversions">
                {(fields, { add, remove }) => (
                  <>
                    {fields.map((field) => (
                      <div key={field.key} className="detail-grid" style={{ gridTemplateColumns: '1fr 1fr auto' }}>
                        <Form.Item
                          name={[field.name, 'uomId']}
                          label="ĐVT quy đổi"
                          rules={[
                            { required: true, message: 'Hãy chọn ĐVT.' },
                            {
                              validator: (_, v: string) =>
                                v && v === form.getFieldValue('baseUomId')
                                  ? Promise.reject(new Error('Trùng ĐVT cơ sở.'))
                                  : Promise.resolve(),
                            },
                          ]}
                        >
                          <Select options={uomOptions} showSearch={{ optionFilterProp: 'label' }} />
                        </Form.Item>
                        <Form.Item
                          name={[field.name, 'factor']}
                          label={`Hệ số (số ${baseUomCode})`}
                          rules={[
                            { required: true, message: 'Hãy nhập hệ số.' },
                            { type: 'number', min: 0.000001, message: 'Hệ số phải lớn hơn 0.' },
                          ]}
                        >
                          <InputNumber {...numberProps} precision={6} />
                        </Form.Item>
                        <Button
                          type="text"
                          icon={<DeleteOutlined />}
                          aria-label="Xóa dòng quy đổi"
                          onClick={() => remove(field.name)}
                          style={{ marginTop: 22 }}
                        />
                      </div>
                    ))}
                    <Button icon={<PlusOutlined />} onClick={() => add({ uomId: undefined, factor: null })}>
                      Thêm ĐVT quy đổi
                    </Button>
                  </>
                )}
              </Form.List>
            </>
          ),
        },
        {
          key: 'specs',
          label: 'Thông số kỹ thuật',
          children: (
            <Form.Item label="Thông số kỹ thuật" name="technicalSpecs" rules={[{ max: 4000 }]}>
              <Input.TextArea rows={10} />
            </Form.Item>
          ),
        },
        ...(itemId ? [{ key: 'suppliers', label: 'Nhà cung cấp', children: <ItemSuppliersTab itemId={itemId} /> }] : []),
      ]}
    />
  )
}
