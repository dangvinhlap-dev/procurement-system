/**
 * Dữ liệu mẫu cho màn hình thử "Tạo đơn mua hàng" phong cách ERP cổ điển (docs/ui-style.md).
 * Tên NCC, mặt hàng đều là tên giả; màn hình thử không gọi API, không ghi CSDL.
 */

/** Loại đơn mua hàng. */
export const demoPoTypes = [
  { value: 'NB', label: 'NB – Mua trong nước' },
  { value: 'NK', label: 'NK – Nhập khẩu' },
  { value: 'DV', label: 'DV – Mua dịch vụ' },
]

/** Nhà cung cấp mẫu. */
export const demoSuppliers = [
  { code: 'NCC-0001', name: 'Công ty TNHH Thép An Phú', address: 'KCN Quang Minh, Mê Linh, Hà Nội', contact: 'Nguyễn Văn Hải – 0912 345 678', paymentTerm: 'Thanh toán 30 ngày' },
  { code: 'NCC-0002', name: 'Công ty CP Nhựa Đông Á', address: 'KCN Sóng Thần 2, Dĩ An, Bình Dương', contact: 'Lê Thị Thu – 0903 222 111', paymentTerm: 'Thanh toán 45 ngày' },
  { code: 'NCC-0003', name: 'Công ty TNHH Thiết bị điện Minh Long', address: '125 Lê Văn Lương, Thanh Xuân, Hà Nội', contact: 'Phạm Quốc Anh – 0987 654 321', paymentTerm: 'Trả ngay khi nhận hàng' },
]

/** Mặt hàng mẫu: mã, tên, ĐVT, nhóm hàng, đơn giá gợi ý (VND). */
export const demoItems = [
  { code: 'VT-0001', name: 'Thép tấm SS400 dày 10mm', uom: 'kg', group: 'Vật tư thép', price: 18_500 },
  { code: 'VT-0002', name: 'Thép hộp 40x80x1,8mm', uom: 'cây', group: 'Vật tư thép', price: 412_000 },
  { code: 'NL-0003', name: 'Hạt nhựa PP Homopolymer', uom: 'kg', group: 'Nguyên liệu nhựa', price: 32_750 },
  { code: 'PT-0004', name: 'Cáp điện CVV 3x4mm²', uom: 'm', group: 'Thiết bị điện', price: 45_200 },
  { code: 'VP-0005', name: 'Giấy A4 80gsm', uom: 'ram', group: 'Văn phòng phẩm', price: 78_000 },
]

/** Kho nhận hàng mẫu (kho Main và kho Sub của một công ty). */
export const demoWarehouses = [
  { value: 'KC', label: 'KC – Kho chính' },
  { value: 'KP1', label: 'KP1 – Kho phụ 1' },
]
