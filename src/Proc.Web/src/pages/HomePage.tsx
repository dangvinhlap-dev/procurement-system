import { useQuery } from '@tanstack/react-query'
import { Alert, Card, Spin, Statistic, Table, Typography, theme } from 'antd'
import type { ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { useDashboard, type Dashboard } from '../api/dashboard'
import { fetchHealth } from '../api/health'
import { screens } from '../app/screens'
import { useAuth } from '../auth/useAuth'
import { formatDate, formatMoney } from '../utils/format'
import './HomePage.css'

/** Nhãn trạng thái kỳ kho. */
const periodLabels: Record<string, string> = { NotOpened: 'Chưa mở', Open: 'Đang mở', Costing: 'Đang tính giá', Closed: 'Đã đóng' }

/** Một ô số liệu trên trang chủ. */
interface Tile {
  /** Mã màn hình mở khi bấm (ô lệnh, screens.tsx). */
  code: string
  title: string
  value: number | string
  /** Dòng phụ dưới số. */
  note?: string
  /** Cần chú ý: số màu đỏ. */
  attention?: boolean
}

/**
 * Dựng các ô số liệu theo phần người dùng được xem (phần null = không có quyền thì không có ô).
 * @param d Số liệu trang chủ.
 */
function buildTiles(d: Dashboard): Tile[] {
  const tiles: Tile[] = [{ code: 'TODO', title: 'Phiếu chờ tôi duyệt', value: d.purchase.pendingApprovals, attention: d.purchase.pendingApprovals > 0 }]
  const p = d.purchase
  if (p.pendingRequests != null)
    tiles.push({ code: 'PRPEND', title: 'PR chưa xử lý', value: p.pendingRequests, note: `${p.overdueRequests} PR quá ngày cần`, attention: (p.overdueRequests ?? 0) > 0 })
  if (p.openOrders != null)
    tiles.push({ code: 'POTRK', title: 'PO đang chờ giao', value: p.openOrders, note: `${p.lateOrders} PO trễ hạn`, attention: (p.lateOrders ?? 0) > 0 })
  if (p.openRfqs != null) tiles.push({ code: 'RFQ', title: 'RFQ đang nhận báo giá', value: p.openRfqs })
  const s = d.stock
  if (s) {
    const drafts = Object.values(s.draftDocuments).reduce((sum, n) => sum + n, 0)
    if (Object.keys(s.draftDocuments).length > 0 || drafts === 0)
      tiles.push({ code: 'GRN', title: 'Chứng từ kho nháp', value: drafts, note: draftNote(s.draftDocuments) })
    if (s.belowMinItems != null) tiles.push({ code: 'STK', title: 'Hàng dưới tồn tối thiểu', value: s.belowMinItems, attention: s.belowMinItems > 0 })
    if (s.expiredLots != null)
      tiles.push({
        code: 'EXP',
        title: 'Lô hết hạn / sắp hết hạn',
        value: `${s.expiredLots} / ${s.expiringLots}`,
        note: 'Đã hết hạn / trong 30 ngày tới',
        attention: s.expiredLots > 0,
      })
    if (s.goodsInTransit != null) tiles.push({ code: 'TRANSIT', title: 'Hàng đi đường nội bộ', value: s.goodsInTransit })
    if (s.periodStatus)
      tiles.push({ code: 'PER', title: `Kỳ kho ${s.periodLabel}`, value: periodLabels[s.periodStatus] ?? s.periodStatus, attention: s.periodStatus !== 'Open' })
  }
  return tiles
}

/** Dòng phụ "GRN 2 · TRF 1" của ô chứng từ nháp. */
function draftNote(drafts: Record<string, number>) {
  const parts = Object.entries(drafts).map(([type, n]) => `${type} ${n}`)
  return parts.length > 0 ? parts.join(' · ') : 'Không có phiếu nháp'
}

/**
 * Trang chủ (design.md mục 11.4): các ô số liệu của công ty đang làm việc – bấm để mở màn hình chi tiết (cùng cách tính nên số khớp);
 * mua hàng tháng này so với tháng trước và 5 NCC mua nhiều nhất; trạng thái hệ thống. Chỉ hiện phần người dùng có quyền xem.
 */
export function HomePage() {
  const navigate = useNavigate()
  const { workingCompany } = useAuth()
  const { token } = theme.useToken()
  const dashboard = useDashboard()
  const health = useQuery({ queryKey: ['health'], queryFn: fetchHealth, retry: false })
  const d = dashboard.data

  const open = (code: string) => {
    const screen = screens.find((s) => s.code === code)
    if (screen) navigate(screen.path)
  }

  let body: ReactNode = <Spin />
  if (dashboard.isError) body = <Alert type="error" showIcon title="Không tải được số liệu trang chủ" description={dashboard.error.message} />
  else if (d)
    body = (
      <>
        <div className="home-tiles">
          {buildTiles(d).map((t) => (
            <Card key={t.code} size="small" hoverable className="home-tile" onClick={() => open(t.code)} role="button" aria-label={t.title}>
              <Statistic title={t.title} value={t.value} valueStyle={{ color: t.attention ? token.colorError : token.colorText }} />
              {t.note && <div className="home-tile-note">{t.note}</div>}
            </Card>
          ))}
        </div>
        {d.spend && <SpendPanel spend={d.spend} onOpen={() => open('RPSUP')} />}
      </>
    )

  return (
    <div className="simple-page home-page">
      <Typography.Title level={4}>
        Trang chủ – {workingCompany?.code} {workingCompany?.name}
      </Typography.Title>
      <Typography.Text type="secondary">Số liệu ngày {d ? formatDate(d.today) : ''}; tự cập nhật mỗi phút. Bấm một ô để xem chi tiết.</Typography.Text>
      {body}
      <div className="home-health">
        {health.isError && <Alert type="error" showIcon title="Không kết nối được API" description={health.error.message} />}
        {health.data && (
          <Typography.Text type="secondary">
            Hệ thống: {health.data.status === 'Healthy' ? 'hoạt động bình thường' : 'có lỗi'} · Cơ sở dữ liệu:{' '}
            {health.data.checks.every((c) => c.status === 'Healthy') ? 'kết nối được' : 'lỗi'} · Phiên bản API {health.data.version}
          </Typography.Text>
        )}
      </div>
    </div>
  )
}

/**
 * Khung mua hàng thuần tháng này / tháng trước và top 5 NCC.
 * @param spend Số liệu chi tiêu.
 * @param onOpen Mở báo cáo Mua hàng theo NCC.
 */
function SpendPanel({ spend, onOpen }: { spend: NonNullable<Dashboard['spend']>; onOpen: () => void }) {
  const change = spend.lastMonth > 0 ? Math.round(((spend.thisMonth - spend.lastMonth) / spend.lastMonth) * 1000) / 10 : null
  return (
    <Card size="small" className="home-spend" title="Mua hàng (trước VAT, nhập − trả NCC)" extra={<a onClick={onOpen}>Xem báo cáo</a>}>
      <div className="home-spend-totals">
        <Statistic title="Tháng này" value={formatMoney(spend.thisMonth)} suffix="đ" />
        <Statistic title="Tháng trước" value={formatMoney(spend.lastMonth)} suffix="đ" />
        {change != null && <Statistic title="So với tháng trước" value={`${change > 0 ? '+' : ''}${change.toLocaleString('vi-VN')}%`} />}
      </div>
      <Table
        size="small"
        pagination={false}
        rowKey="code"
        dataSource={spend.topSuppliers}
        locale={{ emptyText: 'Tháng này chưa nhập mua' }}
        columns={[
          { title: 'Mã NCC', dataIndex: 'code', width: 120 },
          { title: 'Nhà cung cấp (top 5 tháng này)', dataIndex: 'name' },
          { title: 'Mua thuần', dataIndex: 'netAmount', align: 'right', width: 160, render: (v: number) => formatMoney(v) },
        ]}
      />
    </Card>
  )
}
