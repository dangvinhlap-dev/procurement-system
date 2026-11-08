import { CheckOutlined, CloseOutlined, DownloadOutlined, PartitionOutlined, SaveOutlined, StopOutlined, WarningFilled } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Flex, Form, Input, InputNumber, Tooltip } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import {
  expectedQty,
  pooledOrderStatusLabels,
  pooledOrderStatusTone,
  suggestAllocation,
  tolerance,
  usePooledApprovals,
  usePooledContext,
  usePooledOrder,
  usePooledOrderMutations,
  usePooledOrders,
  type CompanyPlan,
  type PooledOrder,
  type PooledOrderListItem,
  type SavePooledOrderInput,
} from '../../api/pooledOrders'
import { purchaseStatusLabels } from '../../api/purchasing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { Select } from '../../classic/Select'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import type { GridColumn } from '../../components/DataGrid/types'
import { DocumentForm, DocumentTotal } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { useDocumentFormSync } from '../../components/DocumentForm/useDocumentFormSync'
import { ListPage } from '../../components/ListPage/ListPage'
import { DATE_INPUT_FORMATS, formatDate, formatDateTime, formatMoney, formatQuantity, vnNumberInput } from '../../utils/format'

/** Đường dẫn danh sách phiếu đặt hàng gộp (màn hình DHG). */
export const POOLED_ORDER_PATH = '/mua-hang/dat-hang-gop'

const listColumns: GridColumn<PooledOrderListItem>[] = [
  { key: 'no', title: 'Số phiếu', width: 150, value: (r) => r.no },
  { key: 'date', title: 'Ngày đặt', width: 100, type: 'date', value: (r) => r.orderDate },
  { key: 'gth', title: 'Phiếu duyệt tháng', width: 150, value: (r) => r.monthlyApprovalNo, groupable: true },
  { key: 'supplier', title: 'Nhà cung cấp', width: 240, value: (r) => `${r.supplierCode} – ${r.supplierName}`, groupable: true },
  { key: 'lines', title: 'Số mặt hàng', width: 90, type: 'quantity', value: (r) => r.lineCount },
  { key: 'amount', title: 'Thành tiền', width: 130, type: 'money', value: (r) => r.totalAmount },
  { key: 'orders', title: 'Số PO', width: 70, type: 'quantity', value: (r) => r.orderCount },
  {
    key: 'status', title: 'Trạng thái', width: 110, type: 'status', groupable: true,
    value: (r) => pooledOrderStatusLabels[r.status], tone: (r) => pooledOrderStatusTone[r.status],
  },
  { key: 'creator', title: 'Người lập', width: 150, value: (r) => r.createdByName, groupable: true },
]

/**
 * Màn hình Mua hàng → Đặt hàng gộp (DHG – design.md mục 18.25, lát G7): Phòng cung ứng chung đặt hàng cho các công ty theo NCC và giá Ban đã
 * duyệt trong tháng, phân bổ trong biên độ, sinh mỗi pháp nhân một PO.
 */
export function PooledOrderListPage() {
  const navigate = useNavigate()
  const { hasPermission, workingCompany } = useAuth()
  const list = usePooledOrders()
  return (
    <ListPage
      title="Đặt hàng gộp"
      exportFileName="dat-hang-gop"
      rows={list.data ?? []}
      columns={listColumns}
      rowKey={(r) => r.id}
      auditEntity="PooledOrder"
      loading={list.isLoading}
      // Phiếu thuộc tổng công ty – Phòng cung ứng chung đặt cho các công ty.
      canCreate={hasPermission(Permission.PooledOrderManage) && !!workingCompany?.isHeadOffice}
      onCreate={() => navigate(`${POOLED_ORDER_PATH}/moi`)}
      onOpenRow={(r) => navigate(`${POOLED_ORDER_PATH}/${r.id}`)}
      isMarked={(r) => r.status === 'Draft'}
    />
  )
}

