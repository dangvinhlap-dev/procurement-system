import { useDocumentFormSync } from '../../components/DocumentForm/useDocumentFormSync'
import { Alert, DatePicker, Form, Input, InputNumber } from 'antd'
import { Select } from '../../classic/Select'
import dayjs, { type Dayjs } from 'dayjs'
import { useMemo, useRef, useState } from 'react'
import { useParams } from 'react-router'
import {
  documentStatusLabels,
  useAvailableStock,
  useInventoryDocument,
  useItemLookup,
  useStockAdjustmentMutations,
  type InventoryDocument,
  type ItemLookup,
  type SaveAdjustmentInput,
} from '../../api/inventoryDocuments'
import { useWarehouses } from '../../api/organization'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { DocumentForm, DocumentTotal } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { formatDateTime, formatMoney, formatQuantity, vnNumberInput, DATE_INPUT_FORMATS } from '../../utils/format'
import { documentStatusTone } from './documentStatus'
import { useDocumentCommands } from './useDocumentCommands'

/** Đường dẫn màn hình phiếu điều chỉnh / kiểm kê; trang phiếu là `${ADJUSTMENT_PATH}/:id`. */
export const ADJUSTMENT_PATH = '/kho/dieu-chinh'

interface AdjustmentLineValues {
  id: string | null
  itemId?: string
  uomId?: string
  direction?: 1 | -1
  qty?: number | null
  unitPrice?: number | null
  lotNo?: string
  expiryDate?: Dayjs | null
  note?: string
}

interface AdjustmentFormValues {
  date: Dayjs
  warehouseId?: string
  countReportNo?: string
  reason?: string
  note?: string
  lines: AdjustmentLineValues[]
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

const cellNumber = { ...vnNumberInput, variant: 'borderless', style: { width: '100%' } } as const

const directionOptions = [
  { value: 1, label: 'Tăng' },
  { value: -1, label: 'Giảm' },
]

/** Số lượng theo ĐVT cơ sở của một dòng (ví dụ 2 thùng × 24 = 48 chai), làm tròn 4 số lẻ như server. */
function baseQty(line: AdjustmentLineValues | undefined, item: ItemLookup | undefined) {
  const factor = item?.uoms.find((u) => u.uomId === line?.uomId)?.factor ?? 1
  return Math.round((line?.qty ?? 0) * factor * 10000) / 10000
}

/**
 * Thành tiền dòng tăng = SL × đơn giá, làm tròn 2 số lẻ như server (ví dụ 10 × 12.000 = 120.000). Dòng giảm không có giá trên phiếu –
 * xuất theo giá vốn tạm tính khi ghi sổ, chốt khi đóng kỳ (design.md mục 18.23).
 */
function lineAmount(line: AdjustmentLineValues | undefined) {
  if (line?.direction !== 1) return 0
  return Math.round((line.qty ?? 0) * (line.unitPrice ?? 0) * 100) / 100
}

/**
 * Trang phiếu điều chỉnh / kiểm kê kho – ADJ (design.md mục 6.1, 18.23) trên khuôn DocumentForm: một kho, lý do và số biên bản kiểm kê
 * bắt buộc; dòng Tăng (hàng thừa, giá nhập tay) hoặc Giảm (hàng thiếu, xuất theo giá vốn). Quyền riêng: lập/sửa/hủy và ghi sổ/bỏ ghi
 * sổ tách nhau. Dòng hiện tồn hiện tại của kho để đối chiếu với số kiểm kê.
 */
export function StockAdjustmentPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<AdjustmentFormValues>()
  const document = useInventoryDocument(isNew ? null : id)
  const mutations = useStockAdjustmentMutations()
  const warehouses = useWarehouses()
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const addRef = useRef<(() => void) | null>(null)

