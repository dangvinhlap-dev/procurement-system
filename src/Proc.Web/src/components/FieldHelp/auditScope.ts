/** Bản ghi gắn với một vùng màn hình để bấm F1 xem lịch sử (design.md mục 9.3a). */
export interface AuditScope {
  /** Mã loại đối tượng trong nhật ký, ví dụ PurchaseOrder, InventoryDocument, Supplier. */
  entityType: string
  /** Khóa bản ghi; null/undefined = chưa lưu. */
  id?: string | null
  /** Đổi tên ô sang tên trường khi khác nhau ({ tên ô: tên trường }); null = ô không thuộc bản ghi này. */
  fields?: Record<string, string | null>
}

/**
 * Thuộc tính data-* gắn lên phần tử bao vùng của bản ghi – auditDom.ts đọc lại khi bấm F1.
 * @param scope Bản ghi; undefined thì không gắn gì.
 */
export function auditScopeProps(scope: AuditScope | undefined): Record<string, string> {
  if (!scope) return {}
  return {
    'data-audit-entity': scope.entityType,
    'data-audit-id': scope.id ?? '',
    ...(scope.fields ? { 'data-audit-fields': JSON.stringify(scope.fields) } : {}),
  }
}
