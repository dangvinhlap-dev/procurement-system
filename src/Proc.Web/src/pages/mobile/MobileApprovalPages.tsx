import { CheckOutlined, CloseOutlined, DesktopOutlined, LeftOutlined, LogoutOutlined, ReloadOutlined } from '@ant-design/icons'
import { Alert, App, Button, ConfigProvider, Input } from 'antd'
import { useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useApprovalDecision, type ApprovalDecision } from '../../api/mobileApprovals'
import { purchaseSummaryFormLabels, usePurchaseSummary, type PurchaseSummary } from '../../api/purchaseSummaries'
import {
  priceSourceLabels,
  purchaseTypeLabels,
  usePendingApprovals,
  usePurchaseOrder,
  usePurchaseRequest,
  type ApprovalStep,
  type PendingApproval,
  type PurchaseOrder,
  type PurchaseRequest,
} from '../../api/purchasing'
import { approvalFullPaths, isMobileApprovable } from '../../app/approvalRoutes'
import { useAuth } from '../../auth/useAuth'
import { ClassicScreen } from '../../classic/ClassicScreen'
import { Select } from '../../classic/Select'
import { classicMobileTheme } from '../../classic/classicTheme'
import '../../classic/classic-mobile.css'
import { formatDate, formatDateTime, formatMoney, formatQuantity } from '../../utils/format'
import { EnvironmentBanner } from '../../classic/EnvironmentBanner'

/**
 * Khung trang duyệt gọn trên điện thoại (lát O2d, design.md mục 19.3, docs/ui-style.md mục 5c): một cột, thanh tiêu đề dải màu cổ điển,
 * ô chọn công ty (người làm việc nhiều công ty) và nút về bản đầy đủ. Đứng ngoài AppShell vì cây điều hướng, thanh menu không dùng được trên
 * màn hình hẹp.
 * @param title Tiêu đề trên thanh.
 * @param onBack Có thì hiện nút quay lại bên trái.
 * @param children Nội dung trang.
 */
function MobileFrame({ title, onBack, children }: { title: string; onBack?: () => void; children: ReactNode }) {
  const { session, workingCompany, setWorkingCompany, logout } = useAuth()
  const companies = session?.user.companies ?? []
  return (
    <ClassicScreen className="erp-mobile">
      <EnvironmentBanner />
      <ConfigProvider theme={classicMobileTheme}>
        <header className="erp-mobile-bar">
          {onBack && <Button type="text" icon={<LeftOutlined />} onClick={onBack} aria-label="Quay lại" title="Quay lại" />}
          <h1 className="erp-mobile-title">{title}</h1>
          <Link to="/" className="erp-mobile-bar-link" title="Mở bản đầy đủ (máy tính)"><DesktopOutlined /> Bản đầy đủ</Link>
        </header>
        <div className="erp-mobile-user">
          <span>{session?.user.fullName}</span>
          {companies.length > 1 ? (
            <Select
              aria-label="Công ty"
              showSearch={false}
              value={workingCompany?.id}
              onChange={(v) => setWorkingCompany(v as string)}
              options={companies.map((c) => ({ value: c.id, label: c.shortName ?? c.code }))}
              className="erp-mobile-company"
            />
          ) : (
            <span>{workingCompany?.shortName ?? workingCompany?.code}</span>
          )}
          <Button type="text" icon={<LogoutOutlined />} onClick={() => logout()} aria-label="Đăng xuất" title="Đăng xuất" />
        </div>
        <main className="erp-mobile-body">{children}</main>
      </ConfigProvider>
    </ClassicScreen>
  )
}

/**
 * Trang `/duyet`: danh sách phiếu chờ chính tôi duyệt ở công ty đang làm việc – cùng nguồn với màn hình Việc cần làm
 * (`GET /api/approvals/pending`, gồm phiếu được ủy quyền duyệt thay – lát O2a). Mỗi phiếu một thẻ, bấm để xem và Duyệt / Từ chối.
 */
