import { CloseOutlined, DeleteOutlined, PaperClipOutlined, SendOutlined } from '@ant-design/icons'
import { useQueryClient } from '@tanstack/react-query'
import { Alert, App, Button, Form, Input, InputNumber, Upload } from 'antd'
import { useMemo, useState } from 'react'
import { DateInput } from '../classic/DateInput'
import { DataGrid } from '../components/DataGrid/DataGrid'
import type { GridColumn, StatusTone } from '../components/DataGrid/types'
import { LineGrid, type LineColumn } from '../components/DocumentForm/LineGrid'
import { formatDate, formatQuantity, vnNumberInput } from '../utils/format'
import {
  portalApi,
  portalDeliveryNoticeStatusLabels,
  portalFiles,
  type PortalDeliveryNotice,
  type PortalDeliveryNoticeLine,
  type PortalDeliveryNoticeStatus,
  type PortalOrder,
} from './portalApi'
import { PortalFiles, Section } from './PortalParts'

const cellNumber = { ...vnNumberInput, variant: 'borderless', style: { width: '100%' } } as const

/** Màu đèn trạng thái phiếu báo giao (luôn kèm chữ). */
const noticeTone: Record<PortalDeliveryNoticeStatus, StatusTone> = {
  Submitted: 'warning',
  Receiving: 'neutral',
  Received: 'success',
  Cancelled: 'error',
}

const noticeColumns: GridColumn<PortalDeliveryNotice>[] = [
  { key: 'no', title: 'Số phiếu', width: 140, value: (r) => r.no, auditField: false },
  { key: 'expectedDate', title: 'Ngày giao dự kiến', width: 110, type: 'date', value: (r) => r.expectedDate, auditField: false },
  { key: 'deliveryNoteNo', title: 'Số phiếu giao', width: 120, value: (r) => r.deliveryNoteNo, auditField: false },
  { key: 'deliverer', title: 'Người giao, phương tiện', width: 160, value: (r) => r.deliverer, auditField: false },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 160,
    type: 'status',
    value: (r) => portalDeliveryNoticeStatusLabels[r.status],
    tone: (r) => noticeTone[r.status],
    auditField: false,
  },
  { key: 'receiptNo', title: 'Số phiếu nhận hàng', width: 140, value: (r) => r.receiptNo, auditField: false },
  { key: 'receivedDate', title: 'Ngày nhận', width: 90, type: 'date', value: (r) => r.receivedDate, auditField: false },
  { key: 'submittedAt', title: 'Thời điểm gửi', width: 130, type: 'datetime', value: (r) => r.submittedAt, auditField: false },
  { key: 'submittedByEmail', title: 'Người gửi', width: 170, value: (r) => r.submittedByEmail, auditField: false },
  { key: 'cancelReason', title: 'Lý do hủy', width: 200, value: (r) => r.cancelReason, auditField: false },
]

const noticeLineColumns: GridColumn<PortalDeliveryNoticeLine>[] = [
  { key: 'poLineNo', title: 'Dòng đơn', width: 70, type: 'quantity', value: (r) => r.poLineNo, auditField: false },
  { key: 'itemCode', title: 'Mã hàng', width: 100, value: (r) => r.itemCode, auditField: false },
  { key: 'itemName', title: 'Tên hàng', width: 220, value: (r) => r.itemName, auditField: false },
  { key: 'uomCode', title: 'ĐVT', width: 55, value: (r) => r.uomCode, auditField: false },
  { key: 'qty', title: 'SL báo giao', width: 90, type: 'quantity', value: (r) => r.qty, auditField: false },
  { key: 'receivedQty', title: 'SL nhận đạt', width: 90, type: 'quantity', value: (r) => r.receivedQty, auditField: false },
  { key: 'rejectedQty', title: 'SL không đạt', width: 90, type: 'quantity', value: (r) => r.rejectedQty, auditField: false },
  { key: 'note', title: 'Ghi chú', width: 180, value: (r) => r.note, auditField: false },
]

/** Ô ngày gắn Form.Item (Form truyền value / onChange). */
function FormDateInput({ value, onChange }: { value?: string | null; onChange?: (v: string | null) => void }) {
  return <DateInput value={value ?? null} onChange={(v) => onChange?.(v)} aria-label="Ngày giao dự kiến" />
}

