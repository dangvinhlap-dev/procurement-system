import { useDocumentFormSync } from '../../components/DocumentForm/useDocumentFormSync'
import { CheckOutlined, CloseCircleOutlined, CloseOutlined, CopyOutlined, RollbackOutlined, SaveOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import { Alert, App, Button, Checkbox, DatePicker, Flex, Form, Input, InputNumber, Table, Typography } from 'antd'
import { Select } from '../../classic/Select'
import dayjs, { type Dayjs } from 'dayjs'
import { useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { useItemLookup } from '../../api/inventoryDocuments'
import { useItemCategories, useSuppliers } from '../../api/masterData'
import { useMonthlyApprovalCompanies } from '../../api/monthlyApprovals'
import { withPendingApprovers } from '../../api/purchasing'
import {
  volumeRebateBasisLabels,
  volumeRebatePeriodLabels,
  volumeRebateStateLabels,
  volumeRebateStatusLabels,
  volumeRebateStatusTone,
  volumeRebateTargetLabels,
  volumeRebateTargetTone,
  useVolumeRebate,
  useVolumeRebateMutations,
  useVolumeRebateProgress,
  useVolumeRebates,
  useVolumeRebateTracking,
  type SaveVolumeRebateInput,
  type VolumeRebate,
  type VolumeRebateBasis,
  type VolumeRebateListItem,
  type VolumeRebatePeriod,
  type VolumeRebatePeriodRow,
  type VolumeRebateShare,
  type VolumeRebateTrackingRow,
} from '../../api/volumeRebates'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { DataGrid } from '../../components/DataGrid/DataGrid'
import type { GridColumn } from '../../components/DataGrid/types'
import { DocumentForm } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { DATE_INPUT_FORMATS, formatDate, formatDateTime, formatMoney, formatQuantity, vnNumberInput } from '../../utils/format'
import { ApprovalHistory } from '../purchasing/ApprovalHistory'
import { VolumeRebateReceiptPanel } from './VolumeRebateReceiptPanel'
import { approveContent, confirmReject, confirmSubmitWithPreview } from '../purchasing/useApprovalCommands'
import { confirmApprove } from '../purchasing/approvalDialogs'

/** Đường dẫn danh sách thỏa thuận chiết khấu sản lượng gộp (màn hình CKB). */
export const VOLUME_REBATE_PATH = '/gia-ncc/chiet-khau-san-luong'

/** X% mặc định cảnh báo sắp chạm bậc (design.md mục 18.25 – G6a). */
const DEFAULT_NEAR = 10

const options = <T extends string>(labels: Record<T, string>) => (Object.keys(labels) as T[]).map((value) => ({ value, label: labels[value] }))

/**
 * Lũy kế/ngưỡng theo cơ sở tính bậc: số lượng (kèm ĐVT) hoặc tiền VND.
 * @param basis Cơ sở.
 * @param value Giá trị.
 * @param uom Mã ĐVT (tính theo số lượng).
 */
const formatVolume = (basis: VolumeRebateBasis, value: number | null | undefined, uom?: string | null) =>
  value == null ? '' : basis === 'Quantity' ? `${formatQuantity(value)}${uom ? ` ${uom}` : ''}` : formatMoney(value)

/** % hiển thị kiểu Việt Nam, tối đa 4 số lẻ (ví dụ 2,5%). */
const formatPercent = (value: number) => `${value.toLocaleString('vi-VN', { maximumFractionDigits: 4 })}%`

const listColumns: GridColumn<VolumeRebateListItem>[] = [
  { key: 'no', title: 'Số thỏa thuận', width: 150, value: (r) => r.no, docRef: (r) => ({ docType: 'CKB', id: r.id }) },
  { key: 'title', title: 'Tên thỏa thuận', width: 220, value: (r) => r.title },
  { key: 'supplier', title: 'Nhà cung cấp', width: 200, value: (r) => `${r.supplierCode} – ${r.supplierName}`, groupable: true },
  { key: 'category', title: 'Nhóm hàng', width: 180, value: (r) => `${r.categoryCode} – ${r.categoryName}`, groupable: true },
  { key: 'validFrom', title: 'Hiệu lực từ', width: 95, type: 'date', value: (r) => r.validFrom },
  { key: 'validTo', title: 'Hiệu lực đến', width: 95, type: 'date', value: (r) => r.validTo },
  { key: 'period', title: 'Kỳ tính bậc', width: 100, value: (r) => volumeRebatePeriodLabels[r.period], groupable: true },
  { key: 'basis', title: 'Cơ sở', width: 130, value: (r) => volumeRebateBasisLabels[r.basis], groupable: true },
  { key: 'companies', title: 'Công ty áp dụng', width: 150, value: (r) => r.companyCodes },
  { key: 'lines', title: 'Số mặt hàng', width: 90, type: 'quantity', value: (r) => r.lineCount },
  { key: 'tiers', title: 'Số bậc', width: 70, type: 'quantity', value: (r) => r.tierCount },
  {
    key: 'status', title: 'Trạng thái', width: 120, type: 'status', groupable: true,
    value: (r) => volumeRebateStatusLabels[r.status], tone: (r) => volumeRebateStatusTone[r.status],
  },
  { key: 'creator', title: 'Người lập', width: 150, value: (r) => r.createdByName, groupable: true },
]

/**
 * Màn hình Giá và NCC → Chiết khấu sản lượng gộp (CKB – design.md mục 18.25, lát G6a): Ban cung ứng thương lượng bậc chiết khấu theo sản
 * lượng gộp của các công ty với NCC; tổng công ty lập, Ban duyệt; công ty áp dụng xem thỏa thuận hiệu lực.
 */
export function VolumeRebateListPage() {
  const navigate = useNavigate()
  const { hasPermission, workingCompany } = useAuth()
  const list = useVolumeRebates()
  return (
    <ListPage
      title="Chiết khấu sản lượng gộp"
      exportFileName="chiet-khau-san-luong-gop"
      rows={list.data ?? []}
      columns={listColumns}
      rowKey={(r) => r.id}
      auditEntity="VolumeRebateAgreement"
      loading={list.isLoading}
      // Chỉ tổng công ty lập thỏa thuận (quyết định Ban cung ứng); công ty thành viên chỉ xem.
      canCreate={hasPermission(Permission.VolumeRebateManage) && !!workingCompany?.isHeadOffice}
      onCreate={() => navigate(`${VOLUME_REBATE_PATH}/moi`)}
      onOpenRow={(r) => navigate(`${VOLUME_REBATE_PATH}/${r.id}`)}
      isMarked={(r) => r.status === 'Submitted'}
    />
  )
}

interface LineValues {
  id: string | null
  itemId?: string
  uomId?: string
  note?: string
}

interface TierValues {
  fromValue?: number | null
  percent?: number | null
}

interface FormValues {
  title?: string
  supplierId?: string
  itemCategoryId?: string
  validFrom?: Dayjs
  validTo?: Dayjs
  period: VolumeRebatePeriod
  basis: VolumeRebateBasis
  targetTierNo?: number | null
  companyIds: string[]
  note?: string
  lines: LineValues[]
  tiers: TierValues[]
}

/** Chuỗi rỗng hoặc toàn khoảng trắng gửi lên API là null; còn lại bỏ khoảng trắng hai đầu. */
const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/** Đèn + chữ (màu không là thông tin duy nhất – docs/ui-style.md mục 2). */
function Light({ tone, label }: { tone: 'success' | 'warning' | 'error' | 'neutral'; label: string }) {
  const color = tone === 'success' ? 'green' : tone === 'warning' ? 'yellow' : tone === 'error' ? 'red' : null
  return (
    <Flex align="center" gap={4}>
      {color && <span className={`erp-light erp-light--${color}`} aria-hidden />}
      {label}
    </Flex>
  )
}

/** Tình trạng so bậc mục tiêu + sắp chạm bậc kế. */
function TargetCell({ row }: { row: VolumeRebatePeriodRow }) {
  return (
    <Flex vertical>
      <Light tone={volumeRebateTargetTone[row.targetStatus]} label={volumeRebateTargetLabels[row.targetStatus]} />
      {row.isNearNextTier && <Light tone="warning" label={`Sắp chạm bậc ${row.nextTierNo}`} />}
    </Flex>
  )
}

/**
 * Tab LŨY KẾ của thỏa thuận: mỗi kỳ tính bậc một dòng (lũy kế gộp, bậc đạt, chiết khấu phải nhận, còn thiếu lên bậc kế, dự báo, so bậc
 * mục tiêu); mở dòng xem phần từng công ty theo tỷ lệ doanh số (công ty thành viên chỉ thấy phần mình).
 * @param doc Thỏa thuận đã lưu.
 */
function ProgressPanel({ doc }: { doc: VolumeRebate }) {
  const [near, setNear] = useState(DEFAULT_NEAR)
  const progress = useVolumeRebateProgress(doc.id, near)
  const data = progress.data
  const basis = data?.basis ?? doc.basis
  const uom = data?.uomCode
  const volume = (v: number | null) => formatVolume(basis, v, uom)
  const money = (v: number) => formatMoney(v)
  return (
    <Flex vertical gap={8} style={{ marginTop: 8 }}>
      <Flex align="center" gap={8} wrap>
        <Typography.Text type="secondary">
          Lũy kế = PO đã duyệt/đã đóng (VND) của các công ty áp dụng với NCC, mặt hàng trên thỏa thuận, theo kỳ. Đạt bậc thì cả kỳ hưởng % của bậc
          trên doanh số; chiết khấu chia cho từng công ty theo tỷ lệ doanh số.
        </Typography.Text>
        <label htmlFor="vr-near">Cảnh báo sắp chạm bậc khi đạt từ (100 − X)%, X =</label>
        {/* Ô xem (không phải dữ liệu thỏa thuận): luôn sửa được kể cả khi form chỉ đọc. */}
        <InputNumber {...vnNumberInput} id="vr-near" disabled={false} min={1} max={50} precision={0} value={near} style={{ width: 70 }}
          onChange={(v) => setNear(typeof v === 'number' && v >= 1 ? v : DEFAULT_NEAR)} />
      </Flex>
      {progress.error && <Alert type="error" showIcon title={progress.error.message} />}
      <Table<VolumeRebatePeriodRow>
        size="small"
        pagination={false}
        loading={progress.isLoading}
        rowKey={(r) => r.periodFrom}
        dataSource={data?.periods ?? []}
        tableLayout="fixed"
        scroll={{ x: 1300 }}
        aria-label="Lũy kế theo kỳ tính bậc"
        expandable={{
          rowExpandable: (r) => r.companies.length > 0,
          expandedRowRender: (r) => (
            <Table<VolumeRebateShare>
              size="small"
              pagination={false}
              rowKey={(s) => s.companyId}
              dataSource={r.companies}
              aria-label={`Chia chiết khấu kỳ ${formatDate(r.periodFrom)}`}
              columns={[
                { title: 'Công ty', dataIndex: 'companyCode', width: 110 },
                ...(basis === 'Quantity' ? [{ title: 'Số lượng', dataIndex: 'quantity', width: 110, align: 'right' as const, render: (v: number) => volume(v) }] : []),
                { title: 'Doanh số', dataIndex: 'amount', width: 130, align: 'right', render: money },
                { title: 'Tỷ lệ', dataIndex: 'sharePercent', width: 90, align: 'right', render: (v: number) => formatPercent(v) },
                { title: 'Chiết khấu được chia', dataIndex: 'rebateShare', width: 150, align: 'right', render: money },
              ]}
            />
          ),
        }}
        columns={[
          { title: 'Kỳ', key: 'period', width: 165, render: (_, r) => `${formatDate(r.periodFrom)} – ${formatDate(r.periodTo)}` },
          { title: 'Tình trạng kỳ', dataIndex: 'state', width: 105, render: (v: VolumeRebatePeriodRow['state']) => volumeRebateStateLabels[v] },
          { title: 'Lũy kế', dataIndex: 'volume', width: 120, align: 'right', render: volume },
          { title: 'Bậc đạt', dataIndex: 'tierNo', width: 65, align: 'right', render: (v: number | null) => v ?? 'Chưa đạt' },
          { title: '% CK', dataIndex: 'percent', width: 65, align: 'right', render: (v: number) => formatPercent(v) },
          { title: 'Doanh số', dataIndex: 'amount', width: 125, align: 'right', render: money },
          { title: 'CK phải nhận', dataIndex: 'rebateAmount', width: 115, align: 'right', render: money },
          { title: 'Bậc kế', dataIndex: 'nextTierNo', width: 60, align: 'right', render: (v: number | null) => v ?? '' },
          { title: 'Còn thiếu lên bậc kế', dataIndex: 'shortfall', width: 135, align: 'right', render: volume },
          { title: 'Dự báo cuối kỳ', dataIndex: 'forecast', width: 120, align: 'right', render: volume },
          { title: 'So bậc mục tiêu', key: 'target', width: 160, render: (_, r) => <TargetCell row={r} /> },
        ]}
      />
    </Flex>
  )
}

/**
 * Trang thỏa thuận chiết khấu sản lượng gộp trên khuôn DocumentForm: NCC, nhóm hàng, thời hạn, kỳ tính bậc, cơ sở, bậc mục tiêu, công ty
 * áp dụng, ghi chú; dòng = mặt hàng tính sản lượng; tab BẬC CHIẾT KHẤU (ngưỡng → %); tab LŨY KẾ. Nháp → Gửi duyệt → Hiệu lực; Hủy, Chép
 * (design.md mục 18.25 – G6a).
 */
export function VolumeRebatePage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const { hasPermission, workingCompany } = useAuth()
  const [form] = Form.useForm<FormValues>()
  const query = useVolumeRebate(isNew ? null : id)
  const mutations = useVolumeRebateMutations()
  const companies = useMonthlyApprovalCompanies()
  const categories = useItemCategories()
  const suppliers = useSuppliers({})
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const addRef = useRef<(() => void) | null>(null)
  const addTierRef = useRef<(() => void) | null>(null)
  const doc = query.data
  const canCreate = hasPermission(Permission.VolumeRebateManage) && !!workingCompany?.isHeadOffice
  const readOnly = isNew ? !canCreate : !doc?.actions.canEdit
  // Đính kèm văn bản thỏa thuận, thư chính sách chiết khấu của NCC – quyền theo quyền lập (design.md mục 18.4).
  const attachmentTab = useAttachmentTab('VolumeRebateAgreement', doc?.id, hasPermission(Permission.VolumeRebateManage))

  const watchedLines = Form.useWatch('lines', form) as LineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const basis = (Form.useWatch('basis', form) as VolumeRebateBasis | undefined) ?? 'Quantity'
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])
  // Ban chỉ thỏa thuận với NCC Đã duyệt (danh sách NCC đủ điều kiện – G1); NCC trên thỏa thuận cũ vẫn hiện để đọc được.
  const supplierOptions = (suppliers.data?.items ?? [])
    .filter((s) => s.status === 'Approved' || s.id === doc?.supplierId)
    .map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))

  const { loadSaved, targetId } = useDocumentFormSync(form, id, doc, (doc) => ({
    title: doc.title,
    supplierId: doc.supplierId,
    itemCategoryId: doc.itemCategoryId,
    validFrom: dayjs(doc.validFrom),
    validTo: dayjs(doc.validTo),
    period: doc.period,
    basis: doc.basis,
    targetTierNo: doc.targetTierNo,
    companyIds: doc.companies.map((c) => c.id),
    note: doc.note ?? '',
    lines: doc.lines.map((l) => ({ id: l.id, itemId: l.itemId, uomId: l.uomId, note: l.note ?? '' })),
    tiers: doc.tiers.map((t) => ({ fromValue: t.fromValue, percent: t.percent })),
  }))

  const fail = (e: unknown, fallback: string) => setError(e instanceof Error ? e.message : fallback)

  const persist = async (): Promise<VolumeRebate | null> => {
    let values: FormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu thỏa thuận, dòng mặt hàng và tab Bậc chiết khấu.')
      return null
    }
    setError(null)
    const input: SaveVolumeRebateInput = {
      title: values.title?.trim() ?? '',
      supplierId: values.supplierId!,
      itemCategoryId: values.itemCategoryId!,
      validFrom: values.validFrom!.format('YYYY-MM-DD'),
      validTo: values.validTo!.format('YYYY-MM-DD'),
      period: values.period,
      basis: values.basis,
      targetTierNo: values.targetTierNo ?? null,
      companyIds: values.companyIds ?? [],
      note: orNull(values.note),
      lines: (values.lines ?? []).map((l) => ({ id: l.id ?? null, itemId: l.itemId!, uomId: l.uomId!, note: orNull(l.note) })),
      tiers: (values.tiers ?? []).map((t) => ({ fromValue: t.fromValue ?? 0, percent: t.percent ?? 0 })),
    }
    try {
      const target = targetId()
      const saved = target ? await mutations.update.mutateAsync({ id: target, input }) : await mutations.create.mutateAsync(input)
      loadSaved(saved)
      return saved
    } catch (e) {
      fail(e, 'Không lưu được thỏa thuận.')
      return null
    }
  }

  const save = async () => {
    if (readOnly) return
    const saved = await persist()
    if (!saved) return
    message.success(`Đã lưu thỏa thuận ${saved.no}.`)
    if (isNew) navigate(`${VOLUME_REBATE_PATH}/${saved.id}`, { replace: true })
  }

  // Người lập không tự duyệt; lưu, xem trước chuỗi duyệt rồi mới gửi (như các phiếu có duyệt khác).
  const submit = () => {
    setError(null)
    void confirmSubmitWithPreview(modal, {
      title: 'Gửi Ban cung ứng duyệt thỏa thuận chiết khấu?',
      docType: 'CKB',
      prepare: async () => {
        const saved = await persist()
        if (!saved) return null
        if (isNew) navigate(`${VOLUME_REBATE_PATH}/${saved.id}`, { replace: true })
        return { id: saved.id }
      },
      onSubmit: async (target) => {
        try {
          const sent = await mutations.submit.mutateAsync({ id: target })
          message.success(sent.status === 'Active' ? `Thỏa thuận ${sent.no} đã hiệu lực.` : `Đã gửi duyệt thỏa thuận ${sent.no}.`)
        } catch (e) {
          fail(e, 'Không gửi duyệt được.')
        }
      },
      onError: (e) => fail(e, 'Không gửi duyệt được.'),
    })
  }

  const approve = () =>
    doc &&
    confirmApprove(modal, message, {
      attach: { ownerType: 'VolumeRebateAgreement', ownerId: doc.id },
      title: `Duyệt thỏa thuận ${doc.no}?`,
      content: (
        <>
          <p>Duyệt cấp cuối thì thỏa thuận hiệu lực: Phòng cung ứng chung theo dõi lũy kế sản lượng gộp so với các bậc.</p>
          {approveContent(doc)}
        </>
      ),
      onOk: async (comment) => {
        setError(null)
        try {
          const result = await mutations.approve.mutateAsync({ id: doc.id, comment: comment ?? undefined })
          message.success(result.status === 'Active' ? `Đã duyệt xong thỏa thuận ${doc.no}.` : `Đã duyệt cấp của bạn – thỏa thuận ${doc.no} chuyển cấp tiếp theo.`)
        } catch (e) {
          fail(e, 'Không duyệt được.')
          return false
        }
      },
    })

  const reject = () =>
    doc &&
    confirmReject(modal, message, `Từ chối thỏa thuận ${doc.no}?`, async (reason) => {
      setError(null)
      try {
        await mutations.reject.mutateAsync({ id: doc.id, comment: reason })
        message.success(`Đã từ chối thỏa thuận ${doc.no}.`)
      } catch (e) {
        fail(e, 'Không từ chối được.')
        return false
      }
    }, { ownerType: 'VolumeRebateAgreement', ownerId: doc.id })

  const recall = () =>
    doc &&
    modal.confirm({
      title: `Rút thỏa thuận ${doc.no} về nháp?`,
      content: 'Các cấp đang chờ duyệt bỏ qua; sửa xong thì gửi duyệt lại từ đầu.',
      okText: 'Rút về nháp',
      cancelText: 'Không',
      onOk: async () => {
        try {
          await mutations.recall.mutateAsync(doc.id)
          message.success(`Đã rút thỏa thuận ${doc.no} về nháp.`)
        } catch (e) {
          fail(e, 'Không rút về nháp được.')
        }
      },
    })

  const cancel = () => {
    if (!doc) return
    let reason = ''
    modal.confirm({
      title: `Hủy thỏa thuận ${doc.no}?`,
      content: (
        <>
          <p>Thỏa thuận hủy không còn theo dõi lũy kế; muốn đổi bậc thì chép thành thỏa thuận mới rồi gửi duyệt.</p>
          <Input.TextArea aria-label="Lý do hủy" rows={3} maxLength={1000} onChange={(e) => (reason = e.target.value)} />
        </>
      ),
      okText: 'Hủy thỏa thuận',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        if (!reason.trim()) {
          message.error('Hãy nhập lý do hủy.')
          throw new Error('missing reason')
        }
        try {
          await mutations.cancel.mutateAsync({ id: doc.id, reason: reason.trim() })
          message.success(`Đã hủy thỏa thuận ${doc.no}.`)
        } catch (e) {
          fail(e, 'Không hủy được.')
        }
      },
    })
  }

  /** Chép thành thỏa thuận nháp mới (thời hạn nối tiếp, cùng bậc và mặt hàng) rồi mở thỏa thuận mới. */
  const copy = async () => {
    if (!doc) return
    setError(null)
    try {
      const created = await mutations.copy.mutateAsync(doc.id)
      message.success(`Đã chép thành thỏa thuận ${created.no} (hiệu lực từ ${formatDate(created.validFrom)}).`)
      navigate(`${VOLUME_REBATE_PATH}/${created.id}`)
    } catch (e) {
      fail(e, 'Không chép được thỏa thuận.')
    }
  }

  const columns: LineColumn[] = [
    {
      key: 'item', title: 'Mặt hàng', required: true, width: 320,
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
      key: 'uom', title: 'ĐVT sản lượng', required: true, width: 110,
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
      key: 'note', title: 'Ghi chú', width: 280,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle rules={[{ max: 500 }]}>
          <Input variant="borderless" aria-label={`Ghi chú dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const tierColumns: LineColumn[] = [
    {
      key: 'fromValue', title: basis === 'Quantity' ? 'Ngưỡng từ (SL theo ĐVT dòng)' : 'Ngưỡng từ (doanh số VND)', required: true, width: 200, align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'fromValue']} noStyle rules={[{ required: true, type: 'number', min: 0.0001, message: 'Ngưỡng > 0.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={basis === 'Quantity' ? 4 : 0}
            aria-label={`Ngưỡng bậc ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'percent', title: '% chiết khấu', required: true, width: 120, align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'percent']} noStyle rules={[{ required: true, type: 'number', min: 0.0001, max: 100, message: '0 < % ≤ 100.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} max={100} precision={4}
            aria-label={`% chiết khấu bậc ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : {
        label: doc ? withPendingApprovers(volumeRebateStatusLabels[doc.status], doc.approvals) : '',
        tone: doc ? volumeRebateStatusTone[doc.status] : ('neutral' as const),
      }

  const notices = doc && [
    doc.rejectReason && doc.status === 'Draft' && <Alert key="reject" type="warning" showIcon title={`Bị từ chối: ${doc.rejectReason}`} />,
    doc.status === 'Cancelled' && <Alert key="cancel" type="error" showIcon title={`Đã hủy: ${doc.cancelReason ?? ''}`} />,
    doc.actions.readOnlyReason && <Alert key="readonly" type="info" showIcon title={doc.actions.readOnlyReason} />,
  ].filter(Boolean)

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
      {doc?.actions.canCopy && (
        <Button icon={<CopyOutlined />} onClick={() => void copy()} loading={mutations.copy.isPending}>
          Chép thành thỏa thuận mới
        </Button>
      )}
      {doc?.actions.canCancel && (
        <Button danger icon={<StopOutlined />} onClick={cancel} loading={mutations.cancel.isPending}>
          Hủy thỏa thuận
        </Button>
      )}
      <Button type="text" icon={<CloseOutlined />} onClick={() => navigate(VOLUME_REBATE_PATH)}>
        Đóng
      </Button>
    </>
  )

  return (
    <DocumentForm
      approval={doc ? { steps: doc.approvals, createdByName: doc.createdByName, createdAt: doc.createdAt } : undefined}
      title="Chiết khấu sản lượng gộp"
      linesLabel="Mặt hàng"
      extraTabs={[
        {
          key: 'tiers', label: 'Bậc chiết khấu',
          children: (
            <LineGrid name="tiers" auditEntity="VolumeRebateTier" columns={tierColumns} readOnly={readOnly} addRef={addTierRef}
              newLine={() => ({ fromValue: null, percent: null })} />
          ),
        },
        ...(doc ? [{ key: 'progress', label: 'Lũy kế', children: <ProgressPanel doc={doc} /> }] : []),
        // Thực nhận theo kỳ (lát G6b) chỉ có ý nghĩa khi thỏa thuận đã hiệu lực (hủy rồi vẫn xem lại các lần đã ghi).
        ...(doc && (doc.status === 'Active' || doc.status === 'Cancelled')
          ? [{ key: 'receipts', label: 'Thực nhận', children: <VolumeRebateReceiptPanel doc={doc} /> }]
          : []),
        attachmentTab,
        ...(doc ? [{ key: 'approvals', label: 'Lịch sử duyệt', children: <ApprovalHistory steps={doc.approvals} createdByName={doc.createdByName} createdAt={doc.createdAt} /> }] : []),
      ]}
      number={isNew ? null : (doc?.no ?? '')}
      audit={{ entityType: 'VolumeRebateAgreement', id: doc?.id }}
      status={status}
      meta={doc && `Công ty lập: ${doc.companyCode} · Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
      loading={query.isLoading}
      error={error ?? (query.error ? query.error.message : null)}
      notice={notices && notices.length > 0 ? <Flex vertical gap={8}>{notices}</Flex> : null}
      form={form}
      initialValues={{ period: 'Month', basis: 'Quantity', companyIds: [], lines: [], tiers: [], validFrom: dayjs().startOf('year'), validTo: dayjs().endOf('year') }}
      readOnly={readOnly}
      onSave={save}
      onAddLine={() => addRef.current?.()}
      actions={actions}
      header={
        <>
          <Form.Item label="Tên thỏa thuận" name="title" rules={[{ required: true, whitespace: true, message: 'Nhập tên thỏa thuận.' }, { max: 200 }]}>
            <Input aria-label="Tên thỏa thuận" />
          </Form.Item>
          <Form.Item label="Nhà cung cấp" name="supplierId" rules={[{ required: true, message: 'Chọn NCC.' }]}>
            <Select showSearch={{ optionFilterProp: 'label' }} aria-label="Nhà cung cấp" options={supplierOptions} popupMatchSelectWidth={360} />
          </Form.Item>
          <Form.Item label="Nhóm hàng" name="itemCategoryId" rules={[{ required: true, message: 'Chọn nhóm hàng.' }]}>
            <Select showSearch={{ optionFilterProp: 'label' }} aria-label="Nhóm hàng"
              options={(categories.data ?? []).map((c) => ({ value: c.id, label: `${c.code} – ${c.name}` }))} />
          </Form.Item>
          <Form.Item label="Hiệu lực từ" name="validFrom" rules={[{ required: true, message: 'Chọn ngày.' }]}>
            <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} aria-label="Hiệu lực từ" />
          </Form.Item>
          <Form.Item label="Hiệu lực đến" name="validTo" rules={[{ required: true, message: 'Chọn ngày.' }]}>
            <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} aria-label="Hiệu lực đến" />
          </Form.Item>
          <Form.Item label="Kỳ tính bậc" name="period" rules={[{ required: true }]}>
            <Select aria-label="Kỳ tính bậc" options={options(volumeRebatePeriodLabels)} />
          </Form.Item>
          <Form.Item label="Cơ sở tính bậc" name="basis" rules={[{ required: true }]}
            tooltip="Số lượng gộp: mọi dòng cùng một ĐVT. Chiết khấu luôn tính % trên doanh số của kỳ.">
            <Select aria-label="Cơ sở tính bậc" options={options(volumeRebateBasisLabels)} />
          </Form.Item>
          <Form.Item label="Bậc mục tiêu" name="targetTierNo" tooltip="Bậc đã đàm phán dựa trên sản lượng dự kiến – để cảnh báo nguy cơ hụt bậc.">
            <InputNumber {...vnNumberInput} min={1} max={10} precision={0} style={{ width: '100%' }} aria-label="Bậc mục tiêu" placeholder="Không đặt" />
          </Form.Item>
          <Form.Item label="Công ty áp dụng" name="companyIds" rules={[{ required: true, type: 'array', min: 1, message: 'Chọn ít nhất một công ty.' }]}>
            <Select mode="multiple" aria-label="Công ty áp dụng" optionFilterProp="label"
              options={(companies.data ?? doc?.companies ?? []).map((c) => ({ value: c.id, label: `${c.code} – ${c.name}` }))} />
          </Form.Item>
          <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]}>
            <Input aria-label="Ghi chú" />
          </Form.Item>
        </>
      }
      lines={
        <LineGrid name="lines" auditEntity="VolumeRebateLine" columns={columns} readOnly={readOnly} addRef={addRef} newLine={() => ({ id: null })} />
      }
    />
  )
}

