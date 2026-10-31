import { CheckOutlined, CloseOutlined, FileSearchOutlined, SaveOutlined, StopOutlined } from '@ant-design/icons'
import { App, Button, DatePicker, Form, Input, InputNumber, Select } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { useItemLookup } from '../../api/inventoryDocuments'
import { useSuppliers } from '../../api/masterData'
import {
  candidateStatusLabels,
  sourcingStatusLabels,
  sourcingStatusTone,
  useSourcing,
  useSourcingList,
  useSourcingMutations,
  type CandidateStatus,
  type SaveSourcingInput,
  type Sourcing,
  type SourcingListItem,
} from '../../api/sourcing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { DocumentForm } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { ListPage } from '../../components/ListPage/ListPage'
import { formatDateTime, vnNumberInput } from '../../utils/format'

const LIST_PATH = '/dau-thau/tim-nguon'

const listColumns: GridColumn<SourcingListItem>[] = [
  { key: 'no', title: 'Số phiếu', width: 150, value: (r) => r.no },
  { key: 'date', title: 'Ngày', width: 90, type: 'date', value: (r) => r.date },
  { key: 'subject', title: 'Hàng cần tìm', width: 260, value: (r) => r.subject },
  { key: 'qty', title: 'SL dự kiến', width: 90, type: 'quantity', value: (r) => r.qty },
  { key: 'deadline', title: 'Hạn chót', width: 90, type: 'date', value: (r) => r.deadline },
  { key: 'responsible', title: 'Người phụ trách', width: 140, value: (r) => r.responsible, groupable: true },
  { key: 'candidates', title: 'Số NCC', width: 70, type: 'quantity', value: (r) => r.candidateCount },
  { key: 'passed', title: 'NCC đạt', width: 70, type: 'quantity', value: (r) => r.passedCount },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 100,
    type: 'status',
    value: (r) => sourcingStatusLabels[r.status],
    tone: (r) => sourcingStatusTone[r.status],
    groupable: true,
  },
]

/** Màn hình Đấu thầu → Tìm nguồn (design.md mục 8.3): các phiếu tìm NCC cho một sản phẩm. */
export function SourcingListPage() {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const list = useSourcingList()
  return (
    <ListPage
      title="Danh sách tìm nguồn"
      exportFileName="tim-nguon"
      rows={list.data ?? []}
      columns={listColumns}
      rowKey={(r) => r.id}
      loading={list.isLoading}
      canCreate={hasPermission(Permission.RfqManage)}
      onCreate={() => navigate(`${LIST_PATH}/moi`)}
      onOpenRow={(r) => navigate(`${LIST_PATH}/${r.id}`)}
    />
  )
}

interface CandidateValues {
  id: string | null
  supplierId?: string
  status?: CandidateStatus
  note?: string
}

interface FormValues {
  date: Dayjs
  itemId?: string | null
  description?: string
  specification?: string
  qty?: number | null
  deadline?: Dayjs | null
  responsible?: string
  note?: string
  candidates: CandidateValues[]
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Trang phiếu tìm nguồn trên khuôn DocumentForm: hàng cần tìm (mặt hàng hoặc mô tả), yêu cầu kỹ thuật, SL, hạn chót, người phụ trách;
 * tab NCC ứng viên với trạng thái Đã liên hệ → Đã gửi RFQ → Đã báo giá → Đạt / Không đạt (RFQ lập từ phiếu tự cập nhật hai bước giữa).
 * NCC chưa có trong danh mục: thêm ở Nhà cung cấp (trạng thái Tiềm năng) rồi chọn vào đây.
 */
export function SourcingPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<FormValues>()
  const sourcing = useSourcing(isNew ? null : id)
  const mutations = useSourcingMutations()
  const suppliers = useSuppliers({})
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const addRef = useRef<(() => void) | null>(null)
  const doc = sourcing.data
  const readOnly = isNew ? !hasPermission(Permission.RfqManage) : !doc?.canEdit
  const itemId = Form.useWatch('itemId', form) as string | undefined
  const description = Form.useWatch('description', form) as string | undefined
  // Phải có mặt hàng hoặc mô tả (Sourcing.Create ở Domain) – ô còn lại thôi bắt buộc khi đã nhập một trong hai.
  const needsItemOrDescription = !itemId && !description?.trim()
  const lookup = useItemLookup(search, itemId ? [itemId] : [])
  const watched = Form.useWatch('candidates', form) as CandidateValues[] | undefined
  const usedSuppliers = useMemo(() => new Set((watched ?? []).map((c) => c?.supplierId).filter(Boolean)), [watched])

