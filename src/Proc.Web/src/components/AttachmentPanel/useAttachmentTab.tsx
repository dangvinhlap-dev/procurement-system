import { useAttachments, type AttachmentOwnerType } from '../../api/attachments'
import type { DocumentTab } from '../DocumentForm/DocumentForm'
import { AttachmentPanel } from './AttachmentPanel'

/**
 * Tab "Đính kèm (n)" cho DocumentForm hoặc ngăn kéo – một dòng ở mỗi màn hình thay vì lặp lại khối tab.
 * @param ownerType Loại đối tượng chủ.
 * @param ownerId Id đối tượng chủ (chưa lưu thì không có).
 * @param canManage Được tải lên, xóa.
 * @returns Mục tab.
 */
export function useAttachmentTab(ownerType: AttachmentOwnerType, ownerId: string | undefined, canManage: boolean): DocumentTab {
  const { data } = useAttachments(ownerType, ownerId)
  return {
    key: 'attachments',
    label: data?.length ? `Đính kèm (${data.length})` : 'Đính kèm',
    children: <AttachmentPanel ownerType={ownerType} ownerId={ownerId} canManage={canManage} />,
  }
}
