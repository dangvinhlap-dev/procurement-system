import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { PurchaseStatus } from './purchasing'

/** Trạng thái phiếu đặt hàng gộp (design.md mục 18.25 – lát G7). */
export type PooledOrderStatus = 'Draft' | 'Ordered' | 'Cancelled'

/** Nhãn tiếng Việt của trạng thái. */
export const pooledOrderStatusLabels: Record<PooledOrderStatus, string> = {
  Draft: 'Nháp',
  Ordered: 'Đã tạo PO',
  Cancelled: 'Đã hủy',
}

/** Màu đèn theo trạng thái (luôn kèm chữ – docs/ui-style.md mục 2). */
export const pooledOrderStatusTone: Record<PooledOrderStatus, 'success' | 'warning' | 'error' | 'neutral'> = {
  Draft: 'neutral',
  Ordered: 'success',
  Cancelled: 'error',
}

/** Một dòng danh sách phiếu. */
export interface PooledOrderListItem {
  id: string
  no: string
  orderDate: string
  monthlyApprovalNo: string
  supplierCode: string
  supplierName: string
  lineCount: number
  totalAmount: number
  orderCount: number
  status: PooledOrderStatus
  createdByName: string | null
}

/** Công ty nhận hàng kèm PO đã sinh. */
export interface PooledOrderCompany {
  companyId: string
  companyCode: string
  companyName: string
  warehouseId: string
  warehouseCode: string | null
  departmentId: string | null
  departmentName: string | null
  amount: number
  purchaseOrderId: string | null
  purchaseOrderNo: string | null
  purchaseOrderStatus: PurchaseStatus | null
}

/** SL phân bổ một công ty (kế hoạch, đề xuất, vượt biên độ – máy chủ tính). */
export interface PooledAllocation {
  companyId: string
  qty: number
  planQty: number
  expectedQty: number | null
  isOutOfMargin: boolean
}

/** Dòng phiếu đặt gộp. */
export interface PooledOrderLine {
  id: string
  lineNo: number
  monthlyApprovalLineId: string
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  unitPrice: number
  totalQty: number
  amount: number
  tolerance: number
  hasPlan: boolean
  allocationReason: string | null
  note: string | null
  allocations: PooledAllocation[]
}

/** Thao tác được phép (máy chủ tính). */
export interface PooledOrderActions {
  canEdit: boolean
  canCreateOrders: boolean
  canCancel: boolean
  readOnlyReason: string | null
  /** Lý do không tạo PO được dù sửa được (thiếu quyền tạo PO ở tổng công ty / quyền đặc biệt tạo PO cho công ty thành viên). */
  createOrdersBlockedReason: string | null
}

/** Chi tiết phiếu. */
export interface PooledOrder {
  id: string
  no: string
  orderDate: string
  monthlyApprovalId: string
  monthlyApprovalNo: string
  period: string
  categoryCode: string
  categoryName: string
  supplierId: string
  supplierCode: string
  supplierName: string
  expectedDate: string | null
  note: string | null
  status: PooledOrderStatus
  marginPercent: number
  orderedAt: string | null
  orderedByName: string | null
  cancelledOn: string | null
  cancelReason: string | null
  createdAt: string
  createdByName: string | null
  totalAmount: number
  companies: PooledOrderCompany[]
  lines: PooledOrderLine[]
  actions: PooledOrderActions
}

/** Phiếu GTH đã duyệt dùng để đặt gộp. */
export interface PooledApprovalOption {
  id: string
  no: string
  period: string
  categoryCode: string
  categoryName: string
  suppliers: { supplierId: string; code: string; name: string }[]
  companies: { companyId: string; code: string; name: string }[]
}

/** Kho/bộ phận của công ty nhận hàng. */
export interface PooledPlaceOption {
  id: string
  code: string
  name: string
  isMain: boolean
}

/** Ngữ cảnh lập phiếu theo phiếu GTH + NCC. */
export interface PooledOrderContext {
  marginPercent: number
  companies: { companyId: string; code: string; name: string; warehouses: PooledPlaceOption[]; departments: PooledPlaceOption[] }[]
  lines: {
    monthlyApprovalLineId: string
    itemId: string
    itemCode: string
    itemName: string
    uomId: string
    uomCode: string
    unitPrice: number
    isBackup: boolean
    quantityLimit: number | null
    plans: { companyId: string; planQty: number }[]
  }[]
}

/** Dữ liệu lưu phiếu. */
export interface SavePooledOrderInput {
  orderDate: string
  monthlyApprovalId: string
  supplierId: string
  expectedDate: string | null
  note: string | null
  companies: { companyId: string; warehouseId: string; departmentId: string | null }[]
  lines: {
    id: string | null
    monthlyApprovalLineId: string
    allocations: { companyId: string; qty: number }[]
    allocationReason: string | null
    note: string | null
  }[]
}

const BASE = '/api/pooled-orders'

/** Id công ty đang làm việc – đưa vào khóa truy vấn để đổi công ty thì tải lại dữ liệu. */
function useCompanyId(): string {
  return useAuth().workingCompany?.id ?? ''
}

/** Danh sách phiếu đặt hàng gộp của tổng công ty. */
export function usePooledOrders() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['pooled-orders', companyId], queryFn: () => apiFetch<PooledOrderListItem[]>(BASE) })
}

/**
 * Chi tiết một phiếu.
 * @param id Id phiếu; null = không tải (đang lập mới).
 */
