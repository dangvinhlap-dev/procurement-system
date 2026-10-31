import { Alert, DatePicker, Form, Input, InputNumber, Select } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useParams } from 'react-router'
import {
  documentStatusLabels,
  useAvailableStock,
  useInventoryDocument,
  useItemLookup,
  useStockTransferMutations,
  type InventoryDocument,
  type ItemLookup,
  type SaveTransferInput,
} from '../../api/inventoryDocuments'
import { useWarehouses } from '../../api/organization'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DocumentForm, DocumentTotal } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { formatDateTime, formatQuantity, vnNumberInput } from '../../utils/format'
import { documentStatusTone } from './documentStatus'
import { useDocumentCommands } from './useDocumentCommands'

interface TransferLineValues {
  id: string | null
  itemId?: string
  uomId?: string
  qty?: number | null
  lotNo?: string
  note?: string
}

interface TransferFormValues {
  date: Dayjs
  fromWarehouseId?: string
  toWarehouseId?: string
  deliverer?: string
  receiver?: string
  note?: string
  lines: TransferLineValues[]
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/** Số lượng theo ĐVT cơ sở của một dòng (ví dụ 2 thùng × 24 = 48 chai), làm tròn 4 số lẻ như server. */
function baseQty(line: TransferLineValues | undefined, item: ItemLookup | undefined) {
  const factor = item?.uoms.find((u) => u.uomId === line?.uomId)?.factor ?? 1
  return Math.round((line?.qty ?? 0) * factor * 10000) / 10000
}

/**
 * Trang phiếu chuyển kho nội bộ (design.md mục 10.6) trên khuôn DocumentForm: chuyển giữa hai kho cùng công ty, không có đơn giá –
 * giá vốn tạm tính khi ghi sổ, chốt khi đóng kỳ (design.md mục 7). Dòng hiện tồn khả dụng tại kho xuất để tránh chuyển quá tồn.
 */
export function StockTransferPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<TransferFormValues>()
  const document = useInventoryDocument(isNew ? null : id)
  const mutations = useStockTransferMutations()
  const warehouses = useWarehouses()
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const addRef = useRef<(() => void) | null>(null)

