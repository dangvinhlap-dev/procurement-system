import { App, DatePicker, Form, Input, InputNumber, Select } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useState } from 'react'
import { useGroupCompanies, useInternalPriceMutations, useInternalPrices, type InternalPrice } from '../../api/intercompany'
import { useItemLookup } from '../../api/inventoryDocuments'
import { vatOptions } from '../../api/masterData'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { ListPage } from '../../components/ListPage/ListPage'
import { vnNumberInput } from '../../utils/format'

const columns: GridColumn<InternalPrice>[] = [
  { key: 'code', title: 'Mã hàng', width: 130, value: (r) => r.itemCode },
  { key: 'name', title: 'Tên hàng', width: 240, value: (r) => r.itemName },
  { key: 'buyer', title: 'Công ty mua', width: 130, value: (r) => r.buyerCompanyCode ?? 'Mọi công ty', groupable: true },
  { key: 'uom', title: 'ĐVT', width: 60, value: (r) => r.uomCode },
  { key: 'price', title: 'Giá bán nội bộ', width: 130, type: 'money', value: (r) => r.unitPrice },
  { key: 'vat', title: 'VAT', width: 70, value: (r) => (r.vatCode ? (r.vatCode === 'KCT' ? 'KCT' : `${r.vatCode}%`) : 'Theo hàng') },
  { key: 'from', title: 'Hiệu lực từ', width: 100, type: 'date', value: (r) => r.validFrom },
  { key: 'to', title: 'Đến', width: 100, type: 'date', value: (r) => r.validTo },
  { key: 'note', title: 'Ghi chú', width: 200, value: (r) => r.note },
]

/**
 * Màn hình Giá và NCC → Bảng giá nội bộ (design.md mục 4, 6.6): giá công ty đang làm việc bán cho các công ty khác trong tập đoàn,
 * theo ĐVT cơ sở, có hiệu lực; công ty mua trống = mọi công ty. Phiếu xuất bán nội bộ lấy giá từ đây.
 */
export function InternalPriceListPage() {
  const { workingCompany, hasPermission } = useAuth()
  const prices = useInternalPrices()
  const canManage = hasPermission(Permission.PriceListManage)
  // undefined = đóng; null = thêm mới; còn lại = dòng đang mở.
  const [open, setOpen] = useState<InternalPrice | null | undefined>(undefined)

  return (
    <>
      <ListPage
        title={`Bảng giá nội bộ – ${workingCompany?.code ?? ''} bán`}
        exportFileName="bang-gia-noi-bo"
        rows={prices.data ?? []}
        columns={columns}
        rowKey={(r) => r.id}
        loading={prices.isLoading}
        canCreate={canManage}
        onCreate={() => setOpen(null)}
        onOpenRow={setOpen}
      />
      {open !== undefined && <InternalPriceDrawer price={open} canManage={canManage} onClose={() => setOpen(undefined)} />}
    </>
  )
}

interface PriceFormValues {
  buyerCompanyId: string
  itemId?: string
  unitPrice?: number
  vatCode?: string
  validFrom: Dayjs
  validTo?: Dayjs | null
  note?: string
}

/**
 * Ngăn kéo thêm/sửa giá nội bộ. Sửa thì công ty mua, mặt hàng giữ nguyên (muốn đổi thì thêm dòng mới).
 * @param price Dòng đang mở; null = thêm mới.
 * @param canManage Có quyền sửa.
 * @param onClose Đóng.
 */
function InternalPriceDrawer({ price, canManage, onClose }: { price: InternalPrice | null; canManage: boolean; onClose: () => void }) {
  const { message } = App.useApp()
  const [form] = Form.useForm<PriceFormValues>()
  const mutations = useInternalPriceMutations()
  const companies = useGroupCompanies()
  const [search, setSearch] = useState('')
  const lookup = useItemLookup(search, price ? [price.itemId] : [])
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const save = async () => {
    const values = await form.validateFields().catch(() => null)
    if (!values) return
    setError(null)
    setSaving(true)
    const input = {
      buyerCompanyId: values.buyerCompanyId === '' ? null : values.buyerCompanyId,
      itemId: values.itemId!,
      unitPrice: values.unitPrice ?? 0,
      vatCode: values.vatCode ? values.vatCode : null,
      validFrom: values.validFrom.format('YYYY-MM-DD'),
      validTo: values.validTo ? values.validTo.format('YYYY-MM-DD') : null,
      note: values.note?.trim() ? values.note.trim() : null,
    }
    try {
      if (price) await mutations.update.mutateAsync({ id: price.id, input })
      else await mutations.create.mutateAsync(input)
      message.success('Đã lưu giá nội bộ.')
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được giá nội bộ.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <DetailDrawer
      open
      title={price ? `Giá nội bộ: ${price.itemCode}` : 'Thêm giá nội bộ'}
      onClose={onClose}
      onSave={canManage ? save : undefined}
      saving={saving}
      error={error}
      form={form}
      readOnly={!canManage}
      initialValues={
        price
          ? {
              buyerCompanyId: price.buyerCompanyId ?? '', itemId: price.itemId, unitPrice: price.unitPrice, vatCode: price.vatCode ?? '',
              validFrom: dayjs(price.validFrom), validTo: price.validTo ? dayjs(price.validTo) : null, note: price.note ?? '',
            }
          : { buyerCompanyId: '', vatCode: '', validFrom: dayjs() }
      }
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <div className="detail-grid">
              <Form.Item label="Mặt hàng" name="itemId" rules={[{ required: true, message: 'Chọn mặt hàng.' }]}>
                <Select
                  disabled={price !== null}
                  showSearch={{ filterOption: false, onSearch: setSearch }}
                  placeholder="Gõ mã hoặc tên hàng"
                  options={(lookup.data ?? []).map((i) => ({ value: i.id, label: `${i.code} – ${i.name}` }))}
                />
              </Form.Item>
              <Form.Item label="Công ty mua" name="buyerCompanyId" extra="Giá riêng cho công ty mua được ưu tiên hơn giá chung.">
                <Select
                  disabled={price !== null}
                  options={[{ value: '', label: 'Mọi công ty' }, ...(companies.data ?? []).map((c) => ({ value: c.id, label: `${c.code} – ${c.name}` }))]}
                />
              </Form.Item>
              <Form.Item label="Giá bán nội bộ (theo ĐVT cơ sở, chưa VAT)" name="unitPrice" rules={[{ required: true, type: 'number', min: 0, message: 'Nhập giá ≥ 0.' }]}>
                <InputNumber {...vnNumberInput} style={{ width: '100%' }} min={0} />
              </Form.Item>
              <Form.Item label="Thuế suất" name="vatCode">
                <Select options={[{ value: '', label: 'Theo mặt hàng' }, ...vatOptions]} />
              </Form.Item>
              <Form.Item label="Hiệu lực từ" name="validFrom" rules={[{ required: true, message: 'Chọn ngày.' }]}>
                <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
              </Form.Item>
              <Form.Item label="Hiệu lực đến" name="validTo" extra="Trống = tới khi có giá mới.">
                <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
              </Form.Item>
              <Form.Item label="Ghi chú" name="note" rules={[{ max: 500 }]}>
                <Input />
              </Form.Item>
            </div>
          ),
        },
      ]}
    />
  )
}
