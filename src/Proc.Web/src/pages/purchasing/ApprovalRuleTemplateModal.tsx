import { Alert, App, Form, Modal, Table, Tag, Typography } from 'antd'
import { useState } from 'react'
import {
  useApprovalRuleMutations,
  useApprovalRuleTemplate,
  type ApprovalRuleTemplate,
  type ApprovalRuleTemplateSet,
  type ApprovalRuleTemplateStatus,
} from '../../api/purchasing'
import { Select } from '../../classic/Select'

/** Nhãn và màu thẻ tình trạng (thẻ có chữ – màu không là thông tin duy nhất). */
const statusTag: Record<ApprovalRuleTemplateStatus, { label: string; color: string }> = {
  Create: { label: 'Sẽ tạo', color: 'processing' },
  Configured: { label: 'Đã cấu hình – giữ nguyên', color: 'default' },
  MissingRole: { label: 'Thiếu vai trò mẫu', color: 'warning' },
  HeadOfficeOnly: { label: 'Chỉ ở tổng công ty', color: 'default' },
}

/** Các bộ mẫu để chọn. */
const setOptions: { value: ApprovalRuleTemplateSet; label: string }[] = [
  { value: 'SODO', label: 'Theo sơ đồ phê duyệt của Ban cung ứng' },
  { value: 'CUVT', label: 'Theo biểu mẫu CUVT (A, B, DV)' },
]

/** Giải thích từng bộ mẫu. */
const setNote: Record<ApprovalRuleTemplateSet, string> = {
  SODO:
    'Theo sơ đồ phê duyệt mua sắm của Ban và Phòng cung ứng chung: PO qua 02 Phó ban rồi Chủ tịch/TGĐ; ngoại lệ (ngoài danh sách/giá, lệch '
    + 'giá, khẩn cấp, độc quyền) qua Hội đồng rồi Chủ tịch/TGĐ; duyệt NCC & giá tháng qua 3 thành viên Hội đồng (thay biểu quyết).',
  CUVT:
    'Theo chuỗi ký của biểu mẫu CUVT: đề nghị mua 1A/1B/DV qua Trưởng phòng ban → Phòng CUVT → TC-NQ-KT → GĐ khối → Phó CT UBCL&PT → '
    + 'Chủ tịch; so sánh/trình giá 3a/3A/3B và PO 4A/4B qua Phòng CUVT, TC-NQ-KT rồi Chủ tịch. Trưởng phòng ban, GĐ khối là người duyệt động '
    + '– khai ở danh mục bộ phận, khối. Bước Thủ kho và Trưởng phòng HC (nhóm B) thêm khi đề nghị mua có loại mua. Áp bộ này trước rồi áp bộ '
    + 'theo sơ đồ để có thêm các chặng ngoại lệ, hợp đồng.',
}

/**
 * Hộp thoại cấu hình duyệt mẫu (theo sơ đồ phê duyệt mua sắm – lát G10, mục 18.25; theo biểu mẫu CUVT – lát O3, mục 19.3): chọn bộ mẫu, xem
 * trước từng cấp (loại phiếu, cấp, vai trò hoặc người duyệt động, căn cứ, tình trạng) rồi áp cho công ty đang làm việc. Chỉ tạo loại phiếu
 * chưa cấu hình, không sửa cấu hình đã có; ngưỡng 0 – người quản trị sửa ngưỡng theo phân quyền nội bộ sau khi áp.
 * @param open Đang mở.
 * @param companyCode Mã công ty đang làm việc.
 * @param canManage Có quyền sửa tham số (được áp).
 * @param onClose Đóng hộp thoại.
 */
export function ApprovalRuleTemplateModal({ open, companyCode, canManage, onClose }: {
  open: boolean
  companyCode: string
  canManage: boolean
  onClose: () => void
}) {
  const { message } = App.useApp()
  const [set, setSet] = useState<ApprovalRuleTemplateSet>('SODO')
  const template = useApprovalRuleTemplate(open, set)
  const { applyTemplate } = useApprovalRuleMutations()
  const [error, setError] = useState<string | null>(null)
  const rows = template.data ?? []
  const toCreate = rows.filter((r) => r.status === 'Create').length
  const missingRole = rows.some((r) => r.status === 'MissingRole')

  const apply = async () => {
    setError(null)
    try {
      const result = await applyTemplate.mutateAsync(set)
      const source = set === 'CUVT' ? 'theo biểu mẫu CUVT' : 'theo sơ đồ'
      message.success(result.created > 0 ? `Đã tạo ${result.created} cấp duyệt ${source}.` : 'Không có cấp duyệt mới cần tạo.')
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không áp được cấu hình duyệt mẫu.')
    }
  }

  return (
    <Modal
      title={`Cấu hình duyệt mẫu – ${companyCode}`}
      open={open}
      onCancel={onClose}
      onOk={apply}
      okText={toCreate > 0 ? `Tạo ${toCreate} cấp duyệt` : 'Không có cấp cần tạo'}
      okButtonProps={{ disabled: !canManage || toCreate === 0 }}
      cancelText="Đóng"
      confirmLoading={applyTemplate.isPending}
      destroyOnHidden
      width={1100}
    >
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form layout="horizontal" style={{ marginBottom: 4 }}>
        <Form.Item label="Bộ mẫu" style={{ marginBottom: 4 }}>
          <Select<ApprovalRuleTemplateSet> aria-label="Bộ mẫu" value={set} onChange={setSet} options={setOptions} style={{ width: 320 }} showSearch={false} />
        </Form.Item>
      </Form>
      <Typography.Paragraph type="secondary">
        {setNote[set]} Chỉ tạo loại phiếu chưa có cấp nào, không sửa cấu hình đã có; mọi cấp ngưỡng 0 – sửa ngưỡng theo phân quyền nội bộ sau khi tạo.
      </Typography.Paragraph>
      {missingRole && (
        <Alert type="warning" showIcon style={{ marginBottom: 8 }}
          title="Một số loại chưa tạo được vì thiếu vai trò mẫu – vào Hệ thống → Vai trò → Bộ vai trò mẫu… để tạo trước." />
      )}
      <Table<ApprovalRuleTemplate>
        size="small"
        pagination={false}
        loading={template.isLoading}
        rowKey={(r) => `${r.docType}-${r.level}`}
        dataSource={rows}
        tableLayout="fixed"
        scroll={{ y: 420 }}
        columns={[
          { title: 'Loại phiếu', key: 'type', width: 250, ellipsis: { showTitle: true }, render: (_, r) => `${r.docType} – ${r.typeName}` },
          { title: 'Cấp', dataIndex: 'level', width: 50, align: 'right' },
          {
            title: 'Người duyệt', key: 'role', width: 290, ellipsis: { showTitle: true },
            // Người duyệt động không có mã vai trò – hiện tên loại người duyệt như trên cấu hình APRL.
            render: (_, r) => (r.roleCode === null ? r.roleName : r.roleName ? `${r.roleCode} – ${r.roleName}` : r.roleCode),
          },
          { title: 'Căn cứ', dataIndex: 'note', ellipsis: { showTitle: true } },
          {
            title: 'Tình trạng', dataIndex: 'status', width: 190,
            render: (v: ApprovalRuleTemplateStatus) => <Tag color={statusTag[v].color}>{statusTag[v].label}</Tag>,
          },
        ]}
      />
    </Modal>
  )
}
