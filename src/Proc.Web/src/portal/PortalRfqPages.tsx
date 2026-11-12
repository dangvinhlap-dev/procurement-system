import { DeleteOutlined, DownloadOutlined, SendOutlined, UploadOutlined } from '@ant-design/icons'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, App, Button, Form, Input, InputNumber, Spin, Upload } from 'antd'
import dayjs from 'dayjs'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { DateInput } from '../classic/DateInput'
import { Select } from '../classic/Select'
import { DataGrid } from '../components/DataGrid/DataGrid'
import type { GridColumn, StatusTone } from '../components/DataGrid/types'
import { LineGrid, type LineColumn } from '../components/DocumentForm/LineGrid'
import { formatMoney, formatQuantity, vnNumberInput } from '../utils/format'
import {
  portalApi,
  portalRfqStatusLabels,
  type PortalAttachment,
  type PortalRfq,
  type PortalRfqListItem,
  type PortalRfqStatus,
  type PortalSubmission,
} from './portalApi'

/** Ô số trong lưới nhập: gõ kiểu Việt Nam, không viền. */
const cellNumber = { ...vnNumberInput, variant: 'borderless', style: { width: '100%' } } as const

/** Màu đèn trạng thái thư mời. */
const statusTone: Record<PortalRfqStatus, StatusTone> = {
  Open: 'success',
  Closed: 'warning',
  Reviewing: 'neutral',
  Awarded: 'neutral',
  Cancelled: 'error',
}

/**
 * Hạn nộp do máy chủ trả là giờ Việt Nam không kèm múi giờ – hiện nguyên giờ đó (không đổi theo múi giờ máy NCC).
 * @param value Chuỗi "yyyy-MM-ddTHH:mm:ss".
 * @returns "dd/MM/yyyy HH:mm".
 */
function formatLocalDateTime(value: string): string {
  return dayjs(value).format('DD/MM/YYYY HH:mm')
}

/** Khối có dải tiêu đề (như trang chủ Portal). */
function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="erp-portal-section">
      <h2>{title}</h2>
      <div className="erp-portal-section-body">{children}</div>
    </section>
  )
}

const listColumns: GridColumn<PortalRfqListItem>[] = [
  { key: 'no', title: 'Số yêu cầu', width: 150, value: (r) => r.no, auditField: false },
  { key: 'companyName', title: 'Công ty mời', width: 200, value: (r) => r.companyName, auditField: false },
  { key: 'title', title: 'Nội dung', width: 260, value: (r) => r.title, auditField: false },
  { key: 'deadlineAt', title: 'Hạn nộp', width: 130, value: (r) => formatLocalDateTime(r.deadlineAt), auditField: false },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 170,
    type: 'status',
    value: (r) => portalRfqStatusLabels[r.status],
    tone: (r) => statusTone[r.status],
    auditField: false,
  },
  { key: 'submissionCount', title: 'Số lần nộp', width: 90, type: 'quantity', value: (r) => r.submissionCount, auditField: false },
  { key: 'lastSubmittedAt', title: 'Nộp lần cuối', width: 140, type: 'datetime', value: (r) => r.lastSubmittedAt, auditField: false },
]

/**
 * Danh sách thư mời báo giá của NCC (lát P3a – design.md 19.5): chỉ RFQ đã gửi có mời NCC mình. Bấm đúp mở để xem và nộp báo giá.
 */
export function PortalRfqListPage() {
  const navigate = useNavigate()
  const rfqs = useQuery({ queryKey: ['portal', 'rfqs'], queryFn: portalApi.rfqs })
  const [groupBy, setGroupBy] = useState<string | null>(null)
  return (
    <>
      <h1 className="erp-portal-title">Thư mời báo giá</h1>
      {rfqs.error && <Alert type="error" showIcon title={rfqs.error.message} />}
      <Section title="Yêu cầu báo giá gửi tới công ty bạn">
        <DataGrid
          rows={rfqs.data ?? []}
          columns={listColumns}
          rowKey={(r) => r.id}
          groupBy={groupBy}
          onGroupByChange={setGroupBy}
          loading={rfqs.isFetching}
          onOpenRow={(r) => navigate(`/ncc/bao-gia/${r.id}`)}
        />
        <p className="erp-portal-hint">Bấm đúp một dòng để xem yêu cầu và nộp báo giá.</p>
      </Section>
    </>
  )
}

