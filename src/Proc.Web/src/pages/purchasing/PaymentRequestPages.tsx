import { DollarOutlined, FileAddOutlined } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Form, Input, InputNumber, Modal, Table } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router'
import type { DateRange } from '../../api/logs'
import { useDepartments } from '../../api/organization'
import {
  paymentMethodLabels,
  paymentRequestFormLabels,
  paymentStatusLabel,
  useMarkPaymentPaid,
  usePaymentOrders,
  usePaymentRequest,
  usePaymentRequests,
  usePaymentSource,
  useSundryPaymentSheets,
  type PaymentMethod,
  type PaymentReceiptOption,
  type PaymentRequest,
  type PaymentRequestListItem,
  type SavePaymentRequestInput,
  type SundryPaymentSheetOption,
} from '../../api/paymentRequests'
import { PURCHASE_STAGE_LABELS, purchaseStatusLabels, purchaseStatusTone, usePurchaseMutations, withPendingApprovers } from '../../api/purchasing'
import { SUNDRY_FORMS_SIMULATED, SUNDRY_SIMULATED_NOTICE } from '../../api/sundryExpenseSheets'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DateInput } from '../../classic/DateInput'
import { DocNo } from '../../classic/DocNo'
import { Select } from '../../classic/Select'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import type { GridColumn } from '../../components/DataGrid/types'
import { DocumentForm, DocumentTotal } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { useDocumentFormSync } from '../../components/DocumentForm/useDocumentFormSync'
import { ListPage } from '../../components/ListPage/ListPage'
import { DATE_INPUT_FORMATS, formatDate, formatDateTime, formatMoney, vnNumberInput } from '../../utils/format'
import { LogDateRangeFilter } from '../system/logFilters'
import { ApprovalHistory } from './ApprovalHistory'
import { useApprovalCommands } from './useApprovalCommands'

/** Đường dẫn danh sách đề nghị thanh toán (màn hình DNT). */
export const PAYMENT_REQUEST_PATH = '/mua-hang/de-nghi-thanh-toan'

const listColumns: GridColumn<PaymentRequestListItem>[] = [
  { key: 'no', title: 'Số phiếu', width: 150, value: (r) => r.no, docRef: (r) => ({ docType: 'DNT', id: r.id }) },
  { key: 'date', title: 'Ngày đề nghị', width: 100, type: 'date', value: (r) => r.date },
  { key: 'form', title: 'Biểu mẫu', width: 70, value: (r) => r.form, groupable: true },
  {
    key: 'po', title: 'Đơn mua hàng', width: 140, value: (r) => r.purchaseOrderNo,
    docRef: (r) => (r.purchaseOrderId ? { docType: 'PO', id: r.purchaseOrderId } : null),
  },
  // 1C tạp vụ (lát Y11b) không có NCC – hiện người nhận tiền.
  {
    key: 'supplier', title: 'NCC / người nhận', width: 220, groupable: true,
    value: (r) => (r.supplierCode ? `${r.supplierCode} – ${r.supplierName ?? ''}` : `Người nhận: ${r.payeeName ?? ''}`),
  },
  { key: 'department', title: 'Bộ phận', width: 160, value: (r) => r.departmentName, groupable: true },
  { key: 'method', title: 'Hình thức', width: 100, value: (r) => paymentMethodLabels[r.method], groupable: true },
  { key: 'total', title: 'Tổng chứng từ', width: 130, type: 'money', value: (r) => r.totalAmount },
  { key: 'deduction', title: 'Giảm trừ', width: 110, type: 'money', value: (r) => r.deductionAmount || null },
  { key: 'payable', title: 'Số tiền đề nghị', width: 130, type: 'money', value: (r) => r.payableAmount },
  {
    key: 'status', title: 'Trạng thái', width: 110, type: 'status', groupable: true,
    value: (r) => paymentStatusLabel(r.status, r.isPaid, purchaseStatusLabels),
    tone: (r) => (r.isPaid ? 'success' : purchaseStatusTone[r.status]),
  },
  { key: 'paidDate', title: 'Ngày chi', width: 90, type: 'date', value: (r) => r.paidDate },
  { key: 'creator', title: 'Người lập', width: 150, value: (r) => r.createdByName, groupable: true },
]

/**
 * Màn hình Mua hàng → Đề nghị thanh toán (DNT – lát Y8, design.md mục 19.3 bước 7): đề nghị thanh toán 5A/5B cho PO đã nhận hàng có hóa đơn,
 * duyệt theo chuỗi GĐ khối → TP TC-NQ-KT → GĐ TC-NQ-KT → Chủ tịch; kế toán ghi Đã chi.
 */
export function PaymentRequestListPage() {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const [range, setRange] = useState<DateRange>(() => ({ from: dayjs().subtract(89, 'day').format('YYYY-MM-DD'), to: dayjs().format('YYYY-MM-DD') }))
  const list = usePaymentRequests(range)
  return (
    <ListPage
      title="Đề nghị thanh toán (DNT)"
      exportFileName="de-nghi-thanh-toan"
      rows={list.data ?? []}
      columns={listColumns}
      rowKey={(r) => r.id}
      auditEntity="PaymentRequest"
      loading={list.isLoading}
      canCreate={hasPermission(Permission.PaymentCreate)}
      onCreate={() => navigate(`${PAYMENT_REQUEST_PATH}/moi`)}
      onOpenRow={(r) => navigate(`${PAYMENT_REQUEST_PATH}/${r.id}`)}
      isMarked={(r) => r.status === 'Draft' || r.status === 'Submitted' || (r.status === 'Approved' && !r.isPaid)}
      filters={<LogDateRangeFilter value={range} onChange={setRange} />}
      actions={
        hasPermission(Permission.PaymentCreate) && (
          <Button icon={<FileAddOutlined />} onClick={() => navigate(`${PAYMENT_REQUEST_PATH}/moi?form=1C`)}>
            Lập đề nghị 1C (tạp vụ)
          </Button>
        )
      }
    />
  )
}

