import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'
import type { ReceiptAcceptance } from './inventoryDocuments'

/*
 * Kiểu dữ liệu và hàm gọi API báo cáo kho (khớp Proc.Application.Reports – StockReports.cs, design.md mục 11.2).
 */

const useCompanyId = () => useAuth().workingCompany?.id ?? ''

/** Một dòng hàng chậm luân chuyển: kho × mặt hàng còn tồn, lâu không xuất. */
export interface SlowMovingRow {
  warehouseId: string
  warehouseCode: string
  warehouseName: string
  itemId: string
  itemCode: string
  itemName: string
  categoryCode: string
  categoryName: string
  uomCode: string
  qty: number
  amount: number
  firstInDate: string | null
  lastInDate: string | null
  lastOutDate: string | null
  idleDays: number
}

/** Báo cáo hàng chậm luân chuyển. */
export interface SlowMovingResult {
  asOf: string
  days: number
  items: SlowMovingRow[]
}

/** Một lô còn tồn sắp (hoặc đã) hết hạn. */
export interface ExpiringRow {
  warehouseId: string
  warehouseCode: string
  warehouseName: string
  itemId: string
  itemCode: string
  itemName: string
  categoryCode: string
  uomCode: string
  lotNo: string
  expiryDate: string
  daysLeft: number
  qty: number
  amount: number
}

/** Báo cáo hàng sắp hết hạn. */
export interface ExpiringResult {
  asOf: string
  days: number
  items: ExpiringRow[]
}

/** Tồn một mặt hàng tại một công ty – chỉ số lượng. */
export interface GroupStockRow {
  companyId: string
  companyCode: string
  companyName: string
  isCurrent: boolean
  mainQty: number
  mainAvailable: number
  subQty: number
  totalQty: number
}

/** Tồn tập đoàn của một mặt hàng. */
export interface GroupStockResult {
  itemId: string
  itemCode: string
  itemName: string
  uomCode: string
  minStock: number | null
  items: GroupStockRow[]
}

/** Điều kiện chung của báo cáo kho: kho, nhóm hàng (gồm nhóm con), số ngày. */
export interface StockReportFilter {
  warehouseId?: string
  categoryId?: string
  days: number
}

/** Chuỗi tham số URL của báo cáo kho (số ngày, kho, nhóm hàng…), bỏ tham số trống. */
function toQuery(filter: StockReportFilter) {
  const params = new URLSearchParams({ days: String(filter.days) })
  if (filter.warehouseId) params.set('warehouseId', filter.warehouseId)
  if (filter.categoryId) params.set('categoryId', filter.categoryId)
  return params.toString()
}

/**
 * Hàng chậm luân chuyển: còn tồn mà không xuất từ filter.days ngày.
 * @param filter Kho, nhóm hàng, số ngày không xuất.
 */
export function useSlowMoving(filter: StockReportFilter) {
  const companyId = useCompanyId()
  const query = toQuery(filter)
  return useQuery({
    queryKey: ['reports', companyId, 'slow-moving', query],
    queryFn: () => apiFetch<SlowMovingResult>(`/api/reports/slow-moving?${query}`),
    placeholderData: keepPreviousData,
  })
}

/**
 * Lô sắp hết hạn trong filter.days ngày tới (gồm lô đã hết hạn).
 * @param filter Kho, nhóm hàng, số ngày tới.
 */
export function useExpiring(filter: StockReportFilter) {
  const companyId = useCompanyId()
  const query = toQuery(filter)
  return useQuery({
    queryKey: ['reports', companyId, 'expiring', query],
    queryFn: () => apiFetch<ExpiringResult>(`/api/reports/expiring?${query}`),
    placeholderData: keepPreviousData,
  })
}

/**
 * Tồn một mặt hàng ở mọi công ty (cần quyền xem toàn tập đoàn).
 * @param itemId Mặt hàng; trống thì không tải.
 */
export function useGroupStock(itemId: string | undefined) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['reports', companyId, 'group-stock', itemId],
    queryFn: () => apiFetch<GroupStockResult>(`/api/reports/group-stock?itemId=${itemId}`),
    enabled: !!itemId,
  })
}

