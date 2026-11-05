import { CheckCircleFilled, ClockCircleFilled, CloseCircleFilled, EditOutlined, FieldTimeOutlined, MinusCircleOutlined, SendOutlined } from '@ant-design/icons'
import { ConfigProvider, Empty, Tree } from 'antd'
import type { DataNode } from 'antd/es/tree'
import type { ReactNode } from 'react'
import { nextApproversText, stepStatusLabels, type ApprovalStep } from '../../api/purchasing'
import { formatDateTime } from '../../utils/format'
import './ApprovalHistory.css'

/** Thuộc tính cây lịch sử duyệt. */
interface ApprovalHistoryProps {
  /** Các bước theo lượt rồi cấp. */
  steps: ApprovalStep[]
  /** Người lập phiếu – nút gốc "Lập phiếu". */
  createdByName?: string | null
  /** Thời điểm lập phiếu. */
  createdAt?: string | null
  /** Duyệt nhiều công ty (duyệt NCC – design.md mục 18.6): ghi chặng và công ty trên từng bước. */
  byCompany?: boolean
  /**
   * Tên các chặng phụ ở cùng công ty theo số chặng: 2 = ngoại lệ giá (PO lệch giá – design.md mục 18.10), 3 = vượt ngân sách (PR/PO –
   * mục 18.24). Chặng 1 hiện "Duyệt thường".
   */
  stageLabels?: Record<number, string>
}

/** Biểu tượng kết quả bước – màu theo đèn trạng thái, luôn có chữ đi kèm (ui-style.md mục 2). */
const stepIcon: Record<ApprovalStep['status'], ReactNode> = {
  Approved: <CheckCircleFilled className="approval-tree-ok" />,
  Rejected: <CloseCircleFilled className="approval-tree-no" />,
  Pending: <ClockCircleFilled className="approval-tree-wait" />,
  Skipped: <MinusCircleOutlined className="approval-tree-skip" />,
}

/** Tên những người duyệt được, rê chuột thấy đủ danh sách. */
function Names({ prefix, names }: { prefix: string; names: string[] }) {
  return (
    <span className="approval-next" title={names.join(', ')}>
      {prefix}: {nextApproversText(names)}
    </span>
  )
}

/**
 * Lịch sử duyệt của phiếu dạng cây (yêu cầu người dùng 03/11/2026; tab "Lịch sử duyệt" – design.md mục 10.1): gốc "Lập phiếu" (người, ngày
 * giờ) → từng lượt gửi duyệt (người gửi, ngày giờ) → từng cấp: đã duyệt/từ chối (người, ngày giờ, ý kiến), đang đến lượt ("Chờ duyệt: …"),
 * cấp sau ("Sẽ duyệt: …"). Ai mở được phiếu cũng thấy.
 */
export function ApprovalHistory({ steps, createdByName, createdAt, byCompany = false, stageLabels }: ApprovalHistoryProps) {
  const showStages = !byCompany && !!stageLabels && steps.some((s) => stageLabels[s.stage] !== undefined)
  const rounds = [...new Set(steps.map((s) => s.round))].sort((a, b) => a - b)

  const stepTitle = (s: ApprovalStep): ReactNode => {
    const where = [
      byCompany ? (s.stage === 2 ? 'Tổng công ty (cuối)' : 'Công ty liên quan') : null,
      byCompany ? s.companyCode : null,
      showStages ? (stageLabels?.[s.stage] ?? 'Duyệt thường') : null,
      `Cấp ${s.level}`,
    ].filter(Boolean).join(' · ')
    const role = s.roleName ?? 'Người có quyền duyệt'
    let detail: ReactNode
    if (s.status === 'Approved' || s.status === 'Rejected')
      detail = (
        <>
          {stepStatusLabels[s.status]}: <strong>{s.actedByName}</strong> lúc {formatDateTime(s.actedAt)}
          {s.comment ? <span className="approval-tree-comment"> – “{s.comment}”</span> : null}
        </>
      )
    else if (s.status === 'Pending' && s.nextApprovers) detail = <Names prefix="Chờ duyệt" names={s.nextApprovers} />
    else if (s.status === 'Pending' && s.upcomingApprovers) detail = <Names prefix="Sẽ duyệt" names={s.upcomingApprovers} />
    else detail = stepStatusLabels[s.status]
    return (
      <span>
        {where} ({role}) – {detail}
      </span>
    )
  }

  const treeData: DataNode[] = [
    ...(createdByName || createdAt
      ? [{ key: 'created', icon: <EditOutlined />, title: <span>Lập phiếu: <strong>{createdByName ?? '—'}</strong>{createdAt ? ` lúc ${formatDateTime(createdAt)}` : ''}</span> }]
      : []),
    ...rounds.map((round) => {
      const roundSteps = steps.filter((s) => s.round === round)
      const first = roundSteps[0]
      const sent = first?.submittedAt
        ? <>gửi duyệt{first.submittedByName ? <> bởi <strong>{first.submittedByName}</strong></> : null} lúc {formatDateTime(first.submittedAt)}</>
        : 'gửi duyệt'
      const done = roundSteps.every((s) => s.status === 'Approved')
      return {
        key: `round-${round}`,
        icon: done ? <FieldTimeOutlined /> : <SendOutlined />,
        title: <span>Lượt {round} – {sent}{done ? ' – hoàn thành duyệt' : ''}</span>,
        children: roundSteps.map((s) => ({
          key: `${round}-${s.stage}-${s.companyCode ?? ''}-${s.level}`,
          icon: stepIcon[s.status],
          title: stepTitle(s),
          isLeaf: true,
        })),
      }
    }),
  ]

  if (steps.length === 0 && !createdByName) return <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa gửi duyệt." />
  return (
    <div className="approval-tree" aria-label="Cây lịch sử phê duyệt">
      {/* defaultExpandAll chỉ áp lúc dựng lần đầu – lịch sử tải về sau (gửi duyệt, duyệt) thì dựng lại để mọi lượt luôn mở sẵn. */}
      {/* Form chỉ xem đặt disabled cho mọi component bên trong – cây chỉ để đọc, không cần làm mờ chữ. */}
      <ConfigProvider componentDisabled={false}>
      <Tree
        key={steps.map((s) => `${s.round}.${s.level}.${s.status}`).join('|')}
        showLine
        showIcon
        blockNode
        selectable={false}
        defaultExpandAll
        treeData={treeData}
      />
      </ConfigProvider>
      {steps.length === 0 && <div className="approval-tree-empty">Chưa gửi duyệt.</div>}
    </div>
  )
}

