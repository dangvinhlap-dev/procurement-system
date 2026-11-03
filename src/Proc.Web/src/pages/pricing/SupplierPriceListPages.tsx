import { useDocumentFormSync } from '../../components/DocumentForm/useDocumentFormSync'
import { CheckOutlined, CloseCircleOutlined, CloseOutlined, RollbackOutlined, SaveOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import { Alert, App, Button, Checkbox, DatePicker, Flex, Form, Input, InputNumber } from 'antd'
import { Select } from '../../classic/Select'
import dayjs, { type Dayjs } from 'dayjs'
import { useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { useItemLookup } from '../../api/inventoryDocuments'
import { useSuppliers, vatOptions } from '../../api/masterData'
import {
  priceListImportSource,
  priceListStatusLabels,
  priceListStatusTone,
  usePriceList,
  usePriceListMutations,
  usePriceLists,
  type PriceList,
  type PriceListListItem,
  type SavePriceListInput,
} from '../../api/pricing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { DocumentForm } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { ListPage } from '../../components/ListPage/ListPage'
import { withPendingApprovers } from '../../api/purchasing'
import { formatDateTime, formatMoney, vnNumberInput, DATE_INPUT_FORMATS } from '../../utils/format'
import { ApprovalHistory } from '../purchasing/ApprovalHistory'
import { approveContent, confirmReject, confirmSubmitWithPreview } from '../purchasing/useApprovalCommands'

const LIST_PATH = '/gia-ncc/bang-gia'

const listColumns: GridColumn<PriceListListItem>[] = [
  { key: 'no', title: 'Số bảng giá', width: 150, value: (r) => r.no },
  { key: 'title', title: 'Tên bảng giá', width: 220, value: (r) => r.title },
  { key: 'supplier', title: 'Nhà cung cấp', width: 230, value: (r) => `${r.supplierCode} – ${r.supplierName}`, groupable: true },
  { key: 'scope', title: 'Áp dụng', width: 110, value: (r) => (r.isGroupWide ? 'Toàn tập đoàn' : r.companyCode), groupable: true },
  { key: 'from', title: 'Hiệu lực từ', width: 95, type: 'date', value: (r) => r.validFrom },
  { key: 'to', title: 'Đến', width: 95, type: 'date', value: (r) => r.validTo },
  { key: 'lines', title: 'Số dòng', width: 70, type: 'quantity', value: (r) => r.lineCount },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 130,
    type: 'status',
    // Sắp hết hiệu lực (còn ≤ 30 ngày – lát B1): nhãn vàng có chữ, không chỉ đổi màu (ui-guide mục 2).
    value: (r) => (r.expiringSoon ? 'Sắp hết hiệu lực' : priceListStatusLabels[r.status]),
    tone: (r) => (r.expiringSoon ? 'warning' : priceListStatusTone[r.status]),
    groupable: true,
  },
]

/** Màn hình Giá và NCC → Bảng giá NCC (design.md mục 8.1): bảng giá của công ty và bảng giá toàn tập đoàn (chỉ xem). */
export function PriceListListPage() {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const lists = usePriceLists()
  return (
    <ListPage
      title="Bảng giá nhà cung cấp"
      exportFileName="bang-gia-ncc"
      rows={lists.data ?? []}
      columns={listColumns}
      rowKey={(r) => r.id}
      auditEntity="SupplierPriceList"
      loading={lists.isLoading}
      canCreate={hasPermission(Permission.PriceListManage)}
      onCreate={() => navigate(`${LIST_PATH}/moi`)}
      onOpenRow={(r) => navigate(`${LIST_PATH}/${r.id}`)}
      excelImport={{ source: priceListImportSource, title: 'bảng giá NCC', canImport: hasPermission(Permission.PriceListManage) }}
    />
  )
}

interface LineValues {
  id: string | null
  itemId?: string
  uomId?: string
  minQty?: number | null
  unitPrice?: number | null
  discountPct?: number | null
  vatCode?: string | null
  note?: string
}

interface FormValues {
  supplierId?: string
  title?: string
  isGroupWide?: boolean
  validFrom: Dayjs
  validTo?: Dayjs | null
  deliveryTerms?: string
  note?: string
  lines: LineValues[]
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Thông báo đầu phiếu bảng giá: lý do bị từ chối (đang nháp), sắp hết hiệu lực (còn ≤ 30 ngày – lát B1), lý do chỉ xem.
 * @param doc Bảng giá đã tải.
 */
function PriceListNotices({ doc }: { doc: PriceList }) {
  const notices = [
    doc.rejectReason && doc.status === 'Draft' && <Alert key="reject" type="warning" showIcon title={`Bị từ chối: ${doc.rejectReason}`} />,
    doc.expiringSoon && (
      <Alert
        key="expiry"
        type="warning"
        showIcon
        title={doc.daysToExpiry === 0 ? 'Bảng giá hết hiệu lực hôm nay.' : `Bảng giá sắp hết hiệu lực – còn ${doc.daysToExpiry} ngày.`}
        description="Đàm phán và lập bảng giá mới để đơn mua hàng tiếp tục có giá gợi ý."
      />
    ),
    doc.actions.readOnlyReason && <Alert key="readonly" type="info" showIcon title={doc.actions.readOnlyReason} />,
  ].filter(Boolean)
  return notices.length ? <Flex vertical gap={8}>{notices}</Flex> : null
}

/**
 * Trang bảng giá NCC trên khuôn DocumentForm: NCC, tên, hiệu lực, (tổng công ty) áp dụng toàn tập đoàn; dòng: mặt hàng, ĐVT, bậc SL,
 * đơn giá, % CK, VAT và giá quy về ĐVT cơ sở. Nháp → Gửi duyệt → (duyệt đủ cấp) Hiệu lực → Ngừng; từ chối hoặc rút lại về Nháp;
 * bảng đã gửi duyệt/hiệu lực không sửa (lát B1 – design.md mục 8.5).
 */
export function SupplierPriceListPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const { hasPermission, workingCompany } = useAuth()
  const [form] = Form.useForm<FormValues>()
  const list = usePriceList(isNew ? null : id)
  const mutations = usePriceListMutations()
  const suppliers = useSuppliers({})
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const addRef = useRef<(() => void) | null>(null)
  const doc = list.data
  const readOnly = isNew ? !hasPermission(Permission.PriceListManage) : !doc?.actions.canEdit
  // Đính kèm được cả khi phiếu đã duyệt/ghi sổ (scan hóa đơn, biên bản) – quyền theo quyền lập phiếu, design.md mục 18.4.
  const attachmentTab = useAttachmentTab('SupplierPriceList', doc?.id, hasPermission(Permission.PriceListManage))

  const watchedLines = Form.useWatch('lines', form) as LineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])

  const { loadSaved, targetId } = useDocumentFormSync(form, id, doc, (doc) => ({
    supplierId: doc.supplierId,
    title: doc.title,
    isGroupWide: doc.isGroupWide,
    validFrom: dayjs(doc.validFrom),
    validTo: doc.validTo ? dayjs(doc.validTo) : null,
    deliveryTerms: doc.deliveryTerms ?? '',
    note: doc.note ?? '',
    lines: doc.lines.map((l) => ({
      id: l.id, itemId: l.itemId, uomId: l.uomId, minQty: l.minQty, unitPrice: l.unitPrice, discountPct: l.discountPct, vatCode: l.vatCode, note: l.note ?? '',
    })),
  }))

  const persist = async (): Promise<PriceList | null> => {
    let values: FormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng giá.')
      return null
    }
    setError(null)
    const input: SavePriceListInput = {
      supplierId: values.supplierId!,
      title: values.title?.trim() ?? '',
      isGroupWide: values.isGroupWide ?? false,
      validFrom: values.validFrom.format('YYYY-MM-DD'),
      validTo: values.validTo ? values.validTo.format('YYYY-MM-DD') : null,
      deliveryTerms: orNull(values.deliveryTerms),
      note: orNull(values.note),
      lines: (values.lines ?? []).map((l) => ({
        id: l.id ?? null, itemId: l.itemId!, uomId: l.uomId!, minQty: l.minQty ?? 0, unitPrice: l.unitPrice ?? 0, discountPct: l.discountPct ?? 0,
        vatCode: l.vatCode ?? null, note: orNull(l.note),
      })),
    }
    try {
      const target = targetId()
      const saved = target ? await mutations.update.mutateAsync({ id: target, input }) : await mutations.create.mutateAsync(input)
      loadSaved(saved)
      return saved
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được bảng giá.')
      return null
    }
  }

  const save = async () => {
    if (readOnly) return
    const saved = await persist()
    if (!saved) return
    message.success(`Đã lưu bảng giá ${saved.no}.`)
    if (isNew) navigate(`${LIST_PATH}/${saved.id}`, { replace: true })
  }

  const fail = (e: unknown, fallback: string) => setError(e instanceof Error ? e.message : fallback)

  // Bảng giá phải duyệt mới hiệu lực, người lập không tự duyệt (lát B1 – design.md mục 8.5, 18.6).
  // Lưu, xem trước chuỗi duyệt (popup liệt kê người duyệt) rồi mới gửi – yêu cầu người dùng 03/11/2026.
  const submit = () => {
    setError(null)
    void confirmSubmitWithPreview(modal, {
      title: 'Gửi duyệt bảng giá? Duyệt xong mới có hiệu lực.',
      docType: 'PRL',
      prepare: async () => {
        const saved = await persist()
        if (!saved) return null
        if (isNew) navigate(`${LIST_PATH}/${saved.id}`, { replace: true })
        return { id: saved.id }
      },
      onSubmit: async (id) => {
        try {
          const sent = await mutations.submit.mutateAsync({ id })
          message.success(sent.status === 'Active' ? `Bảng giá ${sent.no} đã có hiệu lực.` : `Đã gửi duyệt bảng giá ${sent.no}.`)
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
      title: `Duyệt bảng giá ${doc.no}?`,
      content: (
        <>
          <p>Duyệt cấp cuối thì bảng giá có hiệu lực: đơn mua hàng của NCC này gợi ý giá từ bảng giá.</p>
          {approveContent(doc)}
        </>
      ),
      okText: 'Duyệt',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        try {
          const result = await mutations.approve.mutateAsync({ id: doc.id })
          message.success(result.status === 'Active' ? `Đã duyệt xong – bảng giá ${doc.no} đã có hiệu lực.` : `Đã duyệt cấp của bạn – bảng giá ${doc.no} chuyển cấp tiếp theo.`)
        } catch (e) {
          fail(e, 'Không duyệt được.')
        }
      },
    })

  const reject = () =>
    doc &&
    confirmReject(modal, message, `Từ chối bảng giá ${doc.no}?`, async (reason) => {
      setError(null)
      try {
        await mutations.reject.mutateAsync({ id: doc.id, comment: reason })
        message.success(`Đã từ chối bảng giá ${doc.no}.`)
      } catch (e) {
        fail(e, 'Không từ chối được.')
      }
    })

  const recall = () =>
    doc &&
    modal.confirm({
      title: `Rút bảng giá ${doc.no} về nháp?`,
      content: 'Các cấp đang chờ duyệt bỏ qua; sửa xong thì gửi duyệt lại từ đầu.',
      okText: 'Rút về nháp',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        try {
          await mutations.recall.mutateAsync({ id: doc.id })
          message.success(`Đã rút bảng giá ${doc.no} về nháp.`)
        } catch (e) {
          fail(e, 'Không rút về nháp được.')
        }
      },
    })

  const deactivate = () =>
    doc &&
    modal.confirm({
      title: `Ngừng áp dụng bảng giá ${doc.no}?`,
      okText: 'Ngừng áp dụng',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        try {
          await mutations.deactivate.mutateAsync(doc.id)
          message.success(`Đã ngừng bảng giá ${doc.no}.`)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không ngừng được bảng giá.')
        }
      },
    })

  /** Giá quy về ĐVT cơ sở, trước VAT = đơn giá × (100 − %CK) / 100 / hệ số ĐVT (như server). */
  const baseNet = (line: LineValues | undefined) => {
    const item = line?.itemId ? itemsById.get(line.itemId) : undefined
    const factor = item?.uoms.find((u) => u.uomId === line?.uomId)?.factor ?? 1
    return ((line?.unitPrice ?? 0) * (100 - (line?.discountPct ?? 0))) / 100 / factor
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
      key: 'minQty',
      title: 'Từ SL',
      width: 85,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'minQty']} noStyle rules={[{ type: 'number', min: 0, message: '≥ 0.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={4} aria-label={`Từ SL dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'price',
      title: 'Đơn giá', required: true,
      width: 115,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'unitPrice']} noStyle rules={[{ required: true, type: 'number', min: 0, message: 'Nhập đơn giá.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} aria-label={`Đơn giá dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'discount',
      title: '% CK',
      width: 65,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'discountPct']} noStyle rules={[{ type: 'number', min: 0, max: 100, message: '0–100.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} max={100} aria-label={`Chiết khấu dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'vat',
      title: 'VAT',
      width: 75,
      render: (field) => (
        <Form.Item name={[field.name, 'vatCode']} noStyle>
          <Select variant="borderless" style={{ width: '100%' }} allowClear placeholder="Theo hàng" aria-label={`VAT dòng ${field.name + 1}`}
            options={vatOptions.map((o) => ({ value: o.value, label: o.value === 'KCT' ? 'KCT' : o.label }))} />
        </Form.Item>
      ),
    },
    {
      key: 'base',
      title: 'Giá quy đổi',
      width: 115,
      align: 'right',
      render: (field) => <span className="line-grid-readonly">{formatMoney(baseNet(lines[field.name]))}</span>,
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
    : { label: doc ? withPendingApprovers(priceListStatusLabels[doc.status], doc.approvals) : '', tone: doc ? priceListStatusTone[doc.status] : ('neutral' as const) }

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
        <Button icon={<RollbackOutlined />} onClick={recall} loading={mutations.recall.isPending}>
          Rút về nháp
        </Button>
      )}
      {doc?.actions.canDeactivate && (
        <Button danger icon={<StopOutlined />} onClick={deactivate} loading={mutations.deactivate.isPending}>
          Ngừng áp dụng
        </Button>
      )}
      <Button type="text" icon={<CloseOutlined />} onClick={() => navigate(LIST_PATH)}>
        Đóng
      </Button>
    </>
  )

  return (
    <DocumentForm
      title="Bảng giá nhà cung cấp"
      extraTabs={[
        attachmentTab,
        ...(doc ? [{ key: 'approvals', label: 'Lịch sử duyệt', children: <ApprovalHistory steps={doc.approvals} createdByName={doc.createdByName} createdAt={doc.createdAt} /> }] : []),
      ]}
      number={isNew ? null : (doc?.no ?? '')}
      audit={{ entityType: 'SupplierPriceList', id: doc?.id }}
      status={status}
      meta={doc && `Công ty lập: ${doc.companyCode} · Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
      loading={list.isLoading}
      error={error ?? (list.error ? list.error.message : null)}
      notice={doc && <PriceListNotices doc={doc} />}
      form={form}
      initialValues={{ validFrom: dayjs(), isGroupWide: false, lines: [] }}
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
                .filter((s) => s.status !== 'Blacklisted' || s.id === doc?.supplierId)
                .map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))}
            />
          </Form.Item>
          <Form.Item label="Tên bảng giá" name="title" rules={[{ required: true, message: 'Nhập tên bảng giá.' }, { max: 200 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Hiệu lực từ" name="validFrom" rules={[{ required: true, message: 'Chọn ngày.' }]}>
            <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Hiệu lực đến" name="validTo" extra="Để trống = tới khi ngừng áp dụng.">
            <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Điều kiện giao hàng" name="deliveryTerms" rules={[{ max: 200 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]}>
            <Input />
          </Form.Item>
          {(workingCompany?.isHeadOffice || doc?.isGroupWide) && (
            <Form.Item label="Phạm vi" name="isGroupWide" valuePropName="checked" extra="Mọi công ty trong tập đoàn dùng được giá này.">
              <Checkbox>Áp dụng toàn tập đoàn</Checkbox>
            </Form.Item>
          )}
        </>
      }
      lines={
        <LineGrid name="lines" auditEntity="SupplierPriceListLine" columns={columns} readOnly={readOnly} addRef={addRef}
          newLine={() => ({ id: null, minQty: 0, unitPrice: 0, discountPct: 0 })} />
      }
    />
  )
}