/** Mức của dòng gợi ý đặt hàng: Urgent – tồn dưới tồn an toàn; Reorder – khả dụng dưới ngưỡng; Ok – còn đủ. */
export type ReorderLevel = 'Urgent' | 'Reorder' | 'Ok'

/** Một mặt hàng trong báo cáo gợi ý đặt hàng (design.md mục 18.16) – số lượng theo ĐVT cơ sở, cộng mọi kho của công ty. */
export interface ReorderSuggestionRow {
  itemId: string
  itemCode: string
  itemName: string
  categoryCode: string
  categoryName: string
  baseUomId: string
  uomCode: string
  isCritical: boolean
  safetyStock: number | null
  /** Ngưỡng đang áp dụng: điểm đặt hàng lại, chưa có thì tồn tối thiểu. */
  reorderPoint: number
  maxStock: number | null
  onHandQty: number
  onOrderQty: number
  openRequestQty: number
  availableQty: number
  suggestedQty: number
  level: ReorderLevel
  preferredSupplierId: string | null
  preferredSupplierCode: string | null
}

/** Kết quả báo cáo gợi ý đặt hàng. */
export interface ReorderSuggestionResult {
  items: ReorderSuggestionRow[]
  truncated: boolean
  limit: number
}

/** Điều kiện báo cáo gợi ý đặt hàng. */
export interface ReorderSuggestionFilter {
  categoryId?: string
  criticalOnly: boolean
  includeAll: boolean
}

/**
 * Gợi ý đặt hàng: mặt hàng có khả dụng (tồn + đang đặt + đề nghị còn mở) dưới điểm đặt hàng lại, kèm SL gợi ý.
 * @param filter Nhóm hàng, chỉ trọng yếu, gồm cả hàng còn đủ.
 */
export function useReorderSuggestions(filter: ReorderSuggestionFilter) {
  const companyId = useCompanyId()
  const params = new URLSearchParams({ criticalOnly: String(filter.criticalOnly), includeAll: String(filter.includeAll) })
  if (filter.categoryId) params.set('categoryId', filter.categoryId)
  const query = params.toString()
  return useQuery({
    queryKey: ['reports', companyId, 'reorder-suggestions', query],
    queryFn: () => apiFetch<ReorderSuggestionResult>(`/api/reports/reorder-suggestions?${query}`),
    placeholderData: keepPreviousData,
  })
}

/** Một dòng PR chưa xử lý: chờ duyệt, hoặc đã duyệt chưa đặt đủ. */
export interface PendingRequestLine {
  requestId: string
  requestNo: string
  date: string
  department: string | null
  status: 'Submitted' | 'Approved'
  priority: 'Low' | 'Normal' | 'Urgent'
  lineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uomCode: string
  qty: number
  orderedQty: number
  remainingQty: number
  estAmount: number
  neededDate: string | null
  overdueDays: number
}

/** Cách nhóm báo cáo mua hàng. */
export type PurchaseSpendGroup = 'supplier' | 'category' | 'item' | 'company'

/** Một dòng mua hàng theo nhóm. */
export interface PurchaseSpendRow {
  key: string
  code: string
  name: string
  receiptCount: number
  receiptAmount: number
  returnAmount: number
  netAmount: number
  sharePct: number
}

/** Báo cáo mua hàng theo nhóm. */
export interface PurchaseSpendResult {
  groupBy: PurchaseSpendGroup
  allCompanies: boolean
  totalNet: number
  items: PurchaseSpendRow[]
}

/** Chỉ số đánh giá một NCC. */
export interface SupplierEvaluationRow {
  supplierId: string
  supplierCode: string
  supplierName: string
  receiptCount: number
  receiptAmount: number
  returnAmount: number
  returnPct: number | null
  deliveryLines: number
  onTimeLines: number
  onTimePct: number | null
  avgLateDays: number | null
  /** Mã hạng, "Không đạt", hoặc null khi chưa cấu hình hạng. */
  rating: string | null
}

