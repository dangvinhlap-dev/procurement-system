import { CheckOutlined, CloseCircleOutlined, CloseOutlined, FileAddOutlined, RollbackOutlined, SaveOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Flex, Form, Input, InputNumber, Select, Table, Typography } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router'
import {
  amendmentStatusLabels,
  contractStatusLabels,
  contractImportSource,
  contractStatusTone,
  useContract,
  useContractMutations,
  useContracts,
  type Contract,
  type ContractAmendmentListItem,
  type ContractListItem,
  type SaveContractInput,
} from '../../api/contracts'
import { useItemLookup } from '../../api/inventoryDocuments'
import { useSuppliers } from '../../api/masterData'
import { useDepartments } from '../../api/organization'
import { usePriceList, usePriceLists } from '../../api/pricing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { DocumentForm, DocumentTotal } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { ListPage } from '../../components/ListPage/ListPage'
import { renderRequiredMark } from '../../components/RequiredMark/requiredMark'
import { formatDate, formatDateTime, formatMoney, vnNumberInput, DATE_INPUT_FORMATS } from '../../utils/format'
import { ApprovalHistory } from './ApprovalHistory'
import { ContractAmendmentDrawer } from './ContractAmendmentDrawer'
import { contractTermFields as termFields } from './contractTerms'
import { confirmReject } from './useApprovalCommands'

const LIST_PATH = '/mua-hang/hop-dong-khung'

const listColumns: GridColumn<ContractListItem>[] = [
  { key: 'no', title: 'Số hợp đồng', width: 150, value: (r) => r.no },
  { key: 'contractNo', title: 'Số HĐ giấy', width: 120, value: (r) => r.contractNo },
  { key: 'title', title: 'Tên hợp đồng', width: 240, value: (r) => r.title },
  { key: 'supplier', title: 'Nhà cung cấp', width: 230, value: (r) => `${r.supplierCode} – ${r.supplierName}`, groupable: true },
  { key: 'from', title: 'Hiệu lực từ', width: 95, type: 'date', value: (r) => r.validFrom },
  { key: 'to', title: 'Đến', width: 95, type: 'date', value: (r) => r.validTo },
  { key: 'amount', title: 'Giá trị dự kiến', width: 130, type: 'money', value: (r) => r.expectedAmount },
  { key: 'creator', title: 'Người lập', width: 140, value: (r) => r.createdByName, groupable: true },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 130,
    type: 'status',
    // Sắp hết hiệu lực (còn ≤ 30 ngày): nhãn vàng có chữ, không chỉ đổi màu.
    value: (r) => (r.expiringSoon ? 'Sắp hết hiệu lực' : contractStatusLabels[r.status]),
    tone: (r) => (r.expiringSoon ? 'warning' : contractStatusTone[r.status]),
    groupable: true,
  },
]

/** Màn hình Mua hàng → Hợp đồng nguyên tắc (CTR – lát B4a, design.md mục 18.7): hợp đồng của công ty đang làm việc. */
export function ContractListPage() {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const contracts = useContracts()
  return (
    <ListPage
      title="Hợp đồng nguyên tắc"
      exportFileName="hop-dong-nguyen-tac"
      rows={contracts.data ?? []}
      columns={listColumns}
      rowKey={(r) => r.id}
      loading={contracts.isLoading}
      canCreate={hasPermission(Permission.ContractManage)}
      onCreate={() => navigate(`${LIST_PATH}/moi`)}
      onOpenRow={(r) => navigate(`${LIST_PATH}/${r.id}`)}
      // Nhập hợp đồng đang thực hiện khi chuyển giao (lát B4c) – thành hợp đồng nháp, quyền như lập hợp đồng.
      excelImport={{ source: contractImportSource, title: 'hợp đồng nguyên tắc', canImport: hasPermission(Permission.ContractManage) }}
    />
  )
}

interface LineValues {
  id: string | null
  itemId?: string
  uomId?: string
  expectedQty?: number | null
  minCommitQty?: number | null
  note?: string
}

