import { Alert, App, Checkbox, DatePicker, Form, Input } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useState } from 'react'
import { useCompany, useCompanyMutations, type CompanyInfoInput } from '../../api/organization'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'

interface CompanyFormValues {
  code: string
  name: string
  shortName: string
  taxCode: string
  address: string
  representative: string
  startDate: Dayjs | null
  isActive: boolean
  mainWarehouseCode: string
  mainWarehouseName: string
}

const emptyValues: CompanyFormValues = {
  code: '',
  name: '',
  shortName: '',
  taxCode: '',
  address: '',
  representative: '',
  startDate: null,
  isActive: true,
  mainWarehouseCode: 'KT',
  mainWarehouseName: '',
}

/** Chuỗi rỗng gửi lên là null, để API lưu "chưa có" thay vì chuỗi trắng. */
const orNull = (value: string | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Lấy thông tin chung từ form theo đúng dạng API nhận.
 * @param v Giá trị form.
 * @returns Thông tin công ty; ngày dạng yyyy-MM-dd.
 */
function toInfo(v: CompanyFormValues): CompanyInfoInput {
  return {
    name: v.name.trim(),
    shortName: orNull(v.shortName),
    taxCode: orNull(v.taxCode),
    address: orNull(v.address),
    representative: orNull(v.representative),
    startDate: v.startDate ? v.startDate.format('YYYY-MM-DD') : null,
  }
}

/**
 * Ngăn kéo chi tiết công ty. Thêm công ty thì khai báo luôn kho tổng (mỗi công ty đúng một kho tổng – design.md mục 4);
 * mã công ty không đổi được sau khi tạo vì nằm trong số chứng từ.
 * @param companyId Id công ty; null = thêm mới.
 * @param onClose Đóng ngăn kéo.
 */
export function CompanyDrawer({ companyId, onClose }: { companyId: string | null; onClose: () => void }) {
  const { message } = App.useApp()
  const { workingCompany, hasPermission } = useAuth()
  const [form] = Form.useForm<CompanyFormValues>()
  const company = useCompany(companyId)
  const mutations = useCompanyMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const isNew = companyId === null
  const detail = company.data
  const editable = isNew ? workingCompany?.isHeadOffice === true && hasPermission(Permission.CompanyManage) : detail?.canEdit === true

  useEffect(() => {
    if (!detail) return
    form.setFieldsValue({
      code: detail.code,
      name: detail.name,
      shortName: detail.shortName ?? '',
      taxCode: detail.taxCode ?? '',
      address: detail.address ?? '',
      representative: detail.representative ?? '',
      startDate: detail.startDate ? dayjs(detail.startDate) : null,
      isActive: detail.isActive,
    })
  }, [detail, form])

  const save = async () => {
    let values: CompanyFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn trường chưa hợp lệ – xem các ô báo đỏ.')
      return
    }
    setError(null)
    setSaving(true)
    try {
      if (isNew) {
        await mutations.create.mutateAsync({
          ...toInfo(values),
          code: values.code,
          mainWarehouseCode: values.mainWarehouseCode,
          mainWarehouseName: values.mainWarehouseName.trim(),
        })
        message.success(`Đã thêm công ty ${values.code.toUpperCase()}. Bạn đã được gán vào công ty này.`)
      } else if (detail) {
        await mutations.update.mutateAsync({ id: detail.id, input: { ...toInfo(values), isActive: values.isActive } })
        message.success(`Đã lưu công ty ${detail.code}.`)
      }
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được công ty.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <DetailDrawer
      open
      title={isNew ? 'Thêm công ty' : `Công ty: ${detail?.code ?? ''}`}
      onClose={onClose}
      onSave={editable ? save : undefined}
      saving={saving}
      loading={company.isLoading}
      error={error ?? (company.error ? company.error.message : null)}
      form={form}
      initialValues={emptyValues}
      readOnly={!editable}
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <>
              {detail?.isHeadOffice && (
                <Alert type="info" showIcon className="detail-section" title="Tổng công ty: luôn đang dùng, không ngừng dùng được." />
              )}
              <div className="detail-grid">
                <Form.Item
                  label="Mã công ty"
                  name="code"
                  extra={isNew ? '2–10 chữ không dấu hoặc số, ví dụ BHA. Nằm trong số chứng từ nên không đổi được.' : undefined}
                  rules={[
                    { required: true, message: 'Hãy nhập mã công ty.' },
                    { pattern: /^[A-Za-z0-9]{2,10}$/, message: 'Mã gồm 2–10 chữ không dấu hoặc số.' },
                  ]}
                  normalize={(v: string) => v.toUpperCase()}
                >
                  <Input disabled={!isNew} autoFocus={isNew} autoComplete="off" />
                </Form.Item>
                <Form.Item label="Tên viết tắt" name="shortName" rules={[{ max: 100 }]}>
                  <Input />
                </Form.Item>
              </div>
              <Form.Item label="Tên công ty" name="name" rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên công ty.' }, { max: 300 }]}>
                <Input />
              </Form.Item>
              <div className="detail-grid">
                <Form.Item
                  label="Mã số thuế"
                  name="taxCode"
                  rules={[{ pattern: /^\s*\d{10}(-\d{3})?\s*$/, message: '10 chữ số, hoặc 10 chữ số + "-" + 3 chữ số.' }]}
                >
                  <Input autoComplete="off" />
                </Form.Item>
                <Form.Item label="Người đại diện" name="representative" rules={[{ max: 200 }]}>
                  <Input />
                </Form.Item>
              </div>
              <Form.Item label="Địa chỉ" name="address" rules={[{ max: 500 }]}>
                <Input />
              </Form.Item>
              <div className="detail-grid">
                <Form.Item label="Ngày bắt đầu dùng hệ thống" name="startDate">
                  <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
                </Form.Item>
                <Form.Item label="Tiền tệ hạch toán">
                  <Input value={detail?.accountingCurrency ?? 'VND'} disabled />
                </Form.Item>
              </div>
              {isNew && (
                <fieldset className="detail-section">
                  <legend>Kho tổng</legend>
                  <div className="detail-hint" style={{ marginBottom: 6 }}>
                    Mỗi công ty có đúng một kho tổng – nơi duy nhất nhập/xuất bán nội bộ với công ty khác.
                  </div>
                  <div className="detail-grid">
                    <Form.Item
                      label="Mã kho tổng"
                      name="mainWarehouseCode"
                      rules={[
                        { required: true, message: 'Hãy nhập mã kho tổng.' },
                        { pattern: /^[A-Za-z0-9]{2,20}$/, message: 'Mã gồm 2–20 chữ không dấu hoặc số.' },
                      ]}
                      normalize={(v: string) => v.toUpperCase()}
                    >
                      <Input autoComplete="off" />
                    </Form.Item>
                    <Form.Item
                      label="Tên kho tổng"
                      name="mainWarehouseName"
                      rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên kho tổng.' }, { max: 200 }]}
                    >
                      <Input />
                    </Form.Item>
                  </div>
                </fieldset>
              )}
              {!isNew && (
                <Form.Item name="isActive" valuePropName="checked" extra="Ngừng dùng thì không ai chọn được công ty này để làm việc nữa.">
                  <Checkbox disabled={detail?.isHeadOffice}>Còn sử dụng</Checkbox>
                </Form.Item>
              )}
            </>
          ),
        },
      ]}
    />
  )
}