/** Một dòng RFQ đã chọn thầu và tiền tiết kiệm. */
export interface SourcingSavingRow {
  rfqId: string
  rfqNo: string
  title: string
  awardDate: string
  companyCode: string
  itemCode: string
  itemName: string
  uomCode: string
  qty: number
  supplierCode: string
  supplierName: string
  quoteCount: number
  awardedPrice: number
  minPrice: number
  avgPrice: number
  maxPrice: number
  awardedAmount: number
  savingVsAvg: number
  savingVsMax: number
  isLowest: boolean
  awardReason: string | null
}

/** Khoảng ngày và phạm vi của báo cáo mua hàng. */
export interface PurchaseReportPeriod {
  from: string
  to: string
  allCompanies: boolean
}

/** Chuỗi tham số URL khoảng ngày và cờ toàn tập đoàn của báo cáo mua hàng. */
const periodQuery = (p: PurchaseReportPeriod) => new URLSearchParams({ from: p.from, to: p.to, allCompanies: String(p.allCompanies) })

/** Dòng PR chưa xử lý của công ty đang làm việc. */
export function usePendingRequests() {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['reports', companyId, 'pending-requests'],
    queryFn: () => apiFetch<PendingRequestLine[]>('/api/reports/pending-requests'),
  })
}

/**
 * Mua hàng thuần theo nhóm.
 * @param period Khoảng ngày, phạm vi.
 * @param groupBy Cách nhóm.
 */
export function usePurchaseSpend(period: PurchaseReportPeriod, groupBy: PurchaseSpendGroup) {
  const companyId = useCompanyId()
  const params = periodQuery(period)
  params.set('groupBy', groupBy)
  const query = params.toString()
  return useQuery({
    queryKey: ['reports', companyId, 'purchase-spend', query],
    queryFn: () => apiFetch<PurchaseSpendResult>(`/api/reports/purchase-spend?${query}`),
    placeholderData: keepPreviousData,
  })
}

/** Một dòng PO trên báo cáo SL đặt – SL thực nhận / OTIF (lát K2, design.md mục 19.4). */
export interface OrderFulfillmentRow {
  orderId: string
  orderNo: string
  orderDate: string
  companyCode: string
  supplierCode: string
  supplierName: string
  lineNo: number
  itemCode: string
  itemName: string
  uomCode: string
  orderedQty: number
  receivedQty: number
  /** SL đặt − thực nhận (âm = nhận thừa trong dung sai). */
  shortQty: number
  dueDate: string | null
  lastReceiptDate: string | null
  /** null = dòng không có hạn giao (không chấm). */
  onTime: boolean | null
  inFull: boolean
  closedManually: boolean
}

/** Tổng hợp OTIF theo NCC (hoặc dòng tổng). */
export interface OrderFulfillmentSupplier {
  supplierCode: string
  supplierName: string
  lines: number
  inFullLines: number
  scoredLines: number
  onTimeLines: number
  otifLines: number
  inFullPct: number | null
  onTimePct: number | null
  otifPct: number | null
}

/** Báo cáo SL đặt – SL thực nhận / OTIF. */
export interface OrderFulfillmentResult {
  rows: OrderFulfillmentRow[]
  suppliers: OrderFulfillmentSupplier[]
  total: OrderFulfillmentSupplier
  allCompanies: boolean
}

/**
 * SL đặt – SL thực nhận / OTIF theo ngày PO.
 * @param period Khoảng ngày PO, phạm vi.
 */
export function useOrderFulfillment(period: PurchaseReportPeriod) {
  const companyId = useCompanyId()
  const query = periodQuery(period).toString()
  return useQuery({
    queryKey: ['reports', companyId, 'order-fulfillment', query],
    queryFn: () => apiFetch<OrderFulfillmentResult>(`/api/reports/order-fulfillment?${query}`),
    placeholderData: keepPreviousData,
  })
}

/**
 * Một PO trên báo cáo theo dõi thanh toán (lát Y9a, design.md mục 19.3 "Lát Y9"). Tiền VND gồm VAT; còn phải trả = hóa đơn − đã chi − giảm trừ
 * của đề nghị đã chi.
 */
export interface PaymentTrackingRow {
  orderId: string
  orderNo: string
  orderDate: string
  companyCode: string
  supplierCode: string
  supplierName: string
  orderClosed: boolean
  orderAmount: number
  receivedAmount: number
  receiptCount: number
  invoiceAmount: number
  noInvoiceReceipts: number
  unrequestedAmount: number
  inProgressAmount: number
  approvedUnpaidAmount: number
  paidAmount: number
  paidDeductionAmount: number
  outstandingAmount: number
  lastPaidDate: string | null
}