interface FormValues {
  supplierId?: string
  priceListId?: string
  contractNo?: string
  title?: string
  signDate?: Dayjs | null
  validFrom: Dayjs
  validTo?: Dayjs
  creditLimit?: number | null
  note?: string
  priceAdjustmentTerms?: string
  paymentTerms?: string
  deliveryTerms?: string
  discountTerms?: string
  sponsorshipTerms?: string
  penaltyTerms?: string
  otherTerms?: string
  departmentIds: string[]
  lines: LineValues[]
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Thông báo đầu hợp đồng: lý do bị từ chối, sắp hết hiệu lực, đã chấm dứt, bảng giá chưa hiệu lực, lý do chỉ xem.
 * @param doc Hợp đồng đã tải.
 */
function ContractNotices({ doc }: { doc: Contract }) {
  const notices = [
    doc.rejectReason && doc.status === 'Draft' && <Alert key="reject" type="warning" showIcon title={`Bị từ chối: ${doc.rejectReason}`} />,
    doc.expiringSoon && (
      <Alert
        key="expiry"
        type="warning"
        showIcon
        title={doc.daysToExpiry === 0 ? 'Hợp đồng hết hiệu lực hôm nay.' : `Hợp đồng sắp hết hiệu lực – còn ${doc.daysToExpiry} ngày.`}
        description="Đàm phán gia hạn hoặc lập hợp đồng mới để tiếp tục mua theo giá đã duyệt."
      />
    ),
    doc.status === 'Terminated' && (
      <Alert key="terminated" type="info" showIcon title={`Đã chấm dứt ngày ${formatDate(doc.terminatedOn)}: ${doc.terminationReason ?? ''}`} />
    ),
    doc.priceListStatus !== 'Active' && ['Draft', 'Submitted'].includes(doc.status) && (
      <Alert key="pricelist" type="warning" showIcon title={`Bảng giá ${doc.priceListNo} chưa hiệu lực – phải duyệt bảng giá trước khi duyệt hợp đồng.`} />
    ),
    doc.actions.readOnlyReason && <Alert key="readonly" type="info" showIcon title={doc.actions.readOnlyReason} />,
  ].filter(Boolean)
  return notices.length ? <Flex vertical gap={8}>{notices}</Flex> : null
}

/**
 * Bảng phụ lục của hợp đồng (tab Phụ lục): bấm số phụ lục hoặc bấm đúp dòng để mở ngăn kéo phụ lục.
 * @param rows Phụ lục, mới nhất trước.
 * @param onOpen Mở phụ lục theo Id.
 */
function AmendmentTable({ rows, onOpen }: { rows: ContractAmendmentListItem[]; onOpen: (id: string) => void }) {
  return (
    <Table<ContractAmendmentListItem>
      size="small"
      pagination={false}
      rowKey={(a) => a.id}
      dataSource={rows}
      // Độ rộng cột cố định, nhiều cột thì cuộn ngang (docs/ui-style.md mục 5) – tránh cột chữ dài bị ép thành một từ mỗi dòng.
      tableLayout="fixed"
      scroll={{ x: 1300 }}
      locale={{ emptyText: 'Chưa có phụ lục. Gia hạn, đổi bảng giá hay điều khoản thì bấm "Lập phụ lục" (hợp đồng đang hiệu lực).' }}
      onRow={(a) => ({ onDoubleClick: () => onOpen(a.id) })}
      columns={[
        {
          title: 'Số phụ lục',
          dataIndex: 'no',
          width: 190,
          // Liên kết chữ (không dùng Button): form hợp đồng hiệu lực ở chế độ chỉ đọc sẽ khóa mọi nút bên trong.
          render: (v: string, a) => <Typography.Link onClick={() => onOpen(a.id)}>{v}</Typography.Link>,
        },
        { title: 'Số giấy', dataIndex: 'paperNo', width: 100 },
        { title: 'Ngày ký', dataIndex: 'signDate', width: 95, render: (v: string | null) => formatDate(v) },
        { title: 'Thay đổi', dataIndex: 'changeSummary', width: 340 },
        { title: 'Lý do', dataIndex: 'reason', width: 240 },
        { title: 'Giá trị dự kiến', dataIndex: 'expectedAmount', width: 120, align: 'right',
          // Số tiền phông đơn cách (docs/ui-style.md mục 3).
          render: (v: number) => <span style={{ fontFamily: 'var(--erp-font-mono)' }}>{formatMoney(v)}</span> },
        { title: 'Trạng thái', dataIndex: 'status', width: 125, render: (v: ContractAmendmentListItem['status']) => amendmentStatusLabels[v] },
        { title: 'Người lập', dataIndex: 'createdByName', width: 140 },
      ]}
    />
  )
}

/**
 * Trang hợp đồng nguyên tắc trên khuôn DocumentForm (phương án 2 – mỗi công ty một hợp đồng với NCC, dùng chung bảng giá): đầu hợp đồng,
 * sản lượng dự kiến (giá theo bảng giá), điều khoản, bộ phận được phép đặt. Nháp → Gửi duyệt → Hiệu lực → Chấm dứt.
 */
export function ContractPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<FormValues>()
  const contract = useContract(isNew ? null : id)
  const mutations = useContractMutations()
  const suppliers = useSuppliers({})
  const priceLists = usePriceLists()
  const departments = useDepartments()
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const addRef = useRef<(() => void) | null>(null)
  const doc = contract.data
  // Ngăn kéo phụ lục mở theo ?phu-luc=<Id|moi> – mở thẳng được từ thông báo, Việc cần làm (lát B4b).
  const [searchParams, setSearchParams] = useSearchParams()
  const amendmentId = searchParams.get('phu-luc')
  const openAmendment = (value: string | null) =>
    setSearchParams((params) => {
      if (value) params.set('phu-luc', value)
      else params.delete('phu-luc')
      return params
    }, { replace: true })
  const readOnly = isNew ? !hasPermission(Permission.ContractManage) : !doc?.actions.canEdit
  const attachmentTab = useAttachmentTab('PurchaseContract', doc?.id, hasPermission(Permission.ContractManage))

