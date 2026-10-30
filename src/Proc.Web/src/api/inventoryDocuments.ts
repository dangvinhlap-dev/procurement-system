import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Kiểu dữ liệu và hàm gọi API chứng từ kho (khớp Proc.Application.Inventory.InventoryDocuments).
 * Viết tay tạm thời; sẽ sinh tự động từ OpenAPI khi số API nhiều lên (design.md mục 3.2).
 */

/** Trạng thái chứng từ kho ở giai đoạn 2. */
export type DocumentStatus = 'Draft' | 'Posted' | 'Cancelled'

/** Tên tiếng Việt của trạng thái chứng từ. */
export const documentStatusLabels: Record<DocumentStatus, string> = {
  Draft: 'Nháp',
  Posted: 'Đã ghi sổ',
  Cancelled: 'Đã hủy',
}

/** Một dòng trên danh sách chứng từ. */
export interface InventoryDocumentListItem {
  id: string
  docType: string
  no: string
  date: string
  status: DocumentStatus
  warehouseCode: string | null
  warehouseName: string | null
  supplierCode: string | null
  supplierName: string | null
  invoiceNo: string | null
  totalAmount: number
  totalVat: number
  lineCount: number
}

/** Kết quả tìm chứng từ. */
export interface InventoryDocumentListResult {
  items: InventoryDocumentListItem[]
  truncated: boolean
  limit: number
}

/** Một dòng chứng từ. */
export interface InventoryLine {
  id: string
  lineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  qty: number
  uomFactor: number
  baseQty: number
  baseUomCode: string
  unitPrice: number
  amount: number
  vatCode: string | null
  vatAmount: number
  lotNo: string | null
  /** yyyy-MM-dd. */
  expiryDate: string | null
  note: string | null
}

/** Chi tiết chứng từ kho. */
export interface InventoryDocument {
  id: string
  docType: string
  no: string
  date: string
  status: DocumentStatus
  fromWarehouseId: string | null
  toWarehouseId: string | null
  supplierId: string | null
  supplierCode: string | null
  supplierName: string | null
  invoiceNo: string | null
  invoiceSeries: string | null
  invoiceDate: string | null
  deliveryNoteNo: string | null
  deliverer: string | null
  note: string | null
  totalAmount: number
  totalVat: number
  createdAt: string
  createdByName: string | null
  postedAt: string | null
  lines: InventoryLine[]
  canEdit: boolean
  readOnlyReason: string | null
  /** Ghi sổ được (nháp, có quyền ghi sổ, thấy hết mặt hàng). */
  canPost: boolean
  /** Bỏ ghi sổ được (đã ghi sổ, có quyền bỏ ghi sổ; kỳ còn mở kiểm tra khi bấm). */
  canUnpost: boolean
}

/** Một dòng gửi lên khi lưu; id null = dòng mới. */
export interface SaveLineInput {
  id: string | null
  itemId: string
  uomId: string
  qty: number
  unitPrice: number
  vatCode: string | null
  lotNo: string | null
  expiryDate: string | null
  note: string | null
}

/** Nội dung phiếu nhập mua NCC gửi lên khi lưu. */
export interface SaveReceiptInput {
  date: string
  warehouseId: string
  supplierId: string
  invoiceNo: string | null
  invoiceSeries: string | null
  invoiceDate: string | null
  deliveryNoteNo: string | null
  deliverer: string | null
  note: string | null
  lines: SaveLineInput[]
}

/** Mặt hàng cho ô chọn hàng trên chứng từ, kèm ĐVT chọn được. */
export interface ItemLookup {
  id: string
  code: string
  name: string
  type: string
  baseUomId: string
  vatCode: string | null
  trackLot: boolean
  trackExpiry: boolean
  uoms: { uomId: string; code: string; factor: number }[]
}

function useCompanyId(): string {
  return useAuth().workingCompany?.id ?? ''
}

/**
 * Danh sách chứng từ kho một loại trong khoảng ngày.
 * @param filter Loại chứng từ, khoảng ngày (yyyy-MM-dd), trạng thái.
 */
