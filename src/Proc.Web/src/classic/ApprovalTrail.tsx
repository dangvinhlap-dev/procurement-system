import { AuditOutlined, LockFilled, UnlockOutlined } from '@ant-design/icons'
import { Button, ConfigProvider, Modal, Table } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useState } from 'react'
import { nextApproversText, stepStatusLabels, type ApprovalStep } from '../api/purchasing'
import { formatDateTime } from '../utils/format'

/** Dữ liệu phê duyệt của một chứng từ – dùng cho dải ổ khóa và "Kiểm tra phê duyệt" (design.md mục 20.1 R3, R4). */
export interface ApprovalTrailInfo {
  /** Các bước duyệt mọi lượt (từ API, theo lượt rồi chặng, cấp). */
  steps: ApprovalStep[]
  /** Người lập – bước 0 khi phiếu chưa từng gửi duyệt. */
  createdByName?: string | null
  /** Thời điểm lập phiếu. */
  createdAt?: string | null
  /** Duyệt nhiều công ty (duyệt NCC – design.md mục 18.6): ghi công ty trên từng bước. */
  byCompany?: boolean
  /** Tên chặng phụ theo số chặng (như ApprovalHistory) – ví dụ PO: 2 = "Ngoại lệ giá". Chặng 1 là "Duyệt thường". */
  stageLabels?: Record<number, string>
}

/** Tên bước: "Cấp 2", kèm chặng / công ty khi phiếu có nhiều chặng. */
function stepName(s: ApprovalStep, steps: ApprovalStep[], byCompany: boolean, stageLabels?: Record<number, string>): string {
  const staged = !byCompany && !!stageLabels && steps.some((x) => stageLabels[x.stage] !== undefined)
  return [
    byCompany ? (s.stage === 2 ? 'Tổng công ty' : 'Công ty liên quan') : null,
    byCompany ? s.companyCode : null,
    staged ? (stageLabels?.[s.stage] ?? 'Duyệt thường') : null,
    `Cấp ${s.level}`,
  ]
    .filter(Boolean)
    .join(' · ')
}

/** Các bước của lượt gửi duyệt gần nhất – đúng chuỗi đang áp cho phiếu. */
function latestRound(steps: ApprovalStep[]): ApprovalStep[] {
  const round = Math.max(0, ...steps.map((s) => s.round))
  return steps.filter((s) => s.round === round)
}

/**
 * Dải ổ khóa vàng trên form chứng từ (design.md mục 20.1 R3): mỗi bước duyệt của lượt gần nhất là một ổ khóa – **đóng** khi bước đã duyệt,
 * **mở** khi chưa duyệt (chờ, từ chối, bỏ qua). Rê chuột thấy cấp, kết quả, người duyệt, thời điểm – màu không là thông tin duy nhất.
 * Phiếu chưa gửi duyệt thì không vẽ gì.
 */
export function ApprovalLocks({ steps, byCompany = false, stageLabels }: Omit<ApprovalTrailInfo, 'createdByName' | 'createdAt'>) {
  const current = latestRound(steps)
  if (current.length === 0) return null
  const done = current.filter((s) => s.status === 'Approved').length
  return (
    <span className="erp-approval-locks" role="list" aria-label={`Phê duyệt: ${done}/${current.length} bước đã duyệt`}>
      {current.map((s) => {
        const closed = s.status === 'Approved'
        const who =
          s.status === 'Approved' || s.status === 'Rejected'
            ? ` – ${s.actedByName ?? ''} lúc ${formatDateTime(s.actedAt)}`
            : s.nextApprovers
              ? ` – chờ: ${nextApproversText(s.nextApprovers)}`
              : ''
        const text = `${stepName(s, current, byCompany, stageLabels)}: ${stepStatusLabels[s.status]}${who}`
        return (
          <span
            key={`${s.stage}-${s.companyCode ?? ''}-${s.level}`}
            role="listitem"
            className={`erp-approval-lock ${closed ? 'is-closed' : 'is-open'}`}
            title={text}
            aria-label={text}
            data-status={s.status}
          >
            {closed ? <LockFilled /> : <UnlockOutlined />}
          </span>
        )
      })}
    </span>
  )
}

/** Một dòng trên bảng "Kiểm tra phê duyệt". */
interface CheckRow {
  key: string
  round: number
  /** 0 = lập/gửi phê duyệt; 1…n = các cấp. */
  step: number
  content: string
  result: string
  userName: string | null
  fullName: string | null
  position: string | null
  at: string | null
  comment: string | null
}

/**
 * Dựng các dòng "Kiểm tra phê duyệt": mỗi lượt gửi có Bước 0 (người gửi phê duyệt; dữ liệu cũ không có người gửi thì là người lập) rồi
 * Bước 1…n theo thứ tự duyệt. Phiếu chưa gửi duyệt chỉ có Bước 0 "Lập phiếu".
 */
