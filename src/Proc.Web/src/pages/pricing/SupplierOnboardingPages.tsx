import { CheckOutlined, CloseOutlined, EditOutlined, RollbackOutlined, SaveOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import { Alert, App, Button, Checkbox, DatePicker, Descriptions, Flex, Form, Input, InputNumber } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useItemCategoryCatalog, useSuppliers } from '../../api/masterData'
import {
  onboardingCriteria,
  onboardingResultLabels,
  onboardingStatusLabels,
  onboardingStatusTone,
  useSupplierOnboarding,
  useSupplierOnboardingMutations,
  useSupplierOnboardings,
  type OnboardingResult,
  type OnboardingScoreKey,
  type SaveSupplierOnboardingInput,
  type SupplierOnboarding,
  type SupplierOnboardingListItem,
} from '../../api/supplierOnboardings'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { Select } from '../../classic/Select'
import { TreeSelect } from '../../classic/TreeSelect'
import { useAppraisalTab } from '../../components/AppraisalPanel/useAppraisalTab'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { DataGrid } from '../../components/DataGrid/DataGrid'
import type { GridColumn } from '../../components/DataGrid/types'
import { DocumentForm } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { useDocumentFormSync } from '../../components/DocumentForm/useDocumentFormSync'
import { ListPage } from '../../components/ListPage/ListPage'
import { DATE_INPUT_FORMATS, formatDateTime, formatQuantity } from '../../utils/format'
import { buildCategoryTree } from '../masterData/categoryTree'

/** Đường dẫn danh sách hồ sơ đánh giá NCC mới (màn hình HSN). */
export const ONBOARDING_PATH = '/gia-ncc/ho-so-ncc-moi'

/** Danh mục giấy tờ cố định (khớp SupplierOnboardingDocuments ở server) – dùng khi lập mới. */
const DOCUMENTS = [
  { code: 'LICENSE', name: 'Giấy phép kinh doanh, mã số thuế', isRequiredForPass: true },
  { code: 'QUALITY', name: 'Chứng nhận chất lượng (ISO, ATTP, HACCP…)', isRequiredForPass: false },
  { code: 'QUOTE', name: 'Báo giá chính thức', isRequiredForPass: false },
  { code: 'SAMPLE', name: 'Hàng mẫu, kết quả thử mẫu', isRequiredForPass: false },
  { code: 'FINANCE', name: 'Báo cáo / năng lực tài chính', isRequiredForPass: false },
]

/** Chữ kết luận đề xuất của hồ sơ đánh giá NCC mới. */
const recommendLabel = (r: boolean | null) => (r === null ? '' : r ? 'Đề xuất' : 'Không đề xuất')

const listColumns: GridColumn<SupplierOnboardingListItem>[] = [
  { key: 'no', title: 'Số hồ sơ', width: 150, value: (r) => r.no },
  { key: 'date', title: 'Ngày đánh giá', width: 100, type: 'date', value: (r) => r.date },
  { key: 'supplier', title: 'Nhà cung cấp', width: 260, value: (r) => `${r.supplierCode} – ${r.supplierName}` },
  { key: 'category', title: 'Nhóm hàng đề xuất', width: 200, value: (r) => r.categoryName, groupable: true },
  { key: 'score', title: 'Điểm', width: 70, type: 'quantity', value: (r) => r.score },
  { key: 'result', title: 'Kết luận', width: 90, value: (r) => (r.result ? onboardingResultLabels[r.result] : ''), groupable: true },
  { key: 'recommended', title: 'Văn phòng Ban', width: 110, value: (r) => recommendLabel(r.recommended), groupable: true },
  {
    key: 'status', title: 'Trạng thái', width: 140, type: 'status', groupable: true,
    value: (r) => onboardingStatusLabels[r.status], tone: (r) => onboardingStatusTone[r.status],
  },
  { key: 'creator', title: 'Người lập', width: 150, value: (r) => r.createdByName, groupable: true },
]