/** Tổng hợp theo dõi thanh toán theo NCC (hoặc dòng tổng). */
export interface PaymentTrackingSupplier {
  supplierCode: string
  supplierName: string
  orders: number
  orderAmount: number
  receivedAmount: number
  invoiceAmount: number
  noInvoiceReceipts: number
  unrequestedAmount: number
  inProgressAmount: number
  approvedUnpaidAmount: number
  paidAmount: number
  paidDeductionAmount: number
  outstandingAmount: number
}

/** Báo cáo theo dõi thanh toán. */
export interface PaymentTrackingResult {
  rows: PaymentTrackingRow[]
  suppliers: PaymentTrackingSupplier[]
  total: PaymentTrackingSupplier
  allCompanies: boolean
}

/**
 * Theo dõi thanh toán theo ngày PO.
 * @param period Khoảng ngày PO, phạm vi.
 */
export function usePaymentTracking(period: PurchaseReportPeriod) {
  const companyId = useCompanyId()
  const query = periodQuery(period).toString()
  return useQuery({
    queryKey: ['reports', companyId, 'payment-tracking', query],
    queryFn: () => apiFetch<PaymentTrackingResult>(`/api/reports/payment-tracking?${query}`),
    placeholderData: keepPreviousData,
  })
}

/**
 * Đánh giá NCC trong khoảng ngày.
 * @param period Khoảng ngày, phạm vi.
 */
export function useSupplierEvaluation(period: PurchaseReportPeriod) {
  const companyId = useCompanyId()
  const query = periodQuery(period).toString()
  return useQuery({
    queryKey: ['reports', companyId, 'supplier-evaluation', query],
    queryFn: () => apiFetch<SupplierEvaluationRow[]>(`/api/reports/supplier-evaluation?${query}`),
    placeholderData: keepPreviousData,
  })
}

/**
 * Tiết kiệm từ đấu thầu theo ngày chọn thầu.
 * @param period Khoảng ngày, phạm vi.
 */
export function useSourcingSavings(period: PurchaseReportPeriod) {
  const companyId = useCompanyId()
  const query = periodQuery(period).toString()
  return useQuery({
    queryKey: ['reports', companyId, 'sourcing-savings', query],
    queryFn: () => apiFetch<SourcingSavingRow[]>(`/api/reports/sourcing-savings?${query}`),
    placeholderData: keepPreviousData,
  })
}

/** Nguồn giá trong báo cáo giá mua (design.md mục 18.17); Deviation = lệch giá đã duyệt. */
export type PriceSourceKind = 'MonthlyApproval' | 'Rfq' | 'Contract' | 'PriceList' | 'Internal' | 'Deviation' | 'None' | 'Unknown'

/** Tổng hợp giá trị dòng PO theo một nguồn giá. */
export interface PriceSourceSummary {
  source: PriceSourceKind
  lineCount: number
  amount: number
  sharePct: number
}

/** Một dòng PO mua ngoài hợp đồng: không có giá đã duyệt hoặc lệch giá đã duyệt. */
export interface OutOfContractLine {
  orderId: string
  orderNo: string
  date: string
  companyCode: string
  department: string | null
  supplierCode: string
  supplierName: string
  lineNo: number
  itemCode: string
  itemName: string
  uomCode: string
  qty: number
  netUnitPrice: number
  approvedNetPrice: number | null
  deviationPct: number | null
  amount: number
  deviationAmount: number | null
  kind: 'Deviation' | 'None'
  reason: string | null
}

/** Báo cáo tuần giá mua & mua ngoài hợp đồng. */
export interface ContractComplianceResult {
  orderCount: number
  totalAmount: number
  compliancePct: number | null
  sources: PriceSourceSummary[]
  lines: OutOfContractLine[]
}

/**
 * Giá mua & mua ngoài hợp đồng theo ngày PO (design.md mục 18.17).
 * @param period Khoảng ngày, phạm vi.
 */
