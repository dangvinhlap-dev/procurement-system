import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Kiểu dữ liệu và hàm gọi API kho (khớp Proc.Application.Inventory).
 * Viết tay tạm thời; sẽ sinh tự động từ OpenAPI khi số API nhiều lên (design.md mục 3.2).
 */

/** Trạng thái kỳ kho (design.md mục 7.1). */
export type PeriodStatus = 'NotOpened' | 'Open' | 'Costing' | 'Closed' | 'Locked'

/** Tên tiếng Việt của trạng thái kỳ. */
export const periodStatusLabels: Record<PeriodStatus, string> = {
  NotOpened: 'Chưa mở',
  Open: 'Đang mở',
  Costing: 'Đang tính giá',
  Closed: 'Đã đóng',
  Locked: 'Khóa vĩnh viễn',
}

/** Một kỳ kho. */
export interface InventoryPeriod {
  id: string
  year: number
  /** 0 = kỳ năm. */
  month: number
  label: string
  /** yyyy-MM-dd. */
  startDate: string
  endDate: string
  status: PeriodStatus
  closedAt: string | null
  /** Mở được ngay bây giờ (đúng thứ tự, có quyền). */
  canOpen: boolean
  /** Kỳ đang mở, kỳ trước đã đóng, người xem có quyền đóng kỳ. */
  canClose: boolean
  /** Kỳ đã đóng gần nhất, người xem có quyền mở lại. */
  canReopen: boolean
  /** Lý do lần mở lại gần nhất. */
  reopenReason: string | null
}

/** Lịch kỳ của công ty đang làm việc. */
export interface InventoryCalendar {
  years: number[]
  year: number | null
  periods: InventoryPeriod[]
  nextYear: number
}

/**
 * Lịch kỳ kho một năm của công ty đang làm việc.
 * @param year Năm; undefined = năm đang nhập liệu.
 */
export function useInventoryCalendar(year: number | undefined) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['inventory-periods', companyId, year ?? 'current'],
    queryFn: () => apiFetch<InventoryCalendar>(`/api/inventory-periods${year ? `?year=${year}` : ''}`),
    placeholderData: keepPreviousData,
  })
}

/** Tạo lịch năm, mở kỳ tháng. Xong thì tải lại lịch kỳ. */
export function useInventoryPeriodMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['inventory-periods'] })
  return {
    createYear: useMutation({
      mutationFn: (year: number) => apiFetch<InventoryCalendar>('/api/inventory-periods/years', { method: 'POST', body: { year } }),
      onSuccess,
    }),
    open: useMutation({
      mutationFn: (id: string) => apiFetch<InventoryPeriod>(`/api/inventory-periods/${id}/open`, { method: 'POST' }),
      onSuccess,
    }),
    // Đóng/mở lại kỳ đổi giá vốn, số dư: làm mới cả tồn kho, báo cáo và chứng từ.
    close: useMutation({
      mutationFn: (id: string) => apiFetch<InventoryPeriod>(`/api/inventory-periods/${id}/close`, { method: 'POST' }),
      onSuccess: () => {
        onSuccess()
        void queryClient.invalidateQueries({ queryKey: ['stock'] })
        void queryClient.invalidateQueries({ queryKey: ['inventory-documents'] })
      },
    }),
    reopen: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) =>
        apiFetch<InventoryPeriod>(`/api/inventory-periods/${id}/reopen`, { method: 'POST', body: { reason } }),
      onSuccess: () => {
        onSuccess()
        void queryClient.invalidateQueries({ queryKey: ['stock'] })
        void queryClient.invalidateQueries({ queryKey: ['inventory-documents'] })
      },
    }),
  }
}
