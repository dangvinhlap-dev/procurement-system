import { Alert, App, List, Modal, Tag, Typography } from 'antd'
import { useState } from 'react'
import { useRoleMutations, useRoleTemplates } from '../../api/administration'

/**
 * Hộp thoại tạo bộ vai trò mẫu Ban Cung ứng (design.md mục 18.6): liệt kê vai trò mẫu, vai trò nào đã có thì giữ nguyên (không sửa
 * quyền người quản trị đã chỉnh), chỉ tạo vai trò còn thiếu. Sau khi tạo, gán vai trò cho người dùng ở màn hình Người dùng.
 * @param open Đang mở.
 * @param onClose Đóng hộp thoại.
 */
export function RoleTemplatesModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { message } = App.useApp()
  const templates = useRoleTemplates(open)
  const { createFromTemplates } = useRoleMutations()
  const [error, setError] = useState<string | null>(null)
  const missing = (templates.data ?? []).filter((t) => !t.exists).length

  const apply = async () => {
    setError(null)
    try {
      const result = await createFromTemplates.mutateAsync()
      message.success(result.created.length > 0 ? `Đã tạo ${result.created.length} vai trò: ${result.created.join(', ')}.` : 'Không có vai trò mới cần tạo.')
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không tạo được vai trò mẫu.')
    }
  }

  return (
    <Modal
      title="Bộ vai trò mẫu Ban Cung ứng"
      open={open}
      onCancel={onClose}
      onOk={apply}
      okText={missing > 0 ? `Tạo ${missing} vai trò còn thiếu` : 'Đã có đủ'}
      okButtonProps={{ disabled: missing === 0 }}
      cancelText="Đóng"
      confirmLoading={createFromTemplates.isPending}
      destroyOnHidden
      width={640}
    >
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Typography.Paragraph type="secondary">
        Theo cơ cấu đề xuất và sơ đồ tổ chức Ban, Phòng cung ứng chung: người tìm NCC, đề xuất giá, đặt hàng không tự duyệt; Hội đồng
        (Trưởng ban, Phó ban) và Chủ tịch/TGĐ duyệt; thành viên chuyên môn thẩm định, không duyệt; Kiểm soát nội bộ chỉ xem. Vai trò đã có
        giữ nguyên quyền hiện tại. Tạo xong có thể áp cấu hình duyệt mẫu ở Hệ thống → Quy trình duyệt.
      </Typography.Paragraph>
      <List
        size="small"
        loading={templates.isLoading}
        dataSource={templates.data ?? []}
        renderItem={(t) => (
          <List.Item extra={<Tag color={t.exists ? 'default' : 'processing'}>{t.exists ? 'Đã có' : 'Sẽ tạo'}</Tag>}>
            <List.Item.Meta
              title={`${t.code} – ${t.name}`}
              description={`${t.description} (${t.permissionCodes.length} quyền)`}
            />
          </List.Item>
        )}
      />
    </Modal>
  )
}
