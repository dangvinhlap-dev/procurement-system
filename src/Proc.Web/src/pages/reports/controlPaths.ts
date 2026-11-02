/**
 * Đường dẫn mở chứng từ liên quan của dấu hiệu bất thường/vi phạm KSNB (design.md mục 18.19).
 * @param screenCode Mã màn hình: PO, GRN (mở trang phiếu), SUPAPR (ngăn kéo yêu cầu duyệt NCC), PRCW (báo cáo mua ngoài hợp đồng).
 * @param targetId Chứng từ cần mở.
 * @returns Đường dẫn; null nếu không mở được.
 */
export function controlRefPath(screenCode: string | null, targetId: string | null): string | null {
  switch (screenCode) {
    case 'PO':
      return targetId ? `/mua-hang/po/${targetId}` : null
    case 'GRN':
      return targetId ? `/kho/grn/${targetId}` : null
    case 'SUPAPR':
      return targetId ? `/gia-ncc/duyet-ncc?mo=${targetId}` : '/gia-ncc/duyet-ncc'
    case 'PRCW':
      return '/bao-cao/gia-mua-ngoai-hop-dong'
    default:
      return null
  }
}
