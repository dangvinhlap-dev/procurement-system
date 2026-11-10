import {
  AuditOutlined, CheckCircleOutlined, CloseOutlined, DiffOutlined, FileDoneOutlined, SafetyCertificateOutlined, SaveOutlined, StopOutlined,
} from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Flex, Form, Input, InputNumber, Modal, Radio } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router'
import type { DateRange } from '../../api/logs'
import { usePurchaseOrder, useReceivableOrders } from '../../api/purchasing'
import {
  receiptAcceptanceLabels,
  receiptCheckResultLabels,
  receiptConfirmationStatusLabels,
  receiptConfirmationStatusTone,
  useReceiptConfirmation,
  useReceiptConfirmationMutations,
  useReceiptConfirmations,
  type ReceiptCheckResult,
  type ReceiptConfirmation,
  type ReceiptConfirmationListItem,
  type ReceiptConfirmationStatus,
  type SaveReceiptConfirmationInput,
} from '../../api/receiptConfirmations'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { Select } from '../../classic/Select'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { DataGrid } from '../../components/DataGrid/DataGrid'
import type { GridColumn } from '../../components/DataGrid/types'
import { DocumentForm, DocumentTotal } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { useDocumentFormSync } from '../../components/DocumentForm/useDocumentFormSync'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { DATE_INPUT_FORMATS, formatDateTime, formatMoney, formatQuantity, vnNumberInput } from '../../utils/format'
import { LogDateRangeFilter } from '../system/logFilters'

/** Đường dẫn màn hình XNH (screens.tsx). */
export const RECEIPT_PATH = '/mua-hang/xac-nhan-nhan-hang'

/** Chuỗi rỗng hoặc toàn khoảng trắng gửi lên API là null; còn lại bỏ khoảng trắng hai đầu. */
const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/** Ô số trong lưới dòng: không viền, căn phải, kiểu số Việt Nam. */
const cellNumber = { ...vnNumberInput, variant: 'borderless', style: { width: '100%' } } as const

const listColumns: GridColumn<ReceiptConfirmationListItem>[] = [
  { key: 'no', title: 'Số phiếu', width: 150, value: (r) => r.no },
  { key: 'date', title: 'Ngày nhận', width: 90, type: 'date', value: (r) => r.date },
  { key: 'po', title: 'Đơn mua hàng', width: 150, value: (r) => r.purchaseOrderNo },
  { key: 'supplier', title: 'Nhà cung cấp', width: 240, value: (r) => `${r.supplierCode} – ${r.supplierName}`, groupable: true },
  { key: 'warehouse', title: 'Nơi nhận', width: 90, value: (r) => r.warehouseCode, groupable: true },
  { key: 'delivery', title: 'Phiếu giao NCC', width: 110, value: (r) => r.deliveryNoteNo },
  { key: 'invoice', title: 'Hóa đơn', width: 100, value: (r) => r.invoiceNo },
  { key: 'amount', title: 'Tiền thực nhận (gồm VAT)', width: 140, type: 'money', value: (r) => r.totalWithVat },
  {
    key: 'status', title: 'Trạng thái', width: 110, type: 'status', groupable: true,
    value: (r) => receiptConfirmationStatusLabels[r.status], tone: (r) => receiptConfirmationStatusTone[r.status],
  },
  {
    key: 'acceptance', title: 'Nghiệm thu', width: 110, type: 'status', groupable: true,
    value: (r) => receiptAcceptanceLabels[r.acceptance],
    tone: (r) => (r.acceptance === 'Accepted' ? 'success' : r.acceptance === 'Disputed' ? 'error' : 'neutral'),
  },
  { key: 'creator', title: 'Người lập', width: 140, value: (r) => r.createdByName, groupable: true },
]

/**
 * Màn hình Mua hàng → Xác nhận nhận hàng (XNH – lát K2, design.md mục 19.4): thay phiếu nhập kho từ bản 0.22.0. Thủ kho nơi nhận (hàng) hoặc
 * phòng ban đề xuất (dịch vụ) lập theo PO đã duyệt; đơn vị đặt hàng nghiệm thu.
 */