export function MobileApprovalListPage() {
  const navigate = useNavigate()
  const pending = usePendingApprovals()
  const rows = pending.data ?? []
  return (
    <MobileFrame title="Phiếu chờ tôi duyệt">
      <div className="erp-mobile-toolbar">
        <span>{pending.isLoading ? 'Đang tải…' : `${rows.length} phiếu`}</span>
        <Button icon={<ReloadOutlined className="erp-tone--warning" />} onClick={() => void pending.refetch()} loading={pending.isFetching}>
          Tải lại
        </Button>
      </div>
      {pending.isError && <Alert type="error" showIcon title={(pending.error as Error).message} />}
      {!pending.isLoading && rows.length === 0 && !pending.isError && (
        <p className="erp-mobile-empty">Không có phiếu nào chờ bạn duyệt ở công ty này.</p>
      )}
      <ul className="erp-mobile-cards">
        {rows.map((r) => (
          <li key={`${r.docType}-${r.documentId}`}>
            <button type="button" className="erp-mobile-card" onClick={() => navigate(`/duyet/${r.docType}/${r.documentId}`)}>
              <span className="erp-mobile-card-head">
                <strong>{r.no}</strong>
                <span>{r.docTypeName}</span>
              </span>
              {r.description && <span className="erp-mobile-card-desc">{r.description}</span>}
              <span className="erp-mobile-card-foot">
                <span>{formatDate(r.date)} · Cấp {r.level}{r.createdByName ? ` · ${r.createdByName}` : ''}</span>
                {r.amount > 0 && <span className="erp-mobile-money">{formatMoney(r.amount)}</span>}
              </span>
            </button>
          </li>
        ))}
      </ul>
    </MobileFrame>
  )
}

/**
 * Trang `/duyet/:docType/:id`: tóm tắt phiếu và nút Duyệt / Từ chối. PR, PO, bảng tổng hợp BTH hiện thêm dòng hàng, cảnh báo và lịch sử duyệt lượt hiện tại;
 * loại khác hiện thông tin như Việc cần làm kèm nút mở bản đầy đủ để xem kỹ. Từ chối bắt buộc lý do (design.md mục 18.6). Phiếu không còn
 * chờ người dùng duyệt (đã duyệt, đổi công ty, chưa đến lượt) thì chỉ cho xem – máy chủ vẫn kiểm tra lại khi bấm.
 */
export function MobileApprovalDetailPage() {
  const { docType = '', id = '' } = useParams()
  const navigate = useNavigate()
  const { modal, message } = App.useApp()
  const pending = usePendingApprovals()
  const request = usePurchaseRequest(docType === 'PR' ? id : null)
  const order = usePurchaseOrder(docType === 'PO' ? id : null)
  const summary = usePurchaseSummary(docType === 'BTH' ? id : null)
  const decide = useApprovalDecision()
  const [comment, setComment] = useState('')
  const row = pending.data?.find((r) => r.docType === docType && r.documentId === id)
  const fullPath = isMobileApprovable(docType) ? approvalFullPaths[docType]!(id) : null
  const back = () => navigate('/duyet')

  if (!isMobileApprovable(docType)) {
    return (
      <MobileFrame title="Duyệt phiếu" onBack={back}>
        <Alert type="warning" showIcon title={`Loại phiếu ${docType} chưa duyệt được trên trang gọn – hãy mở bản đầy đủ trên máy tính.`} />
      </MobileFrame>
    )
  }

  const no = row?.no ?? request.data?.no ?? order.data?.no ?? summary.data?.no ?? ''
  const run = (decision: ApprovalDecision) => {
    const text = comment.trim()
    if (decision === 'reject' && !text) {
      message.error('Hãy nhập lý do từ chối – người lập sẽ thấy lý do để sửa.')
      return
    }
    modal.confirm({
      title: decision === 'approve' ? `Duyệt phiếu ${no}?` : `Từ chối phiếu ${no}?`,
      content: text ? `${decision === 'approve' ? 'Ý kiến' : 'Lý do'}: ${text}` : undefined,
      okText: decision === 'approve' ? 'Duyệt' : 'Từ chối',
      okButtonProps: { danger: decision === 'reject' },
      cancelText: 'Không',
      onOk: async () => {
        try {
          await decide.mutateAsync({ docType, id, decision, comment: text || null })
          message.success(decision === 'approve' ? `Đã duyệt ${no}.` : `Đã từ chối ${no}.`)
          back()
        } catch (e) {
          message.error((e as Error).message)
        }
      },
    })
  }

  const loading = pending.isLoading || request.isLoading || order.isLoading || summary.isLoading
  return (
    <MobileFrame title={no || 'Duyệt phiếu'} onBack={back}>
      {loading && <p className="erp-mobile-empty">Đang tải…</p>}
      {!loading && !row && (
        <Alert type="info" showIcon title="Phiếu không còn chờ bạn duyệt (đã được duyệt, bị từ chối, chưa đến lượt bạn hoặc thuộc công ty khác)." />
      )}
      {row && <PendingSummary row={row} />}
      {request.data && <RequestDetail doc={request.data} />}
      {order.data && <OrderDetail doc={order.data} />}
      {summary.data && <SummaryDetail doc={summary.data} />}
      {fullPath && (
        <p>
          <Link to={fullPath}>Xem bản đầy đủ (đính kèm, lịch sử, so sánh giá…)</Link>
        </p>
      )}
      {row && (
        <section className="erp-mobile-decision">
          <label htmlFor="mobile-approval-comment">Ý kiến / lý do (bắt buộc khi từ chối)</label>
          <Input.TextArea id="mobile-approval-comment" rows={3} maxLength={500} value={comment} onChange={(e) => setComment(e.target.value)} />
          <div className="erp-mobile-actions">
            <Button icon={<CloseOutlined />} onClick={() => run('reject')} disabled={decide.isPending}>Từ chối</Button>
            <Button type="primary" icon={<CheckOutlined />} onClick={() => run('approve')} loading={decide.isPending}>
              Duyệt
            </Button>
          </div>
        </section>
      )}
    </MobileFrame>
  )
}

