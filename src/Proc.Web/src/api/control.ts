import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Kiểm soát nội bộ – KSNB (design.md mục 18.19): dấu hiệu bất thường, sổ vi phạm. Khớp Proc.Application.Control.
 */

const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/** Loại dấu hiệu bất thường – khớp AnomalyCodes. */
export type AnomalyCode =
  | 'HighPriceRepeat'
  | 'NearThreshold'
  | 'BankAccountChange'
  | 'OffContract'
  | 'InvoiceShortage'
  | 'BackdatedOrder'
  | 'ConflictOfInterest'
  | 'UnrecordedRebate'

/** Nhãn loại dấu hiệu. */
export const anomalyLabels: Record<AnomalyCode, string> = {
  HighPriceRepeat: 'Giá cao lặp lại một NCC',
  NearThreshold: 'PO sát dưới ngưỡng duyệt',
  BankAccountChange: 'Đổi tài khoản ngân hàng NCC',
  OffContract: 'Mua ngoài hợp đồng cao',
  InvoiceShortage: 'Hóa đơn tính cả hàng thiếu',
  BackdatedOrder: 'Hàng/hóa đơn trước PO',
  ConflictOfInterest: 'Xung đột lợi ích',
  UnrecordedRebate: 'Chiết khấu/tài trợ chưa ghi nhận',
}

/** Mức dấu hiệu. */
export type AnomalySeverity = 'High' | 'Medium'

/** Một dấu hiệu bất thường. */
export interface Anomaly {
  code: AnomalyCode
  severity: AnomalySeverity
  companyId: string
  companyCode: string
  date: string
  /** Màn hình mở chứng từ liên quan: PO, GRN, SUPAPR, PRCW. */
  screenCode: string | null
  targetId: string | null
  refNo: string | null
  supplierCode: string | null
  supplierName: string | null
  userName: string | null
  amount: number | null
  detail: string
}

/** Tổng hợp theo loại dấu hiệu. */
export interface AnomalySummary {
  code: AnomalyCode
  count: number
  measured: boolean
  note: string
}

/** Kết quả rà dấu hiệu bất thường. */
export interface AnomalyResult {
  from: string
  to: string
  allCompanies: boolean
  summary: AnomalySummary[]
  items: Anomaly[]
}

/** Điều kiện rà: khoảng ngày, phạm vi, ngưỡng. */
export interface AnomalyQuery {
  from: string
  to: string
  allCompanies: boolean
  priceGapPct: number
  nearThresholdPct: number
  nearThresholdCount: number
  offContractPct: number
}

/**
 * Rà dấu hiệu bất thường (quyền KSNB; toàn tập đoàn cần thêm quyền xem báo cáo mua hàng toàn tập đoàn).
 * @param query Điều kiện rà.
 */
export function useAnomalies(query: AnomalyQuery) {
  const companyId = useCompanyId()
  const params = new URLSearchParams(Object.entries(query).map(([k, v]) => [k, String(v)])).toString()
  return useQuery({
    queryKey: ['control', companyId, 'anomalies', params],
    queryFn: () => apiFetch<AnomalyResult>(`/api/control/anomalies?${params}`),
    placeholderData: keepPreviousData,
  })
}

/** Mức vi phạm – khớp FindingSeverity. */
export type FindingSeverity = 'Normal' | 'Serious'

/** Nhãn mức vi phạm. */
export const findingSeverityLabels: Record<FindingSeverity, string> = { Normal: 'Thường', Serious: 'Nghiêm trọng' }

/** Vi phạm KSNB – khớp ControlFindingDto. */
export interface ControlFinding {
  id: string
  no: string
  date: string
  severity: FindingSeverity
  status: 'Open' | 'Concluded'
  anomalyCode: AnomalyCode | null
  title: string
  description: string
  refScreen: string | null
  refId: string | null
  refNo: string | null
  supplierId: string | null
  supplierCode: string | null
  supplierName: string | null
  conclusion: string | null
  concludedAt: string | null
  concludedByName: string | null
  createdAt: string
  createdByName: string | null
}

/** Ghi nhận/sửa vi phạm; loại dấu hiệu và chứng từ liên quan chỉ dùng khi ghi nhận. */
export interface ControlFindingInput {
  date: string
  severity: FindingSeverity
  title: string
  description: string
  supplierId: string | null
  anomalyCode?: AnomalyCode | null
  refScreen?: string | null
  refId?: string | null
  refNo?: string | null
}

/** Trạng thái điều hướng khi mở sổ vi phạm từ màn hình dấu hiệu bất thường: điền sẵn phiếu ghi nhận mới. */
export interface FindingPrefillState {
  findingPrefill: ControlFindingInput & { supplierCode?: string | null; supplierName?: string | null }
}

/** Sổ vi phạm của công ty đang làm việc. */
export function useControlFindings() {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['control', companyId, 'findings'],
    queryFn: () => apiFetch<ControlFinding[]>('/api/control/findings'),
  })
}

/**
 * Chi tiết một vi phạm.
 * @param id Id; null thì không tải.
 */
export function useControlFinding(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['control', companyId, 'findings', id],
    queryFn: () => apiFetch<ControlFinding>(`/api/control/findings/${id}`),
    enabled: !!id,
  })
}

/** Ghi nhận, sửa, kết luận vi phạm. Xong thì tải lại sổ (và bảng KPI – vi phạm nghiêm trọng). */
export function useControlFindingMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['control'] })
    void queryClient.invalidateQueries({ queryKey: ['reports'] })
  }
  return {
    create: useMutation({
      mutationFn: (input: ControlFindingInput) => apiFetch<ControlFinding>('/api/control/findings', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: ControlFindingInput }) =>
        apiFetch<ControlFinding>(`/api/control/findings/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    conclude: useMutation({
      mutationFn: ({ id, conclusion, severity }: { id: string; conclusion: string; severity: FindingSeverity }) =>
        apiFetch<ControlFinding>(`/api/control/findings/${id}/conclude`, { method: 'POST', body: { conclusion, severity } }),
      onSuccess,
    }),
  }
}
