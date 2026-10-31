import { CheckOutlined, CloseOutlined, EditOutlined, FileAddOutlined, SaveOutlined, SendOutlined, ShoppingCartOutlined, StopOutlined } from '@ant-design/icons'
import { Alert, App, Button, ConfigProvider, DatePicker, Form, Input, InputNumber, Modal, Select, Space, Table, Tag } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router'
import { useItemLookup } from '../../api/inventoryDocuments'
import { useSuppliers, vatOptions } from '../../api/masterData'
import type { OpenRequestLine } from '../../api/purchasing'
import {
  rfqStatusLabels,
  rfqStatusTone,
  useRfq,
  useRfqMutations,
  useRfqs,
  useSourcing,
  type Rfq,
  type RfqListItem,
  type RfqSupplier,
  type SaveQuotationInput,
  type SaveRfqInput,
} from '../../api/sourcing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { DocumentForm } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { ListPage } from '../../components/ListPage/ListPage'
import { formatDate, formatDateTime, formatMoney, formatQuantity, vnNumberInput } from '../../utils/format'
import { RequestLinePicker } from '../purchasing/PurchaseOrderPage'

const LIST_PATH = '/dau-thau/rfq'

const listColumns: GridColumn<RfqListItem>[] = [
  { key: 'no', title: 'Số RFQ', width: 150, value: (r) => r.no },
  { key: 'date', title: 'Ngày', width: 90, type: 'date', value: (r) => r.date },
  { key: 'title', title: 'Tiêu đề', width: 260, value: (r) => r.title },
  { key: 'deadline', title: 'Hạn nộp', width: 90, type: 'date', value: (r) => r.deadline },
  { key: 'lines', title: 'Số dòng', width: 70, type: 'quantity', value: (r) => r.lineCount },
  { key: 'suppliers', title: 'NCC mời', width: 70, type: 'quantity', value: (r) => r.supplierCount },
  { key: 'quoted', title: 'Đã báo giá', width: 80, type: 'quantity', value: (r) => r.quotedCount },
  { key: 'creator', title: 'Người lập', width: 140, value: (r) => r.createdByName, groupable: true },
  { key: 'status', title: 'Trạng thái', width: 130, type: 'status', value: (r) => rfqStatusLabels[r.status], tone: (r) => rfqStatusTone[r.status], groupable: true },
]

/**
 * Danh sách RFQ (design.md mục 8.4). Màn hình So sánh báo giá dùng lại danh sách này, chỉ hiện RFQ đang nhận báo giá / đã chọn thầu và
 * mở thẳng tab so sánh.
 * @param compare true = màn hình So sánh báo giá.
 */
export function RfqListPage({ compare = false }: { compare?: boolean }) {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const rfqs = useRfqs(compare ? 'Sent,Awarded' : undefined)
  return (
    <ListPage
      title={compare ? 'So sánh báo giá' : 'Yêu cầu báo giá (RFQ)'}
      exportFileName={compare ? 'so-sanh-bao-gia' : 'yeu-cau-bao-gia'}
      rows={rfqs.data ?? []}
      columns={listColumns}
      rowKey={(r) => r.id}
      loading={rfqs.isLoading}
      canCreate={!compare && hasPermission(Permission.RfqManage)}
      onCreate={() => navigate(`${LIST_PATH}/moi`)}
      onOpenRow={(r) => navigate(`${LIST_PATH}/${r.id}${compare ? '?tab=compare' : ''}`)}
      notice={compare && <Alert type="info" showIcon title="RFQ đang nhận báo giá hoặc đã chọn thầu. Bấm đúp để mở bảng so sánh." />}
    />
  )
}

interface LineValues {
  id: string | null
  itemId?: string
  uomId?: string
  qty?: number | null
  sourcePrLineId?: string | null
  note?: string
}

interface FormValues {
  date: Dayjs
  title?: string
  deadline?: Dayjs
  sourcingId?: string | null
  priceWeight?: number
  qualityWeight?: number
  deliveryWeight?: number
  paymentWeight?: number
  warrantyWeight?: number
  note?: string
  lines: LineValues[]
  supplierIds: string[]
}

