import { WarningFilled } from '@ant-design/icons'
import { Alert, Checkbox, DatePicker, Flex, Modal, Radio, Table, Tooltip } from 'antd'
import type { Dayjs } from 'dayjs'
import { useMemo, useState } from 'react'
import { usePlannedQuantities, type PlannedQuantity } from '../../api/demandPlans'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DATE_INPUT_FORMATS, formatQuantity } from '../../utils/format'
import type { PlannedQuantityLine } from './plannedLines'

/** SL áp dụng cho một dòng: SL mới, kèm ĐVT mới khi đổi dòng sang ĐVT cơ sở. */
export interface PlannedQuantityChange {
  qty: number
  uomId?: string
}

/**
 * Cách xử lý SL lẻ theo ĐVT dòng (ví dụ 50 cái, dòng theo thùng 12 cái = 4,1667 thùng): làm tròn lên số nguyên ĐVT dòng (5 thùng), đổi dòng
 * sang ĐVT cơ sở (50 cái, cơ sở còn lẻ thì cũng làm tròn lên), hoặc giữ số lẻ 4 chữ số.
 */
type FractionMode = 'ceil' | 'base' | 'exact'

/** Dòng của bảng xem trước: kết quả server kèm vị trí, SL hiện tại và SL sẽ áp dụng. */
type PreviewRow = PlannedQuantity & { index: number; current: number | null; fractional: boolean; change: PlannedQuantityChange | null }

/** SL theo ĐVT dòng có lẻ không (server trả 4 số lẻ nên so số nguyên là đủ). */
const isFractional = (qty: number | null) => qty != null && !Number.isInteger(qty)

/** SL sẽ chép vào dòng theo cách xử lý SL lẻ; null khi không có kế hoạch. */
function changeOf(r: PlannedQuantity, mode: FractionMode, allowBaseUom: boolean): PlannedQuantityChange | null {
  if (r.qty == null) return null
  if (!isFractional(r.qty) || mode === 'exact') return { qty: r.qty }
  if (mode === 'base' && allowBaseUom && r.baseUomId && r.baseQty != null && r.baseUomId !== r.uomId) {
    return { qty: Math.ceil(r.baseQty), uomId: r.baseUomId }
  }
  return { qty: Math.ceil(r.qty) }
}

/**
 * Mô tả cách tính khoảng: số tháng chạm tới, tháng đầu / cuối không trọn thì ghi số ngày được tính (ví dụ "tháng 01/2027 tính 17/31 ngày").
 * @param from Ngày đầu.
 * @param to Ngày cuối.
 */
function rangeText(from: Dayjs, to: Dayjs): string {
  const months = (to.year() - from.year()) * 12 + to.month() - from.month() + 1
  const part = (start: Dayjs, end: Dayjs) => `${start.format('MM/YYYY')} tính ${end.diff(start, 'day') + 1}/${start.daysInMonth()} ngày`
  const notes: string[] = []
  if (months === 1) {
    if (from.date() !== 1 || to.date() !== to.daysInMonth()) notes.push(`tháng ${part(from, to)}`)
  } else {
    if (from.date() !== 1) notes.push(`tháng đầu ${part(from, from.endOf('month'))}`)
    if (to.date() !== to.daysInMonth()) notes.push(`tháng cuối ${part(to.startOf('month'), to)}`)
  }
  return `Tính ${months} tháng (${from.format('DD/MM/YYYY')} – ${to.format('DD/MM/YYYY')})${
    notes.length > 0 ? `; ${notes.join(', ')} – kế hoạch theo tháng nên chia theo số ngày` : ', trọn tháng'
  }.`
}