/** Dòng trên form: XNH và nội dung thanh toán; thông tin hóa đơn lấy từ {@link LineInfo}. */
interface LineValues {
  id: string | null
  receiptConfirmationId?: string
  description?: string
}

interface FormValues {
  date: Dayjs
  purchaseOrderId?: string
  method: PaymentMethod
  bankAccountId?: string
  deductionAmount?: number | null
  deductionReason?: string
  note?: string
  lines: LineValues[]
}

/** Thông tin hóa đơn của một dòng – từ dòng đã lưu (đã chụp) hoặc XNH vừa chọn. */
interface LineInfo {
  receiptNo: string
  receiptDate: string | null
  invoiceNo: string
  invoiceSeries: string | null
  invoiceDate: string | null
  amount: number
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Trang đề nghị thanh toán: chọn khuôn theo biểu mẫu – 1C tổng hợp tạp vụ (`?form=1C` khi lập mới, lát Y11b) hoặc 5A/5B theo PO (lát Y8).
 * Phiếu đã lưu: đọc biểu mẫu của phiếu (đang tải thì hiện khuôn 5A/5B ở trạng thái đang tải).
 */
export function PaymentRequestPage() {
  const { id = 'moi' } = useParams()
  const [params] = useSearchParams()
  const isNew = id === 'moi'
  const payment = usePaymentRequest(isNew ? null : id)
  const sundry = isNew ? params.get('form') === '1C' : payment.data?.form === '1C'
  return sundry ? <SundryPaymentRequestPage /> : <PurchasePaymentRequestPage />
}

/**
 * Trang phiếu đề nghị thanh toán 5A/5B (lát Y8) trên khuôn DocumentForm. Đầu phiếu: ngày, đơn mua hàng (không đổi sau khi lập), hình thức, tài
 * khoản NCC (chuyển khoản), giảm trừ + lý do, ghi chú. Dòng: nhiều hóa đơn – mỗi dòng một phiếu nhận hàng (XNH) có hóa đơn, chọn qua hộp "Chọn
 * phiếu nhận hàng"; số tiền = tiền hóa đơn trên XNH, nội dung sửa được. Duyệt xong kế toán bấm "Ghi Đã chi".
 * Mở `/mua-hang/de-nghi-thanh-toan/moi?po=…` (nút trên PO) thì chọn sẵn đơn và mọi XNH có hóa đơn chưa đề nghị.
 */
function PurchasePaymentRequestPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const [params] = useSearchParams()
  const { hasPermission } = useAuth()
  const { message } = App.useApp()
  const [form] = Form.useForm<FormValues>()
  const payment = usePaymentRequest(isNew ? null : id)
  const mutations = usePurchaseMutations<PaymentRequest, SavePaymentRequestInput>('payment-requests')
  const [error, setError] = useState<string | null>(null)
  const [picking, setPicking] = useState(false)
  const [paying, setPaying] = useState(false)
  const [picked, setPicked] = useState<Map<string, LineInfo>>(() => new Map())
  const addRef = useRef<(() => void) | null>(null)
  const doc = payment.data
  const readOnly = isNew ? !hasPermission(Permission.PaymentCreate) : !doc?.actions.canEdit
  const attachmentTab = useAttachmentTab('PaymentRequest', doc?.id, hasPermission(Permission.PaymentCreate))

  const orderId = (Form.useWatch('purchaseOrderId', form) as string | undefined) ?? doc?.purchaseOrderId
  const method = (Form.useWatch('method', form) as PaymentMethod | undefined) ?? doc?.method ?? 'BankTransfer'
  const deduction = (Form.useWatch('deductionAmount', form) as number | null | undefined) ?? 0
  const watchedLines = Form.useWatch('lines', form) as LineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const orders = usePaymentOrders(isNew)
  const source = usePaymentSource(orderId ?? null, doc?.id ?? null)

  const { loadSaved, targetId } = useDocumentFormSync(form, id, doc, (d) => ({
    date: dayjs(d.date),
    purchaseOrderId: d.purchaseOrderId,
    method: d.method,
    bankAccountId: d.bankAccountId ?? undefined,
    deductionAmount: d.deductionAmount,
    deductionReason: d.deductionReason ?? '',
    note: d.note ?? '',
    lines: d.lines.map((l) => ({ id: l.id, receiptConfirmationId: l.receiptConfirmationId, description: l.description })),
  }))

  /** Thông tin dòng theo Id XNH: XNH vừa chọn ưu tiên, sau đó dòng đã lưu. */
  const infoOf = (receiptId: string | undefined): LineInfo | undefined => {
    if (!receiptId) return undefined
    const fresh = picked.get(receiptId)
    if (fresh) return fresh
    const saved = doc?.lines.find((l) => l.receiptConfirmationId === receiptId)
    return saved
      ? { receiptNo: saved.receiptNo ?? '', receiptDate: saved.receiptDate, invoiceNo: saved.invoiceNo ?? '', invoiceSeries: saved.invoiceSeries, invoiceDate: saved.invoiceDate, amount: saved.amount }
      : undefined
  }

  /** XNH chọn được: có hóa đơn, chưa thuộc đề nghị khác, chưa có trên phiếu. */
  const selectable = (exclude: Set<string>) =>
    (source.data?.receipts ?? []).filter((r) => r.invoiceNo && (r.invoiceAmount ?? 0) > 0 && !r.heldBy && !exclude.has(r.id))

  const addReceipts = (rows: PaymentReceiptOption[]) => {
    setPicked((prev) => {
      const next = new Map(prev)
      for (const r of rows)
        next.set(r.id, { receiptNo: r.no, receiptDate: r.date, invoiceNo: r.invoiceNo ?? '', invoiceSeries: r.invoiceSeries, invoiceDate: r.invoiceDate, amount: r.invoiceAmount ?? 0 })
      return next
    })
    const current = (form.getFieldValue('lines') as LineValues[] | undefined) ?? []
    form.setFieldValue('lines', [...current, ...rows.map((r) => ({ id: null, receiptConfirmationId: r.id, description: '' }))])
    setPicking(false)
  }

