import { useQuery } from '@tanstack/react-query'
import { Alert, Spin } from 'antd'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { DataGrid } from '../components/DataGrid/DataGrid'
import type { GridColumn, StatusTone } from '../components/DataGrid/types'
import { formatDate, formatMoney } from '../utils/format'
import {
  portalApi,
  portalInvoiceStatusLabels,
  portalPaymentStatusLabels,
  type PortalInvoiceStatus,
  type PortalPaymentInvoice,
  type PortalPaymentOrder,
  type PortalPaymentRound,
  type PortalPaymentStatus,
} from './portalApi'
import { Section } from './PortalParts'

/** Đèn trạng thái thanh toán của đơn (như đèn TTPO – Y9-3; luôn kèm chữ). */
const orderTone: Record<PortalPaymentStatus, StatusTone> = {
  NoInvoice: 'neutral',
  Unpaid: 'error',
  PartlyPaid: 'warning',
  Paid: 'success',
}

/** Đèn trạng thái thanh toán của hóa đơn. */
const invoiceTone: Record<PortalInvoiceStatus, StatusTone> = {
  AwaitingInvoice: 'neutral',
  Invoiced: 'error',
  Processing: 'warning',
  Approved: 'warning',
  Paid: 'success',
}

const orderColumns: GridColumn<PortalPaymentOrder>[] = [
  { key: 'orderNo', title: 'Số đơn', width: 135, value: (r) => r.orderNo, auditField: false },
  { key: 'companyName', title: 'Công ty đặt hàng', width: 160, value: (r) => r.companyName, auditField: false },
  { key: 'orderDate', title: 'Ngày đặt', width: 85, type: 'date', value: (r) => r.orderDate, auditField: false },
  {
    key: 'status',
    title: 'Thanh toán',
    width: 160,
    type: 'status',
    value: (r) => portalPaymentStatusLabels[r.status],
    tone: (r) => orderTone[r.status],
    auditField: false,
  },
  { key: 'outstandingAmount', title: 'Còn phải trả', width: 110, type: 'money', value: (r) => r.outstandingAmount, auditField: false },
  { key: 'invoiceAmount', title: 'Tiền hóa đơn', width: 110, type: 'money', value: (r) => r.invoiceAmount, auditField: false },
  { key: 'paidAmount', title: 'Đã thanh toán', width: 110, type: 'money', value: (r) => r.paidAmount, auditField: false },
  { key: 'deductionAmount', title: 'Giảm trừ', width: 100, type: 'money', value: (r) => r.deductionAmount, auditField: false },
  { key: 'orderAmount', title: 'Giá trị đơn', width: 120, type: 'money', value: (r) => r.orderAmount, auditField: false },
  { key: 'receivedAmount', title: 'Đã nhận hàng', width: 120, type: 'money', value: (r) => r.receivedAmount, auditField: false },
  { key: 'awaitingInvoiceCount', title: 'Phiếu chờ HĐ', width: 90, type: 'quantity', value: (r) => r.awaitingInvoiceCount, auditField: false },
  { key: 'lastPaidDate', title: 'Ngày TT gần nhất', width: 110, type: 'date', value: (r) => r.lastPaidDate, auditField: false },
]

const invoiceColumns: GridColumn<PortalPaymentInvoice>[] = [
  { key: 'receiptNo', title: 'Số phiếu nhận hàng', width: 140, value: (r) => r.receiptNo, auditField: false },
  { key: 'receivedDate', title: 'Ngày nhận', width: 90, type: 'date', value: (r) => r.receivedDate, auditField: false },
  { key: 'invoiceNo', title: 'Số hóa đơn', width: 95, value: (r) => r.invoiceNo, auditField: false },
  { key: 'invoiceDate', title: 'Ngày HĐ', width: 90, type: 'date', value: (r) => r.invoiceDate, auditField: false },
  { key: 'invoiceAmount', title: 'Tiền hóa đơn', width: 115, type: 'money', value: (r) => r.invoiceAmount, auditField: false },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 150,
    type: 'status',
    value: (r) => portalInvoiceStatusLabels[r.status],
    tone: (r) => invoiceTone[r.status],
    auditField: false,
  },
  { key: 'paidDate', title: 'Ngày thanh toán', width: 105, type: 'date', value: (r) => r.paidDate, auditField: false },
  { key: 'paymentNo', title: 'Đợt thanh toán', width: 135, value: (r) => r.paymentNo, auditField: false },
  { key: 'invoiceSeries', title: 'Ký hiệu', width: 85, value: (r) => r.invoiceSeries, auditField: false },
  { key: 'receivedAmount', title: 'Giá trị nhận', width: 115, type: 'money', value: (r) => r.receivedAmount, auditField: false },
  { key: 'deliveryNoteNo', title: 'Số phiếu giao', width: 110, value: (r) => r.deliveryNoteNo, auditField: false },
]