/** Giá trị form nộp báo giá; unitPrice trống = không chào dòng đó. */
interface QuoteForm {
  validUntil: string | null
  paymentTerms?: string
  note?: string
  lines: { rfqLineId: string; unitPrice?: number | null; discountPct?: number | null; vatCode?: string | null; leadTimeDays?: number | null; note?: string }[]
}

/** Thành tiền một dòng theo giá đang gõ: SL × đơn giá × (100 − %CK)/100. */
function AmountCell({ index, qty }: { index: number; qty: number }) {
  const line = Form.useWatch(['lines', index]) as QuoteForm['lines'][number] | undefined
  if (line?.unitPrice == null) return <span className="erp-number">–</span>
  const amount = Math.round(qty * line.unitPrice * (100 - (line.discountPct ?? 0))) / 100
  return <span className="erp-number">{formatMoney(amount)}</span>
}

const submissionColumns: GridColumn<PortalSubmission>[] = [
  { key: 'sequence', title: 'Lần nộp', width: 70, type: 'quantity', value: (r) => r.sequence, auditField: false },
  { key: 'submittedAt', title: 'Thời điểm nộp', width: 140, type: 'datetime', value: (r) => r.submittedAt, auditField: false },
  { key: 'submittedByEmail', title: 'Người nộp', width: 200, value: (r) => r.submittedByEmail, auditField: false },
  { key: 'totalAmount', title: 'Tổng tiền trước VAT', width: 150, type: 'money', value: (r) => r.totalAmount, auditField: false },
  { key: 'contentHash', title: 'Mã biên nhận (SHA-256)', width: 460, value: (r) => r.contentHash, auditField: false },
]

/**
 * Xem một thư mời và nộp báo giá (lát P3a): giá niêm phong – bên mời chỉ mở sau giờ hạn nộp; nộp lại được nhiều lần trước giờ hạn, bản cuối
 * có hiệu lực; mỗi lần nộp có biên nhận mã băm. NCC không thấy NCC khác.
 */
