import { Alert, DatePicker, Form, Input, InputNumber, Select } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useParams } from 'react-router'
import { useGroupCompanies, useResolvedPrices } from '../../api/intercompany'
import {
  documentStatusLabels,
  receiveStatusLabels,
  useAvailableStock,
  useIntercompanySaleMutations,
  useInventoryDocument,
  useItemLookup,
  type InventoryDocument,
  type ItemLookup,
  type SaveSaleInput,
} from '../../api/inventoryDocuments'
import { useWarehouses } from '../../api/organization'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DocumentForm, DocumentTotal } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { formatDateTime, formatMoney, formatQuantity, vnNumberInput } from '../../utils/format'
import { documentStatusTone } from './documentStatus'
import { useDocumentCommands } from './useDocumentCommands'

interface SaleLineValues {
  id: string | null
  itemId?: string
  uomId?: string
  qty?: number | null
  lotNo?: string
  note?: string
}

interface SaleFormValues {
  date: Dayjs
  fromWarehouseId?: string
  buyerCompanyId?: string
  invoiceNo?: string
  invoiceDate?: Dayjs | null
  deliverer?: string
  note?: string
  lines: SaleLineValues[]
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Trang phiếu xuất bán nội bộ (design.md mục 6.3, 10.7) trên khuôn DocumentForm: chỉ xuất từ kho tổng sang kho tổng công ty mua;
 * đơn giá, thuế suất lấy từ bảng giá nội bộ (hiện trước khi lưu, server lấy lại khi lưu – không nhập tay). Ghi sổ sinh phiếu nhập
 * mua nội bộ "Chờ nhận" ở công ty mua; đầu phiếu hiện số phiếu đó và trạng thái nhận.
 */
export function IntercompanySalePage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<SaleFormValues>()
  const document = useInventoryDocument(isNew ? null : id)
  const mutations = useIntercompanySaleMutations()
  const warehouses = useWarehouses()
  const companies = useGroupCompanies()
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const addRef = useRef<(() => void) | null>(null)

