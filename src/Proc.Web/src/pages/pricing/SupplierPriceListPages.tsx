import { CheckOutlined, CloseOutlined, SaveOutlined, StopOutlined } from '@ant-design/icons'
import { Alert, App, Button, Checkbox, DatePicker, Form, Input, InputNumber, Select } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { useItemLookup } from '../../api/inventoryDocuments'
import { useSuppliers, vatOptions } from '../../api/masterData'
import {
  priceListStatusLabels,
  priceListStatusTone,
  usePriceList,
  usePriceListMutations,
  usePriceLists,
  type PriceList,
  type PriceListListItem,
  type SavePriceListInput,
} from '../../api/pricing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { DocumentForm } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { ListPage } from '../../components/ListPage/ListPage'
import { formatDateTime, formatMoney, vnNumberInput } from '../../utils/format'

const LIST_PATH = '/gia-ncc/bang-gia'

const listColumns: GridColumn<PriceListListItem>[] = [
  { key: 'no', title: 'Số bảng giá', width: 150, value: (r) => r.no },
  { key: 'title', title: 'Tên bảng giá', width: 220, value: (r) => r.title },
  { key: 'supplier', title: 'Nhà cung cấp', width: 230, value: (r) => `${r.supplierCode} – ${r.supplierName}`, groupable: true },
  { key: 'scope', title: 'Áp dụng', width: 110, value: (r) => (r.isGroupWide ? 'Toàn tập đoàn' : r.companyCode), groupable: true },
  { key: 'from', title: 'Hiệu lực từ', width: 95, type: 'date', value: (r) => r.validFrom },
  { key: 'to', title: 'Đến', width: 95, type: 'date', value: (r) => r.validTo },
  { key: 'lines', title: 'Số dòng', width: 70, type: 'quantity', value: (r) => r.lineCount },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 100,
    type: 'status',
    value: (r) => priceListStatusLabels[r.status],
    tone: (r) => priceListStatusTone[r.status],
    groupable: true,
  },
]

/** Màn hình Giá và NCC → Bảng giá NCC (design.md mục 8.1): bảng giá của công ty và bảng giá toàn tập đoàn (chỉ xem). */
export function PriceListListPage() {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const lists = usePriceLists()
  return (
    <ListPage
      title="Bảng giá nhà cung cấp"
      exportFileName="bang-gia-ncc"
      rows={lists.data ?? []}
      columns={listColumns}
      rowKey={(r) => r.id}
      loading={lists.isLoading}
      canCreate={hasPermission(Permission.PriceListManage)}
      onCreate={() => navigate(`${LIST_PATH}/moi`)}
      onOpenRow={(r) => navigate(`${LIST_PATH}/${r.id}`)}
    />
  )
}

interface LineValues {
  id: string | null
  itemId?: string
  uomId?: string
  minQty?: number | null
  unitPrice?: number | null
  discountPct?: number | null
  vatCode?: string | null
  note?: string
}

interface FormValues {
  supplierId?: string
  title?: string
  isGroupWide?: boolean
  validFrom: Dayjs
  validTo?: Dayjs | null
  deliveryTerms?: string
  note?: string
  lines: LineValues[]
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Trang bảng giá NCC trên khuôn DocumentForm: NCC, tên, hiệu lực, (tổng công ty) áp dụng toàn tập đoàn; dòng: mặt hàng, ĐVT, bậc SL,
 * đơn giá, % CK, VAT và giá quy về ĐVT cơ sở. Nháp → Đưa vào hiệu lực → Ngừng; bảng đã hiệu lực không sửa.
 */
export function SupplierPriceListPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const { hasPermission, workingCompany } = useAuth()
  const [form] = Form.useForm<FormValues>()
  const list = usePriceList(isNew ? null : id)
  const mutations = usePriceListMutations()
  const suppliers = useSuppliers({})
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const addRef = useRef<(() => void) | null>(null)
  const doc = list.data
  const readOnly = isNew ? !hasPermission(Permission.PriceListManage) : !doc?.actions.canEdit

