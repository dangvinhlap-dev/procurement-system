import { FileSearchOutlined, LinkOutlined } from '@ant-design/icons'
import { Alert, App, Button, ConfigProvider, DatePicker, Form, Input, InputNumber, Modal, Select } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useParams } from 'react-router'
import { useItemLookup, useSupplierLookup } from '../../api/inventoryDocuments'
import { useUoms } from '../../api/masterData'
import { useDepartments, useWarehouses } from '../../api/organization'
import {
  priorityLabels,
  purchaseStatusLabels,
  purchaseStatusTone,
  usePurchaseMutations,
  usePurchaseRequest,
  usePrNewItemActions,
  type PurchasePriority,
  type PurchaseRequest,
  type SavePurchaseRequestInput,
} from '../../api/purchasing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { DocumentForm, DocumentTotal } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { formatDateTime, formatMoney, formatQuantity, vnNumberInput, DATE_INPUT_FORMATS } from '../../utils/format'
import { ApprovalHistory } from './ApprovalHistory'
import { useApprovalCommands } from './useApprovalCommands'

interface PrLineValues {
  id: string | null
  itemId?: string
  uomId?: string
  qty?: number | null
  suggestedSupplierId?: string
  neededDate?: Dayjs | null
  note?: string
}

/** Dòng hàng mới chưa có mã trên form (design.md mục 18.12). */
interface PrNewItemValues {
  id: string | null
  description?: string
  specification?: string
  uomId?: string
  qty?: number | null
  estPrice?: number | null
  neededDate?: Dayjs | null
  note?: string
}

interface PrFormValues {
  date: Dayjs
  departmentId?: string
  purpose?: string
  neededDate?: Dayjs | null
  warehouseId?: string
  priority: PurchasePriority
  note?: string
  lines: PrLineValues[]
  newItems: PrNewItemValues[]
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Trang đề nghị mua hàng (design.md mục 10.2) trên khuôn DocumentForm. Đơn giá tham khảo = giá nhập gần nhất do server lấy khi lưu
 * (chỉ đọc); tồn hiện tại chỉ để xem. Gửi duyệt theo quy trình duyệt của công ty; tab Lịch sử duyệt.
 */
export function PurchaseRequestPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<PrFormValues>()
  const request = usePurchaseRequest(isNew ? null : id)
  const mutations = usePurchaseMutations<PurchaseRequest, SavePurchaseRequestInput>('purchase-requests')
  const warehouses = useWarehouses()
  const departments = useDepartments()
  const uoms = useUoms()
  const newItemActions = usePrNewItemActions()
  const { message } = App.useApp()
  const [linking, setLinking] = useState<{ newItemId: string; label: string } | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const addRef = useRef<(() => void) | null>(null)
  const doc = request.data
  const readOnly = isNew ? !hasPermission(Permission.PrCreate) : !doc?.actions.canEdit
  // Đính kèm được cả khi phiếu đã duyệt/ghi sổ (scan hóa đơn, biên bản) – quyền theo quyền lập phiếu, design.md mục 18.4.
  const attachmentTab = useAttachmentTab('PurchaseRequest', doc?.id, hasPermission(Permission.PrCreate))

