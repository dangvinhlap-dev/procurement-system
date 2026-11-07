import { Alert } from 'antd'
import type { ItemCategory } from '../../api/masterData'

/**
 * Cảnh báo trùng tên nhóm hàng khi thêm/sửa (yêu cầu người dùng 07/11/2026): trùng trong cùng nhóm cha – không lưu được (máy chủ chặn);
 * trùng tên ở nhánh khác – nhắc kiểm tra kỹ. Kết quả từ `sameNameCategories`.
 * @param sameParent Nhóm cùng tên trong cùng nhóm cha.
 * @param elsewhere Nhóm cùng tên ở nhánh khác.
 */
export function CategoryDuplicateNotice({ sameParent, elsewhere }: { sameParent: ItemCategory | undefined; elsewhere: ItemCategory[] }) {
  if (sameParent)
    return (
      <Alert
        type="error"
        showIcon
        style={{ marginBottom: 8 }}
        title={`Đã có nhóm ${sameParent.code} – ${sameParent.name}${sameParent.isActive ? '' : ' (ngừng dùng – dùng lại nhóm này)'} trong cùng nhóm cha – không tạo trùng được.`}
      />
    )
  if (elsewhere.length === 0) return null
  return (
    <Alert
      type="warning"
      showIcon
      style={{ marginBottom: 8 }}
      title={`Đã có nhóm cùng tên ở nhánh khác: ${elsewhere.map((c) => `${c.code} (${c.path})`).join('; ')}. Kiểm tra kỹ trước khi tạo để tránh trùng.`}
    />
  )
}
