import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { DateRange } from './logs'
import type { OverviewStatus, SystemOverviewRow } from './operations'

/*
 * Sao lưu – màn hình BAK (lát QT3b, design.md mục 21.5). Khớp Proc.Application.Operations.BackupRuns. Không có API tải file sao lưu (Q5).
 */

/** Kiểu sao lưu (tên enum máy chủ trả về trong DTO). */
export type BackupKind = 'Full' | 'Diff' | 'Dump' | 'Attachments'

/** Số enum máy chủ khi gửi "Sao lưu ngay" (API nhận enum dạng số). */
export const backupKindValue: Record<BackupKind, number> = { Full: 0, Diff: 1, Dump: 2, Attachments: 3 }

/** Chữ hiển thị kiểu sao lưu (trùng BackupLabels.Kind máy chủ). */
export const backupKindLabels: Record<BackupKind, string> = { Full: 'Full', Diff: 'Diff', Dump: 'pg_dump', Attachments: 'Đính kèm' }

/** Một lần sao lưu (tab Lịch sử). */
export interface BackupRun {
  id: string
  kind: BackupKind
  kindLabel: string
  trigger: 'Manual' | 'Schedule'
  status: 'Queued' | 'Running' | 'Succeeded' | 'Failed'
  /** Chữ tình trạng, ví dụ "Xong", "Xong – chưa chép nơi thứ hai", "Đã dọn". */
  statusLabel: string
  light: OverviewStatus
  scheduledDate: string | null
  requestedByName: string | null
  queuedAt: string
  startedAt: string | null
  finishedAt: string | null
  durationSeconds: number | null
  relativePath: string | null
  sizeBytes: number | null
  fileCount: number | null
  sha256: string | null
  baseFullRunId: string | null
  startWalFile: string | null
  verified: boolean | null
  verifyMessage: string | null
  secondaryCopied: boolean | null
  secondaryMessage: string | null
  errorMessage: string | null
  note: string | null
  purgedAt: string | null
}

/** Kết quả tìm lịch sử sao lưu. */
export interface BackupRunPage {
  items: BackupRun[]
  truncated: boolean
}

/** Hai bảng của tab "Lịch và thời hạn giữ", "Nơi lưu". */
export interface BackupOverview {
  scheduleRows: SystemOverviewRow[]
  storageRows: SystemOverviewRow[]
}

/**
 * Lịch sử sao lưu theo khoảng ngày. Có lần đang chờ / đang chạy thì tự lấy lại mỗi 5 giây để thấy kết quả.
 * @param range Từ ngày, đến ngày (yyyy-MM-dd).
 */
export function useBackupRuns(range: DateRange) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'backup-runs', range.from, range.to],
    queryFn: () => apiFetch<BackupRunPage>(`/api/ops/v1/backup/runs?from=${range.from}&to=${range.to}`),
    placeholderData: keepPreviousData,
    retry: false,
    refetchInterval: (q) => (q.state.data?.items.some((r) => r.status === 'Queued' || r.status === 'Running') ? 5000 : false),
  })
}

/** Lịch, thời hạn giữ, nơi lưu. */
export function useBackupOverview() {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'backup-overview'],
    queryFn: () => apiFetch<BackupOverview>('/api/ops/v1/backup/overview'),
    retry: false,
  })
}

/** "Sao lưu ngay" – đặt một lần sao lưu vào hàng chờ (quyền System.OpsBackup.Run). */
export function useQueueBackup() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (kind: BackupKind) =>
      apiFetch<BackupRun>('/api/ops/v1/backup/runs', { method: 'POST', body: { kind: backupKindValue[kind] } }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['ops'] }),
  })
}
