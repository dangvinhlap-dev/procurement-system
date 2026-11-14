import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { DateRange } from './logs'

/*
 * Kiểu dữ liệu và hàm gọi API module Quản trị vận hành (khớp Proc.Application.Operations – design.md mục 21, lát QT1).
 */

/** Môi trường máy chủ: Dev, Test, Prod; None = máy chưa khai App:Environment (không gửi email, dải đỏ). */
export type AppEnvironment = 'Dev' | 'Test' | 'Prod' | 'None'

/** Môi trường và cảnh báo cấu hình (API ẩn danh). */
export interface EnvironmentStatus {
  environment: AppEnvironment
  /** Cảnh báo cấu hình (chưa khai môi trường, TEST chưa khai hộp thư test…); null = ổn. */
  warning: string | null
}

/**
 * Môi trường của máy chủ – gọi một lần mỗi phiên trình duyệt (cấu hình chỉ đổi khi khởi động lại máy chủ). Lỗi mạng thì coi như không biết
 * (không vẽ dải) – trang vẫn dùng được.
 * @param endpoint API: nội bộ `/api/ops/v1/environment`, Portal NCC `/api/portal/auth/environment` (site Portal chỉ mở /api/portal).
 */
export function useEnvironmentStatus(endpoint = '/api/ops/v1/environment') {
  return useQuery({
    queryKey: ['environment-status', endpoint],
    queryFn: () => apiFetch<EnvironmentStatus>(endpoint),
    staleTime: Infinity,
    retry: false,
  })
}

/** Đèn của một dòng tổng quan. */
export type OverviewStatus = 'Ok' | 'Warning' | 'Error' | 'Neutral'

/** Một dòng bảng tổng quan SYS. */
export interface SystemOverviewRow {
  group: string
  item: string
  value: string
  status: OverviewStatus
  note: string | null
}

/** Tổng quan hệ thống của môi trường đang chạy. */
export interface SystemOverview {
  environment: AppEnvironment
  version: string
  checkedAt: string
  appliedMigrations: number
  pendingMigrations: string[]
  rows: SystemOverviewRow[]
}

/** Tổng quan SYS – chỉ ở tổng công ty, quyền System.Ops.View; bấm "Làm mới" để lấy lại số liệu. */
export function useSystemOverview() {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'overview'],
    queryFn: () => apiFetch<SystemOverview>('/api/ops/v1/overview'),
    retry: false,
  })
}

/** Kết quả thao tác vận hành. */
export type OperationResult = 'Succeeded' | 'Failed' | 'Info'

/** Một dòng nhật ký vận hành. */
export interface OperationLogItem {
  id: string
  occurredAt: string
  environment: AppEnvironment
  userName: string | null
  kind: string
  target: string | null
  reason: string | null
  result: OperationResult
  detail: string | null
}

/** Trang nhật ký vận hành. */
export interface OperationLogPage {
  items: OperationLogItem[]
  truncated: boolean
}

/**
 * Nhật ký vận hành theo khoảng ngày.
 * @param range Từ ngày, đến ngày (yyyy-MM-dd).
 */
export function useOperationLogs(range: DateRange) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'operation-logs', range.from, range.to],
    queryFn: () => apiFetch<OperationLogPage>(`/api/ops/v1/operation-logs?from=${range.from}&to=${range.to}`),
    placeholderData: keepPreviousData,
    retry: false,
  })
}
