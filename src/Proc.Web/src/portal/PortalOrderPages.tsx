import { CheckOutlined, CloseOutlined } from '@ant-design/icons'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, App, Button, Input, Spin } from 'antd'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { DateInput } from '../classic/DateInput'
import { DataGrid } from '../components/DataGrid/DataGrid'
import type { GridColumn, StatusTone } from '../components/DataGrid/types'
import { formatDate, formatDateTime, formatMoney } from '../utils/format'
import {
  portalApi,
  portalOrderStatusLabels,
  type PortalOrder,
  type PortalOrderLine,
  type PortalOrderListItem,
  type PortalOrderResponse,
  type PortalOrderStatus,
} from './portalApi'
import { PortalFiles, Section } from './PortalParts'

/** Màu đèn trạng thái đơn (luôn kèm chữ). */
const statusTone: Record<PortalOrderStatus, StatusTone> = {
  Pending: 'warning',
  Confirmed: 'success',
  Rejected: 'error',
  Received: 'neutral',
  Cancelled: 'error',
}

const listColumns: GridColumn<PortalOrderListItem>[] = [
  { key: 'no', title: 'Số đơn', width: 150, value: (r) => r.no, auditField: false },
  { key: 'companyName', title: 'Công ty đặt hàng', width: 200, value: (r) => r.companyName, auditField: false },
  { key: 'date', title: 'Ngày đặt', width: 90, type: 'date', value: (r) => r.date, auditField: false },
  { key: 'expectedDate', title: 'Ngày giao dự kiến', width: 110, type: 'date', value: (r) => r.expectedDate, auditField: false },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 140,
    type: 'status',
    value: (r) => portalOrderStatusLabels[r.status],
    tone: (r) => statusTone[r.status],
    auditField: false,
  },
  { key: 'promisedDate', title: 'Ngày giao cam kết', width: 110, type: 'date', value: (r) => r.promisedDate, auditField: false },
  { key: 'totalPayable', title: 'Tổng thanh toán', width: 130, type: 'money', value: (r) => r.totalPayable, auditField: false },
  { key: 'currencyCode', title: 'Tiền tệ', width: 60, value: (r) => r.currencyCode, auditField: false },
]

/**
 * Danh sách đơn đặt hàng của NCC (lát P4a – design.md 19.5, P4-1): PO đã duyệt đặt cho NCC mình (kể cả đã nhận đủ, bên đặt đã hủy).
 * Bấm đúp mở để xem và xác nhận / từ chối.
 */
export function PortalOrderListPage() {
  const navigate = useNavigate()
  const orders = useQuery({ queryKey: ['portal', 'orders'], queryFn: portalApi.orders })
  const [groupBy, setGroupBy] = useState<string | null>(null)
  return (
    <>
      <h1 className="erp-portal-title">Đơn đặt hàng</h1>
      {orders.error && <Alert type="error" showIcon title={orders.error.message} />}
      <Section title="Đơn đặt hàng gửi tới công ty bạn">
        <DataGrid
          rows={orders.data ?? []}
          columns={listColumns}
          rowKey={(r) => r.id}
          groupBy={groupBy}
          onGroupByChange={setGroupBy}
          loading={orders.isFetching}
          onOpenRow={(r) => navigate(`/ncc/don-hang/${r.id}`)}
        />
        <p className="erp-portal-hint">Bấm đúp một dòng để xem đơn và xác nhận hoặc từ chối.</p>
      </Section>
    </>
  )
}

const lineColumns: GridColumn<PortalOrderLine>[] = [
  { key: 'lineNo', title: 'STT', width: 45, type: 'quantity', value: (r) => r.lineNo, auditField: false },
  { key: 'itemCode', title: 'Mã hàng', width: 100, value: (r) => r.itemCode, auditField: false },
  { key: 'supplierItemCode', title: 'Mã hàng NCC', width: 100, value: (r) => r.supplierItemCode, auditField: false },
  { key: 'itemName', title: 'Tên hàng', width: 220, value: (r) => r.itemName, auditField: false },
  { key: 'uomCode', title: 'ĐVT', width: 55, value: (r) => r.uomCode, auditField: false },
  { key: 'qty', title: 'Số lượng', width: 85, type: 'quantity', value: (r) => r.qty, auditField: false },
  { key: 'unitPrice', title: 'Đơn giá', width: 110, type: 'money', value: (r) => r.unitPrice, auditField: false },
  { key: 'discountPct', title: 'CK %', width: 60, type: 'quantity', value: (r) => r.discountPct, auditField: false },
  { key: 'vatCode', title: 'Thuế suất', width: 70, value: (r) => (r.vatCode && /^\d+$/.test(r.vatCode) ? `${r.vatCode}%` : r.vatCode), auditField: false },
  { key: 'amount', title: 'Thành tiền', width: 120, type: 'money', value: (r) => r.amount, auditField: false },
  { key: 'deliveryDate', title: 'Ngày giao', width: 90, type: 'date', value: (r) => r.deliveryDate, auditField: false },
  { key: 'note', title: 'Ghi chú', width: 160, value: (r) => r.note, auditField: false },
]