/**
 * Màn hình Giá và NCC → Hồ sơ NCC mới (HSN – design.md mục 18.25, lát G8): Phòng cung ứng chung lập hồ sơ, đánh giá NCC Tiềm năng; Văn phòng
 * Ban đề xuất; Hội đồng duyệt ở màn hình Duyệt NCC. Chỉ lập ở tổng công ty.
 */
export function SupplierOnboardingListPage() {
  const navigate = useNavigate()
  const { hasPermission, workingCompany } = useAuth()
  const list = useSupplierOnboardings()
  return (
    <ListPage
      title="Hồ sơ đánh giá NCC mới"
      exportFileName="ho-so-danh-gia-ncc-moi"
      rows={list.data ?? []}
      columns={listColumns}
      rowKey={(r) => r.id}
      auditEntity="SupplierOnboarding"
      loading={list.isLoading}
      canCreate={hasPermission(Permission.SupplierOnboardingManage) && !!workingCompany?.isHeadOffice}
      onCreate={() => navigate(`${ONBOARDING_PATH}/moi`)}
      onOpenRow={(r) => navigate(`${ONBOARDING_PATH}/${r.id}`)}
      // Việc còn phải làm: Phòng cung ứng (Nháp), Văn phòng Ban (Đã đánh giá).
      isMarked={(r) => r.status === 'Draft' || r.status === 'Evaluated'}
      notice={
        <Alert type="info" showIcon
          title="NCC tạo mới muốn lên Đã duyệt phải qua hồ sơ này: Phòng cung ứng chung đánh giá → Văn phòng Ban đề xuất → Hội đồng duyệt (màn hình Duyệt NCC)." />
      }
    />
  )
}

interface DocumentValues {
  code: string
  name: string
  isRequiredForPass: boolean
  provided: boolean
  validTo: Dayjs | null
  note?: string
}

type FormValues = {
  supplierId?: string
  categoryId?: string
  date: Dayjs
  source?: string
  documents: DocumentValues[]
  result?: OnboardingResult | null
  comment?: string
  note?: string
} & Partial<Record<OnboardingScoreKey, number | null>>

/** Chuỗi rỗng hoặc toàn khoảng trắng gửi lên API là null; còn lại bỏ khoảng trắng hai đầu. */
const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Thông báo đầu hồ sơ theo trạng thái: chờ Văn phòng Ban, chờ Hội đồng (kèm yêu cầu duyệt), kết quả, lý do.
 * @param doc Hồ sơ đã tải.
 */
function OnboardingNotices({ doc }: { doc: SupplierOnboarding }) {
  const changeLink = doc.changeRequestId && (
    <Link to={`/gia-ncc/duyet-ncc/${doc.changeRequestId}`}>yêu cầu duyệt {doc.changeRequestNo}</Link>
  )
  const notices = [
    doc.status === 'Evaluated' && (
      <Alert key="evaluated" type="warning" showIcon
        title={`Phòng cung ứng đã hoàn thành đánh giá${doc.evaluatedByName ? ` (${doc.evaluatedByName})` : ''} – chờ Văn phòng Ban đề xuất.`} />
    ),
    doc.status === 'Submitted' && (
      <Alert key="submitted" type="warning" showIcon title={<>Văn phòng Ban đã đề xuất – chờ Hội đồng duyệt theo {changeLink}.</>} />
    ),
    doc.status === 'Approved' && (
      <Alert key="approved" type="success" showIcon
        title={<>Hội đồng đã duyệt {doc.approvedAt ? formatDateTime(doc.approvedAt) : ''} ({changeLink}): NCC chuyển Đã duyệt, ngành hàng {doc.categoryName}.</>} />
    ),
    doc.status === 'Rejected' && <Alert key="rejected" type="error" showIcon title={`Hội đồng từ chối: ${doc.rejectReason ?? ''}`} />,
    doc.status === 'NotRecommended' && (
      <Alert key="not" type="error" showIcon title={`Văn phòng Ban không đề xuất: ${doc.proposalComment ?? ''}`} />
    ),
    doc.status === 'Cancelled' && <Alert key="cancel" type="error" showIcon title={`Đã hủy: ${doc.cancelReason ?? ''}`} />,
  ].filter(Boolean)
  return notices.length ? <Flex vertical gap={8}>{notices}</Flex> : null
}