interface LineValues {
  id: string | null
  monthlyApprovalLineId?: string
  /** Tổng SL đặt của dòng (= tổng phân bổ). */
  total?: number | null
  /** SL phân bổ theo Id công ty. */
  alloc?: Record<string, number | null | undefined>
  allocationReason?: string
  note?: string
}

interface CompanyValues {
  companyId: string
  warehouseId?: string
  departmentId?: string | null
}

interface FormValues {
  orderDate: Dayjs
  monthlyApprovalId?: string
  supplierId?: string
  expectedDate?: Dayjs | null
  note?: string
  companies: CompanyValues[]
  lines: LineValues[]
}

/** Thông tin dòng GTH dùng để hiển thị: mã, tên, ĐVT, giá, kế hoạch theo công ty. */
interface SourceInfo {
  itemCode: string
  itemName: string
  uomCode: string
  unitPrice: number
  isBackup: boolean
  plans: CompanyPlan[]
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/** Tổng phân bổ của dòng (4 số lẻ). */
const sumAlloc = (alloc: LineValues['alloc']) => Math.round(Object.values(alloc ?? {}).reduce<number>((s, v) => s + (v ?? 0), 0) * 10000) / 10000

/**
 * Thông báo đầu phiếu: PO đã tạo, đã hủy, lý do chỉ xem, biên độ phân bổ.
 * @param doc Phiếu đã tải (null khi lập mới).
 * @param margin % biên độ đang áp.
 * @param editable Phiếu đang sửa được.
 */
function PooledNotices({ doc, margin, editable }: { doc: PooledOrder | undefined; margin: number; editable: boolean }) {
  const created = doc?.companies.filter((c) => c.purchaseOrderNo) ?? []
  const notices = [
    doc?.status === 'Ordered' && (
      <Alert key="ordered" type="success" showIcon
        title={`Đã tạo PO ${doc.orderedAt ? formatDateTime(doc.orderedAt) : ''}${doc.orderedByName ? ` – ${doc.orderedByName}` : ''}: ${created
          .map((c) => `${c.companyCode} – ${c.purchaseOrderNo} (${c.purchaseOrderStatus ? purchaseStatusLabels[c.purchaseOrderStatus] : ''})`)
          .join('; ')}. Mở, gửi duyệt từng PO khi chuyển sang công ty đó (PO sai thì hủy PO ở công ty của nó).`} />
    ),
    doc?.status === 'Cancelled' && <Alert key="cancel" type="error" showIcon title={`Đã hủy ${doc.cancelledOn ? formatDate(doc.cancelledOn) : ''}: ${doc.cancelReason ?? ''}`} />,
    doc?.actions.readOnlyReason && <Alert key="readonly" type="info" showIcon title={doc.actions.readOnlyReason} />,
    editable && (
      <Alert key="margin" type="info" showIcon
        title={`Biên độ phân bổ ${margin.toLocaleString('vi-VN')}%: mỗi công ty được lệch SL đề xuất theo tỷ lệ kế hoạch nhu cầu tháng tối đa max(tổng dòng × ${margin.toLocaleString('vi-VN')}%, 1 ĐVT). Ô có dấu cảnh báo đang vượt – không tạo PO được. Mặt hàng chưa có kế hoạch ở công ty nào thì phải ghi lý do phân bổ.`} />
    ),
  ].filter(Boolean)
  return notices.length ? <Flex vertical gap={8}>{notices}</Flex> : null
}

/**
 * Trang phiếu đặt hàng gộp trên khuôn DocumentForm: ngày đặt, phiếu duyệt NCC và giá tháng (GTH), NCC (chính/dự phòng của GTH), ngày giao,
 * ghi chú; lưới dòng: mặt hàng của GTH, giá Ban duyệt, tổng SL (tự chia theo kế hoạch), SL từng công ty; tab Công ty nhận: kho, bộ phận, PO
 * đã sinh. "Tạo PO" sinh mỗi công ty một PO nháp (design.md mục 18.25, G7).
 */
export function PooledOrderPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const { hasPermission, workingCompany } = useAuth()
  const [form] = Form.useForm<FormValues>()
  const query = usePooledOrder(isNew ? null : id)
  const mutations = usePooledOrderMutations()
  const [error, setError] = useState<string | null>(null)
  const addRef = useRef<(() => void) | null>(null)
  const doc = query.data
  const canManage = hasPermission(Permission.PooledOrderManage) && !!workingCompany?.isHeadOffice
  const readOnly = isNew ? !canManage : !doc?.actions.canEdit
  // Đính kèm xác nhận của NCC, bảng phân bổ – quyền theo quyền lập phiếu (design.md mục 18.4).
  const attachmentTab = useAttachmentTab('PooledOrder', doc?.id, hasPermission(Permission.PooledOrderManage))

