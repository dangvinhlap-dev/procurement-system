import { Alert } from 'antd'
import type { BudgetCheck } from '../../api/purchasing'
import { formatMoney } from '../../utils/format'

/**
 * Thông báo ngân sách ở đầu PR/PO (design.md mục 18.24). Vượt ngân sách: cảnh báo vàng (không chặn – gửi duyệt qua thêm chặng "Vượt
 * ngân sách"); trong ngân sách: chỉ hiện khi phiếu còn nháp để người lập biết còn lại bao nhiêu. Số liệu tính lúc lưu/gửi gần nhất.
 * @param budget Kết quả kiểm tra của phiếu; null = bộ phận không kiểm soát ngân sách tháng đó.
 * @param draft Phiếu còn nháp (số liệu sẽ tính lại khi lưu, gửi duyệt).
 */
export function BudgetNotice({ budget, draft }: { budget: BudgetCheck | null | undefined; draft: boolean }) {
  if (!budget) return null
  const period = `${String(budget.month).padStart(2, '0')}/${budget.year}`
  const figures = `ngân sách ${formatMoney(budget.budgetAmount)}, đã dùng ${formatMoney(budget.usedAmount)}, phiếu này ${formatMoney(budget.documentAmount)}`
  const when = draft ? 'tính lúc lưu, tính lại khi gửi duyệt' : 'lúc gửi duyệt'
  if (budget.isOver)
    return (
      <Alert
        type="warning"
        showIcon
        title={`Vượt ngân sách bộ phận tháng ${period}: ${figures} – vượt ${formatMoney(budget.overAmount)} (${when}). Phiếu phải qua thêm chặng duyệt vượt ngân sách.`}
      />
    )
  if (!draft) return null
  const remaining = budget.budgetAmount - budget.usedAmount - budget.documentAmount
  return <Alert type="info" showIcon title={`Ngân sách bộ phận tháng ${period}: ${figures}, còn lại ${formatMoney(remaining)} (${when}).`} />
}
