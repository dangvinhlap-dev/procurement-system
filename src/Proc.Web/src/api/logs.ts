import { useQuery } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Kiểu dữ liệu và hàm gọi API nhật ký (khớp Proc.Application.Logging). Viết tay tạm thời như administration.ts.
 */

/** Kết quả tìm nhật ký: tối đa một giới hạn dòng, truncated = còn dòng chưa trả. */
export interface LogPage<T> {
  items: T[]
  truncated: boolean
}

/** Một dòng nhật ký thao tác. */
export interface AuditLogItem {
  id: string
  occurredAt: string
  /** null = hệ thống (khởi tạo dữ liệu, job nền). */
  userName: string | null
  entityType: string
  entityName: string
  entityId: string
  entityLabel: string | null
  documentNumber: string | null
  action: string
  changedFields: string[]
  ipAddress: string | null
}

/** Chi tiết một dòng nhật ký thao tác. */
export interface AuditLogDetail {
  item: AuditLogItem
  userAgent: string | null
  correlationId: string | null
  changes: { field: string; oldValue: string | null; newValue: string | null }[]
}

/** Sự kiện truy cập – khớp AccessEvent ở server. */
export type AccessEvent =
  | 'Login'
  | 'LoginFailed'
  | 'LockedOut'
  | 'Logout'
  | 'PasswordChanged'
  | 'PasswordChangeFailed'
  | 'CompanySwitched'

/** Một dòng nhật ký truy cập. */
export interface AccessLogItem {
  id: string
  occurredAt: string
  event: AccessEvent
  userName: string | null
  companyCode: string | null
  detail: string | null
  ipAddress: string | null
  userAgent: string | null
}

/** Khoảng ngày lọc, dạng yyyy-MM-dd theo giờ Việt Nam. */
export interface DateRange {
  from: string
  to: string
}

/** Mặc định xem 7 ngày gần nhất – đủ cho tra cứu thường ngày mà không tải quá nhiều dòng. */
export function defaultLogRange(): DateRange {
  return { from: dayjs().subtract(6, 'day').format('YYYY-MM-DD'), to: dayjs().format('YYYY-MM-DD') }
}

/**
 * Nhật ký thao tác của công ty đang làm việc trong khoảng ngày.
 * @param range Khoảng ngày.
 */
export function useAuditLogs(range: DateRange) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['audit-logs', companyId, range.from, range.to],
    queryFn: () => apiFetch<LogPage<AuditLogItem>>(`/api/audit-logs?from=${range.from}&to=${range.to}`),
  })
}

/**
 * Chi tiết một dòng nhật ký thao tác.
 * @param id Id dòng nhật ký.
 */
export function useAuditLog(id: string) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['audit-logs', companyId, id],
    queryFn: () => apiFetch<AuditLogDetail>(`/api/audit-logs/${id}`),
    // Nhật ký không bao giờ đổi nên giữ cache mãi.
    staleTime: Infinity,
  })
}

/**
 * Nhật ký truy cập trong khoảng ngày (phạm vi theo công ty đang làm việc – xem design.md mục 9.3).
 * @param range Khoảng ngày.
 */
export function useAccessLogs(range: DateRange) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['access-logs', companyId, range.from, range.to],
    queryFn: () => apiFetch<LogPage<AccessLogItem>>(`/api/access-logs?from=${range.from}&to=${range.to}`),
  })
}
