import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from './client'

/*
 * Tài khoản Portal NCC – phía nội bộ (Phòng CUVT mời, khóa, mở khóa; design.md 19.5, lát P1). Khớp Proc.Application.Portal.
 */

/** Trạng thái tài khoản Portal. */
export type SupplierPortalUserStatus = 'Invited' | 'Active' | 'Disabled'

/** Nhãn tiếng Việt của trạng thái tài khoản Portal. */
export const portalUserStatusLabels: Record<SupplierPortalUserStatus, string> = {
  Invited: 'Đã mời – chưa kích hoạt',
  Active: 'Đang dùng',
  Disabled: 'Đã khóa',
}

/** Tài khoản Portal của một NCC. */
export interface SupplierPortalUser {
  id: string
  supplierId: string
  email: string
  fullName: string
  phone: string | null
  position: string | null
  /** Quản trị NCC: tự mời, khóa người dùng con của NCC mình. */
  isSupplierAdmin: boolean
  status: SupplierPortalUserStatus
  invitedAt: string
  activatedAt: string | null
  lastLoginAt: string | null
}

/** Thông tin người được mời. */
export interface PortalInviteInput {
  email: string
  fullName: string
  phone: string | null
  position: string | null
}

/** Kết quả mời / gửi lại lời mời. */
export interface PortalInviteResult {
  user: SupplierPortalUser
  /** false khi máy chủ chưa bật email hoặc gửi lỗi – người mời tự chuyển link. */
  emailSent: boolean
  /** Link kích hoạt dùng một lần. */
  inviteUrl: string
  expiresAt: string
}

/**
 * Danh sách tài khoản Portal của NCC.
 * @param supplierId NCC; null thì không tải.
 */
export function useSupplierPortalUsers(supplierId: string | null) {
  return useQuery({
    queryKey: ['supplier-portal-users', supplierId ?? ''],
    queryFn: () => apiFetch<SupplierPortalUser[]>(`/api/suppliers/${supplierId}/portal-users`),
    enabled: supplierId !== null,
  })
}

/**
 * Mời, gửi lại lời mời, khóa, mở khóa tài khoản Portal của NCC.
 * @param supplierId NCC.
 */
export function useSupplierPortalMutations(supplierId: string) {
  const queryClient = useQueryClient()
  const refresh = () => void queryClient.invalidateQueries({ queryKey: ['supplier-portal-users', supplierId] })
  const base = `/api/suppliers/${supplierId}/portal-users`
  return {
    invite: useMutation({
      mutationFn: (input: PortalInviteInput) => apiFetch<PortalInviteResult>(base, { method: 'POST', body: input }),
      onSuccess: refresh,
    }),
    resend: useMutation({
      mutationFn: (userId: string) => apiFetch<PortalInviteResult>(`${base}/${userId}/resend-invite`, { method: 'POST' }),
      onSuccess: refresh,
    }),
    disable: useMutation({
      mutationFn: (userId: string) => apiFetch<SupplierPortalUser>(`${base}/${userId}/disable`, { method: 'POST' }),
      onSuccess: refresh,
    }),
    enable: useMutation({
      mutationFn: (userId: string) => apiFetch<SupplierPortalUser>(`${base}/${userId}/enable`, { method: 'POST' }),
      onSuccess: refresh,
    }),
  }
}