export function ReceiptConfirmationListPage() {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const [range, setRange] = useState<DateRange>(() => ({ from: dayjs().subtract(29, 'day').format('YYYY-MM-DD'), to: dayjs().format('YYYY-MM-DD') }))
  const [status, setStatus] = useState<ReceiptConfirmationStatus | undefined>()
  const list = useReceiptConfirmations({ ...range, status })
  return (
    <ListPage
      title="Xác nhận nhận hàng (XNH)"
      exportFileName="xac-nhan-nhan-hang"
      rows={list.data?.items ?? []}
      columns={listColumns}
      rowKey={(r) => r.id}
      auditEntity="GoodsReceiptConfirmation"
      loading={list.isLoading}
      canCreate={hasPermission(Permission.ReceiptManage)}
      onCreate={() => navigate(`${RECEIPT_PATH}/moi`)}
      onOpenRow={(r) => navigate(`${RECEIPT_PATH}/${r.id}`)}
      onClearFilters={() => setStatus(undefined)}
      // Việc còn phải làm: phiếu nháp chưa xác nhận, phiếu đã xác nhận chưa nghiệm thu.
      isMarked={(r) => r.status === 'Draft' || (r.status === 'Confirmed' && r.acceptance !== 'Accepted')}
      notice={list.data?.truncated && <Alert type="warning" showIcon title="Còn phiếu chưa hiện – thu hẹp khoảng ngày." />}
      filters={
        <>
          <LogDateRangeFilter value={range} onChange={setRange} />
          <FilterField label="Trạng thái" width={130}>
            {(id) => (
              <Select id={id} value={status ?? ''} onChange={(v) => setStatus(v === '' ? undefined : (v as ReceiptConfirmationStatus))}
                options={[{ value: '', label: 'Tất cả' }, ...Object.entries(receiptConfirmationStatusLabels).map(([value, label]) => ({ value, label }))]} />
            )}
          </FilterField>
        </>
      }
    />
  )
}

interface LineValues {
  purchaseOrderLineId: string
  poLineNo: number
  itemCode: string
  itemName: string
  uomCode: string
  orderedQty: number
  otherReceivedQty: number
  unitPrice: number
  vatCode: string | null
  deliveredQty?: number | null
  acceptedQty?: number | null
  rejectedQty?: number | null
  adjustedQty?: number
  checkResult?: ReceiptCheckResult
  checkNote?: string
  note?: string
}

interface FormValues {
  purchaseOrderId?: string
  date: Dayjs
  deliveryNoteNo?: string
  deliverer?: string
  invoiceNo?: string
  invoiceSeries?: string
  invoiceDate?: Dayjs | null
  invoiceAmount?: number | null
  note?: string
  lines: LineValues[]
}

/**
 * Tiền một dòng như server (GoodsReceiptConfirmationLine): SL thực nhận × giá thực mua, VAT theo thuế suất dòng PO.
 * Ví dụ đạt 8 × 95.000, VAT 10% → 760.000; 76.000.
 */
function lineMoney(line: LineValues | undefined) {
  const qty = (line?.acceptedQty ?? 0) + (line?.adjustedQty ?? 0)
  const amount = Math.round(qty * (line?.unitPrice ?? 0) * 100) / 100
  const rate = line?.vatCode && line.vatCode !== 'KCT' ? Number(line.vatCode) : 0
  return { amount, vat: Math.round(amount * rate) / 100 }
}

/**
 * Trang phiếu xác nhận nhận hàng XNH (DocumentForm + LineGrid – design.md mục 10.1, 19.4): lập mới (/…/moi, có thể kèm ?po=Id) hoặc mở
 * phiếu đã có. Dòng lấy từ dòng PO còn lại; nháp sửa được, xác nhận thì SL thực nhận tính vào PO.
 */
