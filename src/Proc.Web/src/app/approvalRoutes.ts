import type { ApprovalDocType } from '../api/purchasing'

/**
 * Đường dẫn trang đầy đủ của phiếu chờ duyệt theo loại – dùng chung cho màn hình Việc cần làm và trang duyệt gọn trên điện thoại (lát O2d).
 * Loại không có trong bảng thì không mở được từ Việc cần làm.
 */
export const approvalFullPaths: Partial<Record<ApprovalDocType, (id: string) => string>> = {
  PR: (id) => `/mua-hang/pr/${id}`,
  PO: (id) => `/mua-hang/po/${id}`,
  BTH: (id) => `/mua-hang/bang-tong-hop/${id}`,
  PRL: (id) => `/gia-ncc/bang-gia/${id}`,
  RFQ: (id) => `/dau-thau/rfq/${id}?tab=compare`,
  CTR: (id) => `/mua-hang/hop-dong-khung/${id}`,
  CTA: (id) => `/mua-hang/hop-dong-khung/phu-luc/${id}`,
  // Trang đầy đủ (TC-3): duyệt NCC kèm hồ sơ NCC; NCC độc quyền.
  NCC: (id) => `/gia-ncc/duyet-ncc/${id}`,
  NDQ: (id) => `/gia-ncc/ncc-doc-quyen/${id}`,
  GTH: (id) => `/gia-ncc/duyet-thang/${id}`,
  CKB: (id) => `/gia-ncc/chiet-khau-san-luong/${id}`,
}

/**
 * Gốc API của từng loại phiếu chờ duyệt: duyệt = `POST {gốc}/{id}/approve`, từ chối = `POST {gốc}/{id}/reject`, thân `{ comment }` – mọi loại
 * dùng chung `ApprovalActionRequest` phía máy chủ nên trang duyệt gọn (lát O2d) gọi thẳng, không cần API mới. Máy chủ vẫn kiểm tra quyền,
 * lượt duyệt, ủy quyền như khi duyệt trên trang đầy đủ.
 */
export const approvalApiBase: Partial<Record<ApprovalDocType, string>> = {
  PR: '/api/purchase-requests',
  PO: '/api/purchase-orders',
  BTH: '/api/purchase-summaries',
  PRL: '/api/price-lists',
  RFQ: '/api/rfqs',
  CTR: '/api/contracts',
  CTA: '/api/contract-amendments',
  NCC: '/api/supplier-change-requests',
  NDQ: '/api/sole-sources',
  GTH: '/api/monthly-supplier-approvals',
  CKB: '/api/volume-rebates',
}

/**
 * Loại phiếu duyệt được trên trang duyệt gọn: phải có cả trang đầy đủ (xem kỹ khi cần) và API duyệt/từ chối.
 * @param docType Mã loại phiếu (thường lấy từ đường dẫn hoặc mã màn hình thông báo).
 */
export function isMobileApprovable(docType: string): docType is ApprovalDocType {
  return docType in approvalApiBase && docType in approvalFullPaths
}
