import { Alert, InputNumber, Modal, Table } from 'antd'
import type { TableColumnsType } from 'antd'
import { useMemo, useState } from 'react'
import { useCountSheet, type CountSheetRow } from '../../api/inventoryDocuments'
import { formatDate, formatQuantity, vnNumberInput } from '../../utils/format'

/** Một dòng chênh lệch sinh từ bảng kiểm kê: thực đếm khác tồn sổ. */
export interface CountedDifference {
  row: CountSheetRow
  countedQty: number
}

/** Khóa duy nhất của một dòng phiếu kiểm kê: mặt hàng + số lô. */
const rowKey = (r: CountSheetRow) => `${r.itemId}|${r.lotNo ?? ''}`

/**
 * Hộp thoại bảng kiểm kê (design.md mục 18.23 – ADJ-3): liệt kê tồn sổ hiện tại của kho theo mặt hàng, lô; người kiểm kê gõ SL thực đếm,
 * hộp thoại hiện chênh lệch (thực đếm − tồn sổ, âm là thiếu). Bấm "Lập dòng chênh lệch" chỉ đưa các dòng có chênh lệch vào phiếu;
 * server tính lại chênh lệch theo tồn sổ lúc lưu, không tin số trên giao diện.
 * @param props.warehouseId Kho kiểm kê (đang chọn ở đầu phiếu).
 * @param props.open Hiện hộp thoại.
 * @param props.onClose Đóng không lập dòng.
 * @param props.onApply Nhận các dòng có chênh lệch để thêm vào phiếu.
 */
export function CountSheetDialog({
  warehouseId,
  open,
  onClose,
  onApply,
}: {
  warehouseId: string | undefined
  open: boolean
  onClose: () => void
  onApply: (differences: CountedDifference[]) => void
}) {
  const sheet = useCountSheet(warehouseId, open)
  const [counted, setCounted] = useState<Record<string, number | null>>({})
  const rows = useMemo(() => sheet.data ?? [], [sheet.data])
  const differences = rows
    .map((row) => ({ row, countedQty: counted[rowKey(row)] }))
    .filter((d): d is CountedDifference => d.countedQty !== null && d.countedQty !== undefined && d.countedQty !== d.row.bookQty)

  const columns: TableColumnsType<CountSheetRow> = [
    { key: 'code', title: 'Mã hàng', dataIndex: 'itemCode', width: 120 },
    { key: 'name', title: 'Tên hàng', dataIndex: 'itemName' },
    { key: 'uom', title: 'ĐVT', dataIndex: 'uomCode', width: 60 },
    { key: 'lot', title: 'Số lô', dataIndex: 'lotNo', width: 100 },
    { key: 'expiry', title: 'Hạn dùng', width: 100, render: (_, r) => (r.expiryDate ? formatDate(r.expiryDate) : '') },
    { key: 'book', title: 'Tồn sổ', width: 100, align: 'right', render: (_, r) => formatQuantity(r.bookQty) },
    {
      key: 'counted',
      title: 'Thực đếm',
      width: 120,
      render: (_, r) => (
        <InputNumber
          {...vnNumberInput}
          size="small"
          min={0}
          precision={4}
          style={{ width: '100%' }}
          aria-label={`Thực đếm ${r.itemCode}${r.lotNo ? ` lô ${r.lotNo}` : ''}`}
          value={counted[rowKey(r)] ?? null}
          onChange={(v) => setCounted((prev) => ({ ...prev, [rowKey(r)]: typeof v === 'number' ? v : null }))}
        />
      ),
    },
    {
      key: 'diff',
      title: 'Chênh lệch',
      width: 100,
      align: 'right',
      render: (_, r) => {
        const value = counted[rowKey(r)]
        if (value === null || value === undefined) return ''
        const diff = Math.round((value - r.bookQty) * 10000) / 10000
        return <span className={diff < 0 ? 'line-grid-warning' : undefined}>{diff > 0 ? `+${formatQuantity(diff)}` : formatQuantity(diff)}</span>
      },
    },
  ]

  return (
    <Modal
      title="Bảng kiểm kê – nhập SL thực đếm"
      open={open}
      width={900}
      okText={`Lập dòng chênh lệch (${differences.length})`}
      cancelText="Hủy"
      okButtonProps={{ disabled: differences.length === 0 }}
      onCancel={onClose}
      onOk={() => {
        onApply(differences)
        setCounted({})
      }}
      afterClose={() => setCounted({})}
      destroyOnHidden
    >
      <Alert
        type="info"
        showIcon
        title="Tồn sổ theo ĐVT cơ sở tại thời điểm mở bảng. Bỏ trống ô Thực đếm là không kiểm mặt hàng đó; chỉ dòng có chênh lệch được lập vào phiếu. Hàng thừa không có trên sổ thì thêm dòng Tăng trên phiếu."
      />
      <Table<CountSheetRow>
        size="small"
        rowKey={rowKey}
        columns={columns}
        dataSource={rows}
        loading={sheet.isLoading}
        pagination={false}
        scroll={{ y: 420 }}
        locale={{ emptyText: sheet.error ? sheet.error.message : 'Kho không có tồn sổ.' }}
        data-testid="count-sheet"
      />
    </Modal>
  )
}
