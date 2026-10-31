import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/** Một tham số tại công ty đang làm việc – khớp Proc.Application.Organization.SystemParameterDto. */
export interface SystemParameter {
  code: string
  group: string
  name: string
  /** Giá trị dạng chữ để hiển thị. */
  value: string
  /** Giá trị mặc định; null nếu tham số chỉ xem, không có mặc định. */
  defaultValue: string | null
  isDefault: boolean
  /** Sửa được trên màn hình Tham số hệ thống. */
  editable: boolean
  note: string
}

/** Tham số của công ty đang làm việc. */
export function useSystemParameters() {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['system-parameters', companyId],
    queryFn: () => apiFetch<SystemParameter[]>('/api/system-parameters'),
  })
}

/** Đổi một tham số; value null/rỗng = về mặc định. */
export function useUpdateSystemParameter() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ code, value }: { code: string; value: string | null }) =>
      apiFetch<SystemParameter>(`/api/system-parameters/${code}`, { method: 'PUT', body: { value } }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['system-parameters'] }),
  })
}