/** Lựa chọn chọn thầu đang soạn của một dòng. */
type AwardDraft = Record<string, { supplierId: string | null; reason: string }>

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Trang RFQ trên khuôn DocumentForm (design.md mục 8.4): đầu phiếu (hạn nộp, phiếu tìm nguồn, trọng số tiêu chí), dòng hàng (nhập tay
 * hoặc lấy từ PR đã duyệt), tab NCC mời (≥ 3 mới gửi được), tab So sánh & chọn thầu (nhập báo giá từng NCC, đánh dấu giá thấp nhất,
 * điểm theo trọng số, chọn NCC theo dòng – không chọn giá thấp nhất phải ghi lý do – rồi sinh PO nháp).
 */
export function RfqPage() {
  const { id = 'moi' } = useParams()
  const [params] = useSearchParams()
  const isNew = id === 'moi'
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<FormValues>()
  const rfq = useRfq(isNew ? null : id)
  const sourcingId = isNew ? params.get('sourcingId') : null
  const source = useSourcing(sourcingId)
  const mutations = useRfqMutations()
  const suppliers = useSuppliers({})
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [picking, setPicking] = useState(false)
  const [quoting, setQuoting] = useState<RfqSupplier | null>(null)
  const [awardEdits, setAwardEdits] = useState<AwardDraft>({})
  const addRef = useRef<(() => void) | null>(null)
  const doc = rfq.data
  const readOnly = isNew ? !hasPermission(Permission.RfqManage) : !doc?.actions.canEdit

  const watchedLines = Form.useWatch('lines', form) as LineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])

  useEffect(() => {
    if (!doc) return
    form.setFieldsValue({
      date: dayjs(doc.date),
      title: doc.title,
      deadline: dayjs(doc.deadline),
      sourcingId: doc.sourcingId,
      priceWeight: doc.weights.price,
      qualityWeight: doc.weights.quality,
      deliveryWeight: doc.weights.delivery,
      paymentWeight: doc.weights.payment,
      warrantyWeight: doc.weights.warranty,
      note: doc.note ?? '',
      lines: doc.lines.map((l) => ({ id: l.id, itemId: l.itemId, uomId: l.uomId, qty: l.qty, sourcePrLineId: l.sourcePrLineId, note: l.note ?? '' })),
      supplierIds: doc.suppliers.map((s) => s.supplierId),
    })
  }, [doc, form])

  // Lựa chọn chọn thầu = mặc định (đã chọn thầu thì theo kết quả; chưa thì NCC giá thấp nhất của dòng) đè bởi các ô người dùng đã sửa.
  const award = useMemo<AwardDraft>(() => {
    if (!doc) return {}
    const defaults = doc.lines.map((l) => {
      const lowest = doc.suppliers.find((s) => s.lines.some((q) => q.rfqLineId === l.id && q.isLowest))
      const supplierId = l.awardedSupplierId ?? (doc.status === 'Sent' ? (lowest?.supplierId ?? null) : null)
      return [l.id, awardEdits[l.id] ?? { supplierId, reason: l.awardReason ?? '' }] as const
    })
    return Object.fromEntries(defaults)
  }, [doc, awardEdits])

  // Lập từ phiếu tìm nguồn: mang sẵn mặt hàng, SL và các NCC ứng viên chưa bị loại.
  useEffect(() => {
    const s = source.data
    if (!s) return
    form.setFieldsValue({
      sourcingId: s.id,
      title: `Báo giá ${s.itemName ?? s.description ?? ''}`.trim(),
      supplierIds: s.candidates.filter((c) => c.status !== 'Failed').map((c) => c.supplierId),
    })
    if (s.itemId) {
      form.setFieldValue('lines', [{ id: null, itemId: s.itemId, uomId: undefined, qty: s.qty ?? 1, sourcePrLineId: null, note: '' }])
    }
  }, [source.data, form])

  // Mặt hàng mang từ phiếu tìm nguồn chưa có ĐVT: gán ĐVT cơ sở khi tra được mặt hàng.
  useEffect(() => {
    lines.forEach((l, index) => {
      if (l?.itemId && !l.uomId && itemsById.get(l.itemId)) form.setFieldValue(['lines', index, 'uomId'], itemsById.get(l.itemId)!.baseUomId)
    })
  }, [lines, itemsById, form])

  const persist = async (): Promise<Rfq | null> => {
    let values: FormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu, dòng hàng và tab NCC mời.')
      return null
    }
    setError(null)
    const input: SaveRfqInput = {
      date: values.date.format('YYYY-MM-DD'),
      title: values.title?.trim() ?? '',
      deadline: values.deadline!.format('YYYY-MM-DD'),
      sourcingId: values.sourcingId ?? null,
      weights: {
        price: values.priceWeight ?? 0,
        quality: values.qualityWeight ?? 0,
        delivery: values.deliveryWeight ?? 0,
        payment: values.paymentWeight ?? 0,
        warranty: values.warrantyWeight ?? 0,
      },
      note: orNull(values.note),
      lines: (values.lines ?? []).map((l) => ({
        id: l.id ?? null, itemId: l.itemId!, uomId: l.uomId!, qty: l.qty ?? 0, sourcePrLineId: l.sourcePrLineId ?? null, note: orNull(l.note),
      })),
      supplierIds: values.supplierIds ?? [],
    }
    try {
      return isNew ? await mutations.create.mutateAsync(input) : await mutations.update.mutateAsync({ id, input })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được RFQ.')
      return null
    }
  }

  const fail = (e: unknown, fallback: string) => setError(e instanceof Error ? e.message : fallback)

  const save = async () => {
    if (readOnly) return
    const saved = await persist()
    if (!saved) return
    message.success(`Đã lưu RFQ ${saved.no}.`)
    if (isNew) navigate(`${LIST_PATH}/${saved.id}`, { replace: true })
  }

  const send = () =>
    modal.confirm({
      title: 'Gửi RFQ cho các NCC?',
      content: 'RFQ chuyển sang Đang nhận báo giá; dòng hàng, NCC mời, trọng số không sửa được nữa.',
      okText: 'Gửi NCC',
      cancelText: 'Không',
      onOk: async () => {
        const saved = await persist()
        if (!saved) return
        try {
          await mutations.send.mutateAsync(saved.id)
          message.success(`Đã gửi RFQ ${saved.no}.`)
        } catch (e) {
          fail(e, 'Không gửi được RFQ.')
        }
        if (isNew) navigate(`${LIST_PATH}/${saved.id}?tab=compare`, { replace: true })
      },
    })

  const confirmAward = () =>
    doc &&
    modal.confirm({
      title: `Chốt kết quả chọn thầu RFQ ${doc.no}?`,
      content: 'Sau khi chốt không nhập thêm báo giá được; từ kết quả có thể tạo đơn mua hàng.',
      okText: 'Chọn thầu',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        try {
          await mutations.award.mutateAsync({
            id: doc.id,
            lines: doc.lines.map((l) => ({ rfqLineId: l.id, supplierId: award[l.id]?.supplierId ?? null, reason: orNull(award[l.id]?.reason) })),
          })
          message.success(`Đã chọn thầu RFQ ${doc.no}.`)
        } catch (e) {
          fail(e, 'Không chọn thầu được.')
        }
      },
    })

  const createOrders = () =>
    doc &&
    modal.confirm({
      title: 'Tạo đơn mua hàng từ kết quả chọn thầu?',
      content: 'Mỗi NCC trúng thầu một PO nháp: giá theo báo giá, kho nhận là kho tổng. Mở PO để kiểm tra rồi gửi duyệt.',
      okText: 'Tạo PO',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        try {
          const created = await mutations.createOrders.mutateAsync(doc.id)
          message.success(`Đã tạo ${created.map((c) => `${c.purchaseOrderNo} (${c.supplierCode})`).join(', ')}.`)
        } catch (e) {
          fail(e, 'Không tạo được đơn mua hàng.')
        }
      },
    })

  const cancel = () =>
    doc &&
    modal.confirm({
      title: `Hủy RFQ ${doc.no}?`,
      okText: 'Hủy RFQ',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        try {
          await mutations.cancel.mutateAsync(doc.id)
          message.success(`Đã hủy RFQ ${doc.no}.`)
        } catch (e) {
          fail(e, 'Không hủy được RFQ.')
        }
      },
    })

  /** Thêm các dòng PR đã chọn (SL = SL còn chưa đặt). */
  const addFromRequests = (picked: OpenRequestLine[]) => {
    const current = (form.getFieldValue('lines') as LineValues[] | undefined) ?? []
    const existing = new Set(current.map((l) => l.sourcePrLineId).filter(Boolean))
    const added = picked
      .filter((p) => !existing.has(p.prLineId))
      .map((p) => ({ id: null, itemId: p.itemId, uomId: p.uomId, qty: p.remainingQty, sourcePrLineId: p.prLineId, note: '' }))
    form.setFieldValue('lines', [...current, ...added])
    setPicking(false)
  }

  const savedLine = (index: number) => (lines[index]?.id ? doc?.lines.find((l) => l.id === lines[index].id) : undefined)

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
      title: 'Số lượng',
      width: 100,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'qty']} noStyle rules={[{ required: true, type: 'number', min: 0.0001, message: 'SL > 0.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={4} aria-label={`Số lượng dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'pr',
      title: 'PR nguồn',
      width: 140,
      render: (_, index) => <span className="line-grid-readonly">{savedLine(index)?.sourcePrNo ?? (lines[index]?.sourcePrLineId ? '(từ PR)' : '')}</span>,
    },
    {
      key: 'note',
      title: 'Yêu cầu, ghi chú',
      width: 260,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle rules={[{ max: 500 }]}>
          <Input variant="borderless" aria-label={`Ghi chú dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? rfqStatusLabels[doc.status] : '', tone: doc ? rfqStatusTone[doc.status] : ('neutral' as const) }

  const actions = (
    <>
      {(isNew ? !readOnly : doc?.actions.canEdit) && (
        <Button type="primary" icon={<SaveOutlined />} onClick={save} loading={mutations.create.isPending || mutations.update.isPending}>
          Lưu nháp
        </Button>
      )}
      {(isNew ? !readOnly : doc?.actions.canSend) && (
        <Button icon={<SendOutlined />} onClick={send} loading={mutations.send.isPending}>
          Gửi NCC
        </Button>
      )}
      {doc?.actions.canAward && (
        <Button type="primary" icon={<CheckOutlined />} onClick={confirmAward} loading={mutations.award.isPending}>
          Chọn thầu
        </Button>
      )}
      {doc?.actions.canCreateOrders && (
        <Button type="primary" icon={<ShoppingCartOutlined />} onClick={createOrders} loading={mutations.createOrders.isPending}>
          Tạo PO
        </Button>
      )}
      {doc?.actions.canCancel && (
        <Button danger icon={<StopOutlined />} onClick={cancel}>
          Hủy RFQ
        </Button>
      )}
      <Button type="text" icon={<CloseOutlined />} onClick={() => navigate(LIST_PATH)}>
        Đóng
      </Button>
    </>
  )

  const supplierOptions = (suppliers.data?.items ?? [])
    .filter((s) => s.status !== 'Blacklisted' || doc?.suppliers.some((x) => x.supplierId === s.id))
    .map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))
  const weight = (name: keyof FormValues, label: string) => (
    <Form.Item label={label} name={name} rules={[{ required: true, type: 'number', min: 0, max: 100, message: '0–100.' }]}>
      <InputNumber {...vnNumberInput} style={{ width: '100%' }} min={0} max={100} precision={0} />
    </Form.Item>
  )

  const extraTabs = [
    {
      key: 'suppliers',
      label: 'NCC mời',
      children: (
        <Form.Item
          label={`Nhà cung cấp được mời (tối thiểu ${doc?.minSuppliers ?? 3} NCC mới gửi được)`}
          name="supplierIds"
          style={{ maxWidth: 900 }}
        >
          <Select mode="multiple" showSearch={{ optionFilterProp: 'label' }} options={supplierOptions} placeholder="Chọn các NCC" aria-label="NCC mời" />
        </Form.Item>
      ),
    },
    ...(doc && doc.status !== 'Draft'
      ? [
          {
            key: 'compare',
            label: 'So sánh & chọn thầu',
            children: (
              // Bảng so sánh vẫn thao tác được (nhập báo giá, chọn NCC) dù đầu phiếu đã khóa sau khi gửi.
              <ConfigProvider componentDisabled={false}>
                <ComparisonView rfq={doc} award={award} onAwardChange={setAwardEdits} onQuote={setQuoting} />
              </ConfigProvider>
            ),
          },
        ]
      : []),
  ]

  return (
    <>
      <DocumentForm
        title="Yêu cầu báo giá (RFQ)"
        number={isNew ? null : (doc?.no ?? '')}
        status={status}
        meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}${doc.sourcingNo ? ` · Từ phiếu tìm nguồn ${doc.sourcingNo}` : ''}`}
        loading={rfq.isLoading}
        error={error ?? (rfq.error ? rfq.error.message : null)}
        notice={doc?.actions.readOnlyReason && <Alert type="info" showIcon title={doc.actions.readOnlyReason} />}
        form={form}
        initialValues={{
          date: dayjs(), deadline: dayjs().add(7, 'day'), priceWeight: 60, qualityWeight: 20, deliveryWeight: 10, paymentWeight: 5, warrantyWeight: 5,
          lines: [], supplierIds: [],
        }}
        readOnly={readOnly}
        onSave={save}
        onAddLine={() => addRef.current?.()}
        actions={actions}
        defaultTab={params.get('tab') === 'compare' ? 'compare' : 'lines'}
        header={
          <>
            <Form.Item label="Ngày lập" name="date" rules={[{ required: true, message: 'Chọn ngày.' }]}>
              <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Tiêu đề" name="title" rules={[{ required: true, message: 'Nhập tiêu đề.' }, { max: 200 }]}>
              <Input />
            </Form.Item>
            <Form.Item label="Hạn nộp báo giá" name="deadline" rules={[{ required: true, message: 'Chọn hạn nộp.' }]}>
              <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
            </Form.Item>
            {weight('priceWeight', 'Trọng số giá (%)')}
            {weight('qualityWeight', 'Trọng số chất lượng (%)')}
            {weight('deliveryWeight', 'Trọng số giao hàng (%)')}
            {weight('paymentWeight', 'Trọng số thanh toán (%)')}
            {weight('warrantyWeight', 'Trọng số bảo hành (%)')}
            <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]}>
              <Input />
            </Form.Item>
            <Form.Item name="sourcingId" hidden>
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
              </Space>
            )}
            <LineGrid name="lines" columns={columns} readOnly={readOnly} addRef={addRef} newLine={() => ({ id: null, qty: 1, sourcePrLineId: null })} />
          </>
        }
        extraTabs={extraTabs}
      />
      {picking && <RequestLinePicker onCancel={() => setPicking(false)} onPick={addFromRequests} />}
      {quoting && doc && <QuotationDialog rfq={doc} supplier={quoting} onClose={() => setQuoting(null)} />}
    </>
  )
}