export function ReceiptConfirmationPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<FormValues>()
  const document = useReceiptConfirmation(isNew ? null : id)
  const mutations = useReceiptConfirmationMutations()
  const [error, setError] = useState<string | null>(null)
  const [dialog, setDialog] = useState<'invoice' | 'acceptance' | 'adjust' | null>(null)
  const doc = document.data
  const readOnly = isNew ? !hasPermission(Permission.ReceiptManage) : !doc?.actions.canEdit
  const attachmentTab = useAttachmentTab('GoodsReceiptConfirmation', doc?.id, hasPermission(Permission.ReceiptManage) && doc?.status !== 'Cancelled')
  // preserve: lấy cả trường chỉ hiển thị (mã hàng, SL đặt…) không gắn ô nhập – mặc định useWatch chỉ trả ô đã gắn Form.Item.
  const watchedLines = Form.useWatch('lines', { form, preserve: true }) as LineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const purchaseOrderId = Form.useWatch('purchaseOrderId', form) as string | undefined
  const receivable = useReceivableOrders(isNew && !readOnly)
  const order = usePurchaseOrder(isNew ? (purchaseOrderId ?? null) : null)

  const { loadSaved, targetId } = useDocumentFormSync(form, id, doc, (d) => ({
    purchaseOrderId: d.purchaseOrderId,
    date: dayjs(d.date),
    deliveryNoteNo: d.deliveryNoteNo ?? '',
    deliverer: d.deliverer ?? '',
    invoiceNo: d.invoiceNo ?? '',
    invoiceSeries: d.invoiceSeries ?? '',
    invoiceDate: d.invoiceDate ? dayjs(d.invoiceDate) : null,
    invoiceAmount: d.invoiceAmount ?? null,
    note: d.note ?? '',
    lines: d.lines.map((l) => ({
      purchaseOrderLineId: l.purchaseOrderLineId, poLineNo: l.poLineNo, itemCode: l.itemCode, itemName: l.itemName, uomCode: l.uomCode,
      orderedQty: l.orderedQty, otherReceivedQty: l.otherReceivedQty, unitPrice: l.unitPrice, vatCode: l.vatCode,
      deliveredQty: l.deliveredQty, acceptedQty: l.acceptedQty, rejectedQty: l.rejectedQty, adjustedQty: l.adjustedQty,
      checkResult: l.checkResult, checkNote: l.checkNote ?? '', note: l.note ?? '',
    })),
  }))

  // Mở từ nút "Lập XNH" trên PO (?po=Id): chọn sẵn đơn.
  const poFromUrl = searchParams.get('po')
  useEffect(() => {
    if (isNew && poFromUrl && !form.getFieldValue('purchaseOrderId')) form.setFieldValue('purchaseOrderId', poFromUrl)
  }, [isNew, poFromUrl, form])

  /** Điền mọi dòng PO còn lại: SL phiếu giao = SL đạt = còn lại; kết quả kiểm tra để người nhận chọn. */
  const takeOrderLines = () =>
    form.setFieldValue(
      'lines',
      (order.data?.lines ?? [])
        .filter((l) => l.remainingQty > 0)
        .map((l): LineValues => ({
          purchaseOrderLineId: l.id, poLineNo: l.lineNo, itemCode: l.itemCode, itemName: l.itemName, uomCode: l.uomCode, orderedQty: l.qty,
          otherReceivedQty: l.receivedQty, vatCode: l.vatCode,
          // Giá thực mua sau chiết khấu (server tính lại khi lưu), ví dụ 100.000 − 5% = 95.000.
          unitPrice: Math.round(l.unitPrice * (100 - l.discountPct) * 10_000) / 1_000_000,
          deliveredQty: l.remainingQty, acceptedQty: l.remainingQty, rejectedQty: 0, adjustedQty: 0, checkResult: 'NotChecked', checkNote: '', note: '',
        })),
    )

  // PO vừa chọn khi lập mới: tự lấy dòng còn lại (đổi PO thì thay dòng).
  useEffect(() => {
    if (isNew && !readOnly && order.data && order.data.id === purchaseOrderId) takeOrderLines()
    // eslint-disable-next-line react-hooks/exhaustive-deps -- chỉ chạy khi PO tải xong
  }, [order.data?.id])

  const totals = lines.reduce((sum, l) => {
    const m = lineMoney(l)
    return { amount: sum.amount + m.amount, vat: sum.vat + m.vat }
  }, { amount: 0, vat: 0 })

  /** Lưu nháp; trả về phiếu đã lưu, null nếu ô chưa hợp lệ hoặc server báo lỗi (lỗi hiện trên phiếu). */
  const persist = async (): Promise<ReceiptConfirmation | null> => {
    let values: FormValues
    try {
      await form.validateFields()
      // Lấy cả trường không có ô nhập (Id dòng PO, mã hàng… chỉ hiển thị) – validateFields chỉ trả các ô đã gắn Form.Item.
      values = form.getFieldsValue(true) as FormValues
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng hàng.')
      return null
    }
    setError(null)
    const input: SaveReceiptConfirmationInput = {
      purchaseOrderId: values.purchaseOrderId!,
      date: values.date.format('YYYY-MM-DD'),
      deliveryNoteNo: orNull(values.deliveryNoteNo),
      deliverer: orNull(values.deliverer),
      note: orNull(values.note),
      invoiceNo: orNull(values.invoiceNo),
      invoiceSeries: orNull(values.invoiceSeries),
      invoiceDate: values.invoiceDate ? values.invoiceDate.format('YYYY-MM-DD') : null,
      invoiceAmount: values.invoiceAmount ?? null,
      // Dòng không giao gì (cả ba SL = 0) không lưu – NCC giao các dòng đó ở lần sau.
      lines: (values.lines ?? [])
        .filter((l) => (l.deliveredQty ?? 0) > 0 || (l.acceptedQty ?? 0) > 0 || (l.rejectedQty ?? 0) > 0)
        .map((l) => ({
          purchaseOrderLineId: l.purchaseOrderLineId, deliveredQty: l.deliveredQty ?? 0, acceptedQty: l.acceptedQty ?? 0,
          rejectedQty: l.rejectedQty ?? 0, checkResult: l.checkResult ?? 'NotChecked', checkNote: orNull(l.checkNote), note: orNull(l.note),
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

  const save = async () => {
    if (readOnly) return
    const saved = await persist()
    if (!saved) return
    message.success(`Đã lưu phiếu ${saved.no}.`)
    if (isNew) navigate(`${RECEIPT_PATH}/${saved.id}`, { replace: true })
  }

  const confirmReceipt = () =>
    modal.confirm({
      title: 'Xác nhận nhận hàng?',
      content: 'SL đạt tính vào SL đã nhận của đơn mua hàng; nhận đủ mọi dòng thì đơn chuyển Đã nhận đủ. Sau khi xác nhận không sửa dòng được – '
        + 'sai SL thì dùng Điều chỉnh SL thực nhận (có lý do).',
      okText: 'Xác nhận',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        const saved = readOnly ? doc : await persist()
        if (!saved) return
        try {
          const confirmed = await mutations.confirm.mutateAsync(saved.id)
          message.success(`Đã xác nhận nhận hàng phiếu ${confirmed.no}.`)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không xác nhận được phiếu.')
        }
        if (isNew) navigate(`${RECEIPT_PATH}/${saved.id}`, { replace: true })
      },
    })

  const cancel = () => {
    if (!doc) return
    let reason = ''
    modal.confirm({
      title: `Hủy phiếu ${doc.no}?`,
      content: (
        <Input.TextArea rows={2} maxLength={500} placeholder="Lý do hủy (bắt buộc)" aria-label="Lý do hủy" onChange={(e) => { reason = e.target.value }} />
      ),
      okText: 'Hủy phiếu',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        if (!reason.trim()) {
          message.error('Hãy nhập lý do hủy.')
          throw new Error('missing reason')
        }
        try {
          await mutations.cancel.mutateAsync({ id: doc.id, reason: reason.trim() })
          message.success(`Đã hủy phiếu ${doc.no}.`)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không hủy được phiếu.')
        }
      },
    })
  }

  const actions = (
    <>
      {!readOnly && (
        <Button type="primary" icon={<SaveOutlined />} onClick={() => void save()} loading={mutations.create.isPending || mutations.update.isPending}>
          Lưu nháp
        </Button>
      )}
      {(isNew ? !readOnly : doc?.actions.canConfirm) && (
        <Button icon={<CheckCircleOutlined />} onClick={confirmReceipt} loading={mutations.confirm.isPending}>
          Xác nhận nhận hàng
        </Button>
      )}
      {doc?.actions.canAdjust && (
        <Button icon={<DiffOutlined />} onClick={() => setDialog('adjust')} title="Trả NCC, nhận nhầm… – ghi lịch sử, PO mở lại / đóng theo SL mới">
          Điều chỉnh SL thực nhận
        </Button>
      )}
      {doc?.actions.canEditInvoice && doc.status === 'Confirmed' && (
        <Button icon={<AuditOutlined />} onClick={() => setDialog('invoice')} title="Hóa đơn NCC về sau khi nhận hàng">
          Cập nhật hóa đơn
        </Button>
      )}
      {doc?.actions.canAccept && (
        <Button icon={<SafetyCertificateOutlined />} onClick={() => setDialog('acceptance')}
          title="Đơn vị đặt hàng xác nhận số lượng, chất lượng hàng đã nhận (đạt / không đạt)">
          Xác nhận nghiệm thu
        </Button>
      )}
      {doc?.actions.canCancel && (
        <Button danger icon={<StopOutlined />} onClick={cancel}>
          Hủy phiếu
        </Button>
      )}
      <Button type="text" icon={<CloseOutlined />} onClick={() => navigate(RECEIPT_PATH)}>
        Đóng
      </Button>
    </>
  )

  const confirmed = doc?.status === 'Confirmed'
  const columns: LineColumn[] = [
    {
      key: 'poLine', title: 'Dòng PO', width: 60, align: 'right', auditField: 'purchaseOrderLineId',
      render: (_, i) => <span className="line-grid-readonly">{lines[i]?.poLineNo}</span>,
    },
    {
      key: 'item', title: 'Mặt hàng', width: 240, auditField: 'itemId',
      render: (_, i) => <span className="line-grid-readonly">{lines[i] ? `${lines[i].itemCode} – ${lines[i].itemName}` : ''}</span>,
    },
    { key: 'uom', title: 'ĐVT', width: 60, render: (_, i) => <span className="line-grid-readonly">{lines[i]?.uomCode}</span> },
    {
      key: 'ordered', title: 'SL đặt', width: 80, align: 'right',
      render: (_, i) => <span className="line-grid-readonly">{formatQuantity(lines[i]?.orderedQty ?? 0)}</span>,
    },
    {
      key: 'other', title: 'Đã nhận trước', width: 90, align: 'right',
      render: (_, i) => <span className="line-grid-readonly">{formatQuantity(lines[i]?.otherReceivedQty ?? 0)}</span>,
    },
    {
      key: 'delivered', title: 'SL phiếu giao', width: 95, align: 'right', required: true,
      render: (field) => (
        <Form.Item name={[field.name, 'deliveredQty']} noStyle rules={[{ required: true, type: 'number', min: 0, message: 'Nhập SL phiếu giao.' }]}>
          <InputNumber {...cellNumber} min={0} precision={4} aria-label={`SL phiếu giao dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'accepted', title: 'SL đạt (thực nhận)', width: 110, align: 'right', required: true,
      render: (field) => (
        <Form.Item name={[field.name, 'acceptedQty']} noStyle rules={[{ required: true, type: 'number', min: 0, message: 'Nhập SL đạt.' }]}>
          <InputNumber {...cellNumber} min={0} precision={4} aria-label={`SL đạt dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'rejected', title: 'SL không đạt', width: 90, align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'rejectedQty']} noStyle>
          <InputNumber {...cellNumber} min={0} precision={4} aria-label={`SL không đạt dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      // Sai lệch so với phiếu giao: đạt + không đạt − phiếu giao (âm = NCC giao thiếu so với phiếu giao).
      key: 'diff', title: 'Thiếu/thừa', width: 80, align: 'right',
      render: (_, i) => {
        const l = lines[i]
        const diff = (l?.acceptedQty ?? 0) + (l?.rejectedQty ?? 0) - (l?.deliveredQty ?? 0)
        return <span className={diff ? 'line-grid-readonly line-grid-warning' : 'line-grid-readonly'}>{diff ? formatQuantity(diff) : ''}</span>
      },
    },
    ...(confirmed
      ? [
        {
          key: 'adjusted', title: 'Điều chỉnh', width: 80, align: 'right' as const,
          render: (_: unknown, i: number) => <span className="line-grid-readonly">{lines[i]?.adjustedQty ? formatQuantity(lines[i].adjustedQty!) : ''}</span>,
        },
        {
          key: 'received', title: 'SL thực nhận', width: 90, align: 'right' as const,
          render: (_: unknown, i: number) => (
            <span className="line-grid-readonly">{formatQuantity((lines[i]?.acceptedQty ?? 0) + (lines[i]?.adjustedQty ?? 0))}</span>
          ),
        },
      ]
      : []),
    {
      key: 'check', title: 'Kết quả KT', width: 110,
      render: (field) => (
        <Form.Item name={[field.name, 'checkResult']} noStyle>
          <Select variant="borderless" style={{ width: '100%' }} showSearch={false} aria-label={`Kết quả kiểm tra dòng ${field.name + 1}`}
            options={Object.entries(receiptCheckResultLabels).map(([value, label]) => ({ value, label }))} />
        </Form.Item>
      ),
    },
    {
      key: 'checkNote', title: 'Ghi chú kiểm tra', width: 170,
      render: (field) => (
        <Form.Item name={[field.name, 'checkNote']} noStyle>
          <Input variant="borderless" maxLength={500} placeholder="Bắt buộc khi không đạt" aria-label={`Ghi chú kiểm tra dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'price', title: 'Giá thực mua', width: 110, align: 'right',
      render: (_, i) => <span className="line-grid-readonly">{formatMoney(lines[i]?.unitPrice ?? 0)}</span>,
    },
    {
      key: 'amount', title: 'Thành tiền', width: 110, align: 'right',
      render: (_, i) => <span className="line-grid-readonly">{formatMoney(lineMoney(lines[i]).amount)}</span>,
    },
    {
      key: 'note', title: 'Ghi chú', width: 140,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle>
          <Input variant="borderless" maxLength={500} aria-label={`Ghi chú dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? receiptConfirmationStatusLabels[doc.status] : '', tone: doc ? receiptConfirmationStatusTone[doc.status] : ('neutral' as const) }

  const notices = doc && [
    doc.actions.readOnlyReason && doc.status !== 'Draft' && <Alert key="ro" type="info" showIcon title={doc.actions.readOnlyReason} />,
    doc.status === 'Cancelled' && doc.cancelReason && <Alert key="cancel" type="error" showIcon title={`Đã hủy: ${doc.cancelReason}`} />,
    confirmed && (
      doc.acceptance === 'None'
        ? <Alert key="acc" type="info" showIcon title="Đơn vị đặt hàng chưa xác nhận nghiệm thu." />
        : (
          <Alert key="acc" type={doc.acceptance === 'Accepted' ? 'success' : 'error'} showIcon
            title={`Nghiệm thu: ${receiptAcceptanceLabels[doc.acceptance]} – ${doc.acceptedByName ?? ''} lúc ${formatDateTime(doc.acceptedAt)}${doc.acceptanceNote ? ` · Ý kiến: ${doc.acceptanceNote}` : ''}`} />
        )
    ),
  ].filter(Boolean)

  const adjustmentTab = {
    key: 'adjustments',
    label: 'Lịch sử điều chỉnh',
    children: (
      <DataGrid
        rows={doc?.adjustments ?? []}
        rowKey={(r) => r.id}
        groupBy={null}
        onGroupByChange={() => {}}
        columns={[
          { key: 'at', title: 'Thời điểm', width: 130, value: (r) => formatDateTime(r.adjustedAt) },
          { key: 'line', title: 'Dòng', width: 60, type: 'quantity', value: (r) => r.lineNo },
          { key: 'qty', title: 'SL điều chỉnh', width: 100, type: 'quantity', value: (r) => r.qty },
          { key: 'reason', title: 'Lý do', width: 320, value: (r) => r.reason },
          { key: 'by', title: 'Người điều chỉnh', width: 150, value: (r) => r.adjustedByName },
        ]}
      />
    ),
  }

  return (
    <>
      <DocumentForm
        title="Xác nhận nhận hàng"
        number={isNew ? null : (doc?.no ?? '')}
        audit={{ entityType: 'GoodsReceiptConfirmation', id: doc?.id }}
        status={status}
        meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}${doc.confirmedAt ? ` · Xác nhận: ${doc.confirmedByName ?? ''} lúc ${formatDateTime(doc.confirmedAt)}` : ''}`}
        loading={document.isLoading}
        error={error ?? (document.error ? document.error.message : null)}
        notice={notices && notices.length > 0 ? <Flex vertical gap={8}>{notices}</Flex> : undefined}
        form={form}
        initialValues={{ date: dayjs(), lines: [] }}
        readOnly={readOnly}
        onSave={() => void save()}
        actions={actions}
        extraTabs={[...(doc ? [adjustmentTab] : []), attachmentTab]}
        header={
          <>
            <Form.Item label="Đơn mua hàng" name="purchaseOrderId" rules={[{ required: true, message: 'Chọn đơn mua hàng.' }]}
              extra={isNew ? 'PO đã duyệt, chưa nhận đủ; nơi nhận trong phạm vi kho của bạn.' : undefined}>
              <Select
                disabled={!isNew}
                options={[
                  ...(receivable.data?.items ?? []).map((o) => ({ value: o.id, label: `${o.no} – ${o.description ?? ''}` })),
                  ...(doc ? [{ value: doc.purchaseOrderId, label: `${doc.purchaseOrderNo} – ${doc.supplierCode} ${doc.supplierName}` }] : []),
                ]}
              />
            </Form.Item>
            <Form.Item label="Ngày nhận" name="date" rules={[{ required: true, message: 'Chọn ngày nhận.' }]}>
              <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Nhà cung cấp">
              <Input disabled value={doc ? `${doc.supplierCode} – ${doc.supplierName}` : (order.data ? `${order.data.supplierCode} – ${order.data.supplierName}` : '')} />
            </Form.Item>
            <Form.Item label="Nơi nhận">
              <Input disabled value={doc ? `${doc.warehouseCode ?? ''} – ${doc.warehouseName ?? ''}` : (order.data?.warehouseCode ?? '')} />
            </Form.Item>
            <Form.Item label="Số phiếu giao NCC" name="deliveryNoteNo" rules={[{ max: 100 }]}>
              <Input autoComplete="off" />
            </Form.Item>
            <Form.Item label="Người giao" name="deliverer" rules={[{ max: 200 }]}>
              <Input />
            </Form.Item>
            <Form.Item label="Số hóa đơn" name="invoiceNo" rules={[{ max: 50 }]}>
              <Input autoComplete="off" />
            </Form.Item>
            <Form.Item label="Ký hiệu hóa đơn" name="invoiceSeries" rules={[{ max: 50 }]}>
              <Input autoComplete="off" />
            </Form.Item>
            <Form.Item label="Ngày hóa đơn" name="invoiceDate">
              <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Tiền hóa đơn (gồm VAT)" name="invoiceAmount" extra="Đối chiếu với tiền thực nhận (MATCH3).">
              <InputNumber {...vnNumberInput} min={0} precision={0} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]}>
              <Input />
            </Form.Item>
          </>
        }
        lines={
          <>
            {isNew && !readOnly && purchaseOrderId && (
              <Button icon={<FileDoneOutlined />} onClick={takeOrderLines} disabled={!order.data || order.isFetching} loading={order.isFetching}
                style={{ marginBottom: 6 }}>
                Lấy lại các dòng còn lại của PO
              </Button>
            )}
            <LineGrid auditEntity="GoodsReceiptConfirmationLine" name="lines" columns={columns} readOnly={readOnly} fixedRows scrollX newLine={() => ({})} />
          </>
        }
        footer={
          <>
            <DocumentTotal label="Tiền hàng thực nhận" value={formatMoney(totals.amount)} />
            <DocumentTotal label="Tiền VAT" value={formatMoney(totals.vat)} />
            <DocumentTotal label="Tổng thanh toán" value={formatMoney(totals.amount + totals.vat)} strong />
          </>
        }
      />
      {doc && dialog === 'invoice' && <InvoiceDialog doc={doc} onClose={() => setDialog(null)} />}
      {doc && dialog === 'acceptance' && <AcceptanceDialog doc={doc} onClose={() => setDialog(null)} />}
      {doc && dialog === 'adjust' && <AdjustDialog doc={doc} onClose={() => setDialog(null)} />}
    </>
  )
}

/**
 * Hộp thoại cập nhật hóa đơn NCC trên XNH đã xác nhận (hóa đơn về sau hàng) – mọi thay đổi vào nhật ký.
 * @param props.doc Phiếu đang mở.
 * @param props.onClose Đóng hộp thoại.
 */
function InvoiceDialog({ doc, onClose }: { doc: ReceiptConfirmation; onClose: () => void }) {
  const { message } = App.useApp()
  const [form] = Form.useForm<{ invoiceNo?: string; invoiceSeries?: string; invoiceDate?: Dayjs | null; invoiceAmount?: number | null }>()
  const { invoice } = useReceiptConfirmationMutations()
  const submit = async () => {
    const v = await form.validateFields()
    try {
      await invoice.mutateAsync({
        id: doc.id,
        input: {
          invoiceNo: orNull(v.invoiceNo), invoiceSeries: orNull(v.invoiceSeries),
          invoiceDate: v.invoiceDate ? v.invoiceDate.format('YYYY-MM-DD') : null, invoiceAmount: v.invoiceAmount ?? null,
        },
      })
      message.success('Đã cập nhật hóa đơn.')
      onClose()
    } catch (e) {
      message.error((e as Error).message)
    }
  }
  return (
    <Modal title="Cập nhật hóa đơn NCC" open okText="Lưu" cancelText="Hủy" confirmLoading={invoice.isPending} onCancel={onClose} onOk={() => void submit()} destroyOnHidden>
      <Form form={form} name="receiptConfirmationInvoice" layout="vertical" preserve={false}
        initialValues={{
          invoiceNo: doc.invoiceNo ?? '', invoiceSeries: doc.invoiceSeries ?? '',
          invoiceDate: doc.invoiceDate ? dayjs(doc.invoiceDate) : null, invoiceAmount: doc.invoiceAmount ?? null,
        }}>
        <Form.Item label="Số hóa đơn" name="invoiceNo" rules={[{ max: 50 }]}><Input autoComplete="off" /></Form.Item>
        <Form.Item label="Ký hiệu hóa đơn" name="invoiceSeries" rules={[{ max: 50 }]}><Input autoComplete="off" /></Form.Item>
        <Form.Item label="Ngày hóa đơn" name="invoiceDate"><DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} /></Form.Item>
        <Form.Item label="Tiền hóa đơn (gồm VAT)" name="invoiceAmount">
          <InputNumber {...vnNumberInput} min={0} precision={0} style={{ width: '100%' }} aria-label="Tiền hóa đơn" />
        </Form.Item>
      </Form>
    </Modal>
  )
}

/**
 * Hộp thoại nghiệm thu của đơn vị đặt hàng (Q8, design.md mục 19.1): Đạt, hoặc Không đạt kèm ý kiến; xác nhận lại được.
 * @param props.doc Phiếu đã xác nhận.
 * @param props.onClose Đóng hộp thoại.
 */
function AcceptanceDialog({ doc, onClose }: { doc: ReceiptConfirmation; onClose: () => void }) {
  const { message } = App.useApp()
  const [form] = Form.useForm<{ result: 'Accepted' | 'Disputed'; note?: string }>()
  const { accept } = useReceiptConfirmationMutations()
  const result = Form.useWatch('result', form)
  const submit = async () => {
    const v = await form.validateFields()
    try {
      await accept.mutateAsync({ id: doc.id, accepted: v.result === 'Accepted', note: orNull(v.note) })
      message.success(`Đã xác nhận nghiệm thu phiếu ${doc.no}: ${receiptAcceptanceLabels[v.result]}.`)
      onClose()
    } catch (e) {
      message.error((e as Error).message)
    }
  }
  return (
    <Modal title="Xác nhận nghiệm thu" open okText="Xác nhận" cancelText="Hủy" confirmLoading={accept.isPending} onCancel={onClose} onOk={() => void submit()} destroyOnHidden>
      {doc.acceptance !== 'None' && (
        <Alert type="info" showIcon style={{ marginBottom: 8 }}
          title={`Đã xác nhận: ${receiptAcceptanceLabels[doc.acceptance]} – ${doc.acceptedByName ?? ''} lúc ${formatDateTime(doc.acceptedAt)}. Xác nhận lại sẽ thay kết quả cũ.`} />
      )}
      <Form form={form} name="receiptConfirmationAcceptance" layout="vertical" preserve={false}
        initialValues={{ result: doc.acceptance === 'Disputed' ? 'Disputed' : 'Accepted', note: doc.acceptanceNote ?? '' }}>
        <Form.Item label="Kết quả nghiệm thu" name="result" rules={[{ required: true }]}>
          <Radio.Group options={[
            { value: 'Accepted', label: 'Đạt – đúng số lượng, chất lượng' },
            { value: 'Disputed', label: 'Không đạt – có ý kiến' },
          ]} />
        </Form.Item>
        <Form.Item label="Ý kiến nghiệm thu" name="note"
          rules={[{ max: 500 }, { required: result === 'Disputed', whitespace: true, message: 'Không đạt phải ghi ý kiến (thiếu, hỏng, sai quy cách…).' }]}
          extra="Không đạt: Phòng cung ứng xử lý với NCC (điều chỉnh SL, giao bù bằng PO mới); kế toán thấy trên báo cáo đối chiếu PO – nhận – hóa đơn.">
          <Input.TextArea rows={3} maxLength={500} aria-label="Ý kiến nghiệm thu" />
        </Form.Item>
      </Form>
    </Modal>
  )
}

/**
 * Hộp thoại điều chỉnh SL thực nhận một dòng (thay phiếu trả NCC – design.md mục 19.4): SL âm = trả NCC / nhận nhầm, dương = ghi bổ sung;
 * bắt buộc lý do; PO mở lại hoặc đóng theo SL mới.
 * @param props.doc Phiếu đã xác nhận.
 * @param props.onClose Đóng hộp thoại.
 */
function AdjustDialog({ doc, onClose }: { doc: ReceiptConfirmation; onClose: () => void }) {
  const { message } = App.useApp()
  const [form] = Form.useForm<{ lineId: string; qty: number | null; reason?: string }>()
  const { adjust } = useReceiptConfirmationMutations()
  const lineId = Form.useWatch('lineId', form)
  const line = doc.lines.find((l) => l.id === lineId)
  const submit = async () => {
    const v = await form.validateFields()
    try {
      await adjust.mutateAsync({ id: doc.id, lineId: v.lineId, qty: v.qty ?? 0, reason: v.reason!.trim() })
      message.success(`Đã điều chỉnh SL thực nhận phiếu ${doc.no}.`)
      onClose()
    } catch (e) {
      message.error((e as Error).message)
    }
  }
  return (
    <Modal title="Điều chỉnh SL thực nhận" open okText="Lưu" cancelText="Hủy" confirmLoading={adjust.isPending} onCancel={onClose} onOk={() => void submit()} destroyOnHidden>
      <Form form={form} name="receiptAdjust" layout="vertical" preserve={false} initialValues={{ lineId: doc.lines[0]?.id, qty: null, reason: '' }}>
        <Form.Item label="Dòng" name="lineId" rules={[{ required: true, message: 'Chọn dòng.' }]}>
          <Select showSearch={false} options={doc.lines.map((l) => ({ value: l.id, label: `${l.lineNo}. ${l.itemCode} – ${l.itemName} (thực nhận ${formatQuantity(l.receivedQty)} ${l.uomCode})` }))} />
        </Form.Item>
        <Form.Item label={`SL điều chỉnh${line ? ` (${line.uomCode})` : ''}`} name="qty"
          rules={[{ required: true, message: 'Nhập SL.' }, { validator: (_, v) => (v === 0 ? Promise.reject(new Error('SL phải khác 0.')) : Promise.resolve()) }]}
          extra="Âm = trả lại NCC / nhận nhầm (ví dụ −3); dương = ghi bổ sung. Không vượt SL đặt + dung sai.">
          <InputNumber {...vnNumberInput} precision={4} style={{ width: '100%' }} aria-label="SL điều chỉnh" />
        </Form.Item>
        <Form.Item label="Lý do" name="reason" rules={[{ required: true, whitespace: true, message: 'Nhập lý do điều chỉnh.' }, { max: 500 }]}>
          <Input.TextArea rows={2} maxLength={500} aria-label="Lý do điều chỉnh" />
        </Form.Item>
      </Form>
    </Modal>
  )
}
