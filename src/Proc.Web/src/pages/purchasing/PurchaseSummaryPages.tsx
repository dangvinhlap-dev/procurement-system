import { FileAddOutlined } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Form, Input, Modal, Table } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { useConstructionProjectOptions } from '../../api/constructionProjects'
import { useDepartments } from '../../api/organization'
import {
  purchaseSummaryFormCodes,
  purchaseSummaryFormLabels,
  usePurchaseSummaries,
  usePurchaseSummary,
  usePurchaseSummaryCandidates,
  type PurchaseSummary,
  type PurchaseSummaryCandidate,
  type PurchaseSummaryForm,
  type PurchaseSummaryFrame,
  type PurchaseSummaryListItem,
  type SavePurchaseSummaryInput,
} from '../../api/purchaseSummaries'
import {
  PURCHASE_STAGE_LABELS,
  purchaseCategoryLabels,
  purchaseStatusLabels,
  purchaseStatusTone,
  usePurchaseMutations,
  withPendingApprovers,
  type PurchaseCategory,
} from '../../api/purchasing'
import { usePurchasingUnitOptions } from '../../api/purchasingUnits'
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
import { DATE_INPUT_FORMATS, formatDate, formatDateTime, formatMoney, formatQuantity } from '../../utils/format'
import { ApprovalHistory } from './ApprovalHistory'
import { useApprovalCommands } from './useApprovalCommands'

/** Đường dẫn danh sách bảng tổng hợp đề xuất mua (màn hình BTH). */
export const PURCHASE_SUMMARY_PATH = '/mua-hang/bang-tong-hop'

const listColumns: GridColumn<PurchaseSummaryListItem>[] = [
  { key: 'no', title: 'Số phiếu', width: 150, value: (r) => r.no, docRef: (r) => ({ docType: 'BTH', id: r.id }) },
  { key: 'date', title: 'Ngày lập', width: 100, type: 'date', value: (r) => r.date },
  { key: 'form', title: 'Biểu mẫu', width: 80, value: (r) => purchaseSummaryFormCodes[r.form], groupable: true },
  { key: 'department', title: 'Bộ phận đề xuất', width: 200, value: (r) => r.departmentName, groupable: true },
  { key: 'project', title: 'Công trình', width: 120, value: (r) => r.constructionProjectCode, groupable: true },
  { key: 'lines', title: 'Số dòng', width: 80, type: 'quantity', value: (r) => r.lineCount },
  { key: 'amount', title: 'Tổng đề xuất', width: 140, type: 'money', value: (r) => r.totalAmount },
  {
    key: 'status', title: 'Trạng thái', width: 110, type: 'status', groupable: true,
    value: (r) => purchaseStatusLabels[r.status], tone: (r) => purchaseStatusTone[r.status],
  },
  { key: 'creator', title: 'Người lập', width: 150, value: (r) => r.createdByName, groupable: true },
]

/**
 * Màn hình Mua hàng → Bảng tổng hợp đề xuất (BTH – lát Y5c-2, design.md mục 19.3 bước 2): Phòng CUVT gom các dòng đề nghị mua đã duyệt còn
 * cần mua theo biểu mẫu 2A (một công trình) / 2B (hàng hóa, dịch vụ), gán đơn vị mua, trình duyệt. Mọi công ty bắt buộc qua bảng đã duyệt
 * trước khi lên RFQ / PO (Y5c-1).
 */
export function PurchaseSummaryListPage() {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const list = usePurchaseSummaries()
  return (
    <ListPage
      title="Bảng tổng hợp đề xuất mua (BTH)"
      exportFileName="bang-tong-hop-de-xuat-mua"
      rows={list.data ?? []}
      columns={listColumns}
      rowKey={(r) => r.id}
      auditEntity="PurchaseSummary"
      loading={list.isLoading}
      canCreate={hasPermission(Permission.SummaryManage)}
      onCreate={() => navigate(`${PURCHASE_SUMMARY_PATH}/moi`)}
      onOpenRow={(r) => navigate(`${PURCHASE_SUMMARY_PATH}/${r.id}`)}
      isMarked={(r) => r.status === 'Draft' || r.status === 'Submitted'}
    />
  )
}