const responseColumns: GridColumn<PortalOrderResponse>[] = [
  { key: 'respondedAt', title: 'Thời điểm', width: 130, type: 'datetime', value: (r) => r.respondedAt, auditField: false },
  {
    key: 'decision',
    title: 'Phản hồi',
    width: 110,
    type: 'status',
    value: (r) => (r.decision === 'Confirmed' ? 'Xác nhận' : 'Từ chối'),
    tone: (r) => (r.decision === 'Confirmed' ? 'success' : 'error'),
    auditField: false,
  },
  { key: 'promisedDate', title: 'Ngày giao cam kết', width: 110, type: 'date', value: (r) => r.promisedDate, auditField: false },
  { key: 'note', title: 'Ghi chú / lý do', width: 220, value: (r) => r.note, auditField: false },
  { key: 'respondedByEmail', title: 'Người phản hồi', width: 180, value: (r) => r.respondedByEmail, auditField: false },
  {
    key: 'reopen',
    title: 'Bên đặt mở lại',
    width: 260,
    value: (r) => (r.reopenedAt ? `${formatDateTime(r.reopenedAt)} – ${r.reopenReason ?? ''}` : 'Đang có hiệu lực'),
    auditField: false,
  },
]

/**
 * Xem một đơn đặt hàng và phản hồi (lát P4a – design.md 19.5, P4-2, P4-3): NCC **xác nhận** kèm ngày giao cam kết hoặc **từ chối** kèm lý do;
 * gửi rồi không sửa – bên đặt hàng mở lại thì phản hồi lại được (lý do mở lại hiện ở lịch sử). File gửi kèm thêm, xóa trước khi phản hồi.
 */
export function PortalOrderPage() {
  const { id = '' } = useParams()
  const order = useQuery({ queryKey: ['portal', 'order', id], queryFn: () => portalApi.order(id) })
  const [groupBy, setGroupBy] = useState<string | null>(null)
  const [historyGroupBy, setHistoryGroupBy] = useState<string | null>(null)
  const data = order.data

  if (!data) {
    return (
      <Spin spinning={order.isFetching}>
        {order.error ? <Alert type="error" showIcon title={order.error.message} /> : <div style={{ minHeight: 120 }} />}
      </Spin>
    )
  }

  const current = data.responses.find((r) => !r.reopenedAt)
  const lastReopen = !current ? data.responses.find((r) => r.reopenedAt) : undefined
  return (
    <>
      <h1 className="erp-portal-title">Đơn đặt hàng {data.no}</h1>
      <p className="erp-portal-hint">
        <Link to="/ncc/don-hang">← Đơn đặt hàng</Link>
      </p>
      {data.canRespond ? (
        <Alert
          type="info"
          showIcon
          title={
            lastReopen
              ? `Bên đặt hàng đề nghị phản hồi lại đơn. Lý do: ${lastReopen.reopenReason ?? ''}`
              : 'Đơn đang chờ công ty bạn xác nhận. Xác nhận kèm ngày giao cam kết, hoặc từ chối kèm lý do.'
          }
        />
      ) : (
        <Alert
          type={data.status === 'Confirmed' || data.status === 'Received' ? 'success' : 'warning'}
          showIcon
          title={`${portalOrderStatusLabels[data.status]}.${current ? ` Phản hồi lúc ${formatDateTime(current.respondedAt)} – liên hệ Phòng Cung ứng vật tư nếu cần thay đổi.` : ''}`}
        />
      )}
      <Section title="Thông tin đơn đặt hàng">
        <dl className="erp-portal-fields" data-testid="portal-order-info">
          <dt>Số đơn</dt>
          <dd>{data.no}</dd>
          <dt>Công ty đặt hàng</dt>
          <dd>{data.companyName}</dd>
          <dt>Ngày đặt hàng</dt>
          <dd>{formatDate(data.date)}</dd>
          <dt>Ngày giao dự kiến</dt>
          <dd>{data.expectedDate ? formatDate(data.expectedDate) : '–'}</dd>
          <dt>Nơi nhận hàng</dt>
          <dd>{[data.deliveryPlace, data.deliveryAddress].filter(Boolean).join(' – ') || '–'}</dd>
          <dt>Điều khoản thanh toán</dt>
          <dd>{data.paymentTerms ?? '–'}</dd>
          <dt>Điều khoản giao hàng</dt>
          <dd>{data.deliveryTerms ?? '–'}</dd>
          <dt>Ghi chú</dt>
          <dd>{data.note ?? '–'}</dd>
          <dt>Trạng thái</dt>
          <dd>{portalOrderStatusLabels[data.status]}</dd>
        </dl>
      </Section>
      <Section title={`Dòng hàng (${data.lines.length})`}>
        <div style={{ height: 200, display: 'flex', flexDirection: 'column' }} data-testid="portal-order-lines">
          <DataGrid rows={data.lines} columns={lineColumns} rowKey={(r) => String(r.lineNo)} groupBy={groupBy} onGroupByChange={setGroupBy} />
        </div>
        <dl className="erp-portal-fields erp-portal-totals">
          <dt>Tiền hàng</dt>
          <dd className="erp-number">{formatMoney(data.grossAmount)} {data.currencyCode}</dd>
          <dt>Chiết khấu</dt>
          <dd className="erp-number">{formatMoney(data.totalDiscount)} {data.currencyCode}</dd>
          <dt>Tiền VAT</dt>
          <dd className="erp-number">{formatMoney(data.totalVat)} {data.currencyCode}</dd>
          <dt>Tổng thanh toán</dt>
          <dd className="erp-number" data-testid="portal-order-total">
            <strong>{formatMoney(data.totalAmount + data.totalVat)}</strong> {data.currencyCode}
          </dd>
        </dl>
      </Section>
      {data.canRespond && <RespondForm order={data} />}
      <PortalFiles
        kind="orders"
        id={data.id}
        title="File gửi kèm phản hồi"
        canEdit={data.canRespond}
        maxFileSizeMb={data.maxFileSizeMb}
        testId="portal-order-files"
        hint={
          data.canRespond
            ? `Phiếu xác nhận đơn, lịch giao hàng… tối đa ${data.maxFileSizeMb} MB/file. Thêm, xóa được đến khi gửi phản hồi; gửi rồi file khóa.`
            : 'File đã gửi kèm phản hồi – đã khóa, chỉ tải xuống được.'
        }
      />
      {data.responses.length > 0 && (
        <Section title="Lịch sử phản hồi">
          <div data-testid="portal-order-responses">
            <DataGrid
              rows={data.responses}
              columns={responseColumns}
              rowKey={(r) => r.respondedAt}
              groupBy={historyGroupBy}
              onGroupByChange={setHistoryGroupBy}
            />
          </div>
        </Section>
      )}
    </>
  )
}

