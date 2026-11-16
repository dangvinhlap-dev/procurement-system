import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import type { StatusTone } from '../components/DataGrid/types'
import { apiFetch, apiPostFile } from './client'

/** Chữ xác nhận quay về bản cũ (Q6 – khớp IReleaseService.RollbackConfirmation). */
export const ROLLBACK_CONFIRMATION = 'QUAY VỀ'

/** Chữ xác nhận cài – "CÀI " + phiên bản (khớp IReleaseService.DeployConfirmation). */
export const deployConfirmation = (version: string) => `CÀI ${version}`

export type ReleaseStatus = 'Received' | 'UatInProgress' | 'UatPassed' | 'ProdInstalled' | 'Closed' | 'Cancelled' | 'RolledBack'
export type RollbackLevel = 'CodeOnly' | 'RemoveSchema' | 'RestoreDatabase'
export type ReleaseTestResult = 'NotTested' | 'Passed' | 'Failed'
export type ReleaseDefectStatus = 'Open' | 'Fixed' | 'Rejected'

/** Mức quay về: số, chữ, hậu quả (design.md 21.6a). */
export const rollbackLevels: Record<RollbackLevel, { no: number; label: string; note: string }> = {
  CodeOnly: { no: 1, label: '1 – Chỉ trả code', note: 'Giữ toàn bộ dữ liệu; bảng / cột mới nằm im.' },
  RemoveSchema: { no: 2, label: '2 – Trả code + gỡ cấu trúc', note: 'Mất dữ liệu ở bảng / cột bị gỡ (script gỡ của bản).' },
  RestoreDatabase: { no: 3, label: '3 – Trả code + khôi phục CSDL', note: 'Mất mọi dữ liệu nhập sau lần cài (khôi phục bản sao lưu trước cài).' },
}

export const testResultLabels: Record<ReleaseTestResult, { label: string; tone: StatusTone }> = {
  NotTested: { label: 'Chưa kiểm thử', tone: 'neutral' },
  Passed: { label: 'Đạt', tone: 'success' },
  Failed: { label: 'Không đạt', tone: 'error' },
}

export const defectStatusLabels: Record<ReleaseDefectStatus, { label: string; tone: StatusTone }> = {
  Open: { label: 'Chưa xử lý', tone: 'warning' },
  Fixed: { label: 'Đã sửa', tone: 'success' },
  Rejected: { label: 'Không sửa', tone: 'neutral' },
}

export interface ReleaseListItem {
  id: string
  version: string
  status: ReleaseStatus
  statusLabel: string
  tone: StatusTone
  source: string
  packageSize: number
  receivedAt: string
  receivedByName: string | null
  testedItems: number
  totalItems: number
  openDefects: number
  prodInstalledAt: string | null
}

export interface ReleaseTestItem {
  id: string
  seq: number
  section: string
  content: string
  result: ReleaseTestResult
  testerName: string | null
  testedAt: string | null
  comment: string | null
}

export interface ReleaseDefect {
  id: string
  title: string
  description: string | null
  screen: string | null
  reporterName: string | null
  reportedAt: string
  status: ReleaseDefectStatus
  resolution: string | null
  resolvedByName: string | null
  resolvedAt: string | null
}

export interface ReleaseInstall {
  id: string
  environment: string
  kind: 'Deploy' | 'Rollback'
  status: 'Pending' | 'Succeeded' | 'Failed'
  previousVersion: string | null
  targetVersion: string | null
  level: RollbackLevel | null
  installedSha256: string | null
  reason: string | null
  byName: string | null
  startedAt: string
  finishedAt: string | null
  message: string | null
  agentCommandId: string
}

export interface ReleaseTicket {
  id: string
  version: string
  status: ReleaseStatus
  statusLabel: string
  tone: StatusTone
  source: string
  sourceUrl: string | null
  packageFileName: string
  packageSize: number
  sha256: string
  changelog: string | null
  databaseChanges: string | null
  minRollbackLevel: RollbackLevel
  risks: string | null
  rollbackPlan: string | null
  note: string | null
  receivedAt: string
  receivedByName: string | null
  uatSignedAt: string | null
  uatSignedByName: string | null
  uatComment: string | null
  uatWithoutTest: boolean
  prodInstalledAt: string | null
  endedAt: string | null
  endedByName: string | null
  endReason: string | null
  isLocked: boolean
  openIssues: string[]
  items: ReleaseTestItem[]
  defects: ReleaseDefect[]
  installs: ReleaseInstall[]
  /** TEST đang chạy bản của phiếu (lát QT9 – quay về trên TEST). */
  installedOnTest: boolean
}