  const orderDate = Form.useWatch('orderDate', form) as Dayjs | undefined
  const approvalId = Form.useWatch('monthlyApprovalId', form) as string | undefined
  const supplierId = Form.useWatch('supplierId', form) as string | undefined
  const watchedLines = Form.useWatch('lines', form) as LineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const watchedCompanies = Form.useWatch('companies', form) as CompanyValues[] | undefined
  const companyRows = useMemo(() => watchedCompanies ?? [], [watchedCompanies])
  const approvals = usePooledApprovals(!readOnly && orderDate ? orderDate.format('YYYY-MM-DD') : null)
  const context = usePooledContext(readOnly ? undefined : approvalId, readOnly ? undefined : supplierId)
  const approval = approvals.data?.find((a) => a.id === approvalId)
  const margin = context.data?.marginPercent ?? doc?.marginPercent ?? 10

  // Công ty: theo ngữ cảnh khi đang sửa, theo phiếu đã lưu khi chỉ xem.
  const companies = useMemo(
    () => context.data?.companies.map((c) => ({ id: c.companyId, code: c.code, name: c.name }))
      ?? doc?.companies.map((c) => ({ id: c.companyId, code: c.companyCode, name: c.companyName }))
      ?? [],
    [context.data, doc],
  )

  // Dòng GTH theo Id: từ ngữ cảnh (đang sửa) hoặc từ phiếu đã lưu (chỉ xem – kế hoạch lấy theo dòng phân bổ).
  const sources = useMemo(() => {
    const map = new Map<string, SourceInfo>()
    for (const l of doc?.lines ?? [])
      map.set(l.monthlyApprovalLineId, {
        itemCode: l.itemCode, itemName: l.itemName, uomCode: l.uomCode, unitPrice: l.unitPrice, isBackup: false,
        plans: l.allocations.map((a) => ({ companyId: a.companyId, plan: a.planQty })),
      })
    for (const l of context.data?.lines ?? [])
      map.set(l.monthlyApprovalLineId, {
        itemCode: l.itemCode, itemName: l.itemName, uomCode: l.uomCode, unitPrice: l.unitPrice, isBackup: l.isBackup,
        plans: l.plans.map((p) => ({ companyId: p.companyId, plan: p.planQty })),
      })
    return map
  }, [doc, context.data])

  const { loadSaved, targetId } = useDocumentFormSync(form, id, doc, (doc) => ({
    orderDate: dayjs(doc.orderDate),
    monthlyApprovalId: doc.monthlyApprovalId,
    supplierId: doc.supplierId,
    expectedDate: doc.expectedDate ? dayjs(doc.expectedDate) : null,
    note: doc.note ?? '',
    companies: doc.companies.map((c) => ({ companyId: c.companyId, warehouseId: c.warehouseId, departmentId: c.departmentId })),
    lines: doc.lines.map((l) => ({
      id: l.id, monthlyApprovalLineId: l.monthlyApprovalLineId, total: l.totalQty,
      alloc: Object.fromEntries(l.allocations.map((a) => [a.companyId, a.qty])), allocationReason: l.allocationReason ?? '', note: l.note ?? '',
    })),
  }))

