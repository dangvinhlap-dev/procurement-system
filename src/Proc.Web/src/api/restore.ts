import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { DateRange } from './logs'
import type { OverviewStatus } from './operations'

/*
 * Phục hồi – màn hình RST (lát QT6, design.md mục 21.4, 21.15; ui-style.md mục 5o). API dưới /api/ops/v1/restore, chỉ ở tổng công ty;
 * xem cần System.OpsRestore.View, thao tác cần System.OpsRestore.Run.
 */

/** Cách phục hồi. */
export type RestoreKind = 'Dump' | 'PointInTime'

/** Số của enum RestoreKind ở máy chủ (API nhận enum dạng số như các API khác). */
const restoreKindValue: Record<RestoreKind, number> = { Dump: 0, PointInTime: 1 }

/** Trạng thái lần phục hồi. */
export type RestoreStatus = 'Queued' | 'Restoring' | 'Ready' | 'Swapping' | 'Swapped' | 'Failed' | 'Discarded'

/** Chữ phải gõ để xác nhận hoán đổi (Q6). */
export const RESTORE_CONFIRMATION = 'KHÔI PHỤC'

/** Một dòng so sánh số dòng một bảng. */
export interface RestoreCompareRow {
  schema: string
  table: string
  liveRows: number | null
  restoreRows: number | null
  /** Bảng vận hành – khi hoán đổi giữ theo CSDL đang chạy. */
  operational: boolean
  light: OverviewStatus
}

/** Bảng so sánh CSDL đang chạy với bản khôi phục. */
export interface RestoreComparison {
  comparedAt: string
  liveMigration: string | null
  restoreMigration: string | null
  rows: RestoreCompareRow[]
}

/** Kế hoạch khôi phục về mốc thời gian (chuỗi Full → Diff → WAL). */
export interface RestorePlan {
  targetTime: string
  feasible: boolean
  fullRunId: string | null
  fullFinishedAt: string | null
  fullPath: string | null
  diffRunId: string | null
  diffFinishedAt: string | null
  diffPath: string | null
  firstWal: string | null
  lastWal: string | null
  walCount: number
  lastArchivedAt: string | null
  problems: string[]
  warnings: string[]
}

/** Một lần phục hồi. */
export interface RestoreRun {
  id: string
  kind: RestoreKind
  kindLabel: string
  status: RestoreStatus
  statusLabel: string
  light: OverviewStatus
  sourceLabel: string
  dumpRunId: string | null
  fullRunId: string | null
  diffRunId: string | null
  targetTime: string | null
  liveDatabase: string
  restoreDatabase: string
  oldDatabase: string | null
  tempPort: number | null
  reason: string
  requestedByName: string | null
  queuedAt: string
  startedAt: string | null
  readyAt: string | null
  swapReason: string | null
  swapRequestedByName: string | null
  swapRequestedAt: string | null
  finishedAt: string | null
  discardedByName: string | null
  agentCommandId: string | null
  canSwap: boolean | null
  note: string | null
  errorMessage: string | null
  comparison: RestoreComparison | null
  plan: RestorePlan | null
}

/** Lịch sử phục hồi. */
export interface RestoreRunPage {
  items: RestoreRun[]
  truncated: boolean
}

/** Bản pg_dump chọn được. */
export interface RestoreDumpSource {
  id: string
  finishedAt: string | null
  relativePath: string | null
  sizeBytes: number | null
  fileExists: boolean
}

/** Thông tin chung màn hình RST. */
export interface RestoreOverview {
  liveDatabase: string
  dumps: RestoreDumpSource[]
  usesMaintenanceAccount: boolean
  walArchiveConfigured: boolean
  tempPort: number
  agentEnvironment: string
  agentReady: boolean
  agentNote: string | null
  /** CSDL khác cùng máy chủ PostgreSQL – khôi phục về mốc thời gian cũng đưa các CSDL này về mốc. */
  otherDatabases: string[]
}

/** Trạng thái còn chờ kết quả (lưới tự làm mới). */
const OPEN: RestoreStatus[] = ['Queued', 'Restoring', 'Swapping']

/** Thông tin chung: CSDL đang chạy, bản dump, bộ thực thi, CSDL khác cùng máy chủ. */
export function useRestoreOverview() {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'restore-overview'],
    queryFn: () => apiFetch<RestoreOverview>('/api/ops/v1/restore/overview'),
    retry: false,
  })
}

/**
 * Lịch sử phục hồi theo khoảng ngày; còn lần đang chờ / khôi phục / hoán đổi thì tự lấy lại mỗi 5 giây.
 * @param range Từ ngày, đến ngày (yyyy-MM-dd).
 */
export function useRestoreRuns(range: DateRange) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'restore-runs', range.from, range.to],
    queryFn: () => apiFetch<RestoreRunPage>(`/api/ops/v1/restore/runs?from=${range.from}&to=${range.to}`),
    placeholderData: keepPreviousData,
    retry: false,
    refetchInterval: (q) => (q.state.data?.items.some((r) => OPEN.includes(r.status)) ? 5000 : false),
  })
}

/**
 * Một lần phục hồi kèm bảng so sánh, kế hoạch; đang chờ kết quả thì tự lấy lại mỗi 5 giây.
 * @param id Id lần phục hồi; null = không lấy.
 */
export function useRestoreRun(id: string | null) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'restore-run', id],
    queryFn: () => apiFetch<RestoreRun>(`/api/ops/v1/restore/runs/${id}`),
    enabled: id != null,
    retry: false,
    refetchInterval: (q) => (q.state.data && OPEN.includes(q.state.data.status) ? 5000 : false),
  })
}

/**
 * Lập kế hoạch khôi phục về mốc (chỉ xem).
 * @param target Mốc (ISO có múi giờ); null = không lấy.
 */
export function useRestorePlan(target: string | null) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'restore-plan', target],
    queryFn: () => apiFetch<RestorePlan>(`/api/ops/v1/restore/plan?target=${encodeURIComponent(target!)}`),
    enabled: target != null,
    retry: false,
  })
}

/** Đặt lần phục hồi (pg_dump hoặc về mốc). */
export function useQueueRestore() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (v: { kind: RestoreKind; dumpRunId?: string; targetTime?: string; reason: string }) =>
      apiFetch<RestoreRun>('/api/ops/v1/restore/runs', {
        method: 'POST',
        body: { kind: restoreKindValue[v.kind], dumpRunId: v.dumpRunId ?? null, targetTime: v.targetTime ?? null, reason: v.reason },
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['ops'] }),
  })
}

/** Thao tác trên một lần phục hồi: so sánh lại, bỏ bản khôi phục. */
export function useRestoreAction() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (v: { id: string; action: 'compare' | 'discard' }) =>
      apiFetch<RestoreRun>(`/api/ops/v1/restore/runs/${v.id}/${v.action}`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['ops'] }),
  })
}

/** Hoán đổi – xác nhận Q6: mật khẩu + lý do + gõ "KHÔI PHỤC". */
export function useSwapRestore() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (v: { id: string; password: string; reason: string; confirmation: string }) =>
      apiFetch<RestoreRun>(`/api/ops/v1/restore/runs/${v.id}/swap`, {
        method: 'POST',
        body: { password: v.password, reason: v.reason, confirmation: v.confirmation },
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['ops'] }),
  })
}
