import { useAppraisals, type AppraisalOwnerType } from '../../api/appraisals'
import type { DocumentTab } from '../DocumentForm/DocumentForm'
import { AppraisalPanel } from './AppraisalPanel'

/**
 * Tab THẨM ĐỊNH cho phiếu GTH, RFQ, hợp đồng, hồ sơ NCC (lát H1 – design.md mục 18.25). Nhãn kèm số ý kiến còn hiệu lực, đánh dấu khi có ý
 * kiến Không đạt. Đối tượng chưa lưu thì không có tab (ý kiến gắn với đối tượng đã có).
 * @param ownerType Loại đối tượng.
 * @param ownerId Id đối tượng; rỗng khi đang lập mới.
 * @returns Mảng 0 hoặc 1 tab – trải vào extraTabs/tabs của trang.
 */
export function useAppraisalTab(ownerType: AppraisalOwnerType, ownerId: string | undefined): DocumentTab[] {
  const { data } = useAppraisals(ownerType, ownerId)
  if (!ownerId) return []
  const total = (data?.passCount ?? 0) + (data?.failCount ?? 0)
  const label = !total ? 'Thẩm định' : data?.failCount ? `Thẩm định (${total}, ${data.failCount} không đạt)` : `Thẩm định (${total})`
  return [{ key: 'appraisals', label, children: <AppraisalPanel ownerType={ownerType} ownerId={ownerId} /> }]
}
