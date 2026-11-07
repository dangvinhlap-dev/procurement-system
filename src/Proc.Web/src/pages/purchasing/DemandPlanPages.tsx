import { useDocumentFormSync } from '../../components/DocumentForm/useDocumentFormSync'
import { CheckOutlined, CloseOutlined, EditOutlined, SaveOutlined, StopOutlined } from '@ant-design/icons'
import { Alert, App, Button, Flex, Form, Input, InputNumber } from 'antd'
import { Select } from '../../classic/Select'
import { useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { useItemLookup } from '../../api/inventoryDocuments'
import {
  demandPlanStatusLabels,
  demandPlanStatusTone,
  useDemandPlan,
  useDemandPlanMutations,
  useDemandPlans,
  type DemandPlan,
  type DemandPlanListItem,
  type SaveDemandPlanInput,
} from '../../api/demandPlans'
import { useItemCategories } from '../../api/masterData'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import type { GridColumn } from '../../components/DataGrid/types'
import { DocumentForm } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { ListPage } from '../../components/ListPage/ListPage'
import { formatDate, formatDateTime, formatQuantity, vnNumberInput } from '../../utils/format'

/** Đường dẫn danh sách phiếu kế hoạch nhu cầu (màn hình KHN). */
export const DEMAND_PLAN_PATH = '/mua-hang/ke-hoach-nhu-cau'

/** Năm hiện tại theo giờ máy người dùng (server kiểm tra lại theo giờ Việt Nam). */
const thisYear = () => new Date().getFullYear()

/** Số năm tới tối đa được lập kế hoạch – khớp DemandPlan.MaxYearsAhead ở server. */
const MAX_YEARS_AHEAD = 2

const QUARTER_NAMES = ['I', 'II', 'III', 'IV']

const listColumns: GridColumn<DemandPlanListItem>[] = [
  { key: 'no', title: 'Số phiếu', width: 150, value: (r) => r.no },
  { key: 'year', title: 'Năm', width: 70, value: (r) => String(r.year), groupable: true },
  { key: 'category', title: 'Nhóm hàng', width: 220, value: (r) => `${r.categoryCode} – ${r.categoryName}`, groupable: true },
  { key: 'lines', title: 'Số mặt hàng', width: 90, type: 'quantity', value: (r) => r.lineCount },
  {
    key: 'status', title: 'Trạng thái', width: 110, type: 'status', groupable: true,
    value: (r) => demandPlanStatusLabels[r.status], tone: (r) => demandPlanStatusTone[r.status],
  },
  { key: 'replaces', title: 'Điều chỉnh phiếu', width: 150, value: (r) => r.replacesNo },
  { key: 'creator', title: 'Người lập', width: 150, value: (r) => r.createdByName, groupable: true },
]

/**
 * Màn hình Mua hàng → Kế hoạch nhu cầu (KHN – design.md mục 18.25, lát G5a): mỗi công ty lập nhu cầu năm × nhóm hàng, SL từng tháng của
 * mỗi mặt hàng; chốt để tổng hợp nhu cầu gộp làm cơ sở thương lượng.
 */
export function DemandPlanListPage() {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const list = useDemandPlans()
  return (
    <ListPage
      title="Kế hoạch nhu cầu"
      exportFileName="ke-hoach-nhu-cau"
      rows={list.data ?? []}
      columns={listColumns}
      rowKey={(r) => r.id}
      auditEntity="DemandPlan"
      loading={list.isLoading}
      canCreate={hasPermission(Permission.DemandPlanManage)}
      onCreate={() => navigate(`${DEMAND_PLAN_PATH}/moi`)}
      onOpenRow={(r) => navigate(`${DEMAND_PLAN_PATH}/${r.id}`)}
      isMarked={(r) => r.status === 'Draft'}
    />
  )
}

interface LineValues {
  id: string | null
  itemId?: string
  uomId?: string
  months?: (number | null)[]
  note?: string
}

interface FormValues {
  year?: number
  itemCategoryId?: string
  note?: string
  lines: LineValues[]
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/** SL 12 tháng của dòng đang nhập (ô trống tính 0). */
const monthsOf = (line: LineValues | undefined) => Array.from({ length: 12 }, (_, m) => Number(line?.months?.[m] ?? 0) || 0)

/**
 * Thông báo đầu phiếu: đã chốt, đã thay, điều chỉnh phiếu nào, đã hủy, lý do chỉ xem.
 * @param doc Phiếu đã tải.
 * @param onOpen Mở phiếu khác (phiếu được điều chỉnh / phiếu điều chỉnh).
 */
function PlanNotices({ doc, onOpen }: { doc: DemandPlan; onOpen: (id: string) => void }) {
  const link = (id: string, no: string) => (
    <Button type="link" size="small" style={{ padding: 0, height: 'auto' }} onClick={() => onOpen(id)}>
      {no}
    </Button>
  )
  const notices = [
    doc.replacesId && doc.replacesNo && (
      <Alert key="replaces" type="info" showIcon
        title={<>Phiếu điều chỉnh của {link(doc.replacesId, doc.replacesNo)} – chốt xong thì phiếu đó thành "Đã thay".</>} />
    ),
    doc.status === 'Confirmed' && (
      <Alert key="confirmed" type="success" showIcon
        title={`Đã chốt ${doc.confirmedAt ? formatDateTime(doc.confirmedAt) : ''}${doc.confirmedByName ? ` – ${doc.confirmedByName}` : ''}: số liệu dùng để tổng hợp nhu cầu gộp, không sửa được (sửa bằng phiếu điều chỉnh).`} />
    ),
    doc.replacedById && doc.replacedByNo && (
      <Alert key="replaced" type="warning" showIcon
        title={<>{doc.status === 'Replaced' ? 'Đã thay bằng phiếu' : 'Đang có phiếu điều chỉnh'} {link(doc.replacedById, doc.replacedByNo)}.</>} />
    ),
    doc.status === 'Cancelled' && <Alert key="cancel" type="error" showIcon title={`Đã hủy ${doc.cancelledOn ? formatDate(doc.cancelledOn) : ''}: ${doc.cancelReason ?? ''}`} />,
    doc.actions.readOnlyReason && <Alert key="readonly" type="info" showIcon title={doc.actions.readOnlyReason} />,
  ].filter(Boolean)
  return notices.length ? <Flex vertical gap={8}>{notices}</Flex> : null
}

/**
 * Trang phiếu kế hoạch nhu cầu trên khuôn DocumentForm: năm, nhóm hàng, ghi chú; dòng: mặt hàng, ĐVT, SL tháng 1–12 (xen tổng quý, cuối là
 * cả năm – tự tính). Nháp → Chốt; đã chốt thì Điều chỉnh (phiếu mới) hoặc Hủy có lý do (design.md mục 18.25).
 */
export function DemandPlanPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<FormValues>()
  const query = useDemandPlan(isNew ? null : id)
  const mutations = useDemandPlanMutations()
  const categories = useItemCategories()
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const addRef = useRef<(() => void) | null>(null)
  const doc = query.data
  const canManage = hasPermission(Permission.DemandPlanManage)
  const readOnly = isNew ? !canManage : !doc?.actions.canEdit
  // Đính kèm căn cứ lập kế hoạch (định mức, đề xuất của kho, bộ phận) – quyền theo quyền lập phiếu (design.md mục 18.4).
  const attachmentTab = useAttachmentTab('DemandPlan', doc?.id, canManage)

  const watchedLines = Form.useWatch('lines', form) as LineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])
  const years = Array.from({ length: MAX_YEARS_AHEAD + 1 }, (_, i) => thisYear() + i)
  if (doc && !years.includes(doc.year)) years.unshift(doc.year)

  const { loadSaved, targetId } = useDocumentFormSync(form, id, doc, (doc) => ({
    year: doc.year,
    itemCategoryId: doc.itemCategoryId,
    note: doc.note ?? '',
    lines: doc.lines.map((l) => ({ id: l.id, itemId: l.itemId, uomId: l.uomId, months: [...l.months], note: l.note ?? '' })),
  }))

  const fail = (e: unknown, fallback: string) => setError(e instanceof Error ? e.message : fallback)
  const open = (target: string) => navigate(`${DEMAND_PLAN_PATH}/${target}`)

  const persist = async (): Promise<DemandPlan | null> => {
    let values: FormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng mặt hàng.')
      return null
    }
    setError(null)
    const input: SaveDemandPlanInput = {
      year: values.year!,
      itemCategoryId: values.itemCategoryId!,
      note: orNull(values.note),
      lines: (values.lines ?? []).map((l) => ({
        id: l.id ?? null, itemId: l.itemId!, uomId: l.uomId!, months: monthsOf(l), note: orNull(l.note),
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
    if (isNew) navigate(`${DEMAND_PLAN_PATH}/${saved.id}`, { replace: true })
  }

  // Chốt: lưu trước rồi chốt – số liệu vào tổng hợp nhu cầu, không sửa được nữa.
  const confirm = () =>
    modal.confirm({
      title: 'Chốt kế hoạch nhu cầu?',
      content: 'Số liệu trên phiếu dùng để tổng hợp nhu cầu gộp của các công ty; sau khi chốt không sửa được (sửa bằng phiếu điều chỉnh).',
      okText: 'Chốt',
      cancelText: 'Không',
      onOk: async () => {
        const saved = await persist()
        if (!saved) return
        if (isNew) navigate(`${DEMAND_PLAN_PATH}/${saved.id}`, { replace: true })
        try {
          const done = await mutations.confirm.mutateAsync(saved.id)
          message.success(`Đã chốt phiếu ${done.no}.`)
        } catch (e) {
          fail(e, 'Không chốt được.')
        }
      },
    })

  const adjust = async () => {
    if (!doc) return
    setError(null)
    try {
      const created = await mutations.adjust.mutateAsync(doc.id)
      message.success(`Đã lập phiếu điều chỉnh ${created.no}.`)
      open(created.id)
    } catch (e) {
      fail(e, 'Không lập được phiếu điều chỉnh.')
    }
  }

  const cancel = () => {
    if (!doc) return
    let reason = ''
    modal.confirm({
      title: `Hủy phiếu ${doc.no}?`,
      content: (
        <>
          <p>Phiếu hủy thì số liệu không còn tính vào tổng hợp nhu cầu.</p>
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

  /** Ô SL một tháng. */
  const monthColumn = (m: number): LineColumn => ({
    key: `m${m + 1}`, title: `T${m + 1}`, width: 62, align: 'right',
    render: (field) => (
      <Form.Item name={[field.name, 'months', m]} noStyle rules={[{ type: 'number', min: 0, message: 'Số lượng không được âm.' }]}>
        <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0}
          aria-label={`Tháng ${m + 1} dòng ${field.name + 1}`} />
      </Form.Item>
    ),
  })

  /** Ô tổng quý (chỉ đọc) = tổng 3 tháng của quý. */
  const quarterColumn = (q: number): LineColumn => ({
    key: `q${q + 1}`, title: `Quý ${QUARTER_NAMES[q]}`, width: 72, align: 'right',
    render: (field) => (
      <span className="line-grid-readonly">{formatQuantity(monthsOf(lines[field.name]).slice(q * 3, q * 3 + 3).reduce((a, b) => a + b, 0))}</span>
    ),
  })

  const periodColumns = [0, 1, 2, 3].flatMap((q) => [monthColumn(q * 3), monthColumn(q * 3 + 1), monthColumn(q * 3 + 2), quarterColumn(q)])

  const columns: LineColumn[] = [
    {
      key: 'item', title: 'Mặt hàng', required: true, width: 220,
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
      key: 'uom', title: 'ĐVT', required: true, width: 60,
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
    ...periodColumns,
    {
      key: 'total', title: 'Cả năm', width: 96, align: 'right',
      render: (field) => <strong className="line-grid-readonly">{formatQuantity(monthsOf(lines[field.name]).reduce((a, b) => a + b, 0))}</strong>,
    },
    {
      key: 'note', title: 'Ghi chú', width: 160,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle rules={[{ max: 500 }]}>
          <Input variant="borderless" aria-label={`Ghi chú dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? demandPlanStatusLabels[doc.status] : '', tone: doc ? demandPlanStatusTone[doc.status] : ('neutral' as const) }

  const actions = (
    <>
      {!readOnly && (
        <>
          <Button type="primary" icon={<SaveOutlined />} onClick={save} loading={mutations.create.isPending || mutations.update.isPending}>
            Lưu nháp
          </Button>
          <Button icon={<CheckOutlined />} onClick={confirm} loading={mutations.confirm.isPending}>
            Chốt
          </Button>
        </>
      )}
      {doc?.actions.canAdjust && (
        <Button icon={<EditOutlined />} onClick={() => void adjust()} loading={mutations.adjust.isPending}>
          Điều chỉnh
        </Button>
      )}
      {doc?.actions.canCancel && (
        <Button danger icon={<StopOutlined />} onClick={cancel} loading={mutations.cancel.isPending}>
          Hủy phiếu
        </Button>
      )}
      <Button type="text" icon={<CloseOutlined />} onClick={() => navigate(DEMAND_PLAN_PATH)}>
        Đóng
      </Button>
    </>
  )

  return (
    <DocumentForm
      title="Kế hoạch nhu cầu"
      extraTabs={[attachmentTab]}
      number={isNew ? null : (doc?.no ?? '')}
      audit={{ entityType: 'DemandPlan', id: doc?.id }}
      status={status}
      meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
      loading={query.isLoading}
      error={error ?? (query.error ? query.error.message : null)}
      notice={doc && <PlanNotices doc={doc} onOpen={open} />}
      form={form}
      initialValues={{ year: thisYear() + 1, lines: [] }}
      readOnly={readOnly}
      onSave={save}
      onAddLine={() => addRef.current?.()}
      actions={actions}
      header={
        <>
          <Form.Item label="Năm kế hoạch" name="year" rules={[{ required: true, message: 'Chọn năm kế hoạch.' }]}>
            <Select aria-label="Năm kế hoạch" options={years.map((y) => ({ value: y, label: String(y) }))} />
          </Form.Item>
          <Form.Item label="Nhóm hàng" name="itemCategoryId" rules={[{ required: true, message: 'Chọn nhóm hàng.' }]}>
            <Select showSearch={{ optionFilterProp: 'label' }} aria-label="Nhóm hàng"
              options={(categories.data ?? []).map((c) => ({ value: c.id, label: `${c.code} – ${c.name}` }))} />
          </Form.Item>
          <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]}>
            <Input aria-label="Ghi chú" placeholder="Căn cứ lập: định mức, nhu cầu kho, bộ phận sử dụng…" />
          </Form.Item>
        </>
      }
      lines={
        <LineGrid name="lines" auditEntity="DemandPlanLine" columns={columns} readOnly={readOnly} addRef={addRef} scrollX
          newLine={() => ({ id: null, months: Array(12).fill(null) })} />
      }
    />
  )
}