/** Giá trị form lập phiếu báo giao. */
interface NoticeFormValues {
  expectedDate: string | null
  deliveryNoteNo: string
  deliverer: string
  note: string
  lines: { purchaseOrderLineId: string; qty: number | null; note: string }[]
}

/**
 * Lập phiếu báo giao hàng (lát P5 – design.md 19.5, P5-1, P5-3): ngày giao dự kiến, số phiếu giao (bắt buộc), người giao / phương tiện, ghi chú;
 * lưới dòng đơn còn giao được – SL giao mặc định bằng phần còn giao (dòng để 0 / trống là không giao lần này). File kèm (phiếu giao, CO/CQ) chọn
 * trước, gửi phiếu xong tự tải lên (hàng chờ – CLAUDE.md mục 4). Gửi hỏi lại; gửi rồi không sửa, chỉ hủy khi bên đặt chưa lập phiếu nhận hàng.
 * @param order Đơn đang xem.
 * @param onClose Đóng form (sau khi gửi hoặc bấm Bỏ qua).
 */
export function DeliveryNoticeForm({ order, onClose }: { order: PortalOrder; onClose: () => void }) {
  const { message, modal } = App.useApp()
  const queryClient = useQueryClient()
  const [form] = Form.useForm<NoticeFormValues>()
  const [pending, setPending] = useState<File[]>([])
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const lines = useMemo(() => order.lines.filter((l) => l.deliverableQty > 0), [order.lines])
  const initial = useMemo<NoticeFormValues>(
    () => ({
      expectedDate: order.expectedDate && order.expectedDate >= order.date ? order.expectedDate : order.date,
      deliveryNoteNo: '',
      deliverer: '',
      note: '',
      lines: lines.map((l) => ({ purchaseOrderLineId: l.purchaseOrderLineId, qty: l.deliverableQty, note: '' })),
    }),
    [lines, order.date, order.expectedDate],
  )

  const columns: LineColumn[] = [
    { key: 'lineNo', title: 'Dòng đơn', width: 70, align: 'right', render: (_, i) => <span className="erp-number">{lines[i]?.lineNo}</span> },
    { key: 'itemCode', title: 'Mã hàng', width: 100, render: (_, i) => lines[i]?.itemCode },
    { key: 'itemName', title: 'Tên hàng', width: 220, render: (_, i) => lines[i]?.itemName },
    { key: 'uomCode', title: 'ĐVT', width: 55, render: (_, i) => lines[i]?.uomCode },
    {
      key: 'deliverableQty',
      title: 'Còn giao được',
      width: 100,
      align: 'right',
      render: (_, i) => <span className="erp-number">{formatQuantity(lines[i]?.deliverableQty)}</span>,
    },
    {
      key: 'qty',
      title: 'SL giao',
      width: 100,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'qty']} noStyle>
          <InputNumber {...cellNumber} min={0} max={lines[field.name]?.deliverableQty} precision={4} aria-label={`SL giao dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'note',
      title: 'Ghi chú (số lô, hạn dùng…)',
      width: 200,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle>
          <Input variant="borderless" maxLength={500} aria-label={`Ghi chú dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const submit = async () => {
    const values = form.getFieldsValue(true) as NoticeFormValues
    const chosen = (values.lines ?? []).filter((l) => (l.qty ?? 0) > 0)
    if (!values.expectedDate) return setError('Nhập ngày giao dự kiến.')
    if (!values.deliveryNoteNo?.trim()) return setError('Nhập số phiếu giao hàng của công ty bạn để nơi nhận đối chiếu khi hàng đến.')
    if (chosen.length === 0) return setError('Nhập SL giao cho ít nhất một dòng.')
    setError(null)
    modal.confirm({
      title: 'Gửi phiếu báo giao',
      content: `Báo giao ${chosen.length} dòng của đơn ${order.no} ngày ${formatDate(values.expectedDate)}? Gửi rồi không sửa được – sai thì hủy và báo lại.`,
      okText: 'Gửi',
      cancelText: 'Không',
      onOk: async () => {
        setBusy(true)
        try {
          const notice = await portalApi.createDeliveryNotice(order.id, {
            expectedDate: values.expectedDate!,
            deliveryNoteNo: values.deliveryNoteNo.trim(),
            deliverer: values.deliverer?.trim() || null,
            note: values.note?.trim() || null,
            lines: chosen.map((l) => ({ purchaseOrderLineId: l.purchaseOrderLineId, qty: l.qty!, note: l.note?.trim() || null })),
          })
          // Phiếu đã gửi: tải file trong hàng chờ; file lỗi báo riêng, phiếu vẫn giữ (NCC tải lại ở khối file của phiếu).
          const files = portalFiles('delivery-notices')
          for (const file of pending) {
            try {
              await files.upload(`${order.id}/${notice.id}`, file, null)
            } catch (e) {
              void message.error(`Không tải lên được "${file.name}": ${e instanceof Error ? e.message : String(e)}`)
            }
          }
          await queryClient.invalidateQueries({ queryKey: ['portal', 'order', order.id] })
          await queryClient.invalidateQueries({ queryKey: ['portal', 'orders'] })
          void message.success(`Đã gửi phiếu báo giao ${notice.no}.`)
          onClose()
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không gửi được phiếu báo giao.')
        } finally {
          setBusy(false)
        }
      },
    })
  }

  return (
    <Section title="Lập phiếu báo giao hàng">
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form form={form} layout="vertical" requiredMark={false} initialValues={initial} data-testid="portal-delivery-form">
        <div className="erp-portal-delivery-head">
          <Form.Item name="expectedDate" label="Ngày giao dự kiến *">
            <FormDateInput />
          </Form.Item>
          <Form.Item name="deliveryNoteNo" label="Số phiếu giao *">
            <Input maxLength={100} aria-label="Số phiếu giao" />
          </Form.Item>
          <Form.Item name="deliverer" label="Người giao, phương tiện">
            <Input maxLength={200} aria-label="Người giao, phương tiện" />
          </Form.Item>
          <Form.Item name="note" label="Ghi chú">
            <Input maxLength={1000} aria-label="Ghi chú phiếu báo giao" />
          </Form.Item>
        </div>
        <LineGrid name="lines" columns={columns} fixedRows newLine={() => ({})} />
        <p className="erp-portal-hint">
          SL theo ĐVT của dòng đơn, không vượt phần còn giao được (đã trừ hàng bên đặt đã nhận và các phiếu báo giao chưa nhận). Dòng để 0 là không
          giao lần này.
        </p>
      </Form>
      <div className="erp-portal-file-bar">
        <Upload
          showUploadList={false}
          multiple
          beforeUpload={(file) => {
            if (file.size > order.maxFileSizeMb * 1024 * 1024) {
              void message.error(`File "${file.name}" quá ${order.maxFileSizeMb} MB. Hãy nén hoặc chia nhỏ file.`)
            } else {
              setPending((list) => [...list, file])
            }
            return false
          }}
        >
          <Button icon={<PaperClipOutlined />}>Chọn file kèm…</Button>
        </Upload>
        <span className="erp-portal-hint" data-testid="portal-delivery-pending">
          {pending.length === 0 ? `Phiếu giao, CO/CQ, ảnh hàng… tối đa ${order.maxFileSizeMb} MB/file – tải lên khi gửi phiếu.` : ''}
        </span>
        {pending.map((file, index) => (
          <span key={`${file.name}-${index}`} className="erp-portal-pending-file">
            {file.name}
            <Button
              type="text"
              size="small"
              icon={<DeleteOutlined />}
              title={`Bỏ file ${file.name}`}
              aria-label={`Bỏ file ${file.name}`}
              onClick={() => setPending((list) => list.filter((_, i) => i !== index))}
            />
          </span>
        ))}
      </div>
      <div className="erp-portal-actions" style={{ marginTop: 8 }}>
        <Button icon={<CloseOutlined />} onClick={onClose} disabled={busy}>
          Bỏ qua
        </Button>
        <Button type="primary" icon={<SendOutlined />} loading={busy} onClick={() => void submit()}>
          Gửi phiếu báo giao
        </Button>
      </div>
    </Section>
  )
}

/**
 * Các phiếu báo giao của đơn (lát P5 – P5-2, P5-6): lưới phiếu; chọn một phiếu xem dòng (kèm SL bên đặt nhận đạt / không đạt khi đã nhận), file
 * kèm (thêm, xóa khi phiếu còn chờ nhận) và **Hủy phiếu báo giao** (icon đỏ, bắt buộc lý do) khi bên đặt chưa lập phiếu nhận hàng.
 * @param order Đơn đang xem.
 */
export function DeliveryNoticeList({ order }: { order: PortalOrder }) {
  const { message, modal } = App.useApp()
  const queryClient = useQueryClient()
  const [selectedId, setSelectedId] = useState<string | null>(order.deliveryNotices[0]?.id ?? null)
  const [groupBy, setGroupBy] = useState<string | null>(null)
  const [lineGroupBy, setLineGroupBy] = useState<string | null>(null)
  const selected = order.deliveryNotices.find((n) => n.id === selectedId) ?? order.deliveryNotices[0]
  if (!selected) return null

  const cancel = (notice: PortalDeliveryNotice) => {
    let reason = ''
    modal.confirm({
      title: `Hủy phiếu báo giao ${notice.no}`,
      content: (
        <Input.TextArea
          autoFocus
          maxLength={1000}
          autoSize={{ minRows: 2, maxRows: 4 }}
          placeholder="Lý do hủy (bắt buộc) – bên đặt hàng nhận được lý do này"
          aria-label="Lý do hủy phiếu báo giao"
          onChange={(e) => (reason = e.target.value)}
        />
      ),
      okText: 'Hủy phiếu',
      cancelText: 'Không',
      okButtonProps: { danger: true },
      onOk: async () => {
        if (!reason.trim()) {
          void message.error('Nhập lý do hủy phiếu báo giao.')
          throw new Error('Thiếu lý do')
        }
        try {
          const result = await portalApi.cancelDeliveryNotice(order.id, notice.id, reason.trim())
          queryClient.setQueryData(['portal', 'order', order.id], result)
          await queryClient.invalidateQueries({ queryKey: ['portal', 'delivery-notices-files', `${order.id}/${notice.id}`] })
          void message.success(`Đã hủy phiếu báo giao ${notice.no}.`)
        } catch (e) {
          void message.error(e instanceof Error ? e.message : 'Không hủy được phiếu báo giao.')
        }
      },
    })
  }

  return (
    <>
      <Section title={`Phiếu báo giao hàng (${order.deliveryNotices.length})`}>
        <div style={{ height: 140, display: 'flex', flexDirection: 'column' }} data-testid="portal-delivery-notices">
          <DataGrid
            rows={order.deliveryNotices}
            columns={noticeColumns}
            rowKey={(r) => r.id}
            groupBy={groupBy}
            onGroupByChange={setGroupBy}
            onSelectRow={(r) => r && setSelectedId(r.id)}
          />
        </div>
        <p className="erp-portal-hint">
          Phiếu {selected.no} – {portalDeliveryNoticeStatusLabels[selected.status]}
          {selected.note ? `. Ghi chú: ${selected.note}` : ''}. Chọn một dòng để xem phiếu khác.
        </p>
        <div style={{ height: 140, display: 'flex', flexDirection: 'column' }} data-testid="portal-delivery-lines">
          <DataGrid
            rows={selected.lines}
            columns={noticeLineColumns}
            rowKey={(r) => String(r.lineNo)}
            groupBy={lineGroupBy}
            onGroupByChange={setLineGroupBy}
          />
        </div>
        {selected.canCancel && (
          <div className="erp-portal-actions" style={{ marginTop: 8 }}>
            <Button icon={<CloseOutlined />} onClick={() => cancel(selected)}>
              Hủy phiếu báo giao
            </Button>
            <span />
          </div>
        )}
      </Section>
      <PortalFiles
        key={selected.id}
        kind="delivery-notices"
        id={`${order.id}/${selected.id}`}
        title={`File kèm phiếu báo giao ${selected.no}`}
        canEdit={selected.canCancel}
        maxFileSizeMb={order.maxFileSizeMb}
        testId="portal-delivery-files"
        hint={
          selected.canCancel
            ? `Phiếu giao, CO/CQ, ảnh hàng… tối đa ${order.maxFileSizeMb} MB/file. Thêm, xóa được đến khi bên đặt lập phiếu nhận hàng.`
            : 'Bên đặt đã lập phiếu nhận hàng hoặc phiếu đã hủy – file đã khóa, chỉ tải xuống được.'
        }
      />
    </>
  )
}