  const doc = document.data
  const readOnly = isNew ? !hasPermission(Permission.InventoryDocumentCreate) : !doc?.canEdit
  const watchedLines = Form.useWatch('lines', form) as TransferLineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const fromWarehouseId = Form.useWatch('fromWarehouseId', form) as string | undefined
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])
  const stock = useAvailableStock(readOnly ? undefined : fromWarehouseId, itemIds)

  /**
   * Tồn tại kho xuất theo ĐVT cơ sở: đúng lô nếu dòng có số lô, ngược lại cộng mọi lô. Phiếu chỉ xem (đã ghi sổ, đã hủy) không
   * hiện, vì tồn lúc này đã trừ chính phiếu nên so sánh không còn ý nghĩa.
   */
  const available = (line: TransferLineValues | undefined) => {
    if (readOnly || !line?.itemId || !stock.data) return undefined
    const lot = orNull(line.lotNo)
    return stock.data.filter((s) => s.itemId === line.itemId && (lot === null || s.lotNo === lot)).reduce((sum, s) => sum + s.qty, 0)
  }

  useEffect(() => {
    if (!doc) return
    form.setFieldsValue({
      date: dayjs(doc.date),
      fromWarehouseId: doc.fromWarehouseId ?? undefined,
      toWarehouseId: doc.toWarehouseId ?? undefined,
      deliverer: doc.deliverer ?? '',
      receiver: doc.receiver ?? '',
      note: doc.note ?? '',
      lines: doc.lines.map((l) => ({ id: l.id, itemId: l.itemId, uomId: l.uomId, qty: l.qty, lotNo: l.lotNo ?? '', note: l.note ?? '' })),
    })
  }, [doc, form])

  const persist = async (): Promise<InventoryDocument | null> => {
    let values: TransferFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng hàng.')
      return null
    }
    setError(null)
    const input: SaveTransferInput = {
      date: values.date.format('YYYY-MM-DD'),
      fromWarehouseId: values.fromWarehouseId!,
      toWarehouseId: values.toWarehouseId!,
      deliverer: orNull(values.deliverer),
      receiver: orNull(values.receiver),
      note: orNull(values.note),
      lines: (values.lines ?? []).map((l) => ({
        id: l.id ?? null,
        itemId: l.itemId!,
        uomId: l.uomId!,
        qty: l.qty ?? 0,
        lotNo: orNull(l.lotNo),
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

  const { save, actions } = useDocumentCommands({
    isNew, doc, readOnly, persist, mutations, listPath: '/kho/trf', noun: 'phiếu chuyển kho', setError,
  })

  const warehouseOptions = (current: string | null | undefined) =>
    (warehouses.data ?? [])
      .filter((w) => w.isActive || w.id === current)
      .map((w) => ({ value: w.id, label: `${w.code} – ${w.name}` }))

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
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={4} aria-label={`Số lượng dòng ${field.name + 1}`} />
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
        return <span className="line-grid-readonly">{item ? `${formatQuantity(baseQty(line, item))} ${baseCode}` : ''}</span>
      },
    },
    {
      key: 'available',
      title: 'Tồn kho xuất',
      width: 110,
      align: 'right',
      render: (field) => {
        const line = lines[field.name]
        const qty = available(line)
        const item = line?.itemId ? itemsById.get(line.itemId) : undefined
        // Chỉ cảnh báo trên giao diện; server mới là nơi chặn tồn âm khi ghi sổ (design.md mục 4).
        const short = qty !== undefined && baseQty(line, item) > qty
        return (
          <span className={short ? 'line-grid-readonly line-grid-warning' : 'line-grid-readonly'} title={short ? 'Vượt tồn tại kho xuất' : undefined}>
            {qty === undefined ? '' : formatQuantity(qty)}
          </span>
        )
      },
    },
    {
      key: 'lot',
      title: 'Số lô',
      width: 100,
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
      width: 200,
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
      title="Phiếu chuyển kho nội bộ"
      number={isNew ? null : (doc?.no ?? '')}
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
          <Form.Item label="Ngày chuyển" name="date" rules={[{ required: true, message: 'Chọn ngày chuyển.' }]} extra="Quyết định kỳ kho khi ghi sổ.">
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Kho xuất" name="fromWarehouseId" rules={[{ required: true, message: 'Chọn kho xuất.' }]}>
            <Select showSearch={{ optionFilterProp: 'label' }} options={warehouseOptions(doc?.fromWarehouseId)} />
          </Form.Item>
          <Form.Item
            label="Kho nhập"
            name="toWarehouseId"
            dependencies={['fromWarehouseId']}
            extra="Cùng công ty; liên công ty dùng cặp phiếu ICS/ICR."
            rules={[
              { required: true, message: 'Chọn kho nhập.' },
              ({ getFieldValue }) => ({
                validator: (_, value) =>
                  value && value === getFieldValue('fromWarehouseId') ? Promise.reject(new Error('Kho nhập phải khác kho xuất.')) : Promise.resolve(),
              }),
            ]}
          >
            <Select showSearch={{ optionFilterProp: 'label' }} options={warehouseOptions(doc?.toWarehouseId)} />
          </Form.Item>
          <Form.Item label="Người giao" name="deliverer" rules={[{ max: 200 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Người nhận" name="receiver" rules={[{ max: 200 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Lý do chuyển" name="note" rules={[{ max: 1000 }]}>
            <Input />
          </Form.Item>
        </>
      }
      lines={<LineGrid name="lines" columns={columns} readOnly={readOnly} addRef={addRef} newLine={() => ({ id: null, qty: 1 })} />}
      footer={<DocumentTotal label="Số dòng" value={formatQuantity(lines.length)} strong />}
    />
  )
}
