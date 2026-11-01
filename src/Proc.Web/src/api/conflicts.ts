import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from './client'

/** Loại quan hệ lợi ích – khớp enum Proc.Domain.MasterData.ConflictRelation. */
export type ConflictRelation = 'Family' | 'Ownership' | 'Employment' | 'Other'

/** Nhãn loại quan hệ. */
export const conflictRelationLabels: Record<ConflictRelation, string> = {
  Family: 'Người thân sở hữu/làm việc tại NCC',
  Ownership: 'Góp vốn, cổ phần',
  Employment: 'Đang/từng làm việc cho NCC',
  Other: 'Quan hệ khác',
}

/**
 * Kê khai xung đột lợi ích nhân sự ↔ NCC (design.md mục 18.8) – khớp Proc.Application.MasterData.ConflictDeclarationDto.
 * Còn hiệu lực thì người đó không duyệt, chấm thầu, chọn thầu có NCC này.
 */
export interface ConflictDeclaration {
  id: string
  userId: string
  userName: string
  userFullName: string
  supplierId: string
  supplierCode: string
  supplierName: string
  relation: ConflictRelation
  description: string
  validFrom: string
  validTo: string | null
  endReason: string | null
  /** Còn hiệu lực hôm nay. */
  isActive: boolean
  createdAt: string
  createdByName: string | null
}

/** Ghi nhận kê khai; userId null = tự kê khai. */
export interface ConflictDeclarationInput {
  userId: string | null
  supplierId: string
  relation: ConflictRelation
  description: string
  validFrom: string | null
}

/** Danh sách kê khai: của mình, hoặc toàn bộ khi có quyền xem mọi kê khai (máy chủ tự giới hạn). */
export function useConflictDeclarations() {
  return useQuery({
    queryKey: ['conflict-declarations'],
    queryFn: () => apiFetch<ConflictDeclaration[]>('/api/conflict-declarations'),
  })
}

/** Mọi NCC (mọi trạng thái) để chọn khi kê khai. */
export function useConflictSupplierOptions(enabled = true) {
  return useQuery({
    queryKey: ['conflict-declarations', 'suppliers'],
    queryFn: () => apiFetch<{ id: string; code: string; name: string }[]>('/api/conflict-declarations/suppliers'),
    enabled,
  })
}

/** Người dùng để chọn khi ghi kê khai thay (cần quyền quản lý kê khai). */
export function useConflictUserOptions(enabled: boolean) {
  return useQuery({
    queryKey: ['conflict-declarations', 'users'],
    queryFn: () => apiFetch<{ id: string; userName: string; fullName: string }[]>('/api/conflict-declarations/users'),
    enabled,
  })
}

/** Ghi nhận, kết thúc kê khai. Xong thì tải lại danh sách. */
export function useConflictDeclarationMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['conflict-declarations'] })
  return {
    create: useMutation({
      mutationFn: (input: ConflictDeclarationInput) =>
        apiFetch<ConflictDeclaration>('/api/conflict-declarations', { method: 'POST', body: input }),
      onSuccess,
    }),
    end: useMutation({
      mutationFn: ({ id, validTo, reason }: { id: string; validTo: string; reason: string }) =>
        apiFetch<ConflictDeclaration>(`/api/conflict-declarations/${id}/end`, { method: 'POST', body: { validTo, reason } }),
      onSuccess,
    }),
  }
}
