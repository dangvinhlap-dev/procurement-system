/**
 * Đường dẫn mở chứng từ liên quan của dấu hiệu bất thường/vi phạm KSNB (design.md mục 18.19).
 * @param screenCode Mã màn hình: PO, XNH (phiếu xác nhận nhận hàng – từ 0.22.0), GRN (phiếu nhập kho cũ), SUPAPR (trang yêu cầu duyệt NCC), PRCW (báo cáo mua ngoài hợp đồng),
 *   CTR (hợp đồng nguyên tắc – chiết khấu/tài trợ chưa ghi nhận).
 * @param targetId Chứng từ cần mở.
 * @returns Đường dẫn; null nếu không mở được.
 */
export function controlRefPath(screenCode: string | null, targetId: string | null): string | null {
  switch (screenCode) {
    case 'PO':
      return targetId ? `/mua-hang/po/${targetId}` : null
    case 'XNH':
      return targetId ? `/mua-hang/xac-nhan-nhan-hang/${targetId}` : null
    case 'GRN':
      return targetId ? `/kho/grn/${targetId}` : null
    case 'SUPAPR':
      return targetId ? `/gia-ncc/duyet-ncc/${targetId}` : '/gia-ncc/duyet-ncc'
    case 'CTR':
      return targetId ? `/mua-hang/hop-dong-khung/${targetId}` : null
    case 'CKB':
      // Chiết khấu sản lượng gộp chưa ghi nhận (lát G6b) – mở thỏa thuận để xem tab Thực nhận.
      return targetId ? `/gia-ncc/chiet-khau-san-luong/${targetId}` : '/gia-ncc/chiet-khau-san-luong'
    case 'PRCW':
      return '/bao-cao/gia-mua-ngoai-hop-dong'
    default:
      return null
  }
}
