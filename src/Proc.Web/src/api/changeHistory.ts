import { useQuery } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiFetch } from './client'

/*
 * Lịch sử thay đổi theo ô (phím F1) và theo bản ghi (khớp Proc.Application.Logging.ChangeHistory – design.md mục 9.3a).
 */

/** Kiểu giá trị của trường – quyết định cách định dạng giá trị cũ/mới. */
export type FieldValueKind = 'text' | 'number' | 'date' | 'datetime' | 'bool' | 'enum' | 'reference'

/** Thông tin kỹ thuật của ô (tab "Thông tin ô"). */
export interface FieldInfo {
  entityType: string
  entityName: string
  /** null = ô không lưu thẳng vào bản ghi (tính toán, ghép từ ô khác). */
  field: string | null
  table: string | null
  column: string | null
  dataType: string | null
  maxLength: number | null
  required: boolean
  kind: FieldValueKind
  referenceName: string | null
  redacted: boolean
}

/** Người, thời điểm tạo và sửa gần nhất của bản ghi. */
export interface RecordStamp {
  label: string | null
  createdAt: string | null
  createdBy: string | null
  updatedAt: string | null
  updatedBy: string | null
}

/** Một lần đổi giá trị của một trường. */
export interface FieldChange {
  logId: string
  occurredAt: string
  /** null = hệ thống (khởi tạo dữ liệu, job nền). */
  userName: string | null
  userFullName: string | null
  /** Create, Update, Delete. */
  action: string
  entityType: string
  entityName: string
  entityId: string
  entityLabel: string | null
  lineNo: number | null
  field: string
  kind: FieldValueKind
  oldValue: string | null
  newValue: string | null
  /** Ô tham chiếu: mã – tên. */
  oldText: string | null
  newText: string | null
}

/** Kết quả F1 trên một ô. */
export interface FieldHistory {
  info: FieldInfo
  record: RecordStamp
  changes: FieldChange[]
  truncated: boolean
}

/** Lịch sử cả bản ghi kèm dòng con (cả dòng đã xóa). */
export interface RecordHistory {
  entityType: string
  entityName: string
  record: RecordStamp
  changes: FieldChange[]
  truncated: boolean
}

/** Ô cần xem lịch sử: loại đối tượng, khóa bản ghi, tên ô (tên ô ở form, ví dụ supplierId). */
export interface FieldRef {
  entityType: string
  entityId: string
  field: string
}

/**
 * Lịch sử một ô; ref null thì không gọi API.
 * @param ref Ô cần xem.
 */
export function useFieldHistory(ref: FieldRef | null) {
  const companyId = useAuth().workingCompany?.id ?? ''
  const query = ref ? new URLSearchParams({ entityType: ref.entityType, entityId: ref.entityId, field: ref.field }).toString() : ''
  return useQuery({
    queryKey: ['change-history', companyId, 'field', query],
    queryFn: () => apiFetch<FieldHistory>(`/api/change-history/field?${query}`),
    enabled: ref !== null,
    // Lịch sử thay đổi theo từng lần mở: vừa lưu xong bấm F1 phải thấy ngay lần sửa mới.
    staleTime: 0,
    gcTime: 0,
  })
}

/**
 * Lịch sử cả bản ghi (đầu phiếu và dòng); entityId null thì không gọi API.
 * @param entityType Mã loại đối tượng, ví dụ PurchaseOrder.
 * @param entityId Khóa bản ghi.
 */
export function useRecordHistory(entityType: string, entityId: string | null) {
  const companyId = useAuth().workingCompany?.id ?? ''
  const query = entityId ? new URLSearchParams({ entityType, entityId }).toString() : ''
  return useQuery({
    queryKey: ['change-history', companyId, 'record', query],
    queryFn: () => apiFetch<RecordHistory>(`/api/change-history/record?${query}`),
    enabled: entityId !== null,
    staleTime: 0,
    gcTime: 0,
  })
}
