import { useDocumentFormSync } from '../../components/DocumentForm/useDocumentFormSync'
import {
  CheckOutlined, CloseCircleOutlined, CloseOutlined, CopyOutlined, EditOutlined, RollbackOutlined, SaveOutlined, SendOutlined, StopOutlined,
  WarningFilled,
} from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Flex, Form, Input, InputNumber } from 'antd'
import { Select } from '../../classic/Select'
import dayjs, { type Dayjs } from 'dayjs'
import { useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { useItemLookup } from '../../api/inventoryDocuments'
import { useMarketPrices } from '../../api/marketSurveys'
import { useItemCategories, useSuppliers } from '../../api/masterData'
import {
  MONTHLY_STAGE_LABELS,
  monthlyApprovalStatusLabels,
  monthlyApprovalStatusTone,
  useMonthlyApproval,
  useMonthlyApprovalCompanies,
  useMonthlyApprovalMutations,
  useMonthlyApprovals,
  type MonthlyApproval,
  type MonthlyApprovalListItem,
  type SaveMonthlyApprovalInput,
} from '../../api/monthlyApprovals'
import { withPendingApprovers } from '../../api/purchasing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { useAppraisalTab } from '../../components/AppraisalPanel/useAppraisalTab'
import { AppraisalSummary } from '../../components/AppraisalPanel/AppraisalPanel'
import { useAppraisals } from '../../api/appraisals'
import type { GridColumn } from '../../components/DataGrid/types'
import { DocumentForm } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { ListPage } from '../../components/ListPage/ListPage'
import { MONTH_INPUT_FORMATS, formatDate, formatDateTime, formatMoney, vnNumberInput } from '../../utils/format'
import { ApprovalHistory } from '../purchasing/ApprovalHistory'
import { approveContent, confirmReject, confirmSubmitWithPreview } from '../purchasing/useApprovalCommands'

/** Đường dẫn danh sách phiếu duyệt NCC và giá tháng (màn hình GTH). */
export const MONTHLY_APPROVAL_PATH = '/gia-ncc/duyet-thang'

/** Ô tháng gõ tay MM/yyyy (gõ nhanh 122026, 12.2026) hoặc chọn trên lịch tháng. */
const MONTH_FORMATS = MONTH_INPUT_FORMATS

/** Tháng hiển thị MM/yyyy từ ngày đầu tháng "yyyy-MM-dd". */
const formatMonth = (value: string) => dayjs(value).format('MM/YYYY')

const listColumns: GridColumn<MonthlyApprovalListItem>[] = [
  { key: 'no', title: 'Số phiếu', width: 150, value: (r) => r.no },
  { key: 'period', title: 'Tháng', width: 80, value: (r) => formatMonth(r.period), groupable: true },
  { key: 'category', title: 'Nhóm hàng', width: 220, value: (r) => `${r.categoryCode} – ${r.categoryName}`, groupable: true },
  { key: 'companies', title: 'Công ty áp dụng', width: 160, value: (r) => r.companyCodes },
  { key: 'lines', title: 'Số mặt hàng', width: 90, type: 'quantity', value: (r) => r.lineCount },
  {
    key: 'status', title: 'Trạng thái', width: 170, type: 'status', groupable: true,
    value: (r) => monthlyApprovalStatusLabels[r.status], tone: (r) => monthlyApprovalStatusTone[r.status],
  },
  // Lượt gửi gần nhất có ngoại lệ – qua chặng Chủ tịch/TGĐ (lát H2).
  { key: 'exception', title: 'Ngoại lệ', width: 80, value: (r) => (r.hasException ? 'Có' : ''), groupable: true },
  { key: 'replaces', title: 'Điều chỉnh phiếu', width: 150, value: (r) => r.replacesNo },
  { key: 'creator', title: 'Người lập', width: 150, value: (r) => r.createdByName, groupable: true },
]

/**
 * Màn hình Giá và NCC → Duyệt NCC và giá tháng (GTH – design.md mục 18.25, lát G2): Văn phòng Ban lập ở tổng công ty, Ban cung ứng duyệt;
 * công ty áp dụng thấy phiếu đã duyệt (chỉ xem).
 */
export function MonthlyApprovalListPage() {
  const navigate = useNavigate()
  const { hasPermission, workingCompany } = useAuth()
  const list = useMonthlyApprovals()
  return (
    <ListPage
      title="Duyệt NCC và giá tháng"
      exportFileName="duyet-ncc-gia-thang"
      rows={list.data ?? []}
      columns={listColumns}
      rowKey={(r) => r.id}
      auditEntity="MonthlySupplierApproval"
      loading={list.isLoading}
      // Chỉ tổng công ty lập phiếu (quyết định Ban cung ứng); công ty thành viên chỉ xem kết quả tháng.
      canCreate={hasPermission(Permission.MonthlyApprovalManage) && !!workingCompany?.isHeadOffice}
      onCreate={() => navigate(`${MONTHLY_APPROVAL_PATH}/moi`)}
      onOpenRow={(r) => navigate(`${MONTHLY_APPROVAL_PATH}/${r.id}`)}
      isMarked={(r) => r.status === 'Submitted'}
    />
  )
}

interface LineValues {
  id: string | null
  itemId?: string
  uomId?: string
  mainSupplierId?: string
  unitPrice?: number | null
  backupSupplierId?: string | null
  backupUnitPrice?: number | null
  quantityLimit?: number | null
  note?: string
  standardPrice?: number | null
  ceilingPrice?: number | null
  targetPrice?: number | null
}

interface FormValues {
  period: Dayjs
  itemCategoryId?: string
  companyIds: string[]
  note?: string
  exceptionNote?: string
  lines: LineValues[]
}

/** Chuỗi rỗng hoặc toàn khoảng trắng gửi lên API là null; còn lại bỏ khoảng trắng hai đầu. */
const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Thông báo đầu phiếu: bị từ chối, đã hủy, đã thay bằng điều chỉnh, phiếu điều chỉnh của phiếu nào, lý do chỉ xem.
 * @param doc Phiếu đã tải.
 * @param onOpen Mở phiếu liên quan.
 */
function MonthlyNotices({ doc, onOpen }: { doc: MonthlyApproval; onOpen: (id: string) => void }) {
  const link = (id: string, no: string) => <Button type="link" size="small" onClick={() => onOpen(id)}>{no}</Button>
  const notices = [
    doc.rejectReason && doc.status === 'Draft' && <Alert key="reject" type="warning" showIcon title={`Bị từ chối: ${doc.rejectReason}`} />,
    doc.status === 'Cancelled' && <Alert key="cancel" type="error" showIcon title={`Đã hủy: ${doc.cancelReason ?? ''}`} />,
    doc.replacesId && doc.replacesNo && (
      <Alert key="replaces" type="info" showIcon title={<>Phiếu điều chỉnh của {link(doc.replacesId, doc.replacesNo)} – duyệt xong thì thay phiếu đó.</>} />
    ),
    doc.replacedById && doc.replacedByNo && (
      <Alert key="replaced" type="info" showIcon
        title={<>{doc.status === 'Replaced' ? 'Đã thay bằng phiếu điều chỉnh' : 'Đang có phiếu điều chỉnh'} {link(doc.replacedById, doc.replacedByNo)}.</>} />
    ),
    doc.actions.readOnlyReason && <Alert key="readonly" type="info" showIcon title={doc.actions.readOnlyReason} />,
    // Q6 (lát H2): phiếu có ngoại lệ qua thêm chặng Chủ tịch/TGĐ sau Hội đồng; còn nháp thì nhắc nhập giải trình.
    exceptionCount(doc) > 0 && (
      <Alert key="exception" type="warning" showIcon
        title={`Có ${exceptionCount(doc)} ngoại lệ ở cột Ngoại lệ (chưa đủ 03 NCC đủ điều kiện, độc quyền, giá vượt giá mục tiêu): phiếu phải có `
          + 'giải trình ngoại lệ và qua thêm chặng duyệt Chủ tịch/TGĐ sau Hội đồng.'} />
    ),
    exceptionCount(doc) === 0 && doc.exceptionNote && doc.status === 'Draft' && (
      <Alert key="declared" type="warning" showIcon title="Đã ghi giải trình ngoại lệ: khi gửi, phiếu qua thêm chặng duyệt Chủ tịch/TGĐ." />
    ),
    doc.hasException && doc.status !== 'Draft' && exceptionCount(doc) === 0 && (
      <Alert key="had-exception" type="info" showIcon title={`Phiếu có ngoại lệ – qua chặng Chủ tịch/TGĐ. Giải trình: ${doc.exceptionNote ?? ''}`} />
    ),
  ].filter(Boolean)
  return notices.length ? <Flex vertical gap={8}>{notices}</Flex> : null
}

/** Tổng số ngoại lệ tự phát hiện trên các dòng (server tính khi phiếu nháp/chờ duyệt – lát H2). */
const exceptionCount = (doc: MonthlyApproval) => doc.lines.reduce((sum, l) => sum + (l.exceptions?.length ?? 0), 0)

/**
 * Trang phiếu duyệt NCC và giá tháng trên khuôn DocumentForm: tháng, nhóm hàng, công ty áp dụng, ghi chú; dòng: mặt hàng, ĐVT, NCC chính +
 * giá, NCC dự phòng + giá, hạn mức SL tháng (gộp các công ty – để đạt bậc chiết khấu của NCC). Nháp → Gửi duyệt → Đã duyệt; Điều chỉnh,
 * Hủy, Chép sang tháng sau (design.md mục 18.25).
 */
export function MonthlyApprovalPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const { hasPermission, workingCompany } = useAuth()
  const [form] = Form.useForm<FormValues>()
  const query = useMonthlyApproval(isNew ? null : id)
  const mutations = useMonthlyApprovalMutations()
  const companies = useMonthlyApprovalCompanies()
  const categories = useItemCategories()
  const suppliers = useSuppliers({})
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const addRef = useRef<(() => void) | null>(null)
  const doc = query.data
  const canCreate = hasPermission(Permission.MonthlyApprovalManage) && !!workingCompany?.isHeadOffice
  const readOnly = isNew ? !canCreate : !doc?.actions.canEdit
  // Đính kèm biên bản họp Ban, bảng so sánh giá – quyền theo quyền lập phiếu (design.md mục 18.4).
  const attachmentTab = useAttachmentTab('MonthlySupplierApproval', doc?.id, hasPermission(Permission.MonthlyApprovalManage))
  // Ý kiến thẩm định của thành viên chuyên môn (lát H1) – Hội đồng xem trước khi duyệt, không phải cấp duyệt.
  const appraisalTabs = useAppraisalTab('MonthlySupplierApproval', doc?.id)
  const appraisals = useAppraisals('MonthlySupplierApproval', doc?.id).data

  const watchedLines = Form.useWatch('lines', form) as LineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])
  // Giá thị trường (ngân hàng giá – phiếu khảo sát đã chốt 180 ngày gần nhất) để Ban so với giá đề xuất (design.md mục 18.25, G4b).
  const market = useMarketPrices(hasPermission(Permission.PriceBankView) ? itemIds : [])
  // Ban chỉ chọn NCC Đã duyệt (danh sách NCC đủ điều kiện – G1); NCC đang có trên phiếu cũ vẫn hiện để đọc được.
  const onDoc = new Set((doc?.lines ?? []).flatMap((l) => [l.mainSupplierId, l.backupSupplierId ?? '']))
  const supplierOptions = (suppliers.data?.items ?? [])
    .filter((s) => s.status === 'Approved' || onDoc.has(s.id))
    .map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))

  const { loadSaved, targetId } = useDocumentFormSync(form, id, doc, (doc) => ({
    period: dayjs(doc.period),
    itemCategoryId: doc.itemCategoryId,
    companyIds: doc.companies.map((c) => c.id),
    note: doc.note ?? '',
    exceptionNote: doc.exceptionNote ?? '',
    lines: doc.lines.map((l) => ({
      id: l.id, itemId: l.itemId, uomId: l.uomId, mainSupplierId: l.mainSupplierId, unitPrice: l.unitPrice, backupSupplierId: l.backupSupplierId,
      backupUnitPrice: l.backupUnitPrice, quantityLimit: l.quantityLimit, note: l.note ?? '', standardPrice: l.standardPrice,
      ceilingPrice: l.ceilingPrice, targetPrice: l.targetPrice,
    })),
  }))

  const open = (target: string) => navigate(`${MONTHLY_APPROVAL_PATH}/${target}`)
  const fail = (e: unknown, fallback: string) => setError(e instanceof Error ? e.message : fallback)

  const persist = async (): Promise<MonthlyApproval | null> => {
    let values: FormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng mặt hàng.')
      return null
    }
    setError(null)
    const input: SaveMonthlyApprovalInput = {
      period: values.period.startOf('month').format('YYYY-MM-DD'),
      itemCategoryId: values.itemCategoryId!,
      companyIds: values.companyIds ?? [],
      note: orNull(values.note),
      exceptionNote: orNull(values.exceptionNote),
      lines: (values.lines ?? []).map((l) => ({
        id: l.id ?? null, itemId: l.itemId!, uomId: l.uomId!, mainSupplierId: l.mainSupplierId!, unitPrice: l.unitPrice ?? 0,
        backupSupplierId: l.backupSupplierId ?? null, backupUnitPrice: l.backupSupplierId ? (l.backupUnitPrice ?? null) : null,
        quantityLimit: l.quantityLimit ?? null, note: orNull(l.note), standardPrice: l.standardPrice ?? null, ceilingPrice: l.ceilingPrice ?? null,
        targetPrice: l.targetPrice ?? null,
      })),
    }
    try {
      const target = targetId()
      const saved = target ? await mutations.update.mutateAsync({ id: target, input }) : await mutations.create.mutateAsync(input)
      loadSaved(saved)
      return saved
    } catch (e) {
      fail(e, 'Không lưu được phiếu.')
      return null
    }
  }

  const save = async () => {
    if (readOnly) return
    const saved = await persist()
    if (!saved) return
    message.success(`Đã lưu phiếu ${saved.no}.`)
    if (isNew) navigate(`${MONTHLY_APPROVAL_PATH}/${saved.id}`, { replace: true })
  }

  // Người lập không tự duyệt; lưu, xem trước chuỗi duyệt rồi mới gửi (như các phiếu có duyệt khác).
  const submit = () => {
    setError(null)
    void confirmSubmitWithPreview(modal, {
      title: 'Gửi Ban cung ứng duyệt phiếu NCC và giá tháng?',
      docType: 'GTH',
      prepare: async () => {
        const saved = await persist()
        if (!saved) return null
        if (isNew) navigate(`${MONTHLY_APPROVAL_PATH}/${saved.id}`, { replace: true })
        return { id: saved.id }
      },
      onSubmit: async (target) => {
        try {
          const sent = await mutations.submit.mutateAsync({ id: target })
          message.success(sent.status === 'Approved' ? `Phiếu ${sent.no} đã duyệt.` : `Đã gửi duyệt phiếu ${sent.no}.`)
        } catch (e) {
          fail(e, 'Không gửi duyệt được.')
        }
      },
      onError: (e) => fail(e, 'Không gửi duyệt được.'),
    })
  }

  const approve = () =>
    doc &&
    modal.confirm({
      title: `Duyệt phiếu ${doc.no}?`,
      content: (
        <>
          <p>Duyệt cấp cuối thì đây là kết quả tháng: Phòng cung ứng chung đặt hàng đúng NCC, giá và hạn mức trên phiếu.</p>
          {doc.hasException && (
            <Alert type="warning" showIcon
              title={`Phiếu có ngoại lệ (${exceptionCount(doc)} ở các dòng) – qua chặng Chủ tịch/TGĐ. Giải trình: ${doc.exceptionNote ?? ''}`} />
          )}
          <AppraisalSummary data={appraisals} />
          {approveContent(doc)}
        </>
      ),
      okText: 'Duyệt',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        try {
          const result = await mutations.approve.mutateAsync({ id: doc.id })
          message.success(result.status === 'Approved' ? `Đã duyệt xong phiếu ${doc.no}.` : `Đã duyệt cấp của bạn – phiếu ${doc.no} chuyển cấp tiếp theo.`)
        } catch (e) {
          fail(e, 'Không duyệt được.')
        }
      },
    })

  const reject = () =>
    doc &&
    confirmReject(modal, message, `Từ chối phiếu ${doc.no}?`, async (reason) => {
      setError(null)
      try {
        await mutations.reject.mutateAsync({ id: doc.id, comment: reason })
        message.success(`Đã từ chối phiếu ${doc.no}.`)
      } catch (e) {
        fail(e, 'Không từ chối được.')
      }
    })

  const recall = () =>
    doc &&
    modal.confirm({
      title: `Rút phiếu ${doc.no} về nháp?`,
      content: 'Các cấp đang chờ duyệt bỏ qua; sửa xong thì gửi duyệt lại từ đầu.',
      okText: 'Rút về nháp',
      cancelText: 'Không',
      onOk: async () => {
        try {
          await mutations.recall.mutateAsync(doc.id)
          message.success(`Đã rút phiếu ${doc.no} về nháp.`)
        } catch (e) {
          fail(e, 'Không rút về nháp được.')
        }
      },
    })

  const cancel = () => {
    if (!doc) return
    let reason = ''
    modal.confirm({
      title: `Hủy phiếu ${doc.no}?`,
      content: (
        <>
          <p>Phiếu hủy không còn tác dụng; các mặt hàng trên phiếu lập được phiếu mới cho tháng này.</p>
          <Input.TextArea aria-label="Lý do hủy" rows={3} maxLength={1000} onChange={(e) => (reason = e.target.value)} />
        </>
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
          fail(e, 'Không hủy được.')
        }
      },
    })
  }

  /** Lập phiếu mới từ phiếu này (điều chỉnh hoặc chép sang tháng sau) rồi mở phiếu mới. */
  const derive = async (kind: 'adjust' | 'copyNextMonth') => {
    if (!doc) return
    setError(null)
    try {
      const created = await mutations[kind].mutateAsync(doc.id)
      message.success(kind === 'adjust' ? `Đã lập phiếu điều chỉnh ${created.no}.` : `Đã chép sang tháng ${formatMonth(created.period)} – phiếu ${created.no}.`)
      open(created.id)
    } catch (e) {
      fail(e, 'Không lập được phiếu.')
    }
  }

  /**
   * Giá thị trường gần nhất của dòng theo ĐVT dòng: phiếu khảo sát mới nhất của mặt hàng, lấy giá thấp nhất trong phiếu đó;
   * giá cơ sở × hệ số ĐVT dòng (ví dụ 10.000/cái, dòng theo thùng 12 cái → 120.000).
   */
  const marketCell = (index: number) => {
    const line = lines[index]
    const rows = (market.data ?? []).filter((m) => m.itemId === line?.itemId)
    if (!line?.uomId || rows.length === 0) return <span className="line-grid-readonly" />
    const latest = rows.filter((m) => m.surveyId === rows[0].surveyId)
    const best = latest.reduce((a, b) => (b.baseUnitPrice < a.baseUnitPrice ? b : a))
    const factor = (line.itemId ? itemsById.get(line.itemId) : undefined)?.uoms.find((u) => u.uomId === line.uomId)?.factor
      ?? doc?.lines.find((l) => l.id === line.id && l.uomId === line.uomId)?.uomFactor
    if (factor === undefined) return <span className="line-grid-readonly" />
    const detail = latest.map((m) => `${m.source}: ${formatMoney(m.baseUnitPrice * factor)}`).join('\n')
    return (
      <span className="line-grid-readonly" title={`${best.surveyNo} ngày ${formatDate(best.surveyDate)}\n${detail}`}>
        {formatMoney(best.baseUnitPrice * factor)}
      </span>
    )
  }

  /**
   * Ngoại lệ của dòng (lát H2) theo dữ liệu đã lưu: tam giác vàng + mô tả, rê chuột xem đủ; dòng chưa lưu thì trống (lưu nháp để tính).
   * @param index Chỉ số dòng trong lưới.
   */
  const exceptionCell = (index: number) => {
    const id = lines[index]?.id
    const found = id ? (doc?.lines.find((l) => l.id === id)?.exceptions ?? []) : []
    if (found.length === 0) return <span className="line-grid-readonly" />
    return (
      <span className="line-grid-readonly" title={found.join('\n')}>
        <WarningFilled className="line-grid-alert-icon" aria-label={`Ngoại lệ dòng ${index + 1}`} /> {found.join('; ')}
      </span>
    )
  }

  const priceInput = (
    name: number, field: 'unitPrice' | 'backupUnitPrice' | 'standardPrice' | 'ceilingPrice' | 'targetPrice', label: string, required: boolean,
  ) => (
    <Form.Item name={[name, field]} noStyle rules={required ? [{ required: true, type: 'number', min: 0, message: `Nhập ${label}.` }] : [{ type: 'number', min: 0 }]}>
      <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} aria-label={`${label} dòng ${name + 1}`} />
    </Form.Item>
  )

  const columns: LineColumn[] = [
    {
      key: 'item', title: 'Mặt hàng', required: true, width: 250,
      render: (field) => (
        <>
          <Form.Item name={[field.name, 'id']} hidden>
            <Input />
          </Form.Item>
          <Form.Item name={[field.name, 'itemId']} noStyle rules={[{ required: true, message: 'Chọn mặt hàng.' }]}>
            <Select
              variant="borderless" style={{ width: '100%' }} aria-label={`Mặt hàng dòng ${field.name + 1}`}
              showSearch={{ filterOption: false, onSearch: setSearch }} placeholder="Gõ mã hoặc tên hàng"
              options={(lookup.data ?? []).map((i) => ({ value: i.id, label: `${i.code} – ${i.name}` }))}
              onChange={(v: string) => form.setFieldValue(['lines', field.name, 'uomId'], itemsById.get(v)?.baseUomId)}
              popupMatchSelectWidth={420}
            />
          </Form.Item>
        </>
      ),
    },
    {
      key: 'uom', title: 'ĐVT', required: true, width: 70,
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
      key: 'main', title: 'NCC chính', required: true, width: 220,
      render: (field) => (
        <Form.Item name={[field.name, 'mainSupplierId']} noStyle rules={[{ required: true, message: 'Chọn NCC chính.' }]}>
          <Select variant="borderless" style={{ width: '100%' }} aria-label={`NCC chính dòng ${field.name + 1}`}
            showSearch={{ optionFilterProp: 'label' }} options={supplierOptions} popupMatchSelectWidth={360} />
        </Form.Item>
      ),
    },
    { key: 'price', title: 'Giá NCC chính', required: true, width: 115, align: 'right', render: (field) => priceInput(field.name, 'unitPrice', 'Giá NCC chính', true) },
    {
      key: 'backup', title: 'NCC dự phòng', width: 220,
      render: (field) => (
        <Form.Item name={[field.name, 'backupSupplierId']} noStyle>
          <Select variant="borderless" style={{ width: '100%' }} aria-label={`NCC dự phòng dòng ${field.name + 1}`} allowClear
            showSearch={{ optionFilterProp: 'label' }} options={supplierOptions} popupMatchSelectWidth={360} />
        </Form.Item>
      ),
    },
    {
      key: 'backupPrice', title: 'Giá dự phòng', width: 115, align: 'right',
      render: (field) => priceInput(field.name, 'backupUnitPrice', 'Giá dự phòng', !!lines[field.name]?.backupSupplierId),
    },
    {
      key: 'limit', title: 'Hạn mức SL tháng', width: 120, align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'quantityLimit']} noStyle rules={[{ type: 'number', min: 0.0001, message: '> 0 hoặc để trống.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={4} placeholder="Không giới hạn"
            aria-label={`Hạn mức dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    // Ngân hàng giá (G4b): ba giá không bắt buộc; giá chính, dự phòng, chuẩn, mục tiêu không vượt trần (server kiểm tra).
    { key: 'standard', title: 'Giá chuẩn', width: 105, align: 'right', render: (field) => priceInput(field.name, 'standardPrice', 'Giá chuẩn', false) },
    { key: 'ceiling', title: 'Giá trần', width: 105, align: 'right', render: (field) => priceInput(field.name, 'ceilingPrice', 'Giá trần', false) },
    { key: 'target', title: 'Giá mục tiêu', width: 105, align: 'right', render: (field) => priceInput(field.name, 'targetPrice', 'Giá mục tiêu', false) },
    { key: 'market', title: 'Giá thị trường', width: 110, align: 'right', render: (field) => marketCell(field.name) },
    { key: 'exception', title: 'Ngoại lệ', width: 220, render: (field) => exceptionCell(field.name) },
    {
      key: 'note', title: 'Ghi chú', width: 180,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle rules={[{ max: 500 }]}>
          <Input variant="borderless" aria-label={`Ghi chú dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : {
        label: doc ? withPendingApprovers(monthlyApprovalStatusLabels[doc.status], doc.approvals) : '',
        tone: doc ? monthlyApprovalStatusTone[doc.status] : ('neutral' as const),
      }

  const actions = (
    <>
      {!readOnly && (
        <>
          <Button type="primary" icon={<SaveOutlined />} onClick={save} loading={mutations.create.isPending || mutations.update.isPending}>
            Lưu nháp
          </Button>
          <Button icon={<SendOutlined />} onClick={submit} loading={mutations.submit.isPending}>
            Gửi duyệt
          </Button>
        </>
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
          Rút về nháp
        </Button>
      )}
      {doc?.actions.canAdjust && (
        <Button icon={<EditOutlined />} onClick={() => void derive('adjust')} loading={mutations.adjust.isPending}>
          Điều chỉnh
        </Button>
      )}
      {doc?.actions.canCopyNextMonth && (
        <Button icon={<CopyOutlined />} onClick={() => void derive('copyNextMonth')} loading={mutations.copyNextMonth.isPending}>
          Chép sang tháng sau
        </Button>
      )}
      {doc?.actions.canCancel && (
        <Button danger icon={<StopOutlined />} onClick={cancel} loading={mutations.cancel.isPending}>
          Hủy phiếu
        </Button>
      )}
      <Button type="text" icon={<CloseOutlined />} onClick={() => navigate(MONTHLY_APPROVAL_PATH)}>
        Đóng
      </Button>
    </>
  )

  return (
    <DocumentForm
      title="Duyệt NCC và giá tháng"
      extraTabs={[
        ...appraisalTabs,
        attachmentTab,
        ...(doc ? [{ key: 'approvals', label: 'Lịch sử duyệt', children: <ApprovalHistory steps={doc.approvals} stageLabels={MONTHLY_STAGE_LABELS} createdByName={doc.createdByName} createdAt={doc.createdAt} /> }] : []),
      ]}
      number={isNew ? null : (doc?.no ?? '')}
      audit={{ entityType: 'MonthlySupplierApproval', id: doc?.id }}
      status={status}
      meta={doc && `Công ty lập: ${doc.companyCode} · Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
      loading={query.isLoading}
      error={error ?? (query.error ? query.error.message : null)}
      notice={doc && <MonthlyNotices doc={doc} onOpen={open} />}
      form={form}
      initialValues={{ period: dayjs().startOf('month'), companyIds: [], lines: [] }}
      readOnly={readOnly}
      onSave={save}
      onAddLine={() => addRef.current?.()}
      actions={actions}
      header={
        <>
          <Form.Item label="Tháng áp dụng" name="period" rules={[{ required: true, message: 'Chọn tháng.' }]}>
            <DatePicker picker="month" format={MONTH_FORMATS} style={{ width: '100%' }} aria-label="Tháng áp dụng" />
          </Form.Item>
          <Form.Item label="Nhóm hàng" name="itemCategoryId" rules={[{ required: true, message: 'Chọn nhóm hàng.' }]}>
            <Select showSearch={{ optionFilterProp: 'label' }} aria-label="Nhóm hàng"
              options={(categories.data ?? []).map((c) => ({ value: c.id, label: `${c.code} – ${c.name}` }))} />
          </Form.Item>
          <Form.Item label="Công ty áp dụng" name="companyIds" rules={[{ required: true, type: 'array', min: 1, message: 'Chọn ít nhất một công ty.' }]}>
            <Select mode="multiple" aria-label="Công ty áp dụng" optionFilterProp="label"
              options={(companies.data ?? doc?.companies ?? []).map((c) => ({ value: c.id, label: `${c.code} – ${c.name}` }))} />
          </Form.Item>
          <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]}>
            <Input aria-label="Ghi chú" />
          </Form.Item>
          {/* Bắt buộc khi có ngoại lệ ở cột Ngoại lệ; ghi khi không có thì là tự khai ngoại lệ – cũng lên Chủ tịch/TGĐ (Q6, lát H2). */}
          <Form.Item label="Giải trình ngoại lệ" name="exceptionNote" rules={[{ max: 1000 }]}
            tooltip="Bắt buộc khi phiếu có ngoại lệ (cột Ngoại lệ). Có giải trình thì phiếu qua thêm chặng duyệt Chủ tịch/TGĐ.">
            <Input aria-label="Giải trình ngoại lệ" />
          </Form.Item>
        </>
      }
      lines={
        <LineGrid name="lines" auditEntity="MonthlySupplierApprovalLine" columns={columns} readOnly={readOnly} addRef={addRef}
          newLine={() => ({ id: null, unitPrice: 0 })} />
      }
    />
  )
}
