import { WarningFilled } from '@ant-design/icons'
import { Alert } from 'antd'
import type { PurchaseRequestLine } from '../../api/purchasing'
import { formatQuantity } from '../../utils/format'

/**
 * Kỳ lũy kế của định mức dạng "01–02/2027" (từ tháng 1 đến hết tháng cần hàng của dòng).
 * @param through Ngày cuối tháng cần hàng (yyyy-MM-dd).
 */
function normPeriod(through: string | null): string {
  if (!through) return ''
  const [year, month] = through.split('-')
  return month === '01' ? `01/${year}` : `01–${month}/${year}`
}

/**
 * Chú thích định mức của một dòng (rê chuột): kỳ lũy kế, đã đề nghị / định mức theo ĐVT cơ sở.
 * @param line Dòng PR đã lưu.
 */
function usageNormTitle(line: PurchaseRequestLine): string {
  if (line.normStatus === 'NoPlan')
    return `Mặt hàng không có trong kế hoạch nhu cầu đã chốt của nhóm hàng (năm ${line.normThrough?.slice(0, 4) ?? ''}).`
  return `Lũy kế ${normPeriod(line.normThrough)}: đã đề nghị ${formatQuantity(line.normRequestedQty)} / định mức ${formatQuantity(line.normQty)} ${line.baseUomCode} (kế hoạch nhu cầu đã chốt; gồm PR khác đã gửi, đã duyệt và các dòng của phiếu này).`
}

/**
 * Ô "Định mức LK" trên lưới dòng PR (lát G11, design.md mục 18.25): "đã đề nghị / định mức" theo ĐVT cơ sở; vượt hoặc ngoài kế hoạch có
 * tam giác vàng. Trống khi chưa lưu hoặc nhóm hàng chưa có kế hoạch nhu cầu đã chốt (không kiểm tra).
 * @param line Dòng PR đã lưu (undefined khi dòng chưa lưu).
 * @param index Chỉ số dòng trong lưới (cho nhãn đọc màn hình).
 */
export function UsageNormCell({ line, index }: { line: PurchaseRequestLine | undefined; index: number }) {
  if (!line?.normStatus) return <span className="line-grid-readonly" />
  const warn = line.normStatus !== 'Ok'
  const text = line.normStatus === 'NoPlan' ? 'Ngoài kế hoạch' : `${formatQuantity(line.normRequestedQty)} / ${formatQuantity(line.normQty)}`
  return (
    <span className={warn ? 'line-grid-readonly line-grid-warning' : 'line-grid-readonly'} title={usageNormTitle(line)} data-testid={`norm-${index + 1}`}>
      {warn && <WarningFilled className="line-grid-alert-icon" aria-label={`Định mức dòng ${index + 1}`} />} {text}
    </span>
  )
}

/**
 * Cảnh báo đầu PR khi có dòng vượt định mức sử dụng hoặc ngoài kế hoạch nhu cầu (lát G11). Chỉ cảnh báo – không chặn gửi, duyệt; người
 * duyệt thấy cùng thông báo. Số liệu tính lúc mở phiếu.
 * @param lines Dòng PR đã lưu.
 */
export function UsageNormNotice({ lines }: { lines: PurchaseRequestLine[] | undefined }) {
  const over = (lines ?? []).filter((l) => l.normStatus === 'Over')
  const noPlan = (lines ?? []).filter((l) => l.normStatus === 'NoPlan')
  if (over.length === 0 && noPlan.length === 0) return null
  const parts: string[] = []
  if (over.length > 0)
    parts.push(
      `Vượt định mức sử dụng: ${over
        .map((l) => `dòng ${l.lineNo} ${l.itemCode} đã đề nghị ${formatQuantity(l.normRequestedQty)} / định mức ${formatQuantity(l.normQty)} ${l.baseUomCode} (lũy kế ${normPeriod(l.normThrough)})`)
        .join('; ')}.`,
    )
  if (noPlan.length > 0) parts.push(`Ngoài kế hoạch nhu cầu: ${noPlan.map((l) => `dòng ${l.lineNo} ${l.itemCode}`).join(', ')}.`)
  return <Alert type="warning" showIcon data-testid="usage-norm-notice" title={`${parts.join(' ')} Định mức căn cứ kế hoạch nhu cầu đã chốt của công ty – chỉ cảnh báo.`} />
}
