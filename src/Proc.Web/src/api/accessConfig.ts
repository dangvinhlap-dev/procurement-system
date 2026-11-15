import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { SystemOverviewRow } from './operations'

/** Chữ xác nhận "Đăng xuất tất cả" (Q6 – khớp IAccessConfigService.SignOutAllConfirmation). */
export const SIGN_OUT_ALL_CONFIRMATION = 'ĐĂNG XUẤT TẤT CẢ'

/** Các giá trị sửa được trên CFG (lát QT7 – design.md mục 21.16). */
export interface AccessPolicyValues {
  minPasswordLength: number
  blockCommonPasswords: boolean
  /** Mật khẩu cấm thêm, mỗi dòng một mật khẩu; chuỗi rỗng = không có; null khi người xem không có quyền quản lý CFG. */
  extraBlockedPasswords: string | null
  maxFailedLogins: number
  lockoutMinutes: number
  sessionMinutes: number
  /** Dải IP được dùng chức năng quản trị vận hành, mỗi dòng một dải; null = mọi IP. */
  opsAllowedIpRanges: string | null
  loginPerMinutePerIp: number
  apiPerMinutePerUser: number
  heavyConcurrentPerUser: number
}

/** Cấu hình truy cập máy chủ trả về (GET /api/ops/v1/access-config). */
export interface AccessConfig extends AccessPolicyValues {
  sessionsRevokedAt: string | null
  updatedAt: string | null
  updatedByName: string | null
  commonPasswordCount: number
  /** IP của người đang xem – để khai dải IP. */
  yourIp: string | null
  /** Khóa dòng cấu hình (F1 xem lịch sử); null khi chưa lưu lần nào. */
  auditId: string | null
  /** Thông tin bảo mật chỉ đọc (header, HTTPS, proxy, giới hạn tần suất) – cùng cột với bảng SYS. */
  security: SystemOverviewRow[]
}

/** Kết quả "Đăng xuất tất cả": token mới cho phiên đang dùng. */
export interface SignOutAllResult {
  revokedAt: string
  accessToken: string
  expiresAt: string
}

/** Đọc cấu hình truy cập (quyền xem vận hành hoặc quản lý CFG, chỉ ở tổng công ty). */
export function useAccessConfig() {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['ops', companyId, 'access-config'],
    queryFn: () => apiFetch<AccessConfig>('/api/ops/v1/access-config'),
    retry: false,
  })
}

/** Lưu cấu hình truy cập (bắt buộc lý do ≥ 5 ký tự; dải IP mới phải gồm IP người lưu). */
export function useSaveAccessConfig() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (v: AccessPolicyValues & { reason: string }) =>
      apiFetch<AccessConfig>('/api/ops/v1/access-config', { method: 'PUT', body: v }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['ops'] }),
  })
}

/** "Đăng xuất tất cả" (Q6): mọi phiên cấp trước lúc bấm hết hiệu lực; trả token mới cho người bấm. */
export function useSignOutAll() {
  return useMutation({
    mutationFn: (v: { reason: string; password: string; confirmation: string }) =>
      apiFetch<SignOutAllResult>('/api/ops/v1/access-config/sign-out-all', { method: 'POST', body: v }),
  })
}