/**
 * Trang hồ sơ đánh giá NCC mới trên khuôn DocumentForm (design.md mục 18.25 – G8): đầu phiếu NCC, nhóm hàng đề xuất, ngày, nguồn, ghi chú;
 * lưới Hồ sơ pháp lý 5 giấy tờ cố định; tab Đánh giá (6 tiêu chí 1–5, kết luận), Đề xuất (Văn phòng Ban), Thẩm định, Đính kèm.
 */
export function SupplierOnboardingPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const { hasPermission, workingCompany } = useAuth()
  const [form] = Form.useForm<FormValues>()
  const query = useSupplierOnboarding(isNew ? null : id)
  const mutations = useSupplierOnboardingMutations()
  const catalog = useItemCategoryCatalog()
  const potential = useSuppliers({ status: 'Potential' })
  const [error, setError] = useState<string | null>(null)
  const doc = query.data
  const canManage = hasPermission(Permission.SupplierOnboardingManage) && !!workingCompany?.isHeadOffice
  const readOnly = isNew ? !canManage : !doc?.actions.canEdit
  const attachmentTab = useAttachmentTab('SupplierOnboarding', doc?.id, hasPermission(Permission.SupplierOnboardingManage))
  // Ý kiến thẩm định (H1) ghi trên NCC – cùng nội dung với ngăn kéo NCC và màn hình Duyệt NCC.
  const appraisalTabs = useAppraisalTab('Supplier', doc?.supplierId)

  const categoryTree = useMemo(() => buildCategoryTree(catalog.data ?? [], { activeOnly: true }), [catalog.data])
  const supplierOptions = useMemo(() => {
    const options = (potential.data?.items ?? []).map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))
    // Hồ sơ đã lập: NCC có thể không còn Tiềm năng (đã duyệt) – vẫn hiện đúng tên.
    if (doc && !options.some((o) => o.value === doc.supplierId)) options.unshift({ value: doc.supplierId, label: `${doc.supplierCode} – ${doc.supplierName}` })
    return options
  }, [potential.data, doc])

  const { loadSaved, targetId } = useDocumentFormSync(form, id, doc, (d) => ({
    supplierId: d.supplierId,
    categoryId: d.categoryId,
    date: dayjs(d.date),
    source: d.source ?? '',
    documents: d.documents.map((x) => ({ ...x, validTo: x.validTo ? dayjs(x.validTo) : null, note: x.note ?? '' })),
    result: d.result,
    comment: d.comment ?? '',
    note: d.note ?? '',
    ...Object.fromEntries(onboardingCriteria.map((c) => [c.key, d[c.key]])),
  }))

  const scores = Form.useWatch((values: FormValues) => onboardingCriteria.map((c) => values[c.key]), form) ?? []
  // Điểm tạm tính để người đánh giá thấy ngay; điểm chính thức do server tính lúc hoàn thành (bình quân 6 tiêu chí, 2 số lẻ).
  const average = scores.length > 0 && scores.every((s) => typeof s === 'number') ? Math.round(((scores as number[]).reduce((a, b) => a + b, 0) / scores.length) * 100) / 100 : null

  const fail = (e: unknown, fallback: string) => setError(e instanceof Error ? e.message : fallback)

  const collect = async (): Promise<SaveSupplierOnboardingInput | null> => {
    let values: FormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu hồ sơ, lưới hồ sơ pháp lý và tab Đánh giá.')
      return null
    }
    setError(null)
    return {
      supplierId: values.supplierId!,
      categoryId: values.categoryId!,
      date: values.date.format('YYYY-MM-DD'),
      source: orNull(values.source),
      documents: (values.documents ?? []).map((d) => ({
        code: d.code, provided: !!d.provided, validTo: d.provided && d.validTo ? d.validTo.format('YYYY-MM-DD') : null, note: orNull(d.note),
      })),
      result: values.result ?? null,
      comment: orNull(values.comment),
      note: orNull(values.note),
      ...(Object.fromEntries(onboardingCriteria.map((c) => [c.key, values[c.key] ?? null])) as Record<OnboardingScoreKey, number | null>),
    }
  }

  /** Lưu nháp (lập mới hoặc sửa); trả về hồ sơ đã lưu. */
  const persist = async (): Promise<SupplierOnboarding | null> => {
    const input = await collect()
    if (!input) return null
    try {
      const target = targetId()
      const saved = target ? await mutations.update.mutateAsync({ id: target, input }) : await mutations.create.mutateAsync(input)
      loadSaved(saved)
      if (isNew) navigate(`${ONBOARDING_PATH}/${saved.id}`, { replace: true })
      return saved
    } catch (e) {
      fail(e, 'Không lưu được hồ sơ.')
      return null
    }
  }

  const save = async () => {
    if (readOnly) return
    const saved = await persist()
    if (saved) message.success(`Đã lưu hồ sơ ${saved.no}.`)
  }

  // Hoàn thành đánh giá: hồ sơ mới thì lưu trước để có số; server lưu nội dung rồi kiểm tra đủ điểm, kết luận, giấy phép.
  const evaluate = () =>
    modal.confirm({
      title: 'Hoàn thành đánh giá?',
      content: 'Hồ sơ chuyển cho Văn phòng Ban đề xuất; muốn sửa thì mở lại đánh giá (khi Văn phòng Ban chưa đề xuất).',
      okText: 'Hoàn thành',
      cancelText: 'Không',
      onOk: async () => {
        const input = await collect()
        if (!input) return
        try {
          const target = targetId() ?? (await persist())?.id
          if (!target) return
          const done = await mutations.evaluate.mutateAsync({ id: target, input })
          loadSaved(done)
          message.success(`Đã hoàn thành đánh giá hồ sơ ${done.no}.`)
        } catch (e) {
          fail(e, 'Không hoàn thành được đánh giá.')
        }
      },
    })

  const run = async (action: () => Promise<SupplierOnboarding>, done: string, fallback: string) => {
    try {
      const saved = await action()
      loadSaved(saved)
      message.success(done)
    } catch (e) {
      fail(e, fallback)
    }
  }

  /** Hộp thoại nhập chữ (ý kiến, lý do) rồi chạy thao tác; bắt buộc nhập khi `required`. */
  const askText = (opts: { title: string; content: string; label: string; okText: string; danger?: boolean; required: boolean; onOk: (text: string) => Promise<void> }) => {
    let text = ''
    modal.confirm({
      title: opts.title,
      content: (
        <>
          <p>{opts.content}</p>
          <Input.TextArea aria-label={opts.label} rows={3} maxLength={2000} onChange={(e) => (text = e.target.value)} />
        </>
      ),
      okText: opts.okText,
      okButtonProps: { danger: opts.danger },
      cancelText: 'Không',
      onOk: async () => {
        if (opts.required && !text.trim()) {
          message.error(`Hãy nhập ${opts.label.toLowerCase()}.`)
          throw new Error('missing text')
        }
        await opts.onOk(text.trim())
      },
    })
  }

  const recommend = () => doc && askText({
    title: `Đề xuất đưa NCC ${doc.supplierCode} vào danh sách?`,
    content: 'Lập yêu cầu duyệt NCC sang Đã duyệt gửi Hội đồng (màn hình Duyệt NCC). Duyệt xong NCC được mua hàng và có ngành hàng là nhóm đề xuất.',
    label: 'Ý kiến đề xuất', okText: 'Đề xuất', required: false,
    onOk: (text) => run(() => mutations.propose.mutateAsync({ id: doc.id, recommend: true, comment: text || null }),
      `Đã đề xuất hồ sơ ${doc.no} lên Hội đồng.`, 'Không đề xuất được.'),
  })

  const notRecommend = () => doc && askText({
    title: `Không đề xuất NCC ${doc.supplierCode}?`,
    content: 'Hồ sơ kết thúc; muốn xét lại thì Phòng cung ứng lập hồ sơ mới.',
    label: 'Lý do không đề xuất', okText: 'Không đề xuất', danger: true, required: true,
    onOk: (text) => run(() => mutations.propose.mutateAsync({ id: doc.id, recommend: false, comment: text }),
      `Đã ghi không đề xuất hồ sơ ${doc.no}.`, 'Không ghi được ý kiến.'),
  })

  const withdraw = () => doc && modal.confirm({
    title: `Rút đề xuất hồ sơ ${doc.no}?`,
    content: `Yêu cầu duyệt ${doc.changeRequestNo ?? ''} bị rút, hồ sơ về Đã đánh giá để đề xuất lại hoặc Phòng cung ứng mở lại sửa.`,
    okText: 'Rút đề xuất',
    okButtonProps: { danger: true },
    cancelText: 'Không',
    onOk: () => run(() => mutations.withdraw.mutateAsync(doc.id), `Đã rút đề xuất hồ sơ ${doc.no}.`, 'Không rút được.'),
  })

  const reopen = () => doc && run(() => mutations.reopen.mutateAsync(doc.id), `Đã mở lại đánh giá hồ sơ ${doc.no}.`, 'Không mở lại được.')

  const cancel = () => doc && askText({
    title: `Hủy hồ sơ ${doc.no}?`,
    content: 'Hồ sơ hủy thì không xét tiếp; NCC giữ trạng thái hiện tại.',
    label: 'Lý do hủy', okText: 'Hủy hồ sơ', danger: true, required: true,
    onOk: (text) => run(() => mutations.cancel.mutateAsync({ id: doc.id, reason: text }), `Đã hủy hồ sơ ${doc.no}.`, 'Không hủy được.'),
  })

  // preserve: tên giấy tờ, cờ bắt buộc không có ô nhập (không đăng ký) – useWatch mặc định chỉ trả về ô đã đăng ký.
  const watchedDocs = Form.useWatch('documents', { form, preserve: true }) as DocumentValues[] | undefined
  const docColumns: LineColumn[] = [
    {
      key: 'name', title: 'Giấy tờ', width: 300,
      render: (field) => {
        const row = watchedDocs?.[field.name]
        return (
          <>
            <Form.Item name={[field.name, 'code']} hidden><Input /></Form.Item>
            <span>{row?.name}{row?.isRequiredForPass ? ' (bắt buộc khi Đạt)' : ''}</span>
          </>
        )
      },
    },
    {
      key: 'provided', title: 'Đã nộp', width: 80, align: 'center',
      render: (field) => (
        <Form.Item name={[field.name, 'provided']} valuePropName="checked" noStyle>
          <Checkbox aria-label={`Đã nộp dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'validTo', title: 'Hết hiệu lực', width: 130,
      render: (field) => (
        <Form.Item name={[field.name, 'validTo']} noStyle>
          <DatePicker variant="borderless" format={DATE_INPUT_FORMATS} style={{ width: '100%' }} aria-label={`Hết hiệu lực dòng ${field.name + 1}`}
            disabled={readOnly || !watchedDocs?.[field.name]?.provided} />
        </Form.Item>
      ),
    },
    {
      key: 'note', title: 'Ghi chú (số giấy, nơi cấp…)', width: 300,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle rules={[{ max: 500 }]}>
          <Input variant="borderless" aria-label={`Ghi chú giấy tờ dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const evaluationTab = {
    key: 'evaluation',
    label: 'Đánh giá',
    children: (
      <div style={{ maxWidth: 720 }}>
        {onboardingCriteria.map((c) => (
          <Form.Item key={c.key} label={c.label} name={c.key} layout="horizontal" labelCol={{ flex: '220px' }}
            rules={[{ type: 'number', min: 1, max: 5, message: 'Điểm từ 1 đến 5.' }]}>
            <InputNumber min={1} max={5} precision={0} style={{ width: 90 }} aria-label={c.label} placeholder="1–5" />
          </Form.Item>
        ))}
        <Form.Item label="Điểm bình quân" layout="horizontal" labelCol={{ flex: '220px' }}>
          <span className="erp-number">{doc?.score != null && !doc.actions.canEdit ? formatQuantity(doc.score) : average !== null ? formatQuantity(average) : '—'}</span>
        </Form.Item>
        <Form.Item label="Kết luận" name="result" layout="horizontal" labelCol={{ flex: '220px' }}>
          <Select showSearch={false} allowClear style={{ width: 160 }} aria-label="Kết luận"
            options={(['Pass', 'Fail'] as const).map((r) => ({ value: r, label: onboardingResultLabels[r] }))} />
        </Form.Item>
        <Form.Item label="Nhận xét" name="comment" rules={[{ max: 2000 }]}
          extra="Bắt buộc khi kết luận Không đạt hoặc có tiêu chí từ 2 điểm trở xuống. Đạt cần đã nộp giấy phép kinh doanh còn hiệu lực.">
          <Input.TextArea rows={3} aria-label="Nhận xét" />
        </Form.Item>
        {doc?.evaluatedAt && <p>Hoàn thành đánh giá: {doc.evaluatedByName} – {formatDateTime(doc.evaluatedAt)}</p>}
      </div>
    ),
  }

  const proposalTab = {
    key: 'proposal',
    label: 'Đề xuất',
    children: doc ? (
      <Descriptions column={1} size="small" bordered style={{ maxWidth: 720 }}>
        <Descriptions.Item label="Văn phòng Ban">{doc.recommended === null ? 'Chưa có ý kiến' : recommendLabel(doc.recommended)}</Descriptions.Item>
        <Descriptions.Item label="Ý kiến">{doc.proposalComment ?? '—'}</Descriptions.Item>
        <Descriptions.Item label="Người đề xuất">{doc.proposedByName ? `${doc.proposedByName} – ${formatDateTime(doc.proposedAt!)}` : '—'}</Descriptions.Item>
        <Descriptions.Item label="Yêu cầu duyệt NCC">
          {doc.changeRequestId ? <Link to={`/gia-ncc/duyet-ncc/${doc.changeRequestId}`}>{doc.changeRequestNo}</Link> : '—'}
        </Descriptions.Item>
      </Descriptions>
    ) : <p>Lưu và hoàn thành đánh giá để Văn phòng Ban đề xuất.</p>,
  }

  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? onboardingStatusLabels[doc.status] : '', tone: doc ? onboardingStatusTone[doc.status] : ('neutral' as const) }

  const busy = mutations.create.isPending || mutations.update.isPending
  const actions = (
    <>
      {!readOnly && (
        <>
          <Button type="primary" icon={<SaveOutlined />} onClick={save} loading={busy}>Lưu nháp</Button>
          <Button icon={<CheckOutlined />} onClick={evaluate} loading={mutations.evaluate.isPending}>Hoàn thành đánh giá</Button>
        </>
      )}
      {doc?.actions.canReopen && <Button icon={<EditOutlined />} onClick={reopen} loading={mutations.reopen.isPending}>Mở lại đánh giá</Button>}
      {doc?.actions.canPropose && (
        <>
          <Button type="primary" icon={<SendOutlined />} onClick={recommend} loading={mutations.propose.isPending}>Đề xuất Hội đồng</Button>
          <Button danger icon={<StopOutlined />} onClick={notRecommend}>Không đề xuất</Button>
        </>
      )}
      {doc?.actions.canWithdraw && <Button danger icon={<RollbackOutlined />} onClick={withdraw} loading={mutations.withdraw.isPending}>Rút đề xuất</Button>}
      {doc?.actions.canCancel && <Button danger icon={<StopOutlined />} onClick={cancel} loading={mutations.cancel.isPending}>Hủy hồ sơ</Button>}
      <Button type="text" icon={<CloseOutlined />} onClick={() => navigate(ONBOARDING_PATH)}>Đóng</Button>
    </>
  )

  return (
    <DocumentForm
      title="Hồ sơ đánh giá NCC mới"
      number={isNew ? null : (doc?.no ?? '')}
      audit={{ entityType: 'SupplierOnboarding', id: doc?.id }}
      status={status}
      meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)} · NCC đang ${doc.supplierStatus === 'Approved' ? 'Đã duyệt' : doc.supplierStatus === 'Potential' ? 'Tiềm năng' : doc.supplierStatus}`}
      loading={query.isLoading}
      error={error ?? (query.error ? query.error.message : null)}
      notice={doc && <OnboardingNotices doc={doc} />}
      form={form}
      initialValues={{ date: dayjs(), documents: DOCUMENTS.map((d) => ({ ...d, provided: false, validTo: null, note: '' })) }}
      readOnly={readOnly}
      onSave={save}
      linesLabel="Hồ sơ pháp lý"
      extraTabs={[evaluationTab, proposalTab, ...appraisalTabs, attachmentTab]}
      actions={actions}
      header={
        <>
          <Form.Item label="Nhà cung cấp (Tiềm năng)" name="supplierId" rules={[{ required: true, message: 'Chọn nhà cung cấp.' }]}>
            <Select aria-label="Nhà cung cấp" options={supplierOptions} loading={potential.isLoading} disabled={readOnly || !isNew}
              popupMatchSelectWidth={420} placeholder="Gõ mã hoặc tên NCC" />
          </Form.Item>
          <Form.Item label="Nhóm hàng đề xuất" name="categoryId" rules={[{ required: true, message: 'Chọn nhóm hàng đề xuất.' }]}>
            <TreeSelect aria-label="Nhóm hàng đề xuất" treeData={categoryTree} loading={catalog.isLoading} placeholder="Chọn nhóm hàng" />
          </Form.Item>
          <Form.Item label="Ngày đánh giá" name="date" rules={[{ required: true, message: 'Chọn ngày đánh giá.' }]}>
            <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} aria-label="Ngày đánh giá" disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
          </Form.Item>
          <Form.Item label="Nguồn tìm thấy NCC" name="source" rules={[{ max: 500 }]}>
            <Input aria-label="Nguồn tìm thấy NCC" placeholder="Giới thiệu, hội chợ, website…" />
          </Form.Item>
          <Form.Item label="Ghi chú" name="note" rules={[{ max: 2000 }]}>
            <Input aria-label="Ghi chú" />
          </Form.Item>
        </>
      }
      lines={<LineGrid name="documents" columns={docColumns} readOnly={readOnly} fixedRows newLine={() => ({})} />}
    />
  )
}

/** Lưới hồ sơ NCC mới chỉ xem (cùng cột màn hình HSN) – dùng ở báo cáo tháng cho Ban (lát G12). */
export function SupplierOnboardingGrid({ rows }: { rows: SupplierOnboardingListItem[] }) {
  return <DataGrid rows={rows} columns={listColumns} rowKey={(r) => r.id} groupBy={null} onGroupByChange={() => {}} />
}