/**
 * Bảng so sánh báo giá: dòng hàng × NCC (đơn giá, % CK, giá quy về ĐVT cơ sở; giá thấp nhất gắn nhãn), cột chọn NCC trúng và lý do;
 * bảng điểm NCC theo trọng số. Đơn giá theo ĐVT của dòng RFQ, trước VAT.
 */
function ComparisonView({
  rfq,
  award,
  onAwardChange,
  onQuote,
}: {
  rfq: Rfq
  award: AwardDraft
  onAwardChange: (next: AwardDraft) => void
  onQuote: (supplier: RfqSupplier) => void
}) {
  const canAward = rfq.actions.canAward
  const set = (lineId: string, patch: Partial<AwardDraft[string]>) =>
    onAwardChange({ ...award, [lineId]: { supplierId: award[lineId]?.supplierId ?? null, reason: award[lineId]?.reason ?? '', ...patch } })
  const isLowestChoice = (lineId: string) => {
    const chosen = award[lineId]?.supplierId
    return !chosen || rfq.suppliers.some((s) => s.supplierId === chosen && s.lines.some((q) => q.rfqLineId === lineId && q.isLowest))
  }

  const lineColumns = [
    { title: 'STT', key: 'no', width: 45, render: (_: unknown, l: Rfq['lines'][number]) => l.lineNo },
    { title: 'Mặt hàng', key: 'item', width: 220, render: (_: unknown, l: Rfq['lines'][number]) => `${l.itemCode} – ${l.itemName}` },
    { title: 'ĐVT', key: 'uom', width: 60, render: (_: unknown, l: Rfq['lines'][number]) => l.uomCode },
    { title: 'SL', key: 'qty', width: 80, align: 'right' as const, render: (_: unknown, l: Rfq['lines'][number]) => formatQuantity(l.qty) },
    ...rfq.suppliers.map((s) => ({
      title: s.supplierCode,
      key: s.supplierId,
      width: 150,
      align: 'right' as const,
      render: (_: unknown, l: Rfq['lines'][number]) => {
        const q = s.lines.find((x) => x.rfqLineId === l.id)
        if (!q) return <span className="line-grid-readonly">—</span>
        return (
          <div data-testid={`quote-${s.supplierCode}-${l.lineNo}`}>
            {formatMoney(q.unitPrice)}
            {q.discountPct ? ` −${formatQuantity(q.discountPct)}%` : ''}
            {q.isLowest && (
              <Tag color="success" style={{ marginLeft: 4, marginRight: 0 }}>
                Thấp nhất
              </Tag>
            )}
          </div>
        )
      },
    })),
    {
      title: 'NCC trúng',
      key: 'award',
      width: 150,
      render: (_: unknown, l: Rfq['lines'][number]) =>
        canAward ? (
          <Select
            size="small"
            style={{ width: '100%' }}
            allowClear
            aria-label={`NCC trúng dòng ${l.lineNo}`}
            value={award[l.id]?.supplierId ?? undefined}
            onChange={(v?: string) => set(l.id, { supplierId: v ?? null })}
            options={rfq.suppliers.filter((s) => s.lines.some((q) => q.rfqLineId === l.id)).map((s) => ({ value: s.supplierId, label: s.supplierCode }))}
          />
        ) : (
          (rfq.suppliers.find((s) => s.supplierId === l.awardedSupplierId)?.supplierCode ?? '')
        ),
    },
    {
      title: 'Lý do chọn',
      key: 'reason',
      width: 220,
      render: (_: unknown, l: Rfq['lines'][number]) =>
        canAward ? (
          <Input
            size="small"
            aria-label={`Lý do dòng ${l.lineNo}`}
            value={award[l.id]?.reason ?? ''}
            status={!isLowestChoice(l.id) && !award[l.id]?.reason?.trim() ? 'error' : undefined}
            placeholder={isLowestChoice(l.id) ? '' : 'Bắt buộc – không chọn giá thấp nhất'}
            onChange={(e) => set(l.id, { reason: e.target.value })}
          />
        ) : (
          l.awardReason
        ),
    },
  ]

  type Metric = { key: string; label: string; value: (s: RfqSupplier) => React.ReactNode }
  const metrics: Metric[] = [
    { key: 'quoted', label: 'Báo giá ngày', value: (s) => (s.hasQuoted ? formatDate(s.quoteDate) : 'Chưa báo giá') },
    { key: 'lines', label: 'Số dòng chào', value: (s) => `${s.quotedLineCount}/${rfq.lines.length}` },
    { key: 'payment', label: 'Thanh toán', value: (s) => s.paymentTerms },
    { key: 'priceScore', label: `Điểm giá (${rfq.weights.price}%)`, value: (s) => formatQuantity(s.priceScore) },
    { key: 'quality', label: `Chất lượng (${rfq.weights.quality}%)`, value: (s) => formatQuantity(s.qualityScore) },
    { key: 'delivery', label: `Giao hàng (${rfq.weights.delivery}%)`, value: (s) => formatQuantity(s.deliveryScore) },
    { key: 'paymentScore', label: `Thanh toán (${rfq.weights.payment}%)`, value: (s) => formatQuantity(s.paymentScore) },
    { key: 'warranty', label: `Bảo hành (${rfq.weights.warranty}%)`, value: (s) => formatQuantity(s.warrantyScore) },
    { key: 'total', label: 'Tổng điểm (thang 10)', value: (s) => <strong>{formatQuantity(s.totalScore)}</strong> },
    { key: 'awarded', label: 'Giá trị trúng thầu', value: (s) => (s.awardedAmount ? formatMoney(s.awardedAmount) : '') },
    { key: 'po', label: 'Đơn mua hàng', value: (s) => s.purchaseOrderNo },
    ...(rfq.actions.canQuote
      ? [
          {
            key: 'action',
            label: '',
            value: (s: RfqSupplier) => (
              <Button size="small" icon={<EditOutlined />} onClick={() => onQuote(s)} aria-label={`Nhập báo giá ${s.supplierCode}`}>
                Nhập báo giá
              </Button>
            ),
          },
        ]
      : []),
  ]

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      <Table
        size="small"
        bordered
        rowKey="id"
        pagination={false}
        dataSource={rfq.lines}
        columns={lineColumns}
        scroll={{ x: 'max-content' }}
        aria-label="Bảng so sánh báo giá"
      />
      <Table
        size="small"
        bordered
        rowKey="key"
        pagination={false}
        dataSource={metrics}
        scroll={{ x: 'max-content' }}
        aria-label="Điểm nhà cung cấp"
        columns={[
          { title: 'Tiêu chí', key: 'label', width: 180, render: (_: unknown, m: Metric) => m.label },
          ...rfq.suppliers.map((s) => ({
            title: `${s.supplierCode} – ${s.supplierName}`,
            key: s.supplierId,
            width: 180,
            render: (_: unknown, m: Metric) => m.value(s),
          })),
        ]}
      />
    </div>
  )
}