  const doc = document.data
  const readOnly = isNew ? !hasPermission(Permission.InventoryDocumentCreate) : !doc?.canEdit
  const watchedLines = Form.useWatch('lines', form) as SaleLineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const fromWarehouseId = Form.useWatch('fromWarehouseId', form) as string | undefined
  const buyerCompanyId = Form.useWatch('buyerCompanyId', form) as string | undefined
  const date = Form.useWatch('date', form) as Dayjs | undefined
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])
  const stock = useAvailableStock(readOnly ? undefined : fromWarehouseId, itemIds)
  const prices = useResolvedPrices(readOnly ? undefined : buyerCompanyId, date?.format('YYYY-MM-DD'), itemIds)
  const priceById = useMemo(() => new Map((prices.data ?? []).map((p) => [p.itemId, p])), [prices.data])
  // Liên công ty chỉ đi kho tổng → kho tổng (CLAUDE.md mục 4): chỉ cho chọn kho tổng.
  const mainWarehouses = (warehouses.data ?? []).filter((w) => w.type === 'Main' && (w.isActive || w.id === doc?.fromWarehouseId))

  useEffect(() => {
    if (isNew && mainWarehouses.length === 1 && !form.getFieldValue('fromWarehouseId')) form.setFieldValue('fromWarehouseId', mainWarehouses[0].id)
  }, [isNew, mainWarehouses, form])

  useEffect(() => {
    if (!doc) return
    form.setFieldsValue({
      date: dayjs(doc.date),
      fromWarehouseId: doc.fromWarehouseId ?? undefined,
      buyerCompanyId: doc.counterCompanyId ?? undefined,
      invoiceNo: doc.invoiceNo ?? '',
      invoiceDate: doc.invoiceDate ? dayjs(doc.invoiceDate) : null,
      deliverer: doc.deliverer ?? '',
      note: doc.note ?? '',
      lines: doc.lines.map((l) => ({ id: l.id, itemId: l.itemId, uomId: l.uomId, qty: l.qty, lotNo: l.lotNo ?? '', note: l.note ?? '' })),
    })
  }, [doc, form])

  /**
   * Đơn giá, thuế suất, thành tiền, VAT của một dòng: phiếu đã lưu dùng số của server; phiếu đang sửa tính trước từ giá nội bộ
   * theo ĐVT cơ sở × hệ số ĐVT. Ví dụ 15.000 đ/cái × hộp 10 cái = 150.000 đ/hộp.
   */
  const lineMoney = (index: number, line: SaleLineValues | undefined, item: ItemLookup | undefined) => {
    const saved = readOnly ? doc?.lines[index] : undefined
    if (saved) return { price: saved.unitPrice, vat: saved.vatCode, amount: saved.amount, vatAmount: saved.vatAmount }
    const resolved = line?.itemId ? priceById.get(line.itemId) : undefined
    if (!resolved) return null
    const factor = item?.uoms.find((u) => u.uomId === line?.uomId)?.factor ?? 1
    const price = Math.round(resolved.unitPrice * factor * 1_000_000) / 1_000_000
    const amount = Math.round((line?.qty ?? 0) * price * 100) / 100
    const rate = resolved.vatCode && resolved.vatCode !== 'KCT' ? Number(resolved.vatCode) : 0
    return { price, vat: resolved.vatCode, amount, vatAmount: Math.round(amount * rate) / 100 }
  }
  const totals = lines.reduce(
    (sum, line, index) => {
      const m = lineMoney(index, line, line?.itemId ? itemsById.get(line.itemId) : undefined)
      return { amount: sum.amount + (m?.amount ?? 0), vat: sum.vat + (m?.vatAmount ?? 0) }
    },
    { amount: 0, vat: 0 },
  )

  const persist = async (): Promise<InventoryDocument | null> => {
    let values: SaleFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng hàng.')
      return null
    }
    setError(null)
    const input: SaveSaleInput = {
      date: values.date.format('YYYY-MM-DD'),
      fromWarehouseId: values.fromWarehouseId!,
      buyerCompanyId: values.buyerCompanyId!,
      invoiceNo: orNull(values.invoiceNo),
      invoiceDate: values.invoiceDate ? values.invoiceDate.format('YYYY-MM-DD') : null,
      deliverer: orNull(values.deliverer),
      note: orNull(values.note),
      lines: (values.lines ?? []).map((l) => ({
        id: l.id ?? null, itemId: l.itemId!, uomId: l.uomId!, qty: l.qty ?? 0, lotNo: orNull(l.lotNo), note: orNull(l.note),
      })),
    }
    try {
      return isNew ? await mutations.create.mutateAsync(input) : await mutations.update.mutateAsync({ id, input })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được phiếu.')
      return null
    }
  }

  const { save, actions } = useDocumentCommands({
    isNew, doc, readOnly, persist, mutations, listPath: '/kho/ics', noun: 'phiếu xuất bán nội bộ', setError,
  })

  const readonlyCell = (text: string) => <span className="line-grid-readonly">{text}</span>

  const columns: LineColumn[] = [
    {
      key: 'item',
      title: 'Mặt hàng',
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
      title: 'ĐVT',
      width: 80,
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
      title: 'SL xuất',
      width: 95,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'qty']} noStyle rules={[{ required: true, type: 'number', min: 0.0001, message: 'SL > 0.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={4} aria-label={`SL xuất dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'available',
      title: 'Tồn kho xuất',
      width: 100,
      align: 'right',
      render: (field) => {
        const line = lines[field.name]
        if (readOnly || !line?.itemId || !stock.data) return readonlyCell('')
        const qty = stock.data.filter((s) => s.itemId === line.itemId && (!orNull(line.lotNo) || s.lotNo === orNull(line.lotNo))).reduce((sum, s) => sum + s.qty, 0)
        const item = itemsById.get(line.itemId)
        const factor = item?.uoms.find((u) => u.uomId === line.uomId)?.factor ?? 1
        // Chỉ cảnh báo; server chặn tồn âm khi ghi sổ (CLAUDE.md mục 4).
        const short = (line.qty ?? 0) * factor > qty
        return <span className={short ? 'line-grid-readonly line-grid-warning' : 'line-grid-readonly'}>{formatQuantity(qty)}</span>
      },
    },
    {
      key: 'price',
      title: 'Giá bán nội bộ',
      width: 120,
      align: 'right',
      render: (field, index) => {
        const line = lines[field.name]
        const m = lineMoney(index, line, line?.itemId ? itemsById.get(line.itemId) : undefined)
        if (!m && line?.itemId && buyerCompanyId && !readOnly)
          return <span className="line-grid-readonly line-grid-warning" title="Khai báo ở Giá và NCC → Bảng giá nội bộ">Chưa có giá</span>
        return readonlyCell(m ? formatMoney(m.price) : '')
      },
    },
    {
      key: 'vat',
      title: 'VAT',
      width: 60,
      render: (field, index) => {
        const line = lines[field.name]
        const vat = lineMoney(index, line, line?.itemId ? itemsById.get(line.itemId) : undefined)?.vat
        return readonlyCell(vat ? (vat === 'KCT' ? 'KCT' : `${vat}%`) : '')
      },
    },
    {
      key: 'amount',
      title: 'Thành tiền',
      width: 120,
      align: 'right',
      render: (field, index) => {
        const line = lines[field.name]
        return readonlyCell(formatMoney(lineMoney(index, line, line?.itemId ? itemsById.get(line.itemId) : undefined)?.amount ?? 0))
      },
    },
    {
      key: 'lot',
      title: 'Số lô',
      width: 90,
      render: (field) => {
        const item = lines[field.name]?.itemId ? itemsById.get(lines[field.name].itemId!) : undefined
        return (
          <Form.Item name={[field.name, 'lotNo']} noStyle rules={[{ required: item?.trackLot === true, whitespace: true, message: 'Nhập số lô.' }]}>
            <Input variant="borderless" maxLength={50} aria-label={`Số lô dòng ${field.name + 1}`} placeholder={item?.trackLot ? 'Bắt buộc' : ''} />
          </Form.Item>
        )
      },
    },
    {
      key: 'note',
      title: 'Ghi chú',
      width: 150,
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
  const receiveText = doc?.linkedDocumentNo
    ? `${doc.linkedDocumentNo}${doc.receiveStatus ? ` – ${receiveStatusLabels[doc.receiveStatus]}` : ''}`
    : 'Sinh khi ghi sổ'

  return (
    <DocumentForm
      title="Phiếu xuất bán nội bộ"
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
      actions={actions}
      header={
        <>
          <Form.Item label="Ngày xuất" name="date" rules={[{ required: true, message: 'Chọn ngày xuất.' }]} extra="Quyết định kỳ kho và giá nội bộ áp dụng.">
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Kho xuất (kho tổng)" name="fromWarehouseId" rules={[{ required: true, message: 'Chọn kho xuất.' }]}>
            <Select showSearch={{ optionFilterProp: 'label' }} options={mainWarehouses.map((w) => ({ value: w.id, label: `${w.code} – ${w.name}` }))} />
          </Form.Item>
          <Form.Item label="Công ty mua" name="buyerCompanyId" rules={[{ required: true, message: 'Chọn công ty mua.' }]} extra="Nhận vào kho tổng của công ty mua.">
            <Select
              showSearch={{ optionFilterProp: 'label' }}
              options={[
                ...(companies.data ?? []).map((c) => ({ value: c.id, label: `${c.code} – ${c.name}` })),
                ...(doc?.counterCompanyId && !(companies.data ?? []).some((c) => c.id === doc.counterCompanyId)
                  ? [{ value: doc.counterCompanyId, label: `${doc.counterCompanyCode ?? ''} – ${doc.counterCompanyName ?? ''}` }]
                  : []),
              ]}
            />
          </Form.Item>
          <Form.Item label="Số hóa đơn" name="invoiceNo" rules={[{ max: 50 }]}>
            <Input autoComplete="off" />
          </Form.Item>
          <Form.Item label="Ngày hóa đơn" name="invoiceDate">
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Người giao / phương tiện" name="deliverer" rules={[{ max: 200 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Phiếu nhập bên mua">
            <Input disabled aria-label="Phiếu nhập bên mua" value={receiveText} />
          </Form.Item>
          <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]} className="document-form-wide">
            <Input />
          </Form.Item>
        </>
      }
      lines={<LineGrid name="lines" columns={columns} readOnly={readOnly} addRef={addRef} newLine={() => ({ id: null, qty: 1 })} />}
      footer={
        <>
          <DocumentTotal label="Tiền hàng" value={formatMoney(totals.amount)} />
          <DocumentTotal label="Tiền VAT" value={formatMoney(totals.vat)} />
          <DocumentTotal label="Tổng giá trị bán" value={formatMoney(totals.amount + totals.vat)} strong />
        </>
      }
    />
  )
}