  // Mở từ PO ("Lập đề nghị thanh toán"): chọn sẵn đơn, tài khoản mặc định và mọi XNH có hóa đơn chưa đề nghị – một lần khi dữ liệu chọn về.
  const prefilled = useRef(false)
  const poParam = params.get('po')
  useEffect(() => {
    if (!isNew || !poParam || prefilled.current) return
    if (!form.getFieldValue('purchaseOrderId')) form.setFieldValue('purchaseOrderId', poParam)
    if (!source.data || source.data.purchaseOrderId !== poParam) return
    prefilled.current = true
    const bank = source.data.bankAccounts.find((b) => b.isDefault) ?? source.data.bankAccounts[0]
    if (bank) form.setFieldValue('bankAccountId', bank.id)
    else form.setFieldValue('method', 'Cash')
    addReceipts(selectable(new Set()))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isNew, poParam, source.data])

  /** Đổi đơn: bỏ dòng, tài khoản của đơn cũ. */
  const changeOrder = () => {
    form.setFieldValue('lines', [])
    form.setFieldValue('bankAccountId', undefined)
  }

  const openPicker = () => {
    if (!orderId) {
      message.warning('Hãy chọn đơn mua hàng trước khi chọn phiếu nhận hàng.')
      return
    }
    setPicking(true)
  }

