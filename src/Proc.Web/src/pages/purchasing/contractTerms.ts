import type { ContractTerms } from '../../api/contracts'

/** Tên trường điều khoản dạng chữ (không gồm hạn mức công nợ). */
export type ContractTermTextField = Exclude<keyof ContractTerms, 'creditLimit'>

/**
 * Các điều khoản bắt buộc khi đàm phán (đề xuất Ban CUVT Bước 11) – mỗi điều khoản một ô chữ trên tab Điều khoản của hợp đồng và phụ lục
 * (design.md mục 18.7).
 */
export const contractTermFields: { name: ContractTermTextField; label: string; max: number }[] = [
  { name: 'priceAdjustmentTerms', label: 'Cơ chế điều chỉnh giá', max: 2000 },
  { name: 'paymentTerms', label: 'Thanh toán, công nợ', max: 2000 },
  { name: 'deliveryTerms', label: 'Giao hàng (thời gian, địa điểm từng đơn vị)', max: 2000 },
  { name: 'discountTerms', label: 'Chiết khấu thương mại, theo sản lượng, doanh số', max: 2000 },
  { name: 'sponsorshipTerms', label: 'Tài trợ, hỗ trợ, khuyến mại', max: 2000 },
  { name: 'penaltyTerms', label: 'Chế tài chậm giao, giao sai, hàng lỗi', max: 2000 },
  { name: 'otherTerms', label: 'Điều khoản khác (bảo mật, xung đột lợi ích, chấm dứt, gia hạn, tranh chấp)', max: 4000 },
]