  // Ngữ cảnh tải xong: dựng danh sách công ty nhận theo các công ty áp dụng GTH – giữ kho/bộ phận đã chọn, công ty mới mặc định kho Main.
  useEffect(() => {
    if (readOnly || !context.data) return
    const current = (form.getFieldValue('companies') as CompanyValues[] | undefined) ?? []
    const next = context.data.companies.map((c) => {
      const kept = current.find((x) => x.companyId === c.companyId)
      return kept ?? { companyId: c.companyId, warehouseId: c.warehouses[0]?.id, departmentId: null }
    })
    if (JSON.stringify(next) !== JSON.stringify(current)) form.setFieldValue('companies', next)
  }, [context.data, readOnly, form])

  const fail = (e: unknown, fallback: string) => setError(e instanceof Error ? e.message : fallback)

  /** Chia tổng SL của dòng theo kế hoạch; không có kế hoạch thì giữ SL đang nhập. */
  const distribute = (index: number, total: number | null | undefined) => {
    const line = (form.getFieldValue(['lines', index]) as LineValues | undefined) ?? { id: null }
    const source = line.monthlyApprovalLineId ? sources.get(line.monthlyApprovalLineId) : undefined
    const suggestion = suggestAllocation(total ?? 0, source?.plans ?? [])
    if (Object.keys(suggestion).length === 0) return
    form.setFieldValue(['lines', index, 'alloc'], Object.fromEntries(companies.map((c) => [c.id, suggestion[c.id] ?? 0])))
  }

  /** Chia lại mọi dòng theo tổng đang có (nút "Phân bổ theo kế hoạch"). */
  const distributeAll = () => {
    const all = (form.getFieldValue('lines') as LineValues[] | undefined) ?? []
    all.forEach((l, i) => distribute(i, l?.total))
    message.info('Đã chia SL các dòng theo tỷ lệ kế hoạch nhu cầu tháng (dòng chưa có kế hoạch giữ nguyên).')
  }

  /** Thêm mọi mặt hàng của phiếu tháng đặt được với NCC mà phiếu chưa có. */
  const addAllItems = () => {
    const all = (form.getFieldValue('lines') as LineValues[] | undefined) ?? []
    const used = new Set(all.map((l) => l?.monthlyApprovalLineId))
    const added = (context.data?.lines ?? []).filter((l) => !used.has(l.monthlyApprovalLineId))
      .map((l) => ({ id: null, monthlyApprovalLineId: l.monthlyApprovalLineId, total: null, alloc: {} }))
    form.setFieldValue('lines', [...all.filter((l) => l?.monthlyApprovalLineId), ...added])
    if (added.length === 0) message.info('Phiếu đã có mọi mặt hàng của phiếu tháng đặt được với NCC này.')
  }

