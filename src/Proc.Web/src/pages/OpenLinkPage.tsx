import { useEffect } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router'
import { notificationTargetPath } from '../app/notificationTarget'
import { useAuth } from '../auth/useAuth'

/**
 * Trang trung chuyển của nút "Mở trên phần mềm" trong email thông báo (lát O2b): `/mo/{mã màn hình}/{Id}?cty={công ty}`. Đổi sang công ty của
 * thông báo (nếu người dùng làm việc ở đó) rồi mở phiếu như khi bấm thông báo trong phần mềm. Chưa đăng nhập thì RequireAuth đưa về trang
 * đăng nhập trước.
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
    navigate(notificationTargetPath(screen.toUpperCase(), id ?? null), { replace: true })
  }, [companyId, workingCompany?.id, session, setWorkingCompany, navigate, screen, id])

  return <div style={{ padding: 16 }}>Đang mở…</div>
}