  const persist = async (): Promise<PaymentRequest | null> => {
    let values: FormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng.')
      return null
    }
    setError(null)
    const lineValues = (form.getFieldValue('lines') as LineValues[] | undefined) ?? []
    const input: SavePaymentRequestInput = {
      date: values.date.format('YYYY-MM-DD'),
      purchaseOrderId: values.purchaseOrderId ?? doc?.purchaseOrderId ?? '',
      method: values.method,
      bankAccountId: values.method === 'BankTransfer' ? (values.bankAccountId ?? null) : null,
      deductionAmount: values.deductionAmount ?? 0,
      deductionReason: orNull(values.deductionReason),
      note: orNull(values.note),
      lines: lineValues.map((l) => ({ receiptConfirmationId: l.receiptConfirmationId!, description: orNull(l.description) })),
    }
    try {
      const target = targetId()
      const saved = target ? await mutations.update.mutateAsync({ id: target, input }) : await mutations.create.mutateAsync(input)
      setPicked(new Map())
      loadSaved(saved)
      return saved
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được phiếu.')
      return null
    }
  }

  const { save, actions } = useApprovalCommands({
    isNew, doc, readOnly, persist, mutations, listPath: PAYMENT_REQUEST_PATH, noun: 'đề nghị thanh toán', setError, docType: 'DNT',
  })

  const readonlyCell = (text: string) => <span className="line-grid-readonly">{text}</span>
  const columns: LineColumn[] = [
    {
      key: 'receipt', title: 'Phiếu nhận hàng', width: 150,
      render: (field, index) => {
        const receiptId = lines[index]?.receiptConfirmationId
        const info = infoOf(receiptId)
        return (
          <>
            <Form.Item name={[field.name, 'id']} hidden>
              <Input />
            </Form.Item>
            <Form.Item name={[field.name, 'receiptConfirmationId']} hidden rules={[{ required: true, message: 'Dòng chưa có phiếu nhận hàng.' }]}>
              <Input />
            </Form.Item>
            {info ? <DocNo no={info.receiptNo} docType="XNH" id={receiptId} /> : null}
          </>
        )
      },
    },
    { key: 'receiptDate', title: 'Ngày nhận', width: 95, render: (_, i) => readonlyCell(formatDate(infoOf(lines[i]?.receiptConfirmationId)?.receiptDate ?? null)) },
    { key: 'invoiceDate', title: 'Ngày hóa đơn', width: 100, render: (_, i) => readonlyCell(formatDate(infoOf(lines[i]?.receiptConfirmationId)?.invoiceDate ?? null)) },
    { key: 'invoiceSeries', title: 'Ký hiệu', width: 100, render: (_, i) => readonlyCell(infoOf(lines[i]?.receiptConfirmationId)?.invoiceSeries ?? '') },
    { key: 'invoiceNo', title: 'Số hóa đơn', width: 100, render: (_, i) => readonlyCell(infoOf(lines[i]?.receiptConfirmationId)?.invoiceNo ?? '') },
    {
      key: 'description', title: 'Nội dung thanh toán', width: 320,
      render: (field) => (
        <Form.Item name={[field.name, 'description']} noStyle rules={[{ max: 500 }]}>
          <Input variant="borderless" maxLength={500} placeholder="Trống = tự ghi theo hóa đơn" aria-label={`Nội dung thanh toán dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'amount', title: 'Số tiền', width: 130, align: 'right',
      render: (_, i) => { const x = infoOf(lines[i]?.receiptConfirmationId); return readonlyCell(x ? formatMoney(x.amount) : '') },
    },
  ]

  const total = lines.reduce((sum, l) => sum + (infoOf(l?.receiptConfirmationId)?.amount ?? 0), 0)
  const payable = total - (deduction ?? 0)
  const statusLabel = doc ? (doc.paidDate ? 'Đã chi' : withPendingApprovers(purchaseStatusLabels[doc.status], doc.approvals)) : ''
  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: statusLabel, tone: doc?.paidDate ? ('success' as const) : doc ? purchaseStatusTone[doc.status] : ('neutral' as const) }
  const orderOptions = (orders.data ?? []).map((o) => ({
    value: o.id,
    label: `${o.no} – ${o.supplierCode} ${o.supplierName} (${o.openReceiptCount} hóa đơn, ${formatMoney(o.openAmount)})`,
  }))
  if (poParam && !orderOptions.some((o) => o.value === poParam) && source.data?.purchaseOrderId === poParam)
    orderOptions.push({ value: poParam, label: `${source.data.purchaseOrderNo} – ${source.data.supplierCode} ${source.data.supplierName}` })
  const bankOptions = (source.data?.bankAccounts ?? []).map((b) => ({
    value: b.id,
    label: `${b.bankName}${b.branch ? ` – ${b.branch}` : ''}: ${b.accountNumber}${b.accountName ? ` (${b.accountName})` : ''}`,
  }))
  if (doc?.bankAccountId && !bankOptions.some((o) => o.value === doc.bankAccountId))
    bankOptions.push({ value: doc.bankAccountId, label: `${doc.bankName ?? ''}: ${doc.bankAccountNumber ?? ''} (đã đổi trong danh mục)` })
  const info = source.data ?? doc

  return (
    <>
      <DocumentForm
        approval={doc ? { steps: doc.approvals, createdByName: doc.createdByName, createdAt: doc.createdAt, stageLabels: PURCHASE_STAGE_LABELS } : undefined}
        title="Đề nghị thanh toán"
        number={isNew ? null : (doc?.no ?? '')}
        audit={{ entityType: 'PaymentRequest', id: doc?.id }}
        printPath={doc ? `/api/payment-requests/${doc.id}/pdf` : undefined}
        status={status}
        meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
        loading={payment.isLoading}
        error={error ?? (payment.error ? payment.error.message : null)}
        notice={
          <>
            {doc?.rejectReason && doc.status === 'Draft' ? (
              <Alert type="warning" showIcon title={`Bị từ chối: ${doc.rejectReason}`} />
            ) : (
              doc?.actions.readOnlyReason && <Alert type="info" showIcon title={doc.actions.readOnlyReason} />
            )}
            {doc?.cancelReason && <Alert type="error" showIcon title={`Đã hủy: ${doc.cancelReason}`} />}
            {doc?.paidDate && (
              <Alert type="success" showIcon
                title={`Đã chi ngày ${formatDate(doc.paidDate)} – ${doc.paidByName ?? ''}${doc.paymentNote ? `. ${doc.paymentNote}` : ''}`} />
            )}
            {info && (
              <Alert type="info" showIcon
                title={`Biểu mẫu ${paymentRequestFormLabels[info.form]} · NCC ${info.supplierCode ?? ''} – ${info.supplierName ?? ''} · ` +
                  `${info.constructionProjectCode ? `Công trình ${info.constructionProjectCode} · ` : ''}Bộ phận đặt hàng: ${info.departmentName ?? '(chưa có)'}`} />
            )}
          </>
        }
        form={form}
        initialValues={{ date: dayjs(), method: 'BankTransfer', deductionAmount: 0, lines: [] }}
        readOnly={readOnly}
        onSave={save}
        onAddLine={() => addRef.current?.()}
        actions={
          <>
            {doc?.canPay && (
              <Button icon={<DollarOutlined />} onClick={() => setPaying(true)}>
                Ghi Đã chi
              </Button>
            )}
            {actions}
          </>
        }
        header={
          <>
            <Form.Item label="Ngày đề nghị" name="date" rules={[{ required: true, message: 'Chọn ngày.' }]}>
              <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
            </Form.Item>
            {isNew ? (
              <Form.Item label="Đơn mua hàng" name="purchaseOrderId" rules={[{ required: true, message: 'Chọn đơn mua hàng.' }]}>
                <Select options={orderOptions} loading={orders.isLoading} placeholder="PO còn phiếu nhận hàng có hóa đơn chưa đề nghị"
                  onChange={changeOrder} popupMatchSelectWidth={520} />
              </Form.Item>
            ) : (
              <Form.Item label="Đơn mua hàng">
                {doc?.purchaseOrderNo && (
                  <DocNo no={doc.purchaseOrderNo} docType="PO" id={doc.purchaseOrderId ?? undefined} suffix={`ngày ${formatDate(doc.purchaseOrderDate)}`} />
                )}
              </Form.Item>
            )}
            <Form.Item label="Hình thức" name="method" rules={[{ required: true, message: 'Chọn hình thức.' }]}>
              <Select showSearch={false} options={Object.entries(paymentMethodLabels).map(([value, label]) => ({ value, label }))} />
            </Form.Item>
            {method === 'BankTransfer' && (
              <Form.Item label="Tài khoản NCC" name="bankAccountId" rules={[{ required: true, message: 'Chọn tài khoản ngân hàng của NCC.' }]}
                extra={orderId && source.data && bankOptions.length === 0 ? 'NCC chưa khai tài khoản ngân hàng – khai ở danh mục NCC (qua duyệt) hoặc chọn tiền mặt.' : undefined}>
                <Select options={bankOptions} loading={source.isLoading} placeholder="Tài khoản ngân hàng của NCC" popupMatchSelectWidth={420} />
              </Form.Item>
            )}
            <Form.Item label="Số tiền giảm trừ" name="deductionAmount">
              <InputNumber {...vnNumberInput} min={0} precision={0} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Lý do giảm trừ" name="deductionReason" rules={[{ max: 1000 }]}>
              <Input placeholder="Tạm ứng đã chi, phạt chậm giao…" />
            </Form.Item>
            <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]} className="document-form-wide">
              <Input />
            </Form.Item>
          </>
        }
        lines={
          <>
            {!readOnly && (
              <Button icon={<FileAddOutlined />} onClick={openPicker} style={{ marginBottom: 6 }}>
                Chọn phiếu nhận hàng
              </Button>
            )}
            <LineGrid name="lines" auditEntity="PaymentRequestLine" columns={columns} readOnly={readOnly} addRef={addRef}
              newLine={() => ({ id: null })} addLines={openPicker} />
          </>
        }
        footer={
          <>
            <DocumentTotal label="Tổng cộng" value={formatMoney(total)} />
            <DocumentTotal label="Số tiền giảm trừ" value={formatMoney(deduction ?? 0)} />
            <DocumentTotal label="Số tiền đề nghị thanh toán" value={formatMoney(payable)} strong />
          </>
        }
        extraTabs={[
          ...(doc ? [{ key: 'approvals', label: 'Lịch sử duyệt', children: <ApprovalHistory steps={doc.approvals} stageLabels={PURCHASE_STAGE_LABELS} createdByName={doc.createdByName} createdAt={doc.createdAt} /> }] : []),
          attachmentTab,
        ]}
      />
      {picking && (
        <ReceiptPicker
          rows={source.data?.receipts ?? []}
          selectable={selectable(new Set(lines.map((l) => l?.receiptConfirmationId).filter((x): x is string => !!x)))}
          loading={source.isLoading}
          onCancel={() => setPicking(false)}
          onPick={addReceipts}
        />
      )}
      {paying && doc && <PayDialog doc={doc} onClose={() => setPaying(false)} />}
    </>
  )
}

/** Dòng 1C trên form: bảng kê và nội dung thanh toán; số tiền, ngày lấy từ bảng kê. */
interface SundryLineValues {
  id: string | null
  sundryExpenseSheetId?: string
  description?: string
}

interface SundryFormValues {
  date: Dayjs
  departmentId?: string
  payeeName?: string
  method: PaymentMethod
  bankName?: string
  bankBranch?: string
  bankAccountNumber?: string
  bankAccountName?: string
  deductionAmount?: number | null
  deductionReason?: string
  note?: string
  lines: SundryLineValues[]
}

/** Thông tin một bảng kê trên dòng 1C – từ dòng đã lưu (đã chụp) hoặc bảng kê vừa chọn. */
interface SheetInfo {
  no: string
  date: string | null
  amount: number
}

/**
 * Trang đề nghị thanh toán tổng hợp chi phí tạp vụ 1C (lát Y11b – design.md mục 19.2 loại C) trên khuôn DocumentForm. Đầu phiếu: ngày, bộ phận
 * (không đổi sau khi lập), người nhận tiền, hình thức – chuyển khoản nhập tay ngân hàng, số tài khoản, chủ tài khoản; giảm trừ (tạm ứng đã nhận…).
 * Dòng: mỗi dòng một bảng kê 2C đã duyệt của bộ phận, chọn qua hộp "Chọn bảng kê tạp vụ"; số tiền = tổng bảng kê. Duyệt chung chuỗi 5A/5B/1C,
 * kế toán ghi Đã chi. Mở `…/moi?form=1C&dept=…&sheet=…` (nút trên bảng kê) thì chọn sẵn bộ phận và bảng kê.
 */
function SundryPaymentRequestPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const [params] = useSearchParams()
  const { hasPermission } = useAuth()
  const { message } = App.useApp()
  const [form] = Form.useForm<SundryFormValues>()
  const payment = usePaymentRequest(isNew ? null : id)
  const mutations = usePurchaseMutations<PaymentRequest, SavePaymentRequestInput>('payment-requests')
  const departments = useDepartments()
  const [error, setError] = useState<string | null>(null)
  const [picking, setPicking] = useState(false)
  const [paying, setPaying] = useState(false)
  const [picked, setPicked] = useState<Map<string, SheetInfo>>(() => new Map())
  const addRef = useRef<(() => void) | null>(null)
  const doc = payment.data
  const readOnly = isNew ? !hasPermission(Permission.PaymentCreate) : !doc?.actions.canEdit
  const attachmentTab = useAttachmentTab('PaymentRequest', doc?.id, hasPermission(Permission.PaymentCreate))

  const departmentId = (Form.useWatch('departmentId', form) as string | undefined) ?? doc?.departmentId ?? undefined
  const method = (Form.useWatch('method', form) as PaymentMethod | undefined) ?? doc?.method ?? 'Cash'
  const deduction = (Form.useWatch('deductionAmount', form) as number | null | undefined) ?? 0
  const watchedLines = Form.useWatch('lines', form) as SundryLineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const sheets = useSundryPaymentSheets(departmentId ?? null, doc?.id ?? null)

  const { loadSaved, targetId } = useDocumentFormSync(form, id, doc, (d) => ({
    date: dayjs(d.date),
    departmentId: d.departmentId ?? undefined,
    payeeName: d.payeeName ?? '',
    method: d.method,
    bankName: d.bankName ?? '',
    bankBranch: d.bankBranch ?? '',
    bankAccountNumber: d.bankAccountNumber ?? '',
    bankAccountName: d.bankAccountName ?? '',
    deductionAmount: d.deductionAmount,
    deductionReason: d.deductionReason ?? '',
    note: d.note ?? '',
    lines: d.lines.map((l) => ({ id: l.id, sundryExpenseSheetId: l.sundryExpenseSheetId ?? undefined, description: l.description })),
  }))

  /** Thông tin dòng theo Id bảng kê: bảng kê vừa chọn ưu tiên, sau đó dòng đã lưu. */
  const infoOf = (sheetId: string | undefined): SheetInfo | undefined => {
    if (!sheetId) return undefined
    const fresh = picked.get(sheetId)
    if (fresh) return fresh
    const saved = doc?.lines.find((l) => l.sundryExpenseSheetId === sheetId)
    return saved ? { no: saved.sundryExpenseSheetNo ?? '', date: saved.invoiceDate, amount: saved.amount } : undefined
  }

  const selectable = (exclude: Set<string>) => (sheets.data ?? []).filter((s) => !s.heldBy && !exclude.has(s.id))

  const addSheets = (rows: SundryPaymentSheetOption[]) => {
    setPicked((prev) => {
      const next = new Map(prev)
      for (const s of rows) next.set(s.id, { no: s.no, date: s.date, amount: s.totalAmount })
      return next
    })
    const current = (form.getFieldValue('lines') as SundryLineValues[] | undefined) ?? []
    form.setFieldValue('lines', [...current, ...rows.map((s) => ({ id: null, sundryExpenseSheetId: s.id, description: '' }))])
    setPicking(false)
  }

  // Mở từ bảng kê ("Lập đề nghị thanh toán 1C"): chọn sẵn bộ phận và bảng kê – một lần khi danh sách bảng kê về.
  const prefilled = useRef(false)
  const deptParam = params.get('dept')
  const sheetParam = params.get('sheet')
  useEffect(() => {
    if (!isNew || !deptParam || prefilled.current) return
    if (!form.getFieldValue('departmentId')) form.setFieldValue('departmentId', deptParam)
    if (!sheets.data) return
    prefilled.current = true
    addSheets(selectable(new Set()).filter((s) => s.id === sheetParam))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isNew, deptParam, sheetParam, sheets.data])

  const departmentOptions = useMemo(
    () => (departments.data ?? []).filter((d) => d.isActive).map((d) => ({ value: d.id, label: `${d.code} – ${d.name}` })),
    [departments.data],
  )

  const openPicker = () => {
    if (!departmentId) {
      message.warning('Hãy chọn bộ phận trước khi chọn bảng kê tạp vụ.')
      return
    }
    setPicking(true)
  }

  const persist = async (): Promise<PaymentRequest | null> => {
    let values: SundryFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng.')
      return null
    }
    setError(null)
    const lineValues = (form.getFieldValue('lines') as SundryLineValues[] | undefined) ?? []
    const transfer = values.method === 'BankTransfer'
    const input: SavePaymentRequestInput = {
      form: '1C',
      date: values.date.format('YYYY-MM-DD'),
      purchaseOrderId: null,
      departmentId: values.departmentId ?? doc?.departmentId ?? null,
      payeeName: orNull(values.payeeName),
      method: values.method,
      bankAccountId: null,
      bankName: transfer ? orNull(values.bankName) : null,
      bankBranch: transfer ? orNull(values.bankBranch) : null,
      bankAccountNumber: transfer ? orNull(values.bankAccountNumber) : null,
      bankAccountName: transfer ? orNull(values.bankAccountName) : null,
      deductionAmount: values.deductionAmount ?? 0,
      deductionReason: orNull(values.deductionReason),
      note: orNull(values.note),
      lines: lineValues.map((l) => ({ receiptConfirmationId: null, sundryExpenseSheetId: l.sundryExpenseSheetId!, description: orNull(l.description) })),
    }
    try {
      const target = targetId()
      const saved = target ? await mutations.update.mutateAsync({ id: target, input }) : await mutations.create.mutateAsync(input)
      setPicked(new Map())
      loadSaved(saved)
      return saved
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được phiếu.')
      return null
    }
  }

  const { save, actions } = useApprovalCommands({
    isNew, doc, readOnly, persist, mutations, listPath: PAYMENT_REQUEST_PATH, noun: 'đề nghị thanh toán', setError, docType: 'DNT',
  })

  const readonlyCell = (text: string) => <span className="line-grid-readonly">{text}</span>
  const columns: LineColumn[] = [
    {
      key: 'sheet', title: 'Bảng kê tạp vụ', width: 150,
      render: (field, index) => {
        const sheetId = lines[index]?.sundryExpenseSheetId
        const info = infoOf(sheetId)
        return (
          <>
            <Form.Item name={[field.name, 'id']} hidden>
              <Input />
            </Form.Item>
            <Form.Item name={[field.name, 'sundryExpenseSheetId']} hidden rules={[{ required: true, message: 'Dòng chưa có bảng kê.' }]}>
              <Input />
            </Form.Item>
            {info ? <DocNo no={info.no} docType="BKT" id={sheetId} /> : null}
          </>
        )
      },
    },
    { key: 'sheetDate', title: 'Ngày bảng kê', width: 100, render: (_, i) => readonlyCell(formatDate(infoOf(lines[i]?.sundryExpenseSheetId)?.date ?? null)) },
    {
      key: 'description', title: 'Nội dung thanh toán', width: 360,
      render: (field) => (
        <Form.Item name={[field.name, 'description']} noStyle rules={[{ max: 500 }]}>
          <Input variant="borderless" maxLength={500} placeholder="Trống = tự ghi theo bảng kê" aria-label={`Nội dung thanh toán dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'amount', title: 'Số tiền', width: 130, align: 'right',
      render: (_, i) => { const x = infoOf(lines[i]?.sundryExpenseSheetId); return readonlyCell(x ? formatMoney(x.amount) : '') },
    },
  ]

  const total = lines.reduce((sum, l) => sum + (infoOf(l?.sundryExpenseSheetId)?.amount ?? 0), 0)
  const statusLabel = doc ? (doc.paidDate ? 'Đã chi' : withPendingApprovers(purchaseStatusLabels[doc.status], doc.approvals)) : ''
  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: statusLabel, tone: doc?.paidDate ? ('success' as const) : doc ? purchaseStatusTone[doc.status] : ('neutral' as const) }

  return (
    <>
      <DocumentForm
        approval={doc ? { steps: doc.approvals, createdByName: doc.createdByName, createdAt: doc.createdAt, stageLabels: PURCHASE_STAGE_LABELS } : undefined}
        title="Đề nghị thanh toán tổng hợp tạp vụ (1C)"
        number={isNew ? null : (doc?.no ?? '')}
        audit={{ entityType: 'PaymentRequest', id: doc?.id }}
        printPath={doc ? `/api/payment-requests/${doc.id}/pdf` : undefined}
        status={status}
        meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
        loading={payment.isLoading}
        error={error ?? (payment.error ? payment.error.message : null)}
        notice={
          <>
            {SUNDRY_FORMS_SIMULATED && <Alert type="warning" showIcon title={SUNDRY_SIMULATED_NOTICE} />}
            {doc?.rejectReason && doc.status === 'Draft' ? (
              <Alert type="warning" showIcon title={`Bị từ chối: ${doc.rejectReason}`} />
            ) : (
              doc?.actions.readOnlyReason && <Alert type="info" showIcon title={doc.actions.readOnlyReason} />
            )}
            {doc?.cancelReason && <Alert type="error" showIcon title={`Đã hủy: ${doc.cancelReason}`} />}
            {doc?.paidDate && (
              <Alert type="success" showIcon
                title={`Đã chi ngày ${formatDate(doc.paidDate)} – ${doc.paidByName ?? ''}${doc.paymentNote ? `. ${doc.paymentNote}` : ''}`} />
            )}
          </>
        }
        form={form}
        initialValues={{ date: dayjs(), method: 'Cash', deductionAmount: 0, lines: [] }}
        readOnly={readOnly}
        onSave={save}
        onAddLine={() => addRef.current?.()}
        actions={
          <>
            {doc?.canPay && (
              <Button icon={<DollarOutlined />} onClick={() => setPaying(true)}>
                Ghi Đã chi
              </Button>
            )}
            {actions}
          </>
        }
        header={
          <>
            <Form.Item label="Ngày đề nghị" name="date" rules={[{ required: true, message: 'Chọn ngày.' }]}>
              <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
            </Form.Item>
            {isNew ? (
              <Form.Item label="Bộ phận" name="departmentId" rules={[{ required: true, message: 'Chọn bộ phận của các bảng kê.' }]}>
                <Select options={departmentOptions} loading={departments.isLoading} placeholder="Bộ phận của các bảng kê tạp vụ"
                  onChange={() => form.setFieldValue('lines', [])} />
              </Form.Item>
            ) : (
              <Form.Item label="Bộ phận">
                <Input value={departments.data?.find((d) => d.id === doc?.departmentId)?.name ?? ''} disabled />
              </Form.Item>
            )}
            <Form.Item label="Người nhận tiền" name="payeeName" rules={[{ required: true, whitespace: true, message: 'Nhập người nhận tiền.' }, { max: 200 }]}>
              <Input maxLength={200} placeholder="Người đã ứng chi tạp vụ" />
            </Form.Item>
            <Form.Item label="Hình thức" name="method" rules={[{ required: true, message: 'Chọn hình thức.' }]}>
              <Select showSearch={false} options={Object.entries(paymentMethodLabels).map(([value, label]) => ({ value, label }))} />
            </Form.Item>
            {method === 'BankTransfer' && (
              <>
                <Form.Item label="Ngân hàng" name="bankName" rules={[{ required: true, whitespace: true, message: 'Nhập tên ngân hàng.' }, { max: 200 }]}>
                  <Input maxLength={200} />
                </Form.Item>
                <Form.Item label="Chi nhánh" name="bankBranch" rules={[{ max: 200 }]}>
                  <Input maxLength={200} />
                </Form.Item>
                <Form.Item label="Số tài khoản" name="bankAccountNumber" rules={[{ required: true, whitespace: true, message: 'Nhập số tài khoản.' }, { max: 50 }]}>
                  <Input maxLength={50} />
                </Form.Item>
                <Form.Item label="Chủ tài khoản" name="bankAccountName" rules={[{ max: 200 }]}>
                  <Input maxLength={200} />
                </Form.Item>
              </>
            )}
            <Form.Item label="Số tiền giảm trừ" name="deductionAmount">
              <InputNumber {...vnNumberInput} min={0} precision={0} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Lý do giảm trừ" name="deductionReason" rules={[{ max: 1000 }]}>
              <Input placeholder="Tạm ứng đã nhận…" />
            </Form.Item>
            <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]} className="document-form-wide">
              <Input />
            </Form.Item>
          </>
        }
        lines={
          <>
            {!readOnly && (
              <Button icon={<FileAddOutlined />} onClick={openPicker} style={{ marginBottom: 6 }}>
                Chọn bảng kê tạp vụ
              </Button>
            )}
            <LineGrid name="lines" auditEntity="PaymentRequestLine" columns={columns} readOnly={readOnly} addRef={addRef}
              newLine={() => ({ id: null })} addLines={openPicker} />
          </>
        }
        footer={
          <>
            <DocumentTotal label="Tổng cộng" value={formatMoney(total)} />
            <DocumentTotal label="Số tiền giảm trừ" value={formatMoney(deduction ?? 0)} />
            <DocumentTotal label="Số tiền đề nghị thanh toán" value={formatMoney(total - (deduction ?? 0))} strong />
          </>
        }
        extraTabs={[
          ...(doc ? [{ key: 'approvals', label: 'Lịch sử duyệt', children: <ApprovalHistory steps={doc.approvals} stageLabels={PURCHASE_STAGE_LABELS} createdByName={doc.createdByName} createdAt={doc.createdAt} /> }] : []),
          attachmentTab,
        ]}
      />
      {picking && (
        <SheetPicker
          rows={sheets.data ?? []}
          selectable={selectable(new Set(lines.map((l) => l?.sundryExpenseSheetId).filter((x): x is string => !!x)))}
          loading={sheets.isLoading}
          onCancel={() => setPicking(false)}
          onPick={addSheets}
        />
      )}
      {paying && doc && <PayDialog doc={doc} onClose={() => setPaying(false)} />}
    </>
  )
}