export function useContractCompliance(period: PurchaseReportPeriod) {
  const companyId = useCompanyId()
  const query = periodQuery(period).toString()
  return useQuery({
    queryKey: ['reports', companyId, 'contract-compliance', query],
    queryFn: () => apiFetch<ContractComplianceResult>(`/api/reports/contract-compliance?${query}`),
    placeholderData: keepPreviousData,
  })
}

/** Biến động giá mua một mặt hàng: kỳ này so kỳ trước liền trước – trọn tháng thì lùi theo tháng (giá theo ĐVT cơ sở, sau CK, trước VAT). */
export interface PriceVarianceRow {
  itemId: string
  itemCode: string
  itemName: string
  baseUomCode: string
  previousQty: number
  previousAvgPrice: number | null
  currentQty: number
  currentAvgPrice: number
  minPrice: number
  maxPrice: number
  changePct: number | null
  impactAmount: number | null
  orderCount: number
  supplierCount: number
}

/** Báo cáo biến động giá mua theo kỳ. */
export interface PriceVarianceResult {
  previousFrom: string
  previousTo: string
  impactAmount: number
  items: PriceVarianceRow[]
}

/**
 * Biến động giá mua kỳ này so kỳ trước (design.md mục 18.17).
 * @param period Kỳ này, phạm vi.
 * @param categoryId Nhóm hàng (gồm nhóm con), không có = mọi nhóm.
 */
export function usePriceVariance(period: PurchaseReportPeriod, categoryId?: string) {
  const companyId = useCompanyId()
  const params = periodQuery(period)
  if (categoryId) params.set('categoryId', categoryId)
  const query = params.toString()
  return useQuery({
    queryKey: ['reports', companyId, 'price-variance', query],
    queryFn: () => apiFetch<PriceVarianceResult>(`/api/reports/price-variance?${query}`),
    placeholderData: keepPreviousData,
  })
}

/** Tiết kiệm so giá chuẩn của một mặt hàng (lát G4c, design.md mục 18.25) – giá theo ĐVT cơ sở, sau CK, trước VAT. */
export interface StandardSavingRow {
  itemId: string
  itemCode: string
  itemName: string
  baseUomCode: string
  qty: number
  avgPrice: number
  standardAvgPrice: number
  amount: number
  standardAmount: number
  /** Giá trị theo giá chuẩn − thành tiền; âm là mua cao hơn giá chuẩn. */
  saving: number
  savingPct: number | null
  targetAmount: number | null
  /** Thành tiền dòng có giá mục tiêu − giá trị theo giá mục tiêu; ≤ 0 là đạt. */
  targetGap: number | null
  overCeilingLines: number
  orderCount: number
  approvalNos: string
}

/** Báo cáo tiết kiệm so giá chuẩn. */
export interface StandardSavingResult {
  totalAmount: number
  coveredAmount: number
  coveragePct: number | null
  standardAmount: number
  saving: number
  savingPct: number | null
  items: StandardSavingRow[]
}

/**
 * Tiết kiệm so giá chuẩn của phiếu duyệt tháng (G4c): dòng PO đã duyệt bằng VND có giá chuẩn.
 * @param period Khoảng ngày PO, phạm vi.
 * @param categoryId Nhóm hàng (gồm nhóm con), không có = mọi nhóm.
 */
export function useStandardSavings(period: PurchaseReportPeriod, categoryId?: string) {
  const companyId = useCompanyId()
  const params = periodQuery(period)
  if (categoryId) params.set('categoryId', categoryId)
  const query = params.toString()
  return useQuery({
    queryKey: ['reports', companyId, 'standard-savings', query],
    queryFn: () => apiFetch<StandardSavingResult>(`/api/reports/standard-savings?${query}`),
    placeholderData: keepPreviousData,
  })
}

/** Một dòng nhận hàng có sai lệch (design.md mục 18.14). */
export interface ReceiptQualityRow {
  documentId: string
  documentNo: string
  date: string
  supplierCode: string
  supplierName: string
  purchaseOrderNo: string | null
  lineNo: number
  itemCode: string
  itemName: string
  uomCode: string
  deliveredQty: number | null
  qty: number
  rejectedQty: number
  discrepancyQty: number
  qcResult: 'NotChecked' | 'Passed' | 'Failed'
  qcNote: string | null
}

