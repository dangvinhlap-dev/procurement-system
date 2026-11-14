import { useQuery } from '@tanstack/react-query'
import { Alert, Spin } from 'antd'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { DataGrid } from '../components/DataGrid/DataGrid'
import type { GridColumn, StatusTone } from '../components/DataGrid/types'
import { formatDate } from '../utils/format'
import {
  portalApi,
  portalPriceListStatusLabels,
  type PortalPriceList,
  type PortalPriceListLine,
  type PortalPriceListStatus,
} from './portalApi'
import { Section } from './PortalParts'

/** Đèn tình trạng bảng giá (luôn kèm chữ): đang áp dụng xanh, chưa đến ngày vàng, hết hạn xám, ngừng đỏ. */
const statusTone: Record<PortalPriceListStatus, StatusTone> = {
  Current: 'success',
  Upcoming: 'warning',
  Expired: 'neutral',
  Inactive: 'error',
}

/** Phạm vi áp dụng: bảng toàn tập đoàn dùng được ở mọi công ty, bảng thường chỉ ở công ty lập. */
const scopeText = (r: PortalPriceList) => (r.isGroupWide ? `Toàn tập đoàn (lập: ${r.companyName})` : r.companyName)

/** Hiệu lực "từ – đến"; không hạn thì ghi "không thời hạn". */
const validityText = (r: PortalPriceList) => `${formatDate(r.validFrom)} – ${r.validTo ? formatDate(r.validTo) : 'không thời hạn'}`

const listColumns: GridColumn<PortalPriceList>[] = [
  { key: 'no', title: 'Số bảng giá', width: 130, value: (r) => r.no, auditField: false },
  { key: 'title', title: 'Tên bảng giá', width: 180, value: (r) => r.title, auditField: false },
  {
    key: 'status',
    title: 'Tình trạng',
    width: 150,
    type: 'status',
    value: (r) => portalPriceListStatusLabels[r.status],
    tone: (r) => statusTone[r.status],
    auditField: false,
  },
  { key: 'validFrom', title: 'Hiệu lực từ', width: 90, type: 'date', value: (r) => r.validFrom, auditField: false },
  { key: 'validTo', title: 'Hiệu lực đến', width: 95, type: 'date', value: (r) => r.validTo, auditField: false },
  { key: 'scope', title: 'Áp dụng tại', width: 180, value: scopeText, auditField: false },
  { key: 'lineCount', title: 'Số dòng', width: 70, type: 'quantity', value: (r) => r.lineCount, auditField: false },
  { key: 'deliveryTerms', title: 'Điều kiện giao hàng', width: 180, value: (r) => r.deliveryTerms, auditField: false },
  { key: 'approvedDate', title: 'Ngày duyệt', width: 90, type: 'date', value: (r) => r.approvedDate, auditField: false },
]

const lineColumns: GridColumn<PortalPriceListLine>[] = [
  { key: 'lineNo', title: 'STT', width: 45, type: 'quantity', value: (r) => r.lineNo, auditField: false },
  { key: 'itemCode', title: 'Mã hàng', width: 120, value: (r) => r.itemCode, auditField: false },
  { key: 'supplierItemCode', title: 'Mã hàng NCC', width: 95, value: (r) => r.supplierItemCode, auditField: false },
  { key: 'itemName', title: 'Tên hàng', width: 165, value: (r) => r.itemName, auditField: false },
  { key: 'uomCode', title: 'ĐVT', width: 50, value: (r) => r.uomCode, auditField: false },
  { key: 'minQty', title: 'SL từ', width: 70, type: 'quantity', value: (r) => r.minQty, auditField: false },
  { key: 'unitPrice', title: 'Đơn giá', width: 100, type: 'money', value: (r) => r.unitPrice, auditField: false },
  { key: 'currency', title: 'Tiền tệ', width: 62, value: () => 'VND', auditField: false },
  { key: 'discountPct', title: '% CK', width: 55, type: 'quantity', value: (r) => r.discountPct, auditField: false },
  { key: 'netPrice', title: 'Giá sau CK', width: 100, type: 'money', value: (r) => r.netPrice, auditField: false },
  { key: 'vatCode', title: 'Thuế suất', width: 75, value: (r) => (r.vatCode ? `${r.vatCode}${/^\d+$/.test(r.vatCode) ? '%' : ''}` : ''), auditField: false },
]

