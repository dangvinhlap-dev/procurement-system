import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { App, Button, Checkbox, Form, Input, Rate, Select } from 'antd'
import { useEffect, useState } from 'react'
import {
  supplierStatusLabels,
  useSupplier,
  useSupplierMutations,
  type SupplierBankAccount,
  type SupplierContact,
  type SupplierGroup,
  type SupplierStatus,
} from '../../api/masterData'
import { useCompanies } from '../../api/organization'
import { useAuth } from '../../auth/useAuth'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { SupplierCompanyTab, SupplierItemsTab } from './SupplierLinkTabs'

interface SupplierFormValues {
  code: string
  groupId: string | undefined
  name: string
  shortName: string
  isForeign: boolean
  taxCode: string
  address: string
  phone: string
  email: string
  website: string
  paymentTerms: string
  supplyScope: string
  rating: number
  status: SupplierStatus
  statusNote: string
  internalCompanyId: string | null
  contacts: SupplierContact[]
  bankAccounts: SupplierBankAccount[]
}

const emptyValues: SupplierFormValues = {
  code: '',
  groupId: undefined,
  name: '',
  shortName: '',
  isForeign: false,
  taxCode: '',
  address: '',
  phone: '',
  email: '',
  website: '',
  paymentTerms: '',
  supplyScope: '',
  rating: 0,
  status: 'Potential',
  statusNote: '',
  internalCompanyId: null,
  contacts: [],
  bankAccounts: [],
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/** Hàng ô nhập của một dòng bảng con (người liên hệ, tài khoản), cột cuối là nút xóa. */
const rowStyle = { display: 'grid', gridTemplateColumns: '1.4fr 1fr 1fr 1.2fr auto auto', gap: '0 8px', alignItems: 'start' } as const

/**
 * Ngăn kéo chi tiết NCC: tab Thông tin chung, Người liên hệ, Ngân hàng (lưu bằng nút Lưu của ngăn kéo), Sản phẩm/dịch vụ và
 * Áp dụng tại công ty (lưu riêng trong tab) (design.md mục 10.9). Tab Bảng giá, Đánh giá, Tài liệu thêm ở các lát sau.
 * @param supplierId Id NCC; null = thêm mới.
 * @param groups Nhóm NCC.
 * @param canManage Được sửa (ở tổng công ty, có quyền).
 * @param onClose Đóng.
 */
export function SupplierDrawer({
  supplierId,
  groups,
  canManage,
  onClose,
}: {
  supplierId: string | null
  groups: SupplierGroup[]
  canManage: boolean
  onClose: () => void
}) {
  const { message } = App.useApp()
  const { workingCompany } = useAuth()
  const [form] = Form.useForm<SupplierFormValues>()
  const supplier = useSupplier(supplierId)
  const companies = useCompanies()
  const mutations = useSupplierMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const isNew = supplierId === null
  const detail = supplier.data
  const editable = isNew ? canManage : detail?.canEdit === true
  const status = Form.useWatch('status', form)
  const isForeign = Form.useWatch('isForeign', form)
  const needsNote = status === 'Suspended' || status === 'Blacklisted'

  useEffect(() => {
    if (!detail) return
    form.setFieldsValue({
      code: detail.code,
      groupId: detail.groupId,
      name: detail.name,
      shortName: detail.shortName ?? '',
      isForeign: detail.isForeign,
      taxCode: detail.taxCode ?? '',
      address: detail.address ?? '',
      phone: detail.phone ?? '',
      email: detail.email ?? '',
      website: detail.website ?? '',
      paymentTerms: detail.paymentTerms ?? '',
      supplyScope: detail.supplyScope ?? '',
      rating: detail.rating ?? 0,
      status: detail.status,
      statusNote: detail.statusNote ?? '',
      internalCompanyId: detail.internalCompanyId,
      contacts: detail.contacts,
      bankAccounts: detail.bankAccounts,
    })
  }, [detail, form])

  const save = async () => {
    let values: SupplierFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn trường chưa hợp lệ – xem các ô báo đỏ ở từng tab.')
      return
    }
    setError(null)
    setSaving(true)
    const input = {
      // Mã do hệ thống cấp theo dải đánh số (design.md mục 5.11) – không gửi mã.
      code: null,
      groupId: values.groupId!,
      name: values.name.trim(),
      shortName: orNull(values.shortName),
      isForeign: values.isForeign,
      taxCode: orNull(values.taxCode),
      address: orNull(values.address),
      phone: orNull(values.phone),
      email: orNull(values.email),
      website: orNull(values.website),
      paymentTerms: orNull(values.paymentTerms),
      supplyScope: orNull(values.supplyScope),
      rating: values.rating ? values.rating : null,
      status: values.status,
      statusNote: orNull(values.statusNote),
      internalCompanyId: values.internalCompanyId ?? null,
      contacts: (values.contacts ?? []).map((c) => ({
        id: c.id ?? null,
        name: c.name.trim(),
        position: orNull(c.position),
        phone: orNull(c.phone),
        email: orNull(c.email),
        isPrimary: c.isPrimary ?? false,
      })),
      bankAccounts: (values.bankAccounts ?? []).map((b) => ({
        id: b.id ?? null,
        bankName: b.bankName.trim(),
        branch: orNull(b.branch),
        accountNumber: b.accountNumber.trim(),
        accountName: orNull(b.accountName),
        isDefault: b.isDefault ?? false,
      })),
    }
    try {
      if (isNew) {
        const created = await mutations.create.mutateAsync(input)
        message.success(`Đã thêm NCC ${created.code}.`)
      } else if (detail) {
        await mutations.update.mutateAsync({ id: detail.id, input })
        message.success(`Đã lưu NCC ${detail.code}.`)
      }
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được NCC.')
    } finally {
      setSaving(false)
    }
  }

  const groupOptions = groups
    .filter((g) => g.isActive || g.id === detail?.groupId)
    .map((g) => ({ value: g.id, label: `${g.code} – ${g.name}` }))
  // NCC nội bộ đại diện cho công ty thành viên (tổng công ty không bán nội bộ cho chính nó qua NCC).
  const companyOptions = (companies.data ?? [])
    .filter((c) => !c.isHeadOffice)
    .map((c) => ({ value: c.id, label: `${c.code} – ${c.name}` }))

  return (
    <DetailDrawer
      open
      title={isNew ? 'Thêm nhà cung cấp' : `Nhà cung cấp: ${detail?.code ?? ''}`}
      onClose={onClose}
      onSave={editable ? save : undefined}
      saving={saving}
      loading={supplier.isLoading}
      error={error ?? (supplier.error ? supplier.error.message : null)}
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
                  label="Mã NCC"
                  name="code"
                  extra={isNew ? 'Mã do hệ thống tự cấp khi lưu, theo dải ở Hệ thống → Đánh số danh mục.' : undefined}
                >
                  <Input disabled placeholder="(tự cấp khi lưu)" />
                </Form.Item>
                <Form.Item label="Nhóm NCC" name="groupId" rules={[{ required: true, message: 'Hãy chọn nhóm NCC.' }]}>
                  <Select options={groupOptions} />
                </Form.Item>
              </div>
              <Form.Item label="Tên nhà cung cấp" name="name" rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên NCC.' }, { max: 300 }]}>
                <Input />
              </Form.Item>
              <div className="detail-grid">
                <Form.Item label="Tên viết tắt" name="shortName" rules={[{ max: 100 }]}>
                  <Input />
                </Form.Item>
                <Form.Item
                  label={isForeign ? 'Mã thuế nước sở tại' : 'Mã số thuế'}
                  name="taxCode"
                  dependencies={['isForeign']}
                  rules={[
                    isForeign
                      ? { pattern: /^\s*[A-Za-z0-9][A-Za-z0-9._/-]{2,19}\s*$/, message: '3–20 ký tự chữ không dấu, số, - . _ /.' }
                      : { pattern: /^\s*\d{10}(-\d{3})?\s*$/, message: '10 chữ số, hoặc 10 chữ số + "-" + 3 chữ số.' },
                  ]}
                >
                  <Input autoComplete="off" />
                </Form.Item>
              </div>
              <Form.Item name="isForeign" valuePropName="checked">
                <Checkbox>NCC nước ngoài (không có mã số thuế Việt Nam)</Checkbox>
              </Form.Item>
              <Form.Item label="Địa chỉ" name="address" rules={[{ max: 500 }]}>
                <Input />
              </Form.Item>
              <div className="detail-grid">
                <Form.Item label="Điện thoại" name="phone" rules={[{ max: 50 }]}>
                  <Input />
                </Form.Item>
                <Form.Item label="Email" name="email" rules={[{ type: 'email', message: 'Email không hợp lệ.' }, { max: 256 }]}>
                  <Input />
                </Form.Item>
                <Form.Item label="Website" name="website" rules={[{ max: 200 }]}>
                  <Input />
                </Form.Item>
                <Form.Item label="Điều khoản thanh toán" name="paymentTerms" rules={[{ max: 200 }]}>
                  <Input placeholder="Ví dụ: chuyển khoản 30 ngày sau khi nhận hàng" />
                </Form.Item>
              </div>
              <Form.Item label="Ngành hàng cung cấp" name="supplyScope" rules={[{ max: 500 }]}>
                <Input />
              </Form.Item>
              <fieldset className="detail-section">
                <legend>Đánh giá và trạng thái</legend>
                <div className="detail-grid">
                  <Form.Item label="Xếp hạng" name="rating">
                    <Rate />
                  </Form.Item>
                  <Form.Item label="Trạng thái" name="status" rules={[{ required: true, message: 'Chọn trạng thái.' }]}>
                    <Select options={Object.entries(supplierStatusLabels).map(([value, label]) => ({ value, label }))} />
                  </Form.Item>
                </div>
                <Form.Item
                  label="Lý do"
                  name="statusNote"
                  dependencies={['status']}
                  extra="Bắt buộc khi tạm ngưng hoặc đưa vào danh sách đen."
                  rules={[{ required: needsNote, whitespace: true, message: 'Hãy ghi lý do.' }, { max: 500 }]}
                >
                  <Input />
                </Form.Item>
                <Form.Item
                  label="NCC nội bộ của công ty"
                  name="internalCompanyId"
                  extra="Chọn khi NCC này chính là một công ty thành viên (mua bán nội bộ ICS/ICR)."
                >
                  <Select options={companyOptions} allowClear placeholder="NCC bên ngoài" />
                </Form.Item>
              </fieldset>
            </>
          ),
        },
        {
          key: 'contacts',
          label: 'Người liên hệ',
          children: (
            <Form.List name="contacts">
              {(fields, { add, remove }) => (
                <>
                  {fields.map((field) => (
                    <div key={field.key} style={rowStyle}>
                      <Form.Item name={[field.name, 'id']} hidden>
                        <Input />
                      </Form.Item>
                      <Form.Item name={[field.name, 'name']} label="Họ tên" rules={[{ required: true, whitespace: true, message: 'Hãy nhập họ tên.' }, { max: 200 }]}>
                        <Input />
                      </Form.Item>
                      <Form.Item name={[field.name, 'position']} label="Chức vụ" rules={[{ max: 100 }]}>
                        <Input />
                      </Form.Item>
                      <Form.Item name={[field.name, 'phone']} label="Điện thoại" rules={[{ max: 50 }]}>
                        <Input />
                      </Form.Item>
                      <Form.Item name={[field.name, 'email']} label="Email" rules={[{ type: 'email', message: 'Email không hợp lệ.' }, { max: 256 }]}>
                        <Input />
                      </Form.Item>
                      <Form.Item name={[field.name, 'isPrimary']} valuePropName="checked" label="Chính">
                        <Checkbox aria-label="Người liên hệ chính" />
                      </Form.Item>
                      <Button type="text" icon={<DeleteOutlined />} aria-label="Xóa người liên hệ" onClick={() => remove(field.name)} style={{ marginTop: 22 }} />
                    </div>
                  ))}
                  <Button icon={<PlusOutlined />} onClick={() => add({ id: null, name: '', isPrimary: fields.length === 0 })}>
                    Thêm người liên hệ
                  </Button>
                </>
              )}
            </Form.List>
          ),
        },
        {
          key: 'banks',
          label: 'Ngân hàng',
          children: (
            <Form.List name="bankAccounts">
              {(fields, { add, remove }) => (
                <>
                  {fields.map((field) => (
                    <div key={field.key} style={rowStyle}>
                      <Form.Item name={[field.name, 'id']} hidden>
                        <Input />
                      </Form.Item>
                      <Form.Item name={[field.name, 'bankName']} label="Ngân hàng" rules={[{ required: true, whitespace: true, message: 'Hãy nhập ngân hàng.' }, { max: 200 }]}>
                        <Input />
                      </Form.Item>
                      <Form.Item name={[field.name, 'branch']} label="Chi nhánh" rules={[{ max: 200 }]}>
                        <Input />
                      </Form.Item>
                      <Form.Item
                        name={[field.name, 'accountNumber']}
                        label="Số tài khoản"
                        rules={[
                          { required: true, message: 'Hãy nhập số tài khoản.' },
                          { pattern: /^[A-Za-z0-9 ]{4,40}$/, message: '4–30 chữ số hoặc chữ không dấu.' },
                        ]}
                      >
                        <Input autoComplete="off" />
                      </Form.Item>
                      <Form.Item name={[field.name, 'accountName']} label="Chủ tài khoản" rules={[{ max: 200 }]}>
                        <Input />
                      </Form.Item>
                      <Form.Item name={[field.name, 'isDefault']} valuePropName="checked" label="Mặc định">
                        <Checkbox aria-label="Tài khoản mặc định" />
                      </Form.Item>
                      <Button type="text" icon={<DeleteOutlined />} aria-label="Xóa tài khoản" onClick={() => remove(field.name)} style={{ marginTop: 22 }} />
                    </div>
                  ))}
                  <Button icon={<PlusOutlined />} onClick={() => add({ id: null, bankName: '', accountNumber: '', isDefault: fields.length === 0 })}>
                    Thêm tài khoản ngân hàng
                  </Button>
                </>
              )}
            </Form.List>
          ),
        },
        // Hai tab dưới cần NCC đã lưu (có Id); thêm mới thì lưu NCC trước rồi mở lại.
        ...(supplierId
          ? [
              { key: 'items', label: 'Sản phẩm/dịch vụ', children: <SupplierItemsTab supplierId={supplierId} canManage={editable} /> },
              { key: 'company', label: `Áp dụng tại ${workingCompany?.code ?? ''}`, children: <SupplierCompanyTab supplierId={supplierId} /> },
            ]
          : []),
      ]}
    />
  )
}
