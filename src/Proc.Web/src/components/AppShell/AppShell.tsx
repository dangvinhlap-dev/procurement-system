import { App } from 'antd'
import { useEffect, useState } from 'react'
import { Outlet, useLocation } from 'react-router'
import { findScreenByPath } from '../../app/screens'
import { useAuth } from '../../auth/useAuth'
import { useDashboard } from '../../api/dashboard'
import { demoPinnedScreens } from '../../mocks/session'
import { layoutCssVars } from '../../theme'
import { FieldHelpHost } from '../FieldHelp/FieldHelpHost'
import { ChangePasswordDialog } from './ChangePasswordDialog'
import { NavTree } from './NavTree'
import { ShellBar } from './ShellBar'
import { focusListSearch } from '../../app/commands'
import { UnsavedContext } from '../../app/unsaved'
import { ErrorBoundary } from '../../classic/ErrorBoundary'
import { useUnsavedGuard } from './useUnsavedGuard'
import './AppShell.css'
import { EnvironmentBanner } from '../../classic/EnvironmentBanner'

/** Tên ứng dụng ghép vào tiêu đề tab trình duyệt. */
const APP_NAME = 'Quản lý mua hàng và kho'

/**
 * Ngày làm việc theo giờ Việt Nam, dd/MM/yyyy (CLAUDE.md mục 5) – không phụ thuộc múi giờ của máy.
 */
function todayInVietnam(): string {
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(new Date())
}

/**
 * Khung ứng dụng cố định cho mọi trang theo phong cách ERP desktop cổ điển (docs/ui-style.md, design.md mục 17): thanh menu chữ + thanh
 * công cụ 54px trên cùng, cây điều hướng trái 240px, vùng nội dung (ListPage/DocumentForm). Phiên bản và ngày làm việc nằm trong menu người dùng.
 * Phím tắt chung: F1 xem lịch sử thay đổi của ô (FieldHelpHost), F3 tìm kiếm trong danh sách, Alt + chữ gạch chân mở menu module (trong ShellBar).
 * Người dùng và công ty lấy từ phiên đăng nhập; số đếm và số thông báo chưa đọc theo số liệu trang chủ; ghim còn là dữ liệu giả (src/mocks/session.ts).
 * AppShell luôn nằm trong RequireAuth nên phiên đăng nhập chắc chắn có.
 * Vùng nội dung hiện **một màn hình** (bỏ thẻ màn hình theo yêu cầu người dùng 15/11/2026 – docs/ui-style.md mục 8a); rời trang, đổi công ty,
 * đăng xuất khi còn phiếu chưa lưu thì hỏi lại (useUnsavedGuard).
 */
export function AppShell() {
  const { session, workingCompany, setWorkingCompany, logout } = useAuth()
  const { message } = App.useApp()
  // Số đếm cây điều hướng lấy từ số liệu trang chủ (design.md mục 11.4), theo quyền và công ty đang làm việc.
  const navCounts = useDashboard().data?.navCounts ?? {}
  const [changingPassword, setChangingPassword] = useState(false)
  // Hỏi lại khi rời trang còn phiếu chưa lưu (yêu cầu người dùng 13/11/2026).
  const { register, confirmDiscard } = useUnsavedGuard()
  const { pathname } = useLocation()
  const screen = findScreenByPath(pathname)
  const screenTitle = screen?.title ?? 'Không tìm thấy màn hình'

  useEffect(() => {
    document.title = `${screenTitle} – ${APP_NAME}`
  }, [screenTitle])

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'F3') {
        // F3 mặc định của trình duyệt là "tìm trong trang"; ở đây dùng để tìm trong danh sách.
        if (focusListSearch()) e.preventDefault()
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])

  if (!session) return null
  const user = session.user

  return (
    <UnsavedContext value={register}>
    <div className="app-shell" style={layoutCssVars()}>
      <EnvironmentBanner />
      <ShellBar
        appName={APP_NAME}
        activeGroup={screen?.group}
        companies={user.companies.map((c) => ({ value: c.id, label: `${c.code} – ${c.shortName ?? c.name}` }))}
        company={workingCompany?.id ?? user.defaultCompanyId}
        onCompanyChange={(id) => {
          // Trang dựng lại dưới công ty mới – còn phiếu chưa lưu thì hỏi bỏ thay đổi.
          confirmDiscard(() => {
            setWorkingCompany(id)
            const company = user.companies.find((c) => c.id === id)
            if (company) message.success(`Đang làm việc tại ${company.code} – ${company.name}.`)
          })
        }}
        // Số thông báo chưa đọc đi cùng số liệu trang chủ (design.md mục 11.6).
        unreadCount={navCounts.NOTI?.count ?? 0}
        userFullName={user.fullName}
        userName={user.userName}
        workingDate={todayInVietnam()}
        version={__APP_VERSION__}
        onLogout={() => confirmDiscard(() => logout(), true)}
        onChangePassword={() => setChangingPassword(true)}
      />
      <ChangePasswordDialog open={changingPassword} onClose={() => setChangingPassword(false)} />
      <FieldHelpHost />
      <div className="app-body">
        {/* Trang con (phiếu /mua-hang/po/123) đánh dấu màn hình cha trên cây điều hướng. */}
        <NavTree activePath={screen?.path ?? pathname} pinnedCodes={demoPinnedScreens} counts={navCounts} />
        <main className="app-content">
          {/* Lỗi giao diện của một trang chỉ làm hỏng vùng nội dung (báo về nhật ký lỗi ERR – lát QT2); đổi trang thì thử vẽ lại. */}
          <ErrorBoundary resetKey={pathname}>
            <Outlet />
          </ErrorBoundary>
        </main>
      </div>
    </div>
    </UnsavedContext>
  )
}
