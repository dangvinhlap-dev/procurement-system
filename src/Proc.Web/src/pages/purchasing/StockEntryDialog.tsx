import { Alert, Button, InputNumber, Modal, Table } from 'antd'
import { useState } from 'react'
import { useEnterPrStock, type PurchaseRequest, type PurchaseRequestLine } from '../../api/purchasing'
import { DocNo } from '../../classic/DocNo'
import { formatDateTime, formatQuantity, vnNumberInput } from '../../utils/format'

/**
 * Cần mua = max(Cần sử dụng − Tồn, 0) – cùng công thức với máy chủ (lát Y3b, design.md mục 19.3). Ví dụ cần 10, tồn 4 → mua 6; tồn 12 → mua 0.
 * @param required SL cần sử dụng.
 * @param stock Tồn đang nhập; null = chưa nhập (coi như 0).
 */
const needToBuy = (required: number, stock: number | null | undefined) => Math.max(required - (stock ?? 0), 0)

/**
 * Hộp thoại "Nhập tồn kho" của thủ kho nơi nhận (lát Y3b, design.md mục 19.3 – Q2): nhập tồn từng dòng có mã theo ĐVT của dòng, xem ngay SL
 * cần mua; cột gợi ý là tồn đã xác nhận gần nhất cùng mặt hàng, cùng nơi nhận trên đề nghị khác (bấm "Lấy tồn gợi ý" để điền). Mọi dòng phải có
 * số (không có thì 0). Xác nhận xong máy chủ tính lại SL cần mua và tổng giá trị; đã có cấp duyệt thì tồn khóa.
 * @param doc Đề nghị mua đang chờ duyệt.
 * @param onClose Đóng hộp thoại.
 */
export function StockEntryDialog({ doc, onClose }: { doc: PurchaseRequest; onClose: () => void }) {
  const enter = useEnterPrStock()
  // Mặc định: tồn đã nhập ở lượt trước (nếu có), chưa có thì để trống cho thủ kho tự nhập.
  const [stock, setStock] = useState<Record<string, number | null>>(() =>
    Object.fromEntries(doc.lines.map((l) => [l.id, l.stockQty])),
  )
  const [error, setError] = useState<string | null>(null)
  const missing = doc.lines.filter((l) => stock[l.id] == null)
  const hasSuggestion = doc.lines.some((l) => l.lastStockQty != null)

  const confirm = async () => {
    if (missing.length > 0) {
      setError(`Còn ${missing.length} dòng chưa nhập tồn (không có thì nhập 0).`)
      return
    }
    try {
      await enter.mutateAsync({ id: doc.id, lines: doc.lines.map((l) => ({ lineId: l.id, stockQty: stock[l.id]! })) })
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không nhập được tồn.')
    }
  }

  return (
    <Modal
      open
      title={`Nhập tồn kho – ${doc.no}`}
      width={1050}
      okText="Xác nhận tồn"
      cancelText="Đóng"
      okButtonProps={{ loading: enter.isPending }}
      onOk={confirm}
      onCancel={onClose}
      destroyOnHidden
    >
      <Alert
        type={error ? 'error' : 'info'}
        showIcon
        style={{ marginBottom: 8 }}
        title={
          error ??
          `Nơi nhận: ${doc.warehouseName ?? ''}. Nhập tồn thực tế theo ĐVT của dòng; Cần mua = Cần sử dụng − Tồn (không âm). Khi đã có cấp duyệt thì tồn khóa, không sửa được.`
        }
      />
      {hasSuggestion && (
        <Button
          style={{ marginBottom: 8 }}
          onClick={() =>
            setStock((s) => ({
              ...s,
              ...Object.fromEntries(doc.lines.filter((l) => l.lastStockQty != null).map((l) => [l.id, l.lastStockQty!])),
            }))
          }
        >
          Lấy tồn gợi ý
        </Button>
      )}
      <Table<PurchaseRequestLine>
        size="small"
        rowKey="id"
        pagination={false}
        scroll={{ y: 380 }}
        dataSource={doc.lines}
        columns={[
          { title: 'STT', dataIndex: 'lineNo', width: 50 },
          { title: 'Mặt hàng', key: 'item', render: (_, l) => `${l.itemCode} – ${l.itemName}` },
          { title: 'ĐVT', dataIndex: 'uomCode', width: 60 },
          { title: 'SL cần sử dụng', key: 'required', width: 110, align: 'right', render: (_, l) => formatQuantity(l.requiredQty) },
          {
            title: 'Tồn gợi ý',
            key: 'last',
            width: 200,
            render: (_, l) =>
              l.lastStockQty == null ? (
                <span className="line-grid-readonly">Chưa có</span>
              ) : (
                <span title={l.lastStockAt ? `Xác nhận lúc ${formatDateTime(l.lastStockAt)}` : undefined}>
                  {formatQuantity(l.lastStockQty)} <DocNo no={l.lastStockRequestNo ?? null} docType="PR" id={l.lastStockRequestId ?? undefined} />
                </span>
              ),
          },
          {
            title: 'Tồn',
            key: 'stock',
            width: 120,
            render: (_, l) => (
              <InputNumber
                {...vnNumberInput}
                min={0}
                precision={4}
                style={{ width: '100%' }}
                status={error && stock[l.id] == null ? 'error' : undefined}
                value={stock[l.id] ?? null}
                onChange={(v) => setStock((s) => ({ ...s, [l.id]: v == null ? null : Number(v) }))}
                aria-label={`Tồn dòng ${l.lineNo}`}
              />
            ),
          },
          {
            title: 'Cần mua',
            key: 'buy',
            width: 100,
            align: 'right',
            render: (_, l) => <strong>{formatQuantity(needToBuy(l.requiredQty, stock[l.id]))}</strong>,
          },
        ]}
      />
    </Modal>
  )
}
