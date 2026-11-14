import { useDocumentFormSync } from '../../components/DocumentForm/useDocumentFormSync'
import { CheckOutlined, CloseOutlined, SaveOutlined, StopOutlined } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Flex, Form, Input, InputNumber } from 'antd'
import { Select } from '../../classic/Select'
import dayjs, { type Dayjs } from 'dayjs'
import { useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { useItemLookup } from '../../api/lookups'
import {
  marketSurveyStatusLabels,
  marketSurveyStatusTone,
  useMarketSurvey,
  useMarketSurveyMutations,
  useMarketSurveys,
  type MarketSurvey,
  type MarketSurveyListItem,
  type SaveMarketSurveyInput,
} from '../../api/marketSurveys'
import { useItemCategories, useSuppliers } from '../../api/masterData'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import type { GridColumn } from '../../components/DataGrid/types'
import { DocumentForm } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { ListPage } from '../../components/ListPage/ListPage'
import { DATE_INPUT_FORMATS, formatDate, formatDateTime, vnNumberInput } from '../../utils/format'

/** Đường dẫn danh sách phiếu khảo sát giá thị trường (màn hình KSG). */
export const MARKET_SURVEY_PATH = '/gia-ncc/khao-sat-gia'

const listColumns: GridColumn<MarketSurveyListItem>[] = [
  { key: 'no', title: 'Số phiếu', width: 150, value: (r) => r.no, docRef: (r) => ({ docType: 'KSG', id: r.id }) },
  { key: 'date', title: 'Ngày khảo sát', width: 100, type: 'date', value: (r) => r.surveyDate },
  { key: 'category', title: 'Nhóm hàng', width: 220, value: (r) => `${r.categoryCode} – ${r.categoryName}`, groupable: true },
  { key: 'lines', title: 'Số dòng giá', width: 90, type: 'quantity', value: (r) => r.lineCount },
  {
    key: 'status', title: 'Trạng thái', width: 110, type: 'status', groupable: true,
    value: (r) => marketSurveyStatusLabels[r.status], tone: (r) => marketSurveyStatusTone[r.status],
  },
  { key: 'creator', title: 'Người lập', width: 150, value: (r) => r.createdByName, groupable: true },
]

/**
 * Màn hình Giá và NCC → Khảo sát giá thị trường (KSG – design.md mục 18.25, lát G4a): Văn phòng Ban ghi giá khảo sát định kỳ vào ngân hàng
 * giá, làm căn cứ cho Ban duyệt giá tháng.
 */
export function MarketSurveyListPage() {
  const navigate = useNavigate()
  const { hasPermission, workingCompany } = useAuth()
  const list = useMarketSurveys()
  return (
    <ListPage
      title="Khảo sát giá thị trường"
      exportFileName="khao-sat-gia-thi-truong"
      rows={list.data ?? []}
      columns={listColumns}
      rowKey={(r) => r.id}
      auditEntity="MarketPriceSurvey"
      loading={list.isLoading}
      // Ngân hàng giá do Văn phòng Ban (tổng công ty) xây dựng.
      canCreate={hasPermission(Permission.PriceBankManage) && !!workingCompany?.isHeadOffice}
      onCreate={() => navigate(`${MARKET_SURVEY_PATH}/moi`)}
      onOpenRow={(r) => navigate(`${MARKET_SURVEY_PATH}/${r.id}`)}
      isMarked={(r) => r.status === 'Draft'}
    />
  )
}

interface LineValues {
  id: string | null
  itemId?: string
  uomId?: string
  supplierId?: string | null
  sourceName?: string
  unitPrice?: number | null
  note?: string
}

interface FormValues {
  surveyDate: Dayjs
  itemCategoryId?: string
  note?: string
  lines: LineValues[]
}

/** Chuỗi rỗng hoặc toàn khoảng trắng gửi lên API là null; còn lại bỏ khoảng trắng hai đầu. */
const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Thông báo đầu phiếu: đã chốt, đã hủy, lý do chỉ xem.
 * @param doc Phiếu đã tải.
 */
function SurveyNotices({ doc }: { doc: MarketSurvey }) {
  const notices = [
    doc.status === 'Confirmed' && (
      <Alert key="confirmed" type="success" showIcon
        title={`Đã chốt ${doc.confirmedAt ? formatDateTime(doc.confirmedAt) : ''}${doc.confirmedByName ? ` – ${doc.confirmedByName}` : ''}: giá đã vào ngân hàng giá, không sửa được (sai thì hủy và lập phiếu mới).`} />
    ),
    doc.status === 'Cancelled' && <Alert key="cancel" type="error" showIcon title={`Đã hủy ${doc.cancelledOn ? formatDate(doc.cancelledOn) : ''}: ${doc.cancelReason ?? ''}`} />,
    doc.actions.readOnlyReason && <Alert key="readonly" type="info" showIcon title={doc.actions.readOnlyReason} />,
  ].filter(Boolean)
  return notices.length ? <Flex vertical gap={8}>{notices}</Flex> : null
}

/**
 * Trang phiếu khảo sát giá thị trường trên khuôn DocumentForm: ngày khảo sát, nhóm hàng, ghi chú; dòng: mặt hàng, ĐVT, NCC (trong danh mục)
 * hoặc tên nguồn ngoài, đơn giá trước VAT. Nháp → Chốt (vào ngân hàng giá); Hủy có lý do (design.md mục 18.25).
 */
export function MarketSurveyPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const { hasPermission, workingCompany } = useAuth()
  const [form] = Form.useForm<FormValues>()
  const query = useMarketSurvey(isNew ? null : id)
  const mutations = useMarketSurveyMutations()
  const categories = useItemCategories()
  const suppliers = useSuppliers({})
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const addRef = useRef<(() => void) | null>(null)
  const doc = query.data
  const canCreate = hasPermission(Permission.PriceBankManage) && !!workingCompany?.isHeadOffice
  const readOnly = isNew ? !canCreate : !doc?.actions.canEdit
  // Đính kèm báo giá, ảnh bảng giá khảo sát – quyền theo quyền lập phiếu (design.md mục 18.4).
  const attachmentTab = useAttachmentTab('MarketPriceSurvey', doc?.id, hasPermission(Permission.PriceBankManage))

  const watchedLines = Form.useWatch('lines', form) as LineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])
  // Khảo sát thị trường gồm cả NCC tiềm năng – không lọc theo trạng thái NCC.
  const supplierOptions = (suppliers.data?.items ?? []).map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))

  const { loadSaved, targetId } = useDocumentFormSync(form, id, doc, (doc) => ({
    surveyDate: dayjs(doc.surveyDate),
    itemCategoryId: doc.itemCategoryId,
    note: doc.note ?? '',
    lines: doc.lines.map((l) => ({
      id: l.id, itemId: l.itemId, uomId: l.uomId, supplierId: l.supplierId, sourceName: l.sourceName ?? '', unitPrice: l.unitPrice, note: l.note ?? '',
    })),
  }))

  const fail = (e: unknown, fallback: string) => setError(e instanceof Error ? e.message : fallback)

  const persist = async (): Promise<MarketSurvey | null> => {
    let values: FormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng giá.')
      return null
    }
    setError(null)
    const input: SaveMarketSurveyInput = {
      surveyDate: values.surveyDate.format('YYYY-MM-DD'),
      itemCategoryId: values.itemCategoryId!,
      note: orNull(values.note),
      lines: (values.lines ?? []).map((l) => ({
        id: l.id ?? null, itemId: l.itemId!, uomId: l.uomId!, supplierId: l.supplierId ?? null, sourceName: orNull(l.sourceName),
        unitPrice: l.unitPrice ?? 0, note: orNull(l.note),
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
    if (isNew) navigate(`${MARKET_SURVEY_PATH}/${saved.id}`, { replace: true })
  }

  // Chốt: lưu trước rồi chốt – giá vào ngân hàng giá, không sửa được nữa.
  const confirm = () =>
    modal.confirm({
      title: 'Chốt phiếu khảo sát giá?',
      content: 'Giá trên phiếu vào ngân hàng giá để Ban tham khảo khi duyệt giá tháng; sau khi chốt không sửa được (sai thì hủy và lập phiếu mới).',
      okText: 'Chốt',
      cancelText: 'Không',
      onOk: async () => {
        const saved = await persist()
        if (!saved) return
        if (isNew) navigate(`${MARKET_SURVEY_PATH}/${saved.id}`, { replace: true })
        try {
          const done = await mutations.confirm.mutateAsync(saved.id)
          message.success(`Đã chốt phiếu ${done.no}.`)
        } catch (e) {
          fail(e, 'Không chốt được.')
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
          <p>Phiếu hủy thì giá trên phiếu không còn dùng để tham khảo.</p>
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

  const columns: LineColumn[] = [
    {
      key: 'item', title: 'Mặt hàng', required: true, width: 260,
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
      key: 'supplier', title: 'NCC (danh mục)', width: 230,
      render: (field) => (
        <Form.Item name={[field.name, 'supplierId']} noStyle>
          <Select variant="borderless" style={{ width: '100%' }} aria-label={`NCC dòng ${field.name + 1}`} allowClear
            showSearch={{ optionFilterProp: 'label' }} options={supplierOptions} popupMatchSelectWidth={360} />
        </Form.Item>
      ),
    },
    {
      key: 'source', title: 'Nguồn giá khác', width: 200,
      render: (field) => (
        // Không chọn NCC thì phải ghi nguồn (cửa hàng, báo giá, trang web…) để giá truy được.
        <Form.Item name={[field.name, 'sourceName']} noStyle
          rules={[{ max: 200 }, { required: !lines[field.name]?.supplierId, whitespace: true, message: 'Chọn NCC hoặc nhập nguồn.' }]}>
          <Input variant="borderless" placeholder={readOnly || lines[field.name]?.supplierId ? undefined : 'Bắt buộc khi không chọn NCC'} aria-label={`Nguồn giá dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'price', title: 'Đơn giá (trước VAT)', required: true, width: 130, align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'unitPrice']} noStyle rules={[{ required: true, type: 'number', min: 0.000001, message: 'Nhập đơn giá > 0.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} aria-label={`Đơn giá dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    { key: 'currency', title: 'Tiền tệ', width: 60, render: () => 'VND' },
    {
      key: 'note', title: 'Ghi chú', width: 200,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle rules={[{ max: 500 }]}>
          <Input variant="borderless" aria-label={`Ghi chú dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? marketSurveyStatusLabels[doc.status] : '', tone: doc ? marketSurveyStatusTone[doc.status] : ('neutral' as const) }

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
      {doc?.actions.canCancel && (
        <Button danger icon={<StopOutlined />} onClick={cancel} loading={mutations.cancel.isPending}>
          Hủy phiếu
        </Button>
      )}
      <Button type="text" icon={<CloseOutlined />} onClick={() => navigate(MARKET_SURVEY_PATH)}>
        Đóng
      </Button>
    </>
  )

  return (
    <DocumentForm
      title="Khảo sát giá thị trường"
      extraTabs={[attachmentTab]}
      number={isNew ? null : (doc?.no ?? '')}
      audit={{ entityType: 'MarketPriceSurvey', id: doc?.id }}
      status={status}
      meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
      loading={query.isLoading}
      error={error ?? (query.error ? query.error.message : null)}
      notice={doc && <SurveyNotices doc={doc} />}
      form={form}
      initialValues={{ surveyDate: dayjs(), lines: [] }}
      readOnly={readOnly}
      onSave={save}
      onAddLine={() => addRef.current?.()}
      actions={actions}
      header={
        <>
          <Form.Item label="Ngày khảo sát" name="surveyDate" rules={[{ required: true, message: 'Chọn ngày khảo sát.' }]}>
            <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} aria-label="Ngày khảo sát"
              disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
          </Form.Item>
          <Form.Item label="Nhóm hàng" name="itemCategoryId" rules={[{ required: true, message: 'Chọn nhóm hàng.' }]}>
            <Select showSearch={{ optionFilterProp: 'label' }} aria-label="Nhóm hàng"
              options={(categories.data ?? []).map((c) => ({ value: c.id, label: `${c.code} – ${c.name}` }))} />
          </Form.Item>
          <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]}>
            <Input aria-label="Ghi chú" placeholder="Phương pháp, phạm vi khảo sát…" />
          </Form.Item>
        </>
      }
      lines={
        <LineGrid name="lines" auditEntity="MarketPriceSurveyLine" columns={columns} readOnly={readOnly} addRef={addRef}
          newLine={() => ({ id: null, supplierId: null })} />
      }
    />
  )
}