/**
 * Danh sách bảng giá của NCC trên Portal (lát P7a – design.md 19.5, P7-1…P7-3): chỉ bảng bên mua đã duyệt (đang hiệu lực hoặc đã ngừng),
 * đang áp dụng lên đầu. Bấm đúp một dòng mở chi tiết giá.
 */
export function PortalPriceListListPage() {
  const navigate = useNavigate()
  const lists = useQuery({ queryKey: ['portal', 'price-lists'], queryFn: portalApi.priceLists })
  const [groupBy, setGroupBy] = useState<string | null>(null)
  return (
    <>
      <h1 className="erp-portal-title">Bảng giá</h1>
      {lists.error && <Alert type="error" showIcon title={lists.error.message} />}
      <Section title="Bảng giá bên mua đã duyệt">
        <DataGrid
          rows={lists.data ?? []}
          columns={listColumns}
          rowKey={(r) => r.id}
          groupBy={groupBy}
          onGroupByChange={setGroupBy}
          loading={lists.isFetching}
          onOpenRow={(r) => navigate(`/ncc/bang-gia/${r.id}`)}
        />
        <p className="erp-portal-hint">
          Chỉ hiện bảng giá bên mua đã duyệt; bảng đang soạn hoặc chờ duyệt chưa hiện. Muốn đổi giá, liên hệ Phòng cung ứng của bên mua. Bấm
          đúp một dòng để xem chi tiết giá.
        </p>
      </Section>
    </>
  )
}

/** Chi tiết một bảng giá (P7-4): đầu bảng giá và các dòng giá (VND, chưa VAT). */
export function PortalPriceListPage() {
  const { id = '' } = useParams()
  const detail = useQuery({ queryKey: ['portal', 'price-list', id], queryFn: () => portalApi.priceList(id) })
  const [groupBy, setGroupBy] = useState<string | null>(null)
  const data = detail.data
  return (
    <>
      <p className="erp-portal-hint">
        <Link to="/ncc/bang-gia">← Bảng giá</Link>
      </p>
      {!data ? (
        <Spin spinning={detail.isFetching}>
          {detail.error ? <Alert type="error" showIcon title={detail.error.message} /> : <div style={{ minHeight: 120 }} />}
        </Spin>
      ) : (
        <>
          <h1 className="erp-portal-title">Bảng giá {data.header.no}</h1>
          <Section title={`Thông tin bảng giá – ${portalPriceListStatusLabels[data.header.status]}`}>
            <dl className="erp-portal-fields erp-portal-pay-summary" data-testid="portal-price-list-header">
              <dt>Tên bảng giá</dt>
              <dd>{data.header.title}</dd>
              <dt>Tình trạng</dt>
              <dd>{portalPriceListStatusLabels[data.header.status]}</dd>
              <dt>Áp dụng tại</dt>
              <dd>{scopeText(data.header)}</dd>
              <dt>Hiệu lực</dt>
              <dd>{validityText(data.header)}</dd>
              <dt>Điều kiện giao hàng</dt>
              <dd>{data.header.deliveryTerms ?? '–'}</dd>
              <dt>Ngày duyệt</dt>
              <dd>{data.header.approvedDate ? formatDate(data.header.approvedDate) : '–'}</dd>
            </dl>
          </Section>
          <Section title={`Dòng giá (${data.lines.length})`}>
            <div data-testid="portal-price-list-lines">
              <DataGrid rows={data.lines} columns={lineColumns} rowKey={(r) => String(r.lineNo)} groupBy={groupBy} onGroupByChange={setGroupBy} />
            </div>
            <p className="erp-portal-hint">
              Đơn giá chưa gồm VAT. "SL từ": giá áp dụng khi số lượng đặt (theo ĐVT của dòng) từ mức này trở lên; 0 là mọi số lượng. Thuế suất
              trống thì theo quy định của mặt hàng.
            </p>
          </Section>
        </>
      )}
    </>
  )
}