/** Dòng trên form: chỉ phần gửi lên máy chủ; thông tin hàng, SL, giá lấy từ {@link LineInfo}. */
interface LineValues {
  id: string | null
  requestLineId?: string
  purchasingUnitId?: string
  note?: string
}

interface FormValues {
  date: Dayjs
  form: PurchaseSummaryForm
  departmentId?: string
  constructionProjectId?: string
  note?: string
  lines: LineValues[]
}

/** Thông tin hiển thị của một dòng đề nghị – từ dòng đã lưu (SL, giá đã chụp) hoặc dòng vừa chọn (SL còn cần mua hiện tại). */
interface LineInfo {
  requestId: string
  requestNo: string
  requestCategory: string
  itemCode: string
  itemName: string
  uomCode: string
  qty: number
  estPrice: number
  amount: number
  neededDate: string | null
}

/** Chuỗi rỗng hoặc toàn khoảng trắng gửi lên API là null; còn lại bỏ khoảng trắng hai đầu. */
const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/** Nhãn loại mua của dòng đề nghị (A, B, DV). */
const categoryLabel = (category: string) => purchaseCategoryLabels[category as PurchaseCategory] ?? category

/**
 * Khung chi phí đầu bảng tổng hợp như biểu mẫu (lát Y5b): 2A dự toán – lũy kế đã mua – tổng đề xuất – còn lại; 2B ngân sách tháng – lũy kế đã
 * dùng (đã gồm các đề nghị của bảng) – còn lại. Vượt thì cảnh báo vàng, không chặn (như đề nghị mua – design.md mục 19.2, 18.24).
 * @param frame Khung do máy chủ tính khi xem phiếu.
 * @param form Biểu mẫu của phiếu.
 */
function SummaryFrameNotice({ frame, form }: { frame: PurchaseSummaryFrame; form: PurchaseSummaryForm }) {
  const f = frame
  let text: string
  if (form === 'Construction') {
    text =
      `Công trình ${f.projectCode ?? ''} – ${f.projectName ?? ''}: dự toán ${formatMoney(f.limitAmount ?? 0)}, lũy kế đã mua ` +
      `${formatMoney(f.accumulatedAmount)}, tổng đề xuất ${formatMoney(f.proposedAmount)}, còn lại ${formatMoney(f.remainingAmount ?? 0)}. Giá chưa VAT.`
  } else {
    const month = f.budgetMonth ? dayjs(f.budgetMonth).format('MM/YYYY') : ''
    text =
      f.limitAmount === null
        ? `Bộ phận chưa có ngân sách tháng ${month}; lũy kế đã dùng ${formatMoney(f.accumulatedAmount)}, tổng đề xuất ${formatMoney(f.proposedAmount)}.`
        : `Ngân sách tháng ${month}: ${formatMoney(f.limitAmount)}, lũy kế đã dùng ${formatMoney(f.accumulatedAmount)} (đã gồm các đề nghị của ` +
          `bảng), tổng đề xuất ${formatMoney(f.proposedAmount)}, còn lại ${formatMoney(f.remainingAmount ?? 0)}.`
  }
  return <Alert type={f.exceeded ? 'warning' : 'info'} showIcon title={f.exceeded ? `Vượt – ${text}` : text} />
}

