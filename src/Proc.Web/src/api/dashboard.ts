import { useQuery } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Số liệu trang chủ và số đếm cây điều hướng (khớp Proc.Application.Reports – Dashboard.cs, design.md mục 11.4).
 */

/** Số đếm một mục cây điều hướng. */
export interface NavCount {
  count: number
  attention: boolean
}

/** Số liệu mua hàng (null = không có quyền xem). */
export interface DashboardPurchase {
  pendingApprovals: number
  pendingRequests: number | null
  overdueRequests: number | null
  openOrders: number | null
  lateOrders: number | null
  openRfqs: number | null
}

/** Số liệu kho (null = không có quyền xem). */
export interface DashboardStock {
  draftDocuments: Record<string, number>
  belowMinItems: number | null
  expiredLots: number | null
  expiringLots: number | null
  goodsInTransit: number | null
  periodLabel: string | null
  periodStatus: 'NotOpened' | 'Open' | 'Costing' | 'Closed' | null
}

/** Mua hàng thuần tháng này, tháng trước, top NCC. */
export interface DashboardSpend {
  thisMonth: number
  lastMonth: number
  topSuppliers: { code: string; name: string; netAmount: number }[]
}

/** Số liệu trang chủ. */
export interface Dashboard {
  today: string
  purchase: DashboardPurchase
  stock: DashboardStock | null
  spend: DashboardSpend | null
  navCounts: Record<string, NavCount>
}

/** Số liệu trang chủ của công ty đang làm việc; tự tải lại mỗi phút để số đếm cây điều hướng không cũ. */
export function useDashboard() {
  const { workingCompany } = useAuth()
  const companyId = workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['dashboard', companyId],
    queryFn: () => apiFetch<Dashboard>('/api/dashboard'),
    enabled: !!companyId,
    refetchInterval: 60_000,
  })
}
