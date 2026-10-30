import { CheckCircleOutlined, CloseOutlined, RollbackOutlined, SaveOutlined, StopOutlined } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Form, Input, InputNumber, Select } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import {
  documentStatusLabels,
  useGoodsReceiptMutations,
  useInventoryDocument,
  useItemLookup,
  useSupplierLookup,
  type InventoryDocument,
  type ItemLookup,
  type SaveReceiptInput,
} from '../../api/inventoryDocuments'
import { vatOptions } from '../../api/masterData'
import { useWarehouses } from '../../api/organization'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DocumentForm, DocumentTotal } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { formatDateTime, formatMoney, formatQuantity, vnNumberInput } from '../../utils/format'
import { documentStatusTone } from './documentStatus'

interface LineValues {
  id: string | null
  itemId?: string
  uomId?: string
  qty?: number | null
  unitPrice?: number | null
  vatCode?: string | null
  lotNo?: string
  expiryDate?: Dayjs | null
  note?: string
}

interface ReceiptFormValues {
  date: Dayjs
  warehouseId?: string
  supplierId?: string
  invoiceNo?: string
  invoiceSeries?: string
  invoiceDate?: Dayjs | null
  deliveryNoteNo?: string
  deliverer?: string
  note?: string
  lines: LineValues[]
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/** Ô số trong lưới dòng: không viền, căn phải, kiểu số Việt Nam. */
const cellNumber = { ...vnNumberInput, variant: 'borderless', style: { width: '100%' } } as const

/**
 * Tính một dòng giống server (InventoryDocumentLine.Set) để hiện ngay khi gõ: số lượng cơ sở, thành tiền, VAT.
 * Ví dụ 2 thùng × 24, đơn giá 480.000, VAT 10% → 48; 960.000; 96.000.
 * @param line Giá trị dòng.
 * @param item Mặt hàng (để lấy hệ số ĐVT).
 * @returns Số lượng cơ sở, thành tiền, tiền VAT (đã làm tròn như server).
 */
function computeLine(line: LineValues | undefined, item: ItemLookup | undefined) {
  const qty = line?.qty ?? 0
  const factor = item?.uoms.find((u) => u.uomId === line?.uomId)?.factor ?? 1
  const amount = Math.round(qty * (line?.unitPrice ?? 0) * 100) / 100
  const rate = line?.vatCode && line.vatCode !== 'KCT' ? Number(line.vatCode) : 0
  return { baseQty: Math.round(qty * factor * 10000) / 10000, amount, vat: Math.round(amount * rate) / 100 }
}

/**
 * Trang phiếu nhập mua từ NCC (design.md mục 10.4) trên khuôn DocumentForm: lập mới (/kho/grn/moi) hoặc mở phiếu đã có.
 * Phiếu nháp sửa được; số phiếu cấp khi lưu lần đầu. Ghi sổ (sinh sổ kho) thêm ở lát sau.
 */
export function GoodsReceiptPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<ReceiptFormValues>()
  const document = useInventoryDocument(isNew ? null : id)
  const mutations = useGoodsReceiptMutations()
  const warehouses = useWarehouses()
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const addRef = useRef<(() => void) | null>(null)