/** Dòng cần chú ý: sắp chạm/hụt bậc, hoặc có kỳ quá hạn ghi nhận còn thiếu thực nhận (lát G6b) – khớp lọc "Chỉ dòng có cảnh báo" của máy chủ. */
const isTrackingAlert = (r: VolumeRebateTrackingRow) =>
  r.period.isNearNextTier || r.period.targetStatus === 'AtRisk' || r.period.targetStatus === 'Missed' || r.unreceivedPeriods > 0

/** Hiện sản lượng theo cơ sở tính (số lượng kèm ĐVT hoặc số tiền). */
const trackingVolume = (r: VolumeRebateTrackingRow, v: number | null) => formatVolume(r.basis, v, r.uomCode)

const trackingColumns: GridColumn<VolumeRebateTrackingRow>[] = [
  { key: 'no', title: 'Số thỏa thuận', width: 140, value: (r) => r.no, docRef: (r) => ({ docType: 'CKB', id: r.agreementId }) },
  { key: 'title', title: 'Tên thỏa thuận', width: 200, value: (r) => r.title },
  { key: 'supplier', title: 'Nhà cung cấp', width: 190, value: (r) => `${r.supplierCode} – ${r.supplierName}`, groupable: true },
  { key: 'period', title: 'Kỳ', width: 160, value: (r) => `${formatDate(r.period.periodFrom)} – ${formatDate(r.period.periodTo)}`, auditField: false },
  { key: 'state', title: 'Tình trạng kỳ', width: 105, value: (r) => volumeRebateStateLabels[r.period.state], groupable: true, auditField: false },
  { key: 'volume', title: 'Lũy kế', width: 130, value: (r) => trackingVolume(r, r.period.volume), auditField: false },
  { key: 'tier', title: 'Bậc đạt', width: 70, type: 'quantity', value: (r) => r.period.tierNo, auditField: false },
  { key: 'percent', title: '% CK', width: 70, value: (r) => formatPercent(r.period.percent), auditField: false },
  { key: 'rebate', title: 'CK phải nhận', width: 120, type: 'money', value: (r) => r.period.rebateAmount, auditField: false },
  { key: 'next', title: 'Bậc kế', width: 65, type: 'quantity', value: (r) => r.period.nextTierNo, auditField: false },
  { key: 'shortfall', title: 'Còn thiếu lên bậc kế', width: 140, value: (r) => trackingVolume(r, r.period.shortfall), auditField: false },
  { key: 'forecast', title: 'Dự báo cuối kỳ', width: 130, value: (r) => trackingVolume(r, r.period.forecast), auditField: false },
  { key: 'targetTier', title: 'Bậc mục tiêu', width: 90, type: 'quantity', value: (r) => r.targetTierNo, auditField: false },
  {
    key: 'target', title: 'So bậc mục tiêu', width: 150, type: 'status', groupable: true, auditField: false,
    value: (r) => volumeRebateTargetLabels[r.period.targetStatus], tone: (r) => volumeRebateTargetTone[r.period.targetStatus],
  },
  {
    key: 'near', title: 'Sắp chạm bậc', width: 110, type: 'status', auditField: false,
    value: (r) => (r.period.isNearNextTier ? `Sắp chạm bậc ${r.period.nextTierNo}` : ''), tone: (r) => (r.period.isNearNextTier ? 'warning' : 'neutral'),
  },
  { key: 'received', title: 'Đã nhận kỳ này', width: 120, type: 'money', value: (r) => r.period.receivedAmount, auditField: false },
  {
    key: 'unreceived', title: 'Thiếu thực nhận quá hạn', width: 170, type: 'status', auditField: false,
    value: (r) => (r.unreceivedPeriods > 0 ? `${r.unreceivedPeriods} kỳ – ${formatMoney(r.unreceivedAmount)}` : ''),
    tone: (r) => (r.unreceivedPeriods > 0 ? 'error' : 'neutral'),
  },
]

