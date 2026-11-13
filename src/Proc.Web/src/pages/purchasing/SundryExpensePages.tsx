import { AccountBookOutlined } from '@ant-design/icons'
import { Alert, Button, DatePicker, Form, Input, InputNumber } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import type { DateRange } from '../../api/logs'
import { useDepartments } from '../../api/organization'
import { PURCHASE_STAGE_LABELS, purchaseStatusLabels, purchaseStatusTone, usePurchaseMutations, withPendingApprovers } from '../../api/purchasing'
import {
  useSundryExpenseSheet,
  useSundryExpenseSheets,
  type SaveSundryExpenseSheetInput,
  type SundryExpenseSheet,
  type SundryExpenseSheetListItem,
} from '../../api/sundryExpenseSheets'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DocNo } from '../../classic/DocNo'
import { Select } from '../../classic/Select'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import type { GridColumn } from '../../components/DataGrid/types'
import { DocumentForm, DocumentTotal } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { useDocumentFormSync } from '../../components/DocumentForm/useDocumentFormSync'
import { ListPage } from '../../components/ListPage/ListPage'
import { DATE_INPUT_FORMATS, formatDateTime, formatMoney, vnNumberInput } from '../../utils/format'
import { LogDateRangeFilter } from '../system/logFilters'
import { ApprovalHistory } from './ApprovalHistory'
import { BudgetNotice } from './BudgetNotice'
import { useApprovalCommands } from './useApprovalCommands'

/** Đường dẫn danh sách bảng kê chi phí tạp vụ (màn hình BKT). */
export const SUNDRY_EXPENSE_PATH = '/mua-hang/bang-ke-tap-vu'

const listColumns: GridColumn<SundryExpenseSheetListItem>[] = [
  { key: 'no', title: 'Số phiếu', width: 150, value: (r) => r.no, docRef: (r) => ({ docType: 'BKT', id: r.id }) },
  { key: 'date', title: 'Ngày bảng kê', width: 100, type: 'date', value: (r) => r.date },
  { key: 'department', title: 'Bộ phận', width: 220, value: (r) => `${r.departmentCode} – ${r.departmentName}`, groupable: true },
  { key: 'lines', title: 'Số khoản chi', width: 90, type: 'quantity', value: (r) => r.lineCount },
  { key: 'total', title: 'Tổng bảng kê', width: 130, type: 'money', value: (r) => r.totalAmount },
  { key: 'over', title: 'Vượt định mức', width: 100, value: (r) => (r.isOverBudget ? 'Vượt' : ''), groupable: true },
  {
    key: 'status', title: 'Trạng thái', width: 110, type: 'status', groupable: true,
    value: (r) => purchaseStatusLabels[r.status], tone: (r) => purchaseStatusTone[r.status],
  },
  {
    key: 'payment', title: 'Đề nghị thanh toán', width: 140, value: (r) => r.paymentRequestNo,
    docRef: (r) => (r.paymentRequestId ? { docType: 'DNT', id: r.paymentRequestId } : null),
  },
  { key: 'creator', title: 'Người lập', width: 150, value: (r) => r.createdByName, groupable: true },
]

/**
 * Màn hình Mua hàng → Bảng kê tạp vụ (BKT – lát Y11a, design.md mục 19.2 loại C): bộ phận kê chi phí lặt vặt đã phát sinh, không qua PR/RFQ/PO;
 * tổng tính vào định mức tháng; duyệt Trưởng phòng ban → GĐ khối → Phòng TC-NQ-KT → Chủ tịch; đã duyệt thì gộp vào đề nghị thanh toán 1C.
 */
export function SundryExpenseListPage() {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const [range, setRange] = useState<DateRange>(() => ({ from: dayjs().subtract(89, 'day').format('YYYY-MM-DD'), to: dayjs().format('YYYY-MM-DD') }))
  const list = useSundryExpenseSheets(range)
  return (
    <ListPage
      title="Bảng kê chi phí tạp vụ (BKT)"
      exportFileName="bang-ke-tap-vu"
      rows={list.data ?? []}
      columns={listColumns}
      rowKey={(r) => r.id}
      auditEntity="SundryExpenseSheet"
      loading={list.isLoading}
      canCreate={hasPermission(Permission.SundryCreate)}
      onCreate={() => navigate(`${SUNDRY_EXPENSE_PATH}/moi`)}
      onOpenRow={(r) => navigate(`${SUNDRY_EXPENSE_PATH}/${r.id}`)}
      isMarked={(r) => r.status === 'Draft' || r.status === 'Submitted' || (r.status === 'Approved' && !r.paymentRequestNo)}
      filters={<LogDateRangeFilter value={range} onChange={setRange} />}
    />
  )
}

