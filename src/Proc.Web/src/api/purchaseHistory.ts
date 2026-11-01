import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import type { ExcelImportSource, ImportOutcome } from '../components/ListPage/excelImport'
import { apiFetch } from './client'

/*
 * Dữ liệu mua lịch sử (khớp Proc.Application.Analysis – PurchaseHistory.cs, design.md mục 18.1).
 */

const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/** Một dòng dữ liệu mua lịch sử – khớp PurchaseHistoryRecordDto. */
export interface PurchaseHistoryRow {
  id: string
  batchId: string
  rowNumber: number
  department: string | null
  itemCode: string
  itemName: string
  specification: string | null
  brand: string | null
  origin: string | null
  standard: string | null
  uom: string
  requestDate: string | null
  purchaseDate: string
  quantity: number
  unitPrice: number
  vatPercent: number | null
  currency: string
  amount: number
  relatedCost: number | null
  relatedCostNote: string | null
  supplierName: string
  supplierTaxCode: string | null
  supplierContact: string | null
  paymentTerms: string | null
  discount: string | null
  sponsorship: string | null
  onTime: boolean | null
  qualityPassed: boolean | null
  complaint: string | null
  itemId: string | null
  matchedItemCode: string | null
  supplierId: string | null
  matchedSupplierCode: string | null
}

/** Kết quả xem – khớp PurchaseHistoryListResult. */
export interface PurchaseHistoryList {
  rows: PurchaseHistoryRow[]
  truncated: boolean
  limit: number
}

/** Một lô nhập – khớp PurchaseHistoryBatchDto. */
export interface PurchaseHistoryBatch {
  id: string
  fileName: string
  recordCount: number
  fromDate: string
  toDate: string
  importedAt: string
  importedBy: string | null
}

/** Kết quả nhập file – khớp PurchaseHistoryImportResult. */
export interface PurchaseHistoryImportResult extends ImportOutcome {
  validRows: number
  matchedItems: number
  matchedSuppliers: number
  batchId: string | null
}

/** Bộ lọc danh sách; chuỗi rỗng = không lọc. */
export interface PurchaseHistoryFilter {
  batchId: string
  from: string
  to: string
  /** '' | 'matched' | 'unmatched'. */
  match: string
}

/**
 * Dữ liệu mua lịch sử của công ty đang làm việc theo bộ lọc (tối đa 5.000 dòng, ngày mua mới nhất trước).
 * @param filter Lô, khoảng ngày mua, khớp danh mục.
 */
export function usePurchaseHistory(filter: PurchaseHistoryFilter) {
  const companyId = useCompanyId()
  const params = new URLSearchParams(Object.entries(filter).filter(([, v]) => v !== ''))
  const query = params.toString()
  return useQuery({
    queryKey: ['purchase-history', companyId, 'rows', query],
    queryFn: () => apiFetch<PurchaseHistoryList>(`/api/purchase-history?${query}`),
    placeholderData: keepPreviousData,
  })
}

/** Các lô đã nhập của công ty đang làm việc, mới nhất trước. */
export function usePurchaseHistoryBatches() {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['purchase-history', companyId, 'batches'],
    queryFn: () => apiFetch<PurchaseHistoryBatch[]>('/api/purchase-history/batches'),
  })
}

/** Xóa cả một lô nhập nhầm. */
export function useDeletePurchaseHistoryBatch() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => apiFetch<void>(`/api/purchase-history/batches/${id}`, { method: 'DELETE' }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['purchase-history'] }),
  })
}

/** Nguồn nhập Excel cho hộp thoại "Nhập Excel": mỗi lần nhập là một lô mới, không có "cập nhật". */
export const purchaseHistoryImportSource: ExcelImportSource<PurchaseHistoryImportResult> = {
  importUrl: '/api/purchase-history/import',
  templateUrl: '/api/purchase-history/template',
  templateFileName: 'mau-du-lieu-mua-lich-su.xlsx',
  hint:
    'Mỗi lần nhập là một lô mới của công ty đang làm việc (tối đa 50.000 dòng, 20 MB); nhập nhầm thì xóa cả lô. Mã vật tư trùng mã hàng ' +
    'chung, MST trùng NCC thì tự gán; không khớp vẫn nhập. Bấm "Kiểm tra thử" trước – "Nhập thật" chỉ lưu khi mọi dòng đều đúng.',
  summary: (r) => {
    const matched = `khớp mặt hàng ${r.matchedItems.toLocaleString('vi-VN')} dòng, khớp NCC ${r.matchedSuppliers.toLocaleString('vi-VN')} dòng`
    return {
      valid: `File hợp lệ: ${r.validRows.toLocaleString('vi-VN')} dòng (${matched}). Bấm "Nhập thật" để lưu.`,
      committed: `Đã nhập xong ${r.validRows.toLocaleString('vi-VN')} dòng (${matched}).`,
    }
  },
}

/*
 * Ánh xạ dữ liệu mua lịch sử (khớp Proc.Application.Analysis – PurchaseHistoryMapping.cs, design.md mục 18.2).
 */

/** Loại ánh xạ: mã vật tư cũ → mã hàng chung, hoặc NCC cũ → NCC trong danh mục. */
export type PurchaseHistoryMappingKind = 'item' | 'supplier'

