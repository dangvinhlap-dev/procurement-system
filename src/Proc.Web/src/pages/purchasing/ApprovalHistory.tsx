import { Table } from 'antd'
import { nextApproversText, stepStatusLabels, type ApprovalStep } from '../../api/purchasing'
import { formatDateTime } from '../../utils/format'

/**
 * Bảng lịch sử duyệt của phiếu (tab "Lịch sử duyệt" – design.md mục 10.1): lượt gửi, cấp, vai trò, kết quả, người, thời điểm, ý kiến.
 * Bước đang đến lượt hiện "Chờ: <người duyệt tiếp theo>" ở cột Người duyệt (design.md mục 18.22).
 * @param steps Các bước theo lượt rồi cấp.
 * @param byCompany Duyệt nhiều công ty (duyệt NCC – design.md mục 18.6): hiện thêm cột Chặng và Công ty.
 * @param exceptionStage Tên chặng 2 khi phiếu có chặng duyệt ngoại lệ ở cùng công ty (PO lệch giá – design.md mục 18.10); cột Chặng
 *   chỉ hiện khi có bước chặng 2.
 */
export function ApprovalHistory({ steps, byCompany = false, exceptionStage }: { steps: ApprovalStep[]; byCompany?: boolean; exceptionStage?: string }) {
  const showException = !byCompany && !!exceptionStage && steps.some((s) => s.stage === 2)
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
        ...(showException
          ? [{ title: 'Chặng', dataIndex: 'stage', width: 130, render: (v: number) => (v === 2 ? exceptionStage : 'Duyệt thường') }]
          : []),
        { title: 'Cấp', dataIndex: 'level', width: 60 },
        { title: 'Vai trò duyệt', dataIndex: 'roleName', width: 180, render: (v: string | null) => v ?? 'Người có quyền duyệt' },
        { title: 'Kết quả', dataIndex: 'status', width: 110, render: (v: ApprovalStep['status']) => stepStatusLabels[v] },
        {
          title: 'Người duyệt',
          key: 'actedBy',
          width: 220,
          // Bước đang đến lượt chưa có người duyệt: nêu những người duyệt được (người duyệt tiếp theo – TC-4) để biết cần nhắc ai.
          render: (_: unknown, s: ApprovalStep) =>
            s.actedByName ??
            (s.nextApprovers ? (
              // Đủ danh sách ở chú thích khi rê chuột (ô chỉ hiện 5 tên đầu).
              <span className="approval-next" title={s.nextApprovers.join(', ')}>
                Chờ: {nextApproversText(s.nextApprovers)}
              </span>
            ) : null),
        },
        { title: 'Thời điểm', dataIndex: 'actedAt', width: 140, render: (v: string | null) => formatDateTime(v) },
        { title: 'Ý kiến', dataIndex: 'comment' },
      ]}
    />
  )
}