  const watchedLines = Form.useWatch('lines', form) as LineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])

  useEffect(() => {
    if (!doc) return
    form.setFieldsValue({
      supplierId: doc.supplierId,
      title: doc.title,
      isGroupWide: doc.isGroupWide,
      validFrom: dayjs(doc.validFrom),
      validTo: doc.validTo ? dayjs(doc.validTo) : null,
      deliveryTerms: doc.deliveryTerms ?? '',
      note: doc.note ?? '',
      lines: doc.lines.map((l) => ({
        id: l.id, itemId: l.itemId, uomId: l.uomId, minQty: l.minQty, unitPrice: l.unitPrice, discountPct: l.discountPct, vatCode: l.vatCode, note: l.note ?? '',
      })),
    })
  }, [doc, form])

  const persist = async (): Promise<PriceList | null> => {
    let values: FormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng giá.')
      return null
    }
    setError(null)
    const input: SavePriceListInput = {
      supplierId: values.supplierId!,
      title: values.title?.trim() ?? '',
      isGroupWide: values.isGroupWide ?? false,
      validFrom: values.validFrom.format('YYYY-MM-DD'),
      validTo: values.validTo ? values.validTo.format('YYYY-MM-DD') : null,
      deliveryTerms: orNull(values.deliveryTerms),
      note: orNull(values.note),
      lines: (values.lines ?? []).map((l) => ({
        id: l.id ?? null, itemId: l.itemId!, uomId: l.uomId!, minQty: l.minQty ?? 0, unitPrice: l.unitPrice ?? 0, discountPct: l.discountPct ?? 0,
        vatCode: l.vatCode ?? null, note: orNull(l.note),
      })),
    }
    try {
      return isNew ? await mutations.create.mutateAsync(input) : await mutations.update.mutateAsync({ id, input })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được bảng giá.')
      return null
    }
  }

  const save = async () => {
    if (readOnly) return
    const saved = await persist()
    if (!saved) return
    message.success(`Đã lưu bảng giá ${saved.no}.`)
    if (isNew) navigate(`${LIST_PATH}/${saved.id}`, { replace: true })
  }

  const activate = () =>
    modal.confirm({
      title: 'Đưa bảng giá vào hiệu lực?',
      content: 'Đơn mua hàng của NCC này sẽ gợi ý giá từ bảng giá; bảng giá không sửa được nữa (muốn đổi giá thì ngừng và lập bảng mới).',
      okText: 'Đưa vào hiệu lực',
      cancelText: 'Không',
      onOk: async () => {
        const saved = await persist()
        if (!saved) return
        try {
          await mutations.activate.mutateAsync(saved.id)
          message.success(`Bảng giá ${saved.no} đã có hiệu lực.`)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không đưa vào hiệu lực được.')
        }
        if (isNew) navigate(`${LIST_PATH}/${saved.id}`, { replace: true })
      },
    })

  const deactivate = () =>
    doc &&
    modal.confirm({
      title: `Ngừng áp dụng bảng giá ${doc.no}?`,
      okText: 'Ngừng áp dụng',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        try {
          await mutations.deactivate.mutateAsync(doc.id)
          message.success(`Đã ngừng bảng giá ${doc.no}.`)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không ngừng được bảng giá.')
        }
      },
    })

  /** Giá quy về ĐVT cơ sở, trước VAT = đơn giá × (100 − %CK) / 100 / hệ số ĐVT (như server). */
  const baseNet = (line: LineValues | undefined) => {
    const item = line?.itemId ? itemsById.get(line.itemId) : undefined
    const factor = item?.uoms.find((u) => u.uomId === line?.uomId)?.factor ?? 1
    return ((line?.unitPrice ?? 0) * (100 - (line?.discountPct ?? 0))) / 100 / factor
  }

  const columns: LineColumn[] = [
    {
      key: 'item',
      title: 'Mặt hàng', required: true,
      width: 260,
      render: (field) => (
        <>
          <Form.Item name={[field.name, 'id']} hidden>
            <Input />
          </Form.Item>
          <Form.Item name={[field.name, 'itemId']} noStyle rules={[{ required: true, message: 'Chọn mặt hàng.' }]}>
            <Select
              variant="borderless"
              style={{ width: '100%' }}
              aria-label={`Mặt hàng dòng ${field.name + 1}`}
              showSearch={{ filterOption: false, onSearch: setSearch }}
              placeholder="Gõ mã hoặc tên hàng"
              options={(lookup.data ?? []).map((i) => ({ value: i.id, label: `${i.code} – ${i.name}` }))}
              onChange={(v: string) => form.setFieldValue(['lines', field.name, 'uomId'], itemsById.get(v)?.baseUomId)}
              popupMatchSelectWidth={420}
            />
          </Form.Item>
        </>
      ),
    },
    {
      key: 'uom',
      title: 'ĐVT', required: true,
      width: 75,
      render: (field) => {
        const item = lines[field.name]?.itemId ? itemsById.get(lines[field.name].itemId!) : undefined
        return (
          <Form.Item name={[field.name, 'uomId']} noStyle rules={[{ required: true, message: 'Chọn ĐVT.' }]}>
            <Select variant="borderless" style={{ width: '100%' }} aria-label={`ĐVT dòng ${field.name + 1}`}
              options={(item?.uoms ?? []).map((u) => ({ value: u.uomId, label: u.code }))} />
          </Form.Item>
        )
      },
    },
    {
      key: 'minQty',
      title: 'Từ SL',
      width: 85,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'minQty']} noStyle rules={[{ type: 'number', min: 0, message: '≥ 0.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={4} aria-label={`Từ SL dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'price',
      title: 'Đơn giá', required: true,
      width: 115,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'unitPrice']} noStyle rules={[{ required: true, type: 'number', min: 0, message: 'Nhập đơn giá.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} aria-label={`Đơn giá dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'discount',
      title: '% CK',
      width: 65,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'discountPct']} noStyle rules={[{ type: 'number', min: 0, max: 100, message: '0–100.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} max={100} aria-label={`Chiết khấu dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'vat',
      title: 'VAT',
      width: 75,
      render: (field) => (
        <Form.Item name={[field.name, 'vatCode']} noStyle>
          <Select variant="borderless" style={{ width: '100%' }} allowClear placeholder="Theo hàng" aria-label={`VAT dòng ${field.name + 1}`}
            options={vatOptions.map((o) => ({ value: o.value, label: o.value === 'KCT' ? 'KCT' : o.label }))} />
        </Form.Item>
      ),
    },
    {
      key: 'base',
      title: 'Giá quy đổi',
      width: 115,
      align: 'right',
      render: (field) => <span className="line-grid-readonly">{formatMoney(baseNet(lines[field.name]))}</span>,
    },
    {
      key: 'note',
      title: 'Ghi chú',
      width: 180,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle rules={[{ max: 500 }]}>
          <Input variant="borderless" aria-label={`Ghi chú dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? priceListStatusLabels[doc.status] : '', tone: doc ? priceListStatusTone[doc.status] : ('neutral' as const) }

  const actions = (
    <>
      {(isNew ? !readOnly : doc?.actions.canEdit) && (
        <Button type="primary" icon={<SaveOutlined />} onClick={save} loading={mutations.create.isPending || mutations.update.isPending}>
          Lưu nháp
        </Button>
      )}
      {(isNew ? !readOnly : doc?.actions.canActivate) && (
        <Button icon={<CheckOutlined />} onClick={activate} loading={mutations.activate.isPending}>
          Đưa vào hiệu lực
        </Button>
      )}
      {doc?.actions.canDeactivate && (
        <Button danger icon={<StopOutlined />} onClick={deactivate} loading={mutations.deactivate.isPending}>
          Ngừng áp dụng
        </Button>
      )}
      <Button type="text" icon={<CloseOutlined />} onClick={() => navigate(LIST_PATH)}>
        Đóng
      </Button>
    </>
  )

  return (
    <DocumentForm
      title="Bảng giá nhà cung cấp"
      number={isNew ? null : (doc?.no ?? '')}
      status={status}
      meta={doc && `Công ty lập: ${doc.companyCode} · Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
      loading={list.isLoading}
      error={error ?? (list.error ? list.error.message : null)}
      notice={doc?.actions.readOnlyReason && <Alert type="info" showIcon title={doc.actions.readOnlyReason} />}
      form={form}
      initialValues={{ validFrom: dayjs(), isGroupWide: false, lines: [] }}
      readOnly={readOnly}
      onSave={save}
      onAddLine={() => addRef.current?.()}
      actions={actions}
      header={
        <>
          <Form.Item label="Nhà cung cấp" name="supplierId" rules={[{ required: true, message: 'Chọn nhà cung cấp.' }]}>
            <Select
              showSearch={{ optionFilterProp: 'label' }}
              options={(suppliers.data?.items ?? [])
                .filter((s) => s.status !== 'Blacklisted' || s.id === doc?.supplierId)
                .map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))}
            />
          </Form.Item>
          <Form.Item label="Tên bảng giá" name="title" rules={[{ required: true, message: 'Nhập tên bảng giá.' }, { max: 200 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Hiệu lực từ" name="validFrom" rules={[{ required: true, message: 'Chọn ngày.' }]}>
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Hiệu lực đến" name="validTo" extra="Để trống = tới khi ngừng áp dụng.">
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Điều kiện giao hàng" name="deliveryTerms" rules={[{ max: 200 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]}>
            <Input />
          </Form.Item>
          {(workingCompany?.isHeadOffice || doc?.isGroupWide) && (
            <Form.Item label="Phạm vi" name="isGroupWide" valuePropName="checked" extra="Mọi công ty trong tập đoàn dùng được giá này.">
              <Checkbox>Áp dụng toàn tập đoàn</Checkbox>
            </Form.Item>
          )}
        </>
      }
      lines={
        <LineGrid name="lines" columns={columns} readOnly={readOnly} addRef={addRef}
          newLine={() => ({ id: null, minQty: 0, unitPrice: 0, discountPct: 0 })} />
      }
    />
  )
}
