import { Alert, DatePicker, Form, Input, InputNumber, Select } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useParams } from 'react-router'
import { useItemLookup, useSupplierLookup } from '../../api/inventoryDocuments'
import { useWarehouses } from '../../api/organization'
import {
  priorityLabels,
  purchaseStatusLabels,
  purchaseStatusTone,
  usePurchaseMutations,
  usePurchaseRequest,
  type PurchasePriority,
  type PurchaseRequest,
  type SavePurchaseRequestInput,
} from '../../api/purchasing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DocumentForm, DocumentTotal } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { formatDateTime, formatMoney, formatQuantity, vnNumberInput } from '../../utils/format'
import { ApprovalHistory } from './ApprovalHistory'
import { useApprovalCommands } from './useApprovalCommands'

interface PrLineValues {
  id: string | null
  itemId?: string
  uomId?: string
  qty?: number | null
  suggestedSupplierId?: string
  neededDate?: Dayjs | null
  note?: string
}

interface PrFormValues {
  date: Dayjs
  department?: string
  purpose?: string
  neededDate?: Dayjs | null
  warehouseId?: string
  priority: PurchasePriority
  note?: string
  lines: PrLineValues[]
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Trang đề nghị mua hàng (design.md mục 10.2) trên khuôn DocumentForm. Đơn giá tham khảo = giá nhập gần nhất do server lấy khi lưu
 * (chỉ đọc); tồn hiện tại chỉ để xem. Gửi duyệt theo quy trình duyệt của công ty; tab Lịch sử duyệt.
 */
export function PurchaseRequestPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<PrFormValues>()
  const request = usePurchaseRequest(isNew ? null : id)
  const mutations = usePurchaseMutations<PurchaseRequest, SavePurchaseRequestInput>('purchase-requests')
  const warehouses = useWarehouses()
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const addRef = useRef<(() => void) | null>(null)
  const doc = request.data
  const readOnly = isNew ? !hasPermission(Permission.PrCreate) : !doc?.actions.canEdit

