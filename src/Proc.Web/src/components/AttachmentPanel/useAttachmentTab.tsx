import { useAttachments, type AttachmentOwnerType } from '../../api/attachments'
import type { DocumentTab } from '../DocumentForm/DocumentForm'
import { AttachmentPanel } from './AttachmentPanel'
import { usePendingAttachments } from './usePendingAttachments'

/**
 * Tab "Đính kèm (n)" cho DocumentForm hoặc ngăn kéo – một dòng ở mỗi màn hình thay vì lặp lại khối tab. Mọi form chứng từ/phiếu phải có
 * tab này (quy tắc người dùng 03/11/2026, design.md mục 18.4). Hàng chờ file của phiếu đang tạo giữ ở đây (cấp trang) nên đổi tab không
 * mất; lưu phiếu xong tự tải lên.
 * @param ownerType Loại đối tượng chủ.
 * @param ownerId Id đối tượng chủ (chưa lưu thì không có).
 * @param canManage Được tải lên, xóa.
 * @returns Mục tab, kèm <c>uploadTo</c> để form bị dựng lại khi có Id (ngăn kéo có key theo Id) tải hàng chờ ngay trong hàm lưu.
 */
export function useAttachmentTab(
  ownerType: AttachmentOwnerType,
  ownerId: string | undefined,
  canManage: boolean,
): DocumentTab & { uploadTo: (ownerId: string) => Promise<void> } {
  const { data } = useAttachments(ownerType, ownerId)
  const pending = usePendingAttachments(ownerType, ownerId)
  const count = (data?.length ?? 0) + pending.items.length
  return {
    key: 'attachments',
    label: count ? `Đính kèm (${count})` : 'Đính kèm',
    children: <AttachmentPanel ownerType={ownerType} ownerId={ownerId} canManage={canManage} pending={pending} />,
    uploadTo: pending.uploadTo,
  }
}
