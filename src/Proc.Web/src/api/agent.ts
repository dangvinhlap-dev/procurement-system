import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { DateRange } from './logs'
import type { AppEnvironment, OverviewStatus } from './operations'

/*
 * Bộ thực thi (màn hình AGT) và chế độ bảo trì – lát QT5 (docs/ke-hoach-qt.md QT5). Khớp Proc.Application.Operations.Agent.
 */

/** Chữ phải gõ khi tạo khóa bộ thực thi (Q6 – trùng AgentConfirmations.CreateKey máy chủ). */
export const CREATE_KEY_CONFIRMATION = 'TẠO KHÓA'

/** Loại lệnh (tên enum máy chủ trả về). */
export type AgentCommandKind = 'Ping'

/** Số enum máy chủ khi ra lệnh (API nhận enum dạng số). */
const agentCommandKindValue: Record<AgentCommandKind, number> = { Ping: 0 }

/** Trạng thái lệnh. */
export type AgentCommandStatus = 'Queued' | 'Running' | 'Succeeded' | 'Failed' | 'Expired' | 'Cancelled'

/** Một môi trường có bộ thực thi (tab Môi trường). */
export interface AgentEnvironment {
  id: string
  code: Exclude<AppEnvironment, 'None'>
  name: string
  address: string | null
  isActive: boolean
  /** Là môi trường của chính máy chủ đang mở. */
  isCurrent: boolean
  hasKey: boolean
  keyHint: string | null
  keyCreatedAt: string | null
  keyCreatedByName: string | null
  lastSeenAt: string | null
  agentVersion: string | null
  appVersion: string | null
  hostName: string | null
  light: OverviewStatus
  statusLabel: string
  note: string | null
  queuedCount: number
  runningCommandId: string | null
  /** Đã tạo khóa dịch vụ (lát QT9 – máy đó gọi máy này, ví dụ TEST chuyển báo lỗi về PROD). */
  hasServiceKey?: boolean
  serviceKeyHint?: string | null
  serviceKeyCreatedAt?: string | null
}

/** Loại khóa: bộ thực thi (lệnh quyền cao) hoặc dịch vụ (máy chủ gọi máy chủ – lát QT9). */
export type AgentKeyKind = 'agent' | 'service'

/** Một lệnh bộ thực thi (tab Lệnh). */
export interface AgentCommand {
  id: string
  environmentId: string
  environmentCode: string
  environmentName: string
  kind: AgentCommandKind
  kindLabel: string
  status: AgentCommandStatus
  statusLabel: string
  light: OverviewStatus
  reason: string
  createdByName: string | null
  createdAt: string
  expiresAt: string
  startedAt: string | null
  finishedAt: string | null
  durationSeconds: number | null
  resultMessage: string | null
  cancelledByName: string | null
  /** Nhật ký bộ thực thi gửi về – chỉ có khi xem một lệnh. */
  journal: string | null
}

/** Kết quả tìm lệnh. */
export interface AgentCommandPage {
  items: AgentCommand[]
  truncated: boolean
}

/** Khóa vừa tạo – chỉ hiện một lần. */
export interface AgentKeyCreated {
  environmentId: string
  key: string
  hint: string
}

/** Chế độ bảo trì. */
export interface MaintenanceMode {
  isOn: boolean
  message: string | null
  reason: string | null
  startedAt: string | null
  changedByName: string | null
  changedAt: string | null
}

/** Các môi trường; có lệnh chờ / chạy thì tự lấy lại mỗi 5 giây (thấy bộ thực thi nhận lệnh). */
export function useAgentEnvironments() {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'agent-environments'],
    queryFn: () => apiFetch<AgentEnvironment[]>('/api/ops/v1/agent-admin/environments'),
    retry: false,
    refetchInterval: (q) => (q.state.data?.some((e) => e.queuedCount > 0 || e.runningCommandId) ? 5000 : 30_000),
  })
}

/**
 * Lịch sử lệnh theo khoảng ngày; có lệnh chờ / chạy thì tự lấy lại mỗi 5 giây.
 * @param range Từ ngày, đến ngày (yyyy-MM-dd).
 */
export function useAgentCommands(range: DateRange) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'agent-commands', range.from, range.to],
    queryFn: () => apiFetch<AgentCommandPage>(`/api/ops/v1/agent-admin/commands?from=${range.from}&to=${range.to}`),
    placeholderData: keepPreviousData,
    retry: false,
    refetchInterval: (q) => (q.state.data?.items.some((c) => c.status === 'Queued' || c.status === 'Running') ? 5000 : false),
  })
}

/**
 * Một lệnh kèm nhật ký chi tiết.
 * @param id Id lệnh; null = không lấy.
 */
export function useAgentCommand(id: string | null) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'agent-command', id],
    queryFn: () => apiFetch<AgentCommand>(`/api/ops/v1/agent-admin/commands/${id}`),
    enabled: id != null,
    retry: false,
    refetchInterval: (q) => (q.state.data?.status === 'Queued' || q.state.data?.status === 'Running' ? 5000 : false),
  })
}

/** Khai môi trường mới hoặc sửa (có id). */
export function useSaveAgentEnvironment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (v: { id?: string; code: string; name: string; address: string | null; isActive: boolean }) =>
      apiFetch<AgentEnvironment>(v.id ? `/api/ops/v1/agent-admin/environments/${v.id}` : '/api/ops/v1/agent-admin/environments', {
        method: v.id ? 'PUT' : 'POST',
        body: { code: v.code, name: v.name, address: v.address, isActive: v.isActive },
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['ops'] }),
  })
}

/** Tạo khóa bộ thực thi / khóa dịch vụ mới – xác nhận Q6 (mật khẩu + gõ "TẠO KHÓA"); khóa cũ cùng loại hết hiệu lực. */
export function useCreateAgentKey() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (v: { environmentId: string; kind: AgentKeyKind; password: string; confirmation: string }) =>
      apiFetch<AgentKeyCreated>(`/api/ops/v1/agent-admin/environments/${v.environmentId}/${v.kind === 'service' ? 'service-key' : 'key'}`, {
        method: 'POST',
        body: { password: v.password, confirmation: v.confirmation },
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['ops'] }),
  })
}

/** Ra lệnh cho bộ thực thi (QT5: "Lệnh thử" – Ping). */
export function useQueueAgentCommand() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (v: { environmentId: string; kind: AgentCommandKind; reason: string }) =>
      apiFetch<AgentCommand>('/api/ops/v1/agent-admin/commands', {
        method: 'POST',
        body: { environmentId: v.environmentId, kind: agentCommandKindValue[v.kind], reason: v.reason },
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['ops'] }),
  })
}

/** Hủy lệnh còn chờ. */
export function useCancelAgentCommand() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => apiFetch<AgentCommand>(`/api/ops/v1/agent-admin/commands/${id}/cancel`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['ops'] }),
  })
}

/** Chế độ bảo trì (màn hình SYS). */
export function useMaintenanceMode() {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'maintenance-mode'],
    queryFn: () => apiFetch<MaintenanceMode>('/api/ops/v1/maintenance-mode'),
    retry: false,
  })
}

/** Bật / tắt chế độ bảo trì (quyền System.OpsMaintenanceMode.Manage, bắt buộc lý do). */
export function useSetMaintenanceMode() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (v: { isOn: boolean; message: string | null; reason: string }) =>
      apiFetch<MaintenanceMode>('/api/ops/v1/maintenance-mode', { method: 'PUT', body: v }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['ops'] })
      void queryClient.invalidateQueries({ queryKey: ['environment-status'] })
    },
  })
}