export function PortalRfqPage() {
  const { id = '' } = useParams()
  const { message, modal } = App.useApp()
  const queryClient = useQueryClient()
  const [form] = Form.useForm<QuoteForm>()
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [groupBy, setGroupBy] = useState<string | null>(null)
  const rfq = useQuery({ queryKey: ['portal', 'rfq', id], queryFn: () => portalApi.rfq(id) })
  const data = rfq.data

  // Đổ bản nộp cuối (hoặc dòng trống) vào form mỗi khi tải phiếu.
  const initial = useMemo<QuoteForm | null>(() => {
    if (!data) return null
    const latest = data.latest
    return {
      validUntil: latest?.validUntil ?? null,
      paymentTerms: latest?.paymentTerms ?? '',
      note: latest?.note ?? '',
      lines: data.lines.map((l) => {
        const q = latest?.lines.find((x) => x.rfqLineId === l.id)
        return {
          rfqLineId: l.id,
          unitPrice: q?.unitPrice ?? null,
          discountPct: q?.discountPct ?? 0,
          vatCode: q?.vatCode ?? null,
          leadTimeDays: q?.leadTimeDays ?? null,
          note: q?.note ?? '',
        }
      }),
    }
  }, [data])
  useEffect(() => {
    if (initial) form.setFieldsValue(initial)
  }, [form, initial])

  if (!data) {
    return (
      <Spin spinning={rfq.isFetching}>
        {rfq.error ? <Alert type="error" showIcon title={rfq.error.message} /> : <div style={{ minHeight: 120 }} />}
      </Spin>
    )
  }

  const readOnly = !data.canSubmit
  const columns: LineColumn[] = [
    { key: 'itemCode', title: 'Mã hàng', width: 110, render: (_, i) => data.lines[i]?.itemCode },
    { key: 'supplierItemCode', title: 'Mã hàng NCC', width: 100, render: (_, i) => data.lines[i]?.supplierItemCode ?? '' },
    { key: 'itemName', title: 'Tên hàng', width: 220, render: (_, i) => data.lines[i]?.itemName },
    { key: 'uomCode', title: 'ĐVT', width: 60, render: (_, i) => data.lines[i]?.uomCode },
    {
      key: 'qty',
      title: 'Số lượng',
      width: 90,
      align: 'right',
      render: (_, i) => <span className="erp-number">{formatQuantity(data.lines[i]?.qty)}</span>,
    },
    {
      key: 'unitPrice',
      title: 'Đơn giá trước VAT',
      width: 130,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'unitPrice']} noStyle>
          <InputNumber {...cellNumber} min={0} precision={6} disabled={readOnly} aria-label={`Đơn giá dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'discountPct',
      title: 'CK %',
      width: 70,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'discountPct']} noStyle>
          <InputNumber {...cellNumber} min={0} max={100} precision={4} disabled={readOnly} aria-label={`Chiết khấu dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'vatCode',
      title: 'Thuế suất',
      width: 90,
      render: (field) => (
        <Form.Item name={[field.name, 'vatCode']} noStyle>
          <Select
            variant="borderless"
            allowClear
            disabled={readOnly}
            showSearch={false}
            style={{ width: '100%' }}
            aria-label={`Thuế suất dòng ${field.name + 1}`}
            options={data.vatCodes.map((v) => ({ value: v, label: /^\d+$/.test(v) ? `${v}%` : v }))}
          />
        </Form.Item>
      ),
    },
    {
      key: 'leadTimeDays',
      title: 'Giao (ngày)',
      width: 80,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'leadTimeDays']} noStyle>
          <InputNumber {...cellNumber} min={0} max={365} precision={0} disabled={readOnly} aria-label={`Thời gian giao dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'note',
      title: 'Ghi chú',
      width: 160,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle>
          <Input variant="borderless" maxLength={500} disabled={readOnly} aria-label={`Ghi chú dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'amount',
      title: 'Thành tiền',
      width: 130,
      align: 'right',
      render: (_, i) => <AmountCell index={i} qty={data.lines[i]?.qty ?? 0} />,
    },
  ]

  const submit = async () => {
    const values = form.getFieldsValue(true) as QuoteForm
    const lines = (values.lines ?? [])
      .filter((l) => l.unitPrice != null)
      .map((l) => ({
        rfqLineId: l.rfqLineId,
        unitPrice: Number(l.unitPrice),
        discountPct: Number(l.discountPct ?? 0),
        vatCode: l.vatCode ?? null,
        leadTimeDays: l.leadTimeDays ?? null,
        note: l.note?.trim() ? l.note.trim() : null,
      }))
    if (lines.length === 0) {
      setError('Hãy nhập đơn giá cho ít nhất một dòng hàng (dòng để trống là không chào giá).')
      return
    }
    setError(null)
    modal.confirm({
      title: 'Nộp báo giá',
      content: `Nộp báo giá ${lines.length}/${data.lines.length} dòng cho ${data.no}? Báo giá được niêm phong đến hết hạn nộp ${formatLocalDateTime(data.deadlineAt)}; trước hạn bạn vẫn nộp lại được, bản cuối có hiệu lực.`,
      okText: 'Nộp báo giá',
      cancelText: 'Không',
      onOk: async () => {
        setBusy(true)
        try {
          const result = await portalApi.submitQuote(id, {
            validUntil: values.validUntil ?? null,
            paymentTerms: values.paymentTerms?.trim() ? values.paymentTerms.trim() : null,
            note: values.note?.trim() ? values.note.trim() : null,
            lines,
          })
          queryClient.setQueryData(['portal', 'rfq', id], result)
          await queryClient.invalidateQueries({ queryKey: ['portal', 'rfqs'] })
          void message.success(`Đã nộp báo giá lần ${result.latest?.sequence ?? ''}. Mã biên nhận: ${result.latest?.contentHash.slice(0, 12) ?? ''}…`)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không nộp được báo giá.')
        } finally {
          setBusy(false)
        }
      },
    })
  }

  return (
    <>
      <h1 className="erp-portal-title">Báo giá {data.no}</h1>
      <p className="erp-portal-hint">
        <Link to="/ncc/bao-gia">← Thư mời báo giá</Link>
      </p>
      {data.canSubmit ? (
        <Alert
          type="info"
          showIcon
          title={`Hạn nộp: ${formatLocalDateTime(data.deadlineAt)}. Báo giá được niêm phong – bên mời chỉ mở sau giờ hạn nộp và không cho NCC khác xem. Bạn nộp lại được nhiều lần trước hạn, bản nộp cuối có hiệu lực.`}
        />
      ) : (
        <Alert type="warning" showIcon title={`${portalRfqStatusLabels[data.status]}. ${data.closedReason ?? ''}`} />
      )}
      <Section title="Thông tin yêu cầu báo giá">
        <dl className="erp-portal-fields" data-testid="portal-rfq-info">
          <dt>Số yêu cầu</dt>
          <dd>{data.no}</dd>
          <dt>Công ty mời</dt>
          <dd>{data.companyName}</dd>
          <dt>Nội dung</dt>
          <dd>{data.title}</dd>
          <dt>Hạn nộp</dt>
          <dd>{formatLocalDateTime(data.deadlineAt)}</dd>
          <dt>Trạng thái</dt>
          <dd>{portalRfqStatusLabels[data.status]}</dd>
          <dt>Ghi chú</dt>
          <dd>{data.note ?? '–'}</dd>
        </dl>
      </Section>
      <Section title={data.latest ? `Báo giá của bạn – đang có hiệu lực: lần ${data.latest.sequence}` : 'Báo giá của bạn – chưa nộp'}>
        {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
        <Form form={form} layout="vertical" requiredMark={false} initialValues={initial ?? undefined}>
          <div className="erp-portal-quote-head">
            <Form.Item name="validUntil" label="Báo giá hiệu lực đến">
              <QuoteDateInput disabled={readOnly} />
            </Form.Item>
            <Form.Item name="paymentTerms" label="Điều kiện thanh toán">
              <Input maxLength={200} disabled={readOnly} />
            </Form.Item>
            <Form.Item name="note" label="Ghi chú">
              <Input maxLength={1000} disabled={readOnly} />
            </Form.Item>
          </div>
          <LineGrid name="lines" columns={columns} readOnly={readOnly} fixedRows newLine={() => ({})} />
          <p className="erp-portal-hint">Đơn giá theo ĐVT của dòng, chưa gồm VAT. Dòng để trống đơn giá là không chào giá dòng đó.</p>
          {data.canSubmit && (
            <div className="erp-portal-actions">
              <span />
              <Button type="primary" icon={<SendOutlined />} loading={busy} onClick={() => void submit()}>
                Nộp báo giá
              </Button>
            </div>
          )}
        </Form>
      </Section>
      <QuoteFiles rfq={data} />
      {data.submissions.length > 0 && (
        <Section title="Biên nhận các lần nộp">
          <DataGrid
            rows={data.submissions}
            columns={submissionColumns}
            rowKey={(r) => String(r.sequence)}
            groupBy={groupBy}
            onGroupByChange={setGroupBy}
          />
          <p className="erp-portal-hint">Mã biên nhận dùng để đối chiếu: bản bên mời mở ra phải có đúng mã của lần nộp cuối.</p>
        </Section>
      )}
    </>
  )
}