  const persist = async (): Promise<PooledOrder | null> => {
    try {
      await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu, dòng hàng và tab Công ty nhận.')
      return null
    }

    // Lấy cả ô của tab chưa mở (getFieldsValue(true)) – form chung cho cả phiếu.
    const values = form.getFieldsValue(true) as FormValues
    setError(null)
    const input: SavePooledOrderInput = {
      orderDate: values.orderDate.format('YYYY-MM-DD'),
      monthlyApprovalId: values.monthlyApprovalId!,
      supplierId: values.supplierId!,
      expectedDate: values.expectedDate ? values.expectedDate.format('YYYY-MM-DD') : null,
      note: orNull(values.note),
      companies: (values.companies ?? []).map((c) => ({ companyId: c.companyId, warehouseId: c.warehouseId!, departmentId: c.departmentId ?? null })),
      lines: (values.lines ?? []).map((l) => ({
        id: l.id ?? null,
        monthlyApprovalLineId: l.monthlyApprovalLineId!,
        allocations: companies.map((c) => ({ companyId: c.id, qty: l.alloc?.[c.id] ?? 0 })),
        allocationReason: orNull(l.allocationReason),
        note: orNull(l.note),
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
    if (isNew) navigate(`${POOLED_ORDER_PATH}/${saved.id}`, { replace: true })
  }

  // Tạo PO: lưu trước rồi sinh PO – từ đây phiếu không sửa được.
  const createOrders = () =>
    modal.confirm({
      title: 'Tạo PO cho các công ty?',
      content: 'Mỗi công ty có số lượng được tạo một PO nháp tại chính công ty đó, giá theo phiếu duyệt tháng. Sau khi tạo, phiếu đặt gộp không sửa được; PO gửi duyệt ở từng công ty.',
      okText: 'Tạo PO',
      cancelText: 'Không',
      onOk: async () => {
        const saved = await persist()
        if (!saved) return
        if (isNew) navigate(`${POOLED_ORDER_PATH}/${saved.id}`, { replace: true })
        try {
          const done = await mutations.createOrders.mutateAsync(saved.id)
          message.success(`Đã tạo ${done.companies.filter((c) => c.purchaseOrderNo).length} PO từ phiếu ${done.no}.`)
        } catch (e) {
          fail(e, 'Không tạo được PO.')
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
          <p>Phiếu hủy thì không tạo PO được nữa.</p>
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

  const readonlyCell = (text: string) => <span className="line-grid-readonly">{text}</span>
  const sourceOf = (index: number) => {
    const key = lines[index]?.monthlyApprovalLineId
    return key ? sources.get(key) : undefined
  }

  const companyColumns: LineColumn[] = companies.map((c) => ({
    key: `alloc-${c.id}`, title: `SL ${c.code}`, width: 110, align: 'right',
    render: (field) => {
      const line = lines[field.name]
      const plans = sourceOf(field.name)?.plans ?? []
      const total = sumAlloc(line?.alloc)
      const hasPlan = plans.some((p) => p.plan > 0)
      const expected = expectedQty(total, c.id, plans)
      const tol = tolerance(total, margin)
      const out = hasPlan && total > 0 && Math.abs((line?.alloc?.[c.id] ?? 0) - expected) > tol
      return (
        <Flex align="center" gap={2}>
          {out && (
            <Tooltip title={`Vượt biên độ: đề xuất theo kế hoạch ${formatQuantity(Math.round(expected * 10000) / 10000)} ± ${formatQuantity(tol)}`}>
              <WarningFilled className="line-grid-alert-icon" aria-label={`Vượt biên độ ${c.code} dòng ${field.name + 1}`} />
            </Tooltip>
          )}
          <Form.Item name={[field.name, 'alloc', c.id]} noStyle>
            <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} aria-label={`SL ${c.code} dòng ${field.name + 1}`}
              onChange={() => {
                // Tổng dòng luôn bằng tổng phân bổ.
                const alloc = form.getFieldValue(['lines', field.name, 'alloc']) as LineValues['alloc']
                form.setFieldValue(['lines', field.name, 'total'], sumAlloc(alloc))
              }} />
          </Form.Item>
        </Flex>
      )
    },
  }))

  const usedSourceIds = new Set(lines.map((l) => l?.monthlyApprovalLineId).filter(Boolean))
  const columns: LineColumn[] = [
    {
      key: 'item', title: 'Mặt hàng', required: true, width: 260,
      render: (field) => {
        const current = lines[field.name]?.monthlyApprovalLineId
        const s = sourceOf(field.name)
        const options = (context.data?.lines ?? [])
          .filter((l) => l.monthlyApprovalLineId === current || !usedSourceIds.has(l.monthlyApprovalLineId))
          .map((l) => ({ value: l.monthlyApprovalLineId, label: `${l.itemCode} – ${l.itemName}` }))
        if (current && s && !options.some((o) => o.value === current)) options.push({ value: current, label: `${s.itemCode} – ${s.itemName}` })
        return (
          <>
            <Form.Item name={[field.name, 'id']} hidden>
              <Input />
            </Form.Item>
            <Form.Item name={[field.name, 'monthlyApprovalLineId']} noStyle rules={[{ required: true, message: 'Chọn mặt hàng.' }]}>
              <Select variant="borderless" style={{ width: '100%' }} aria-label={`Mặt hàng dòng ${field.name + 1}`} placeholder="Mặt hàng của phiếu tháng"
                showSearch={{ optionFilterProp: 'label' }} options={options} popupMatchSelectWidth={420}
                onChange={() => form.setFieldValue(['lines', field.name, 'alloc'], {})} />
            </Form.Item>
          </>
        )
      },
    },
    { key: 'uom', title: 'ĐVT', width: 60, render: (field) => readonlyCell(sourceOf(field.name)?.uomCode ?? '') },
    {
      key: 'price', title: 'Đơn giá duyệt', width: 115, align: 'right',
      render: (field) => {
        const s = sourceOf(field.name)
        return readonlyCell(s ? `${formatMoney(s.unitPrice)}${s.isBackup ? ' (dự phòng)' : ''}` : '')
      },
    },
    { key: 'currency', title: 'Tiền tệ', width: 55, render: () => readonlyCell('VND') },
    {
      key: 'plan', title: 'KH tháng', width: 90, align: 'right',
      render: (field) => {
        const plans = sourceOf(field.name)?.plans ?? []
        const total = plans.reduce((s, p) => s + p.plan, 0)
        return (
          <Tooltip title={companies.map((c) => `${c.code}: ${formatQuantity(plans.find((p) => p.companyId === c.id)?.plan ?? 0)}`).join(' · ')}>
            {readonlyCell(total ? formatQuantity(total) : 'Chưa có')}
          </Tooltip>
        )
      },
    },
    {
      key: 'total', title: 'Tổng SL', required: true, width: 100, align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'total']} noStyle>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} aria-label={`Tổng SL dòng ${field.name + 1}`}
            onChange={(v) => distribute(field.name, typeof v === 'number' ? v : null)} />
        </Form.Item>
      ),
    },
    ...companyColumns,
    {
      key: 'amount', title: 'Thành tiền', width: 120, align: 'right',
      render: (field) => readonlyCell(formatMoney(sumAlloc(lines[field.name]?.alloc) * (sourceOf(field.name)?.unitPrice ?? 0))),
    },
    {
      key: 'reason', title: 'Lý do phân bổ', width: 180,
      render: (field) => {
        const noPlan = !(sourceOf(field.name)?.plans ?? []).some((p) => p.plan > 0)
        return (
          <Form.Item name={[field.name, 'allocationReason']} noStyle rules={[{ max: 500 }]}>
            <Input variant="borderless" aria-label={`Lý do phân bổ dòng ${field.name + 1}`}
              placeholder={!readOnly && noPlan && lines[field.name]?.monthlyApprovalLineId ? 'Bắt buộc – chưa có kế hoạch' : undefined} />
          </Form.Item>
        )
      },
    },
    {
      key: 'note', title: 'Ghi chú (sang PO)', width: 180,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle rules={[{ max: 500 }]}>
          <Input variant="borderless" aria-label={`Ghi chú dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const companyTabColumns: LineColumn[] = [
    {
      key: 'company', title: 'Công ty', width: 240,
      render: (field) => {
        const c = companies.find((x) => x.id === companyRows[field.name]?.companyId)
        return (
          <>
            {/* Gắn Id công ty vào form để Form.useWatch thấy (useWatch chỉ trả ô đã đăng ký). */}
            <Form.Item name={[field.name, 'companyId']} hidden>
              <Input />
            </Form.Item>
            {readonlyCell(c ? `${c.code} – ${c.name}` : '')}
          </>
        )
      },
    },
    {
      key: 'warehouse', title: 'Kho nhận', required: true, width: 220,
      render: (field) => {
        const cid = companyRows[field.name]?.companyId
        const saved = doc?.companies.find((x) => x.companyId === cid)
        const options = context.data?.companies.find((x) => x.companyId === cid)?.warehouses
          .map((w) => ({ value: w.id, label: `${w.code} – ${w.name}${w.isMain ? ' (kho tổng)' : ''}` }))
          ?? (saved ? [{ value: saved.warehouseId, label: saved.warehouseCode ?? '' }] : [])
        return (
          <Form.Item name={[field.name, 'warehouseId']} noStyle rules={[{ required: true, message: 'Chọn kho nhận.' }]}>
            <Select variant="borderless" style={{ width: '100%' }} aria-label={`Kho nhận dòng ${field.name + 1}`} options={options} />
          </Form.Item>
        )
      },
    },
    {
      key: 'department', title: 'Bộ phận đặt hàng', width: 220,
      render: (field) => {
        const cid = companyRows[field.name]?.companyId
        const saved = doc?.companies.find((x) => x.companyId === cid)
        const options = context.data?.companies.find((x) => x.companyId === cid)?.departments.map((d) => ({ value: d.id, label: `${d.code} – ${d.name}` }))
          ?? (saved?.departmentId ? [{ value: saved.departmentId, label: saved.departmentName ?? '' }] : [])
        return (
          <Form.Item name={[field.name, 'departmentId']} noStyle>
            <Select variant="borderless" style={{ width: '100%' }} aria-label={`Bộ phận dòng ${field.name + 1}`} allowClear options={options} />
          </Form.Item>
        )
      },
    },
    {
      key: 'amount', title: 'Thành tiền', width: 120, align: 'right',
      render: (field) => {
        const cid = companyRows[field.name]?.companyId
        const amount = lines.reduce((s, l) => s + (l?.alloc?.[cid ?? ''] ?? 0) * ((l?.monthlyApprovalLineId && sources.get(l.monthlyApprovalLineId)?.unitPrice) || 0), 0)
        return readonlyCell(formatMoney(amount))
      },
    },
    {
      key: 'po', title: 'PO đã tạo', width: 200,
      render: (field) => {
        const saved = doc?.companies.find((x) => x.companyId === companyRows[field.name]?.companyId)
        return readonlyCell(saved?.purchaseOrderNo
          ? `${saved.purchaseOrderNo} (${saved.purchaseOrderStatus ? purchaseStatusLabels[saved.purchaseOrderStatus] : ''})`
          : '')
      },
    },
  ]

  const totalAmount = lines.reduce((s, l) => s + sumAlloc(l?.alloc) * ((l?.monthlyApprovalLineId && sources.get(l.monthlyApprovalLineId)?.unitPrice) || 0), 0)
  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? pooledOrderStatusLabels[doc.status] : '', tone: doc ? pooledOrderStatusTone[doc.status] : ('neutral' as const) }

  const approvalOptions = (approvals.data ?? []).map((a) => ({ value: a.id, label: `${a.no} – ${a.categoryCode} ${a.categoryName}` }))
  if (doc && !approvalOptions.some((o) => o.value === doc.monthlyApprovalId))
    approvalOptions.push({ value: doc.monthlyApprovalId, label: `${doc.monthlyApprovalNo} – ${doc.categoryCode} ${doc.categoryName}` })
  const supplierOptions = (approval?.suppliers ?? []).map((s) => ({ value: s.supplierId, label: `${s.code} – ${s.name}` }))
  if (doc && !supplierOptions.some((o) => o.value === doc.supplierId)) supplierOptions.push({ value: doc.supplierId, label: `${doc.supplierCode} – ${doc.supplierName}` })

  const actions = (
    <>
      {!readOnly && (
        <>
          <Button type="primary" icon={<SaveOutlined />} onClick={save} loading={mutations.create.isPending || mutations.update.isPending}>
            Lưu nháp
          </Button>
          <Button icon={<DownloadOutlined />} onClick={addAllItems} disabled={!context.data}>
            Lấy mặt hàng từ phiếu tháng
          </Button>
          <Button icon={<PartitionOutlined />} onClick={distributeAll} disabled={!context.data}>
            Phân bổ theo kế hoạch
          </Button>
          <Button icon={<CheckOutlined />} onClick={createOrders} loading={mutations.createOrders.isPending}>
            Tạo PO
          </Button>
        </>
      )}
      {doc?.actions.canCancel && (
        <Button danger icon={<StopOutlined />} onClick={cancel} loading={mutations.cancel.isPending}>
          Hủy phiếu
        </Button>
      )}
      <Button type="text" icon={<CloseOutlined />} onClick={() => navigate(POOLED_ORDER_PATH)}>
        Đóng
      </Button>
    </>
  )

  return (
    <DocumentForm
      title="Đặt hàng gộp"
      extraTabs={[
        {
          key: 'companies', label: 'Công ty nhận',
          children: <LineGrid name="companies" auditEntity="PooledOrderCompany" columns={companyTabColumns} readOnly={readOnly} fixedRows newLine={() => ({})} />,
        },
        attachmentTab,
      ]}
      number={isNew ? null : (doc?.no ?? '')}
      audit={{ entityType: 'PooledOrder', id: doc?.id }}
      status={status}
      meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
      loading={query.isLoading}
      error={error ?? (query.error ? query.error.message : null) ?? (context.error ? context.error.message : null)}
      notice={<PooledNotices doc={doc} margin={margin} editable={!readOnly} />}
      form={form}
      initialValues={{ orderDate: dayjs(), companies: [], lines: [] }}
      readOnly={readOnly}
      onSave={save}
      onAddLine={() => addRef.current?.()}
      actions={actions}
      footer={<DocumentTotal label="Tổng tiền hàng (trước VAT)" value={formatMoney(totalAmount)} strong />}
      header={
        <>
          <Form.Item label="Ngày đặt" name="orderDate" rules={[{ required: true, message: 'Chọn ngày đặt.' }]}>
            <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} aria-label="Ngày đặt"
              onChange={() => form.setFieldsValue({ monthlyApprovalId: undefined, supplierId: undefined, lines: [], companies: [] })} />
          </Form.Item>
          <Form.Item label="Phiếu duyệt NCC và giá tháng" name="monthlyApprovalId" rules={[{ required: true, message: 'Chọn phiếu duyệt tháng.' }]}
            extra={!readOnly && approvals.data?.length === 0 ? 'Tháng này chưa có phiếu duyệt NCC và giá đã duyệt.' : undefined}>
            <Select aria-label="Phiếu duyệt tháng" options={approvalOptions}
              onChange={() => form.setFieldsValue({ supplierId: undefined, lines: [], companies: [] })} />
          </Form.Item>
          <Form.Item label="Nhà cung cấp" name="supplierId" rules={[{ required: true, message: 'Chọn NCC.' }]}
            extra="NCC chính hoặc dự phòng trên phiếu duyệt tháng.">
            <Select aria-label="Nhà cung cấp" options={supplierOptions} />
          </Form.Item>
          <Form.Item label="Ngày giao dự kiến" name="expectedDate">
            <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} aria-label="Ngày giao dự kiến"
              disabledDate={(d) => !!orderDate && d.isBefore(orderDate, 'day')} />
          </Form.Item>
          <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]}>
            <Input aria-label="Ghi chú" placeholder="Chép sang ghi chú các PO" />
          </Form.Item>
        </>
      }
      lines={
        <LineGrid name="lines" auditEntity="PooledOrderLine" columns={columns} readOnly={readOnly} addRef={addRef} scrollX
          newLine={() => ({ id: null, total: null, alloc: {} })} />
      }
    />
  )
}
