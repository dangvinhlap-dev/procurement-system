import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiDownload, apiFetch } from './client'
import type { AnomalySummary, ControlFinding } from './control'
import type { KpiResult, OutOfContractLine, PriceSourceSummary, PriceVarianceRow, SupplierCoverageResult } from './reports'
import type { SupplierOnboardingListItem } from './supplierOnboardings'
import type { VolumeRebateTrackingRow } from './volumeRebates'

/*
 * Báo cáo tháng cho Ban cung ứng (lát G12, design.md mục 18.25) – khớp Proc.Application.Reports.BoardMonthlyReport.cs.
 */

/** Mã mục của báo cáo tháng, theo thứ tự trình bày. */
export type BoardSectionCode =
  | 'Purchase'
  | 'Kpi'
  | 'Savings'
  | 'OutOfContract'
  | 'PriceVariance'
  | 'VolumeRebates'
  | 'SupplierCoverage'
  | 'Onboarding'
  | 'Control'

/** Trạng thái một mục: có số liệu hay không (thiếu quyền), ghi chú phạm vi, báo cáo chi tiết. */
export interface BoardSection {
  code: BoardSectionCode
  title: string
  available: boolean
  note: string | null
  /** Mã màn hình báo cáo chi tiết. */
  report: string | null
}

/** Tổng quan mua hàng tháng này so tháng trước (PRCW). */
export interface BoardPurchaseSummary {
  orderCount: number
  totalAmount: number
  compliancePct: number | null
  outOfContractAmount: number
  previousOrderCount: number
  previousTotalAmount: number
  previousCompliancePct: number | null
  sources: PriceSourceSummary[]
}

/** Tiết kiệm từ đấu thầu và so giá chuẩn. */
export interface BoardSavings {
  rfqCount: number
  awardedAmount: number
  sourcingSaving: number
  sourcingSavingPct: number | null
  standardCoveredAmount: number
  standardCoveragePct: number | null
  standardSaving: number
  standardSavingPct: number | null
}

/** Biến động giá: tổng ảnh hưởng và các mặt hàng tăng giá ảnh hưởng lớn nhất. */
export interface BoardPriceVariance {
  impactAmount: number
  increasedCount: number
  decreasedCount: number
  topIncreases: PriceVarianceRow[]
}

/** Hồ sơ đánh giá NCC mới có ngày hồ sơ trong tháng. */
export interface BoardOnboarding {
  count: number
  approvedCount: number
  inProgressCount: number
  closedCount: number
  items: SupplierOnboardingListItem[]
}

/** Kiểm soát nội bộ (chỉ người có quyền KSNB). */
export interface BoardControl {
  /** null khi chỉ có quyền nhận vi phạm (không xem dấu hiệu). */
  highCount: number | null
  mediumCount: number | null
  summary: AnomalySummary[]
  findingCount: number
  seriousConcludedCount: number
  findings: ControlFinding[]
}

/** Gói báo cáo tháng cho Ban; mục thiếu quyền là null. */
export interface BoardMonthlyReport {
  year: number
  month: number
  from: string
  to: string
  previousFrom: string
  previousTo: string
  allCompanies: boolean
  companyCode: string
  sections: BoardSection[]
  purchase: BoardPurchaseSummary | null
  kpi: KpiResult | null
  savings: BoardSavings | null
  outOfContract: OutOfContractLine[] | null
  outOfContractCount: number
  priceVariance: BoardPriceVariance | null
  volumeRebates: VolumeRebateTrackingRow[] | null
  supplierCoverage: SupplierCoverageResult | null
  onboarding: BoardOnboarding | null
  control: BoardControl | null
}

/** Tháng báo cáo và phạm vi. */
export interface BoardMonthlyFilter {
  year: number
  month: number
  allCompanies: boolean
}

/** Chuỗi tham số URL (năm, tháng, toàn tập đoàn) của báo cáo tháng cho Ban. */
const boardQuery = (f: BoardMonthlyFilter) =>
  new URLSearchParams({ year: String(f.year), month: String(f.month), allCompanies: String(f.allCompanies) }).toString()

/**
 * Báo cáo tháng cho Ban của công ty đang làm việc (hoặc toàn tập đoàn).
 * @param filter Tháng, phạm vi.
 */
export function useBoardMonthlyReport(filter: BoardMonthlyFilter) {
  const companyId = useAuth().workingCompany?.id ?? ''
  const query = boardQuery(filter)
  return useQuery({
    queryKey: ['reports', companyId, 'board-monthly', query],
    queryFn: () => apiFetch<BoardMonthlyReport>(`/api/reports/board-monthly?${query}`),
    placeholderData: keepPreviousData,
  })
}

/**
 * Tải file Excel gói báo cáo tháng (sheet Tổng quan + mỗi mục một sheet).
 * @param filter Tháng, phạm vi.
 */
export const downloadBoardMonthlyReport = (filter: BoardMonthlyFilter) =>
  apiDownload(`/api/reports/board-monthly/excel?${boardQuery(filter)}`, `BaoCaoThangBan_${filter.year}-${String(filter.month).padStart(2, '0')}.xlsx`)
