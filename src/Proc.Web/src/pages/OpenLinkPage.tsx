import { useEffect } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router'
import { isMobileApprovable } from '../app/approvalRoutes'
import { notificationTargetPath } from '../app/notificationTarget'
import { useAuth } from '../auth/useAuth'

/** Độ rộng tối đa (px) coi là điện thoại – mở link email bằng trang duyệt gọn (lát O2d). Máy tính bảng ngang dùng bản đầy đủ. */
const MOBILE_MAX_WIDTH = 768

/**
 * Trang trung chuyển của nút "Mở trên phần mềm" trong email thông báo (lát O2b): `/mo/{mã màn hình}/{Id}?cty={công ty}`. Đổi sang công ty của
 * thông báo (nếu người dùng làm việc ở đó) rồi mở phiếu như khi bấm thông báo trong phần mềm. Chưa đăng nhập thì RequireAuth đưa về trang
 * đăng nhập trước. Trên màn hình hẹp (điện thoại, ≤ {@link MOBILE_MAX_WIDTH}px) phiếu thuộc loại duyệt được trên trang gọn mở thẳng trang duyệt gọn
 * `/duyet/{loại}/{Id}` (lát O2d) – trang đó tự cho xem khi phiếu không còn chờ người dùng duyệt và có nút mở bản đầy đủ.
 */
export function OpenLinkPage() {
  const { screen = '', id } = useParams()
  const [search] = useSearchParams()
  const { session, workingCompany, setWorkingCompany } = useAuth()
  const navigate = useNavigate()
  const companyId = search.get('cty')

  useEffect(() => {
    if (companyId && companyId !== workingCompany?.id && session?.user.companies.some((c) => c.id === companyId)) {
      setWorkingCompany(companyId)
      return
    }
    const code = screen.toUpperCase()
    const narrow = window.matchMedia(`(max-width: ${MOBILE_MAX_WIDTH}px)`).matches
    navigate(narrow && id && isMobileApprovable(code) ? `/duyet/${code}/${id}` : notificationTargetPath(code, id ?? null), { replace: true })
  }, [companyId, workingCompany?.id, session, setWorkingCompany, navigate, screen, id])

  return <div style={{ padding: 16 }}>Đang mở…</div>
}
