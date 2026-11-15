import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { DateRange } from './logs'

/*
 * Kiểu dữ liệu và hàm gọi API nhật ký lỗi (màn hình ERR – lát QT2, khớp Proc.Application.Operations.ErrorLog).
 */

/** Nguồn lỗi. */
export type ErrorSource = 'Server' | 'Client'

/** Trạng thái xử lý nhóm lỗi. */
export type ErrorGroupStatus = 'New' | 'InProgress' | 'Resolved'

/** Một nhóm lỗi. */
export interface ErrorGroupItem {
  id: string
  source: ErrorSource
  status: ErrorGroupStatus
  errorType: string
  message: string
  location: string | null
  path: string | null
  firstSeenAt: string
  lastSeenAt: string
  occurrenceCount: number
  reopenCount: number
  handledByName: string | null
  handledAt: string | null
  note: string | null
  lastTrackingCode: string | null
  lastVersion: string | null
}

/** Một lần xảy ra lỗi. */
export interface ErrorOccurrence {
  id: string
  occurredAt: string
  trackingCode: string
  userName: string | null
  companyCode: string | null
  path: string | null
  method: string | null
  message: string
  detail: string | null
  version: string | null
  environment: string | null
  userAgent: string | null
}

/** Chi tiết nhóm lỗi. */
export interface ErrorGroupDetail {
  group: ErrorGroupItem
  occurrences: ErrorOccurrence[]
}

/** Bộ lọc màn hình ERR. */
export interface ErrorGroupFilter extends DateRange {
  status: ErrorGroupStatus | null
  /** Mã theo dõi người dùng báo – có thì bỏ qua khoảng ngày. */
  trackingCode: string
}

/**
 * Nhóm lỗi theo lần cuối trong khoảng ngày / trạng thái / mã theo dõi.
 * @param filter Bộ lọc.
 */
export function useErrorGroups(filter: ErrorGroupFilter) {
  const companyId = useAuth().workingCompany?.id ?? ''
  const params = new URLSearchParams({ from: filter.from, to: filter.to })
  if (filter.status) params.set('status', filter.status)
  if (filter.trackingCode.trim()) params.set('trackingCode', filter.trackingCode.trim())
  return useQuery({
    queryKey: ['ops', companyId, 'error-groups', params.toString()],
    queryFn: () => apiFetch<{ items: ErrorGroupItem[]; truncated: boolean }>(`/api/ops/v1/error-groups?${params.toString()}`),
    placeholderData: keepPreviousData,
    retry: false,
  })
}

/**
 * Chi tiết nhóm lỗi (các lần xảy ra gần nhất).
 * @param id Id nhóm; null = không tải.
 */
export function useErrorGroup(id: string | null) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'error-group', id],
    queryFn: () => apiFetch<ErrorGroupDetail>(`/api/ops/v1/error-groups/${id}`),
    enabled: id !== null,
    retry: false,
  })
}

/** Đổi trạng thái / ghi chú nhóm lỗi và gây lỗi máy chủ thử. */
export function useErrorGroupMutations() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['ops'] })
  return {
    changeStatus: useMutation({
      mutationFn: (input: { id: string; status: ErrorGroupStatus; note: string | null }) =>
        apiFetch<ErrorGroupItem>(`/api/ops/v1/error-groups/${input.id}/status`, {
          method: 'PUT',
          body: { status: input.status, note: input.note },
        }),
      onSuccess: invalidate,
    }),
    testServerError: useMutation({
      mutationFn: () => apiFetch<void>('/api/ops/v1/error-groups/test-server-error', { method: 'POST' }),
      onSettled: invalidate,
    }),
  }
}
