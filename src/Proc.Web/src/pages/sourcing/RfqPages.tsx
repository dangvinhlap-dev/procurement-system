import { useDocumentFormSync } from '../../components/DocumentForm/useDocumentFormSync'
import {
  CalendarOutlined,
  CheckOutlined,
  CloseCircleOutlined,
  CloseOutlined,
  DollarOutlined,
  EditOutlined,
  FileAddOutlined,
  FileProtectOutlined,
  RollbackOutlined,
  SaveOutlined,
  SendOutlined,
  ShoppingCartOutlined,
  StopOutlined,
} from '@ant-design/icons'
import { Alert, App, Button, Checkbox, ConfigProvider, DatePicker, Flex, Form, Input, InputNumber, Modal, Space, Table, Tag } from 'antd'
import { Select } from '../../classic/Select'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router'
import { useItemLookup } from '../../api/inventoryDocuments'
import { supplierItemKey, useSupplierItemLookup, useSuppliers, vatOptions } from '../../api/masterData'
import { withPendingApprovers, type OpenRequestLine } from '../../api/purchasing'
import {
  rfqStatusLabels,
  rfqStatusTone,
  useRfq,
  useRfqMutations,
  useRfqs,
  useSourcing,
  type Rfq,
  type RfqDossierItem,
  type RfqListItem,
  type RfqSupplier,
  type SaveQuotationInput,
  type SaveRfqInput,
} from '../../api/sourcing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { useAppraisalTab } from '../../components/AppraisalPanel/useAppraisalTab'
import { AppraisalSummary } from '../../components/AppraisalPanel/AppraisalPanel'
import { useAppraisals } from '../../api/appraisals'
import { DocumentForm } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { ListPage } from '../../components/ListPage/ListPage'
import { formatDate, formatDateTime, formatMoney, formatQuantity, vnNumberInput, DATE_INPUT_FORMATS } from '../../utils/format'
import { ApprovalHistory } from '../purchasing/ApprovalHistory'
import { RequestLinePicker } from '../purchasing/PurchaseOrderPage'
import { PlannedQuantityDialog } from '../purchasing/PlannedQuantityDialog'
import { toPlannedLines } from '../purchasing/plannedLines'
import { approveContent, confirmReject, confirmSubmitWithPreview, confirmWithReason } from '../purchasing/useApprovalCommands'
import { confirmApprove } from '../purchasing/approvalDialogs'
import { renderRequiredMark } from '../../components/RequiredMark/requiredMark'
import { DocNo } from '../../classic/DocNo'

const LIST_PATH = '/dau-thau/rfq'

