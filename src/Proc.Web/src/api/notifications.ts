import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Thông báo của người đăng nhập tại công ty đang làm việc (khớp Proc.Application.Notifications, design.md mục 11.6).
 * Số chưa đọc lấy từ số liệu trang chủ (navCounts.NOTI) để không phải hỏi máy chủ thêm một lần mỗi phút.
 */

/** Loại thông báo. */
export type NotificationType = 'ApprovalPending' | 'Approved' | 'Rejected' | 'IntercompanyReceipt' | 'PeriodReopened'

/** Một thông báo. */
export interface AppNotification {
  id: string
  type: NotificationType
  title: string
  message: string
  /** Mã màn hình để mở khi bấm (PR, PO, ICR, PER…). */
  screenCode: string
  /** Phiếu/kỳ cần mở; null nếu chỉ mở màn hình. */
  targetId: string | null
  createdAt: string
  /** null = chưa đọc. */
  readAt: string | null
}

/** Tên loại thông báo hiển thị. */
export const notificationTypeLabels: Record<NotificationType, string> = {
  ApprovalPending: 'Chờ duyệt',
  Approved: 'Đã duyệt',
  Rejected: 'Bị từ chối',
  IntercompanyReceipt: 'Hàng nội bộ chờ nhận',
  PeriodReopened: 'Mở lại kỳ kho',
}

/** Thông báo của tôi, mới nhất trước. */
export function useNotifications() {
  const { workingCompany } = useAuth()
  const companyId = workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['notifications', companyId],
    queryFn: () => apiFetch<AppNotification[]>('/api/notifications'),
    enabled: !!companyId,
    // Cùng nhịp với số đếm trang chủ để danh sách và chuông không lệch nhau lâu.
    refetchInterval: 60_000,
  })
}

/** Đánh dấu đã đọc một / tất cả; xong thì tải lại danh sách và số chưa đọc trên chuông, cây điều hướng. */
export function useNotificationMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['notifications'] })
    void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
  }
  return {
    markRead: useMutation({ mutationFn: (id: string) => apiFetch<void>(`/api/notifications/${id}/read`, { method: 'POST' }), onSuccess }),
    markAllRead: useMutation({ mutationFn: () => apiFetch<void>('/api/notifications/read-all', { method: 'POST' }), onSuccess }),
  }
}
