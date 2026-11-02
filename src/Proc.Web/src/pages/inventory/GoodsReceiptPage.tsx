import { useDocumentFormSync } from '../../components/DocumentForm/useDocumentFormSync'
import { AuditOutlined, FileDoneOutlined, FileProtectOutlined } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Form, Input, InputNumber, Select } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useParams } from 'react-router'
import { apiOpenPdf } from '../../api/client'
import {
  documentStatusLabels,
  qcResultLabels,
  useGoodsReceiptMutations,
  useInventoryDocument,
  useItemLookup,
  useSupplierLookup,
  type InventoryDocument,
  type ItemLookup,
  type QcResult,
  type SaveReceiptInput,
} from '../../api/inventoryDocuments'
import { vatOptions } from '../../api/masterData'
import { useWarehouses } from '../../api/organization'
import { usePurchaseOrder, useReceivableOrders } from '../../api/purchasing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { DocumentForm, DocumentTotal } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { formatDateTime, formatMoney, formatQuantity, vnNumberInput, DATE_INPUT_FORMATS } from '../../utils/format'
import { documentStatusTone } from './documentStatus'
import { ReceiptInvoiceDialog } from './ReceiptInvoiceDialog'
import { useDocumentCommands } from './useDocumentCommands'

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
  purchaseOrderLineId?: string | null
  deliveredQty?: number | null
  rejectedQty?: number | null
  qcResult?: QcResult
  qcNote?: string
}

interface ReceiptFormValues {
  date: Dayjs
  warehouseId?: string
  supplierId?: string
  invoiceNo?: string
  invoiceSeries?: string
  invoiceDate?: Dayjs | null
  invoiceAmount?: number | null
  deliveryNoteNo?: string
  deliverer?: string
  note?: string
  purchaseOrderId?: string | null
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
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<ReceiptFormValues>()
  const document = useInventoryDocument(isNew ? null : id)
  const mutations = useGoodsReceiptMutations()
  const warehouses = useWarehouses()
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [editingInvoice, setEditingInvoice] = useState(false)
  const addRef = useRef<(() => void) | null>(null)