/**
 * Một dòng nhãn – giá trị của khối thông tin.
 * @param label Nhãn.
 * @param children Giá trị; trống thì không vẽ dòng.
 * @param money Giá trị là số tiền (canh phải, phông đơn cách).
 */
function Row({ label, children, money = false }: { label: string; children: ReactNode; money?: boolean }) {
  if (children === null || children === undefined || children === '') return null
  return (
    <div className="erp-mobile-row">
      <span className="erp-mobile-label">{label}</span>
      <span className={money ? 'erp-mobile-money' : undefined}>{children}</span>
    </div>
  )
}

/** Thông tin chung của phiếu chờ duyệt (từ danh sách Việc cần làm) – đủ cho mọi loại phiếu. */
function PendingSummary({ row }: { row: PendingApproval }) {
  return (
    <section className="erp-mobile-section">
      <h2>{row.docTypeName}</h2>
      <Row label="Số phiếu">{row.no}</Row>
      <Row label="Ngày">{formatDate(row.date)}</Row>
      <Row label="Nội dung">{row.description}</Row>
      <Row label="Giá trị" money>{row.amount > 0 ? formatMoney(row.amount) : null}</Row>
      <Row label="Người lập">{row.createdByName}</Row>
      <Row label="Cấp chờ duyệt">{`Cấp ${row.level}`}</Row>
    </section>
  )
}

/** Chi tiết đề nghị mua: mục đích, ngày cần, dòng hàng (cảnh báo vượt định mức – lát G11), lịch sử duyệt. */
function RequestDetail({ doc }: { doc: PurchaseRequest }) {
  return (
    <>
      <section className="erp-mobile-section">
        <h2>Đề nghị</h2>
        <Row label="Bộ phận">{doc.department}</Row>
        <Row label="Người đề nghị">{doc.requesterName}</Row>
        <Row label="Mục đích">{doc.purpose}</Row>
        <Row label="Ngày cần">{doc.neededDate ? formatDate(doc.neededDate) : null}</Row>
        <Row label="Ghi chú">{doc.note}</Row>
        <Row label="Tổng dự kiến" money>{formatMoney(doc.totalAmount)}</Row>
      </section>
      <section className="erp-mobile-section">
        <h2>Dòng hàng ({doc.lines.length + doc.newItems.length})</h2>
        <ul className="erp-mobile-lines">
          {doc.lines.map((l) => (
            <li key={l.id}>
              <strong>{l.itemCode} – {l.itemName}</strong>
              <span>
                {formatQuantity(l.qty)} {l.uomCode} × {formatMoney(l.estPrice)} = <span className="erp-mobile-money">{formatMoney(l.amount)}</span>
              </span>
              {l.normStatus === 'Over' && <span className="erp-mobile-warn">Vượt định mức sử dụng lũy kế</span>}
              {l.normStatus === 'NoPlan' && <span className="erp-mobile-warn">Ngoài kế hoạch nhu cầu</span>}
            </li>
          ))}
          {doc.newItems.map((n) => (
            <li key={n.id}>
              <strong>Hàng mới: {n.description}</strong>
              <span>{formatQuantity(n.qty)} × {formatMoney(n.estPrice)}</span>
            </li>
          ))}
        </ul>
      </section>
      <StepList steps={doc.approvals} />
    </>
  )
}