  useEffect(() => {
    if (!doc) return
    form.setFieldsValue({
      date: dayjs(doc.date),
      itemId: doc.itemId,
      description: doc.description ?? '',
      specification: doc.specification ?? '',
      qty: doc.qty,
      deadline: doc.deadline ? dayjs(doc.deadline) : null,
      responsible: doc.responsible ?? '',
      note: doc.note ?? '',
      candidates: doc.candidates.map((c) => ({ id: c.id, supplierId: c.supplierId, status: c.status, note: c.note ?? '' })),
    })
  }, [doc, form])

  const persist = async (): Promise<Sourcing | null> => {
    let values: FormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ.')
      return null
    }
    setError(null)
    const input: SaveSourcingInput = {
      date: values.date.format('YYYY-MM-DD'),
      itemId: values.itemId ?? null,
      description: orNull(values.description),
      specification: orNull(values.specification),
      qty: values.qty ?? null,
      deadline: values.deadline ? values.deadline.format('YYYY-MM-DD') : null,
      responsible: orNull(values.responsible),
      note: orNull(values.note),
      candidates: (values.candidates ?? []).map((c) => ({ id: c.id ?? null, supplierId: c.supplierId!, status: c.status ?? 'Contacted', note: orNull(c.note) })),
    }
    try {
      return isNew ? await mutations.create.mutateAsync(input) : await mutations.update.mutateAsync({ id, input })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được phiếu.')
      return null
    }
  }

  const save = async () => {
    if (readOnly) return
    const saved = await persist()
    if (!saved) return
    message.success(`Đã lưu phiếu ${saved.no}.`)
    if (isNew) navigate(`${LIST_PATH}/${saved.id}`, { replace: true })
  }

  /** Đóng (đã xong) hoặc hủy phiếu sau khi hỏi lại. */
  const finish = (kind: 'close' | 'cancel') =>
    doc &&
    modal.confirm({
      title: kind === 'close' ? `Đánh dấu đã xong phiếu ${doc.no}?` : `Hủy phiếu ${doc.no}?`,
      content: 'Phiếu không sửa được nữa.',
      okText: kind === 'close' ? 'Đã xong' : 'Hủy phiếu',
      okButtonProps: { danger: kind === 'cancel' },
      cancelText: 'Không',
      onOk: async () => {
        try {
          await (kind === 'close' ? mutations.close : mutations.cancel).mutateAsync(doc.id)
          message.success(kind === 'close' ? `Phiếu ${doc.no} đã xong.` : `Đã hủy phiếu ${doc.no}.`)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không thực hiện được.')
        }
      },
    })

  const supplierOptions = (suppliers.data?.items ?? [])
    .filter((s) => s.status !== 'Blacklisted')
    .map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))

  const columns: LineColumn[] = [
    {
      key: 'supplier',
      title: 'Nhà cung cấp', required: true,
      width: 300,
      render: (field) => (
        <>
          <Form.Item name={[field.name, 'id']} hidden>
            <Input />
          </Form.Item>
          <Form.Item name={[field.name, 'supplierId']} noStyle rules={[{ required: true, message: 'Chọn NCC.' }]}>
            <Select
              variant="borderless"
              style={{ width: '100%' }}
              aria-label={`NCC dòng ${field.name + 1}`}
              showSearch={{ optionFilterProp: 'label' }}
              placeholder="Gõ mã hoặc tên NCC"
              options={supplierOptions.map((o) => ({ ...o, disabled: usedSuppliers.has(o.value) && watched?.[field.name]?.supplierId !== o.value }))}
              popupMatchSelectWidth={420}
            />
          </Form.Item>
        </>
      ),
    },
    {
      key: 'status',
      title: 'Trạng thái',
      width: 130,
      render: (field) => (
        <Form.Item name={[field.name, 'status']} noStyle>
          <Select variant="borderless" style={{ width: '100%' }} aria-label={`Trạng thái dòng ${field.name + 1}`}
            options={Object.entries(candidateStatusLabels).map(([value, label]) => ({ value, label }))} />
        </Form.Item>
      ),
    },
    {
      key: 'note',
      title: 'Ghi chú, hồ sơ năng lực',
      width: 360,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle rules={[{ max: 500 }]}>
          <Input variant="borderless" aria-label={`Ghi chú dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const status = isNew
    ? { label: 'Đang tìm (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? sourcingStatusLabels[doc.status] : '', tone: doc ? sourcingStatusTone[doc.status] : ('neutral' as const) }

  const actions = (
    <>
      {!readOnly && (
        <Button type="primary" icon={<SaveOutlined />} onClick={save} loading={mutations.create.isPending || mutations.update.isPending}>
          Lưu
        </Button>
      )}
      {doc?.status === 'Open' && hasPermission(Permission.RfqManage) && (
        <Button icon={<FileSearchOutlined />} onClick={() => navigate(`/dau-thau/rfq/moi?sourcingId=${doc.id}`)}>
          Lập RFQ
        </Button>
      )}
      {doc?.canEdit && (
        <>
          <Button icon={<CheckOutlined />} onClick={() => finish('close')}>
            Đã xong
          </Button>
          <Button danger icon={<StopOutlined />} onClick={() => finish('cancel')}>
            Hủy phiếu
          </Button>
        </>
      )}
      <Button type="text" icon={<CloseOutlined />} onClick={() => navigate(LIST_PATH)}>
        Đóng
      </Button>
    </>
  )

  return (
    <DocumentForm
      title="Phiếu tìm nguồn"
      number={isNew ? null : (doc?.no ?? '')}
      status={status}
      meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
      loading={sourcing.isLoading}
      error={error ?? (sourcing.error ? sourcing.error.message : null)}
      form={form}
      initialValues={{ date: dayjs(), candidates: [] }}
      readOnly={readOnly}
      onSave={save}
      onAddLine={() => addRef.current?.()}
      actions={actions}
      linesLabel="NCC ứng viên"
      header={
        <>
          <Form.Item label="Ngày lập" name="date" rules={[{ required: true, message: 'Chọn ngày.' }]}>
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item
            label="Mặt hàng"
            name="itemId"
            extra="Để trống nếu hàng chưa có mã – nhập mô tả."
            dependencies={['description']}
            rules={[{ required: needsItemOrDescription, message: 'Chọn mặt hàng hoặc nhập mô tả hàng cần tìm.' }]}
          >
            <Select
              allowClear
              showSearch={{ filterOption: false, onSearch: setSearch }}
              placeholder="Gõ mã hoặc tên hàng"
              options={(lookup.data ?? []).map((i) => ({ value: i.id, label: `${i.code} – ${i.name}` }))}
              popupMatchSelectWidth={420}
            />
          </Form.Item>
          <Form.Item
            label="Mô tả hàng cần tìm"
            name="description"
            dependencies={['itemId']}
            rules={[{ required: needsItemOrDescription, whitespace: true, message: 'Chọn mặt hàng hoặc nhập mô tả hàng cần tìm.' }, { max: 500 }]}
          >
            <Input />
          </Form.Item>
          <Form.Item label="Yêu cầu kỹ thuật" name="specification" rules={[{ max: 2000 }]}>
            <Input.TextArea rows={1} autoSize={{ minRows: 1, maxRows: 4 }} />
          </Form.Item>
          <Form.Item label={`Số lượng dự kiến${doc?.uomCode ? ` (${doc.uomCode})` : ''}`} name="qty">
            <InputNumber {...vnNumberInput} style={{ width: '100%' }} min={0} precision={4} />
          </Form.Item>
          <Form.Item label="Hạn chót" name="deadline">
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Người phụ trách" name="responsible" rules={[{ max: 200 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]}>
            <Input />
          </Form.Item>
        </>
      }
      lines={
        <LineGrid name="candidates" columns={columns} readOnly={readOnly} addRef={addRef} newLine={() => ({ id: null, status: 'Contacted' })} />
      }
    />
  )
}