/** Báo cáo sai lệch nhận hàng và tỷ lệ đạt chất lượng. */
export interface ReceiptQualityResult {
  rows: ReceiptQualityRow[]
  totalLines: number
  checkedLines: number
  passedLines: number
  passedPct: number | null
}

/**
 * Sai lệch nhận hàng theo ngày phiếu nhập mua đã ghi sổ.
 * @param range Khoảng ngày yyyy-MM-dd.
 */
export function useReceiptQuality(range: { from: string; to: string }) {
  const companyId = useCompanyId()
  const query = new URLSearchParams(range).toString()
  return useQuery({
    queryKey: ['reports', companyId, 'receipt-quality', query],
    queryFn: () => apiFetch<ReceiptQualityResult>(`/api/reports/receipt-quality?${query}`),
    placeholderData: keepPreviousData,
  })
}

/** Kết quả đối chiếu một phiếu nhập: khớp, chưa có hóa đơn, lệch tiền (design.md mục 18.15). */
export type ThreeWayMatchStatus = 'Matched' | 'NoInvoice' | 'Mismatch'

/** Mã cảnh báo của báo cáo đối chiếu PO – nhận – hóa đơn. */
export type ThreeWayMatchIssue =
  | 'NoInvoice'
  | 'AmountMismatch'
  | 'InvoiceIncludesShortage'
  | 'NoPurchaseOrder'
  | 'ReceivedBeforeOrder'
  | 'ReceivedBeforeApproval'
  | 'InvoiceBeforeOrder'
  | 'AcceptanceDisputed'

/** Một phiếu nhập mua trên báo cáo đối chiếu PO – nhận – hóa đơn. */
export interface ThreeWayMatchRow {
  documentId: string
  documentNo: string
  date: string
  supplierCode: string
  supplierName: string
  purchaseOrderId: string | null
  purchaseOrderNo: string | null
  purchaseOrderDate: string | null
  purchaseOrderApprovedDate: string | null
  orderTotal: number | null
  receiptTotal: number
  invoiceNo: string | null
  invoiceSeries: string | null
  invoiceDate: string | null
  invoiceAmount: number | null
  difference: number | null
  result: ThreeWayMatchStatus
  issues: ThreeWayMatchIssue[]
  /** Xác nhận nghiệm thu của đơn vị đặt hàng (lát H4). */
  acceptance: ReceiptAcceptance
}


/** Báo cáo đối chiếu PO – nhận – hóa đơn: dòng và tổng hợp. */
export interface ThreeWayMatchResult {
  rows: ThreeWayMatchRow[]
  matched: number
  noInvoice: number
  mismatch: number
  withIssues: number
  receiptTotal: number
  invoiceTotal: number
  hiddenDocuments: number
}

/** Điều kiện báo cáo đối chiếu: khoảng ngày nhập, dung sai tiền (đ), chỉ phiếu có cảnh báo. */
export interface ThreeWayMatchFilter {
  from: string
  to: string
  tolerance: number
  onlyIssues: boolean
}

/**
 * Đối chiếu PO – nhận – hóa đơn theo ngày phiếu nhập mua đã ghi sổ (design.md mục 18.15).
 * @param filter Điều kiện lọc.
 */
export function useThreeWayMatch(filter: ThreeWayMatchFilter) {
  const companyId = useCompanyId()
  const query = new URLSearchParams({
    from: filter.from,
    to: filter.to,
    tolerance: String(filter.tolerance),
    onlyIssues: String(filter.onlyIssues),
  }).toString()
  return useQuery({
    queryKey: ['reports', companyId, 'three-way-match', query],
    queryFn: () => apiFetch<ThreeWayMatchResult>(`/api/reports/three-way-match?${query}`),
    placeholderData: keepPreviousData,
  })
}

/** Loại yêu cầu tính SLA (design.md mục 18.13). */
export type SlaCategory = 'Priced' | 'Sourcing' | 'Urgent'

/** Trạng thái SLA của một dòng PR. */
export type SlaStatus = 'OnTime' | 'Late' | 'InProgress' | 'Overdue'