/** Một khoản chi trên form. */
interface LineValues {
  id: string | null
  expenseDate?: Dayjs | null
  description?: string
  vendor?: string
  receiptNo?: string
  amount?: number | null
}

interface FormValues {
  date: Dayjs
  departmentId?: string
  note?: string
  lines: LineValues[]
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Trang bảng kê chi phí tạp vụ 2C (lát Y11a) trên khuôn DocumentForm. Đầu phiếu: ngày bảng kê (tháng tính định mức), bộ phận, ghi chú. Lưới: ngày
 * chi (không sau ngày bảng kê), nội dung chi, nơi mua / người bán, số hóa đơn / chứng từ, số tiền đã chi. Định mức tháng tính lúc lưu và lúc gửi;
 * vượt thì cảnh báo vàng và phiếu qua thêm chặng duyệt vượt định mức (không chặn – Q5).
 */
export function SundryExpensePage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<FormValues>()
  const sheet = useSundryExpenseSheet(isNew ? null : id)
  const mutations = usePurchaseMutations<SundryExpenseSheet, SaveSundryExpenseSheetInput>('sundry-expense-sheets')
  const departments = useDepartments()
  const [error, setError] = useState<string | null>(null)
  const addRef = useRef<(() => void) | null>(null)
  const doc = sheet.data
  const readOnly = isNew ? !hasPermission(Permission.SundryCreate) : !doc?.actions.canEdit
  const attachmentTab = useAttachmentTab('SundryExpenseSheet', doc?.id, hasPermission(Permission.SundryCreate))
  const watchedLines = Form.useWatch('lines', form) as LineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])

  const { loadSaved, targetId } = useDocumentFormSync(form, id, doc, (d) => ({
    date: dayjs(d.date),
    departmentId: d.departmentId,
    note: d.note ?? '',
    lines: d.lines.map((l) => ({
      id: l.id, expenseDate: dayjs(l.expenseDate), description: l.description, vendor: l.vendor ?? '', receiptNo: l.receiptNo ?? '', amount: l.amount,
    })),
  }))

  // Chỉ chọn bộ phận đang dùng; bộ phận phiếu đã chọn nay ngừng dùng vẫn giữ để hiện tên (như đề nghị mua – design.md mục 18.5).
  const departmentOptions = useMemo(
    () =>
      (departments.data ?? [])
        .filter((d) => d.isActive || d.id === doc?.departmentId)
        .map((d) => ({ value: d.id, label: `${d.code} – ${d.name}${d.isActive ? '' : ' (ngừng dùng)'}` })),
    [departments.data, doc?.departmentId],
  )

  const persist = async (): Promise<SundryExpenseSheet | null> => {
    let values: FormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng.')
      return null
    }
    setError(null)
    const lineValues = ((form.getFieldValue('lines') as LineValues[] | undefined) ?? [])
      // Dòng trống hoàn toàn (chưa gõ gì) thì bỏ – lưới luôn có dòng trống cuối để gõ tiếp.
      .filter((l) => orNull(l.description) || l.amount)
    const input: SaveSundryExpenseSheetInput = {
      date: values.date.format('YYYY-MM-DD'),
      departmentId: values.departmentId ?? '',
      note: orNull(values.note),
      lines: lineValues.map((l) => ({
        expenseDate: (l.expenseDate ?? values.date).format('YYYY-MM-DD'),
        description: orNull(l.description),
        vendor: orNull(l.vendor),
        receiptNo: orNull(l.receiptNo),
        amount: l.amount ?? 0,
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

  const { save, actions } = useApprovalCommands({
    isNew, doc, readOnly, persist, mutations, listPath: SUNDRY_EXPENSE_PATH, noun: 'bảng kê chi phí tạp vụ', setError, docType: 'BKT',
  })

  const columns: LineColumn[] = [
    {
      key: 'expenseDate', title: 'Ngày chi', width: 120, required: true,
      render: (field) => (
        <>
          <Form.Item name={[field.name, 'id']} hidden>
            <Input />
          </Form.Item>
          <Form.Item name={[field.name, 'expenseDate']} noStyle rules={[{ required: true, message: 'Nhập ngày chi.' }]}>
            <DatePicker variant="borderless" format={DATE_INPUT_FORMATS} style={{ width: '100%' }} aria-label={`Ngày chi dòng ${field.name + 1}`} />
          </Form.Item>
        </>
      ),
    },
    {
      key: 'description', title: 'Nội dung chi', width: 320, required: true,
      render: (field) => (
        <Form.Item name={[field.name, 'description']} noStyle rules={[{ required: true, whitespace: true, message: 'Nhập nội dung chi.' }, { max: 500 }]}>
          <Input variant="borderless" maxLength={500} aria-label={`Nội dung chi dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'vendor', title: 'Nơi mua / người bán', width: 200,
      render: (field) => (
        <Form.Item name={[field.name, 'vendor']} noStyle rules={[{ max: 200 }]}>
          <Input variant="borderless" maxLength={200} aria-label={`Nơi mua dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'receiptNo', title: 'Số hóa đơn / chứng từ', width: 150,
      render: (field) => (
        <Form.Item name={[field.name, 'receiptNo']} noStyle rules={[{ max: 50 }]}>
          <Input variant="borderless" maxLength={50} aria-label={`Số hóa đơn dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'amount', title: 'Số tiền', width: 130, align: 'right', required: true,
      render: (field) => (
        <Form.Item name={[field.name, 'amount']} noStyle rules={[{ required: true, type: 'number', min: 1, message: 'Số tiền > 0.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={0} aria-label={`Số tiền dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const total = lines.reduce((sum, l) => sum + (l?.amount ?? 0), 0)
  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? withPendingApprovers(purchaseStatusLabels[doc.status], doc.approvals) : '', tone: doc ? purchaseStatusTone[doc.status] : ('neutral' as const) }

  return (
    <DocumentForm
      approval={doc ? { steps: doc.approvals, createdByName: doc.createdByName, createdAt: doc.createdAt, stageLabels: PURCHASE_STAGE_LABELS } : undefined}
      title="Bảng kê chi phí tạp vụ"
      number={isNew ? null : (doc?.no ?? '')}
      audit={{ entityType: 'SundryExpenseSheet', id: doc?.id }}
      printPath={doc ? `/api/sundry-expense-sheets/${doc.id}/pdf` : undefined}
      status={status}
      meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
      loading={sheet.isLoading}
      error={error ?? (sheet.error ? sheet.error.message : null)}
      notice={
        <>
          {doc?.rejectReason && doc.status === 'Draft' ? (
            <Alert type="warning" showIcon title={`Bị từ chối: ${doc.rejectReason}`} />
          ) : (
            doc?.actions.readOnlyReason && <Alert type="info" showIcon title={doc.actions.readOnlyReason} />
          )}
          {doc?.cancelReason && <Alert type="error" showIcon title={`Đã hủy: ${doc.cancelReason}`} />}
          <BudgetNotice budget={doc?.budget} draft={doc?.status === 'Draft'} />
          {doc?.paymentRequestNo && (
            <Alert type="info" showIcon
              title={<>Đã gộp vào đề nghị thanh toán <DocNo no={doc.paymentRequestNo} docType="DNT" id={doc.paymentRequestId ?? undefined} /></>} />
          )}
        </>
      }
      form={form}
      initialValues={{ date: dayjs(), lines: [{ id: null, expenseDate: dayjs() }] }}
      readOnly={readOnly}
      onSave={save}
      onAddLine={() => addRef.current?.()}
      actions={
        <>
          {/* Bảng kê đã duyệt, chưa đề nghị: lập đề nghị thanh toán tổng hợp 1C chọn sẵn bộ phận và bảng kê (lát Y11b). */}
          {doc?.status === 'Approved' && !doc.paymentRequestNo && hasPermission(Permission.PaymentCreate) && (
            <Button icon={<AccountBookOutlined />}
              onClick={() => navigate(`/mua-hang/de-nghi-thanh-toan/moi?form=1C&dept=${doc.departmentId}&sheet=${doc.id}`)}>
              Lập đề nghị thanh toán 1C
            </Button>
          )}
          {actions}
        </>
      }
      header={
        <>
          <Form.Item label="Ngày bảng kê" name="date" rules={[{ required: true, message: 'Chọn ngày.' }]}
            tooltip="Tháng của ngày bảng kê là tháng tính định mức của bộ phận; ngày chi các dòng không sau ngày này.">
            <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Bộ phận" name="departmentId" rules={[{ required: true, message: 'Chọn bộ phận phát sinh chi phí.' }]}>
            <Select options={departmentOptions} loading={departments.isLoading} placeholder="Bộ phận phát sinh chi phí" />
          </Form.Item>
          <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]} className="document-form-wide">
            <Input />
          </Form.Item>
        </>
      }
      lines={
        <LineGrid name="lines" auditEntity="SundryExpenseLine" columns={columns} readOnly={readOnly} addRef={addRef}
          newLine={() => ({ id: null, expenseDate: (form.getFieldValue('date') as Dayjs | undefined) ?? dayjs() })} />
      }
      footer={<DocumentTotal label="Tổng bảng kê" value={formatMoney(total)} strong />}
      extraTabs={[
        ...(doc ? [{ key: 'approvals', label: 'Lịch sử duyệt', children: <ApprovalHistory steps={doc.approvals} stageLabels={PURCHASE_STAGE_LABELS} createdByName={doc.createdByName} createdAt={doc.createdAt} /> }] : []),
        attachmentTab,
      ]}
    />
  )
}
