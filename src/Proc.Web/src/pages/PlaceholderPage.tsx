import { Empty, Typography } from 'antd'
import { useLocation } from 'react-router'
import { findScreenByPath } from '../app/screens'

/**
 * Trang tạm cho màn hình đã có trên menu nhưng chưa làm, hoặc đường dẫn không tồn tại.
 */
export function PlaceholderPage() {
  const screen = findScreenByPath(useLocation().pathname)
  return (
    <div className="simple-page">
      <Typography.Title level={4}>{screen?.title ?? 'Không tìm thấy màn hình'}</Typography.Title>
      <Empty description={screen ? 'Màn hình này đang được xây dựng.' : 'Đường dẫn không tồn tại.'} />
    </div>
  )
}
