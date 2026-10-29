import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/** Cấu hình đánh số một loại chứng từ – khớp Proc.Application.Documents.DocumentNumberingDto. */
export interface DocumentNumbering {
  docType: string
  name: string
  format: string
  /** Công ty chưa cấu hình riêng, đang dùng mẫu mặc định. */
  isDefault: boolean
  /** Số cấp gần nhất trong kỳ hiện tại; null nếu kỳ này chưa cấp số nào. */
  lastNumber: string | null
  /** Số sẽ cấp kế tiếp nếu lập chứng từ hôm nay (chỉ để xem). */
  nextNumber: string
}

/** Cấu hình đánh số của công ty đang làm việc. */
export function useDocumentNumbering() {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: ['document-numbering', companyId],
    queryFn: () => apiFetch<DocumentNumbering[]>('/api/document-numbering'),
  })
}

/** Đổi mẫu số; format null/rỗng = về mẫu mặc định. */
export function useUpdateDocumentNumbering() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ docType, format }: { docType: string; format: string | null }) =>
      apiFetch<DocumentNumbering>(`/api/document-numbering/${docType}`, { method: 'PUT', body: { format } }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['document-numbering'] }),
  })
}
