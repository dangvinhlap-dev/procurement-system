/** Một dòng của phiếu đang soạn (RFQ, hợp đồng) cần lấy SL kế hoạch. */
export interface PlannedQuantityLine {
  /** Vị trí dòng trong lưới (0 = dòng 1). */
  index: number
  itemId: string
  uomId: string
  /** SL đang nhập trên dòng. */
  qty: number | null
}

/**
 * Các dòng đã chọn mặt hàng và ĐVT của lưới (bỏ dòng trống) để đưa vào hộp thoại.
 * @param lines Giá trị dòng của form.
 * @param qty Lấy SL đang nhập của dòng.
 */
export function toPlannedLines<T extends { itemId?: string; uomId?: string }>(
  lines: (T | undefined)[],
  qty: (line: T) => number | null | undefined,
): PlannedQuantityLine[] {
  return lines.flatMap((l, index) => (l?.itemId && l.uomId ? [{ index, itemId: l.itemId, uomId: l.uomId, qty: qty(l) ?? null }] : []))
}
