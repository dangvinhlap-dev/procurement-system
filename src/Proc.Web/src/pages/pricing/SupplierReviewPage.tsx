import { Alert, InputNumber } from 'antd'
import { Select } from '../../classic/Select'
import dayjs from 'dayjs'
import { useState } from 'react'
import { useSearchParams } from 'react-router'
import {
  reviewResultLabels,
  reviewStatusLabels,
  useSupplierReviewBoard,
  type SupplierReviewBoardRow,
} from '../../api/supplierReviews'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatDate, formatQuantity } from '../../utils/format'
import { SupplierReviewDrawer, type NewSupplierReview } from './SupplierReviewDrawer'

/** Quý trước so với hôm nay – kỳ cần đánh giá mặc định (quý hiện tại chưa kết thúc). */
function previousQuarter(): { year: number; quarter: number } {
  const start = dayjs().startOf('month').subtract(3, 'month')
  return { year: start.year(), quarter: Math.floor(start.month() / 3) + 1 }
}

const columns: GridColumn<SupplierReviewBoardRow>[] = [
  { key: 'code', title: 'Mã NCC', width: 120, value: (r) => r.supplierCode },
  { key: 'name', title: 'Nhà cung cấp', width: 240, value: (r) => r.supplierName },
  { key: 'due', title: 'Thuộc diện', width: 90, value: (r) => (r.isDue ? 'Có' : 'Không'), groupable: true },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 130,
    type: 'status',
    value: (r) => (r.status ? reviewStatusLabels[r.status] : 'Chưa đánh giá'),
    // Chưa đánh giá NCC thuộc diện là việc còn nợ KPI – đèn đỏ; nháp – vàng; hoàn thành – xanh.
    tone: (r) => (r.status === 'Completed' ? 'success' : r.status === 'Draft' ? 'warning' : r.isDue ? 'error' : 'neutral'),
    groupable: true,
  },
  { key: 'no', title: 'Số phiếu', width: 150, value: (r) => r.reviewNo },
  { key: 'date', title: 'Ngày đánh giá', width: 100, type: 'date', value: (r) => r.date },
  { key: 'score', title: 'Điểm', width: 70, type: 'quantity', value: (r) => r.score },
  { key: 'result', title: 'Kết luận', width: 190, value: (r) => (r.result ? reviewResultLabels[r.result] : null), groupable: true },
  { key: 'receipt', title: 'Giá trị nhập', width: 130, type: 'money', value: (r) => r.receiptAmount },
  { key: 'onTime', title: 'Đúng hạn (%)', width: 100, type: 'quantity', value: (r) => r.onTimePct },
  { key: 'return', title: 'Trả hàng (%)', width: 100, type: 'quantity', value: (r) => r.returnPct },
  { key: 'quality', title: 'Đạt CL (%)', width: 90, type: 'quantity', value: (r) => r.qualityPct },
  { key: 'rating', title: 'Xếp loại HT', width: 90, value: (r) => r.systemRating },
  { key: 'reviewer', title: 'Người đánh giá', width: 150, value: (r) => r.reviewerName, auditField: false },
]

/**
 * Nhà cung cấp → Đánh giá NCC định kỳ (SUPREV, design.md mục 18.20): bảng NCC của một quý – NCC thuộc diện (có phiếu nhập mua đã ghi sổ
 * trong quý) và NCC đã có phiếu; chưa hoàn thành lên đầu. Bấm đúp mở phiếu, chưa có thì lập mới với số liệu hệ thống điền sẵn.
 * Mở thẳng phiếu bằng `?mo=<id>`.
 */
export function SupplierReviewPage() {
  const { hasPermission } = useAuth()
  const [period, setPeriod] = useState(previousQuarter)
  const board = useSupplierReviewBoard(period.year, period.quarter)
  const [params, setParams] = useSearchParams()
  const [fresh, setFresh] = useState<NewSupplierReview | null>(null)
  const openId = params.get('mo')
  const canManage = hasPermission(Permission.SupplierReviewManage)

  const open = (id: string | null) => {
    setFresh(null)
    setParams(id ? { mo: id } : {}, { replace: true })
  }
  const openRow = (r: SupplierReviewBoardRow) => {
    if (r.reviewId) open(r.reviewId)
    else if (canManage)
      setFresh({
        year: period.year,
        quarter: period.quarter,
        supplierId: r.supplierId,
        supplierCode: r.supplierCode,
        supplierName: r.supplierName,
        receiptAmount: r.receiptAmount,
        onTimePct: r.onTimePct,
        returnPct: r.returnPct,
        qualityPct: r.qualityPct,
        systemRating: r.systemRating,
      })
  }

  const data = board.data
  return (
    <>
      <ListPage
        title="Đánh giá NCC định kỳ"
        exportFileName={`danh-gia-ncc-quy-${period.quarter}-${period.year}`}
        exportSubtitle={data ? `Quý ${data.quarter}/${data.year} (${formatDate(data.from)} – ${formatDate(data.to)})` : undefined}
        rows={data?.rows ?? []}
        columns={columns}
        rowKey={(r) => r.supplierId}
        auditEntity="SupplierReview"
        loading={board.isLoading}
        canCreate={canManage}
        onCreate={() => setFresh({ year: period.year, quarter: period.quarter })}
        onOpenRow={openRow}
        isMarked={(r) => r.isDue && r.status !== 'Completed'}
        notice={
          board.error ? (
            <Alert type="error" showIcon title={board.error.message} />
          ) : data ? (
            <Alert
              type={data.dueCount > 0 && data.completedCount < data.dueCount ? 'warning' : 'info'}
              showIcon
              title={
                data.dueCount === 0
                  ? `Quý ${data.quarter}/${data.year} chưa có NCC thuộc diện phải đánh giá (chưa có phiếu nhập mua đã ghi sổ trong quý).`
                  : `Đã hoàn thành ${data.completedCount}/${data.dueCount} NCC thuộc diện (${formatQuantity(data.completedPct)}%). NCC thuộc diện: có phiếu nhập mua đã ghi sổ trong quý. Bấm đúp một dòng để đánh giá.`
              }
            />
          ) : undefined
        }
        onClearFilters={() => setPeriod(previousQuarter())}
        filters={
          <>
            <FilterField label="Năm" width={90}>
              {(id) => (
                <InputNumber
                  id={id}
                  min={2000}
                  max={2100}
                  value={period.year}
                  onChange={(v) => v && setPeriod((p) => ({ ...p, year: v }))}
                  style={{ width: '100%' }}
                />
              )}
            </FilterField>
            <FilterField label="Quý" width={90}>
              {(id) => (
                <Select
                  id={id}
                  value={period.quarter}
                  onChange={(quarter: number) => setPeriod((p) => ({ ...p, quarter }))}
                  options={[1, 2, 3, 4].map((q) => ({ value: q, label: `Quý ${q}` }))}
                />
              )}
            </FilterField>
          </>
        }
      />
      {(openId || fresh) && (
        <SupplierReviewDrawer
          id={fresh ? null : openId}
          fresh={fresh ?? undefined}
          onClose={() => open(null)}
          onOpen={open}
        />
      )}
    </>
  )
}
