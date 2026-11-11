import { Alert } from 'antd'
import type { ConstructionBudget } from '../../api/purchasing'
import { formatMoney } from '../../utils/format'

/**
 * Khung chi phí công trình ở đầu đề nghị mua loại A (biểu mẫu 1A – design.md mục 19.3, Q4): dự toán, lũy kế đã mua theo PO đã duyệt (chưa VAT,
 * kèm số có VAT), còn lại. Phiếu nháp / chờ duyệt cộng thêm tổng dự kiến của chính phiếu: vượt dự toán thì cảnh báo vàng (không chặn – Q5).
 * @param construction Khung chi phí; null = không phải loại A.
 */
export function ConstructionBudgetNotice({ construction }: { construction: ConstructionBudget | null | undefined }) {
  if (!construction) return null
  const c = construction
  const figures =
    `Công trình ${c.code} – ${c.name}: dự toán ${formatMoney(c.estimateAmount)}, lũy kế đã mua ${formatMoney(c.spentAmount)} ` +
    `(gồm VAT ${formatMoney(c.spentAmountWithVat)}), còn lại ${formatMoney(c.remainingAmount)}`
  const request = c.requestAmount > 0 ? `; phiếu này ${formatMoney(c.requestAmount)}` : ''
  if (c.overEstimate)
    return (
      <Alert
        type="warning"
        showIcon
        title={`Vượt dự toán – ${figures}${request}, vượt ${formatMoney(c.spentAmount + c.requestAmount - c.estimateAmount)}. Giá chưa VAT.`}
      />
    )
  return <Alert type="info" showIcon title={`${figures}${request}. Giá chưa VAT.`} />
}
