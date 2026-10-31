import { DollarOutlined, FileAddOutlined } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Form, Input, InputNumber, Modal, Select, Space, Table } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useParams } from 'react-router'
import { useItemLookup, useSupplierLookup } from '../../api/inventoryDocuments'
import { vatOptions } from '../../api/masterData'
import { useWarehouses } from '../../api/organization'
import { pickSupplierPrice, useSupplierPrices } from '../../api/pricing'
import {
  purchaseStatusLabels,
  purchaseStatusTone,
  useOpenRequestLines,
  usePurchaseMutations,
  usePurchaseOrder,
  type OpenRequestLine,
  type PurchaseOrder,
  type SavePurchaseOrderInput,
} from '../../api/purchasing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DocumentForm, DocumentTotal } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { formatDate, formatDateTime, formatMoney, formatQuantity, vnNumberInput } from '../../utils/format'
import { ApprovalHistory } from './ApprovalHistory'
import { useApprovalCommands } from './useApprovalCommands'

interface PoLineValues {
  id: string | null
  itemId?: string
  uomId?: string
  qty?: number | null
  unitPrice?: number | null
  discountPct?: number | null
  vatCode?: string | null
  deliveryDate?: Dayjs | null
  note?: string
  sourcePrLineId?: string | null
}

interface PoFormValues {
  date: Dayjs
  supplierId?: string
  warehouseId?: string
  expectedDate?: Dayjs | null
  paymentTerms?: string
  deliveryTerms?: string
  deliveryAddress?: string
  receiveTolerancePct?: number
  note?: string
  lines: PoLineValues[]
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Tính một dòng giống server (PurchaseOrderLine.Set): tiền hàng = SL × giá; CK = tiền hàng × %; thành tiền = tiền hàng − CK;
 * VAT = thành tiền × thuế suất; mỗi bước làm tròn 2 số. Ví dụ 10 × 100.000, CK 5%, VAT 10% → 950.000; 95.000.
 */
function computeLine(line: PoLineValues | undefined) {
  const gross = Math.round((line?.qty ?? 0) * (line?.unitPrice ?? 0) * 100) / 100
  const discount = Math.round(gross * (line?.discountPct ?? 0)) / 100
  const amount = Math.round((gross - discount) * 100) / 100
  const rate = line?.vatCode && line.vatCode !== 'KCT' ? Number(line.vatCode) : 0
  return { gross, discount, amount, vat: Math.round(amount * rate) / 100 }
}

/**
 * Trang đơn mua hàng (design.md mục 10.3) trên khuôn DocumentForm: NCC đã duyệt và áp dụng tại công ty, kho nhận (mặc định kho tổng),
 * dòng nhập tay hoặc "Lấy dòng từ PR" (PR đã duyệt còn chưa đặt), chiết khấu, VAT, dung sai nhận hàng. Chỉ VND ở giai đoạn này.
 * Phiếu đã duyệt hiện SL đã nhận, còn lại theo phiếu nhập kho. Cột "Giá bảng giá" hiện giá gợi ý từ bảng giá NCC đang hiệu lực
 * (design.md mục 8.1); nút "Áp giá bảng giá" chép giá gợi ý vào các dòng – người lập vẫn sửa tay được.
 */
export function PurchaseOrderPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const { hasPermission } = useAuth()
  const { message } = App.useApp()
  const [form] = Form.useForm<PoFormValues>()
  const order = usePurchaseOrder(isNew ? null : id)
  const mutations = usePurchaseMutations<PurchaseOrder, SavePurchaseOrderInput>('purchase-orders')
  const warehouses = useWarehouses()
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [picking, setPicking] = useState(false)
  const addRef = useRef<(() => void) | null>(null)
  const doc = order.data
  const readOnly = isNew ? !hasPermission(Permission.PoCreate) : !doc?.actions.canEdit
  const suppliers = useSupplierLookup(doc?.supplierId ? [doc.supplierId] : [])