const roundColumns: GridColumn<PortalPaymentRound>[] = [
  { key: 'no', title: 'Đợt thanh toán', width: 135, value: (r) => r.no, auditField: false },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 125,
    type: 'status',
    value: (r) => (r.status === 'Paid' ? 'Đã thanh toán' : 'Đã duyệt – chờ chi'),
    tone: (r) => (r.status === 'Paid' ? 'success' : 'warning'),
    auditField: false,
  },
  { key: 'paidDate', title: 'Ngày thanh toán', width: 105, type: 'date', value: (r) => r.paidDate, auditField: false },
  { key: 'payableAmount', title: 'Số tiền thanh toán', width: 125, type: 'money', value: (r) => r.payableAmount, auditField: false },
  { key: 'totalAmount', title: 'Tổng hóa đơn', width: 115, type: 'money', value: (r) => r.totalAmount, auditField: false },
  { key: 'deductionAmount', title: 'Giảm trừ', width: 100, type: 'money', value: (r) => r.deductionAmount, auditField: false },
  { key: 'deductionReason', title: 'Lý do giảm trừ', width: 160, value: (r) => r.deductionReason, auditField: false },
  { key: 'invoiceNos', title: 'Hóa đơn', width: 120, value: (r) => r.invoiceNos, auditField: false },
  { key: 'approvedDate', title: 'Ngày duyệt', width: 90, type: 'date', value: (r) => r.approvedDate, auditField: false },
  {
    key: 'method',
    title: 'Hình thức',
    width: 260,
    value: (r) =>
      r.method === 'Cash'
        ? 'Tiền mặt'
        : `Chuyển khoản – ${[r.bankAccountNumber, r.bankName, r.bankAccountName].filter(Boolean).join(' – ')}`,
    auditField: false,
  },
]

/**
 * Danh sách tình hình thanh toán các đơn của NCC (lát P6b – design.md 19.5, P6b-1, P6b-4): đơn đã có phiếu nhận hàng xác nhận hoặc đề nghị
 * thanh toán. Bấm đúp mở chi tiết hóa đơn và đợt thanh toán.
 */
export function PortalPaymentListPage() {
  const navigate = useNavigate()
  const payments = useQuery({ queryKey: ['portal', 'payments'], queryFn: portalApi.payments })
  const [groupBy, setGroupBy] = useState<string | null>(null)
  const rows = payments.data ?? []
  const outstanding = rows.reduce((sum, r) => sum + r.outstandingAmount, 0)
  return (
    <>
      <h1 className="erp-portal-title">Thanh toán</h1>
      {payments.error && <Alert type="error" showIcon title={payments.error.message} />}
      <Section title="Tình hình thanh toán theo đơn đặt hàng">
        <DataGrid
          rows={rows}
          columns={orderColumns}
          rowKey={(r) => r.orderId}
          groupBy={groupBy}
          onGroupByChange={setGroupBy}
          loading={payments.isFetching}
          onOpenRow={(r) => navigate(`/ncc/thanh-toan/${r.orderId}`)}
        />
        <dl className="erp-portal-fields erp-portal-totals">
          <dt>Tổng còn phải trả</dt>
          <dd className="erp-number" data-testid="portal-payments-outstanding">
            <strong>{formatMoney(outstanding)}</strong> VND
          </dd>
        </dl>
        <p className="erp-portal-hint">
          Số liệu theo hóa đơn bên đặt hàng đã ghi nhận trên phiếu nhận hàng; hàng đã nhận chưa có hóa đơn chưa tính vào còn phải trả. Bấm đúp
          một dòng để xem hóa đơn và các đợt thanh toán.
        </p>
      </Section>
    </>
  )
}

/** Chi tiết thanh toán một đơn – trang riêng, mở từ danh sách Thanh toán hoặc link trong thư "đã thanh toán". */
export function PortalPaymentPage() {
  const { id = '' } = useParams()
  return (
    <>
      <p className="erp-portal-hint">
        <Link to="/ncc/thanh-toan">← Thanh toán</Link>
      </p>
      <PortalPaymentPanel orderId={id} standalone />
    </>
  )
}

/**
 * Khối thanh toán của một đơn (P6b-2…P6b-4): tổng hợp, hóa đơn – phiếu nhận hàng, đợt thanh toán đã duyệt / đã chi. Trên trang đơn
 * (`standalone` = false) chỉ hiện khi đơn đã có phiếu nhận hàng hoặc đợt thanh toán.
 * @param orderId PO.
 * @param standalone Trang Thanh toán riêng (có tiêu đề trang, luôn hiện).
 */
