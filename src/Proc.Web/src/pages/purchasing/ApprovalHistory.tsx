import { Table } from 'antd'
import { stepStatusLabels, type ApprovalStep } from '../../api/purchasing'
import { formatDateTime } from '../../utils/format'

/**
 * Bảng lịch sử duyệt của phiếu (tab "Lịch sử duyệt" – design.md mục 10.1): lượt gửi, cấp, vai trò, kết quả, người, thời điểm, ý kiến.
 * @param steps Các bước theo lượt rồi cấp.
 */
export function ApprovalHistory({ steps }: { steps: ApprovalStep[] }) {
  return (
    <Table<ApprovalStep>
      size="small"
      pagination={false}
      rowKey={(s) => `${s.round}-${s.level}`}
      dataSource={steps}
      locale={{ emptyText: 'Chưa gửi duyệt.' }}
      columns={[
        { title: 'Lượt', dataIndex: 'round', width: 60 },
        { title: 'Cấp', dataIndex: 'level', width: 60 },
        { title: 'Vai trò duyệt', dataIndex: 'roleName', width: 180, render: (v: string | null) => v ?? 'Người có quyền duyệt' },
        { title: 'Kết quả', dataIndex: 'status', width: 110, render: (v: ApprovalStep['status']) => stepStatusLabels[v] },
        { title: 'Người duyệt', dataIndex: 'actedByName', width: 180 },
        { title: 'Thời điểm', dataIndex: 'actedAt', width: 140, render: (v: string | null) => formatDateTime(v) },
        { title: 'Ý kiến', dataIndex: 'comment' },
      ]}
    />
  )
}
