import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import type { AgentCommand } from './agent'
import { apiFetch } from './client'
import type { DateRange } from './logs'
import type { OverviewStatus } from './operations'

/*
 * Làm mới TEST từ PROD (màn hình TST) và báo lỗi / góp ý từ máy TEST – lát QT9 (design.md 21.5, 21.18). Khớp Proc.Application.Operations.TestRefresh.
 */

/** Chữ phải gõ khi làm mới TEST (Q6 – trùng ITestRefreshService.Confirmation máy chủ). */
export const TEST_REFRESH_CONFIRMATION = 'LÀM MỚI TEST'

/** Môi trường TEST nhìn từ máy chạy TST. */
export interface TestRefreshTarget {
  code: string
  name: string
  contactLight: OverviewStatus
  contactLabel: string
  appVersion: string | null
  agentVersion: string | null
  lastSeenAt: string | null
  hasServiceKey: boolean
}

/** Bản pg_dump sẽ dùng. */
export interface TestRefreshDump {
  runId: string
  finishedAt: string | null
  relativePath: string | null
  sizeBytes: number | null
  sha256: string | null
  ageHours: number
  stale: boolean
}

/** Tổng quan TST. */
export interface TestRefreshOverview {
  machineEnvironment: string
  version: string
  /** Lý do chỉ xem (máy TEST); null = làm mới được nếu không vướng. */
  readOnlyReason: string | null
  test: TestRefreshTarget | null
  dump: TestRefreshDump | null
  blockers: string[]
  warnings: string[]
  keepTables: string[]
  openCommandId: string | null
}

/** Kết quả báo lỗi (PROD trả về). */
export interface ForwardDefectResult {
  version: string
  ticketStatus: string
  openDefects: number
  message: string
}

/** Tổng quan TST; còn lệnh làm mới chờ / chạy thì lấy lại mỗi 5 giây. */
export function useTestRefreshOverview() {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'test-refresh', 'overview'],
    queryFn: () => apiFetch<TestRefreshOverview>('/api/ops/v1/test-refresh/overview'),
    retry: false,
    refetchInterval: (q) => (q.state.data?.openCommandId ? 5000 : 30_000),
  })
}

/**
 * Lịch sử làm mới (lệnh RefreshTest) theo khoảng ngày; còn lệnh chờ / chạy thì lấy lại mỗi 5 giây.
 * @param range Từ ngày, đến ngày (yyyy-MM-dd).
 */
export function useTestRefreshRuns(range: DateRange) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'test-refresh', 'runs', range.from, range.to],
    queryFn: () => apiFetch<AgentCommand[]>(`/api/ops/v1/test-refresh/runs?from=${range.from}&to=${range.to}`),
    placeholderData: keepPreviousData,
    retry: false,
    refetchInterval: (q) => (q.state.data?.some((c) => c.status === 'Queued' || c.status === 'Running') ? 5000 : false),
  })
}

/** Giao bộ thực thi TEST làm mới – xác nhận Q6 (lý do + mật khẩu + gõ "LÀM MỚI TEST"). */
export function useRunTestRefresh() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (v: { reason: string; password: string; confirmation: string }) =>
      apiFetch<AgentCommand>('/api/ops/v1/test-refresh/runs', { method: 'POST', body: v }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['ops'] }),
  })
}

/** Gửi báo lỗi / góp ý từ máy TEST (máy chủ TEST chuyển về PROD). */
export function useSendFeedback() {
  return useMutation({
    mutationFn: (v: { title: string; description: string | null; screen: string | null; imageFileName: string | null; imageBase64: string | null }) =>
      apiFetch<ForwardDefectResult>('/api/ops/v1/feedback', { method: 'POST', body: v }),
  })
}
