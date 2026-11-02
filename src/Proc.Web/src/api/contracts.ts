import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import type { ExcelImportSource, ImportOutcome } from '../components/ListPage/excelImport'
import { apiFetch } from './client'
import type { ApprovalStep } from './purchasing'

/*
 * Kiểu dữ liệu và hàm gọi API hợp đồng nguyên tắc (khớp Proc.Application.Purchasing.Contracts – lát B4a, design.md mục 18.7).
 */

/** Trạng thái hợp đồng nguyên tắc. */
export type ContractStatus = 'Draft' | 'Submitted' | 'Active' | 'Terminated' | 'Cancelled'

/** Nhãn trạng thái hợp đồng. */
export const contractStatusLabels: Record<ContractStatus, string> = {
  Draft: 'Nháp',
  Submitted: 'Chờ duyệt',
  Active: 'Hiệu lực',
  Terminated: 'Đã chấm dứt',
  Cancelled: 'Đã hủy',
}

/** Tông màu trạng thái hợp đồng. */
export const contractStatusTone: Record<ContractStatus, 'success' | 'warning' | 'neutral'> = {
  Draft: 'neutral',
  Submitted: 'warning',
  Active: 'success',
  Terminated: 'neutral',
  Cancelled: 'neutral',
}

/** Một dòng danh sách hợp đồng. */
export interface ContractListItem {
  id: string
  no: string
  contractNo: string | null
  title: string
  supplierCode: string
  supplierName: string
  validFrom: string
  validTo: string
  expectedAmount: number
  status: ContractStatus
  /** Đang hiệu lực và còn ≤ 30 ngày tới ngày hết hạn. */
  expiringSoon: boolean
  createdByName: string | null
}

/** Sản lượng dự kiến một mặt hàng; giá theo bảng giá gắn với hợp đồng. */
export interface ContractLine {
  id: string
  lineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  expectedQty: number
  minCommitQty: number | null
  unitPrice: number
  discountPct: number
  expectedAmount: number
  note: string | null
}

/** Chi tiết hợp đồng nguyên tắc. */
export interface Contract {
  id: string
  no: string
  companyCode: string
  supplierId: string
  supplierCode: string
  supplierName: string
  priceListId: string
  priceListNo: string
  priceListTitle: string
  priceListStatus: string
  contractNo: string | null
  title: string
  signDate: string | null
  validFrom: string
  validTo: string
  priceAdjustmentTerms: string | null
  paymentTerms: string | null
  creditLimit: number | null
  deliveryTerms: string | null
  discountTerms: string | null
  sponsorshipTerms: string | null
  penaltyTerms: string | null
  otherTerms: string | null
  note: string | null
  status: ContractStatus
  expectedAmount: number
  departmentIds: string[]
  departmentNames: string[]
  lines: ContractLine[]
  createdAt: string
  createdByName: string | null
  activatedAt: string | null
  terminatedOn: string | null
  terminationReason: string | null
  rejectReason: string | null
  daysToExpiry: number
  expiringSoon: boolean
  approvals: ApprovalStep[]
  amendments: ContractAmendmentListItem[]
  /** Cam kết chiết khấu doanh số/tài trợ (lát F4b). */
  rebates: ContractRebate[]
  actions: {
    canEdit: boolean
    canSubmit: boolean
    canApprove: boolean
    canRecall: boolean
    canTerminate: boolean
    canCancel: boolean
    /** Lập phụ lục được: hợp đồng hiệu lực, chưa có phụ lục đang mở, có quyền sửa. */
    canAmend: boolean
    readOnlyReason: string | null
  }
}

/** Trạng thái phụ lục hợp đồng (lát B4b). */
export type ContractAmendmentStatus = 'Draft' | 'Submitted' | 'Approved' | 'Cancelled'

/** Nhãn trạng thái phụ lục. */
export const amendmentStatusLabels: Record<ContractAmendmentStatus, string> = {
  Draft: 'Nháp',
  Submitted: 'Chờ duyệt',
  Approved: 'Đã duyệt – đã áp',
  Cancelled: 'Đã hủy',
}

/** Tông màu trạng thái phụ lục. */
export const amendmentStatusTone: Record<ContractAmendmentStatus, 'success' | 'warning' | 'neutral'> = {
  Draft: 'neutral',
  Submitted: 'warning',
  Approved: 'success',
  Cancelled: 'neutral',
}