/**
 * Giá và NCC → Theo dõi bậc chiết khấu (CKBT – design.md mục 18.25, lát G6a): kỳ hiện tại của mọi thỏa thuận hiệu lực (kể cả vừa hết hạn
 * trong 31 ngày) – lũy kế gộp, bậc đạt, còn thiếu lên bậc kế, dự báo cuối kỳ; cảnh báo sắp chạm bậc và nguy cơ/hụt bậc mục tiêu.
 */
export function VolumeRebateTrackingPage() {
  const navigate = useNavigate()
  const [near, setNear] = useState(DEFAULT_NEAR)
  const [onlyAlerts, setOnlyAlerts] = useState(false)
  const tracking = useVolumeRebateTracking(near, onlyAlerts)
  const rows = tracking.data ?? []
  const alerts = rows.filter(isTrackingAlert).length
  return (
    <ListPage
      title="Theo dõi bậc chiết khấu sản lượng gộp"
      exportFileName="theo-doi-bac-chiet-khau"
      exportSubtitle={`Cảnh báo sắp chạm bậc từ ${100 - near}% ngưỡng`}
      canCreate={false}
      rows={rows}
      columns={trackingColumns}
      rowKey={(r) => r.agreementId}
      loading={tracking.isLoading}
      onOpenRow={(r) => navigate(`${VOLUME_REBATE_PATH}/${r.agreementId}`)}
      isMarked={isTrackingAlert}
      notice={
        tracking.error ? (
          <Alert type="error" showIcon title={tracking.error.message} />
        ) : (
          <Alert
            type={alerts > 0 ? 'warning' : 'info'}
            showIcon
            title={`${alerts.toLocaleString('vi-VN')} / ${rows.length.toLocaleString('vi-VN')} thỏa thuận cần chú ý (sắp chạm bậc, nguy cơ hụt, đã hụt bậc mục tiêu hoặc thiếu thực nhận quá hạn). Lũy kế theo PO đã duyệt/đã đóng của các công ty áp dụng; dự báo theo nhịp mua từ đầu kỳ.`}
          />
        )
      }
      onClearFilters={() => {
        setNear(DEFAULT_NEAR)
        setOnlyAlerts(false)
      }}
      filters={
        <>
          <FilterField label="X% sắp chạm bậc" width={130}>
            {(id) => (
              <InputNumber {...vnNumberInput} id={id} min={1} max={50} precision={0} value={near} style={{ width: '100%' }}
                onChange={(v) => setNear(typeof v === 'number' && v >= 1 ? v : DEFAULT_NEAR)} />
            )}
          </FilterField>
          <FilterField label="Phạm vi" width={170}>
            {(id) => (
              <div id={id} style={{ display: 'flex', alignItems: 'center', height: '100%' }}>
                <Checkbox checked={onlyAlerts} onChange={(e) => setOnlyAlerts(e.target.checked)}>
                  Chỉ dòng có cảnh báo
                </Checkbox>
              </div>
            )}
          </FilterField>
        </>
      }
    />
  )
}

/** Lưới theo dõi bậc chỉ xem (cùng cột màn hình CKBT) – dùng ở báo cáo tháng cho Ban (lát G12). */
export function VolumeRebateTrackingGrid({ rows }: { rows: VolumeRebateTrackingRow[] }) {
  return <DataGrid rows={rows} columns={trackingColumns} rowKey={(r) => r.agreementId} groupBy={null} onGroupByChange={() => {}} isMarked={isTrackingAlert} />
}
