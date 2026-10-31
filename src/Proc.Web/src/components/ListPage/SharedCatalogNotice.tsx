import { Alert } from 'antd'
import type { useSharedCatalogRights } from '../../auth/useSharedCatalogRights'

/**
 * Dòng thông báo trên màn hình danh mục dùng chung khi làm việc ở công ty thành viên: chỉ xem, hay được sửa thông tin (trừ mã).
 * @param rights Kết quả useSharedCatalogRights.
 */
export function SharedCatalogNotice({ rights }: { rights: ReturnType<typeof useSharedCatalogRights> }) {
  if (rights.atHeadOffice) return null
  return (
    <Alert
      type="info"
      showIcon
      title={
        rights.canEdit
          ? 'Danh mục dùng chung toàn tập đoàn: bạn được sửa thông tin (trừ mã); thêm mới làm ở tổng công ty.'
          : 'Danh mục dùng chung toàn tập đoàn: chỉ xem. Thêm mới làm ở tổng công ty; sửa thông tin cần quyền "Sửa thông tin danh mục dùng chung".'
      }
    />
  )
}