/** Một dòng báo cáo SLA mua hàng. */
export interface PurchaseSlaRow {
  requestId: string
  requestNo: string
  approvedDate: string
  department: string | null
  lineNo: number
  itemCode: string
  itemName: string
  category: SlaCategory
  slaDays: number
  dueDate: string
  orderId: string | null
  orderNo: string | null
  orderApprovedDate: string | null
  status: SlaStatus
}

/** Báo cáo SLA mua hàng: dòng và tổng hợp. */
export interface PurchaseSlaResult {
  rows: PurchaseSlaRow[]
  onTime: number
  late: number
  inProgress: number
  overdue: number
  /** % đúng SLA; null khi chưa có dòng nào đến hạn hoặc có PO. */
  onTimePct: number | null
}

/**
 * Báo cáo SLA mua hàng của công ty đang làm việc theo ngày PR được duyệt.
 * @param range Khoảng ngày yyyy-MM-dd.
 */
export function usePurchaseSla(range: { from: string; to: string }) {
  const companyId = useCompanyId()
  const query = new URLSearchParams(range).toString()
  return useQuery({
    queryKey: ['reports', companyId, 'purchase-sla', query],
    queryFn: () => apiFetch<PurchaseSlaResult>(`/api/reports/purchase-sla?${query}`),
    placeholderData: keepPreviousData,
  })
}

/** Một chứng từ đã ghi sổ sẽ xuất cho kế toán. */
export interface AccountingDocument {
  documentId: string
  docType: 'GRN' | 'RTV' | 'TRF' | 'ICS' | 'ICR' | 'ADJ'
  no: string
  date: string
  partnerCode: string | null
  partnerLocalCode: string | null
  partnerName: string | null
  partnerTaxCode: string | null
  invoiceNo: string | null
  invoiceSeries: string | null
  invoiceDate: string | null
  lineCount: number
  amount: number
  vatAmount: number
  costAmount: number
  isCostFinal: boolean
}

/** Điều kiện xuất cho kế toán. */
export interface AccountingExportFilter {
  from: string
  to: string
  docTypes: string[]
}

/** Chuỗi truy vấn của điều kiện xuất cho kế toán (dùng chung cho danh sách và file). */
export function accountingExportQuery(filter: AccountingExportFilter) {
  const params = new URLSearchParams({ from: filter.from, to: filter.to })
  for (const t of filter.docTypes) params.append('docTypes', t)
  return params.toString()
}

/**
 * Chứng từ đã ghi sổ sẽ xuất cho kế toán (xem trước).
 * @param filter Khoảng ngày, loại chứng từ.
 */
export function useAccountingDocuments(filter: AccountingExportFilter) {
  const companyId = useCompanyId()
  const query = accountingExportQuery(filter)
  return useQuery({
    queryKey: ['reports', companyId, 'accounting-export', query],
    queryFn: () => apiFetch<AccountingDocument[]>(`/api/accounting-export/documents?${query}`),
    placeholderData: keepPreviousData,
  })
}

/** Một chứng từ mua sẽ xuất cho kế toán (lát Y9b, design.md mục 19.3 "Lát Y9"). */
export interface PurchaseAccountingDocument {
  documentId: string
  docType: 'PO' | 'XNH' | 'DNT'
  no: string
  date: string
  /** Ngày lọc: ngày duyệt (PO, DNT), ngày nhận (XNH). */
  effectiveDate: string
  purchaseOrderNo: string | null
  supplierCode: string
  supplierLocalCode: string | null
  supplierName: string
  supplierTaxCode: string | null
  invoiceNo: string | null
  invoiceSeries: string | null
  invoiceDate: string | null
  lineCount: number
  /** PO, XNH: trước thuế; DNT: tổng tiền hóa đơn gồm VAT. */
  amount: number
  vatAmount: number
  /** PO, XNH: tiền + thuế; DNT: số tiền đề nghị. */
  totalAmount: number
  status: string
}

/**
 * Chứng từ mua (PO, XNH, DNT) sẽ xuất cho kế toán – xem trước.
 * @param filter Khoảng ngày, loại chứng từ (dùng chung chuỗi truy vấn với ACCX).
 */