/** Ô ngày nối với Form.Item (value/onChange do Form truyền vào). */
function QuoteDateInput({ value, onChange, disabled }: { value?: string | null; onChange?: (v: string | null) => void; disabled?: boolean }) {
  return disabled ? (
    <Input value={value ? dayjs(value).format('DD/MM/YYYY') : ''} disabled />
  ) : (
    <DateInput value={value ?? null} onChange={(v) => onChange?.(v)} aria-label="Báo giá hiệu lực đến" />
  )
}

const fileColumns: GridColumn<PortalAttachment>[] = [
  { key: 'fileName', title: 'Tên file', width: 260, value: (r) => r.fileName, auditField: false },
  { key: 'sizeBytes', title: 'Dung lượng (KB)', width: 110, type: 'quantity', value: (r) => Math.ceil(r.sizeBytes / 1024), auditField: false },
  { key: 'uploadedByName', title: 'Người tải lên', width: 160, value: (r) => r.uploadedByName, auditField: false },
  { key: 'uploadedAt', title: 'Thời điểm', width: 140, type: 'datetime', value: (r) => r.uploadedAt, auditField: false },
  { key: 'note', title: 'Ghi chú', width: 220, value: (r) => r.note, auditField: false },
]

/**
 * File đính kèm báo giá (lát P3b-1 – mọi form có đính kèm, CLAUDE.md mục 4): file gắn với thư mời của NCC nên tải lên ngay, không cần chờ nộp
 * giá; thêm, xóa đến giờ hạn nộp; niêm phong cùng báo giá – bên mời chỉ xem sau khi mở. Tối đa theo cấu hình máy chủ (mặc định 2 MB/file).
 * @param rfq Thư mời đang xem.
 */