export function usePooledOrder(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['pooled-orders', companyId, id],
    queryFn: () => apiFetch<PooledOrder>(`${BASE}/${id}`),
    enabled: id !== null,
  })
}

/**
 * Phiếu GTH đã duyệt của tháng chứa ngày đặt.
 * @param date Ngày đặt yyyy-MM-dd; null = không tải.
 */
export function usePooledApprovals(date: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['pooled-orders', companyId, 'approvals', date],
    queryFn: () => apiFetch<PooledApprovalOption[]>(`${BASE}/approvals?date=${date}`),
    enabled: !!date,
  })
}

/**
 * Dòng GTH đặt được với NCC, kế hoạch tháng, kho/bộ phận các công ty, % biên độ.
 * @param approvalId Phiếu GTH.
 * @param supplierId NCC.
 */
export function usePooledContext(approvalId: string | undefined, supplierId: string | undefined) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['pooled-orders', companyId, 'context', approvalId, supplierId],
    queryFn: () => apiFetch<PooledOrderContext>(`${BASE}/context?approvalId=${approvalId}&supplierId=${supplierId}`),
    enabled: !!approvalId && !!supplierId,
  })
}

/** Lệnh trên phiếu: lập, sửa, hủy, tạo PO. */
export function usePooledOrderMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['pooled-orders'] })
    // PO mới sinh ở các công ty – danh sách PO phải tải lại khi chuyển công ty.
    void queryClient.invalidateQueries({ queryKey: ['purchase-orders'] })
  }
  return {
    create: useMutation({ mutationFn: (input: SavePooledOrderInput) => apiFetch<PooledOrder>(BASE, { method: 'POST', body: input }), onSuccess }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SavePooledOrderInput }) => apiFetch<PooledOrder>(`${BASE}/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    cancel: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) => apiFetch<PooledOrder>(`${BASE}/${id}/cancel`, { method: 'POST', body: { reason } }),
      onSuccess,
    }),
    createOrders: useMutation({ mutationFn: (id: string) => apiFetch<PooledOrder>(`${BASE}/${id}/create-orders`, { method: 'POST' }), onSuccess }),
  }
}

/** Kế hoạch tháng của một công ty (ĐVT dòng). */
export interface CompanyPlan {
  companyId: string
  plan: number
}

/**
 * SL đề xuất chính xác của một công ty = tổng × KH công ty ÷ tổng KH (giống máy chủ – design.md mục 18.25, G7).
 * @param total Tổng SL dòng.
 * @param companyId Công ty.
 * @param plans Kế hoạch các công ty.
 * @returns SL đề xuất; 0 khi không có kế hoạch.
 */
export function expectedQty(total: number, companyId: string, plans: CompanyPlan[]): number {
  const sum = plans.reduce((s, p) => s + Math.max(p.plan, 0), 0)
  if (sum <= 0) return 0
  return (total * Math.max(plans.find((p) => p.companyId === companyId)?.plan ?? 0, 0)) / sum
}

/**
 * Dung sai mỗi công ty = max(tổng × biên độ % ÷ 100, 1 ĐVT), 4 số lẻ. Ví dụ tổng 100, biên độ 10% → 10; tổng 5 → 1.
 * @param total Tổng SL dòng.
 * @param marginPercent % biên độ.
 */
export function tolerance(total: number, marginPercent: number): number {
  return Math.max(Math.round(((total * marginPercent) / 100) * 10000) / 10000, 1)
}

/**
 * Chia tổng theo tỷ lệ kế hoạch, cộng đúng bằng tổng (giống máy chủ): tổng số nguyên thì chia số nguyên, phần dư cho công ty có phần lẻ lớn
 * nhất (bằng nhau thì kế hoạch lớn hơn); có số lẻ thì chia 4 số lẻ, phần dư cho kế hoạch lớn nhất. Ví dụ tổng 10, KH A 2, B 1 → A 7, B 3.
 * @param total Tổng SL đặt.
 * @param plans Kế hoạch các công ty.
 * @returns SL theo công ty; rỗng khi không có kế hoạch.
 */
export function suggestAllocation(total: number, plans: CompanyPlan[]): Record<string, number> {
  const positive = plans.filter((p) => p.plan > 0)
  const sum = positive.reduce((s, p) => s + p.plan, 0)
  if (sum <= 0 || total <= 0) return {}
  const integer = Number.isInteger(total)
  const scale = integer ? 1 : 10000
  // Tính theo đơn vị nhỏ nhất (số nguyên) để không sai số dấu phẩy động.
  const units = Math.round(total * scale)
  const exact = positive.map((p) => ({ id: p.companyId, plan: p.plan, value: (units * p.plan) / sum }))
  const result: Record<string, number> = Object.fromEntries(exact.map((e) => [e.id, Math.floor(e.value)]))
  let remainder = units - Object.values(result).reduce((s, v) => s + v, 0)
  if (integer) {
    for (const e of [...exact].sort((a, b) => b.value - Math.floor(b.value) - (a.value - Math.floor(a.value)) || b.plan - a.plan)) {
      if (remainder <= 0) break
      result[e.id] += 1
      remainder -= 1
    }
  } else if (remainder !== 0) {
    const largest = exact.reduce((a, b) => (b.plan > a.plan ? b : a))
    result[largest.id] += remainder
  }
  return Object.fromEntries(Object.entries(result).map(([k, v]) => [k, v / scale]))
}