/**
 * Hộp thoại "Lấy SL kế hoạch" (design.md mục 18.25, lát G5c): tổng SL các tháng trong khoảng ngày của phiếu kế hoạch nhu cầu Đã chốt (tháng
 * đầu / cuối không trọn chia theo số ngày), quy về ĐVT từng dòng; SL lẻ theo ĐVT dòng thì báo và xử lý theo lựa chọn (làm tròn lên, đổi sang
 * ĐVT cơ sở, giữ số lẻ). Xem trước SL hiện tại ↔ SL áp dụng, chọn dòng rồi áp dụng (người dùng vẫn phải Lưu phiếu). Dòng không có kế hoạch không
 * chọn được.
 * @param title Tiêu đề, ví dụ "Lấy SL kế hoạch làm sản lượng dự kiến".
 * @param lines Các dòng đã chọn mặt hàng và ĐVT.
 * @param defaultFrom Ngày đầu mặc định.
 * @param defaultTo Ngày cuối mặc định.
 * @param defaultAllCompanies Mặc định cộng toàn tập đoàn (chỉ có tác dụng khi có quyền xem kế hoạch nhu cầu toàn tập đoàn).
 * @param allowBaseUom Cho đổi dòng sang ĐVT cơ sở khi SL lẻ (RFQ được; hợp đồng không vì đơn giá theo ĐVT của bảng giá).
 * @param onApply Nhận SL (và ĐVT mới nếu đổi) theo vị trí dòng.
 * @param onClose Đóng hộp thoại.
 */