export function usePurchaseAccountingDocuments(filter: AccountingExportFilter) {
  const companyId = useCompanyId()
  const query = accountingExportQuery(filter)
  return useQuery({
    queryKey: ['reports', companyId, 'purchase-accounting-export', query],
    queryFn: () => apiFetch<PurchaseAccountingDocument[]>(`/api/purchase-accounting-export/documents?${query}`),
    placeholderData: keepPreviousData,
  })
}

/** Một chỉ số trên bảng KPI cung ứng (design.md mục 18.18). */
export interface KpiRow {
  code: string
  name: string
  formula: string
  /** true: đạt khi ≥ mục tiêu; false: đạt khi ≤ mục tiêu. */
  higherIsBetter: boolean
  /** true: số vụ; false: tỷ lệ %. */
  isCount: boolean
  target: number
  /** false: chưa đo được trong phần mềm (chờ lát sau). */
  measured: boolean
  value: number | null
  previousValue: number | null
  numerator: number | null
  denominator: number | null
  unit: 'Money' | 'Count'
  met: boolean | null
  note: string | null
  /** Mã màn hình báo cáo chi tiết. */
  report: string | null
}

/** Bảng KPI cung ứng của công ty đang làm việc. */
export interface KpiResult {
  from: string
  to: string
  previousFrom: string
  previousTo: string
  measuredCount: number
  metCount: number
  items: KpiRow[]
}

/**
 * Bảng KPI cung ứng trong khoảng ngày, so kỳ trước liền trước (design.md mục 18.18).
 * @param range Khoảng ngày yyyy-MM-dd.
 */
export function useKpi(range: { from: string; to: string }) {
  const companyId = useCompanyId()
  const query = new URLSearchParams(range).toString()
  return useQuery({
    queryKey: ['reports', companyId, 'kpi', query],
    queryFn: () => apiFetch<KpiResult>(`/api/reports/kpi?${query}`),
    placeholderData: keepPreviousData,
  })
}

/** Trạng thái một nhóm hàng trong báo cáo NCC đủ điều kiện: đủ / thiếu / miễn vì NCC độc quyền. */
export type SupplierCoverageStatus = 'Ok' | 'Short' | 'SoleSource'

/**
 * Một nhóm hàng: số NCC Đã duyệt cung cấp nhóm (ngành hàng là nhóm/nhóm cha hoặc NCC – sản phẩm của mặt hàng trong nhóm) so với mức tối
 * thiểu (design.md mục 18.25, Q1 – lát G1b).
 */
export interface SupplierCoverageRow {
  categoryId: string
  categoryCode: string
  categoryName: string
  itemCount: number
  approvedCount: number
  potentialCount: number
  suspendedCount: number
  missing: number
  status: SupplierCoverageStatus
  approvedSuppliers: string
  soleSourceItemCount: number
  soleSourceNo: string | null
}

/** Báo cáo NCC đủ điều kiện theo nhóm hàng. */
export interface SupplierCoverageResult {
  minSuppliers: number
  categoryCount: number
  shortCount: number
  categories: SupplierCoverageRow[]
}

/**
 * NCC đủ điều kiện theo nhóm hàng – quyết định Ban cung ứng: tối thiểu 03 NCC, đếm theo nhóm hàng (design.md mục 18.25, Q1 – lát G1b).
 * @param minSuppliers Số NCC tối thiểu (1–20).
 * @param categoryId Nhóm hàng (gồm nhóm con), không có = mọi nhóm.
 * @param onlyShort Chỉ nhóm hàng thiếu.
 */
export function useSupplierCoverage(minSuppliers: number, categoryId: string | undefined, onlyShort: boolean) {
  const companyId = useCompanyId()
  const params = new URLSearchParams({ minSuppliers: String(minSuppliers), onlyShort: String(onlyShort) })
  if (categoryId) params.set('categoryId', categoryId)
  const query = params.toString()
  return useQuery({
    queryKey: ['reports', companyId, 'supplier-coverage', query],
    queryFn: () => apiFetch<SupplierCoverageResult>(`/api/reports/supplier-coverage?${query}`),
    placeholderData: keepPreviousData,
  })
}
