import { supplierStatusLabels, itemTypeLabels } from '../../api/masterData'
import { conflictRelationLabels } from '../../api/conflicts'
import { amendmentStatusLabels, contractStatusLabels } from '../../api/contracts'
import { priceListStatusLabels } from '../../api/pricing'
import { priceSourceLabels, priorityLabels, purchaseStatusLabels, purchaseTypeLabels, stepStatusLabels } from '../../api/purchasing'
import { candidateStatusLabels, rfqStatusLabels, sourcingStatusLabels } from '../../api/sourcing'
import { changeKindLabels, changeStatusLabels } from '../../api/supplierApprovals'
import type { FieldChange, FieldValueKind } from '../../api/changeHistory'
import { formatDate, formatDateTime } from '../../utils/format'

/**
 * Nhãn tiếng Việt của giá trị liệt kê lưu trong nhật ký (nhật ký ghi tên giá trị, ví dụ "Emergency"), theo "Loại.Trường" hoặc "Trường".
 * Thiếu thì hiện nguyên tên giá trị.
 */
const enumLabels: Record<string, Record<string, string>> = {
  'PurchaseRequest.Status': purchaseStatusLabels,
  'PurchaseOrder.Status': purchaseStatusLabels,
  'PurchaseContract.Status': contractStatusLabels,
  'PurchaseContractAmendment.Status': amendmentStatusLabels,
  'Supplier.Status': supplierStatusLabels,
  'SupplierPriceList.Status': priceListStatusLabels,
  'SourcingRequest.Status': sourcingStatusLabels,
  'SourcingCandidate.Status': candidateStatusLabels,
  'Rfq.Status': rfqStatusLabels,
  'SupplierChangeRequest.Status': changeStatusLabels,
  'SupplierChangeRequest.Kind': changeKindLabels,
  'ApprovalStep.Status': stepStatusLabels,
  'Item.Type': itemTypeLabels,
  PurchaseType: purchaseTypeLabels,
  Priority: priorityLabels,
  PriceSource: priceSourceLabels,
  Relation: conflictRelationLabels,
}

/** Nhãn hay gặp của trường (khi màn hình không có ô mang tên trường đó – ví dụ dòng đã xóa, trường hệ thống tự tính). */
const commonFieldLabels: Record<string, string> = {
  Code: 'Mã',
  Name: 'Tên',
  No: 'Số phiếu',
  Date: 'Ngày chứng từ',
  Status: 'Trạng thái',
  Note: 'Ghi chú',
  LineNo: 'Số dòng',
  CompanyId: 'Công ty',
  PeriodId: 'Kỳ kho',
  DocumentId: 'Chứng từ',
  OrderId: 'Đơn mua hàng',
  RequestId: 'Đề nghị mua hàng',
  ContractId: 'Hợp đồng',
  PurchaseOrderId: 'Đơn mua hàng',
  PurchaseOrderLineId: 'Dòng đơn mua hàng',
  SourcePrLineId: 'Dòng PR nguồn',
  SourceLineId: 'Dòng phiếu nguồn',
  LinkedDocumentId: 'Chứng từ liên kết',
  CounterCompanyId: 'Công ty đối ứng',
  PriceListId: 'Bảng giá',
  CreatedBy: 'Người tạo',
  UpdatedBy: 'Người sửa',
  DocsCompletedAt: 'Thời điểm bổ sung hồ sơ',
  DocsCompletedBy: 'Người bổ sung hồ sơ',
  DocsCompletionNote: 'Ghi chú bổ sung hồ sơ',
  SupplierId: 'Nhà cung cấp',
  WarehouseId: 'Kho',
  FromWarehouseId: 'Kho xuất',
  ToWarehouseId: 'Kho nhập',
  DepartmentId: 'Bộ phận',
  ItemId: 'Mặt hàng',
  CategoryId: 'Nhóm hàng',
  UomId: 'ĐVT',
  Qty: 'Số lượng',
  BaseQty: 'SL theo ĐVT cơ sở',
  UomFactor: 'Hệ số quy đổi',
  UnitPrice: 'Đơn giá',
  UnitCost: 'Đơn giá vốn',
  DiscountPct: 'Chiết khấu (%)',
  DiscountAmount: 'Tiền chiết khấu',
  GrossAmount: 'Tiền trước chiết khấu',
  Amount: 'Thành tiền',
  VatCode: 'Thuế GTGT',
  VatAmount: 'Tiền thuế GTGT',
  TotalAmount: 'Tổng tiền hàng',
  TotalVat: 'Tổng thuế GTGT',
  TotalDiscount: 'Tổng chiết khấu',
  CurrencyCode: 'Tiền tệ',
  DeliveryDate: 'Ngày giao',
  ExpectedDate: 'Ngày giao dự kiến',
  RequiredDate: 'Ngày cần',
  PaymentTerms: 'Điều khoản thanh toán',
  DeliveryTerms: 'Điều khoản giao hàng',
  DeliveryAddress: 'Địa chỉ giao',
  ReceiveTolerancePct: 'Dung sai nhận hàng (%)',
  PurchaseType: 'Loại mua',
  EmergencyReason: 'Lý do khẩn cấp',
  DocsDueDate: 'Hạn bổ sung hồ sơ',
  Priority: 'Mức ưu tiên',
  Purpose: 'Mục đích',
  RejectReason: 'Lý do từ chối',
  ApprovedAt: 'Thời điểm duyệt',
  ApprovedBy: 'Người duyệt',
  SubmittedAt: 'Thời điểm gửi duyệt',
  PostedAt: 'Thời điểm ghi sổ',
  PostedBy: 'Người ghi sổ',
  InvoiceNo: 'Số hóa đơn',
  InvoiceSeries: 'Ký hiệu hóa đơn',
  InvoiceDate: 'Ngày hóa đơn',
  InvoiceAmount: 'Tiền hóa đơn',
  QcResult: 'Kết quả kiểm tra',
  TaxCode: 'Mã số thuế',
  Address: 'Địa chỉ',
  Phone: 'Điện thoại',
  Email: 'Email',
  IsActive: 'Đang dùng',
  ValidFrom: 'Hiệu lực từ',
  ValidTo: 'Hiệu lực đến',
}