export function PlannedQuantityDialog({
  title,
  lines,
  defaultFrom,
  defaultTo,
  defaultAllCompanies,
  allowBaseUom,
  onApply,
  onClose,
}: {
  title: string
  lines: PlannedQuantityLine[]
  defaultFrom: Dayjs
  defaultTo: Dayjs
  defaultAllCompanies: boolean
  allowBaseUom: boolean
  onApply: (changes: Map<number, PlannedQuantityChange>) => void
  onClose: () => void
}) {
  const { hasPermission } = useAuth()
  const canViewAll = hasPermission(Permission.DemandPlanViewAllCompanies)
  const [from, setFrom] = useState<Dayjs | null>(defaultFrom)
  const [to, setTo] = useState<Dayjs | null>(defaultTo)
  const [allCompanies, setAllCompanies] = useState(defaultAllCompanies && canViewAll)
  // Mặc định làm tròn lên: SL dự kiến / mời thầu là số nguyên ĐVT giao dịch; người lập chọn cách khác nếu cần.
  const [mode, setMode] = useState<FractionMode>('ceil')
  // null = chọn sẵn các dòng có SL áp dụng khác SL đang nhập; đổi khoảng ngày / phạm vi / cách xử lý SL lẻ thì quay về chọn sẵn.
  const [picked, setPicked] = useState<number[] | null>(null)
  const query = useMemo(
    () =>
      from && to
        ? {
            fromDate: from.format('YYYY-MM-DD'),
            toDate: to.format('YYYY-MM-DD'),
            allCompanies,
            lines: lines.map((l) => ({ itemId: l.itemId, uomId: l.uomId })),
          }
        : null,
    [from, to, allCompanies, lines],
  )
  const result = usePlannedQuantities(query)
  const rows = useMemo<PreviewRow[]>(
    () =>
      (result.data ?? []).map((r, i) => ({
        ...r,
        index: lines[i].index,
        current: lines[i].qty,
        fractional: isFractional(r.qty),
        change: changeOf(r, mode, allowBaseUom),
      })),
    [result.data, lines, mode, allowBaseUom],
  )
  const selected =
    picked ?? rows.filter((r) => r.change != null && (r.change.qty !== r.current || r.change.uomId != null)).map((r) => r.index)
  const fractionalCount = rows.filter((r) => r.fractional).length
  const uomCodeOf = (r: PreviewRow) => (r.change?.uomId ? r.baseUomCode : r.uomCode)

  return (
    <Modal
      open
      title={title}
      width={1100}
      okText={`Áp dụng cho ${selected.length} dòng`}
      okButtonProps={{ disabled: selected.length === 0 }}
      cancelText="Đóng"
      onCancel={onClose}
      onOk={() => {
        onApply(new Map(rows.filter((r) => selected.includes(r.index) && r.change != null).map((r) => [r.index, r.change!])))
        onClose()
      }}
    >
      <Flex gap={12} align="center" wrap style={{ marginBottom: 8 }}>
        <label htmlFor="planned-from">Từ ngày</label>
        <DatePicker
          id="planned-from"
          format={DATE_INPUT_FORMATS}
          value={from}
          onChange={(v) => { setFrom(v); setPicked(null) }}
          allowClear={false}
        />
        <label htmlFor="planned-to">Đến ngày</label>
        <DatePicker id="planned-to" format={DATE_INPUT_FORMATS} value={to} onChange={(v) => { setTo(v); setPicked(null) }} allowClear={false} />
        {canViewAll && (
          <Checkbox checked={allCompanies} onChange={(e) => { setAllCompanies(e.target.checked); setPicked(null) }}>
            Toàn tập đoàn
          </Checkbox>
        )}
      </Flex>
      <Alert
        type={result.error ? 'error' : 'info'}
        showIcon
        style={{ marginBottom: 8 }}
        title={
          result.error?.message ??
          `${from && to && !to.isBefore(from) ? `${rangeText(from, to)} ` : ''}Cộng phiếu kế hoạch nhu cầu đã chốt (${
            allCompanies ? 'toàn tập đoàn' : 'công ty đang làm việc'
          }), quy về ĐVT của dòng. Áp dụng xong hãy lưu phiếu.`
        }
      />
      {fractionalCount > 0 && (
        <Flex gap={8} align="center" wrap style={{ marginBottom: 8 }}>
          <span>
            <WarningFilled className="line-grid-alert-icon" /> {fractionalCount} dòng có SL lẻ theo ĐVT dòng:
          </span>
          <Radio.Group value={mode} onChange={(e) => { setMode(e.target.value as FractionMode); setPicked(null) }}>
            <Radio value="ceil">Làm tròn lên số nguyên</Radio>
            {allowBaseUom && <Radio value="base">Đổi dòng sang ĐVT cơ sở</Radio>}
            <Radio value="exact">Giữ số lẻ</Radio>
          </Radio.Group>
        </Flex>
      )}
      <Table<PreviewRow>
        size="small"
        rowKey="index"
        loading={result.isLoading}
        pagination={false}
        scroll={{ y: 360 }}
        dataSource={rows}
        rowSelection={{
          selectedRowKeys: selected,
          onChange: (keys) => setPicked(keys as number[]),
          getCheckboxProps: (r) => ({ disabled: r.change == null, 'aria-label': `Chọn dòng ${r.index + 1}` }),
        }}
        columns={[
          { title: 'Dòng', key: 'line', width: 50, render: (_, r) => r.index + 1 },
          { title: 'Mặt hàng', key: 'item', render: (_, r) => `${r.itemCode} – ${r.itemName}` },
          { title: 'ĐVT', dataIndex: 'uomCode', width: 60 },
          { title: 'SL hiện tại', key: 'current', width: 95, align: 'right', render: (_, r) => formatQuantity(r.current) },
          {
            title: 'SL kế hoạch',
            key: 'planned',
            width: 115,
            align: 'right',
            render: (_, r) =>
              r.qty == null ? (
                'Không có kế hoạch'
              ) : r.fractional ? (
                <Tooltip title={`Không chẵn ${r.uomCode}: ${formatQuantity(r.qty)} ${r.uomCode} = ${formatQuantity(r.baseQty)} ${r.baseUomCode}`}>
                  <WarningFilled className="line-grid-alert-icon" aria-label={`SL lẻ dòng ${r.index + 1}`} /> {formatQuantity(r.qty)}
                </Tooltip>
              ) : (
                formatQuantity(r.qty)
              ),
          },
          {
            title: 'SL theo ĐVT cơ sở',
            key: 'base',
            width: 130,
            align: 'right',
            render: (_, r) => (r.baseQty == null ? '' : `${formatQuantity(r.baseQty)} ${r.baseUomCode}`),
          },
          {
            title: 'SL áp dụng',
            key: 'apply',
            width: 115,
            align: 'right',
            render: (_, r) => (r.change == null ? '' : `${formatQuantity(r.change.qty)} ${uomCodeOf(r)}`),
          },
          {
            title: 'Theo công ty',
            key: 'companies',
            width: 260,
            render: (_, r) => r.companies.map((c) => `${c.companyCode}: ${formatQuantity(c.qty)} (${c.planNos.join(', ')})`).join('; '),
          },
        ]}
      />
    </Modal>
  )
}
