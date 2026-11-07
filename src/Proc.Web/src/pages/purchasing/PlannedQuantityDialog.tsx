import { Alert, Checkbox, DatePicker, Flex, Modal, Table } from 'antd'
import type { Dayjs } from 'dayjs'
import { useMemo, useState } from 'react'
import { usePlannedQuantities, type PlannedQuantity } from '../../api/demandPlans'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { formatQuantity } from '../../utils/format'
import type { PlannedQuantityLine } from './plannedLines'

/** Dòng của bảng xem trước: kết quả server kèm vị trí và SL hiện tại. */
type PreviewRow = PlannedQuantity & { index: number; current: number | null }

/** Định dạng tháng của ô chọn tháng (MM/yyyy – CLAUDE.md mục 5). */
const MONTH_FORMATS = ['MM/YYYY', 'M/YYYY', 'MMYYYY']

/**
 * Hộp thoại "Lấy SL kế hoạch" (design.md mục 18.25, lát G5c): tổng SL các tháng trong khoảng của phiếu kế hoạch nhu cầu Đã chốt, quy về ĐVT
 * từng dòng; xem trước SL hiện tại ↔ SL kế hoạch, chọn dòng rồi áp dụng (người dùng vẫn phải Lưu phiếu). Dòng không có kế hoạch không chọn được.
 * @param title Tiêu đề, ví dụ "Lấy SL kế hoạch làm sản lượng dự kiến".
 * @param lines Các dòng đã chọn mặt hàng và ĐVT.
 * @param defaultFrom Tháng đầu mặc định.
 * @param defaultTo Tháng cuối mặc định.
 * @param defaultAllCompanies Mặc định cộng toàn tập đoàn (chỉ có tác dụng khi có quyền xem kế hoạch nhu cầu toàn tập đoàn).
 * @param onApply Nhận SL mới theo vị trí dòng.
 * @param onClose Đóng hộp thoại.
 */
export function PlannedQuantityDialog({
  title,
  lines,
  defaultFrom,
  defaultTo,
  defaultAllCompanies,
  onApply,
  onClose,
}: {
  title: string
  lines: PlannedQuantityLine[]
  defaultFrom: Dayjs
  defaultTo: Dayjs
  defaultAllCompanies: boolean
  onApply: (qtyByIndex: Map<number, number>) => void
  onClose: () => void
}) {
  const { hasPermission } = useAuth()
  const canViewAll = hasPermission(Permission.DemandPlanViewAllCompanies)
  const [from, setFrom] = useState<Dayjs | null>(defaultFrom)
  const [to, setTo] = useState<Dayjs | null>(defaultTo)
  const [allCompanies, setAllCompanies] = useState(defaultAllCompanies && canViewAll)
  // null = chọn sẵn các dòng có kế hoạch khác SL đang nhập; đổi khoảng tháng / phạm vi thì quay về chọn sẵn.
  const [picked, setPicked] = useState<number[] | null>(null)
  const query = useMemo(
    () =>
      from && to
        ? {
            fromMonth: from.startOf('month').format('YYYY-MM-DD'),
            toMonth: to.startOf('month').format('YYYY-MM-DD'),
            allCompanies,
            lines: lines.map((l) => ({ itemId: l.itemId, uomId: l.uomId })),
          }
        : null,
    [from, to, allCompanies, lines],
  )
  const result = usePlannedQuantities(query)
  const rows = useMemo<PreviewRow[]>(
    () => (result.data ?? []).map((r, i) => ({ ...r, index: lines[i].index, current: lines[i].qty })),
    [result.data, lines],
  )
  const selected = picked ?? rows.filter((r) => r.qty != null && r.qty !== r.current).map((r) => r.index)

  return (
    <Modal
      open
      title={title}
      width={980}
      okText={`Áp dụng cho ${selected.length} dòng`}
      okButtonProps={{ disabled: selected.length === 0 }}
      cancelText="Đóng"
      onCancel={onClose}
      onOk={() => {
        onApply(new Map(rows.filter((r) => selected.includes(r.index) && r.qty != null).map((r) => [r.index, r.qty!])))
        onClose()
      }}
    >
      <Flex gap={12} align="center" wrap style={{ marginBottom: 8 }}>
        <label htmlFor="planned-from">Từ tháng</label>
        <DatePicker id="planned-from" picker="month" format={MONTH_FORMATS} value={from} onChange={(v) => { setFrom(v); setPicked(null) }} allowClear={false} />
        <label htmlFor="planned-to">Đến tháng</label>
        <DatePicker id="planned-to" picker="month" format={MONTH_FORMATS} value={to} onChange={(v) => { setTo(v); setPicked(null) }} allowClear={false} />
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
          `Tổng SL các tháng trong khoảng của phiếu kế hoạch nhu cầu đã chốt (${allCompanies ? 'toàn tập đoàn' : 'công ty đang làm việc'}), quy về ĐVT của dòng. Áp dụng xong hãy lưu phiếu.`
        }
      />
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
          getCheckboxProps: (r) => ({ disabled: r.qty == null, 'aria-label': `Chọn dòng ${r.index + 1}` }),
        }}
        columns={[
          { title: 'Dòng', key: 'line', width: 55, render: (_, r) => r.index + 1 },
          { title: 'Mặt hàng', key: 'item', render: (_, r) => `${r.itemCode} – ${r.itemName}` },
          { title: 'ĐVT', dataIndex: 'uomCode', width: 60 },
          { title: 'SL hiện tại', key: 'current', width: 100, align: 'right', render: (_, r) => formatQuantity(r.current) },
          {
            title: 'SL kế hoạch',
            key: 'planned',
            width: 110,
            align: 'right',
            render: (_, r) => (r.qty == null ? 'Không có kế hoạch' : formatQuantity(r.qty)),
          },
          {
            title: 'Theo công ty',
            key: 'companies',
            width: 300,
            render: (_, r) => r.companies.map((c) => `${c.companyCode}: ${formatQuantity(c.qty)} (${c.planNos.join(', ')})`).join('; '),
          },
        ]}
      />
    </Modal>
  )
}