const listColumns: GridColumn<RfqListItem>[] = [
  { key: 'no', title: 'Số RFQ', width: 150, value: (r) => r.no, docRef: (r) => ({ docType: 'RFQ', id: r.id }) },
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
  const rfqs = useRfqs(compare ? 'Sent,AwardPending,Awarded' : undefined)
  return (
    <ListPage
      title={compare ? 'So sánh báo giá' : 'Yêu cầu báo giá (RFQ)'}
      exportFileName={compare ? 'so-sanh-bao-gia' : 'yeu-cau-bao-gia'}
      rows={rfqs.data ?? []}
      columns={listColumns}
      rowKey={(r) => r.id}
      auditEntity="Rfq"
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

/** Chuỗi rỗng hoặc toàn khoảng trắng gửi lên API là null; còn lại bỏ khoảng trắng hai đầu. */
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
  const { hasPermission, workingCompany } = useAuth()
  const [form] = Form.useForm<FormValues>()
  const rfq = useRfq(isNew ? null : id)
  const sourcingId = isNew ? params.get('sourcingId') : null
  const source = useSourcing(sourcingId)
  const mutations = useRfqMutations()
  const suppliers = useSuppliers({})
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [picking, setPicking] = useState(false)
  const [planning, setPlanning] = useState(false)
  const [quoting, setQuoting] = useState<RfqSupplier | null>(null)
  const [awardEdits, setAwardEdits] = useState<AwardDraft>({})
  const addRef = useRef<(() => void) | null>(null)
  const doc = rfq.data
  const readOnly = isNew ? !hasPermission(Permission.RfqManage) : !doc?.actions.canEdit
  // Đính kèm được cả khi phiếu đã duyệt/ghi sổ (scan hóa đơn, biên bản) – quyền theo quyền lập phiếu, design.md mục 18.4.
  const attachmentTab = useAttachmentTab('Rfq', doc?.id, hasPermission(Permission.RfqManage))
  // Ý kiến thẩm định hồ sơ thầu (lát H1) – Hội đồng xem trước khi duyệt kết quả chọn thầu.
  const appraisalTabs = useAppraisalTab('Rfq', doc?.id)
  const appraisals = useAppraisals('Rfq', doc?.id).data

  const watchedLines = Form.useWatch('lines', form) as LineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const rfqDate = Form.useWatch('date', form) as Dayjs | undefined
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])

  const { loadSaved, targetId } = useDocumentFormSync(form, id, doc, (doc) => ({
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
  }))

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
      const target = targetId()
      const saved = target ? await mutations.update.mutateAsync({ id: target, input }) : await mutations.create.mutateAsync(input)
      loadSaved(saved)
      return saved
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

  const send = () => {
    // Lát B3 (design.md mục 8.6): mời ít hơn số NCC tối thiểu vẫn gửi được khi có giải trình – server kiểm tra lại.
    const invited = ((form.getFieldValue('supplierIds') as string[] | undefined) ?? []).length
    const short = doc ? invited < doc.minSuppliers : false
    let reason = ''
    modal.confirm({
      title: 'Gửi RFQ cho các NCC?',
      content: (
        <>
          <p>RFQ chuyển sang Đang nhận báo giá; dòng hàng, NCC mời, trọng số không sửa được nữa.</p>
          <label htmlFor="few-suppliers-reason">
            {renderRequiredMark(
              doc ? `Giải trình mời ít hơn ${doc.minSuppliers} NCC (đang mời ${invited})` : 'Giải trình nếu mời ít hơn số NCC tối thiểu',
              { required: short },
            )}
          </label>
          <Input.TextArea
            id="few-suppliers-reason"
            rows={3}
            maxLength={500}
            placeholder="Ví dụ: hàng độc quyền, chỉ có 2 đại lý chính hãng"
            onChange={(e) => (reason = e.target.value)}
          />
        </>
      ),
      okText: 'Gửi NCC',
      cancelText: 'Không',
      onOk: async () => {
        if (short && !reason.trim()) {
          message.error('Mời ít hơn số NCC tối thiểu thì phải ghi giải trình.')
          throw new Error('missing reason')
        }
        const saved = await persist()
        if (!saved) return
        try {
          await mutations.send.mutateAsync({ id: saved.id, reason: orNull(reason) })
          message.success(`Đã gửi RFQ ${saved.no}.`)
        } catch (e) {
          fail(e, 'Không gửi được RFQ.')
        }
        if (isNew) navigate(`${LIST_PATH}/${saved.id}?tab=compare`, { replace: true })
      },
    })
  }

  /**
   * Gia hạn nhận báo giá (lát R7, design.md mục 18.26): quá hạn nộp thì không nhập, sửa báo giá được – gia hạn có lý do (nhật ký ghi hạn
   * cũ → mới). Hạn mới phải sau hạn hiện tại và không trước hôm nay.
   */
  const extendDeadline = () => {
    if (!doc) return
    let deadline: Dayjs | null = dayjs(doc.deadline).add(7, 'day')
    let reason = ''
    modal.confirm({
      title: `Gia hạn nhận báo giá RFQ ${doc.no}?`,
      content: (
        <Flex vertical gap={8}>
          <span>Hạn hiện tại: {formatDate(doc.deadline)}</span>
          <label htmlFor="rfq-new-deadline">{renderRequiredMark('Hạn mới', { required: true })}</label>
          <DatePicker id="rfq-new-deadline" format={DATE_INPUT_FORMATS} defaultValue={deadline} onChange={(v) => (deadline = v)} />
          <label htmlFor="rfq-extend-reason">{renderRequiredMark('Lý do gia hạn', { required: true })}</label>
          <Input.TextArea id="rfq-extend-reason" rows={3} maxLength={500} placeholder="Ví dụ: NCC xin thêm thời gian chuẩn bị hồ sơ"
            onChange={(e) => (reason = e.target.value)} />
        </Flex>
      ),
      okText: 'Gia hạn',
      cancelText: 'Không',
      onOk: async () => {
        if (!deadline || !reason.trim()) {
          message.error('Hãy chọn hạn mới và nhập lý do gia hạn.')
          throw new Error('missing input')
        }
        try {
          await mutations.extendDeadline.mutateAsync({ id: doc.id, deadline: deadline.format('YYYY-MM-DD'), reason: reason.trim() })
          message.success(`Đã gia hạn nhận báo giá RFQ ${doc.no} đến ${deadline.format('DD/MM/YYYY')}.`)
        } catch (e) {
          fail(e, 'Không gia hạn được.')
        }
      },
    })
  }

  // Xem trước hội đồng duyệt theo giá trị trúng thầu của lựa chọn đang chọn (server chạy thử rồi hủy) – yêu cầu người dùng 03/11/2026.
  const confirmAward = () => {
    if (!doc) return
    setError(null)
    const quoted = doc.quotedSupplierCount ?? doc.suppliers.length
    // Ít NCC chào giá hơn tối thiểu (lát R7): hỏi giải trình trước, trừ khi đã giải trình mời thiếu NCC và mọi NCC mời đều chào giá.
    const needsReason = quoted < doc.minSuppliers && !(doc.fewSuppliersReason && quoted >= doc.suppliers.length)
    if (needsReason) {
      confirmWithReason(modal, message, {
        title: `Chỉ có ${quoted} NCC chào giá (tối thiểu ${doc.minSuppliers})`,
        description: 'Ít hơn số báo giá tối thiểu phải có giải trình – hội đồng duyệt thấy giải trình này.',
        label: 'Giải trình ít báo giá',
        placeholder: 'Ví dụ: hai NCC được mời không phản hồi trong hạn',
        okText: 'Tiếp tục',
        danger: false,
        onOk: async (reason) => startAward(reason),
      })
      return
    }
    void startAward(null)
  }

  /** Xem trước chuỗi duyệt rồi chọn thầu (kèm giải trình ít báo giá nếu có). */
  const startAward = async (fewQuotesReason: string | null) => {
    if (!doc) return
    const lines = doc.lines.map((l) => ({ rfqLineId: l.id, supplierId: award[l.id]?.supplierId ?? null, reason: orNull(award[l.id]?.reason) }))
    await confirmSubmitWithPreview(modal, {
      title: `Chọn thầu và gửi duyệt kết quả RFQ ${doc.no}? Trong lúc chờ không nhập thêm báo giá; duyệt xong mới tạo được đơn mua hàng.`,
      docType: 'RFQ',
      okText: 'Chọn thầu, gửi duyệt',
      prepare: async () => ({ id: doc.id, award: { lines, fewQuotesReason } }),
      onSubmit: async (id) => {
        try {
          const result = await mutations.award.mutateAsync({ id, lines, fewQuotesReason })
          setAwardEdits({})
          message.success(result.status === 'Awarded' ? `Kết quả RFQ ${doc.no} đã được duyệt ngay.` : `Đã gửi duyệt kết quả chọn thầu RFQ ${doc.no}.`)
        } catch (e) {
          fail(e, 'Không chọn thầu được.')
        }
      },
      onError: (e) => fail(e, 'Không chọn thầu được.'),
    })
  }

  const approve = () =>
    doc &&
    confirmApprove(modal, message, {
      attach: { ownerType: 'Rfq', ownerId: doc.id },
      title: `Duyệt kết quả chọn thầu RFQ ${doc.no}?`,
      content: (
        <>
          <p>Giá trị trúng thầu {formatMoney(doc.awardAmount)} đ. Duyệt cấp cuối thì giá trúng thầu vào lịch sử giá, tạo được đơn mua hàng.</p>
          <p>{dossierSummary(doc.dossier)}</p>
          <AppraisalSummary data={appraisals} />
          {approveContent(doc)}
        </>
      ),
      onOk: async (comment) => {
        setError(null)
        try {
          const result = await mutations.approve.mutateAsync({ id: doc.id, comment })
          message.success(result.status === 'Awarded' ? `Đã duyệt xong kết quả chọn thầu RFQ ${doc.no}.` : `Đã duyệt cấp của bạn – RFQ ${doc.no} chuyển cấp tiếp theo.`)
        } catch (e) {
          fail(e, 'Không duyệt được.')
          return false
        }
      },
    })

  const reject = () =>
    doc &&
    confirmReject(modal, message, `Từ chối kết quả chọn thầu RFQ ${doc.no}?`, async (reason) => {
      setError(null)
      try {
        await mutations.reject.mutateAsync({ id: doc.id, comment: reason })
        message.success(`Đã từ chối kết quả chọn thầu RFQ ${doc.no}.`)
      } catch (e) {
        fail(e, 'Không từ chối được.')
        return false
      }
    }, { ownerType: 'Rfq', ownerId: doc.id })

  const recall = () =>
    doc &&
    modal.confirm({
      title: `Rút kết quả chọn thầu RFQ ${doc.no} về?`,
      content: 'Các cấp đang chờ duyệt bỏ qua; RFQ về Đang nhận báo giá để bổ sung báo giá, chọn lại và gửi duyệt lại.',
      okText: 'Rút về',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        try {
          await mutations.recall.mutateAsync(doc.id)
          message.success(`Đã rút kết quả chọn thầu RFQ ${doc.no} về.`)
        } catch (e) {
          fail(e, 'Không rút về được.')
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

  const createPriceLists = () =>
    doc &&
    modal.confirm({
      title: 'Tạo bảng giá NCC từ kết quả chọn thầu?',
      content:
        'Mỗi NCC trúng thầu một bảng giá nháp của công ty: giá, % CK, VAT theo báo giá, hiệu lực từ hôm nay tới hết hiệu lực báo giá. ' +
        'RFQ của tổng công ty thì bảng giá đánh dấu sẵn "Áp dụng toàn tập đoàn" (bỏ được khi còn nháp). Mở bảng giá để kiểm tra rồi gửi duyệt.',
      okText: 'Tạo bảng giá',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        try {
          const created = await mutations.createPriceLists.mutateAsync(doc.id)
          message.success(`Đã tạo ${created.map((c) => `${c.priceListNo} (${c.supplierCode})`).join(', ')}.`)
        } catch (e) {
          fail(e, 'Không tạo được bảng giá.')
        }
      },
    })

  const createContracts = () =>
    doc &&
    modal.confirm({
      title: 'Tạo hợp đồng nguyên tắc từ kết quả chọn thầu?',
      content:
        'Mỗi NCC trúng thầu một hợp đồng nguyên tắc nháp của công ty, gắn bảng giá đã tạo từ RFQ này: sản lượng dự kiến = SL trúng thầu, ' +
        'hiệu lực theo bảng giá, thanh toán theo báo giá. Mở hợp đồng bổ sung số HĐ giấy, ngày ký, điều khoản rồi gửi duyệt.',
      okText: 'Tạo hợp đồng',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        try {
          const created = await mutations.createContracts.mutateAsync(doc.id)
          message.success(`Đã tạo ${created.map((c) => `${c.contractNo} (${c.supplierCode})`).join(', ')}.`)
        } catch (e) {
          fail(e, 'Không tạo được hợp đồng.')
        }
      },
    })

  // Hủy bắt buộc lý do (design.md mục 20.2a): RFQ nháp – người lập; đã gửi NCC – chỉ quản trị.
  const cancel = () =>
    doc &&
    confirmWithReason(modal, message, {
      title: `Hủy RFQ ${doc.no}?`,
      label: 'Lý do hủy',
      placeholder: 'Ví dụ: đổi phương án mua, gộp vào RFQ khác',
      okText: 'Hủy RFQ',
      onOk: async (reason) => {
        try {
          await mutations.cancel.mutateAsync({ id: doc.id, reason })
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
      title: 'Mặt hàng', required: true,
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
      title: 'ĐVT', required: true,
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
      key: 'pr',
      title: 'PR nguồn',
      width: 140,
      render: (_, index) => {
        const saved = savedLine(index)
        return saved?.sourcePrNo
          ? <DocNo no={saved.sourcePrNo} docType="PR" id={saved.sourcePrId} />
          : <span className="line-grid-readonly">{lines[index]?.sourcePrLineId ? '(từ PR)' : ''}</span>
      },
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
    : { label: doc ? withPendingApprovers(rfqStatusLabels[doc.status], doc.approvals) : '', tone: doc ? rfqStatusTone[doc.status] : ('neutral' as const) }

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
      {doc?.actions.canExtendDeadline && (
        <Button icon={<CalendarOutlined />} onClick={extendDeadline} loading={mutations.extendDeadline.isPending}>
          Gia hạn nhận báo giá
        </Button>
      )}
      {doc?.actions.canAward && (
        <Button type="primary" icon={<CheckOutlined />} onClick={confirmAward} loading={mutations.award.isPending}>
          Chọn thầu, gửi duyệt
        </Button>
      )}
      {doc?.actions.canApprove && (
        <>
          <Button type="primary" icon={<CheckOutlined />} onClick={approve} loading={mutations.approve.isPending}>
            Duyệt
          </Button>
          <Button danger icon={<CloseCircleOutlined />} onClick={reject} loading={mutations.reject.isPending}>
            Từ chối
          </Button>
        </>
      )}
      {doc?.actions.canRecall && (
        <Button icon={<RollbackOutlined />} onClick={recall} loading={mutations.recall.isPending}>
          Rút về
        </Button>
      )}
      {doc?.actions.canCreateOrders && (
        <Button type="primary" icon={<ShoppingCartOutlined />} onClick={createOrders} loading={mutations.createOrders.isPending}>
          Tạo PO
        </Button>
      )}
      {doc?.actions.canCreatePriceLists && (
        <Button icon={<DollarOutlined />} onClick={createPriceLists} loading={mutations.createPriceLists.isPending}>
          Tạo bảng giá
        </Button>
      )}
      {doc?.actions.canCreateContracts && (
        <Button icon={<FileProtectOutlined />} onClick={createContracts} loading={mutations.createContracts.isPending}>
          Tạo hợp đồng
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
          label={`Nhà cung cấp được mời (${doc ? `tối thiểu ${doc.minSuppliers} NCC` : 'số NCC tối thiểu theo tham số công ty'}; mời ít hơn thì phải giải trình khi gửi)`}
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
    ...(doc
      ? [
          {
            key: 'dossier',
            label: `Hồ sơ trình (${doc.dossier.filter((d) => d.isDone).length}/${doc.dossier.length})`,
            children: <DossierView rfq={doc} />,
          },
        ]
      : []),
    ...(doc && doc.approvals.length > 0 ? [{ key: 'approvals', label: 'Lịch sử duyệt', children: <ApprovalHistory steps={doc.approvals} createdByName={doc.createdByName} createdAt={doc.createdAt} /> }] : []),
  ]

  return (
    <>
      <DocumentForm
        approval={doc ? { steps: doc.approvals, createdByName: doc.createdByName, createdAt: doc.createdAt } : undefined}
        title="Yêu cầu báo giá (RFQ)"
        number={isNew ? null : (doc?.no ?? '')}
        audit={{ entityType: 'Rfq', id: doc?.id }}
        status={status}
        meta={
          doc && (
            <>
              {`Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
              {doc.sourcingNo && <> · Từ phiếu tìm nguồn <DocNo no={doc.sourcingNo} docType="SRC" id={doc.sourcingId} /></>}
              {doc.awardedByName && doc.status !== 'Sent' ? ` · Người chọn thầu: ${doc.awardedByName}` : ''}
            </>
          )
        }
        loading={rfq.isLoading}
        error={error ?? (rfq.error ? rfq.error.message : null)}
        notice={doc && <RfqNotices doc={doc} />}
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
              <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Tiêu đề" name="title" rules={[{ required: true, message: 'Nhập tiêu đề.' }, { max: 200 }]}>
              <Input />
            </Form.Item>
            <Form.Item label="Hạn nộp báo giá" name="deadline" rules={[{ required: true, message: 'Chọn hạn nộp.' }]}>
              <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
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
                {hasPermission(Permission.DemandPlanView) && (
                  <Button icon={<CalendarOutlined />} onClick={() => setPlanning(true)} disabled={toPlannedLines(lines, (l) => l.qty).length === 0}>
                    Lấy SL kế hoạch
                  </Button>
                )}
              </Space>
            )}
            <LineGrid name="lines" auditEntity="RfqLine" columns={columns} readOnly={readOnly} addRef={addRef} newLine={() => ({ id: null, qty: 1, sourcePrLineId: null })} />
          </>
        }
        extraTabs={[...extraTabs, ...appraisalTabs, attachmentTab]}
      />
      {picking && <RequestLinePicker onCancel={() => setPicking(false)} onPick={addFromRequests} />}
      {planning && (
        // RFQ thường lập ở tổng công ty cho nhu cầu gộp (Văn phòng Ban) nên ở tổng công ty mặc định toàn tập đoàn; khoảng mặc định trọn 12
        // tháng kể từ tháng sau ngày lập – kỳ mà kết quả thầu sẽ áp dụng. RFQ nháp chưa có giá nên đổi dòng sang ĐVT cơ sở được.
        <PlannedQuantityDialog
          title="Lấy SL kế hoạch làm SL mời thầu"
          lines={toPlannedLines(lines, (l) => l.qty)}
          defaultFrom={(rfqDate ?? dayjs()).add(1, 'month').startOf('month')}
          defaultTo={(rfqDate ?? dayjs()).add(12, 'month').endOf('month').startOf('day')}
          defaultAllCompanies={workingCompany?.isHeadOffice === true}
          allowBaseUom
          onApply={(changes) =>
            changes.forEach((c, index) => {
              form.setFieldValue(['lines', index, 'qty'], c.qty)
              if (c.uomId) form.setFieldValue(['lines', index, 'uomId'], c.uomId)
            })
          }
          onClose={() => setPlanning(false)}
        />
      )}
      {quoting && doc && <QuotationDialog rfq={doc} supplier={quoting} onClose={() => setQuoting(null)} />}
    </>
  )
}

/**
 * Câu tóm tắt hồ sơ trình cho hội đồng: đủ bao nhiêu mục, thiếu mục nào (thiếu không chặn gửi duyệt – lát B3b).
 * @param dossier Các mục hồ sơ trình.
 */
function dossierSummary(dossier: RfqDossierItem[]) {
  const missing = dossier.filter((d) => !d.isDone)
  return missing.length === 0
    ? `Hồ sơ trình đủ ${dossier.length}/${dossier.length} mục.`
    : `Hồ sơ trình đủ ${dossier.length - missing.length}/${dossier.length} mục – còn thiếu: ${missing.map((d) => d.name.toLowerCase()).join(', ')}.`
}

/**
 * Tab hồ sơ trình (design.md mục 8.6): đánh dấu mục đã có + ghi chú khi RFQ còn Nháp/Đang nhận báo giá; file đính kèm ở tab Đính kèm.
 * Bảng so sánh báo giá do hệ thống tự tính. Sau khi gửi duyệt chỉ xem.
 * @param rfq RFQ đã tải.
 */
function DossierView({ rfq }: { rfq: Rfq }) {
  const { message } = App.useApp()
  const mutations = useRfqMutations()
  const [edits, setEdits] = useState<Record<string, { isDone: boolean; note: string }>>({})
  const editable = rfq.actions.canEditDossier
  const value = (d: RfqDossierItem) => edits[d.code] ?? { isDone: d.isDone, note: d.note ?? '' }
  const set = (d: RfqDossierItem, patch: Partial<{ isDone: boolean; note: string }>) => setEdits({ ...edits, [d.code]: { ...value(d), ...patch } })

  const save = async () => {
    try {
      await mutations.saveDossier.mutateAsync({
        id: rfq.id,
        items: Object.entries(edits).map(([code, v]) => ({ code, isDone: v.isDone, note: orNull(v.note) })),
      })
      setEdits({})
      message.success('Đã lưu hồ sơ trình.')
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Không lưu được hồ sơ trình.')
    }
  }

  return (
    // Tab vẫn thao tác được dù đầu phiếu đã khóa sau khi gửi NCC – quyền sửa theo canEditDossier.
    <ConfigProvider componentDisabled={!editable}>
      <Flex vertical gap={6}>
        <Table<RfqDossierItem>
          aria-label="Hồ sơ trình"
          size="small"
          bordered
          pagination={false}
          rowKey="code"
          dataSource={rfq.dossier}
          columns={[
            { title: 'Mục hồ sơ', key: 'name', width: 300, render: (_, d) => d.name },
            {
              title: 'Đã có',
              key: 'done',
              width: 80,
              align: 'center',
              render: (_, d) => (
                <Checkbox
                  aria-label={`Đã có ${d.name}`}
                  checked={value(d).isDone}
                  disabled={d.isAutomatic || !editable}
                  onChange={(e) => set(d, { isDone: e.target.checked })}
                />
              ),
            },
            {
              title: 'Ghi chú (số biên bản, nơi lưu…)',
              key: 'note',
              render: (_, d) =>
                d.isAutomatic ? (
                  'Hệ thống lập từ các báo giá đã nhập (tab So sánh & chọn thầu)'
                ) : editable ? (
                  <Input aria-label={`Ghi chú ${d.name}`} maxLength={500} value={value(d).note} onChange={(e) => set(d, { note: e.target.value })} />
                ) : (
                  d.note
                ),
            },
          ]}
        />
        {editable && (
          <Space>
            <Button icon={<SaveOutlined />} onClick={save} disabled={Object.keys(edits).length === 0} loading={mutations.saveDossier.isPending}>
              Lưu hồ sơ trình
            </Button>
            <span>File biên bản, tờ trình… đính kèm ở tab Đính kèm.</span>
          </Space>
        )}
      </Flex>
    </ConfigProvider>
  )
}

/**
 * Thông báo đầu RFQ: chờ hội đồng duyệt kết quả, lý do bị từ chối (đang nhận báo giá lại), giải trình mời thiếu NCC (lát B3), lý do chỉ xem.
 * @param doc RFQ đã tải.
 */
function RfqNotices({ doc }: { doc: Rfq }) {
  const notices = [
    doc.status === 'AwardPending' && (
      <Alert
        key="pending"
        type="info"
        showIcon
        title={`Kết quả chọn thầu đang chờ hội đồng duyệt – giá trị trúng thầu ${formatMoney(doc.awardAmount)} đ. ${dossierSummary(doc.dossier)}`}
      />
    ),
    doc.rejectReason && doc.status === 'Sent' && <Alert key="reject" type="warning" showIcon title={`Kết quả chọn thầu bị từ chối: ${doc.rejectReason}`} />,
    doc.fewSuppliersReason && <Alert key="few" type="warning" showIcon title={`Mời ít hơn ${doc.minSuppliers} NCC – giải trình: ${doc.fewSuppliersReason}`} />,
    doc.fewQuotesReason && <Alert key="fewQuotes" type="warning" showIcon title={`Ít hơn ${doc.minSuppliers} báo giá – giải trình: ${doc.fewQuotesReason}`} />,
    doc.deadlineExtensionReason && (
      <Alert key="extend" type="info" showIcon title={`Đã gia hạn nhận báo giá đến ${formatDate(doc.deadline)} – lý do: ${doc.deadlineExtensionReason}`} />
    ),
    doc.actions.readOnlyReason && <Alert key="readonly" type="info" showIcon title={doc.actions.readOnlyReason} />,
    doc.actions.conflictReason && <Alert key="conflict" type="warning" showIcon title={doc.actions.conflictReason} />,
  ].filter(Boolean)
  return notices.length ? <Flex vertical gap={8}>{notices}</Flex> : null
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
    // Phiếu tạo ra từ kết quả chọn thầu: ô số chứng từ – bấm sao chép, bấm đúp mở (design.md mục 20.1 R5).
    { key: 'po', label: 'Đơn mua hàng', value: (s) => <DocNo no={s.purchaseOrderNo} docType="PO" id={s.purchaseOrderId} /> },
    { key: 'priceList', label: 'Bảng giá NCC', value: (s) => <DocNo no={s.priceListNo} docType="PRL" id={s.priceListId} /> },
    { key: 'contract', label: 'Hợp đồng nguyên tắc', value: (s) => <DocNo no={s.contractNo} docType="CTR" id={s.contractId} /> },
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
  // Mã hàng theo NCC để đối chiếu với báo giá giấy của NCC (design.md mục 5.9).
  const supplierItems = useSupplierItemLookup([supplier.supplierId])

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
            <DatePicker format={DATE_INPUT_FORMATS} />
          </Form.Item>
          <Form.Item label="Hiệu lực đến" name="validUntil">
            <DatePicker format={DATE_INPUT_FORMATS} />
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
            {
              title: 'Mã hàng NCC',
              key: 'supplierItemCode',
              width: 100,
              render: (_, l) => supplierItems.data?.get(supplierItemKey(supplier.supplierId, l.itemId))?.supplierItemCode ?? '',
            },
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