/** Một phụ lục trong tab Phụ lục của hợp đồng. */
export interface ContractAmendmentListItem {
  id: string
  no: string
  paperNo: string | null
  signDate: string | null
  reason: string
  /** Tóm tắt thay đổi, ví dụ "Gia hạn đến 30/06/2027 (trước: 31/12/2026); Đổi thanh toán". */
  changeSummary: string
  expectedAmount: number
  status: ContractAmendmentStatus
  createdAt: string
  createdByName: string | null
  approvedAt: string | null
}

/** Điều khoản, hạn mức công nợ. */
export interface ContractTerms {
  priceAdjustmentTerms: string | null
  paymentTerms: string | null
  creditLimit: number | null
  deliveryTerms: string | null
  discountTerms: string | null
  sponsorshipTerms: string | null
  penaltyTerms: string | null
  otherTerms: string | null
}

/** Chi tiết phụ lục: terms là điều khoản sau phụ lục, currentTerms của hợp đồng hiện hành. */
export interface ContractAmendment {
  id: string
  no: string
  contractId: string
  contractNo: string
  contractTitle: string
  supplierCode: string
  supplierName: string
  paperNo: string | null
  reason: string
  signDate: string | null
  validFrom: string
  oldValidTo: string
  newValidTo: string
  oldPriceListId: string
  oldPriceListNo: string
  newPriceListId: string
  newPriceListNo: string
  newPriceListTitle: string
  newPriceListStatus: string
  terms: ContractTerms
  currentTerms: ContractTerms
  note: string | null
  currentExpectedAmount: number
  expectedAmount: number
  changeSummary: string
  status: ContractAmendmentStatus
  rejectReason: string | null
  createdAt: string
  createdByName: string | null
  approvedAt: string | null
  approvals: ApprovalStep[]
  actions: { canEdit: boolean; canSubmit: boolean; canApprove: boolean; canRecall: boolean; canCancel: boolean; readOnlyReason: string | null }
}

/** Nội dung phụ lục gửi lên: giá trị của hợp đồng sau phụ lục. */
export interface SaveContractAmendmentInput extends ContractTerms {
  paperNo: string | null
  reason: string
  signDate: string | null
  newValidTo: string
  newPriceListId: string
  note: string | null
}

/** Nội dung hợp đồng gửi lên. */
export interface SaveContractInput {
  supplierId: string
  priceListId: string
  contractNo: string | null
  title: string
  signDate: string | null
  validFrom: string
  validTo: string
  priceAdjustmentTerms: string | null
  paymentTerms: string | null
  creditLimit: number | null
  deliveryTerms: string | null
  discountTerms: string | null
  sponsorshipTerms: string | null
  penaltyTerms: string | null
  otherTerms: string | null
  note: string | null
  departmentIds: string[]
  lines: { id: string | null; itemId: string; uomId: string; expectedQty: number; minCommitQty: number | null; note: string | null }[]
  rebates: SaveContractRebateInput[]
}

/** Loại cam kết chiết khấu/tài trợ (design.md mục 18.21). */
export type RebateKind = 'SalesVolume' | 'Sponsorship' | 'Other'
/** Cách tính: % doanh số hoặc số tiền cố định mỗi kỳ. */
export type RebateMethod = 'Percent' | 'FixedAmount'
/** Kỳ quyết toán. */
export type RebatePeriod = 'Quarter' | 'Year' | 'WholeContract'
/** Hình thức NCC trả. */
export type RebateReceiptForm = 'DebtOffset' | 'Transfer' | 'Goods' | 'Other'
/** Tình trạng một kỳ quyết toán. */
export type RebatePeriodStatus = 'NotDue' | 'Waiting' | 'Received' | 'NotReached' | 'Short'

export const rebateKindLabels: Record<RebateKind, string> = {
  SalesVolume: 'Chiết khấu doanh số',
  Sponsorship: 'Tài trợ',
  Other: 'Chiết khấu/hỗ trợ khác',
}

export const rebateMethodLabels: Record<RebateMethod, string> = {
  Percent: '% doanh số',
  FixedAmount: 'Số tiền mỗi kỳ',
}

export const rebatePeriodLabels: Record<RebatePeriod, string> = {
  Quarter: 'Quý',
  Year: 'Năm',
  WholeContract: 'Cả hợp đồng',
}

export const rebateReceiptFormLabels: Record<RebateReceiptForm, string> = {
  DebtOffset: 'Giảm trừ công nợ',
  Transfer: 'Chuyển khoản',
  Goods: 'Hàng (quy ra tiền)',
  Other: 'Khác',
}

export const rebatePeriodStatusLabels: Record<RebatePeriodStatus, string> = {
  NotDue: 'Chưa hết kỳ',
  Waiting: 'Chờ ghi nhận',
  Received: 'Đủ',
  NotReached: 'Không phải nhận',
  Short: 'Thiếu',
}