  const watchedLines = Form.useWatch('lines', form) as PoLineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])
  const mainWarehouse = warehouses.data?.find((w) => w.type === 'Main' && w.isActive)?.id
  const supplierId = Form.useWatch('supplierId', form) as string | undefined
  const orderDate = Form.useWatch('date', form) as Dayjs | undefined
  const supplierPrices = useSupplierPrices(supplierId, orderDate?.format('YYYY-MM-DD'), itemIds)

  /** Giá gợi ý của dòng theo bảng giá NCC: đúng ĐVT và bậc SL (xem pickSupplierPrice). */
  const suggestedPrice = (line: PoLineValues | undefined) => {
    if (!line?.itemId || !line.uomId) return undefined
    const tiers = (supplierPrices.data ?? []).filter((t) => t.itemId === line.itemId)
    const factor = itemsById.get(line.itemId)?.uoms.find((u) => u.uomId === line.uomId)?.factor ?? 1
    return tiers.length ? pickSupplierPrice(tiers, line.uomId, factor, line.qty ?? 0) : undefined
  }

  /** Chép giá gợi ý (đơn giá, % CK, thuế suất) vào mọi dòng có giá bảng giá. */
  const applyPriceList = () => {
    const current = (form.getFieldValue('lines') as PoLineValues[] | undefined) ?? []
    let count = 0
    const next = current.map((l) => {
      const p = suggestedPrice(l)
      if (!p) return l
      count++
      return { ...l, unitPrice: p.unitPrice, discountPct: p.discountPct, vatCode: p.vatCode ?? l.vatCode }
    })
    form.setFieldValue('lines', next)
    if (count) message.success(`Đã áp giá bảng giá cho ${count} dòng.`)
    else message.info('Không có dòng nào có giá trong bảng giá đang hiệu lực của NCC này.')
  }

  useEffect(() => {
    if (isNew && mainWarehouse && !form.getFieldValue('warehouseId')) form.setFieldValue('warehouseId', mainWarehouse)
  }, [isNew, mainWarehouse, form])

  useEffect(() => {
    if (!doc) return
    form.setFieldsValue({
      date: dayjs(doc.date),
      supplierId: doc.supplierId,
      warehouseId: doc.warehouseId,
      expectedDate: doc.expectedDate ? dayjs(doc.expectedDate) : null,
      paymentTerms: doc.paymentTerms ?? '',
      deliveryTerms: doc.deliveryTerms ?? '',
      deliveryAddress: doc.deliveryAddress ?? '',
      receiveTolerancePct: doc.receiveTolerancePct,
      note: doc.note ?? '',
      lines: doc.lines.map((l) => ({
        id: l.id, itemId: l.itemId, uomId: l.uomId, qty: l.qty, unitPrice: l.unitPrice, discountPct: l.discountPct, vatCode: l.vatCode,
        deliveryDate: l.deliveryDate ? dayjs(l.deliveryDate) : null, note: l.note ?? '', sourcePrLineId: l.sourcePrLineId,
      })),
    })
  }, [doc, form])

  const totals = lines.reduce(
    (sum, l) => {
      const c = computeLine(l)
      return { gross: sum.gross + c.gross, discount: sum.discount + c.discount, amount: sum.amount + c.amount, vat: sum.vat + c.vat }
    },
    { gross: 0, discount: 0, amount: 0, vat: 0 },
  )

  const persist = async (): Promise<PurchaseOrder | null> => {
    let values: PoFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng hàng.')
      return null
    }
    setError(null)
    const input: SavePurchaseOrderInput = {
      date: values.date.format('YYYY-MM-DD'),
      supplierId: values.supplierId!,
      warehouseId: values.warehouseId!,
      expectedDate: values.expectedDate ? values.expectedDate.format('YYYY-MM-DD') : null,
      paymentTerms: orNull(values.paymentTerms),
      deliveryTerms: orNull(values.deliveryTerms),
      deliveryAddress: orNull(values.deliveryAddress),
      receiveTolerancePct: values.receiveTolerancePct ?? 0,
      note: orNull(values.note),
      lines: (values.lines ?? []).map((l) => ({
        id: l.id ?? null, itemId: l.itemId!, uomId: l.uomId!, qty: l.qty ?? 0, unitPrice: l.unitPrice ?? 0, discountPct: l.discountPct ?? 0,
        vatCode: l.vatCode ?? null, deliveryDate: l.deliveryDate ? l.deliveryDate.format('YYYY-MM-DD') : null, note: orNull(l.note),
        sourcePrLineId: l.sourcePrLineId ?? null,
      })),
    }
    try {
      return isNew ? await mutations.create.mutateAsync(input) : await mutations.update.mutateAsync({ id, input })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được phiếu.')
      return null
    }
  }

  const { save, actions } = useApprovalCommands({
    isNew, doc, readOnly, persist, mutations, listPath: '/mua-hang/po', noun: 'đơn mua hàng', setError,
  })

  /** Thêm các dòng PR đã chọn: SL = SL còn chưa đặt, đơn giá = giá tham khảo của PR, thuế suất theo mặt hàng. */
  const addFromRequests = (picked: OpenRequestLine[]) => {
    const current = (form.getFieldValue('lines') as PoLineValues[] | undefined) ?? []
    const existing = new Set(current.map((l) => l.sourcePrLineId).filter(Boolean))
    const added = picked
      .filter((p) => !existing.has(p.prLineId))
      .map((p) => ({
        id: null, itemId: p.itemId, uomId: p.uomId, qty: p.remainingQty, unitPrice: p.estPrice, discountPct: 0, vatCode: p.vatCode,
        deliveryDate: p.neededDate ? dayjs(p.neededDate) : null, note: '', sourcePrLineId: p.prLineId,
      }))
    form.setFieldValue('lines', [...current, ...added])
    if (!form.getFieldValue('supplierId')) {
      const suggested = picked.find((p) => p.suggestedSupplierId)?.suggestedSupplierId
      if (suggested && (suppliers.data ?? []).some((s) => s.id === suggested)) form.setFieldValue('supplierId', suggested)
    }
    setPicking(false)
  }

  const savedLine = (index: number) => (lines[index]?.id ? doc?.lines.find((l) => l.id === lines[index].id) : undefined)
  const readonlyCell = (text: string) => <span className="line-grid-readonly">{text}</span>
  const showReceiving = doc && doc.status !== 'Draft'

  const columns: LineColumn[] = [
    {
      key: 'item',
      title: 'Mặt hàng',
      width: 250,
      render: (field) => (
        <>
          <Form.Item name={[field.name, 'id']} hidden>
            <Input />
          </Form.Item>
          <Form.Item name={[field.name, 'sourcePrLineId']} hidden>
            <Input />
          </Form.Item>
          <Form.Item name={[field.name, 'itemId']} noStyle rules={[{ required: true, message: 'Chọn mặt hàng.' }]}>
            <Select
              variant="borderless"
              style={{ width: '100%' }}
              aria-label={`Mặt hàng dòng ${field.name + 1}`}
              disabled={!!lines[field.name]?.sourcePrLineId}
              showSearch={{ filterOption: false, onSearch: setSearch }}
              placeholder="Gõ mã hoặc tên hàng"
              options={(lookup.data ?? []).map((i) => ({ value: i.id, label: `${i.code} – ${i.name}` }))}
              onChange={(v: string) => {
                form.setFieldValue(['lines', field.name, 'uomId'], itemsById.get(v)?.baseUomId)
                form.setFieldValue(['lines', field.name, 'vatCode'], itemsById.get(v)?.vatCode)
              }}
              popupMatchSelectWidth={420}
            />
          </Form.Item>
        </>
      ),
    },
    {
      key: 'uom',
      title: 'ĐVT',
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
      key: 'qty',
      title: 'SL đặt',
      width: 90,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'qty']} noStyle rules={[{ required: true, type: 'number', min: 0.0001, message: 'SL > 0.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={4} aria-label={`SL đặt dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'price',
      title: 'Đơn giá',
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
          <Select variant="borderless" style={{ width: '100%' }} allowClear aria-label={`VAT dòng ${field.name + 1}`}
            options={vatOptions.map((o) => ({ value: o.value, label: o.value === 'KCT' ? 'KCT' : o.label }))} />
        </Form.Item>
      ),
    },
    { key: 'amount', title: 'Thành tiền', width: 115, align: 'right', render: (field) => readonlyCell(formatMoney(computeLine(lines[field.name]).amount)) },
    ...(readOnly
      ? []
      : ([
          {
            key: 'listPrice',
            title: 'Giá bảng giá',
            width: 110,
            align: 'right',
            render: (field) => {
              const p = suggestedPrice(lines[field.name])
              return readonlyCell(p ? `${formatMoney(p.unitPrice)}${p.discountPct ? ` −${p.discountPct}%` : ''}` : '')
            },
          },
        ] as LineColumn[])),
    {
      key: 'delivery',
      title: 'Ngày giao',
      width: 115,
      render: (field) => (
        <Form.Item name={[field.name, 'deliveryDate']} noStyle>
          <DatePicker variant="borderless" format="DD/MM/YYYY" style={{ width: '100%' }} aria-label={`Ngày giao dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    ...(showReceiving
      ? ([
          { key: 'received', title: 'Đã nhận', width: 80, align: 'right', render: (_, index) => readonlyCell(formatQuantity(savedLine(index)?.receivedQty ?? 0)) },
          { key: 'remaining', title: 'Còn lại', width: 80, align: 'right', render: (_, index) => readonlyCell(formatQuantity(savedLine(index)?.remainingQty ?? 0)) },
        ] as LineColumn[])
      : []),
    { key: 'pr', title: 'PR nguồn', width: 130, render: (_, index) => readonlyCell(savedLine(index)?.sourcePrNo ?? (lines[index]?.sourcePrLineId ? '(từ PR)' : '')) },
  ]

  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? purchaseStatusLabels[doc.status] : '', tone: doc ? purchaseStatusTone[doc.status] : ('neutral' as const) }

  return (
    <>
      <DocumentForm
        title="Đơn mua hàng"
        number={isNew ? null : (doc?.no ?? '')}
        status={status}
        meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
        loading={order.isLoading}
        error={error ?? (order.error ? order.error.message : null)}
        notice={
          doc?.rejectReason && doc.status === 'Draft' ? (
            <Alert type="warning" showIcon title={`Bị từ chối: ${doc.rejectReason}`} />
          ) : (
            doc?.actions.readOnlyReason && <Alert type="info" showIcon title={doc.actions.readOnlyReason} />
          )
        }
        form={form}
        initialValues={{ date: dayjs(), receiveTolerancePct: 0, lines: [] }}
        readOnly={readOnly}
        onSave={save}
        onAddLine={() => addRef.current?.()}
        actions={actions}
        header={
          <>
            <Form.Item label="Ngày đặt hàng" name="date" rules={[{ required: true, message: 'Chọn ngày.' }]}>
              <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Nhà cung cấp" name="supplierId" rules={[{ required: true, message: 'Chọn nhà cung cấp.' }]} extra="Chỉ NCC đã duyệt và đang áp dụng tại công ty.">
              <Select showSearch={{ optionFilterProp: 'label' }} options={(suppliers.data ?? []).map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))} />
            </Form.Item>
            <Form.Item label="Kho nhận" name="warehouseId" rules={[{ required: true, message: 'Chọn kho nhận.' }]}>
              <Select showSearch={{ optionFilterProp: 'label' }}
                options={(warehouses.data ?? []).filter((w) => w.isActive || w.id === doc?.warehouseId).map((w) => ({ value: w.id, label: `${w.code} – ${w.name}` }))} />
            </Form.Item>
            <Form.Item label="Ngày giao dự kiến" name="expectedDate">
              <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Điều khoản thanh toán" name="paymentTerms" rules={[{ max: 200 }]}>
              <Input />
            </Form.Item>
            <Form.Item label="Điều khoản giao hàng" name="deliveryTerms" rules={[{ max: 200 }]}>
              <Input />
            </Form.Item>
            <Form.Item label="Địa chỉ giao" name="deliveryAddress" rules={[{ max: 500 }]}>
              <Input />
            </Form.Item>
            <Form.Item label="Dung sai nhận hàng (%)" name="receiveTolerancePct" extra="Phiếu nhập được nhận tới SL đặt × (1 + dung sai).">
              <InputNumber {...vnNumberInput} style={{ width: '100%' }} min={0} max={20} />
            </Form.Item>
            <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]}>
              <Input />
            </Form.Item>
          </>
        }
        lines={
          <>
            {!readOnly && (
              <Space style={{ marginBottom: 6 }}>
                <Button icon={<FileAddOutlined />} onClick={() => setPicking(true)}>
                  Lấy dòng từ PR đã duyệt
                </Button>
                <Button icon={<DollarOutlined />} onClick={applyPriceList} disabled={!supplierId}>
                  Áp giá bảng giá
                </Button>
              </Space>
            )}
            <LineGrid name="lines" columns={columns} readOnly={readOnly} addRef={addRef}
              newLine={() => ({ id: null, qty: 1, unitPrice: 0, discountPct: 0, sourcePrLineId: null })} />
          </>
        }
        footer={
          <>
            <DocumentTotal label="Tiền hàng" value={formatMoney(totals.gross)} />
            <DocumentTotal label="Chiết khấu" value={formatMoney(totals.discount)} />
            <DocumentTotal label="Tiền VAT" value={formatMoney(totals.vat)} />
            <DocumentTotal label="Tổng thanh toán" value={formatMoney(totals.amount + totals.vat)} strong />
          </>
        }
        extraTabs={doc ? [{ key: 'approvals', label: 'Lịch sử duyệt', children: <ApprovalHistory steps={doc.approvals} /> }] : undefined}
      />
      {picking && <RequestLinePicker onCancel={() => setPicking(false)} onPick={addFromRequests} />}
    </>
  )
}

/**
 * Hộp chọn dòng PR đã duyệt còn chưa đặt hàng.
 * @param onPick Thêm các dòng đã chọn.
 * @param onCancel Đóng.
 */
export function RequestLinePicker({ onPick, onCancel }: { onPick: (lines: OpenRequestLine[]) => void; onCancel: () => void }) {
  const open = useOpenRequestLines(true)
  const [selected, setSelected] = useState<string[]>([])
  const rows = open.data ?? []
  return (
    <Modal
      open
      title="Lấy dòng từ đề nghị mua hàng đã duyệt"
      width={900}
      okText={`Thêm ${selected.length} dòng`}
      okButtonProps={{ disabled: selected.length === 0 }}
      cancelText="Đóng"
      onCancel={onCancel}
      onOk={() => onPick(rows.filter((r) => selected.includes(r.prLineId)))}
    >
      <Table<OpenRequestLine>
        size="small"
        rowKey="prLineId"
        loading={open.isLoading}
        pagination={false}
        scroll={{ y: 360 }}
        dataSource={rows}
        rowSelection={{ selectedRowKeys: selected, onChange: (keys) => setSelected(keys as string[]) }}
        locale={{ emptyText: 'Không còn dòng PR đã duyệt nào chưa đặt hàng.' }}
        columns={[
          { title: 'Số PR', dataIndex: 'requestNo', width: 150 },
          { title: 'Bộ phận', dataIndex: 'department', width: 120 },
          { title: 'Mặt hàng', key: 'item', render: (_, r) => `${r.itemCode} – ${r.itemName}` },
          { title: 'ĐVT', dataIndex: 'uomCode', width: 60 },
          { title: 'Còn chưa đặt', dataIndex: 'remainingQty', width: 100, align: 'right', render: (v: number) => formatQuantity(v) },
          { title: 'Ngày cần', dataIndex: 'neededDate', width: 100, render: (v: string | null) => formatDate(v) },
        ]}
      />
    </Modal>
  )
}
