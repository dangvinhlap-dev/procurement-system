import { useEffect, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { LOGIN_PATH } from './paths'
import { useAuth } from './useAuth'

/**
 * Chặn trang cần đăng nhập: chưa đăng nhập thì chuyển sang màn hình đăng nhập,
 * nhớ trang đang mở để quay lại sau khi đăng nhập xong.
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { session } = useAuth()
  const navigate = useNavigate()

  useEffect(() => {
    if (session) return
    // Đọc đường dẫn từ window.location chứ không từ useLocation: ngay sau đăng xuất router có thể chưa vẽ xong lượt chuyển
    // sang /dang-nhap (lượt vẽ chạy dạng transition), vị trí router giữ có thể là trang cũ.
    const toLogin = () => {
      // URL đã là /dang-nhap: đang chuyển trang rồi (hiệu ứng chạy lại trong StrictMode) – gọi nữa sẽ ghi đè trang cần quay lại.
      if (window.location.pathname === LOGIN_PATH) return
      void navigate(LOGIN_PATH, { replace: true, state: { from: window.location.pathname + window.location.search } })
    }
    toLogin()
    // Bấm Quay lại trong lúc router chưa vẽ xong lượt chuyển sang /dang-nhap: router trở về đúng vị trí nó đang giữ nên không vẽ lại,
    // hiệu ứng trên không chạy lại và trang để trắng. Bắt popstate để chuyển về màn hình đăng nhập lần nữa.
    window.addEventListener('popstate', toLogin)
    return () => window.removeEventListener('popstate', toLogin)
  }, [session, navigate])

  if (!session) return null
  return children
}
