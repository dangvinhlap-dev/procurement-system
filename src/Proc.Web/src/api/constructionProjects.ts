import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/** Công ty đang làm việc – công trình theo từng công ty nên khóa bộ nhớ đệm có công ty. */
const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/** Trạng thái công trình – khớp ConstructionProjectStatus. */
export type ConstructionProjectStatus = 'InProgress' | 'Completed' | 'Suspended'

/** Nhãn trạng thái công trình. */
export const constructionStatusLabels: Record<ConstructionProjectStatus, string> = {
  InProgress: 'Đang thực hiện',
  Completed: 'Đã hoàn thành',
  Suspended: 'Tạm ngừng',
}

/** Một lần ghi dự toán – khớp ConstructionProjectRevisionDto. Lần 0 = dự toán ban đầu. */
export interface ConstructionProjectRevision {
  id: string
  revisionNo: number
  effectiveDate: string
  previousAmount: number
  newAmount: number
  difference: number
  basis: string | null
  reason: string | null
  createdAt: string
  createdByName: string | null
}

/**
 * Công trình có tổng dự toán (lát Y2, design.md mục 19.2 – loại mua A) – khớp Proc.Application.Purchasing.ConstructionProjectDto.
 * Tổng dự toán tính bằng VND chưa VAT.
 */
export interface ConstructionProject {
  id: string
  code: string
  name: string
  departmentId: string
  departmentCode: string
  departmentName: string
  location: string | null
  startDate: string | null
  expectedEndDate: string | null
  estimateAmount: number
  initialEstimateAmount: number
  revisionCount: number
  status: ConstructionProjectStatus
  note: string | null
  createdAt: string
  createdByName: string | null
  /** Lũy kế đã mua chưa VAT – dòng PO đã duyệt/đã đóng lấy từ đề nghị loại A của công trình (lát Y3, Q4). */
  spentAmount: number
  /** Lũy kế đã mua gồm VAT. */
  spentAmountWithVat: number
  /** Còn lại = dự toán − lũy kế chưa VAT (âm = đã vượt). */
  remainingAmount: number
  /** Lịch sử dự toán – chỉ có khi đọc chi tiết. */
  revisions: ConstructionProjectRevision[]
}

/** Nội dung tạo công trình. */
export interface CreateConstructionProjectInput {
  code: string
  name: string
  departmentId: string
  location: string | null
  startDate: string | null
  expectedEndDate: string | null
  estimateAmount: number
  basis: string | null
  note: string | null
}

/** Nội dung sửa công trình (không đổi mã, dự toán). */
export interface UpdateConstructionProjectInput {
  name: string
  departmentId: string
  location: string | null
  startDate: string | null
  expectedEndDate: string | null
  status: ConstructionProjectStatus
  note: string | null
}

/** Nội dung điều chỉnh dự toán. */
export interface ReviseEstimateInput {
  newAmount: number
  effectiveDate: string
  basis: string | null
  reason: string
}

/** Danh sách công trình của công ty đang làm việc (không kèm lịch sử dự toán). */
export function useConstructionProjects() {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['construction-projects', companyId],
    queryFn: () => apiFetch<ConstructionProject[]>('/api/construction-projects'),
  })
}

/** Công trình để chọn trên phiếu – khớp ConstructionProjectOptionDto. */
export interface ConstructionProjectOption {
  id: string
  code: string
  name: string
  departmentId: string
  estimateAmount: number
  status: ConstructionProjectStatus
}

/**
 * Công trình đang thực hiện để chọn trên đề nghị mua loại A (lát Y3). Mở cho người xem/lập đề nghị mua.
 * @param enabled Chỉ tải khi form cần (loại A).
 */
export function useConstructionProjectOptions(enabled: boolean) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['construction-projects', companyId, 'options'],
    queryFn: () => apiFetch<ConstructionProjectOption[]>('/api/construction-projects/options'),
    enabled,
  })
}

/** Chi tiết công trình kèm lịch sử dự toán. */
export function useConstructionProject(id: string | undefined) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['construction-projects', companyId, id],
    queryFn: () => apiFetch<ConstructionProject>(`/api/construction-projects/${id}`),
    enabled: !!id,
  })
}

/** Tạo, sửa công trình, điều chỉnh dự toán. Xong thì tải lại danh sách và chi tiết. */
export function useConstructionProjectMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['construction-projects'] })
  return {
    create: useMutation({
      mutationFn: (input: CreateConstructionProjectInput) =>
        apiFetch<ConstructionProject>('/api/construction-projects', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: UpdateConstructionProjectInput }) =>
        apiFetch<ConstructionProject>(`/api/construction-projects/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    revise: useMutation({
      mutationFn: ({ id, input }: { id: string; input: ReviseEstimateInput }) =>
        apiFetch<ConstructionProject>(`/api/construction-projects/${id}/estimate-revisions`, { method: 'POST', body: input }),
      onSuccess,
    }),
  }
}