export function useInventoryDocuments(filter: { docType: string; from?: string; to?: string; status?: DocumentStatus }) {
  const companyId = useCompanyId()
  const params = new URLSearchParams({ docType: filter.docType })
  if (filter.from) params.set('from', filter.from)
  if (filter.to) params.set('to', filter.to)
  if (filter.status) params.set('status', filter.status)
  const query = params.toString()
  return useQuery({
    queryKey: ['inventory-documents', companyId, query],
    queryFn: () => apiFetch<InventoryDocumentListResult>(`/api/inventory-documents?${query}`),
    placeholderData: keepPreviousData,
  })
}

/**
 * Chi tiết một chứng từ.
 * @param id Id; null thì không tải (đang lập mới).
 */
export function useInventoryDocument(id: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['inventory-documents', companyId, 'detail', id ?? ''],
    queryFn: () => apiFetch<InventoryDocument>(`/api/inventory-documents/${id}`),
    enabled: id !== null,
  })
}

/** Lập, sửa phiếu nhập mua; hủy phiếu nháp; ghi sổ, bỏ ghi sổ. Xong thì tải lại danh sách và chi tiết chứng từ. */
export function useGoodsReceiptMutations() {
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: ['inventory-documents'] })
  return {
    create: useMutation({
      mutationFn: (input: SaveReceiptInput) => apiFetch<InventoryDocument>('/api/goods-receipts', { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: SaveReceiptInput }) =>
        apiFetch<InventoryDocument>(`/api/goods-receipts/${id}`, { method: 'PUT', body: input }),
      onSuccess,
    }),
    cancel: useMutation({
      mutationFn: (id: string) => apiFetch<InventoryDocument>(`/api/inventory-documents/${id}/cancel`, { method: 'POST' }),
      onSuccess,
    }),
    post: useMutation({
      mutationFn: (id: string) => apiFetch<InventoryDocument>(`/api/inventory-documents/${id}/post`, { method: 'POST' }),
      onSuccess,
    }),
    unpost: useMutation({
      mutationFn: (id: string) => apiFetch<InventoryDocument>(`/api/inventory-documents/${id}/unpost`, { method: 'POST' }),
      onSuccess,
    }),
  }
}

/**
 * Tìm hàng cho ô chọn hàng: theo từ khóa (≥ 2 ký tự) và/hoặc theo Id (hiển thị lại các dòng đã có).
 * @param search Từ khóa.
 * @param ids Id các mặt hàng đang có trên phiếu.
 * @param inventoryOnly Chỉ hàng tồn kho (chứng từ kho).
 */
export function useItemLookup(search: string, ids: string[], inventoryOnly = true) {
  const companyId = useCompanyId()
  const keyword = search.trim()
  const params = new URLSearchParams({ inventoryOnly: String(inventoryOnly) })
  if (keyword.length >= 2) params.set('search', keyword)
  for (const id of [...new Set(ids)].sort()) params.append('ids', id)
  const query = params.toString()
  return useQuery({
    queryKey: ['items', companyId, 'lookup', query],
    queryFn: () => apiFetch<ItemLookup[]>(`/api/items/lookup?${query}`),
    enabled: keyword.length >= 2 || ids.length > 0,
    placeholderData: keepPreviousData,
  })
}

/** NCC chọn được trên phiếu nhập mua. */
export interface SupplierLookup {
  id: string
  code: string
  name: string
  taxCode: string | null
}

/**
 * NCC đã duyệt và đang áp dụng tại công ty đang làm việc (kèm NCC đang có trên phiếu dù không còn thỏa điều kiện).
 * @param ids NCC đang có trên phiếu.
 */
export function useSupplierLookup(ids: string[]) {
  const companyId = useCompanyId()
  const params = new URLSearchParams()
  for (const id of ids) params.append('ids', id)
  const query = params.toString()
  return useQuery({
    queryKey: ['suppliers', companyId, 'lookup', query],
    queryFn: () => apiFetch<SupplierLookup[]>(`/api/suppliers/lookup${query ? `?${query}` : ''}`),
  })
}