interface QuoteLineValues {
  rfqLineId: string
  unitPrice?: number | null
  discountPct?: number | null
  vatCode?: string | null
  leadTimeDays?: number | null
}

interface QuoteFormValues {
  quoteDate: Dayjs
  validUntil?: Dayjs | null
  paymentTerms?: string
  qualityScore: number
  deliveryScore: number
  paymentScore: number
  warrantyScore: number
  note?: string
  lines: QuoteLineValues[]
}

/**
 * Hộp nhập báo giá của một NCC: ngày, hiệu lực, thanh toán, điểm các tiêu chí ngoài giá (0–10) và đơn giá từng dòng (để trống = không chào).
 * @param rfq RFQ.
 * @param supplier NCC đang nhập.
 * @param onClose Đóng hộp.
 */
function QuotationDialog({ rfq, supplier, onClose }: { rfq: Rfq; supplier: RfqSupplier; onClose: () => void }) {
  const [form] = Form.useForm<QuoteFormValues>()
  const { message } = App.useApp()
  const mutations = useRfqMutations()
  const [error, setError] = useState<string | null>(null)

  const submit = async () => {
    const values = await form.validateFields()
    const input: SaveQuotationInput = {
      quoteDate: values.quoteDate.format('YYYY-MM-DD'),
      validUntil: values.validUntil ? values.validUntil.format('YYYY-MM-DD') : null,
      paymentTerms: orNull(values.paymentTerms),
      qualityScore: values.qualityScore ?? 0,
      deliveryScore: values.deliveryScore ?? 0,
      paymentScore: values.paymentScore ?? 0,
      warrantyScore: values.warrantyScore ?? 0,
      note: orNull(values.note),
      lines: values.lines
        .filter((l) => l.unitPrice != null)
        .map((l) => ({ rfqLineId: l.rfqLineId, unitPrice: l.unitPrice!, discountPct: l.discountPct ?? 0, vatCode: l.vatCode ?? null, leadTimeDays: l.leadTimeDays ?? null, note: null })),
    }
    try {
      await mutations.quote.mutateAsync({ id: rfq.id, supplierId: supplier.supplierId, input })
      message.success(`Đã lưu báo giá của ${supplier.supplierCode}.`)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được báo giá.')
    }
  }

  const score = (name: keyof QuoteFormValues, label: string) => (
    <Form.Item label={label} name={name} rules={[{ required: true, type: 'number', min: 0, max: 10, message: '0–10.' }]} style={{ width: 120 }}>
      <InputNumber {...vnNumberInput} style={{ width: '100%' }} min={0} max={10} precision={2} />
    </Form.Item>
  )

  return (
    <Modal
      open
      width={980}
      title={`Báo giá của ${supplier.supplierCode} – ${supplier.supplierName}`}
      okText="Lưu báo giá"
      cancelText="Đóng"
      onOk={submit}
      onCancel={onClose}
      confirmLoading={mutations.quote.isPending}
    >
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form
        form={form}
        layout="vertical"
        requiredMark={false}
        initialValues={{
          quoteDate: supplier.quoteDate ? dayjs(supplier.quoteDate) : dayjs(),
          validUntil: supplier.validUntil ? dayjs(supplier.validUntil) : null,
          paymentTerms: supplier.paymentTerms ?? '',
          qualityScore: supplier.hasQuoted ? supplier.qualityScore : 5,
          deliveryScore: supplier.hasQuoted ? supplier.deliveryScore : 5,
          paymentScore: supplier.hasQuoted ? supplier.paymentScore : 5,
          warrantyScore: supplier.hasQuoted ? supplier.warrantyScore : 5,
          note: supplier.note ?? '',
          lines: rfq.lines.map((l) => {
            const q = supplier.lines.find((x) => x.rfqLineId === l.id)
            return { rfqLineId: l.id, unitPrice: q?.unitPrice ?? null, discountPct: q?.discountPct ?? 0, vatCode: q?.vatCode ?? null, leadTimeDays: q?.leadTimeDays ?? null }
          }),
        }}
      >
        <Space wrap align="start">
          <Form.Item label="Ngày báo giá" name="quoteDate" rules={[{ required: true, message: 'Chọn ngày.' }]}>
            <DatePicker format="DD/MM/YYYY" />
          </Form.Item>
          <Form.Item label="Hiệu lực đến" name="validUntil">
            <DatePicker format="DD/MM/YYYY" />
          </Form.Item>
          <Form.Item label="Điều kiện thanh toán" name="paymentTerms" rules={[{ max: 200 }]} style={{ width: 260 }}>
            <Input />
          </Form.Item>
          {score('qualityScore', 'Điểm chất lượng')}
          {score('deliveryScore', 'Điểm giao hàng')}
          {score('paymentScore', 'Điểm thanh toán')}
          {score('warrantyScore', 'Điểm bảo hành')}
        </Space>
        <Table
          size="small"
          bordered
          rowKey="id"
          pagination={false}
          dataSource={rfq.lines}
          columns={[
            { title: 'Mặt hàng', key: 'item', render: (_, l) => `${l.itemCode} – ${l.itemName}` },
            { title: 'ĐVT', key: 'uom', width: 60, render: (_, l) => l.uomCode },
            { title: 'SL', key: 'qty', width: 80, align: 'right', render: (_, l) => formatQuantity(l.qty) },
            {
              title: 'Đơn giá',
              key: 'price',
              width: 130,
              render: (_, l, index) => (
                <>
                  <Form.Item name={['lines', index, 'rfqLineId']} hidden>
                    <Input />
                  </Form.Item>
                  <Form.Item name={['lines', index, 'unitPrice']} noStyle>
                    <InputNumber {...vnNumberInput} style={{ width: '100%' }} min={0} placeholder="Không chào" aria-label={`Đơn giá báo giá dòng ${l.lineNo}`} />
                  </Form.Item>
                </>
              ),
            },
            {
              title: '% CK',
              key: 'discount',
              width: 80,
              render: (_, l, index) => (
                <Form.Item name={['lines', index, 'discountPct']} noStyle>
                  <InputNumber {...vnNumberInput} style={{ width: '100%' }} min={0} max={100} aria-label={`Chiết khấu báo giá dòng ${l.lineNo}`} />
                </Form.Item>
              ),
            },
            {
              title: 'VAT',
              key: 'vat',
              width: 90,
              render: (_, l, index) => (
                <Form.Item name={['lines', index, 'vatCode']} noStyle>
                  <Select style={{ width: '100%' }} allowClear placeholder="Theo hàng" aria-label={`VAT báo giá dòng ${l.lineNo}`}
                    options={vatOptions.map((o) => ({ value: o.value, label: o.value === 'KCT' ? 'KCT' : o.label }))} />
                </Form.Item>
              ),
            },
            {
              title: 'Giao (ngày)',
              key: 'lead',
              width: 90,
              render: (_, l, index) => (
                <Form.Item name={['lines', index, 'leadTimeDays']} noStyle>
                  <InputNumber style={{ width: '100%' }} min={0} max={365} precision={0} aria-label={`Thời gian giao dòng ${l.lineNo}`} />
                </Form.Item>
              ),
            },
          ]}
        />
        <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]} style={{ marginTop: 8 }}>
          <Input />
        </Form.Item>
      </Form>
    </Modal>
  )
}