/** Một mã cũ / NCC cũ – khớp PurchaseHistorySourceDto. */
export interface PurchaseHistorySource {
  key: string
  code: string | null
  name: string
  detail: string | null
  recordCount: number
  matchedCount: number
  amount: number
  lastPurchaseDate: string
  targetId: string | null
  targetCode: string | null
  targetName: string | null
  hasMapping: boolean
}

/** Một gợi ý đích ánh xạ – khớp PurchaseHistoryCandidateDto. */
export interface PurchaseHistoryCandidate {
  id: string
  code: string
  name: string
  detail: string | null
  /** Điểm giống tên 0–100. */
  score: number
}

/**
 * Các mã cũ / NCC cũ trên dữ liệu mua lịch sử của công ty đang làm việc, nhiều tiền trước.
 * @param kind Loại ánh xạ.
 */
export function usePurchaseHistorySources(kind: PurchaseHistoryMappingKind) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['purchase-history', companyId, 'sources', kind],
    queryFn: () => apiFetch<PurchaseHistorySource[]>(`/api/purchase-history/sources?kind=${kind}`),
    placeholderData: keepPreviousData,
  })
}

/**
 * Gợi ý đích ánh xạ cho một nguồn: chưa gõ từ khóa thì 5 tên giống nhất, có từ khóa thì tối đa 20 kết quả khớp.
 * @param kind Loại ánh xạ.
 * @param key Khóa nguồn; null thì không tải.
 * @param search Từ khóa (có dấu hoặc không dấu).
 */
export function usePurchaseHistoryCandidates(kind: PurchaseHistoryMappingKind, key: string | null, search: string) {
  const companyId = useCompanyId()
  const params = new URLSearchParams({ kind, key: key ?? '', search })
  return useQuery({
    queryKey: ['purchase-history', companyId, 'candidates', params.toString()],
    queryFn: () => apiFetch<PurchaseHistoryCandidate[]>(`/api/purchase-history/candidates?${params}`),
    enabled: key != null,
    placeholderData: keepPreviousData,
  })
}

/** Lưu / bỏ ánh xạ; xong thì tải lại mọi danh sách dữ liệu mua lịch sử (dòng đã được gán lại). */
export function usePurchaseHistoryMapping() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['purchase-history'] })
  return {
    save: useMutation({
      mutationFn: (input: { kind: PurchaseHistoryMappingKind; key: string; targetId: string }) =>
        apiFetch<{ updatedRecords: number }>('/api/purchase-history/mappings', { method: 'PUT', body: input }),
      onSuccess,
    }),
    remove: useMutation({
      mutationFn: ({ kind, key }: { kind: PurchaseHistoryMappingKind; key: string }) =>
        apiFetch<{ updatedRecords: number }>(`/api/purchase-history/mappings?${new URLSearchParams({ kind, key })}`, { method: 'DELETE' }),
      onSuccess,
    }),
  }
}

/** Số liệu một pháp nhân cho một mặt hàng × ĐVT – khớp PurchaseAnalysisCompanyDto. */
export interface PurchaseAnalysisCompany {
  companyId: string
  companyCode: string
  companyName: string
  quantity: number
  amount: number
  avgPrice: number
  minPrice: number
  maxPrice: number
  lastPrice: number
  lastDate: string
  purchaseCount: number
  supplierCount: number
  savings: number
}

/** Một dòng phân tích hợp nhất (mặt hàng chung × ĐVT) – khớp PurchaseAnalysisRowDto. */
export interface PurchaseAnalysisRow {
  itemId: string
  itemCode: string
  itemName: string
  uom: string
  companyCount: number
  quantity: number
  amount: number
  avgPrice: number
  minPrice: number
  maxPrice: number
  lastDate: string
  purchaseCount: number
  supplierCount: number
  priceGapPct: number
  targetPrice: number
  savings: number
  savingsPct: number
  bestSupplierName: string
  bestSupplierPrice: number
  companies: PurchaseAnalysisCompany[]
}

/** Kết quả phân tích giá hợp nhất – khớp PurchaseAnalysisDto. */
export interface PurchaseAnalysis {
  from: string
  to: string
  targetDiscountPct: number
  allCompanies: boolean
  companyCount: number
  amount: number
  savings: number
  savingsPct: number
  unmappedRecords: number
  unmappedAmount: number
  foreignCurrencyRecords: number
  zeroPriceRecords: number
  rows: PurchaseAnalysisRow[]
}

/** Điều kiện phân tích: khoảng ngày mua (yyyy-MM-dd), % giảm mục tiêu, toàn tập đoàn. */
export interface PurchaseAnalysisFilter {
  from: string
  to: string
  targetDiscountPct: number
  allCompanies: boolean
}

/**
 * Phân tích giá hợp nhất và tiết kiệm dự kiến (design.md mục 18.3). Khóa truy vấn nằm dưới 'purchase-history' nên lưu/bỏ ánh xạ hay nhập
 * lô mới thì tự tải lại.
 * @param filter Điều kiện phân tích.
 */
export function usePurchaseAnalysis(filter: PurchaseAnalysisFilter) {
  const companyId = useCompanyId()
  const params = new URLSearchParams({
    from: filter.from,
    to: filter.to,
    targetDiscountPct: String(filter.targetDiscountPct),
    allCompanies: String(filter.allCompanies),
  })
  return useQuery({
    queryKey: ['purchase-history', companyId, 'analysis', params.toString()],
    queryFn: () => apiFetch<PurchaseAnalysis>(`/api/purchase-history/analysis?${params}`),
    placeholderData: keepPreviousData,
  })
}