  const doc = document.data
  const readOnly = isNew ? !hasPermission(Permission.InventoryAdjustmentCreate) : !doc?.canEdit
  // Biên bản kiểm kê scan đính kèm vào phiếu (design.md mục 18.4) – quyền theo quyền lập chứng từ kho của khối đính kèm.
  const attachmentTab = useAttachmentTab('InventoryDocument', doc?.id, hasPermission(Permission.InventoryDocumentCreate))
  const watchedLines = Form.useWatch('lines', form) as AdjustmentLineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const warehouseId = Form.useWatch('warehouseId', form) as string | undefined
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])
  const stock = useAvailableStock(readOnly ? undefined : warehouseId, itemIds)

  /** Tồn hiện tại tại kho theo ĐVT cơ sở (đúng lô nếu dòng có lô); phiếu chỉ xem thì không hiện vì tồn đã gồm chính phiếu. */
  const onHand = (line: AdjustmentLineValues | undefined) => {
    if (readOnly || !line?.itemId || !stock.data) return undefined
    const lot = orNull(line.lotNo)
    return stock.data.filter((s) => s.itemId === line.itemId && (lot === null || s.lotNo === lot)).reduce((sum, s) => sum + s.qty, 0)
  }

  const { loadSaved, targetId } = useDocumentFormSync(form, id, doc, (doc) => ({
    date: dayjs(doc.date),
    warehouseId: doc.toWarehouseId ?? undefined,
    countReportNo: doc.countReportNo ?? '',
    reason: doc.reason ?? '',
    note: doc.note ?? '',
    lines: doc.lines.map((l) => ({
      id: l.id, itemId: l.itemId, uomId: l.uomId, direction: l.direction ?? 1, qty: l.qty, unitPrice: l.unitPrice,
      lotNo: l.lotNo ?? '', expiryDate: l.expiryDate ? dayjs(l.expiryDate) : null, note: l.note ?? '',
    })),
  }))

  const persist = async (): Promise<InventoryDocument | null> => {
    let values: AdjustmentFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng hàng.')
      return null
    }
    setError(null)
    const input: SaveAdjustmentInput = {
      date: values.date.format('YYYY-MM-DD'),
      warehouseId: values.warehouseId!,
      reason: values.reason?.trim() ?? '',
      countReportNo: values.countReportNo?.trim() ?? '',
      note: orNull(values.note),
      lines: (values.lines ?? []).map((l) => ({
        id: l.id ?? null,
        itemId: l.itemId!,
        uomId: l.uomId!,
        direction: l.direction ?? 1,
        qty: l.qty ?? 0,
        // Dòng giảm luôn gửi giá 0: xuất theo giá vốn (server từ chối giá khác 0).
        unitPrice: l.direction === -1 ? 0 : (l.unitPrice ?? 0),
        lotNo: orNull(l.lotNo),
        expiryDate: l.direction === 1 && l.expiryDate ? l.expiryDate.format('YYYY-MM-DD') : null,
        note: orNull(l.note),
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

  const { save, actions } = useDocumentCommands({
    isNew, doc, readOnly, persist, mutations, listPath: ADJUSTMENT_PATH, noun: 'phiếu điều chỉnh', setError,
    postPermission: Permission.InventoryAdjustmentPost,
  })

  const warehouseOptions = (current: string | null | undefined) =>
    (warehouses.data ?? [])
      .filter((w) => w.isActive || w.id === current)
      .map((w) => ({ value: w.id, label: `${w.code} – ${w.name}` }))

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
      key: 'direction',
      title: 'Tăng/Giảm', required: true,
      width: 90,
      render: (field) => (
        <Form.Item name={[field.name, 'direction']} noStyle rules={[{ required: true, message: 'Chọn Tăng hoặc Giảm.' }]}>
          <Select
            variant="borderless"
            style={{ width: '100%' }}
            aria-label={`Tăng/Giảm dòng ${field.name + 1}`}
            options={directionOptions}
            // Đổi sang Giảm thì bỏ giá nhập tay – dòng giảm xuất theo giá vốn.
            onChange={(v: 1 | -1) => v === -1 && form.setFieldValue(['lines', field.name, 'unitPrice'], 0)}
          />
        </Form.Item>
      ),
    },
    {
      key: 'uom',
      title: 'ĐVT', required: true,
      width: 80,
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
      key: 'onHand',
      title: 'Tồn hiện tại',
      width: 110,
      align: 'right',
      render: (field) => {
        const line = lines[field.name]
        const qty = onHand(line)
        const item = line?.itemId ? itemsById.get(line.itemId) : undefined
        // Chỉ cảnh báo trên giao diện; server chặn tồn âm khi ghi sổ (CLAUDE.md mục 4).
        const short = qty !== undefined && line?.direction === -1 && baseQty(line, item) > qty
        return (
          <span className={short ? 'line-grid-readonly line-grid-warning' : 'line-grid-readonly'} title={short ? 'Giảm vượt tồn hiện tại' : undefined}>
            {qty === undefined ? '' : formatQuantity(qty)}
          </span>
        )
      },
    },
    {
      key: 'price',
      title: 'Đơn giá (tăng)',
      width: 120,
      align: 'right',
      render: (field) => {
        const increase = lines[field.name]?.direction === 1
        // Dòng giảm không có đơn giá trên phiếu: hiện chữ "Giá vốn" thay số 0 để không hiểu nhầm là xuất giá 0.
        return increase ? (
          <Form.Item
            name={[field.name, 'unitPrice']}
            noStyle
            rules={[{ required: true, type: 'number', min: 0, message: 'Nhập đơn giá dòng tăng.' }]}
          >
            <InputNumber {...cellNumber} min={0} aria-label={`Đơn giá dòng ${field.name + 1}`} />
          </Form.Item>
        ) : (
          <span
            className="line-grid-readonly"
            title="Dòng giảm xuất theo giá vốn – chốt khi đóng kỳ"
            aria-label={`Đơn giá dòng ${field.name + 1}`}
          >
            Giá vốn
          </span>
        )
      },
    },
    {
      key: 'amount',
      title: 'Thành tiền',
      width: 120,
      align: 'right',
      render: (field) => {
        const line = lines[field.name]
        return <span className="line-grid-readonly">{line?.direction === 1 ? formatMoney(lineAmount(line)) : ''}</span>
      },
    },
    {
      key: 'currency',
      title: 'Tiền tệ',
      width: 60,
      render: (field) => <span className="line-grid-readonly">{lines[field.name]?.direction === 1 ? 'VND' : ''}</span>,
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
      key: 'expiry',
      title: 'Hạn dùng',
      width: 120,
      render: (field) => {
        const line = lines[field.name]
        const item = line?.itemId ? itemsById.get(line.itemId) : undefined
        const increase = line?.direction === 1
        return (
          <Form.Item
            name={[field.name, 'expiryDate']}
            noStyle
            rules={[{ required: increase && item?.trackExpiry === true, message: 'Nhập hạn dùng.' }]}
          >
            <DatePicker
              variant="borderless"
              format={DATE_INPUT_FORMATS}
              style={{ width: '100%' }}
              disabled={!increase}
              aria-label={`Hạn dùng dòng ${field.name + 1}`}
            />
          </Form.Item>
        )
      },
    },
    {
      key: 'note',
      title: 'Ghi chú',
      width: 180,
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
  const increaseTotal = lines.reduce((sum, l) => sum + lineAmount(l), 0)
  const increaseCount = lines.filter((l) => l?.direction === 1).length

  return (
    <DocumentForm
      title="Phiếu điều chỉnh, kiểm kê kho"
      extraTabs={[attachmentTab]}
      number={isNew ? null : (doc?.no ?? '')}
      audit={{ entityType: 'InventoryDocument', id: doc?.id }}
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
          <Form.Item label="Ngày điều chỉnh" name="date" rules={[{ required: true, message: 'Chọn ngày điều chỉnh.' }]} extra="Quyết định kỳ kho khi ghi sổ.">
            <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Kho" name="warehouseId" rules={[{ required: true, message: 'Chọn kho điều chỉnh.' }]}>
            <Select showSearch={{ optionFilterProp: 'label' }} options={warehouseOptions(doc?.toWarehouseId)} />
          </Form.Item>
          <Form.Item
            label="Số biên bản kiểm kê"
            name="countReportNo"
            rules={[{ required: true, whitespace: true, message: 'Nhập số biên bản kiểm kê.' }, { max: 50 }]}
            extra="Đính kèm bản scan ở tab Đính kèm."
          >
            <Input />
          </Form.Item>
          <Form.Item label="Lý do điều chỉnh" name="reason" rules={[{ required: true, whitespace: true, message: 'Nhập lý do điều chỉnh.' }, { max: 500 }]}>
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
          auditEntity="InventoryDocumentLine"
          columns={columns}
          readOnly={readOnly}
          addRef={addRef}
          newLine={() => ({ id: null, direction: -1, qty: 1, unitPrice: 0 })}
        />
      }
      footer={
        <>
          <DocumentTotal label="Số dòng" value={formatQuantity(lines.length)} />
          <DocumentTotal label={`Giá trị tăng (${increaseCount} dòng)`} value={formatMoney(increaseTotal)} strong />
        </>
      }
    />
  )
}