function QuoteFiles({ rfq }: { rfq: PortalRfq }) {
  const { message, modal } = App.useApp()
  const queryClient = useQueryClient()
  const files = useQuery({ queryKey: ['portal', 'rfq-files', rfq.id], queryFn: () => portalApi.attachments(rfq.id) })
  const [groupBy, setGroupBy] = useState<string | null>(null)
  const [selected, setSelected] = useState<PortalAttachment | null>(null)
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)
  const reload = () => queryClient.invalidateQueries({ queryKey: ['portal', 'rfq-files', rfq.id] })

  const upload = async (file: File) => {
    // Chặn sớm file quá dung lượng; máy chủ vẫn kiểm lại (dung lượng thật, nội dung khớp đuôi).
    if (file.size > rfq.maxFileSizeMb * 1024 * 1024) {
      void message.error(`File "${file.name}" quá ${rfq.maxFileSizeMb} MB. Hãy nén hoặc chia nhỏ file.`)
      return
    }
    setBusy(true)
    try {
      await portalApi.uploadAttachment(rfq.id, file, note.trim() || null)
      setNote('')
      await reload()
      void message.success(`Đã tải lên "${file.name}".`)
    } catch (e) {
      void message.error(e instanceof Error ? e.message : 'Không tải lên được file.')
    } finally {
      setBusy(false)
    }
  }

  const remove = (item: PortalAttachment) =>
    modal.confirm({
      title: 'Xóa file',
      content: `Xóa file "${item.fileName}" khỏi báo giá?`,
      okText: 'Xóa',
      cancelText: 'Không',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await portalApi.deleteAttachment(rfq.id, item.id)
          setSelected(null)
          await reload()
        } catch (e) {
          void message.error(e instanceof Error ? e.message : 'Không xóa được file.')
        }
      },
    })

  const rows = files.data ?? []
  return (
    <Section title={`File đính kèm báo giá (${rows.length})`}>
      {files.error && <Alert type="error" showIcon title={files.error.message} style={{ marginBottom: 8 }} />}
      <div style={{ height: 160, display: 'flex', flexDirection: 'column' }} data-testid="portal-quote-files">
        <DataGrid
          rows={rows}
          columns={fileColumns}
          rowKey={(r) => r.id}
          groupBy={groupBy}
          onGroupByChange={setGroupBy}
          loading={files.isFetching}
          onSelectRow={setSelected}
          onOpenRow={(r) => void portalApi.downloadAttachment(rfq.id, r).catch((e: unknown) => void message.error(String(e)))}
        />
      </div>
      <div className="erp-portal-file-bar">
        {rfq.canSubmit && (
          <>
            <Input
              value={note}
              onChange={(e) => setNote(e.target.value)}
              maxLength={500}
              placeholder="Ghi chú file (không bắt buộc)"
              aria-label="Ghi chú file"
              style={{ width: 240 }}
            />
            <Upload
              showUploadList={false}
              multiple
              beforeUpload={(file) => {
                void upload(file)
                return false
              }}
            >
              <Button icon={<UploadOutlined />} loading={busy}>
                Tải file lên…
              </Button>
            </Upload>
          </>
        )}
        <Button
          icon={<DownloadOutlined />}
          disabled={!selected}
          onClick={() => selected && void portalApi.downloadAttachment(rfq.id, selected).catch((e: unknown) => void message.error(String(e)))}
        >
          Tải xuống
        </Button>
        {rfq.canSubmit && (
          <Button icon={<DeleteOutlined />} disabled={!selected?.canDelete} onClick={() => selected && remove(selected)}>
            Xóa file
          </Button>
        )}
      </div>
      <p className="erp-portal-hint">
        {rfq.canSubmit
          ? `Bảng giá chi tiết, catalogue, hồ sơ năng lực… tối đa ${rfq.maxFileSizeMb} MB/file. File được niêm phong cùng báo giá; thêm, xóa được đến hết hạn nộp.`
          : 'Đã hết hạn nộp hoặc bên mời đã mở báo giá – file đã khóa, chỉ tải xuống được.'}
      </p>
    </Section>
  )
}
