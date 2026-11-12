import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/**
 * Trạng thái bên bán của đơn mua nội bộ (lát X7, design.md mục 19.3): NotSent – PO chưa duyệt; Pending – chờ công ty bán xác nhận;
 * Confirmed; Rejected; Cancelled – PO bị hủy trước khi công ty bán quyết định.
 */
export type InternalSaleStatus = 'NotSent' | 'Pending' | 'Confirmed' | 'Rejected' | 'Cancelled'

/** Nhãn trạng thái bên bán. */
export const internalSaleStatusLabels: Record<InternalSaleStatus, string> = {
  NotSent: 'Chưa gửi (PO chưa duyệt)',
  Pending: 'Chờ xác nhận',
  Confirmed: 'Đã xác nhận',
  Rejected: 'Từ chối',
  Cancelled: 'PO đã hủy',
}

/** Màu đèn trạng thái bên bán. */
export const internalSaleStatusTones: Record<InternalSaleStatus, 'success' | 'warning' | 'error' | 'neutral'> = {
  NotSent: 'neutral',
  Pending: 'warning',
  Confirmed: 'success',
  Rejected: 'error',
  Cancelled: 'neutral',
}

/** Trạng thái bên bán hiện trên PO mua nội bộ của công ty mua. */
export interface InternalSaleStatusInfo {
  sellerCompanyCode: string
  sellerCompanyName: string
  status: InternalSaleStatus
  note: string | null
  decidedByName: string | null
  decidedAt: string | null
}

/** Một dòng danh sách "Đơn bán nội bộ". */
export interface InternalSaleListItem {
  orderId: string
  orderNo: string
  orderDate: string
  buyerCompanyCode: string
  buyerCompanyName: string
  warehouseName: string | null
  expectedDate: string | null
  amount: number
  lineCount: number
  orderStatus: string
  status: InternalSaleStatus
  decidedByName: string | null
  decidedAt: string | null
}

/** Kết quả danh sách. */
export interface InternalSaleListResult {
  items: InternalSaleListItem[]
  truncated: boolean
  limit: number
}

/** Dòng hàng của đơn bán nội bộ (chỉ xem). */
export interface InternalSaleLine {
  lineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uom: string
  qty: number
  unitPrice: number
  discountPct: number
  amount: number
  vatCode: string | null
  vatAmount: number
  deliveryDate: string | null
  receivedQty: number
  note: string | null
}

/** Chi tiết đơn bán nội bộ. */
export interface InternalSale {
  orderId: string
  orderNo: string
  orderDate: string
  orderStatus: string
  buyerCompanyCode: string
  buyerCompanyName: string
  warehouseName: string | null
  deliveryAddress: string | null
  expectedDate: string | null
  paymentTerms: string | null
  deliveryTerms: string | null
  orderNote: string | null
  orderedByName: string | null
  approvedAt: string | null
  totalAmount: number
  totalVat: number
  lines: InternalSaleLine[]
  status: InternalSaleStatus
  note: string | null
  decidedByName: string | null
  decidedAt: string | null
  canDecide: boolean
  readOnlyReason: string | null
}

/** Bộ lọc danh sách: khoảng ngày PO (yyyy-MM-dd) và trạng thái bên bán (trống = tất cả). */
export interface InternalSaleFilter {
  from: string
  to: string
  status: string
}

/**
 * Danh sách đơn bán nội bộ của công ty đang làm việc (`GET /api/internal-sales`).
 * @param filter Bộ lọc.
 */
export function useInternalSales(filter: InternalSaleFilter) {
  const companyId = useCompanyId()
  const params = new URLSearchParams({ from: filter.from, to: filter.to })
  if (filter.status) params.set('status', filter.status)
  const query = params.toString()
  return useQuery({
    queryKey: ['internal-sales', companyId, query],
    queryFn: () => apiFetch<InternalSaleListResult>(`/api/internal-sales?${query}`),
    enabled: !!companyId,
    placeholderData: keepPreviousData,
  })
}

/**
 * Chi tiết một đơn bán nội bộ (`GET /api/internal-sales/{orderId}`).
 * @param orderId Id PO của công ty mua; rỗng thì không tải.
 */
export function useInternalSale(orderId: string) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['internal-sales', companyId, 'detail', orderId],
    queryFn: () => apiFetch<InternalSale>(`/api/internal-sales/${orderId}`),
    enabled: !!companyId && !!orderId,
  })
}

/** Xác nhận (ghi chú tùy chọn) / từ chối (bắt buộc lý do) đơn bán nội bộ. */
export function useInternalSaleMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['internal-sales'] })
  return {
    confirm: useMutation({
      mutationFn: ({ orderId, note }: { orderId: string; note: string | null }) =>
        apiFetch<InternalSale>(`/api/internal-sales/${orderId}/confirm`, { method: 'POST', body: { note } }),
      onSuccess,
    }),
    reject: useMutation({
      mutationFn: ({ orderId, note }: { orderId: string; note: string }) =>
        apiFetch<InternalSale>(`/api/internal-sales/${orderId}/reject`, { method: 'POST', body: { note } }),
      onSuccess,
    }),
  }
}