/**
 * Hộp chọn bảng kê tạp vụ 2C đã duyệt của bộ phận cho đề nghị 1C: bảng kê đã ở đề nghị khác hiện mờ kèm lý do.
 * @param rows Mọi bảng kê đã duyệt của bộ phận.
 * @param selectable Bảng kê chọn được.
 * @param loading Đang tải.
 * @param onPick Thêm các bảng kê đã chọn.
 * @param onCancel Đóng.
 */
function SheetPicker({
  rows, selectable, loading, onPick, onCancel,
}: {
  rows: SundryPaymentSheetOption[]
  selectable: SundryPaymentSheetOption[]
  loading: boolean
  onPick: (rows: SundryPaymentSheetOption[]) => void
  onCancel: () => void
}) {
  const [selected, setSelected] = useState<string[]>([])
  const ok = new Set(selectable.map((r) => r.id))
  return (
    <Modal open title="Chọn bảng kê chi phí tạp vụ đã duyệt" width={820} okText={`Thêm ${selected.length} dòng`} okButtonProps={{ disabled: selected.length === 0 }}
      cancelText="Đóng" onCancel={onCancel} onOk={() => onPick(selectable.filter((r) => selected.includes(r.id)))}>
      <Table<SundryPaymentSheetOption>
        size="small"
        rowKey="id"
        loading={loading}
        pagination={false}
        scroll={{ y: 360 }}
        dataSource={rows}
        rowSelection={{ selectedRowKeys: selected, onChange: (keys) => setSelected(keys as string[]), getCheckboxProps: (r) => ({ disabled: !ok.has(r.id) }) }}
        locale={{ emptyText: 'Bộ phận chưa có bảng kê tạp vụ đã duyệt.' }}
        columns={[
          { title: 'Bảng kê', dataIndex: 'no', width: 150 },
          { title: 'Ngày', dataIndex: 'date', width: 90, render: (v: string) => formatDate(v) },
          { title: 'Số khoản chi', dataIndex: 'lineCount', width: 90, align: 'right' },
          { title: 'Tổng bảng kê', dataIndex: 'totalAmount', width: 120, align: 'right', render: (v: number) => formatMoney(v) },
          { title: 'Ghi chú', key: 'why', render: (_, r) => (r.heldBy ? `Đã ở đề nghị ${r.heldBy}` : (r.note ?? '')) },
        ]}
      />
    </Modal>
  )
}