/** Chi tiết đơn mua hàng: NCC, loại mua, cảnh báo giá (vượt trần, ngoài danh sách, lệch giá), dòng hàng, tổng tiền, lịch sử duyệt. */
function OrderDetail({ doc }: { doc: PurchaseOrder }) {
  return (
    <>
      <section className="erp-mobile-section">
        <h2>Đơn mua hàng</h2>
        <Row label="Nhà cung cấp">{`${doc.supplierCode} – ${doc.supplierName}`}</Row>
        <Row label="Bộ phận">{doc.departmentName}</Row>
        <Row label="Loại mua">{purchaseTypeLabels[doc.purchaseType]}</Row>
        <Row label="Lý do khẩn">{doc.emergencyReason}</Row>
        <Row label="Ngày giao">{doc.expectedDate ? formatDate(doc.expectedDate) : null}</Row>
        <Row label="Thanh toán">{doc.paymentTerms}</Row>
        <Row label="Ghi chú">{doc.note}</Row>
        <Row label="Tiền trước VAT" money>{`${formatMoney(doc.totalAmount)} ${doc.currencyCode}`}</Row>
        <Row label="VAT" money>{`${formatMoney(doc.totalVat)} ${doc.currencyCode}`}</Row>
        <Row label="Tổng thanh toán" money>{`${formatMoney(doc.totalAmount + doc.totalVat)} ${doc.currencyCode}`}</Row>
        {doc.isOverCeiling && <p className="erp-mobile-warn">Có dòng vượt giá trần của phiếu duyệt tháng.</p>}
        {doc.isOffApprovedList && <p className="erp-mobile-warn">Có dòng ngoài danh sách / không có giá đã duyệt.</p>}
        {doc.hasPriceException && <p className="erp-mobile-warn">Có dòng lệch giá đã duyệt.</p>}
      </section>
      <section className="erp-mobile-section">
        <h2>Dòng hàng ({doc.lines.length})</h2>
        <ul className="erp-mobile-lines">
          {doc.lines.map((l) => (
            <li key={l.id}>
              <strong>{l.itemCode} – {l.itemName}</strong>
              <span>
                {formatQuantity(l.qty)} {l.uomCode} × {formatMoney(l.unitPrice)}
                {l.discountPct > 0 ? ` (CK ${formatQuantity(l.discountPct)}%)` : ''} = <span className="erp-mobile-money">{formatMoney(l.amount)}</span>
              </span>
              {priceSourceLabels[l.priceSource] && <span className="erp-mobile-muted">Nguồn giá: {priceSourceLabels[l.priceSource]}</span>}
              {l.isOverCeiling && <span className="erp-mobile-warn">Vượt giá trần {l.ceilingPrice !== null ? formatMoney(l.ceilingPrice) : ''}</span>}
              {l.isPriceDeviation && <span className="erp-mobile-warn">Lệch giá đã duyệt{l.priceDeviationReason ? `: ${l.priceDeviationReason}` : ''}</span>}
            </li>
          ))}
        </ul>
      </section>
      <StepList steps={doc.approvals} />
    </>
  )
}

/**
 * Chi tiết bảng tổng hợp đề xuất mua 2A/2B (lát Y5c-2): biểu mẫu, bộ phận, khung dự toán / ngân sách (cảnh báo khi vượt), các dòng đề nghị
 * kèm đơn vị mua, lịch sử duyệt.
 */