export const rebatePeriodStatusTone: Record<RebatePeriodStatus, 'success' | 'warning' | 'error' | 'neutral'> = {
  NotDue: 'neutral',
  Waiting: 'warning',
  Received: 'success',
  NotReached: 'neutral',
  Short: 'error',
}

/** Cam kết chiết khấu/tài trợ: value là % (Percent) hoặc số tiền mỗi kỳ (FixedAmount). */
export interface ContractRebate {
  id: string
  lineNo: number
  kind: RebateKind
  method: RebateMethod
  value: number
  minPurchaseAmount: number | null
  period: RebatePeriod
  description: string
}

/** Lưu cam kết (id null = mới). */
export type SaveContractRebateInput = Omit<ContractRebate, 'id' | 'lineNo'> & { id: string | null }

/** Một kỳ quyết toán: phải nhận, đã nhận, còn thiếu. */
export interface ContractRebatePeriodRow {
  rebateId: string
  rebateLineNo: number
  kind: RebateKind
  description: string
  periodFrom: string
  periodTo: string
  dueDate: string
  purchaseAmount: number
  expectedAmount: number
  receivedAmount: number
  shortAmount: number
  status: RebatePeriodStatus
}

/** Một lần ghi nhận thực nhận. */
export interface ContractRebateReceipt {
  id: string
  rebateId: string
  rebateLineNo: number
  periodFrom: string
  receivedOn: string
  amount: number
  form: RebateReceiptForm
  refNo: string | null
  note: string | null
  isCancelled: boolean
  cancelReason: string | null
  createdAt: string
  createdByName: string | null
}

/** Theo dõi chiết khấu/tài trợ của hợp đồng. */
export interface ContractRebateTracking {
  graceDays: number
  periods: ContractRebatePeriodRow[]
  receipts: ContractRebateReceipt[]
  canRecord: boolean
}

/** Ghi nhận thực nhận. */
export interface SaveRebateReceiptInput {
  rebateId: string
  periodFrom: string
  receivedOn: string
  amount: number
  form: RebateReceiptForm
  refNo: string | null
  note: string | null
}

const useCompanyId = () => useAuth().workingCompany?.id

/** Danh sách hợp đồng nguyên tắc của công ty đang làm việc. */
export function useContracts() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['contracts', companyId], queryFn: () => apiFetch<ContractListItem[]>('/api/contracts') })
}

/** Chi tiết hợp đồng; null thì không tải (đang lập mới). */
export function useContract(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['contracts', companyId, id], queryFn: () => apiFetch<Contract>(`/api/contracts/${id}`), enabled: !!id })
}

/** Lập, sửa, gửi duyệt, duyệt, từ chối, rút, chấm dứt, hủy hợp đồng. */
export function useContractMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['contracts'] })
  return {
    create: useMutation({ mutationFn: (input: SaveContractInput) => apiFetch<Contract>('/api/contracts', { method: 'POST', body: input }), onSuccess }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveContractInput }) => apiFetch<Contract>(`/api/contracts/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    submit: useMutation({ mutationFn: (id: string) => apiFetch<Contract>(`/api/contracts/${id}/submit`, { method: 'POST' }), onSuccess }),
    recall: useMutation({ mutationFn: (id: string) => apiFetch<Contract>(`/api/contracts/${id}/recall`, { method: 'POST' }), onSuccess }),
    cancel: useMutation({ mutationFn: (id: string) => apiFetch<Contract>(`/api/contracts/${id}/cancel`, { method: 'POST' }), onSuccess }),
    approve: useMutation({
      mutationFn: ({ id, comment }: { id: string; comment?: string | null }) =>
        apiFetch<Contract>(`/api/contracts/${id}/approve`, { method: 'POST', body: { comment: comment ?? null } }),
      onSuccess,
    }),
    reject: useMutation({
      mutationFn: ({ id, comment }: { id: string; comment?: string | null }) =>
        apiFetch<Contract>(`/api/contracts/${id}/reject`, { method: 'POST', body: { comment: comment ?? null } }),
      onSuccess,
    }),
    terminate: useMutation({
      mutationFn: ({ id, date, reason }: { id: string; date: string; reason: string }) =>
        apiFetch<Contract>(`/api/contracts/${id}/terminate`, { method: 'POST', body: { date, reason } }),
      onSuccess,
    }),
  }
}

/** Chi tiết phụ lục; null thì không tải (đang lập mới). */
export function useContractAmendment(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['contracts', companyId, 'amendment', id],
    queryFn: () => apiFetch<ContractAmendment>(`/api/contract-amendments/${id}`),
    enabled: !!id,
  })
}

/** Lập, sửa, gửi duyệt, duyệt, từ chối, rút, hủy phụ lục; xong thì tải lại hợp đồng (phụ lục duyệt xong đổi hợp đồng). */
export function useContractAmendmentMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['contracts'] })
  const post = (id: string, name: string) => apiFetch<ContractAmendment>(`/api/contract-amendments/${id}/${name}`, { method: 'POST' })
  const decide = (id: string, name: string, comment?: string | null) =>
    apiFetch<ContractAmendment>(`/api/contract-amendments/${id}/${name}`, { method: 'POST', body: { comment: comment ?? null } })
  return {
    create: useMutation({
      mutationFn: ({ contractId, input }: { contractId: string; input: SaveContractAmendmentInput }) =>
        apiFetch<ContractAmendment>(`/api/contracts/${contractId}/amendments`, { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveContractAmendmentInput }) =>
        apiFetch<ContractAmendment>(`/api/contract-amendments/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    submit: useMutation({ mutationFn: (id: string) => post(id, 'submit'), onSuccess }),
    recall: useMutation({ mutationFn: (id: string) => post(id, 'recall'), onSuccess }),
    cancel: useMutation({ mutationFn: (id: string) => post(id, 'cancel'), onSuccess }),
    approve: useMutation({ mutationFn: ({ id, comment }: { id: string; comment?: string | null }) => decide(id, 'approve', comment), onSuccess }),
    reject: useMutation({ mutationFn: ({ id, comment }: { id: string; comment?: string | null }) => decide(id, 'reject', comment), onSuccess }),
  }
}