  const watchedLines = Form.useWatch('lines', form) as PrLineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])
  const supplierIds = useMemo(() => (doc?.lines ?? []).map((l) => l.suggestedSupplierId).filter((x): x is string => !!x), [doc])
  const suppliers = useSupplierLookup(supplierIds)

  useEffect(() => {
    if (!doc) return
    form.setFieldsValue({
      date: dayjs(doc.date),
      department: doc.department ?? '',
      purpose: doc.purpose ?? '',
      neededDate: doc.neededDate ? dayjs(doc.neededDate) : null,
      warehouseId: doc.warehouseId ?? undefined,
      priority: doc.priority,
      note: doc.note ?? '',
      lines: doc.lines.map((l) => ({
        id: l.id, itemId: l.itemId, uomId: l.uomId, qty: l.qty, suggestedSupplierId: l.suggestedSupplierId ?? undefined,
        neededDate: l.neededDate ? dayjs(l.neededDate) : null, note: l.note ?? '',
      })),
    })
  }, [doc, form])

  const persist = async (): Promise<PurchaseRequest | null> => {
    let values: PrFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng hàng.')
      return null
    }
    setError(null)
    const input: SavePurchaseRequestInput = {
      date: values.date.format('YYYY-MM-DD'),
      department: orNull(values.department),
      purpose: orNull(values.purpose),
      neededDate: values.neededDate ? values.neededDate.format('YYYY-MM-DD') : null,
      warehouseId: values.warehouseId ?? null,
      priority: values.priority,
      note: orNull(values.note),
      lines: (values.lines ?? []).map((l) => ({
        id: l.id ?? null, itemId: l.itemId!, uomId: l.uomId!, qty: l.qty ?? 0, suggestedSupplierId: l.suggestedSupplierId ?? null,
        neededDate: l.neededDate ? l.neededDate.format('YYYY-MM-DD') : null, note: orNull(l.note),
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
    isNew, doc, readOnly, persist, mutations, listPath: '/mua-hang/pr', noun: 'đề nghị mua hàng', setError,
  })

  /** Dòng đã lưu tương ứng (để hiện giá tham khảo, tồn, SL đã đặt do server tính). */
  const savedLine = (index: number) => (lines[index]?.id ? doc?.lines.find((l) => l.id === lines[index].id) : undefined)
  const readonlyCell = (text: string) => <span className="line-grid-readonly">{text}</span>

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
      title: 'SL đề nghị', required: true,
      width: 100,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'qty']} noStyle rules={[{ required: true, type: 'number', min: 0.0001, message: 'SL > 0.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={4} aria-label={`SL đề nghị dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    { key: 'onHand', title: 'Tồn hiện tại', width: 95, align: 'right', render: (_, index) => readonlyCell(savedLine(index) ? formatQuantity(savedLine(index)!.onHandQty) : '') },
    { key: 'price', title: 'Giá tham khảo', width: 115, align: 'right', render: (_, index) => readonlyCell(savedLine(index) ? formatMoney(savedLine(index)!.estPrice) : 'Tự lấy khi lưu') },
    { key: 'amount', title: 'Thành tiền dự kiến', width: 125, align: 'right', render: (_, index) => readonlyCell(savedLine(index) ? formatMoney(savedLine(index)!.amount) : '') },
    {
      key: 'supplier',
      title: 'NCC đề xuất',
      width: 170,
      render: (field) => (
        <Form.Item name={[field.name, 'suggestedSupplierId']} noStyle>
          <Select
            variant="borderless"
            style={{ width: '100%' }}
            allowClear
            aria-label={`NCC đề xuất dòng ${field.name + 1}`}
            showSearch={{ optionFilterProp: 'label' }}
            options={(suppliers.data ?? []).map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))}
            popupMatchSelectWidth={320}
          />
        </Form.Item>
      ),
    },
    {
      key: 'needed',
      title: 'Ngày cần',
      width: 115,
      render: (field) => (
        <Form.Item name={[field.name, 'neededDate']} noStyle>
          <DatePicker variant="borderless" format="DD/MM/YYYY" style={{ width: '100%' }} aria-label={`Ngày cần dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    { key: 'ordered', title: 'Đã đặt', width: 80, align: 'right', render: (_, index) => readonlyCell(savedLine(index)?.orderedQty ? formatQuantity(savedLine(index)!.orderedQty) : '') },
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
    : { label: doc ? purchaseStatusLabels[doc.status] : '', tone: doc ? purchaseStatusTone[doc.status] : ('neutral' as const) }

  return (
    <DocumentForm
      title="Đề nghị mua hàng"
      number={isNew ? null : (doc?.no ?? '')}
      printPath={doc ? `/api/purchase-requests/${doc.id}/pdf` : undefined}
      status={status}
      meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
      loading={request.isLoading}
      error={error ?? (request.error ? request.error.message : null)}
      notice={
        doc?.rejectReason && doc.status === 'Draft' ? (
          <Alert type="warning" showIcon title={`Bị từ chối: ${doc.rejectReason}`} />
        ) : (
          doc?.actions.readOnlyReason && <Alert type="info" showIcon title={doc.actions.readOnlyReason} />
        )
      }
      form={form}
      initialValues={{ date: dayjs(), priority: 'Normal', lines: [] }}
      readOnly={readOnly}
      onSave={save}
      onAddLine={() => addRef.current?.()}
      actions={actions}
      header={
        <>
          <Form.Item label="Ngày đề nghị" name="date" rules={[{ required: true, message: 'Chọn ngày.' }]}>
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Bộ phận đề nghị" name="department" rules={[{ max: 200 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Mức ưu tiên" name="priority">
            <Select options={Object.entries(priorityLabels).map(([value, label]) => ({ value, label }))} />
          </Form.Item>
          <Form.Item label="Mục đích sử dụng" name="purpose" rules={[{ max: 500 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Ngày cần hàng" name="neededDate">
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Kho nhận dự kiến" name="warehouseId">
            <Select allowClear showSearch={{ optionFilterProp: 'label' }}
              options={(warehouses.data ?? []).filter((w) => w.isActive || w.id === doc?.warehouseId).map((w) => ({ value: w.id, label: `${w.code} – ${w.name}` }))} />
          </Form.Item>
          <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]} className="document-form-wide">
            <Input />
          </Form.Item>
        </>
      }
      lines={<LineGrid name="lines" columns={columns} readOnly={readOnly} addRef={addRef} newLine={() => ({ id: null, qty: 1 })} />}
      footer={<DocumentTotal label="Tổng giá trị dự kiến" value={formatMoney(doc?.totalAmount ?? 0)} strong />}
      extraTabs={doc ? [{ key: 'approvals', label: 'Lịch sử duyệt', children: <ApprovalHistory steps={doc.approvals} /> }] : undefined}
    />
  )
}