/**
 * Hộp chọn phiếu nhận hàng (XNH đã xác nhận) của đơn: chọn được phiếu có hóa đơn, chưa thuộc đề nghị khác; phiếu chưa có hóa đơn / đã đề nghị
 * hiện mờ kèm lý do để người lập biết cần cập nhật hóa đơn trên XNH.
 * @param rows Mọi XNH đã xác nhận của đơn.
 * @param selectable XNH chọn được.
 * @param loading Đang tải.
 * @param onPick Thêm các XNH đã chọn.
 * @param onCancel Đóng.
 */
function ReceiptPicker({
  rows, selectable, loading, onPick, onCancel,
}: {
  rows: PaymentReceiptOption[]
  selectable: PaymentReceiptOption[]
  loading: boolean
  onPick: (rows: PaymentReceiptOption[]) => void
  onCancel: () => void
}) {
  const [selected, setSelected] = useState<string[]>([])
  const ok = new Set(selectable.map((r) => r.id))
  return (
    <Modal open title="Chọn phiếu nhận hàng có hóa đơn" width={980} okText={`Thêm ${selected.length} dòng`} okButtonProps={{ disabled: selected.length === 0 }}
      cancelText="Đóng" onCancel={onCancel} onOk={() => onPick(selectable.filter((r) => selected.includes(r.id)))}>
      <Table<PaymentReceiptOption>
        size="small"
        rowKey="id"
        loading={loading}
        pagination={false}
        scroll={{ y: 360 }}
        dataSource={rows}
        rowSelection={{ selectedRowKeys: selected, onChange: (keys) => setSelected(keys as string[]), getCheckboxProps: (r) => ({ disabled: !ok.has(r.id) }) }}
        locale={{ emptyText: 'Đơn chưa có phiếu nhận hàng đã xác nhận.' }}
        columns={[
          { title: 'Phiếu nhận hàng', dataIndex: 'no', width: 150 },
          { title: 'Ngày nhận', dataIndex: 'date', width: 90, render: (v: string) => formatDate(v) },
          { title: 'Số phiếu giao', dataIndex: 'deliveryNoteNo', width: 110 },
          { title: 'Hóa đơn', key: 'invoice', width: 160, render: (_, r) => (r.invoiceNo ? `${r.invoiceSeries ?? ''} ${r.invoiceNo}`.trim() : '') },
          { title: 'Ngày HĐ', dataIndex: 'invoiceDate', width: 90, render: (v: string | null) => formatDate(v) },
          { title: 'Tiền hóa đơn', dataIndex: 'invoiceAmount', width: 120, align: 'right', render: (v: number | null) => (v === null ? '' : formatMoney(v)) },
          { title: 'Tiền nhận (gồm VAT)', dataIndex: 'receivedAmount', width: 130, align: 'right', render: (v: number) => formatMoney(v) },
          {
            title: 'Ghi chú', key: 'why', render: (_, r) =>
              r.heldBy ? `Đã ở đề nghị ${r.heldBy}` : !r.invoiceNo || !(r.invoiceAmount ?? 0) ? 'Chưa có hóa đơn – cập nhật hóa đơn trên phiếu nhận hàng' : '',
          },
        ]}
      />
    </Modal>
  )
}