  const supplierId = Form.useWatch('supplierId', form) as string | undefined
  const priceListId = Form.useWatch('priceListId', form) as string | undefined
  const priceList = usePriceList(priceListId ?? null)
  const watchedLines = Form.useWatch('lines', form) as LineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])

  const supplierCode = (suppliers.data?.items ?? []).find((s) => s.id === supplierId)?.code
  // Bảng giá gắn được: cùng NCC; bảng của công ty (chưa ngừng) hoặc bảng toàn tập đoàn đã hiệu lực – server kiểm tra lại.
  const priceListOptions = (priceLists.data ?? [])
    .filter((p) => p.supplierCode === supplierCode && p.status !== 'Inactive' && (p.isOwn || p.status === 'Active'))
    .map((p) => ({ value: p.id, label: `${p.no} – ${p.title}${p.isGroupWide ? ' (toàn tập đoàn)' : ''}` }))
  if (doc && !priceListOptions.some((o) => o.value === doc.priceListId))
    priceListOptions.push({ value: doc.priceListId, label: `${doc.priceListNo} – ${doc.priceListTitle}` })

  useEffect(() => {
    if (!doc) return
    form.setFieldsValue({
      supplierId: doc.supplierId,
      priceListId: doc.priceListId,
      contractNo: doc.contractNo ?? '',
      title: doc.title,
      signDate: doc.signDate ? dayjs(doc.signDate) : null,
      validFrom: dayjs(doc.validFrom),
      validTo: dayjs(doc.validTo),
      creditLimit: doc.creditLimit,
      note: doc.note ?? '',
      priceAdjustmentTerms: doc.priceAdjustmentTerms ?? '',
      paymentTerms: doc.paymentTerms ?? '',
      deliveryTerms: doc.deliveryTerms ?? '',
      discountTerms: doc.discountTerms ?? '',
      sponsorshipTerms: doc.sponsorshipTerms ?? '',
      penaltyTerms: doc.penaltyTerms ?? '',
      otherTerms: doc.otherTerms ?? '',
      departmentIds: doc.departmentIds,
      lines: doc.lines.map((l) => ({
        id: l.id, itemId: l.itemId, uomId: l.uomId, expectedQty: l.expectedQty, minCommitQty: l.minCommitQty, note: l.note ?? '',
      })),
    })
  }, [doc, form])

  /** Giá cơ bản của dòng theo bảng giá đang chọn: bậc có SL tối thiểu nhỏ nhất của cùng mặt hàng × ĐVT (như server). */
  const tierOf = (line: LineValues | undefined) =>
    (priceList.data?.lines ?? [])
      .filter((p) => p.itemId === line?.itemId && p.uomId === line?.uomId)
      .sort((a, b) => a.minQty - b.minQty)[0]
  /** Thành tiền dự kiến = SL × đơn giá × (100 − %CK)/100. */
  const amountOf = (line: LineValues | undefined) => {
    const tier = tierOf(line)
    return tier ? ((line?.expectedQty ?? 0) * tier.unitPrice * (100 - tier.discountPct)) / 100 : 0
  }
  const total = lines.reduce((sum, l) => sum + amountOf(l), 0)

  const persist = async (): Promise<Contract | null> => {
    let values: FormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu hợp đồng, tab Điều khoản và dòng sản lượng.')
      return null
    }
    setError(null)
    const input: SaveContractInput = {
      supplierId: values.supplierId!,
      priceListId: values.priceListId!,
      contractNo: orNull(values.contractNo),
      title: values.title?.trim() ?? '',
      signDate: values.signDate ? values.signDate.format('YYYY-MM-DD') : null,
      validFrom: values.validFrom.format('YYYY-MM-DD'),
      validTo: values.validTo!.format('YYYY-MM-DD'),
      creditLimit: values.creditLimit ?? null,
      priceAdjustmentTerms: orNull(values.priceAdjustmentTerms),
      paymentTerms: orNull(values.paymentTerms),
      deliveryTerms: orNull(values.deliveryTerms),
      discountTerms: orNull(values.discountTerms),
      sponsorshipTerms: orNull(values.sponsorshipTerms),
      penaltyTerms: orNull(values.penaltyTerms),
      otherTerms: orNull(values.otherTerms),
      note: orNull(values.note),
      departmentIds: values.departmentIds ?? [],
      lines: (values.lines ?? []).map((l) => ({
        id: l.id ?? null, itemId: l.itemId!, uomId: l.uomId!, expectedQty: l.expectedQty ?? 0, minCommitQty: l.minCommitQty ?? null, note: orNull(l.note),
      })),
    }
    try {
      return isNew ? await mutations.create.mutateAsync(input) : await mutations.update.mutateAsync({ id, input })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được hợp đồng.')
      return null
    }
  }

  const save = async () => {
    if (readOnly) return
    const saved = await persist()
    if (!saved) return
    message.success(`Đã lưu hợp đồng ${saved.no}.`)
    if (isNew) navigate(`${LIST_PATH}/${saved.id}`, { replace: true })
  }

  const fail = (e: unknown, fallback: string) => setError(e instanceof Error ? e.message : fallback)

  const submit = () =>
    modal.confirm({
      title: 'Gửi duyệt hợp đồng nguyên tắc?',
      content: 'Hợp đồng chuyển sang Chờ duyệt, không sửa được nữa trừ khi bị từ chối hoặc rút về nháp. Duyệt xong mới có hiệu lực.',
      okText: 'Gửi duyệt',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        const saved = await persist()
        if (!saved) return
        try {
          const sent = await mutations.submit.mutateAsync(saved.id)
          message.success(sent.status === 'Active' ? `Hợp đồng ${sent.no} đã có hiệu lực.` : `Đã gửi duyệt hợp đồng ${sent.no}.`)
        } catch (e) {
          fail(e, 'Không gửi duyệt được.')
        }
        if (isNew) navigate(`${LIST_PATH}/${saved.id}`, { replace: true })
      },
    })

  const approve = () =>
    doc &&
    modal.confirm({
      title: `Duyệt hợp đồng ${doc.no}?`,
      content: `Giá trị dự kiến ${formatMoney(doc.expectedAmount)} đ. Duyệt cấp cuối thì hợp đồng có hiệu lực.`,
      okText: 'Duyệt',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        try {
          const result = await mutations.approve.mutateAsync({ id: doc.id })
          message.success(result.status === 'Active' ? `Đã duyệt xong – hợp đồng ${doc.no} đã có hiệu lực.` : `Đã duyệt cấp của bạn – hợp đồng ${doc.no} chuyển cấp tiếp theo.`)
        } catch (e) {
          fail(e, 'Không duyệt được.')
        }
      },
    })

  const reject = () =>
    doc &&
    confirmReject(modal, message, `Từ chối hợp đồng ${doc.no}?`, async (reason) => {
      setError(null)
      try {
        await mutations.reject.mutateAsync({ id: doc.id, comment: reason })
        message.success(`Đã từ chối hợp đồng ${doc.no}.`)
      } catch (e) {
        fail(e, 'Không từ chối được.')
      }
    })

  const simple = (title: string, okText: string, run: () => Promise<unknown>, done: string, danger = false, content?: string) =>
    modal.confirm({
      title,
      content,
      okText,
      okButtonProps: { danger },
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        try {
          await run()
          message.success(done)
        } catch (e) {
          fail(e, 'Không thực hiện được.')
        }
      },
    })

  const terminate = () => {
    if (!doc) return
    let date = dayjs()
    let reason = ''
    modal.confirm({
      title: `Chấm dứt hợp đồng ${doc.no}?`,
      content: (
        <Flex vertical gap={6}>
          <label htmlFor="terminate-date">{renderRequiredMark('Ngày chấm dứt', { required: true })}</label>
          <DatePicker id="terminate-date" format={DATE_INPUT_FORMATS} defaultValue={date} onChange={(d) => (date = d ?? dayjs())} />
          <label htmlFor="terminate-reason">{renderRequiredMark('Lý do chấm dứt', { required: true })}</label>
          <Input.TextArea id="terminate-reason" rows={3} maxLength={500} onChange={(e) => (reason = e.target.value)} />
        </Flex>
      ),
      okText: 'Chấm dứt',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        if (!reason.trim()) {
          message.error('Hãy nhập lý do chấm dứt.')
          throw new Error('missing reason')
        }
        try {
          await mutations.terminate.mutateAsync({ id: doc.id, date: date.format('YYYY-MM-DD'), reason: reason.trim() })
          message.success(`Đã chấm dứt hợp đồng ${doc.no}.`)
        } catch (e) {
          fail(e, 'Không chấm dứt được.')
        }
      },
    })
  }

  const columns: LineColumn[] = [
    {
      key: 'item',
      title: 'Mặt hàng', required: true,
      width: 260,
      render: (field) => (
        <>
          <Form.Item name={[field.name, 'id']} hidden>
            <Input />
          </Form.Item>
          <Form.Item name={[field.name, 'itemId']} noStyle rules={[{ required: true, message: 'Chọn mặt hàng.' }]}>
            <Select
              variant="borderless"
              style={{ width: '100%' }}
              aria-label={`Mặt hàng dòng ${field.name + 1}`}
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
      title: 'SL dự kiến', required: true,
      width: 100,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'expectedQty']} noStyle rules={[{ required: true, type: 'number', min: 0.0001, message: 'SL > 0.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={4} aria-label={`SL dự kiến dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'commit',
      title: 'SL cam kết tối thiểu',
      width: 120,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'minCommitQty']} noStyle rules={[{ type: 'number', min: 0, message: '≥ 0.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={4} placeholder="Không cam kết"
            aria-label={`SL cam kết dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'price',
      title: 'Đơn giá (bảng giá)',
      width: 120,
      align: 'right',
      render: (field) => {
        const tier = tierOf(lines[field.name])
        return <span className="line-grid-readonly">{tier ? formatMoney(tier.unitPrice) : lines[field.name]?.itemId ? 'Không có giá' : ''}</span>
      },
    },
    {
      key: 'discount',
      title: '% CK',
      width: 60,
      align: 'right',
      render: (field) => <span className="line-grid-readonly">{tierOf(lines[field.name])?.discountPct ?? ''}</span>,
    },
    {
      key: 'amount',
      title: 'Thành tiền dự kiến',
      width: 130,
      align: 'right',
      render: (field) => <span className="line-grid-readonly">{formatMoney(amountOf(lines[field.name]))}</span>,
    },
    {
      key: 'note',
      title: 'Ghi chú',
      width: 180,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle rules={[{ max: 500 }]}>
          <Input variant="borderless" aria-label={`Ghi chú dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? contractStatusLabels[doc.status] : '', tone: doc ? contractStatusTone[doc.status] : ('neutral' as const) }

  const actions = (
    <>
      {(isNew ? !readOnly : doc?.actions.canEdit) && (
        <Button type="primary" icon={<SaveOutlined />} onClick={save} loading={mutations.create.isPending || mutations.update.isPending}>
          Lưu nháp
        </Button>
      )}
      {(isNew ? !readOnly : doc?.actions.canSubmit) && (
        <Button icon={<SendOutlined />} onClick={submit} loading={mutations.submit.isPending}>
          Gửi duyệt
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
        <Button icon={<RollbackOutlined />} loading={mutations.recall.isPending}
          onClick={() => simple(`Rút hợp đồng ${doc.no} về nháp?`, 'Rút về nháp', () => mutations.recall.mutateAsync(doc.id), `Đã rút hợp đồng ${doc.no} về nháp.`,
            false, 'Các cấp đang chờ duyệt bỏ qua; sửa xong thì gửi duyệt lại từ đầu.')}>
          Rút về nháp
        </Button>
      )}
      {doc?.actions.canAmend && (
        <Button icon={<FileAddOutlined />} onClick={() => openAmendment('moi')}>
          Lập phụ lục
        </Button>
      )}
      {doc?.actions.canTerminate && (
        <Button danger icon={<StopOutlined />} onClick={terminate} loading={mutations.terminate.isPending}>
          Chấm dứt
        </Button>
      )}
      {!isNew && doc?.actions.canCancel && (
        <Button danger icon={<StopOutlined />}
          onClick={() => simple(`Hủy hợp đồng ${doc.no}?`, 'Hủy hợp đồng', () => mutations.cancel.mutateAsync(doc.id), `Đã hủy hợp đồng ${doc.no}.`, true,
            'Hợp đồng đã hủy giữ lại số và lịch sử, không dùng lại được.')}>
          Hủy hợp đồng
        </Button>
      )}
      <Button type="text" icon={<CloseOutlined />} onClick={() => navigate(LIST_PATH)}>
        Đóng
      </Button>
    </>
  )

  const departmentOptions = (departments.data ?? [])
    .filter((d) => d.isActive || doc?.departmentIds.includes(d.id))
    .map((d) => ({ value: d.id, label: `${d.code} – ${d.name}` }))

  return (
    <>
      <DocumentForm
        title="Hợp đồng nguyên tắc"
        number={isNew ? null : (doc?.no ?? '')}
        audit={{ entityType: 'PurchaseContract', id: doc?.id }}
        status={status}
        meta={
          doc &&
          `Pháp nhân: ${doc.companyCode} · Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)} · Giá trị dự kiến ${formatMoney(doc.expectedAmount)} đ`
        }
        loading={contract.isLoading}
        error={error ?? (contract.error ? contract.error.message : null)}
        notice={doc && <ContractNotices doc={doc} />}
        form={form}
        initialValues={{ validFrom: dayjs(), departmentIds: [], lines: [] }}
        readOnly={readOnly}
        onSave={save}
        onAddLine={() => addRef.current?.()}
        actions={actions}
        header={
          <>
            <Form.Item label="Nhà cung cấp" name="supplierId" rules={[{ required: true, message: 'Chọn nhà cung cấp.' }]}>
              <Select
                showSearch={{ optionFilterProp: 'label' }}
                options={(suppliers.data?.items ?? [])
                  .filter((s) => s.status === 'Approved' || s.id === doc?.supplierId)
                  .map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))}
                onChange={() => form.setFieldValue('priceListId', undefined)}
              />
            </Form.Item>
            <Form.Item label="Bảng giá dùng chung" name="priceListId" rules={[{ required: true, message: 'Chọn bảng giá.' }]}
              extra={supplierId && priceListOptions.length === 0 ? 'NCC chưa có bảng giá dùng được – lập và duyệt bảng giá trước.' : undefined}>
              <Select showSearch={{ optionFilterProp: 'label' }} options={priceListOptions} aria-label="Bảng giá dùng chung" />
            </Form.Item>
            <Form.Item label="Tên hợp đồng" name="title" rules={[{ required: true, message: 'Nhập tên hợp đồng.' }, { max: 200 }]}>
              <Input />
            </Form.Item>
            <Form.Item label="Số HĐ giấy" name="contractNo" rules={[{ max: 100 }]}>
              <Input />
            </Form.Item>
            <Form.Item label="Ngày ký" name="signDate">
              <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Hiệu lực từ" name="validFrom" rules={[{ required: true, message: 'Chọn ngày.' }]}>
              <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Hiệu lực đến" name="validTo" rules={[{ required: true, message: 'Chọn ngày.' }]}>
              <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Hạn mức công nợ (VND)" name="creditLimit" rules={[{ type: 'number', min: 0, message: '≥ 0.' }]}>
              <InputNumber {...vnNumberInput} style={{ width: '100%' }} min={0} precision={0} />
            </Form.Item>
            <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]}>
              <Input />
            </Form.Item>
          </>
        }
        linesLabel="Sản lượng dự kiến"
        lines={<LineGrid name="lines" auditEntity="PurchaseContractLine" columns={columns} readOnly={readOnly} addRef={addRef} newLine={() => ({ id: null, expectedQty: 1, minCommitQty: null })} />}
        footer={<DocumentTotal label="Giá trị dự kiến (sau CK, trước VAT)" value={formatMoney(total)} strong />}
        extraTabs={[
          {
            key: 'terms',
            label: 'Điều khoản',
            children: (
              <div style={{ maxWidth: 900 }}>
                {termFields.map((t) => (
                  <Form.Item key={t.name} label={t.label} name={t.name} rules={[{ max: t.max }]}>
                    <Input.TextArea rows={2} />
                  </Form.Item>
                ))}
              </div>
            ),
          },
          {
            key: 'departments',
            label: `Bộ phận được đặt (${(doc?.departmentIds.length ?? 0) || 'tất cả'})`,
            children: (
              <Form.Item label="Bộ phận được phép đặt hàng theo hợp đồng (để trống = mọi bộ phận của công ty)" name="departmentIds" style={{ maxWidth: 900 }}>
                <Select mode="multiple" showSearch={{ optionFilterProp: 'label' }} options={departmentOptions} placeholder="Mọi bộ phận" aria-label="Bộ phận được đặt" />
              </Form.Item>
            ),
          },
          // Phụ lục (lát B4b): chỉ có khi hợp đồng đã gửi duyệt trở đi; lập phụ lục khi hợp đồng hiệu lực.
          ...(doc && doc.status !== 'Draft' && doc.status !== 'Cancelled'
            ? [{ key: 'amendments', label: `Phụ lục (${doc.amendments.length})`, children: <AmendmentTable rows={doc.amendments} onOpen={openAmendment} /> }]
            : []),
          ...(doc && doc.approvals.length > 0 ? [{ key: 'approvals', label: 'Lịch sử duyệt', children: <ApprovalHistory steps={doc.approvals} /> }] : []),
          attachmentTab,
        ]}
      />
      {doc && amendmentId && (
        <ContractAmendmentDrawer contract={doc} amendmentId={amendmentId} onClose={() => openAmendment(null)} onCreated={(id) => openAmendment(id)} />
      )}
    </>
  )
}