  const doc = document.data
  const readOnly = isNew ? !hasPermission(Permission.InventoryDocumentCreate) : !doc?.canEdit
  // Đính kèm được cả khi phiếu đã duyệt/ghi sổ (scan hóa đơn, biên bản) – quyền theo quyền lập phiếu, design.md mục 18.4.
  const attachmentTab = useAttachmentTab('InventoryDocument', doc?.id, hasPermission(Permission.InventoryDocumentCreate))
  const watchedLines = Form.useWatch('lines', form) as LineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])
  // Nhập theo đơn mua hàng (design.md mục 6.7): chọn PO đã duyệt → NCC, kho theo PO; dòng lấy từ dòng PO còn lại.
  const purchaseOrderId = Form.useWatch('purchaseOrderId', form) as string | null | undefined
  const receivable = useReceivableOrders(!readOnly)
  const order = usePurchaseOrder(purchaseOrderId ?? null)
  const suppliers = useSupplierLookup([doc?.supplierId, order.data?.supplierId].filter((x): x is string => !!x))

  // Lập mới: ngày hôm nay, kho tổng (nếu trong phạm vi) làm kho nhập mặc định.
  const mainWarehouse = warehouses.data?.find((w) => w.type === 'Main' && w.isActive)?.id
  useEffect(() => {
    if (isNew && mainWarehouse && !form.getFieldValue('warehouseId')) form.setFieldValue('warehouseId', mainWarehouse)
  }, [isNew, mainWarehouse, form])

  const { loadSaved, targetId } = useDocumentFormSync(form, id, doc, (doc) => ({
    date: dayjs(doc.date),
    warehouseId: doc.toWarehouseId ?? undefined,
    supplierId: doc.supplierId ?? undefined,
    invoiceNo: doc.invoiceNo ?? '',
    invoiceSeries: doc.invoiceSeries ?? '',
    invoiceDate: doc.invoiceDate ? dayjs(doc.invoiceDate) : null,
    invoiceAmount: doc.invoiceAmount ?? null,
    deliveryNoteNo: doc.deliveryNoteNo ?? '',
    deliverer: doc.deliverer ?? '',
    note: doc.note ?? '',
    purchaseOrderId: doc.purchaseOrderId ?? null,
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
      purchaseOrderLineId: l.purchaseOrderLineId ?? null,
      deliveredQty: l.deliveredQty ?? null,
      rejectedQty: l.rejectedQty ?? 0,
      qcResult: l.qcResult ?? 'NotChecked',
      qcNote: l.qcNote ?? '',
    })),
  }))

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
      invoiceAmount: values.invoiceAmount ?? null,
      deliveryNoteNo: orNull(values.deliveryNoteNo),
      deliverer: orNull(values.deliverer),
      note: orNull(values.note),
      purchaseOrderId: values.purchaseOrderId ?? null,
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
        purchaseOrderLineId: l.purchaseOrderLineId ?? null,
        deliveredQty: l.deliveredQty ?? null,
        rejectedQty: l.rejectedQty ?? 0,
        qcResult: l.qcResult ?? 'NotChecked',
        qcNote: orNull(l.qcNote),
      })),
    }
    try {
      const target = targetId()
      const saved = target ? await mutations.update.mutateAsync({ id: target, input }) : await mutations.create.mutateAsync(input)
      loadSaved(saved)
      return saved
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được phiếu.')
      return null
    }
  }

  const { save, actions: commands } = useDocumentCommands({
    isNew, doc, readOnly, persist, mutations, listPath: '/kho/grn', noun: 'phiếu nhập', setError,
  })

  const { message } = App.useApp()
  const [printingRecord, setPrintingRecord] = useState(false)
  /** In biên bản giao nhận và kiểm tra hàng hóa (sai lệch, không đạt) của phiếu đã lưu – design.md mục 18.14. */
  const printRecord = async () => {
    if (!doc) return
    setPrintingRecord(true)
    try {
      await apiOpenPdf(`/api/goods-receipts/${doc.id}/record-pdf`)
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Không in được biên bản.')
    } finally {
      setPrintingRecord(false)
    }
  }
  const actions = (
    <>
      {doc?.canUpdateInvoice && (
        <Button icon={<AuditOutlined />} onClick={() => setEditingInvoice(true)} title="Cập nhật số, ngày, tiền hóa đơn NCC về sau khi ghi sổ">
          Cập nhật hóa đơn
        </Button>
      )}
      {doc && (
        <Button icon={<FileProtectOutlined />} onClick={() => void printRecord()} loading={printingRecord} title="In biên bản giao nhận / sai lệch">
          In biên bản
        </Button>
      )}
      {commands}
    </>
  )

  const pickItem = (index: number, itemId: string) => {
    const item = itemsById.get(itemId)
    if (!item) return
    form.setFieldValue(['lines', index, 'uomId'], item.baseUomId)
    form.setFieldValue(['lines', index, 'vatCode'], item.vatCode)
  }

  /** Chọn PO: NCC, kho nhận theo PO; xóa dòng cũ (dòng phải trỏ về dòng PO mới). */
  const pickOrder = () => form.setFieldValue('lines', [])

  useEffect(() => {
    // PO vừa tải: NCC, kho nhận theo PO (server vẫn kiểm tra NCC phải trùng NCC của PO).
    if (readOnly || !order.data) return
    form.setFieldValue('supplierId', order.data.supplierId)
    form.setFieldValue('warehouseId', order.data.warehouseId)
  }, [order.data, readOnly, form])

  /** Điền mọi dòng PO còn lại (SL = còn lại; đơn giá = giá thực mua sau chiết khấu, thuế suất theo PO – server gán lại khi lưu). */
  const takeOrderLines = () =>
    form.setFieldValue(
      'lines',
      (order.data?.lines ?? [])
        .filter((l) => l.remainingQty > 0)
        .map((l) => ({
          id: null, itemId: l.itemId, uomId: l.uomId, qty: l.remainingQty, vatCode: l.vatCode, lotNo: '', expiryDate: null, note: '',
          unitPrice: Math.round(l.unitPrice * (100 - l.discountPct) * 10_000) / 1_000_000, purchaseOrderLineId: l.id,
        })),
    )

  const columns: LineColumn[] = [
    {
      key: 'item',
      title: 'Mặt hàng', required: true,
      width: 280,
      render: (field) => (
        <>
          <Form.Item name={[field.name, 'id']} hidden>
            <Input />
          </Form.Item>
          <Form.Item name={[field.name, 'purchaseOrderLineId']} hidden>
            <Input />
          </Form.Item>
          <Form.Item name={[field.name, 'itemId']} noStyle rules={[{ required: true, message: 'Chọn mặt hàng.' }]}>
            <Select
              variant="borderless"
              style={{ width: '100%' }}
              disabled={!!lines[field.name]?.purchaseOrderLineId}
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
      title: 'ĐVT', required: true,
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
      title: 'Số lượng', required: true,
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
      title: 'Đơn giá', required: true,
      width: 130,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'unitPrice']} noStyle rules={[{ required: true, type: 'number', min: 0, message: 'Nhập đơn giá.' }]}>
          <InputNumber {...cellNumber} min={0} disabled={!!lines[field.name]?.purchaseOrderLineId} aria-label={`Đơn giá dòng ${field.name + 1}`} />
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
            <DatePicker variant="borderless" format={DATE_INPUT_FORMATS} style={{ width: '100%' }} aria-label={`Hạn dùng dòng ${field.name + 1}`} />
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
    // Kiểm tra chất lượng, sai lệch nhận hàng (design.md mục 18.14 – V7): SL chứng từ giao, SL không đạt (không nhập kho), kết quả, ghi chú.
    {
      key: 'delivered',
      title: 'SL chứng từ giao',
      width: 100,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'deliveredQty']} noStyle>
          <InputNumber {...cellNumber} min={0} precision={4} aria-label={`SL chứng từ giao dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'rejected',
      title: 'SL không đạt',
      width: 90,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'rejectedQty']} noStyle>
          <InputNumber {...cellNumber} min={0} precision={4} aria-label={`SL không đạt dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'discrepancy',
      title: 'Thiếu/thừa',
      width: 85,
      align: 'right',
      render: (_, index) => {
        const l = lines[index]
        const diff = l?.deliveredQty == null ? 0 : (l.qty ?? 0) + (l.rejectedQty ?? 0) - l.deliveredQty
        return <span className={diff ? 'line-grid-readonly line-grid-warning' : 'line-grid-readonly'}>{diff ? formatQuantity(diff) : ''}</span>
      },
    },
    {
      key: 'qc',
      title: 'Kết quả KT',
      width: 110,
      render: (field) => (
        <Form.Item name={[field.name, 'qcResult']} noStyle>
          <Select variant="borderless" style={{ width: '100%' }} aria-label={`Kết quả kiểm tra dòng ${field.name + 1}`}
            options={Object.entries(qcResultLabels).map(([value, label]) => ({ value, label }))} />
        </Form.Item>
      ),
    },
    {
      key: 'qcNote',
      title: 'Ghi chú kiểm tra',
      width: 180,
      render: (field) => (
        <Form.Item name={[field.name, 'qcNote']} noStyle>
          <Input variant="borderless" maxLength={500} placeholder="Bắt buộc khi không đạt/thiếu thừa" aria-label={`Ghi chú kiểm tra dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? documentStatusLabels[doc.status] : '', tone: doc ? documentStatusTone[doc.status] : ('neutral' as const) }

  return (
    <>
    <DocumentForm
      title="Phiếu nhập mua từ NCC"
      extraTabs={[attachmentTab]}
      number={isNew ? null : (doc?.no ?? '')}
      audit={{ entityType: 'InventoryDocument', id: doc?.id, fields: { warehouseId: 'ToWarehouseId' } }}
      printPath={doc ? `/api/inventory-documents/${doc.id}/pdf` : undefined}
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
      actions={actions}
      header={
        <>
          <Form.Item label="Ngày nhập" name="date" rules={[{ required: true, message: 'Chọn ngày nhập.' }]} extra="Quyết định kỳ kho khi ghi sổ.">
            <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Đơn mua hàng" name="purchaseOrderId" extra="Nhập theo PO đã duyệt (trống = nhập thẳng theo NCC).">
            <Select
              allowClear
              showSearch={{ optionFilterProp: 'label' }}
              onChange={pickOrder}
              options={[
                ...(receivable.data?.items ?? []).map((o) => ({ value: o.id, label: `${o.no} – ${o.description ?? ''}` })),
                ...(doc?.purchaseOrderId && !(receivable.data?.items ?? []).some((o) => o.id === doc.purchaseOrderId)
                  ? [{ value: doc.purchaseOrderId, label: doc.purchaseOrderNo ?? '' }]
                  : []),
              ]}
            />
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
            <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Tiền hóa đơn (gồm VAT)" name="invoiceAmount" extra="Tổng thanh toán trên hóa đơn NCC – đối chiếu với tiền phiếu nhập.">
            <InputNumber {...vnNumberInput} min={0} precision={0} style={{ width: '100%' }} />
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
        <>
          {!readOnly && purchaseOrderId && (
            // Khóa cả khi đang tải lại PO: nút antd đang loading bỏ qua cú bấm mà không báo gì, và số còn lại cũ có thể sai.
            <Button icon={<FileDoneOutlined />} onClick={takeOrderLines} disabled={!order.data || order.isFetching} loading={order.isFetching} style={{ marginBottom: 6 }}>
              Lấy các dòng còn lại của PO
            </Button>
          )}
          <LineGrid
          auditEntity="InventoryDocumentLine"
          name="lines"
          columns={columns}
          readOnly={readOnly}
          addRef={addRef}
          newLine={() => ({ id: null, qty: 1, unitPrice: 0 })}
        />
        </>
      }
      footer={
        <>
          <DocumentTotal label="Tiền hàng" value={formatMoney(totals.amount)} />
          <DocumentTotal label="Tiền VAT" value={formatMoney(totals.vat)} />
          <DocumentTotal label="Tổng thanh toán" value={formatMoney(totals.amount + totals.vat)} strong />
        </>
      }
    />
    {doc?.canUpdateInvoice && <ReceiptInvoiceDialog doc={doc} open={editingInvoice} onClose={() => setEditingInvoice(false)} />}
    </>
  )
}