/**
 * Nội dung popup trước khi gửi duyệt: các cấp sẽ duyệt và người duyệt được từng cấp (yêu cầu người dùng 03/11/2026).
 * @param steps Các bước của lượt sẽ gửi (từ API xem trước).
 * @param approvedImmediately Dưới mọi ngưỡng – gửi là duyệt ngay.
 * @param byCompany Duyệt nhiều công ty – ghi công ty trên từng cấp.
 */
export function ApprovalChain({
  steps,
  approvedImmediately,
  byCompany = false,
  hideIntro = false,
}: {
  steps: ApprovalStep[]
  approvedImmediately: boolean
  byCompany?: boolean
  /** Bỏ câu mở đầu (dùng trong popup duyệt NCC – nhiều khối). */
  hideIntro?: boolean
}) {
  // Chặng 2 cùng công ty = chặng duyệt ngoại lệ (PO lệch giá – design.md mục 18.10); duyệt NCC thì chặng 2 là Tổng công ty.
  const staged = steps.some((s) => s.stage === 2)
  const stageLabel = (s: ApprovalStep) =>
    !staged ? '' : byCompany ? (s.stage === 2 ? 'Tổng công ty (cuối) · ' : 'Công ty liên quan · ') : s.stage === 2 ? 'Chặng ngoại lệ · ' : 'Duyệt thường · '
  if (approvedImmediately) return <p>Phiếu dưới mọi ngưỡng duyệt đã cấu hình – gửi là được duyệt ngay, không cần ai duyệt.</p>
  return (
    <div className="approval-chain">
      {!hideIntro && <p>Phiếu chuyển sang Chờ duyệt, không sửa được nữa trừ khi bị từ chối. Người duyệt lần lượt:</p>}
      <ol>
        {steps.map((s) => {
          const names = s.nextApprovers ?? s.upcomingApprovers ?? []
          return (
            <li key={`${s.stage}-${s.companyCode ?? ''}-${s.level}`}>
              {stageLabel(s)}
              {byCompany && s.companyCode ? `${s.companyCode} · ` : ''}Cấp {s.level} ({s.roleName ?? 'người có quyền duyệt'}):{' '}
              <span className={names.length === 0 ? 'approval-chain-missing' : undefined} title={names.join(', ')}>
                {nextApproversText(names)}
              </span>
            </li>
          )
        })}
      </ol>
    </div>
  )
}

/**
 * Nội dung popup trước khi lưu hồ sơ NCC có thay đổi cần duyệt (design.md mục 18.6): mỗi yêu cầu duyệt sẽ sinh một khối – nội dung đổi và
 * người duyệt từng chặng/công ty/cấp (yêu cầu người dùng 03/11/2026).
 * @param groups Các yêu cầu từ API xem trước.
 */
export function ApprovalGroups({ groups }: { groups: { title: string; approvedImmediately: boolean; steps: ApprovalStep[] }[] }) {
  return (
    <div className="approval-chain">
      <p>Thay đổi sau chưa áp ngay mà thành yêu cầu duyệt (công ty liên quan, rồi Tổng công ty):</p>
      {groups.map((g) => (
        <div key={g.title} className="approval-group">
          <strong>{g.title}</strong>
          <ApprovalChain steps={g.steps} approvedImmediately={g.approvedImmediately} byCompany hideIntro />
        </div>
      ))}
    </div>
  )
}

/**
 * Nội dung popup khi duyệt: người lập, người gửi và ai duyệt tiếp sau mình (yêu cầu người dùng 03/11/2026).
 * @param steps Lịch sử duyệt của phiếu.
 * @param createdByName Người lập.
 */
export function ApproveInfo({ steps, createdByName }: { steps: ApprovalStep[]; createdByName?: string | null }) {
  const round = Math.max(0, ...steps.map((s) => s.round))
  const current = steps.filter((s) => s.round === round)
  const after = current.filter((s) => s.status === 'Pending' && s.upcomingApprovers)
  const sent = current[0]
  return (
    <div className="approval-chain">
      <p>
        Người lập: <strong>{createdByName ?? '—'}</strong>
        {sent?.submittedAt ? <> · gửi duyệt lúc {formatDateTime(sent.submittedAt)}</> : null}
      </p>
      {after.length === 0 ? (
        <p>Bạn duyệt cấp cuối – duyệt xong phiếu hoàn thành phê duyệt.</p>
      ) : (
        <>
          <p>Sau bạn, phiếu chuyển tiếp:</p>
          <ol>
            {after.map((s) => (
              <li key={`${s.stage}-${s.companyCode ?? ''}-${s.level}`}>
                Cấp {s.level} ({s.roleName ?? 'người có quyền duyệt'}): {nextApproversText(s.upcomingApprovers)}
              </li>
            ))}
          </ol>
        </>
      )}
    </div>
  )
}
