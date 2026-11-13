import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/** Tình trạng chung của chứng từ (design.md mục 18.22). */
export type DocumentProgress = 'Open' | 'Done' | 'Cancelled'

/** Nhãn tình trạng. */
export const progressLabels: Record<DocumentProgress, string> = {
  Open: 'Chưa hoàn thành',
  Done: 'Hoàn thành',
  Cancelled: 'Đã hủy / từ chối',
}

/** Một chứng từ trên màn hình Tra cứu chứng từ. */
export interface DocumentRegistryRow {
  docType: string
  id: string
  no: string
  date: string
  partner: string | null
  description: string | null
  amount: number | null
  status: string
  statusName: string
  progress: DocumentProgress
  createdByName: string | null
  createdAt: string
  pendingLevel: number | null
  /** Người duyệt tiếp theo của cấp đang chờ (TC-4); null khi không chờ duyệt. */
  nextApprovers?: string[] | null
}

/** Kết quả tra cứu. */
export interface DocumentRegistryResult {
  rows: DocumentRegistryRow[]
  docTypes: string[]
  truncated: boolean
  limit: number
}

/** Bộ lọc tra cứu. */
export interface DocumentRegistryFilter {
  from: string
  to: string
  docTypes: string[]
  progress?: DocumentProgress
  mine?: 'created' | 'acted'
}

/** Tên loại chứng từ theo mã (cùng tên màn hình của từng loại). */
export const docTypeLabels: Record<string, string> = {
  PR: 'Đề nghị mua hàng',
  PO: 'Đơn mua hàng',
  GRN: 'Nhập mua NCC',
  RTV: 'Xuất trả NCC',
  TRF: 'Chuyển kho nội bộ',
  ICS: 'Xuất bán nội bộ',
  ICR: 'Nhập mua nội bộ',
  ADJ: 'Điều chỉnh kho',
  PRL: 'Bảng giá NCC',
  SRC: 'Tìm nguồn',
  RFQ: 'Yêu cầu báo giá',
  CTR: 'Hợp đồng nguyên tắc',
  CTA: 'Phụ lục hợp đồng',
  NCC: 'Duyệt nhà cung cấp',
  NDQ: 'NCC độc quyền',
  GTH: 'Duyệt NCC và giá tháng',
  BTH: 'Bảng tổng hợp đề xuất mua',
  DNT: 'Đề nghị thanh toán',
  BKT: 'Bảng kê chi phí tạp vụ',
  KSG: 'Khảo sát giá thị trường',
  CKB: 'Thỏa thuận chiết khấu sản lượng gộp',
  KHN: 'Kế hoạch nhu cầu',
  DGN: 'Đánh giá NCC định kỳ',
  HSN: 'Hồ sơ đánh giá NCC mới',
  VP: 'Vi phạm KSNB',
}

/**
 * Đường dẫn mở trang đầy đủ của một chứng từ; null khi loại chưa có trang.
 * Duyệt NCC, NCC độc quyền, đánh giá NCC định kỳ, vi phạm KSNB có trang đầy đủ `<màn hình>/:id` (TC-3).
 * @param docType Mã loại.
 * @param id Id chứng từ.
 */
export function documentPath(docType: string, id: string): string | null {
  const pages: Record<string, string> = {
    PR: '/mua-hang/pr', PO: '/mua-hang/po', GRN: '/kho/grn', RTV: '/kho/rtv', TRF: '/kho/trf', ICS: '/kho/ics', ICR: '/kho/icr',
    ADJ: '/kho/dieu-chinh',
    PRL: '/gia-ncc/bang-gia', SRC: '/dau-thau/tim-nguon', RFQ: '/dau-thau/rfq', CTR: '/mua-hang/hop-dong-khung',
    CTA: '/mua-hang/hop-dong-khung/phu-luc', NCC: '/gia-ncc/duyet-ncc', NDQ: '/gia-ncc/ncc-doc-quyen', VP: '/bao-cao/so-vi-pham',
    DGN: '/gia-ncc/danh-gia-ncc-dinh-ky', GTH: '/gia-ncc/duyet-thang', KSG: '/gia-ncc/khao-sat-gia', CKB: '/gia-ncc/chiet-khau-san-luong', KHN: '/mua-hang/ke-hoach-nhu-cau',
    HSN: '/gia-ncc/ho-so-ncc-moi', XNH: '/mua-hang/xac-nhan-nhan-hang', DHG: '/mua-hang/dat-hang-gop',
    BTH: '/mua-hang/bang-tong-hop', DNT: '/mua-hang/de-nghi-thanh-toan', BKT: '/mua-hang/bang-ke-tap-vu',
  }
  return pages[docType] ? `${pages[docType]}/${id}` : null
}

/**
 * Tra cứu chứng từ của công ty đang làm việc (`GET /api/document-registry`).
 * @param filter Bộ lọc.
 */
export function useDocumentRegistry(filter: DocumentRegistryFilter) {
  const { workingCompany } = useAuth()
  const companyId = workingCompany?.id ?? ''
  const params = new URLSearchParams({ from: filter.from, to: filter.to })
  for (const t of filter.docTypes) params.append('docTypes', t)
  if (filter.progress) params.set('progress', filter.progress)
  if (filter.mine) params.set('mine', filter.mine)
  const query = params.toString()
  return useQuery({
    queryKey: ['document-registry', companyId, query],
    queryFn: () => apiFetch<DocumentRegistryResult>(`/api/document-registry?${query}`),
    enabled: !!companyId,
    placeholderData: keepPreviousData,
  })
}