/**
 * Trang phiếu bảng tổng hợp đề xuất mua 2A/2B (lát Y5c-2) trên khuôn DocumentForm. Đầu phiếu: ngày, biểu mẫu, bộ phận đề xuất, công trình
 * (2A), ghi chú. Dòng chỉ lấy từ đề nghị đã duyệt còn cần mua qua hộp "Chọn dòng đề nghị" (Thêm dòng / F2); mỗi dòng bắt buộc đơn vị mua.
 * SL, đơn giá do máy chủ chụp lúc lưu nháp và lúc gửi duyệt. Lệnh duyệt, rút về nháp, hủy dây chuyền dùng chung với PR/PO.
 */
export function PurchaseSummaryPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const { hasPermission } = useAuth()
  const { message } = App.useApp()
  const [form] = Form.useForm<FormValues>()
  const summary = usePurchaseSummary(isNew ? null : id)
  const mutations = usePurchaseMutations<PurchaseSummary, SavePurchaseSummaryInput>('purchase-summaries')
  const departments = useDepartments()
  const [error, setError] = useState<string | null>(null)
  const [picking, setPicking] = useState(false)
  // Thông tin dòng vừa chọn trong hộp chọn (chưa lưu), theo Id dòng đề nghị.
  const [picked, setPicked] = useState<Map<string, LineInfo>>(() => new Map())
  const addRef = useRef<(() => void) | null>(null)
  const doc = summary.data
  const readOnly = isNew ? !hasPermission(Permission.SummaryManage) : !doc?.actions.canEdit
  const units = usePurchasingUnitOptions(!readOnly)
  const attachmentTab = useAttachmentTab('PurchaseSummary', doc?.id, hasPermission(Permission.SummaryManage))

  const summaryForm = (Form.useWatch('form', form) as PurchaseSummaryForm | undefined) ?? doc?.form
  const departmentId = Form.useWatch('departmentId', form) as string | undefined
  const projectId = Form.useWatch('constructionProjectId', form) as string | undefined
  const watchedLines = Form.useWatch('lines', form) as LineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])

  const projectOptions = useConstructionProjectOptions(summaryForm === 'Construction')
  const projectSelect = useMemo(() => {
    const options = (projectOptions.data ?? []).map((p) => ({ value: p.id, label: `${p.code} – ${p.name}` }))
    if (doc?.constructionProjectId && !options.some((o) => o.value === doc.constructionProjectId))
      options.push({ value: doc.constructionProjectId, label: `${doc.frame.projectCode ?? ''} – ${doc.frame.projectName ?? ''} (không còn thực hiện)` })
    return options
  }, [projectOptions.data, doc])

  // Chỉ chọn bộ phận đang dùng; bộ phận phiếu đã chọn nay ngừng dùng vẫn giữ để hiện tên (design.md mục 18.5).
  const departmentOptions = useMemo(
    () =>
      (departments.data ?? [])
        .filter((d) => d.isActive || d.id === doc?.departmentId)
        .map((d) => ({ value: d.id, label: `${d.code} – ${d.name}${d.isActive ? '' : ' (ngừng dùng)'}` })),
    [departments.data, doc?.departmentId],
  )

  /** Thông tin dòng theo Id dòng đề nghị: dòng vừa chọn ưu tiên, sau đó dòng đã lưu. */
  const infoOf = (requestLineId: string | undefined): LineInfo | undefined => {
    if (!requestLineId) return undefined
    const fresh = picked.get(requestLineId)
    if (fresh) return fresh
    const saved = doc?.lines.find((l) => l.requestLineId === requestLineId)
    return saved
      ? {
          requestId: saved.requestId, requestNo: saved.requestNo, requestCategory: saved.requestCategory, itemCode: saved.itemCode,
          itemName: saved.itemName, uomCode: saved.uomCode, qty: saved.qty, estPrice: saved.estPrice, amount: saved.amount,
          neededDate: saved.neededDate,
        }
      : undefined
  }

  // Đơn vị đang dùng + đơn vị đã gán trên dòng đã lưu (giữ được dù nay ngừng dùng – lát Y5a).
  const unitOptions = useMemo(() => {
    const options = (units.data ?? []).map((u) => ({ value: u.id, label: `${u.code} – ${u.name}` }))
    for (const l of doc?.lines ?? [])
      if (!options.some((o) => o.value === l.purchasingUnitId))
        options.push({ value: l.purchasingUnitId, label: `${l.purchasingUnitCode} – ${l.purchasingUnitName}` })
    return options
  }, [units.data, doc?.lines])

  const { loadSaved, targetId } = useDocumentFormSync(form, id, doc, (d) => ({
    date: dayjs(d.date),
    form: d.form,
    departmentId: d.departmentId,
    constructionProjectId: d.constructionProjectId ?? undefined,
    note: d.note ?? '',
    lines: d.lines.map((l) => ({ id: l.id, requestLineId: l.requestLineId, purchasingUnitId: l.purchasingUnitId, note: l.note ?? '' })),
  }))

  /**
   * Đổi biểu mẫu / bộ phận / công trình làm các dòng đã chọn không còn hợp (mọi dòng cùng biểu mẫu, bộ phận, công trình – lát Y5b) nên bỏ
   * hết dòng, báo người dùng chọn lại.
   */
  const clearLines = () => {
    const count = (form.getFieldValue('lines') as LineValues[] | undefined)?.length ?? 0
    if (count === 0) return
    form.setFieldValue('lines', [])
    message.info(`Đã bỏ ${count} dòng vì đổi biểu mẫu, bộ phận hoặc công trình – hãy chọn lại dòng đề nghị.`)
  }

  /** Mở hộp chọn dòng đề nghị khi đầu phiếu đã đủ biểu mẫu, bộ phận (và công trình với 2A). */
  const openPicker = () => {
    if (!summaryForm || !departmentId) {
      message.warning('Hãy chọn biểu mẫu và bộ phận đề xuất trước khi chọn dòng đề nghị.')
      return
    }
    if (summaryForm === 'Construction' && !projectId) {
      message.warning('Biểu mẫu 2A: hãy chọn công trình trước khi chọn dòng đề nghị.')
      return
    }
    setPicking(true)
  }

  /** Thêm các dòng đề nghị đã chọn, gán sẵn đơn vị mua chọn trong hộp (nếu có). */
  const addPicked = (rows: PurchaseSummaryCandidate[], unitId: string | undefined) => {
    setPicked((prev) => {
      const next = new Map(prev)
      for (const r of rows)
        next.set(r.requestLineId, {
          requestId: r.requestId, requestNo: r.requestNo, requestCategory: r.requestCategory, itemCode: r.itemCode, itemName: r.itemName,
          uomCode: r.uomCode, qty: r.openQty, estPrice: r.estPrice, amount: r.amount, neededDate: r.neededDate,
        })
      return next
    })
    const current = (form.getFieldValue('lines') as LineValues[] | undefined) ?? []
    form.setFieldValue('lines', [
      ...current,
      ...rows.map((r) => ({ id: null, requestLineId: r.requestLineId, purchasingUnitId: unitId, note: '' })),
    ])
    setPicking(false)
  }

  const persist = async (): Promise<PurchaseSummary | null> => {
    let values: FormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng (đơn vị mua bắt buộc).')
      return null
    }
    setError(null)
    const lineValues = (form.getFieldValue('lines') as LineValues[] | undefined) ?? []
    const input: SavePurchaseSummaryInput = {
      date: values.date.format('YYYY-MM-DD'),
      form: values.form,
      departmentId: values.departmentId!,
      constructionProjectId: values.form === 'Construction' ? (values.constructionProjectId ?? null) : null,
      note: orNull(values.note),
      lines: lineValues.map((l) => ({ requestLineId: l.requestLineId!, purchasingUnitId: l.purchasingUnitId!, note: orNull(l.note) })),
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
    isNew, doc, readOnly, persist, mutations, listPath: PURCHASE_SUMMARY_PATH, noun: 'bảng tổng hợp đề xuất mua', setError, docType: 'BTH',
  })

  const readonlyCell = (text: string) => <span className="line-grid-readonly">{text}</span>
  const columns: LineColumn[] = [
    {
      key: 'request', title: 'Số đề nghị', width: 150,
      render: (field, index) => {
        const info = infoOf(lines[index]?.requestLineId)
        return (
          <>
            <Form.Item name={[field.name, 'id']} hidden>
              <Input />
            </Form.Item>
            <Form.Item name={[field.name, 'requestLineId']} hidden rules={[{ required: true, message: 'Dòng chưa có dòng đề nghị.' }]}>
              <Input />
            </Form.Item>
            {info ? <DocNo no={info.requestNo} docType="PR" id={info.requestId} /> : null}
          </>
        )
      },
    },
    { key: 'category', title: 'Loại', width: 60, render: (_, i) => readonlyCell(infoOf(lines[i]?.requestLineId) ? categoryLabel(infoOf(lines[i]?.requestLineId)!.requestCategory).split(' ')[0] : '') },
    { key: 'item', title: 'Mặt hàng', width: 240, render: (_, i) => { const x = infoOf(lines[i]?.requestLineId); return readonlyCell(x ? `${x.itemCode} – ${x.itemName}` : '') } },
    { key: 'uom', title: 'ĐVT', width: 60, render: (_, i) => readonlyCell(infoOf(lines[i]?.requestLineId)?.uomCode ?? '') },
    { key: 'qty', title: 'SL đề xuất mua', width: 110, align: 'right', render: (_, i) => { const x = infoOf(lines[i]?.requestLineId); return readonlyCell(x ? formatQuantity(x.qty) : '') } },
    { key: 'price', title: 'Đơn giá tham khảo', width: 120, align: 'right', render: (_, i) => { const x = infoOf(lines[i]?.requestLineId); return readonlyCell(x ? formatMoney(x.estPrice) : '') } },
    { key: 'amount', title: 'Thành tiền', width: 130, align: 'right', render: (_, i) => { const x = infoOf(lines[i]?.requestLineId); return readonlyCell(x ? formatMoney(x.amount) : '') } },
    { key: 'needed', title: 'Ngày cần', width: 95, render: (_, i) => readonlyCell(formatDate(infoOf(lines[i]?.requestLineId)?.neededDate ?? null)) },
    {
      key: 'unit', title: 'Đơn vị mua', required: true, width: 190,
      render: (field) => (
        <Form.Item name={[field.name, 'purchasingUnitId']} noStyle rules={[{ required: true, message: 'Chọn đơn vị mua.' }]}>
          <Select variant="borderless" style={{ width: '100%' }} aria-label={`Đơn vị mua dòng ${field.name + 1}`} options={unitOptions}
            loading={units.isLoading} placeholder="Chọn đơn vị mua" popupMatchSelectWidth={280} />
        </Form.Item>
      ),
    },
    {
      key: 'note', title: 'Ghi chú', width: 160,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle rules={[{ max: 500 }]}>
          <Input variant="borderless" maxLength={500} aria-label={`Ghi chú dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  // Tổng đề xuất cộng theo các dòng đang có trên form (dòng đã lưu dùng thành tiền máy chủ đã chụp) – bớt / thêm dòng thấy ngay.
  const total = lines.reduce((sum, l) => sum + (infoOf(l?.requestLineId)?.amount ?? 0), 0)
  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? withPendingApprovers(purchaseStatusLabels[doc.status], doc.approvals) : '', tone: doc ? purchaseStatusTone[doc.status] : ('neutral' as const) }

  return (
    <>
      <DocumentForm
        approval={doc ? { steps: doc.approvals, createdByName: doc.createdByName, createdAt: doc.createdAt, stageLabels: PURCHASE_STAGE_LABELS } : undefined}
        title="Bảng tổng hợp đề xuất mua"
        number={isNew ? null : (doc?.no ?? '')}
        audit={{ entityType: 'PurchaseSummary', id: doc?.id }}
        status={status}
        meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
        loading={summary.isLoading}
        error={error ?? (summary.error ? summary.error.message : null)}
        notice={
          <>
            {doc?.rejectReason && doc.status === 'Draft' ? (
              <Alert type="warning" showIcon title={`Bị từ chối: ${doc.rejectReason}`} />
            ) : (
              doc?.actions.readOnlyReason && <Alert type="info" showIcon title={doc.actions.readOnlyReason} />
            )}
            {doc?.cancelReason && <Alert type="error" showIcon title={`Đã hủy: ${doc.cancelReason}`} />}
            {doc && <SummaryFrameNotice frame={doc.frame} form={doc.form} />}
            {doc?.status === 'Approved' && (
              <Alert type="success" showIcon title="Bảng đã duyệt – các dòng đề nghị của bảng lên RFQ / PO được (Lấy dòng từ PR đã duyệt)." />
            )}
          </>
        }
        form={form}
        initialValues={{ date: dayjs(), form: 'Goods', lines: [] }}
        readOnly={readOnly}
        onSave={save}
        onAddLine={() => addRef.current?.()}
        actions={actions}
        header={
          <>
            <Form.Item label="Ngày lập" name="date" rules={[{ required: true, message: 'Chọn ngày.' }]}>
              <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Biểu mẫu" name="form" rules={[{ required: true, message: 'Chọn biểu mẫu.' }]}>
              <Select
                options={Object.entries(purchaseSummaryFormLabels).map(([value, label]) => ({ value, label }))}
                onChange={(v: PurchaseSummaryForm) => {
                  if (v !== 'Construction') form.setFieldValue('constructionProjectId', undefined)
                  clearLines()
                }}
              />
            </Form.Item>
            {summaryForm === 'Construction' && (
              <Form.Item label="Công trình" name="constructionProjectId" rules={[{ required: true, message: 'Chọn công trình.' }]}>
                <Select
                  options={projectSelect}
                  loading={projectOptions.isLoading}
                  placeholder="Chọn công trình đang thực hiện"
                  onChange={(v: string) => {
                    // Điền sẵn phòng ban đề xuất của công trình; người lập vẫn đổi được.
                    const project = projectOptions.data?.find((p) => p.id === v)
                    if (project) form.setFieldValue('departmentId', project.departmentId)
                    clearLines()
                  }}
                />
              </Form.Item>
            )}
            <Form.Item label="Bộ phận đề xuất" name="departmentId" rules={[{ required: true, message: 'Chọn bộ phận đề xuất.' }]}>
              <Select options={departmentOptions} loading={departments.isLoading} placeholder="Chọn bộ phận" onChange={clearLines} />
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
                Chọn dòng đề nghị
              </Button>
            )}
            <LineGrid name="lines" auditEntity="PurchaseSummaryLine" columns={columns} readOnly={readOnly} addRef={addRef}
              newLine={() => ({ id: null })} addLines={openPicker} />
          </>
        }
        footer={<DocumentTotal label="Tổng đề xuất" value={formatMoney(total)} strong />}
        extraTabs={[
          ...(doc ? [{ key: 'approvals', label: 'Lịch sử duyệt', children: <ApprovalHistory steps={doc.approvals} stageLabels={PURCHASE_STAGE_LABELS} createdByName={doc.createdByName} createdAt={doc.createdAt} /> }] : []),
          attachmentTab,
        ]}
      />
      {picking && summaryForm && departmentId && (
        <CandidatePicker
          filter={{ form: summaryForm, departmentId, constructionProjectId: summaryForm === 'Construction' ? (projectId ?? null) : null, summaryId: doc?.id ?? null }}
          exclude={new Set(lines.map((l) => l?.requestLineId).filter((x): x is string => !!x))}
          unitOptions={unitOptions}
          onCancel={() => setPicking(false)}
          onPick={addPicked}
        />
      )}
    </>
  )
}

/**
 * Hộp chọn dòng đề nghị đã duyệt còn cần mua, chưa ở bảng tổng hợp nào chưa hủy, đúng biểu mẫu – bộ phận – công trình của đầu phiếu
 * (`GET /api/purchase-summaries/candidates`). Chọn sẵn đơn vị mua cho mọi dòng chọn được (sửa từng dòng sau).
 * @param filter Bộ lọc theo đầu phiếu.
 * @param exclude Dòng đề nghị đã có trên phiếu.
 * @param unitOptions Đơn vị mua chọn được.
 * @param onPick Thêm các dòng đã chọn kèm đơn vị mua chung (có thể trống).
 * @param onCancel Đóng.
 */
function CandidatePicker({
  filter, exclude, unitOptions, onPick, onCancel,
}: {
  filter: { form: PurchaseSummaryForm; departmentId: string; constructionProjectId: string | null; summaryId: string | null }
  exclude: Set<string>
  unitOptions: { value: string; label: string }[]
  onPick: (rows: PurchaseSummaryCandidate[], unitId: string | undefined) => void
  onCancel: () => void
}) {
  const candidates = usePurchaseSummaryCandidates(filter)
  const [selected, setSelected] = useState<string[]>([])
  const [unitId, setUnitId] = useState<string | undefined>()
  const rows = (candidates.data ?? []).filter((r) => !exclude.has(r.requestLineId))
  return (
    <Modal
      open
      title="Chọn dòng đề nghị mua đã duyệt còn cần mua"
      width={1000}
      okText={`Thêm ${selected.length} dòng`}
      okButtonProps={{ disabled: selected.length === 0 }}
      cancelText="Đóng"
      onCancel={onCancel}
      onOk={() => onPick(rows.filter((r) => selected.includes(r.requestLineId)), unitId)}
    >
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 6 }}>
        <label htmlFor="summary-pick-unit">Đơn vị mua cho các dòng chọn</label>
        <Select id="summary-pick-unit" aria-label="Đơn vị mua cho các dòng chọn" style={{ width: 280 }} allowClear options={unitOptions}
          value={unitId} onChange={setUnitId} placeholder="Gán sau trên từng dòng" />
      </div>
      <Table<PurchaseSummaryCandidate>
        size="small"
        rowKey="requestLineId"
        loading={candidates.isLoading}
        pagination={false}
        scroll={{ y: 360 }}
        dataSource={rows}
        rowSelection={{ selectedRowKeys: selected, onChange: (keys) => setSelected(keys as string[]) }}
        locale={{ emptyText: 'Không còn dòng đề nghị đã duyệt nào cần mua của bộ phận (và công trình) này mà chưa vào bảng tổng hợp.' }}
        columns={[
          { title: 'Số đề nghị', dataIndex: 'requestNo', width: 150 },
          { title: 'Ngày', dataIndex: 'requestDate', width: 90, render: (v: string) => formatDate(v) },
          { title: 'Loại', dataIndex: 'requestCategory', width: 60, render: (v: string) => categoryLabel(v).split(' ')[0] },
          { title: 'Mặt hàng', key: 'item', render: (_, r) => `${r.itemCode} – ${r.itemName}` },
          { title: 'ĐVT', dataIndex: 'uomCode', width: 60 },
          { title: 'Còn cần mua', dataIndex: 'openQty', width: 100, align: 'right', render: (v: number) => formatQuantity(v) },
          { title: 'Thành tiền', dataIndex: 'amount', width: 120, align: 'right', render: (v: number) => formatMoney(v) },
          { title: 'Ngày cần', dataIndex: 'neededDate', width: 90, render: (v: string | null) => formatDate(v) },
        ]}
      />
    </Modal>
  )
}