export interface ReleaseEnvironment {
  code: string
  name: string
  agentOnline: boolean
  appVersion: string | null
  lastSeenAt: string | null
}

export interface ReleaseOverview {
  thisEnvironment: string
  thisVersion: string
  canWrite: boolean
  readOnlyReason: string | null
  singleEnvironment: boolean
  gitHubEnabled: boolean
  maxPackageSizeMb: number
  environments: ReleaseEnvironment[]
}

const base = '/api/ops/v1/releases'

/** Bảng đầu REL: môi trường, phiên bản, ghi được không. */
export function useReleaseOverview() {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({ queryKey: ['ops', companyId, 'releases', 'overview'], queryFn: () => apiFetch<ReleaseOverview>(`${base}/overview`), retry: false })
}

/** Danh sách phiếu phát hành; còn lần cài chờ thì tự làm mới 5 giây (lần cài báo kết quả qua bộ thực thi). */
export function useReleases() {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({ queryKey: ['ops', companyId, 'releases', 'list'], queryFn: () => apiFetch<ReleaseListItem[]>(base), retry: false })
}

/** Một phiếu; còn lần cài chờ thì tự làm mới 5 giây. */
export function useRelease(id: string | undefined) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'releases', id],
    queryFn: () => apiFetch<ReleaseTicket>(`${base}/${id}`),
    enabled: !!id,
    retry: false,
    refetchInterval: (q) => (q.state.data?.installs.some((i) => i.status === 'Pending') ? 5000 : false),
  })
}

/** Các thao tác trên phiếu – xong thì làm mới danh sách và phiếu. */
export function useReleaseMutations() {
  const queryClient = useQueryClient()
  const done = () => void queryClient.invalidateQueries({ queryKey: ['ops'] })
  const post = <T,>(path: string, body: unknown, method: 'POST' | 'PUT' = 'POST') =>
    apiFetch<T>(`${base}${path}`, { method, body })
  return {
    upload: useMutation({
      mutationFn: async (v: { file: File; sha256: string | null; note: string | null }) => {
        const query = new URLSearchParams({ fileName: v.file.name })
        if (v.sha256) query.set('sha256', v.sha256)
        if (v.note) query.set('note', v.note)
        return apiPostFile<ReleaseTicket>(`${base}/upload?${query}`, v.file, 'application/zip')
      },
      onSuccess: done,
    }),
    github: useMutation({ mutationFn: (v: { version: string; note: string | null }) => post<ReleaseTicket>('/github', v), onSuccess: done }),
    info: useMutation({
      mutationFn: (v: { id: string; risks: string | null; rollbackPlan: string | null; note: string | null }) =>
        post<ReleaseTicket>(`/${v.id}/info`, v, 'PUT'),
      onSuccess: done,
    }),
    note: useMutation({ mutationFn: (v: { id: string; text: string }) => post<ReleaseTicket>(`/${v.id}/notes`, v), onSuccess: done }),
    test: useMutation({
      mutationFn: (v: { id: string; itemId: string; result: ReleaseTestResult; comment: string | null }) =>
        post<ReleaseTicket>(`/${v.id}/items/${v.itemId}`, v, 'PUT'),
      onSuccess: done,
    }),
    addDefect: useMutation({
      mutationFn: (v: { id: string; title: string; description: string | null; screen: string | null }) =>
        post<ReleaseTicket>(`/${v.id}/defects`, v),
      onSuccess: done,
    }),
    resolveDefect: useMutation({
      mutationFn: (v: { id: string; defectId: string; status: ReleaseDefectStatus; resolution: string | null }) =>
        post<ReleaseTicket>(`/${v.id}/defects/${v.defectId}`, v, 'PUT'),
      onSuccess: done,
    }),
    signOff: useMutation({ mutationFn: (v: { id: string; comment: string | null }) => post<ReleaseTicket>(`/${v.id}/uat-sign-off`, v), onSuccess: done }),
    close: useMutation({ mutationFn: (id: string) => post<ReleaseTicket>(`/${id}/close`, {}), onSuccess: done }),
    cancel: useMutation({ mutationFn: (v: { id: string; reason: string }) => post<ReleaseTicket>(`/${v.id}/cancel`, v), onSuccess: done }),
    deploy: useMutation({
      mutationFn: (v: { id: string; environment: string; reason: string; password: string; confirmation: string }) =>
        post<ReleaseTicket>(`/${v.id}/deploy`, v),
      onSuccess: done,
    }),
    rollback: useMutation({
      mutationFn: (v: { id: string; environment: 'Test' | 'Prod'; level: RollbackLevel; reason: string; password: string; confirmation: string }) =>
        post<ReleaseTicket>(`/${v.id}/rollback`, v),
      onSuccess: done,
    }),
  }
}