function SummaryDetail({ doc }: { doc: PurchaseSummary }) {
  const f = doc.frame
  const construction = doc.form === 'Construction'
  return (
    <>
      <section className="erp-mobile-section">
        <h2>Bảng tổng hợp</h2>
        <Row label="Biểu mẫu">{purchaseSummaryFormLabels[doc.form]}</Row>
        <Row label="Bộ phận">{doc.departmentName}</Row>
        <Row label="Công trình">{construction ? `${f.projectCode ?? ''} – ${f.projectName ?? ''}` : null}</Row>
        <Row label="Ghi chú">{doc.note}</Row>
        <Row label={construction ? 'Dự toán' : 'Ngân sách tháng'} money>{f.limitAmount !== null ? formatMoney(f.limitAmount) : 'Chưa có'}</Row>
        <Row label={construction ? 'Lũy kế đã mua' : 'Lũy kế đã dùng'} money>{formatMoney(f.accumulatedAmount)}</Row>
        <Row label="Tổng đề xuất" money>{formatMoney(doc.totalAmount)}</Row>
        <Row label="Còn lại" money>{f.remainingAmount !== null ? formatMoney(f.remainingAmount) : null}</Row>
        {f.exceeded && <p className="erp-mobile-warn">{construction ? 'Vượt dự toán công trình.' : 'Vượt ngân sách tháng của bộ phận.'}</p>}
      </section>
      <section className="erp-mobile-section">
        <h2>Dòng đề nghị ({doc.lines.length})</h2>
        <ul className="erp-mobile-lines">
          {doc.lines.map((l) => (
            <li key={l.id}>
              <strong>{l.itemCode} – {l.itemName}</strong>
              <span>
                {formatQuantity(l.qty)} {l.uomCode} × {formatMoney(l.estPrice)} = <span className="erp-mobile-money">{formatMoney(l.amount)}</span>
              </span>
              <span className="erp-mobile-muted">{l.requestNo} · Đơn vị mua: {l.purchasingUnitCode} – {l.purchasingUnitName}</span>
            </li>
          ))}
        </ul>
      </section>
      <StepList steps={doc.approvals} />
    </>
  )
}

/** Nhãn trạng thái bước duyệt – luôn có chữ đi kèm đèn (docs/ui-style.md mục 2). */
const stepStatusLabels: Record<ApprovalStep['status'], { label: string; light: string }> = {
  Pending: { label: 'Chờ duyệt', light: 'yellow' },
  Approved: { label: 'Đã duyệt', light: 'green' },
  Rejected: { label: 'Từ chối', light: 'red' },
  Skipped: { label: 'Bỏ qua', light: 'yellow' },
}

/**
 * Rút gọn danh sách người duyệt được cho màn hình hẹp: tối đa 3 tên, còn lại ghi "và N người khác" (vai trò đông người làm thẻ dài cả trang).
 * @param names Họ tên người duyệt được.
 */
function shortNames(names: string[]): string {
  return names.length <= 3 ? names.join(', ') : `${names.slice(0, 3).join(', ')} và ${names.length - 3} người khác`
}

/** Các cấp duyệt của lượt gửi gần nhất: ai đã duyệt, lúc nào, ý kiến; bước đang chờ ghi người duyệt được. */
function StepList({ steps }: { steps: ApprovalStep[] }) {
  if (steps.length === 0) return null
  const round = Math.max(...steps.map((s) => s.round))
  const current = steps.filter((s) => s.round === round)
  return (
    <section className="erp-mobile-section">
      <h2>Lịch sử duyệt</h2>
      <ol className="erp-mobile-steps">
        {current.map((s) => {
          const status = stepStatusLabels[s.status]
          return (
            <li key={`${s.stage}-${s.companyCode ?? ''}-${s.level}`}>
              <span className={`erp-light erp-light--${status.light}`} title={status.label} />
              <span>
                Cấp {s.level}{s.companyCode ? ` · ${s.companyCode}` : ''} ({s.roleName ?? 'người có quyền duyệt'}): {status.label}
                {s.actedByName ? ` – ${s.actedByName}` : ''}
                {s.actedAt ? ` ${formatDateTime(s.actedAt)}` : ''}
                {s.status === 'Pending' && s.nextApprovers?.length ? ` – ${shortNames(s.nextApprovers)}` : ''}
                {s.comment ? <em> “{s.comment}”</em> : null}
              </span>
            </li>
          )
        })}
      </ol>
    </section>
  )
}