  const watchedLines = Form.useWatch('lines', form) as PrLineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])
  const supplierIds = useMemo(() => (doc?.lines ?? []).map((l) => l.suggestedSupplierId).filter((x): x is string => !!x), [doc])
  const suppliers = useSupplierLookup(supplierIds)

  // Chỉ chọn bộ phận đang dùng; bộ phận phiếu đã chọn nay ngừng dùng vẫn giữ trong danh sách để hiện tên (design.md mục 18.5).
  const departmentOptions = useMemo(
    () =>
      (departments.data ?? [])
        .filter((d) => d.isActive || d.id === doc?.departmentId)
        .map((d) => ({ value: d.id, label: `${d.code} – ${d.name}${d.isActive ? '' : ' (ngừng dùng)'}` })),
    [departments.data, doc?.departmentId],
  )
  // PR lập trước khi có danh mục bộ phận chỉ có tên dạng chữ (cột cũ, chỉ đọc).
  const legacyDepartment = doc && !doc.departmentId ? doc.department : null

  useEffect(() => {
    if (!doc) return
    form.setFieldsValue({
      date: dayjs(doc.date),
      departmentId: doc.departmentId ?? undefined,
      purpose: doc.purpose ?? '',
      neededDate: doc.neededDate ? dayjs(doc.neededDate) : null,
      warehouseId: doc.warehouseId ?? undefined,
      priority: doc.priority,
      note: doc.note ?? '',
      lines: doc.lines.map((l) => ({
        id: l.id, itemId: l.itemId, uomId: l.uomId, qty: l.qty, suggestedSupplierId: l.suggestedSupplierId ?? undefined,
        neededDate: l.neededDate ? dayjs(l.neededDate) : null, note: l.note ?? '',
      })),
      newItems: doc.newItems.map((n) => ({
        id: n.id, description: n.description, specification: n.specification ?? '', uomId: n.uomId, qty: n.qty, estPrice: n.estPrice,
        neededDate: n.neededDate ? dayjs(n.neededDate) : null, note: n.note ?? '',
      })),
    })
  }, [doc, form])

  const persist = async (): Promise<PurchaseRequest | null> => {
    let values: PrFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng hàng.')
      return null
    }
    setError(null)
    const input: SavePurchaseRequestInput = {
      date: values.date.format('YYYY-MM-DD'),
      departmentId: values.departmentId ?? null,
      purpose: orNull(values.purpose),
      neededDate: values.neededDate ? values.neededDate.format('YYYY-MM-DD') : null,
      warehouseId: values.warehouseId ?? null,
      priority: values.priority,
      note: orNull(values.note),
      lines: (values.lines ?? []).map((l) => ({
        id: l.id ?? null, itemId: l.itemId!, uomId: l.uomId!, qty: l.qty ?? 0, suggestedSupplierId: l.suggestedSupplierId ?? null,
        neededDate: l.neededDate ? l.neededDate.format('YYYY-MM-DD') : null, note: orNull(l.note),
      })),
      newItems: (values.newItems ?? []).map((n) => ({
        id: n.id ?? null, description: n.description?.trim() ?? '', specification: orNull(n.specification), uomId: n.uomId!, qty: n.qty ?? 0,
        estPrice: n.estPrice ?? 0, neededDate: n.neededDate ? n.neededDate.format('YYYY-MM-DD') : null, note: orNull(n.note),
      })),
    }
    try {
      return isNew ? await mutations.create.mutateAsync(input) : await mutations.update.mutateAsync({ id, input })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được phiếu.')
      return null
    }
  }

  const { save, actions } = useApprovalCommands({
    isNew, doc, readOnly, persist, mutations, listPath: '/mua-hang/pr', noun: 'đề nghị mua hàng', setError,
  })

  /** Dòng đã lưu tương ứng (để hiện giá tham khảo, tồn, SL đã đặt do server tính). */
  const savedLine = (index: number) => (lines[index]?.id ? doc?.lines.find((l) => l.id === lines[index].id) : undefined)
  const readonlyCell = (text: string) => <span className="line-grid-readonly">{text}</span>

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
      width: 80,
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
      title: 'SL đề nghị', required: true,
      width: 100,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'qty']} noStyle rules={[{ required: true, type: 'number', min: 0.0001, message: 'SL > 0.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={4} aria-label={`SL đề nghị dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    { key: 'onHand', title: 'Tồn hiện tại', width: 95, align: 'right', render: (_, index) => readonlyCell(savedLine(index) ? formatQuantity(savedLine(index)!.onHandQty) : '') },
    // Hàng đang đặt (PO đã duyệt chưa nhận đủ) – kiểm tra trước khi đề nghị thêm (design.md mục 18.13, V2).
    { key: 'onOrder', title: 'Đang đặt', width: 90, align: 'right', render: (_, index) => readonlyCell(savedLine(index) ? formatQuantity(savedLine(index)!.onOrderQty) : '') },
    { key: 'price', title: 'Giá tham khảo', width: 115, align: 'right', render: (_, index) => readonlyCell(savedLine(index) ? formatMoney(savedLine(index)!.estPrice) : 'Tự lấy khi lưu') },
    { key: 'amount', title: 'Thành tiền dự kiến', width: 125, align: 'right', render: (_, index) => readonlyCell(savedLine(index) ? formatMoney(savedLine(index)!.amount) : '') },
    {
      key: 'supplier',
      title: 'NCC đề xuất',
      width: 170,
      render: (field) => (
        <Form.Item name={[field.name, 'suggestedSupplierId']} noStyle>
          <Select
            variant="borderless"
            style={{ width: '100%' }}
            allowClear
            aria-label={`NCC đề xuất dòng ${field.name + 1}`}
            showSearch={{ optionFilterProp: 'label' }}
            options={(suppliers.data ?? []).map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))}
            popupMatchSelectWidth={320}
          />
        </Form.Item>
      ),
    },
    {
      key: 'needed',
      title: 'Ngày cần',
      width: 115,
      render: (field) => (
        <Form.Item name={[field.name, 'neededDate']} noStyle>
          <DatePicker variant="borderless" format={DATE_INPUT_FORMATS} style={{ width: '100%' }} aria-label={`Ngày cần dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    { key: 'ordered', title: 'Đã đặt', width: 80, align: 'right', render: (_, index) => readonlyCell(savedLine(index)?.orderedQty ? formatQuantity(savedLine(index)!.orderedQty) : '') },
    {
      key: 'note',
      title: 'Ghi chú',
      width: 150,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle>
          <Input variant="borderless" maxLength={500} aria-label={`Ghi chú dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const watchedNewItems = Form.useWatch('newItems', form) as PrNewItemValues[] | undefined
  const newItemCount = watchedNewItems?.length ?? doc?.newItems.length ?? 0
  const savedNewItem = (index: number) => (watchedNewItems?.[index]?.id ? doc?.newItems.find((n) => n.id === watchedNewItems[index].id) : undefined)

  /** Lập phiếu tìm nguồn cho dòng hàng mới của PR đã duyệt. */
  const createSourcing = async (newItemId: string) => {
    if (!doc) return
    try {
      const updated = await newItemActions.createSourcing.mutateAsync({ requestId: doc.id, newItemId })
      message.success(`Đã lập phiếu tìm nguồn ${updated.newItems.find((n) => n.id === newItemId)?.sourcingNo ?? ''}.`)
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Không lập được phiếu tìm nguồn.')
    }
  }

  // Nút xử lý hàng mới hiện khi PR đã duyệt (form chỉ đọc) – bỏ khóa của form cho riêng các nút này.
  const newItemColumns: LineColumn[] = [
    {
      key: 'description', title: 'Mô tả hàng', required: true, width: 240,
      render: (field) => (
        <>
          <Form.Item name={[field.name, 'id']} hidden>
            <Input />
          </Form.Item>
          <Form.Item name={[field.name, 'description']} noStyle rules={[{ required: true, whitespace: true, message: 'Nhập mô tả.' }, { max: 500 }]}>
            <Input variant="borderless" aria-label={`Mô tả hàng mới dòng ${field.name + 1}`} />
          </Form.Item>
        </>
      ),
    },
    {
      key: 'spec', title: 'Quy cách', width: 180,
      render: (field) => (
        <Form.Item name={[field.name, 'specification']} noStyle rules={[{ max: 1000 }]}>
          <Input variant="borderless" aria-label={`Quy cách hàng mới dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'uom', title: 'ĐVT', required: true, width: 80,
      render: (field) => (
        <Form.Item name={[field.name, 'uomId']} noStyle rules={[{ required: true, message: 'Chọn ĐVT.' }]}>
          <Select variant="borderless" style={{ width: '100%' }} aria-label={`ĐVT hàng mới dòng ${field.name + 1}`}
            options={(uoms.data ?? []).map((u) => ({ value: u.id, label: u.code }))} />
        </Form.Item>
      ),
    },
    {
      key: 'qty', title: 'SL', required: true, width: 80, align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'qty']} noStyle rules={[{ required: true, type: 'number', min: 0.0001, message: 'SL > 0.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={4} aria-label={`SL hàng mới dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'price', title: 'Giá dự kiến', width: 110, align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'estPrice']} noStyle rules={[{ type: 'number', min: 0, message: '≥ 0.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} aria-label={`Giá dự kiến hàng mới dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'amount', title: 'Thành tiền', width: 110, align: 'right',
      render: (_, index) => (
        <span className="line-grid-readonly">
          {formatMoney(Math.round((watchedNewItems?.[index]?.qty ?? 0) * (watchedNewItems?.[index]?.estPrice ?? 0)))}
        </span>
      ),
    },
    {
      key: 'needed', title: 'Ngày cần', width: 115,
      render: (field) => (
        <Form.Item name={[field.name, 'neededDate']} noStyle>
          <DatePicker variant="borderless" format={DATE_INPUT_FORMATS} style={{ width: '100%' }} aria-label={`Ngày cần hàng mới dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'sourcing', title: 'Phiếu tìm nguồn', width: 150,
      render: (_, index) => {
        const saved = savedNewItem(index)
        if (saved?.sourcingRequestId) return <Link to={`/dau-thau/tim-nguon/${saved.sourcingRequestId}`}>{saved.sourcingNo}</Link>
        return saved && doc?.canCreateSourcing && !saved.itemId ? (
          <ConfigProvider componentDisabled={false}>
            <Button size="small" icon={<FileSearchOutlined />} loading={newItemActions.createSourcing.isPending} onClick={() => void createSourcing(saved.id)}>
              Tìm nguồn
            </Button>
          </ConfigProvider>
        ) : null
      },
    },
    {
      key: 'item', title: 'Mã hàng đã gắn', width: 140,
      render: (_, index) => {
        const saved = savedNewItem(index)
        if (saved?.itemCode) return <span className="line-grid-readonly">{saved.itemCode}</span>
        return saved && doc?.canLinkItem ? (
          <ConfigProvider componentDisabled={false}>
            <Button size="small" icon={<LinkOutlined />} onClick={() => setLinking({ newItemId: saved.id, label: saved.description })}>
              Gắn mã
            </Button>
          </ConfigProvider>
        ) : null
      },
    },
  ]

  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? purchaseStatusLabels[doc.status] : '', tone: doc ? purchaseStatusTone[doc.status] : ('neutral' as const) }

  return (
    <>
    <DocumentForm
      title="Đề nghị mua hàng"
      number={isNew ? null : (doc?.no ?? '')}
      printPath={doc ? `/api/purchase-requests/${doc.id}/pdf` : undefined}
      status={status}
      meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
      loading={request.isLoading}
      error={error ?? (request.error ? request.error.message : null)}
      notice={
        doc?.rejectReason && doc.status === 'Draft' ? (
          <Alert type="warning" showIcon title={`Bị từ chối: ${doc.rejectReason}`} />
        ) : (
          doc?.actions.readOnlyReason && <Alert type="info" showIcon title={doc.actions.readOnlyReason} />
        )
      }
      form={form}
      initialValues={{ date: dayjs(), priority: 'Normal', lines: [] }}
      readOnly={readOnly}
      onSave={save}
      onAddLine={() => addRef.current?.()}
      actions={actions}
      header={
        <>
          <Form.Item label="Ngày đề nghị" name="date" rules={[{ required: true, message: 'Chọn ngày.' }]}>
            <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item
            label="Bộ phận đề nghị"
            name="departmentId"
            rules={[{ required: true, message: 'Chọn bộ phận đề nghị.' }]}
            extra={legacyDepartment ? `Ghi trước khi có danh mục: ${legacyDepartment}` : undefined}
          >
            <Select
              allowClear
              showSearch={{ optionFilterProp: 'label' }}
              options={departmentOptions}
              loading={departments.isLoading}
              placeholder="Chọn bộ phận"
            />
          </Form.Item>
          <Form.Item label="Mức ưu tiên" name="priority">
            <Select options={Object.entries(priorityLabels).map(([value, label]) => ({ value, label }))} />
          </Form.Item>
          <Form.Item label="Mục đích sử dụng" name="purpose" rules={[{ max: 500 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Ngày cần hàng" name="neededDate">
            <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Kho nhận dự kiến" name="warehouseId">
            <Select allowClear showSearch={{ optionFilterProp: 'label' }}
              options={(warehouses.data ?? []).filter((w) => w.isActive || w.id === doc?.warehouseId).map((w) => ({ value: w.id, label: `${w.code} – ${w.name}` }))} />
          </Form.Item>
          <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]} className="document-form-wide">
            <Input />
          </Form.Item>
        </>
      }
      lines={<LineGrid name="lines" columns={columns} readOnly={readOnly} addRef={addRef} newLine={() => ({ id: null, qty: 1 })} />}
      footer={<DocumentTotal label="Tổng giá trị dự kiến" value={formatMoney(doc?.totalAmount ?? 0)} strong />}
      extraTabs={[
          {
            key: 'newItems',
            label: `Hàng mới chưa có mã (${newItemCount})`,
            children: (
              <>
                <Alert type="info" showIcon style={{ marginBottom: 6 }}
                  title="Hàng chưa có trong danh mục: ghi mô tả, quy cách, ĐVT, SL, giá dự kiến. Duyệt cùng PR; sau duyệt lập phiếu tìm nguồn, có mã hàng thì gắn mã mới lên PO được." />
                <LineGrid name="newItems" columns={newItemColumns} readOnly={readOnly} newLine={() => ({ id: null, qty: 1, estPrice: 0 })} />
              </>
            ),
          },
          ...(doc ? [{ key: 'approvals', label: 'Lịch sử duyệt', children: <ApprovalHistory steps={doc.approvals} /> }] : []),
          attachmentTab,
        ]}
    />
    {linking && doc && (
      <LinkItemDialog
        label={linking.label}
        loading={newItemActions.linkItem.isPending}
        onCancel={() => setLinking(null)}
        onLink={async (itemId) => {
          try {
            await newItemActions.linkItem.mutateAsync({ requestId: doc.id, newItemId: linking.newItemId, itemId })
            message.success('Đã gắn mã hàng – dòng có mã đã thêm vào phiếu, lên PO được.')
            setLinking(null)
          } catch (e) {
            message.error(e instanceof Error ? e.message : 'Không gắn được mã hàng.')
          }
        }}
      />
    )}
    </>
  )
}

/**
 * Hộp chọn mặt hàng để gắn mã cho dòng hàng mới (design.md mục 18.12).
 * @param label Mô tả dòng hàng mới.
 * @param loading Đang gửi.
 * @param onLink Gắn với mặt hàng đã chọn.
 * @param onCancel Đóng.
 */
function LinkItemDialog({ label, loading, onLink, onCancel }: { label: string; loading: boolean; onLink: (itemId: string) => void; onCancel: () => void }) {
  const [search, setSearch] = useState('')
  const [itemId, setItemId] = useState<string | undefined>()
  const lookup = useItemLookup(search, itemId ? [itemId] : [], false)
  return (
    <Modal open title={`Gắn mã hàng cho: ${label}`} okText="Gắn mã" cancelText="Đóng" okButtonProps={{ disabled: !itemId, loading }}
      onOk={() => itemId && onLink(itemId)} onCancel={onCancel}>
      <label htmlFor="link-item">Mặt hàng (ĐVT của dòng phải là ĐVT cơ sở hoặc quy đổi của mặt hàng)</label>
      <Select id="link-item" aria-label="Mặt hàng gắn mã" style={{ width: '100%' }} value={itemId} onChange={setItemId}
        showSearch={{ filterOption: false, onSearch: setSearch }} placeholder="Gõ mã hoặc tên hàng"
        options={(lookup.data ?? []).map((i) => ({ value: i.id, label: `${i.code} – ${i.name}` }))} />
    </Modal>
  )
}