  const doc = document.data
  const readOnly = isNew ? !hasPermission(Permission.InventoryDocumentCreate) : !doc?.canEdit
  const watchedLines = Form.useWatch('lines', form) as LineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])
  const suppliers = useSupplierLookup(doc?.supplierId ? [doc.supplierId] : [])

  // Lập mới: ngày hôm nay, kho tổng (nếu trong phạm vi) làm kho nhập mặc định.
  const mainWarehouse = warehouses.data?.find((w) => w.type === 'Main' && w.isActive)?.id
  useEffect(() => {
    if (isNew && mainWarehouse && !form.getFieldValue('warehouseId')) form.setFieldValue('warehouseId', mainWarehouse)
  }, [isNew, mainWarehouse, form])

  useEffect(() => {
    if (!doc) return
    form.setFieldsValue({
      date: dayjs(doc.date),
      warehouseId: doc.toWarehouseId ?? undefined,
      supplierId: doc.supplierId ?? undefined,
      invoiceNo: doc.invoiceNo ?? '',
      invoiceSeries: doc.invoiceSeries ?? '',
      invoiceDate: doc.invoiceDate ? dayjs(doc.invoiceDate) : null,
      deliveryNoteNo: doc.deliveryNoteNo ?? '',
      deliverer: doc.deliverer ?? '',
      note: doc.note ?? '',
      lines: doc.lines.map((l) => ({
        id: l.id,
        itemId: l.itemId,
        uomId: l.uomId,
        qty: l.qty,
        unitPrice: l.unitPrice,
        vatCode: l.vatCode,
        lotNo: l.lotNo ?? '',
        expiryDate: l.expiryDate ? dayjs(l.expiryDate) : null,
        note: l.note ?? '',
      })),
    })
  }, [doc, form])

  const totals = lines.reduce(
    (sum, line) => {
      const c = computeLine(line, line?.itemId ? itemsById.get(line.itemId) : undefined)
      return { amount: sum.amount + c.amount, vat: sum.vat + c.vat }
    },
    { amount: 0, vat: 0 },
  )

  /**
   * Lưu nháp (lập mới hoặc sửa) và trả về phiếu đã lưu; null nếu ô chưa hợp lệ hoặc server báo lỗi (lỗi đã hiện trên phiếu).
   */
  const persist = async (): Promise<InventoryDocument | null> => {
    let values: ReceiptFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng hàng.')
      return null
    }
    setError(null)
    const input: SaveReceiptInput = {
      date: values.date.format('YYYY-MM-DD'),
      warehouseId: values.warehouseId!,
      supplierId: values.supplierId!,
      invoiceNo: orNull(values.invoiceNo),
      invoiceSeries: orNull(values.invoiceSeries),
      invoiceDate: values.invoiceDate ? values.invoiceDate.format('YYYY-MM-DD') : null,
      deliveryNoteNo: orNull(values.deliveryNoteNo),
      deliverer: orNull(values.deliverer),
      note: orNull(values.note),
      lines: (values.lines ?? []).map((l) => ({
        id: l.id ?? null,
        itemId: l.itemId!,
        uomId: l.uomId!,
        qty: l.qty ?? 0,
        unitPrice: l.unitPrice ?? 0,
        vatCode: l.vatCode ?? null,
        lotNo: orNull(l.lotNo),
        expiryDate: l.expiryDate ? l.expiryDate.format('YYYY-MM-DD') : null,
        note: orNull(l.note),
      })),
    }
    try {
      return isNew ? await mutations.create.mutateAsync(input) : await mutations.update.mutateAsync({ id, input })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được phiếu.')
      return null
    }
  }

  const save = async () => {
    if (readOnly) return
    const saved = await persist()
    if (!saved) return
    message.success(`Đã lưu phiếu ${saved.no}.`)
    if (isNew) navigate(`/kho/grn/${saved.id}`, { replace: true })
  }

  /** Ghi sổ: phiếu đang sửa được thì lưu nháp trước (để ghi sổ đúng nội dung đang thấy), rồi ghi sổ. */
  const postDocument = () =>
    modal.confirm({
      title: 'Ghi sổ phiếu nhập?',
      content: 'Ghi sổ thì cộng tồn kho theo phiếu. Sau khi ghi sổ không sửa được; muốn sửa phải bỏ ghi sổ khi kỳ kho còn mở.',
      okText: 'Ghi sổ',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        const saved = readOnly ? doc : await persist()
        if (!saved) return
        try {
          const posted = await mutations.post.mutateAsync(saved.id)
          message.success(`Đã ghi sổ phiếu ${posted.no}.`)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không ghi sổ được phiếu.')
        }
        if (isNew) navigate(`/kho/grn/${saved.id}`, { replace: true })
      },
    })

  const unpostDocument = () =>
    doc &&
    modal.confirm({
      title: `Bỏ ghi sổ phiếu ${doc.no}?`,
      content: 'Tồn kho trừ lại theo phiếu, phiếu về trạng thái nháp để sửa. Chỉ làm được khi kỳ kho của phiếu còn mở.',
      okText: 'Bỏ ghi sổ',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        try {
          await mutations.unpost.mutateAsync(doc.id)
          message.success(`Đã bỏ ghi sổ phiếu ${doc.no}.`)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không bỏ ghi sổ được phiếu.')
        }
      },
    })

  const cancelDocument = () =>
    doc &&
    modal.confirm({
      title: `Hủy phiếu ${doc.no}?`,
      content: 'Phiếu đã hủy giữ lại số phiếu và lịch sử, không sửa hay dùng lại được.',
      okText: 'Hủy phiếu',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        try {
          await mutations.cancel.mutateAsync(doc.id)
          message.success(`Đã hủy phiếu ${doc.no}.`)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không hủy được phiếu.')
        }
      },
    })

  const pickItem = (index: number, itemId: string) => {
    const item = itemsById.get(itemId)
    if (!item) return
    form.setFieldValue(['lines', index, 'uomId'], item.baseUomId)
    form.setFieldValue(['lines', index, 'vatCode'], item.vatCode)
  }

  const columns: LineColumn[] = [
    {
      key: 'item',
      title: 'Mặt hàng',
      width: 280,
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
              onChange={(v: string) => pickItem(field.name, v)}
              popupMatchSelectWidth={420}
            />
          </Form.Item>
        </>
      ),
    },
    {
      key: 'uom',
      title: 'ĐVT',
      width: 90,
      render: (field) => {
        const item = lines[field.name]?.itemId ? itemsById.get(lines[field.name].itemId!) : undefined
        return (
          <Form.Item name={[field.name, 'uomId']} noStyle rules={[{ required: true, message: 'Chọn ĐVT.' }]}>
            <Select
              variant="borderless"
              style={{ width: '100%' }}
              aria-label={`ĐVT dòng ${field.name + 1}`}
              options={(item?.uoms ?? []).map((u) => ({ value: u.uomId, label: u.code }))}
            />
          </Form.Item>
        )
      },
    },
    {
      key: 'qty',
      title: 'Số lượng',
      width: 100,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'qty']} noStyle rules={[{ required: true, type: 'number', min: 0.0001, message: 'SL > 0.' }]}>
          <InputNumber {...cellNumber} min={0} precision={4} aria-label={`Số lượng dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'baseQty',
      title: 'SL quy đổi',
      width: 110,
      align: 'right',
      render: (field) => {
        const line = lines[field.name]
        const item = line?.itemId ? itemsById.get(line.itemId) : undefined
        const baseCode = item?.uoms.find((u) => u.uomId === item.baseUomId)?.code ?? ''
        return <span className="line-grid-readonly">{item ? `${formatQuantity(computeLine(line, item).baseQty)} ${baseCode}` : ''}</span>
      },
    },
    {
      key: 'price',
      title: 'Đơn giá',
      width: 130,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'unitPrice']} noStyle rules={[{ required: true, type: 'number', min: 0, message: 'Nhập đơn giá.' }]}>
          <InputNumber {...cellNumber} min={0} aria-label={`Đơn giá dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'amount',
      title: 'Thành tiền',
      width: 120,
      align: 'right',
      render: (field) => {
        const line = lines[field.name]
        return <span className="line-grid-readonly">{formatMoney(computeLine(line, line?.itemId ? itemsById.get(line.itemId) : undefined).amount)}</span>
      },
    },
    {
      key: 'vat',
      title: 'VAT',
      width: 80,
      render: (field) => (
        <Form.Item name={[field.name, 'vatCode']} noStyle>
          <Select
            variant="borderless"
            style={{ width: '100%' }}
            allowClear
            aria-label={`VAT dòng ${field.name + 1}`}
            options={vatOptions.map((o) => ({ value: o.value, label: o.value === 'KCT' ? 'KCT' : o.label }))}
          />
        </Form.Item>
      ),
    },
    {
      key: 'vatAmount',
      title: 'Tiền VAT',
      width: 100,
      align: 'right',
      render: (field) => {
        const line = lines[field.name]
        return <span className="line-grid-readonly">{formatMoney(computeLine(line, line?.itemId ? itemsById.get(line.itemId) : undefined).vat)}</span>
      },
    },
    {
      key: 'lot',
      title: 'Số lô',
      width: 100,
      render: (field) => {
        const line = lines[field.name]
        const item = line?.itemId ? itemsById.get(line.itemId) : undefined
        return (
          <Form.Item name={[field.name, 'lotNo']} noStyle rules={[{ required: item?.trackLot === true, whitespace: true, message: 'Nhập số lô.' }]}>
            <Input variant="borderless" maxLength={50} aria-label={`Số lô dòng ${field.name + 1}`} placeholder={item?.trackLot ? 'Bắt buộc' : ''} />
          </Form.Item>
        )
      },
    },
    {
      key: 'expiry',
      title: 'Hạn dùng',
      width: 120,
      render: (field) => {
        const line = lines[field.name]
        const item = line?.itemId ? itemsById.get(line.itemId) : undefined
        return (
          <Form.Item name={[field.name, 'expiryDate']} noStyle rules={[{ required: item?.trackExpiry === true, message: 'Nhập hạn dùng.' }]}>
            <DatePicker variant="borderless" format="DD/MM/YYYY" style={{ width: '100%' }} aria-label={`Hạn dùng dòng ${field.name + 1}`} />
          </Form.Item>
        )
      },
    },
    {
      key: 'note',
      title: 'Ghi chú',
      width: 160,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle>
          <Input variant="borderless" maxLength={500} aria-label={`Ghi chú dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? documentStatusLabels[doc.status] : '', tone: doc ? documentStatusTone[doc.status] : ('neutral' as const) }

  return (
    <DocumentForm
      title="Phiếu nhập mua từ NCC"
      number={isNew ? null : (doc?.no ?? '')}
      status={status}
      meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
      loading={document.isLoading}
      error={error ?? (document.error ? document.error.message : null)}
      notice={doc?.readOnlyReason && <Alert type="info" showIcon title={doc.readOnlyReason} />}
      form={form}
      initialValues={{ date: dayjs(), lines: [] }}
      readOnly={readOnly}
      onSave={save}
      onAddLine={() => addRef.current?.()}
      actions={
        <>
          {!readOnly && (
            <Button type="primary" icon={<SaveOutlined />} onClick={save} loading={mutations.create.isPending || mutations.update.isPending}>
              Lưu nháp
            </Button>
          )}
          {(isNew ? !readOnly && hasPermission(Permission.InventoryDocumentPost) : doc?.canPost) && (
            <Button icon={<CheckCircleOutlined />} onClick={postDocument} loading={mutations.post.isPending}>
              Ghi sổ
            </Button>
          )}
          {doc?.canUnpost && (
            <Button danger icon={<RollbackOutlined />} onClick={unpostDocument} loading={mutations.unpost.isPending}>
              Bỏ ghi sổ
            </Button>
          )}
          {!isNew && doc?.status === 'Draft' && doc.canEdit && (
            <Button danger icon={<StopOutlined />} onClick={cancelDocument}>
              Hủy phiếu
            </Button>
          )}
          <Button type="text" icon={<CloseOutlined />} onClick={() => navigate('/kho/grn')}>
            Đóng
          </Button>
        </>
      }
      header={
        <>
          <Form.Item label="Ngày nhập" name="date" rules={[{ required: true, message: 'Chọn ngày nhập.' }]} extra="Quyết định kỳ kho khi ghi sổ.">
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Kho nhập" name="warehouseId" rules={[{ required: true, message: 'Chọn kho nhập.' }]}>
            <Select
              showSearch={{ optionFilterProp: 'label' }}
              options={(warehouses.data ?? [])
                .filter((w) => w.isActive || w.id === doc?.toWarehouseId)
                .map((w) => ({ value: w.id, label: `${w.code} – ${w.name}` }))}
            />
          </Form.Item>
          <Form.Item
            label="Nhà cung cấp"
            name="supplierId"
            rules={[{ required: true, message: 'Chọn nhà cung cấp.' }]}
            extra="Chỉ NCC đã duyệt và đang áp dụng tại công ty."
          >
            <Select
              showSearch={{ optionFilterProp: 'label' }}
              options={(suppliers.data ?? []).map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))}
            />
          </Form.Item>
          <Form.Item label="Số hóa đơn" name="invoiceNo" rules={[{ max: 50 }]}>
            <Input autoComplete="off" />
          </Form.Item>
          <Form.Item label="Ký hiệu hóa đơn" name="invoiceSeries" rules={[{ max: 20 }]}>
            <Input autoComplete="off" />
          </Form.Item>
          <Form.Item label="Ngày hóa đơn" name="invoiceDate">
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Số phiếu giao hàng" name="deliveryNoteNo" rules={[{ max: 50 }]}>
            <Input autoComplete="off" />
          </Form.Item>
          <Form.Item label="Người giao" name="deliverer" rules={[{ max: 200 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]}>
            <Input />
          </Form.Item>
        </>
      }
      lines={
        <LineGrid
          name="lines"
          columns={columns}
          readOnly={readOnly}
          addRef={addRef}
          newLine={() => ({ id: null, qty: 1, unitPrice: 0 })}
        />
      }
      footer={
        <>
          <DocumentTotal label="Tiền hàng" value={formatMoney(totals.amount)} />
          <DocumentTotal label="Tiền VAT" value={formatMoney(totals.vat)} />
          <DocumentTotal label="Tổng thanh toán" value={formatMoney(totals.amount + totals.vat)} strong />
        </>
      }
    />
  )
}