/**
 * Nhãn của trường: nhãn đang hiện trên màn hình (theo tên ô), nhãn hay gặp, hoặc tên trường.
 * @param field Tên trường, ví dụ SupplierId.
 * @param screenLabels Nhãn ô trên màn hình theo tên ô viết thường (collectFieldLabels).
 */
export function fieldLabel(field: string, screenLabels: Record<string, string> = {}): string {
  const lower = field.toLowerCase()
  return screenLabels[lower] ?? screenLabels[lower.replace(/id$/, '')] ?? commonFieldLabels[field] ?? field
}

/**
 * Chuỗi hiển thị của một giá trị trong lịch sử theo quy ước Việt Nam (CLAUDE.md mục 5).
 * @param change Lần thay đổi (để biết loại đối tượng, trường, kiểu).
 * @param side Giá trị cũ hay mới.
 * @returns Chuỗi; rỗng khi trống.
 */
export function changeValueText(change: FieldChange, side: 'old' | 'new'): string {
  const value = side === 'old' ? change.oldValue : change.newValue
  const text = side === 'old' ? change.oldText : change.newText
  return formatValue(value, change.kind, `${change.entityType}.${change.field}`, change.field, text)
}

/**
 * Định dạng một giá trị thô theo kiểu.
 * @param value Giá trị thô (số dạng 1234.5, ngày ISO, tên giá trị liệt kê…).
 * @param kind Kiểu giá trị.
 * @param qualifiedField "Loại.Trường" để tra nhãn giá trị liệt kê.
 * @param field Tên trường.
 * @param referenceText Mã – tên của ô tham chiếu.
 */
export function formatValue(
  value: string | null,
  kind: FieldValueKind,
  qualifiedField: string,
  field: string,
  referenceText?: string | null,
): string {
  if (value == null) return ''
  switch (kind) {
    case 'reference':
      return referenceText ?? value
    case 'number':
      return Number(value).toLocaleString('vi-VN', { maximumFractionDigits: 6 })
    case 'date':
      return formatDate(value)
    case 'datetime':
      return formatDateTime(value)
    case 'bool':
      return value === 'true' ? 'Có' : 'Không'
    case 'enum':
      return (enumLabels[qualifiedField] ?? enumLabels[field])?.[value] ?? value
    default:
      return value
  }
}

/**
 * Tên thao tác như ERP: Tạo, Sửa, Xóa giá trị (sửa thành trống), Nhập giá trị (từ trống), Xóa (cả bản ghi/dòng).
 * @param change Lần thay đổi.
 */
export function actionLabel(change: FieldChange): string {
  if (change.action === 'Create') return 'Tạo'
  if (change.action === 'Delete') return 'Xóa'
  if (change.newValue == null) return 'Xóa giá trị'
  if (change.oldValue == null) return 'Nhập giá trị'
  return 'Sửa'
}

/** Người thao tác: "tên đăng nhập – họ tên"; null là hệ thống (khởi tạo dữ liệu, job nền). */
export function userLabel(change: Pick<FieldChange, 'userName' | 'userFullName'>): string {
  if (!change.userName) return 'Hệ thống'
  return change.userFullName ? `${change.userName} – ${change.userFullName}` : change.userName
}