/**
 * Hộp "Ghi Đã chi" của kế toán (Y8-6): ngày chi (không sau hôm nay, không trước ngày duyệt – máy chủ kiểm tra), ghi chú số ủy nhiệm chi / phiếu
 * chi. Ghi rồi không đổi được.
 * @param doc Đề nghị đã duyệt.
 * @param onClose Đóng.
 */
function PayDialog({ doc, onClose }: { doc: PaymentRequest; onClose: () => void }) {
  const { message } = App.useApp()
  const pay = useMarkPaymentPaid()
  const [paidDate, setPaidDate] = useState<string | null>(dayjs().format('YYYY-MM-DD'))
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  return (
    <Modal open title={`Ghi Đã chi – ${doc.no}`} okText="Ghi Đã chi" cancelText="Không" confirmLoading={pay.isPending} onCancel={onClose}
      onOk={async () => {
        if (!paidDate) return setError('Nhập ngày chi.')
        try {
          await pay.mutateAsync({ id: doc.id, paidDate, note: orNull(note) })
          message.success(`Đã ghi Đã chi đề nghị ${doc.no}.`)
          onClose()
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không ghi được.')
        }
      }}>
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <p>Số tiền đề nghị: <strong>{formatMoney(doc.payableAmount)}</strong> – {paymentMethodLabels[doc.method]}. Ghi rồi không đổi được.</p>
      <label htmlFor="pay-date">Ngày chi</label>
      <DateInput id="pay-date" value={paidDate} onChange={setPaidDate} aria-label="Ngày chi" />
      <label htmlFor="pay-note" style={{ display: 'block', marginTop: 8 }}>Ghi chú (số ủy nhiệm chi, phiếu chi…)</label>
      <Input id="pay-note" maxLength={1000} value={note} onChange={(e) => setNote(e.target.value)} />
    </Modal>
  )
}