/**
 * Ô phản hồi đơn: ngày giao cam kết (mặc định ngày giao dự kiến), ghi chú / lý do; hai nút Từ chối (icon đỏ) – Xác nhận (icon xanh), cả hai
 * hỏi lại trước khi gửi.
 * @param order Đơn đang xem.
 */
function RespondForm({ order }: { order: PortalOrder }) {
  const { message, modal } = App.useApp()
  const queryClient = useQueryClient()
  const [promisedDate, setPromisedDate] = useState<string | null>(order.expectedDate)
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const send = (decision: 'Confirmed' | 'Rejected') => {
    const text = note.trim()
    if (decision === 'Confirmed' && !promisedDate) {
      setError('Nhập ngày giao hàng cam kết.')
      return
    }
    if (decision === 'Rejected' && !text) {
      setError('Nhập lý do từ chối để bên đặt hàng biết xử lý.')
      return
    }
    setError(null)
    modal.confirm({
      title: decision === 'Confirmed' ? 'Xác nhận đơn' : 'Từ chối đơn',
      content:
        decision === 'Confirmed'
          ? `Xác nhận đơn ${order.no}, giao ngày ${formatDate(promisedDate)}? Gửi rồi không sửa được.`
          : `Từ chối đơn ${order.no}? Gửi rồi không sửa được.`,
      okText: decision === 'Confirmed' ? 'Xác nhận' : 'Từ chối',
      cancelText: 'Không',
      okButtonProps: decision === 'Rejected' ? { danger: true } : undefined,
      onOk: async () => {
        setBusy(true)
        try {
          const result = await portalApi.respondOrder(order.id, {
            decision,
            promisedDate: decision === 'Confirmed' ? promisedDate : null,
            note: text || null,
          })
          queryClient.setQueryData(['portal', 'order', order.id], result)
          await queryClient.invalidateQueries({ queryKey: ['portal', 'orders'] })
          await queryClient.invalidateQueries({ queryKey: ['portal', 'orders-files', order.id] })
          void message.success(decision === 'Confirmed' ? `Đã xác nhận đơn ${order.no}.` : `Đã từ chối đơn ${order.no}.`)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không gửi được phản hồi.')
        } finally {
          setBusy(false)
        }
      },
    })
  }

  return (
    <Section title="Phản hồi đơn đặt hàng">
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <dl className="erp-portal-fields">
        <dt>Ngày giao cam kết</dt>
        <dd>
          <DateInput value={promisedDate} onChange={setPromisedDate} aria-label="Ngày giao cam kết" style={{ width: 160 }} />
        </dd>
        <dt>Ghi chú / lý do từ chối</dt>
        <dd>
          <Input.TextArea
            value={note}
            onChange={(e) => setNote(e.target.value)}
            maxLength={1000}
            autoSize={{ minRows: 2, maxRows: 5 }}
            aria-label="Ghi chú / lý do"
            placeholder="Bắt buộc khi từ chối"
          />
        </dd>
      </dl>
      <div className="erp-portal-actions" style={{ marginTop: 8 }}>
        <Button icon={<CloseOutlined />} loading={busy} onClick={() => send('Rejected')}>
          Từ chối đơn
        </Button>
        <Button type="primary" icon={<CheckOutlined />} loading={busy} onClick={() => send('Confirmed')}>
          Xác nhận đơn
        </Button>
      </div>
    </Section>
  )
}
