import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useEffect } from 'react'
import { useAuth } from '../auth/useAuth'
import { apiFetch, MAINTENANCE_EVENT } from './client'
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
  /** Chế độ bảo trì đang bật (lát QT5) – dải đỏ "ĐANG BẢO TRÌ"; null / thiếu = tắt. */
  maintenance?: { message: string; startedAt: string | null } | null
  /** Hiện nút "Báo lỗi / góp ý" (lát QT9): máy TEST đã khai Operations:Upstream. */
  feedbackEnabled?: boolean
}

/**
 * Môi trường của máy chủ. Môi trường chỉ đổi khi khởi động lại máy chủ, nhưng chế độ bảo trì (lát QT5) bật / tắt bất kỳ lúc nào nên lấy lại mỗi
 * phút và ngay khi API trả 503 bảo trì. Lỗi mạng thì giữ số liệu cũ / không vẽ dải – trang vẫn dùng được.
 * @param endpoint API: nội bộ `/api/ops/v1/environment`, Portal NCC `/api/portal/auth/environment` (site Portal chỉ mở /api/portal).
 */
export function useEnvironmentStatus(endpoint = '/api/ops/v1/environment') {
  const query = useQuery({
    queryKey: ['environment-status', endpoint],
    queryFn: () => apiFetch<EnvironmentStatus>(endpoint),
    staleTime: 30_000,
    refetchInterval: 60_000,
    retry: false,
  })
  const { refetch } = query
  useEffect(() => {
    const onMaintenance = () => void refetch()
    window.addEventListener(MAINTENANCE_EVENT, onMaintenance)
    return () => window.removeEventListener(MAINTENANCE_EVENT, onMaintenance)
  }, [refetch])
  return query
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

/** Trạng thái lưu WAL (khớp WalArchiveStates máy chủ – lát QT3a). */
export type WalArchiveState = 'NotEnabled' | 'PendingRestart' | 'Running' | 'Lagging' | 'Failing'

/** Tình trạng lưu WAL của máy chủ CSDL (màn hình BAK). */
export interface WalArchiveStatus {
  state: WalArchiveState
  stateLabel: string
  status: OverviewStatus
  /** Một câu tóm tắt: lần chép cuối, độ trễ, việc cần làm. */
  summary: string
  checkedAt: string
  lastArchivedAt: string | null
  /** Giây trễ khi còn file WAL đầy chờ chép; 0 = không chờ; null = không tính được. */
  lagSeconds: number | null
  /** Tham số PostgreSQL, thống kê chép, nơi lưu – cùng dạng dòng bảng SYS. */
  rows: SystemOverviewRow[]
}

/** Tình trạng lưu WAL – chỉ ở tổng công ty, quyền System.OpsBackup.View; bấm "Làm mới" để lấy lại. */
export function useWalArchiveStatus() {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'wal-status'],
    queryFn: () => apiFetch<WalArchiveStatus>('/api/ops/v1/backup/wal-status'),
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
