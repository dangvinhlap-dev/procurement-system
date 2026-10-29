/**
 * DỮ LIỆU GIẢ cho màn hình Danh sách đơn mua hàng (PO) ở lát giao diện khung.
 * Tên công ty, nhà cung cấp, người dùng đều là tên bịa. Sẽ thay bằng API ở giai đoạn 5 (design.md mục 13.2).
 */

/** Trạng thái của PO hiển thị trên danh sách (vòng đời chứng từ – design.md mục 3.2, theo dõi PO mục 6.2). */
export type PurchaseOrderStatus =
  | 'Nháp'
  | 'Chờ duyệt'
  | 'Đã duyệt'
  | 'Đang giao'
  | 'Quá hạn giao'
  | 'Đã nhận đủ'
  | 'Đã hủy'

/** Một dòng trên danh sách PO. */
export interface PurchaseOrderRow {
  id: string
  /** Số PO theo định dạng PO-{CTY}-{YYMM}-{0000} (design.md mục 5.4). */
  number: string
  /** Ngày chứng từ, ISO yyyy-MM-dd. */
  date: string
  companyCode: string
  companyName: string
  supplierName: string
  /** Tổng thanh toán, VND. */
  totalAmount: number
  status: PurchaseOrderStatus
  createdBy: string
  /** Người dùng đã ghim/đánh dấu dòng này. */
  pinned: boolean
}

export const demoCompanies = [
  { code: 'TCT', name: 'Tổng công ty' },
  { code: 'BHA', name: 'Công ty Thép Bắc Hà' },
  { code: 'NMI', name: 'Công ty Nhựa Miền Nam' },
  { code: 'DTV', name: 'Công ty Điện Tân Việt' },
] as const

const suppliers = [
  'Công ty TNHH Vật tư Hòa Phát Đạt',
  'Công ty CP Thiết bị Công nghiệp Sao Mai',
  'Công ty TNHH Bao bì Đông Á',
  'Công ty CP Hóa chất Phương Nam',
  'Công ty TNHH Cơ khí Trường Thành',
  'Công ty CP Văn phòng phẩm Hồng Hà Mới',
  'Công ty TNHH Dầu nhớt Ánh Dương',
  'Công ty CP Điện máy Thiên Long',
  'Công ty TNHH Vận tải và Kho bãi Lạc Hồng',
] as const

const creators = ['Nguyễn Văn An', 'Trần Thị Bình', 'Lê Minh Châu', 'Phạm Quốc Dũng', 'Hoàng Thu Hà'] as const

const statuses: PurchaseOrderStatus[] = [
  'Đã nhận đủ', 'Đang giao', 'Chờ duyệt', 'Đã duyệt', 'Quá hạn giao', 'Nháp', 'Đã nhận đủ', 'Đã hủy',
]

/**
 * Bộ sinh số giả ngẫu nhiên có hạt giống cố định (thuật toán mulberry32),
 * để dữ liệu giả giống hệt nhau mỗi lần chạy – ảnh chụp và test ổn định.
 * @param seed Hạt giống.
 * @returns Hàm trả số thực trong [0, 1).
 */
function seededRandom(seed: number): () => number {
  let a = seed
  return () => {
    a = (a + 0x6d2b79f5) | 0
    let t = Math.imul(a ^ (a >>> 15), 1 | a)
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

/**
 * Sinh danh sách PO giả, ngày giảm dần từ 28/10/2026, mỗi công ty có dãy số riêng theo tháng.
 * @param count Số dòng cần sinh.
 * @returns Mảng PO giả.
 */
function generatePurchaseOrders(count: number): PurchaseOrderRow[] {
  const random = seededRandom(20261029)
  const pick = <T,>(items: readonly T[]) => items[Math.floor(random() * items.length)]
  const sequence = new Map<string, number>()
  const rows: PurchaseOrderRow[] = []
  let day = new Date(Date.UTC(2026, 9, 28))

  for (let i = 0; i < count; i++) {
    const company = demoCompanies[1 + Math.floor(random() * (demoCompanies.length - 1))]
    const yymm = `${String(day.getUTCFullYear()).slice(2)}${String(day.getUTCMonth() + 1).padStart(2, '0')}`
    const seqKey = `${company.code}-${yymm}`
    const next = (sequence.get(seqKey) ?? 0) + 1
    sequence.set(seqKey, next)

    rows.push({
      id: `po-${i + 1}`,
      number: `PO-${company.code}-${yymm}-${String(next).padStart(4, '0')}`,
      date: day.toISOString().slice(0, 10),
      companyCode: company.code,
      companyName: company.name,
      supplierName: pick(suppliers),
      // Tổng tiền từ 5 triệu đến ~2 tỷ, làm tròn nghìn đồng.
      totalAmount: Math.round((5_000_000 + random() * random() * 2_000_000_000) / 1000) * 1000,
      status: statuses[i % statuses.length],
      createdBy: pick(creators),
      pinned: i === 1 || i === 4,
    })

    // Lùi 0–2 ngày cho dòng kế tiếp.
    day = new Date(day.getTime() - Math.floor(random() * 3) * 86_400_000)
  }

  // Dãy số sinh theo thứ tự ngày giảm dần nên số nhỏ nằm cuối; đánh lại để số tăng theo ngày.
  const byKey = new Map<string, PurchaseOrderRow[]>()
  for (const row of rows) {
    const key = row.number.slice(0, row.number.lastIndexOf('-'))
    byKey.set(key, [...(byKey.get(key) ?? []), row])
  }
  for (const [key, group] of byKey) {
    group.reverse().forEach((row, index) => {
      row.number = `${key}-${String(index + 1).padStart(4, '0')}`
    })
  }
  return rows
}

/** 30 PO giả dùng cho màn hình danh sách. */
export const mockPurchaseOrders: PurchaseOrderRow[] = generatePurchaseOrders(30)
