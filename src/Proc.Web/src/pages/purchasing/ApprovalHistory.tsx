import { Table } from 'antd'
import { stepStatusLabels, type ApprovalStep } from '../../api/purchasing'
import { formatDateTime } from '../../utils/format'

/**
 * Bảng lịch sử duyệt của phiếu (tab "Lịch sử duyệt" – design.md mục 10.1): lượt gửi, cấp, vai trò, kết quả, người, thời điểm, ý kiến.
 * @param steps Các bước theo lượt rồi cấp.
 * @param byCompany Duyệt nhiều công ty (duyệt NCC – design.md mục 18.6): hiện thêm cột Chặng và Công ty.
 */
export function ApprovalHistory({ steps, byCompany = false }: { steps: ApprovalStep[]; byCompany?: boolean }) {
  return (
    <Table<ApprovalStep>
      size="small"
      pagination={false}
      rowKey={(s) => `${s.round}-${s.stage}-${s.companyCode ?? ''}-${s.level}`}
      dataSource={steps}
      locale={{ emptyText: 'Chưa gửi duyệt.' }}
      columns={[
        { title: 'Lượt', dataIndex: 'round', width: 60 },
        ...(byCompany
          ? [
              { title: 'Chặng', dataIndex: 'stage', width: 150, render: (v: number) => (v === 2 ? 'Tổng công ty (cuối)' : 'Công ty liên quan') },
              { title: 'Công ty', dataIndex: 'companyCode', width: 90 },
            ]
          : []),
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