function buildRows({ steps, createdByName, createdAt, byCompany = false, stageLabels }: ApprovalTrailInfo): CheckRow[] {
  if (steps.length === 0)
    return [
      { key: 'created', round: 0, step: 0, content: 'Lập phiếu', result: 'Chưa gửi phê duyệt', userName: null, fullName: createdByName ?? null,
        position: null, at: createdAt ?? null, comment: null },
    ]
  const rows: CheckRow[] = []
  for (const round of [...new Set(steps.map((s) => s.round))].sort((a, b) => a - b)) {
    const roundSteps = steps.filter((s) => s.round === round)
    const first = roundSteps[0]
    rows.push({
      key: `${round}-0`, round, step: 0,
      content: first.submittedAt ? 'Gửi phê duyệt' : 'Lập phiếu',
      result: 'Đã gửi',
      userName: first.submittedByUserName ?? null,
      fullName: first.submittedByName ?? (first.submittedAt ? null : (createdByName ?? null)),
      position: first.submittedByPosition ?? null,
      at: first.submittedAt ?? (round === 1 ? (createdAt ?? null) : null),
      comment: null,
    })
    roundSteps.forEach((s, i) => {
      const acted = s.status === 'Approved' || s.status === 'Rejected'
      const waiting = s.nextApprovers ? `: ${nextApproversText(s.nextApprovers)}` : s.upcomingApprovers ? ` (sẽ duyệt: ${nextApproversText(s.upcomingApprovers)})` : ''
      rows.push({
        key: `${round}-${s.stage}-${s.companyCode ?? ''}-${s.level}`, round, step: i + 1,
        content: `${stepName(s, roundSteps, byCompany, stageLabels)} (${s.roleName ?? 'người có quyền duyệt'})`,
        result: acted ? stepStatusLabels[s.status] : `${stepStatusLabels[s.status]}${s.status === 'Pending' ? waiting : ''}`,
        userName: acted ? (s.actedByUserName ?? null) : null,
        fullName: acted ? (s.actedByName ?? null) : null,
        position: acted ? (s.actedByPosition ?? null) : null,
        at: acted ? s.actedAt : null,
        comment: s.comment,
      })
    })
  }
  return rows
}

/**
 * Nút "Kiểm tra phê duyệt" (design.md mục 20.1 R4): mở hộp thoại liệt kê từng dòng Bước 0 (người lập/gửi phê duyệt) → Bước 1 … bước cuối,
 * mỗi dòng có tên đăng nhập, họ tên, chức vụ (chưa nhập thì là tên vai trò), giờ ngày, ý kiến – ai mở được phiếu cũng xem được.
 * Phiếu gửi duyệt nhiều lượt (bị từ chối rồi gửi lại) thì có cột Lượt.
 */
export function ApprovalCheckButton(props: ApprovalTrailInfo) {
  const [open, setOpen] = useState(false)
  const rows = buildRows(props)
  const manyRounds = new Set(rows.map((r) => r.round)).size > 1
  const columns: ColumnsType<CheckRow> = [
    ...(manyRounds ? [{ key: 'round', title: 'Lượt', width: 48, align: 'right' as const, render: (_: unknown, r: CheckRow) => r.round }] : []),
    { key: 'step', title: 'Bước', width: 52, render: (_, r) => `Bước ${r.step}` },
    { key: 'content', title: 'Nội dung', width: 220, dataIndex: 'content' },
    { key: 'result', title: 'Kết quả', width: 150, dataIndex: 'result' },
    { key: 'userName', title: 'Tên đăng nhập', width: 120, dataIndex: 'userName' },
    { key: 'fullName', title: 'Họ tên', width: 160, dataIndex: 'fullName' },
    { key: 'position', title: 'Chức vụ', width: 150, dataIndex: 'position' },
    { key: 'at', title: 'Giờ, ngày', width: 120, render: (_, r) => formatDateTime(r.at) },
    { key: 'comment', title: 'Ý kiến', dataIndex: 'comment' },
  ]
  return (
    <>
      {/* Form chỉ xem đặt disabled cho mọi component bên trong – xem phê duyệt luôn bấm được. */}
      <ConfigProvider componentDisabled={false}>
        <Button icon={<AuditOutlined />} onClick={() => setOpen(true)} title="Kiểm tra phê duyệt: ai đã lập, gửi, duyệt phiếu">
          Kiểm tra phê duyệt
        </Button>
        <Modal
          open={open}
          title="Kiểm tra phê duyệt"
          onCancel={() => setOpen(false)}
          footer={<Button onClick={() => setOpen(false)}>Đóng</Button>}
          width={1100}
          destroyOnHidden
        >
          <Table<CheckRow>
            size="small"
            bordered
            pagination={false}
            rowKey="key"
            columns={columns}
            dataSource={rows}
            aria-label="Bảng kiểm tra phê duyệt"
            scroll={{ x: 1000 }}
          />
        </Modal>
      </ConfigProvider>
    </>
  )
}