/** Kết quả nhập Excel hợp đồng hiện hữu – khớp ContractImportResult. */
export interface ContractImportResult extends ImportOutcome {
  contracts: number
  lines: number
}

/**
 * Nguồn nhập Excel hợp đồng hiện hữu (lát B4c, design.md mục 18.7): mỗi dòng một mặt hàng × ĐVT, các dòng cùng số HĐ giấy là một hợp
 * đồng; nhập thành hợp đồng nháp của công ty đang làm việc, không cập nhật hợp đồng đã có.
 */
export const contractImportSource: ExcelImportSource<ContractImportResult> = {
  importUrl: '/api/contracts/import',
  templateUrl: '/api/contracts/import-template',
  templateFileName: 'mau-hop-dong-nguyen-tac.xlsx',
  hint:
    'Mỗi dòng một mặt hàng × ĐVT; các dòng cùng Số HĐ giấy là một hợp đồng. Hợp đồng nhập vào ở trạng thái Nháp của công ty đang làm ' +
    'việc – kiểm tra rồi gửi duyệt như lập tay. Số HĐ giấy đã có với cùng NCC thì báo lỗi, không nhập lại. Bấm "Kiểm tra thử" trước – ' +
    '"Nhập thật" chỉ lưu khi mọi hợp đồng đều đúng.',
  summary: (r) => ({
    valid: `File hợp lệ: ${r.contracts.toLocaleString('vi-VN')} hợp đồng, ${r.lines.toLocaleString('vi-VN')} dòng sản lượng. Bấm "Nhập thật" để lưu.`,
    committed: `Đã nhập ${r.contracts.toLocaleString('vi-VN')} hợp đồng nháp, ${r.lines.toLocaleString('vi-VN')} dòng sản lượng.`,
  }),
}

/** Theo dõi chiết khấu/tài trợ theo kỳ của hợp đồng (lát F4b); chỉ tải khi hợp đồng đã hiệu lực. */
export function useContractRebates(contractId: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['contracts', companyId, contractId, 'rebates'],
    queryFn: () => apiFetch<ContractRebateTracking>(`/api/contracts/${contractId}/rebates`),
    enabled: !!contractId,
  })
}

/** Ghi nhận và hủy thực nhận chiết khấu/tài trợ. */
export function useContractRebateMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['contracts'] })
  return {
    record: useMutation({
      mutationFn: ({ contractId, input }: { contractId: string; input: SaveRebateReceiptInput }) =>
        apiFetch<ContractRebateTracking>(`/api/contracts/${contractId}/rebate-receipts`, { method: 'POST', body: input }),
      onSuccess,
    }),
    cancel: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) =>
        apiFetch<ContractRebateTracking>(`/api/contract-rebate-receipts/${id}/cancel`, { method: 'POST', body: { reason } }),
      onSuccess,
    }),
  }
}
