import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { ExcelImportSource, ImportOutcome } from '../components/ListPage/excelImport'
import type { ApprovalStep } from './purchasing'

/*
 * Kiểu dữ liệu và hàm gọi API bảng giá NCC, so sánh giá (khớp Proc.Application.Pricing – SupplierPrices.cs).
 */

const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/** Trạng thái bảng giá NCC: Nháp → Chờ duyệt → Hiệu lực → Ngừng (lát B1 – design.md mục 8.5). */
export type PriceListStatus = 'Draft' | 'Submitted' | 'Active' | 'Inactive' | 'Cancelled'

/** Nhãn trạng thái bảng giá. */
/** Nhãn trạng thái bảng giá; Đã hủy – bảng giá nháp hủy theo dây chuyền RFQ (lát Z4c, design.md mục 20.2 E2). */
export const priceListStatusLabels: Record<PriceListStatus, string> = {
  Draft: 'Nháp', Submitted: 'Chờ duyệt', Active: 'Hiệu lực', Inactive: 'Ngừng', Cancelled: 'Đã hủy',
}

/** Tông màu trạng thái bảng giá (cùng cách với PR/PO: nháp xám, chờ duyệt vàng). */
export const priceListStatusTone: Record<PriceListStatus, 'success' | 'warning' | 'neutral'> = {
  Draft: 'neutral',
  Submitted: 'warning',
  Active: 'success',
  Inactive: 'neutral',
  Cancelled: 'neutral',
}

/** Một dòng danh sách bảng giá. */
export interface PriceListListItem {
  id: string
  no: string
  title: string
  companyCode: string
  isGroupWide: boolean
  supplierCode: string
  supplierName: string
  validFrom: string
  validTo: string | null
  status: PriceListStatus
  lineCount: number
  isOwn: boolean
  /** Đang hiệu lực và còn ≤ 30 ngày tới ngày hết hạn. */
  expiringSoon: boolean
}

/** Một dòng bảng giá. */
export interface PriceListLine {
  id: string
  lineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  uomFactor: number
  minQty: number
  unitPrice: number
  discountPct: number
  vatCode: string | null
  baseNetPrice: number
  note: string | null
}

/** Chi tiết bảng giá. */
export interface PriceList {
  id: string
  no: string
  title: string
  companyId: string
  companyCode: string
  isGroupWide: boolean
  supplierId: string
  supplierCode: string
  supplierName: string
  validFrom: string
  validTo: string | null
  currencyCode: string
  deliveryTerms: string | null
  note: string | null
  status: PriceListStatus
  createdAt: string
  createdByName: string | null
  activatedAt: string | null
  lines: PriceListLine[]
  actions: {
    canEdit: boolean
    canSubmit: boolean
    canApprove: boolean
    canRecall: boolean
    canDeactivate: boolean
    readOnlyReason: string | null
  }
  /** Lý do từ chối gần nhất. */
  rejectReason: string | null
  /** Số ngày còn tới ngày hết hiệu lực (null = không hạn). */
  daysToExpiry: number | null
  /** Đang hiệu lực và còn ≤ 30 ngày. */
  expiringSoon: boolean
  /** Lịch sử duyệt (công ty khác xem bảng toàn tập đoàn thì rỗng). */
  approvals: ApprovalStep[]
}

/** Nội dung bảng giá gửi lên. */
export interface SavePriceListInput {
  supplierId: string
  title: string
  isGroupWide: boolean
  validFrom: string
  validTo: string | null
  deliveryTerms: string | null
  note: string | null
  lines: { id: string | null; itemId: string; uomId: string; minQty: number; unitPrice: number; discountPct: number; vatCode: string | null; note: string | null }[]
}

/** Một bậc giá gợi ý cho PO. */
export interface ResolvedSupplierPrice {
  itemId: string
  uomId: string
  uomFactor: number
  minQty: number
  unitPrice: number
  discountPct: number
  vatCode: string | null
  priceListNo: string
}

/**
 * Bậc giá đã duyệt cho PO: giá NCC chính/dự phòng của phiếu duyệt tháng (MonthlyApproval – priceListNo là số phiếu GTH, đứng đầu), bảng giá
 * của hợp đồng nguyên tắc hiệu lực (Contract) hoặc bảng giá NCC hiệu lực (PriceList).
 */