export function PortalPaymentPanel({ orderId, standalone = false }: { orderId: string; standalone?: boolean }) {
  const detail = useQuery({ queryKey: ['portal', 'payment', orderId], queryFn: () => portalApi.payment(orderId) })
  const [invoiceGroupBy, setInvoiceGroupBy] = useState<string | null>(null)
  const [roundGroupBy, setRoundGroupBy] = useState<string | null>(null)
  const data = detail.data

  if (!data) {
    if (!standalone && !detail.error) return null
    return (
      <Spin spinning={detail.isFetching}>
        {detail.error ? <Alert type="error" showIcon title={detail.error.message} /> : <div style={{ minHeight: 120 }} />}
      </Spin>
    )
  }
  const s = data.summary
  if (!standalone && data.invoices.length === 0 && data.rounds.length === 0) return null
  return (
    <>
      {standalone && <h1 className="erp-portal-title">Thanh toán đơn {s.orderNo}</h1>}
      <Section title={`Thanh toán – ${portalPaymentStatusLabels[s.status]}`}>
        {standalone && (
          <dl className="erp-portal-fields" style={{ marginBottom: 4 }}>
            <dt>Đơn đặt hàng</dt>
            <dd>
              <Link to={`/ncc/don-hang/${s.orderId}`}>{s.orderNo}</Link> – {s.companyName} – ngày {formatDate(s.orderDate)}
            </dd>
          </dl>
        )}
        {/* Hai cột nhãn – số đọc từ trái sang phải: giá trị đơn / hàng đã nhận bên trái, hóa đơn → thanh toán bên phải. */}
        <dl className="erp-portal-fields erp-portal-pay-summary" data-testid="portal-payment-summary">
          <dt>Giá trị đơn</dt>
          <dd className="erp-number">{formatMoney(s.orderAmount)}</dd>
          <dt>Tiền hóa đơn</dt>
          <dd className="erp-number">{formatMoney(s.invoiceAmount)}</dd>
          <dt>Đã nhận hàng</dt>
          <dd className="erp-number">{formatMoney(s.receivedAmount)}</dd>
          <dt>Đang xử lý thanh toán</dt>
          <dd className="erp-number">{formatMoney(s.processingAmount)}</dd>
          <dt>Phiếu nhận hàng</dt>
          <dd className="erp-number">
            {s.receiptCount} phiếu, {s.awaitingInvoiceCount} chờ hóa đơn
          </dd>
          <dt>Đã duyệt – chờ chi</dt>
          <dd className="erp-number">{formatMoney(s.approvedUnpaidAmount)}</dd>
          <dt>Thanh toán gần nhất</dt>
          <dd className="erp-number">{s.lastPaidDate ? formatDate(s.lastPaidDate) : '–'}</dd>
          <dt>Đã thanh toán</dt>
          <dd className="erp-number">{formatMoney(s.paidAmount)}</dd>
          <dt>Tiền tệ</dt>
          <dd className="erp-number">VND (gồm VAT)</dd>
          <dt>Giảm trừ đã cấn trừ</dt>
          <dd className="erp-number">{formatMoney(s.deductionAmount)}</dd>
          <dt />
          <dd />
          <dt>
            <strong>Còn phải trả</strong>
          </dt>
          <dd className="erp-number" data-testid="portal-payment-outstanding">
            <strong>{formatMoney(s.outstandingAmount)}</strong>
          </dd>
        </dl>
      </Section>
      <Section title={`Hóa đơn – phiếu nhận hàng (${data.invoices.length})`}>
        <div data-testid="portal-payment-invoices">
          <DataGrid
            rows={data.invoices}
            columns={invoiceColumns}
            rowKey={(r) => r.receiptNo}
            groupBy={invoiceGroupBy}
            onGroupByChange={setInvoiceGroupBy}
          />
        </div>
        <p className="erp-portal-hint">
          Phiếu "Chờ hóa đơn": bên đặt hàng đã nhận hàng nhưng chưa ghi nhận hóa đơn – gửi hóa đơn theo kênh tiếp nhận của bên đặt hàng.
        </p>
      </Section>
      {data.rounds.length > 0 && (
        <Section title={`Đợt thanh toán (${data.rounds.length})`}>
          <div data-testid="portal-payment-rounds">
            <DataGrid rows={data.rounds} columns={roundColumns} rowKey={(r) => r.no} groupBy={roundGroupBy} onGroupByChange={setRoundGroupBy} />
          </div>
        </Section>
      )}
    </>
  )
}
