import { Alert, App, Form, List, Modal, Tag, Typography } from 'antd'
import { useState } from 'react'
import { ROLE_TEMPLATE_SETS, useRoleMutations, useRoleTemplates, type RoleTemplateSet } from '../../api/administration'
import { Select } from '../../classic/Select'

/** Ghi chú cho từng bộ vai trò mẫu. */
const setNote: Record<RoleTemplateSet, string> = {
  ALL: 'Mọi vai trò mẫu của hai bộ dưới đây.',
  BAN:
    'Theo cơ cấu đề xuất và sơ đồ tổ chức Ban, Phòng cung ứng chung: người tìm NCC, đề xuất giá, đặt hàng không tự duyệt; Hội đồng (Trưởng '
    + 'ban, Phó ban) và Chủ tịch/TGĐ duyệt; thành viên chuyên môn thẩm định, không duyệt; Kiểm soát nội bộ chỉ xem.',
  CUVT:
    'Các vị trí ký trên biểu mẫu CUVT nhóm A, B, DV của từng công ty: người đề xuất, thủ kho, Trưởng phòng ban, Phòng CUVT, TC-NQ-KT, '
    + 'GĐ khối, Phó CT UBCL&PT, Chủ tịch. Trưởng phòng ban và GĐ khối là người duyệt động – khai đúng người ở danh mục bộ phận, khối.',
}

/**
 * Hộp thoại tạo bộ vai trò mẫu (Ban Cung ứng – design.md mục 18.6; vị trí ký biểu mẫu CUVT – lát O3, mục 19.3): chọn bộ, liệt kê vai trò mẫu,
 * vai trò nào đã có thì giữ nguyên (không sửa quyền người quản trị đã chỉnh), chỉ tạo vai trò còn thiếu. Sau khi tạo, gán vai trò cho người
 * dùng ở màn hình Người dùng.
 * @param open Đang mở.
 * @param onClose Đóng hộp thoại.
 */
export function RoleTemplatesModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { message } = App.useApp()
  const [set, setSet] = useState<RoleTemplateSet>('ALL')
  const templates = useRoleTemplates(open, set)
  const { createFromTemplates } = useRoleMutations()
  const [error, setError] = useState<string | null>(null)
  const missing = (templates.data ?? []).filter((t) => !t.exists).length

  const apply = async () => {
    setError(null)
    try {
      const result = await createFromTemplates.mutateAsync(set)
      message.success(result.created.length > 0 ? `Đã tạo ${result.created.length} vai trò: ${result.created.join(', ')}.` : 'Không có vai trò mới cần tạo.')
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không tạo được vai trò mẫu.')
    }
  }

  return (
    <Modal
      title="Bộ vai trò mẫu"
      open={open}
      onCancel={onClose}
      onOk={apply}
      okText={missing > 0 ? `Tạo ${missing} vai trò còn thiếu` : 'Đã có đủ'}
      okButtonProps={{ disabled: missing === 0 }}
      cancelText="Đóng"
      confirmLoading={createFromTemplates.isPending}
      destroyOnHidden
      width={680}
    >
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form layout="horizontal" style={{ marginBottom: 4 }}>
        <Form.Item label="Bộ mẫu" style={{ marginBottom: 4 }}>
          <Select<RoleTemplateSet> aria-label="Bộ mẫu" value={set} onChange={setSet} options={ROLE_TEMPLATE_SETS} style={{ width: 320 }} showSearch={false} />
        </Form.Item>
      </Form>
      <Typography.Paragraph type="secondary">
        {setNote[set]} Vai trò đã có giữ nguyên quyền hiện tại. Tạo xong có thể áp cấu hình duyệt mẫu ở Hệ thống → Quy trình duyệt.
      </Typography.Paragraph>
      <List
        size="small"
        loading={templates.isLoading}
        dataSource={templates.data ?? []}
        style={{ maxHeight: 420, overflowY: 'auto' }}
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
