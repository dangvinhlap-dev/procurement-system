import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { SupplierProfileRequest } from '../portal/portalApi'
import { apiFetch } from './client'

/**
 * Danh sách đề nghị cập nhật hồ sơ NCC gửi từ Portal (lát P2a, design.md 19.5), mới nhất trước.
 * @param status Lọc trạng thái; null = mọi trạng thái.
 */
export function useSupplierProfileRequests(status: string | null) {
  return useQuery({
    queryKey: ['supplier-profile-requests', 'list', status ?? ''],
    queryFn: () =>
      apiFetch<SupplierProfileRequest[]>(`/api/supplier-profile-requests${status ? `?status=${encodeURIComponent(status)}` : ''}`),
  })
}

/**
 * Chi tiết một đề nghị kèm so sánh với hồ sơ hiện tại.
 * @param id Id đề nghị; null thì không tải.
 */
export function useSupplierProfileRequest(id: string | null) {
  return useQuery({
    queryKey: ['supplier-profile-requests', 'one', id ?? ''],
    queryFn: () => apiFetch<SupplierProfileRequest>(`/api/supplier-profile-requests/${id}`),
    enabled: id !== null,
  })
}

/** Chấp nhận / từ chối đề nghị. Xong tải lại đề nghị, NCC, yêu cầu duyệt NCC (tài khoản ngân hàng), thông báo. */
export function useSupplierProfileActions() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['supplier-profile-requests'] })
    void queryClient.invalidateQueries({ queryKey: ['suppliers'] })
    void queryClient.invalidateQueries({ queryKey: ['supplier-change-requests'] })
  }
  const act = (action: 'accept' | 'reject') => ({ id, comment }: { id: string; comment: string | null }) =>
    apiFetch<SupplierProfileRequest>(`/api/supplier-profile-requests/${id}/${action}`, { method: 'POST', body: { comment } })
  return {
    accept: useMutation({ mutationFn: act('accept'), onSuccess }),
    reject: useMutation({ mutationFn: act('reject'), onSuccess }),
  }
}