export interface ApprovedPoPrice extends ResolvedSupplierPrice {
  source: 'MonthlyApproval' | 'Contract' | 'PriceList' | 'Internal'
  contractNo: string | null
}

/** Danh sách bảng giá (của công ty và toàn tập đoàn). */
export function usePriceLists() {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['price-lists', companyId], queryFn: () => apiFetch<PriceListListItem[]>('/api/price-lists') })
}

/** Chi tiết bảng giá; null thì không tải (đang lập mới). */
export function usePriceList(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({ queryKey: ['price-lists', companyId, id], queryFn: () => apiFetch<PriceList>(`/api/price-lists/${id}`), enabled: !!id })
}

/** Lập, sửa, gửi duyệt, duyệt, từ chối, rút về nháp, ngừng bảng giá. */
export function usePriceListMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['price-lists'] })
  return {
    create: useMutation({ mutationFn: (input: SavePriceListInput) => apiFetch<PriceList>('/api/price-lists', { method: 'POST', body: input }), onSuccess }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SavePriceListInput }) => apiFetch<PriceList>(`/api/price-lists/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    submit: useMutation({ mutationFn: ({ id }: { id: string }) => apiFetch<PriceList>(`/api/price-lists/${id}/submit`, { method: 'POST' }), onSuccess }),
    recall: useMutation({ mutationFn: ({ id }: { id: string }) => apiFetch<PriceList>(`/api/price-lists/${id}/recall`, { method: 'POST' }), onSuccess }),
    approve: useMutation({
      mutationFn: ({ id, comment }: { id: string; comment?: string | null }) =>
        apiFetch<PriceList>(`/api/price-lists/${id}/approve`, { method: 'POST', body: { comment: comment ?? null } }),
      onSuccess,
    }),
    reject: useMutation({
      mutationFn: ({ id, comment }: { id: string; comment?: string | null }) =>
        apiFetch<PriceList>(`/api/price-lists/${id}/reject`, { method: 'POST', body: { comment: comment ?? null } }),
      onSuccess,
    }),
    deactivate: useMutation({ mutationFn: (id: string) => apiFetch<PriceList>(`/api/price-lists/${id}/deactivate`, { method: 'POST' }), onSuccess }),
  }
}

/**
 * Bậc giá đã duyệt của NCC vào ngày đặt hàng (PO – design.md mục 18.10): bảng giá của hợp đồng nguyên tắc hiệu lực ưu tiên, rồi bảng giá
 * NCC hiệu lực.
 * @param supplierId NCC; trống thì không tải.
 * @param date Ngày yyyy-MM-dd.
 * @param itemIds Mặt hàng trên phiếu.
 * @param departmentId Bộ phận đặt hàng – hợp đồng giới hạn bộ phận thì chỉ áp cho bộ phận được phép.
 */
export function useSupplierPrices(supplierId: string | undefined, date: string | undefined, itemIds: string[], departmentId?: string | null) {
  const companyId = useCompanyId()
  const params = new URLSearchParams({ supplierId: supplierId ?? '', date: date ?? '' })
  if (departmentId) params.append('departmentId', departmentId)
  for (const id of [...new Set(itemIds)].sort()) params.append('itemIds', id)
  const query = params.toString()
  return useQuery({
    queryKey: ['price-lists', companyId, 'approved-prices', query],
    queryFn: () => apiFetch<ApprovedPoPrice[]>(`/api/purchase-orders/approved-prices?${query}`),
    enabled: !!supplierId && !!date && itemIds.length > 0,
    placeholderData: keepPreviousData,
  })
}

/** Giá thấp nhất tập đoàn của một mặt hàng – khớp GroupMinPriceDto (giá sau CK, trước VAT, theo ĐVT cơ sở). */
export interface GroupMinPrice {
  itemId: string
  basePrice: number
  date: string
  source: 'Receipt' | 'PO'
  /** Số chứng từ, công ty, NCC: null khi người xem không có quyền xem giá toàn tập đoàn và giá thuộc công ty khác. */
  documentNo: string | null
  companyCode: string | null
  supplierCode: string | null
}

/** Giá thấp nhất tập đoàn + % cảnh báo của công ty – khớp GroupMinPricesDto. */
export interface GroupMinPrices {
  warnPercent: number
  items: GroupMinPrice[]
}

/**
 * Giá thấp nhất tập đoàn của các mặt hàng trên PO để cảnh báo giá cao (design.md mục 8.5, 10.3).
 * @param itemIds Mặt hàng trên phiếu.
 * @param excludeOrderId PO đang xem – không tính giá của chính nó.
 */
export function useGroupMinPrices(itemIds: string[], excludeOrderId?: string) {
  const companyId = useCompanyId()
  const params = new URLSearchParams()
  if (excludeOrderId) params.append('excludeOrderId', excludeOrderId)
  for (const id of [...new Set(itemIds)].sort()) params.append('itemIds', id)
  const query = params.toString()
  return useQuery({
    queryKey: ['price-comparison', companyId, 'group-min', query],
    queryFn: () => apiFetch<GroupMinPrices>(`/api/purchase-orders/group-min-prices?${query}`),
    enabled: itemIds.length > 0,
    placeholderData: keepPreviousData,
  })
}

/**
 * % giá dòng PO cao hơn giá thấp nhất tập đoàn, làm tròn 2 số lẻ; null khi chưa đủ dữ liệu.
 * Ví dụ giá thấp nhất 80.000/cái, dòng 2 thùng × 1.140.000 − 5% (thùng = 12 cái) → 90.250/cái → cao hơn 12,81%.
 * @param net Giá sau chiết khấu theo ĐVT dòng.
 * @param factor Hệ số ĐVT dòng → ĐVT cơ sở.
 * @param minBase Giá thấp nhất tập đoàn theo ĐVT cơ sở.
 */
export function aboveGroupMinPct(net: number | null | undefined, factor: number, minBase: number | null | undefined): number | null {
  if (net == null || minBase == null || minBase <= 0 || factor <= 0) return null
  return Math.round(((net / factor - minBase) * 100 * 100) / minBase) / 100
}

/**
 * Chọn bậc giá cho một dòng PO: cùng ĐVT, bậc SL lớn nhất ≤ SL đặt. Không có bậc cùng ĐVT thì quy từ bậc theo ĐVT khác
 * (giá cơ sở × hệ số ĐVT dòng), vẫn chọn bậc lớn nhất có SL quy đổi ≤ SL đặt.
 * Ví dụ bậc 0 cái 50.000, bậc 100 cái 48.000: đặt 120 cái → 48.000; đặt 2 thùng 12 cái (không có bậc theo thùng) → 600.000/thùng.
 * @param tiers Các bậc của mặt hàng.
 * @param uomId ĐVT dòng.
 * @param uomFactor Hệ số ĐVT dòng.
 * @param qty SL đặt.
 * @returns Đơn giá, % CK, thuế suất, nguồn gợi ý; undefined nếu không có bậc nào.
 */
export function pickSupplierPrice(tiers: ApprovedPoPrice[], uomId: string, uomFactor: number, qty: number) {
  const same = tiers.filter((t) => t.uomId === uomId && t.minQty <= qty).sort((a, b) => b.minQty - a.minQty)[0]
  if (same) return { unitPrice: same.unitPrice, discountPct: same.discountPct, vatCode: same.vatCode, priceListNo: same.priceListNo, source: same.source, contractNo: same.contractNo }
  const baseQty = qty * uomFactor
  const other = tiers.filter((t) => t.minQty * t.uomFactor <= baseQty).sort((a, b) => b.minQty * b.uomFactor - a.minQty * a.uomFactor)[0]
  if (!other) return undefined
  const unitPrice = Math.round((other.unitPrice / other.uomFactor) * uomFactor * 1_000_000) / 1_000_000
  return { unitPrice, discountPct: other.discountPct, vatCode: other.vatCode, priceListNo: other.priceListNo, source: other.source, contractNo: other.contractNo }
}

/** Giá bảng giá đang hiệu lực khi so sánh. */
export interface ComparePriceListRow {
  companyCode: string
  isGroupWide: boolean
  supplierCode: string
  supplierName: string
  priceListNo: string
  validFrom: string
  validTo: string | null
  uomCode: string
  minQty: number
  unitPrice: number
  discountPct: number
  baseNetPrice: number
}

/** Giá thực mua theo công ty × NCC. */
export interface ComparePurchaseRow {
  companyCode: string
  supplierCode: string
  supplierName: string
  lastDate: string
  lastSource: 'Receipt' | 'PO'
  lastDocumentNo: string
  lastPrice: number
  minPrice: number | null
  maxPrice: number | null
  avgPrice: number | null
  qty12Months: number
  aboveGroupMinPct: number
  isAboveThreshold: boolean
}

/** Một mục lịch sử giá. */
export interface PriceHistoryEntry {
  date: string
  companyCode: string
  supplierCode: string
  source: 'PriceList' | 'PO' | 'Receipt' | 'Quotation'
  documentNo: string
  baseQty: number
  basePrice: number
}

/** Nhãn nguồn giá. */
export const priceSourceLabels: Record<PriceHistoryEntry['source'], string> = {
  PriceList: 'Bảng giá',
  PO: 'Đơn mua hàng',
  Receipt: 'Phiếu nhập',
  Quotation: 'Báo giá trúng thầu',
}

/** Kết quả so sánh giá một mặt hàng. */
export interface PriceComparison {
  itemId: string
  itemCode: string
  itemName: string
  baseUomCode: string
  allCompanies: boolean
  thresholdPct: number
  groupMinPrice: number | null
  groupMaxPrice: number | null
  groupAvgPrice: number | null
  priceLists: ComparePriceListRow[]
  purchases: ComparePurchaseRow[]
  history: PriceHistoryEntry[]
}

/**
 * So sánh giá một mặt hàng.
 * @param itemId Mặt hàng; trống thì không tải.
 * @param allCompanies Toàn tập đoàn (cần quyền).
 * @param thresholdPct Ngưỡng X% tô màu.
 */
export function usePriceComparison(itemId: string | undefined, allCompanies: boolean, thresholdPct: number) {
  const companyId = useCompanyId()
  const query = new URLSearchParams({ itemId: itemId ?? '', allCompanies: String(allCompanies), thresholdPct: String(thresholdPct) }).toString()
  return useQuery({
    queryKey: ['price-comparison', companyId, query],
    queryFn: () => apiFetch<PriceComparison>(`/api/price-comparison?${query}`),
    enabled: !!itemId,
  })
}

/** Kết quả nhập Excel bảng giá NCC – khớp PriceListImportResult. */
export interface PriceListImportResult extends ImportOutcome {
  priceLists: number
  lines: number
}

/**
 * Nguồn nhập Excel bảng giá NCC (design.md mục 8.5): mỗi dòng một bậc giá (mặt hàng × ĐVT × SL từ), các dòng cùng Mã NCC + Tên bảng giá là
 * một bảng giá; nhập thành bảng giá nháp của công ty đang làm việc, không cập nhật bảng giá đã có.
 */
export const priceListImportSource: ExcelImportSource<PriceListImportResult> = {
  importUrl: '/api/price-lists/import',
  templateUrl: '/api/price-lists/import-template',
  templateFileName: 'mau-bang-gia-ncc.xlsx',
  hint:
    'Mỗi dòng một bậc giá (mặt hàng × ĐVT × SL từ); các dòng cùng Mã NCC + Tên bảng giá là một bảng giá. Bảng giá nhập vào ở trạng thái ' +
    'Nháp của công ty đang làm việc – kiểm tra rồi gửi duyệt như lập tay. Bảng cùng NCC, cùng tên, cùng ngày hiệu lực đã có thì báo lỗi, ' +
    'không nhập lại. Bấm "Kiểm tra thử" trước – "Nhập thật" chỉ lưu khi mọi bảng giá đều đúng.',
  summary: (r) => ({
    valid: `File hợp lệ: ${r.priceLists.toLocaleString('vi-VN')} bảng giá, ${r.lines.toLocaleString('vi-VN')} dòng giá. Bấm "Nhập thật" để lưu.`,
    committed: `Đã nhập ${r.priceLists.toLocaleString('vi-VN')} bảng giá nháp, ${r.lines.toLocaleString('vi-VN')} dòng giá.`,
  }),
}
