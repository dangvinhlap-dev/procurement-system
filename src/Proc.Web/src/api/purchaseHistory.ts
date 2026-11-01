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
