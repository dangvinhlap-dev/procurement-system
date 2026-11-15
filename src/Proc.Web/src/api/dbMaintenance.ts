import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { DateRange } from './logs'
import type { OverviewStatus } from './operations'

/*
 * Bảo trì CSDL – màn hình DBM (lát QT4, design.md mục 21.4). Khớp Proc.Application.Operations.DbMaintenance.
 */

/** Việc bảo trì (tên enum máy chủ trả về trong DTO). */
export type MaintenanceKind = 'Vacuum' | 'Reindex' | 'TerminateSession'

/** Số enum máy chủ khi đặt hàng chờ (API nhận enum dạng số). */
const maintenanceKindValue: Record<Exclude<MaintenanceKind, 'TerminateSession'>, number> = { Vacuum: 0, Reindex: 1 }

/** Chữ người dùng phải gõ để ngắt phiên (Q6 – trùng DangerConfirmations.TerminateSession máy chủ). */
export const TERMINATE_CONFIRMATION = 'NGẮT PHIÊN'

/** Một bảng (tab Bảng). */
export interface DbTableStat {
  schema: string
  name: string
  totalBytes: number
  tableBytes: number
  indexBytes: number
  liveRows: number
  deadRows: number
  deadPercent: number
  lastVacuum: string | null
  lastAutoVacuum: string | null
  lastAnalyze: string | null
  lastAutoAnalyze: string | null
  light: OverviewStatus
  note: string | null
}

/** Tab Bảng. */
export interface DbTableStats {
  databaseName: string
  databaseBytes: number
  checkedAt: string
  tables: DbTableStat[]
}

/** Một phiên kết nối (tab Phiên và khóa). */
export interface DbSession {
  pid: number
  userName: string | null
  applicationName: string | null
  clientAddress: string | null
  state: string | null
  stateLabel: string
  backendStart: string | null
  transactionStart: string | null
  queryStart: string | null
  runningSeconds: number | null
  waitEventType: string | null
  waitEvent: string | null
  blockedBy: number[]
  blockingCount: number
  query: string | null
  /** Chính kết nối máy chủ dùng để đọc số liệu – không ngắt được. */
  isSelf: boolean
  light: OverviewStatus
  note: string | null
}

/** Tab Phiên và khóa. */
export interface DbSessions {
  checkedAt: string
  usesMaintenanceAccount: boolean
  waitingCount: number
  sessions: DbSession[]
}

/** Một lần bảo trì (tab Lịch sử). */
export interface MaintenanceRun {
  id: string
  kind: MaintenanceKind
  kindLabel: string
  trigger: 'Manual' | 'Schedule'
  status: 'Queued' | 'Running' | 'Succeeded' | 'Failed'
  statusLabel: string
  light: OverviewStatus
  target: string | null
  sessionPid: number | null
  reason: string | null
  scheduledDate: string | null
  requestedByName: string | null
  queuedAt: string
  startedAt: string | null
  finishedAt: string | null
  durationSeconds: number | null
  sizeBeforeBytes: number | null
  sizeAfterBytes: number | null
  note: string | null
  errorMessage: string | null
}

/** Kết quả tìm lịch sử bảo trì kèm lịch hằng tuần. */
export interface MaintenanceRunPage {
  items: MaintenanceRun[]
  truncated: boolean
  scheduleEnabled: boolean
  /** Ví dụ "Chủ nhật 03:00". */
  scheduleLabel: string
  /** Giờ Việt Nam, không múi giờ (yyyy-MM-ddTHH:mm:ss); null khi lịch tắt. */
  nextScheduledAt: string | null
}

/** Dung lượng, dòng chết từng bảng. */
export function useDbTables() {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'dbm-tables'],
    queryFn: () => apiFetch<DbTableStats>('/api/ops/v1/db-maintenance/tables'),
    retry: false,
  })
}

/**
 * Phiên kết nối CSDL ứng dụng.
 * @param enabled Chỉ lấy khi đang xem tab Phiên và khóa (đọc pg_stat_activity mỗi lần).
 */
export function useDbSessions(enabled: boolean) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'dbm-sessions'],
    queryFn: () => apiFetch<DbSessions>('/api/ops/v1/db-maintenance/sessions'),
    enabled,
    retry: false,
  })
}

/**
 * Lịch sử bảo trì theo khoảng ngày; có lần đang chờ / đang chạy thì tự lấy lại mỗi 5 giây.
 * @param range Từ ngày, đến ngày (yyyy-MM-dd).
 */
export function useMaintenanceRuns(range: DateRange) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'dbm-runs', range.from, range.to],
    queryFn: () => apiFetch<MaintenanceRunPage>(`/api/ops/v1/db-maintenance/runs?from=${range.from}&to=${range.to}`),
    placeholderData: keepPreviousData,
    retry: false,
    refetchInterval: (q) => (q.state.data?.items.some((r) => r.status === 'Queued' || r.status === 'Running') ? 5000 : false),
  })
}

/** Đặt VACUUM ANALYZE (một bảng / cả CSDL) hoặc REINDEX một bảng vào hàng chờ (quyền System.OpsMaintenance.Run). */
export function useQueueMaintenance() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (v: { kind: 'Vacuum' | 'Reindex'; schema?: string; table?: string }) =>
      apiFetch<MaintenanceRun>('/api/ops/v1/db-maintenance/runs', {
        method: 'POST',
        body: { kind: maintenanceKindValue[v.kind], schema: v.schema ?? null, table: v.table ?? null },
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['ops'] }),
  })
}

/** Ngắt phiên – xác nhận Q6: mật khẩu + lý do + gõ "NGẮT PHIÊN". */
export function useTerminateSession() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (v: { pid: number; password: string; reason: string; confirmation: string }) =>
      apiFetch<MaintenanceRun>('/api/ops/v1/db-maintenance/sessions/terminate', { method: 'POST', body: v }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['ops'] }),
  })
}
