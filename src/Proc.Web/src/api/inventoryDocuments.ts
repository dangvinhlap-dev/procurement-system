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
  /** Mã kho xuất (phiếu chuyển kho). */
  fromWarehouseCode: string | null
  /** Mã kho nhập (phiếu chuyển kho). */
  toWarehouseCode: string | null
  /** Công ty đối ứng (phiếu bán/nhập nội bộ). */
  counterCompanyCode?: string | null
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
  /** Dòng chứng từ gốc (phiếu trả: dòng phiếu nhập). */
  sourceLineId: string | null
  /** SL gửi theo phiếu xuất bán (chỉ phiếu nhập mua nội bộ). */
  sentQty?: number | null
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
  /** Người nhận (phiếu chuyển kho). */
  receiver: string | null
  /** Phiếu nhập gốc (phiếu trả NCC). */
  linkedDocumentId: string | null
  linkedDocumentNo: string | null
  /** Công ty đối ứng (ICS: công ty mua; ICR: công ty bán). */
  counterCompanyId?: string | null
  counterCompanyCode?: string | null
  counterCompanyName?: string | null
  /** Trạng thái nhận hàng của cặp bán nội bộ: Pending, Full, Short. */
  receiveStatus?: ReceiveStatus | null
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

/**
 * Lập, sửa một loại phiếu (qua endpoint riêng của loại đó); hủy phiếu nháp, ghi sổ, bỏ ghi sổ (endpoint chung).
 * Xong thì tải lại danh sách và chi tiết chứng từ, tồn kho.
 * @param endpoint Đường dẫn lập/sửa, ví dụ "/api/goods-receipts".
 */
export function useInventoryDocumentMutations<TInput>(endpoint: string) {
  const queryClient = useQueryClient()
  const onSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: ['inventory-documents'] })
    void queryClient.invalidateQueries({ queryKey: ['stock'] })
  }
  return {
    create: useMutation({
      mutationFn: (input: TInput) => apiFetch<InventoryDocument>(endpoint, { method: 'POST', body: input }),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, input }: { id: string; input: TInput }) =>
        apiFetch<InventoryDocument>(`${endpoint}/${id}`, { method: 'PUT', body: input }),
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

/** Lập, sửa, ghi sổ phiếu nhập mua NCC. */
export const useGoodsReceiptMutations = () => useInventoryDocumentMutations<SaveReceiptInput>('/api/goods-receipts')

/** Một dòng trả NCC gửi lên khi lưu. */
export interface SaveReturnLineInput {
  id: string | null
  sourceLineId: string
  qty: number
  note: string | null
}

/** Nội dung phiếu xuất trả NCC gửi lên khi lưu. */
export interface SaveReturnInput {
  date: string
  sourceDocumentId: string
  invoiceNo: string | null
  note: string | null
  lines: SaveReturnLineInput[]
}

/** Lập, sửa, ghi sổ phiếu xuất trả NCC. */
export const useGoodsReturnMutations = () => useInventoryDocumentMutations<SaveReturnInput>('/api/goods-returns')

/** Một dòng chuyển kho gửi lên khi lưu. */
export interface SaveTransferLineInput {
  id: string | null
  itemId: string
  uomId: string
  qty: number
  lotNo: string | null
  note: string | null
}

/** Nội dung phiếu chuyển kho gửi lên khi lưu. */
export interface SaveTransferInput {
  date: string
  fromWarehouseId: string
  toWarehouseId: string
  deliverer: string | null
  receiver: string | null
  note: string | null
  lines: SaveTransferLineInput[]
}

/** Lập, sửa, ghi sổ phiếu chuyển kho. */
export const useStockTransferMutations = () => useInventoryDocumentMutations<SaveTransferInput>('/api/stock-transfers')

/** Một dòng phiếu nhập còn trả được. */
export interface ReturnableLine {
  sourceLineId: string
  lineNo: number
  itemId: string
  itemCode: string
  itemName: string
  uomId: string
  uomCode: string
  lotNo: string | null
  unitPrice: number
  vatCode: string | null
  receivedQty: number
  returnedQty: number
  remainingQty: number
}

/**
 * Các dòng của phiếu nhập kèm số còn trả được.
 * @param receiptId Phiếu nhập gốc; null thì không tải.
 * @param excludeDocumentId Phiếu trả đang sửa (không tính vào số đã trả).
 */
export function useReturnableLines(receiptId: string | null, excludeDocumentId: string | null) {
  const companyId = useCompanyId()
  return useQuery({
    queryKey: ['inventory-documents', companyId, 'returnable', receiptId ?? '', excludeDocumentId ?? ''],
    queryFn: () =>
      apiFetch<ReturnableLine[]>(
        `/api/goods-receipts/${receiptId}/returnable${excludeDocumentId ? `?excludeDocumentId=${excludeDocumentId}` : ''}`,
      ),
    enabled: receiptId !== null,
  })
}

/** Tồn tức thời một mặt hàng (một lô) tại một kho, theo ĐVT cơ sở. */
export interface StockAvailability {
  itemId: string
  lotNo: string | null
  qty: number
}

/**
 * Tồn khả dụng của các mặt hàng tại một kho – hiện trên dòng phiếu xuất/chuyển.
 * @param warehouseId Kho; undefined thì không tải.
 * @param itemIds Mặt hàng trên phiếu.
 */
export function useAvailableStock(warehouseId: string | undefined, itemIds: string[]) {
  const companyId = useCompanyId()
  const params = new URLSearchParams({ warehouseId: warehouseId ?? '' })
  for (const id of [...new Set(itemIds)].sort()) params.append('itemIds', id)
  const query = params.toString()
  return useQuery({
    queryKey: ['stock', companyId, 'available', query],
    queryFn: () => apiFetch<StockAvailability[]>(`/api/stock-on-hand/available?${query}`),
    enabled: !!warehouseId && itemIds.length > 0,
    placeholderData: keepPreviousData,
  })
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

/** Trạng thái nhận hàng bán nội bộ (design.md mục 10.7). */
export type ReceiveStatus = 'Pending' | 'Full' | 'Short'

/** Nhãn trạng thái nhận hàng. */
export const receiveStatusLabels: Record<ReceiveStatus, string> = {
  Pending: 'Chờ nhận',
  Full: 'Đã nhận đủ',
  Short: 'Nhận thiếu',
}

/** Một dòng phiếu xuất bán nội bộ gửi lên (giá do server lấy từ bảng giá nội bộ). */
export interface SaveSaleLineInput {
  id: string | null
  itemId: string
  uomId: string
  qty: number
  lotNo: string | null
  note: string | null
}

/** Nội dung phiếu xuất bán nội bộ. */
export interface SaveSaleInput {
  date: string
  fromWarehouseId: string
  buyerCompanyId: string
  invoiceNo: string | null
  invoiceDate: string | null
  deliverer: string | null
  note: string | null
  lines: SaveSaleLineInput[]
}

/** Lập, sửa, ghi sổ phiếu xuất bán nội bộ. */
export const useIntercompanySaleMutations = () => useInventoryDocumentMutations<SaveSaleInput>('/api/intercompany-sales')

/** Bên nhận xác nhận phiếu nhập mua nội bộ. */
export interface ConfirmReceiptInput {
  date: string
  note: string | null
  lines: { lineId: string; receivedQty: number; lotNo: string | null; note: string | null }[]
}

/** Xác nhận (lưu), ghi sổ phiếu nhập mua nội bộ – lập mới không có (phiếu do bên bán sinh). */
export const useIntercompanyReceiptMutations = () => useInventoryDocumentMutations<ConfirmReceiptInput>('/api/intercompany-receipts')
