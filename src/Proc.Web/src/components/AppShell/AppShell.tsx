import { useEffect, useState } from 'react'
import { Outlet, useLocation } from 'react-router'
import { findScreenByPath } from '../../app/screens'
import {
  demoNavCounts,
  demoPinnedScreens,
  demoUnreadNotifications,
  demoUser,
  demoWorkingCompanies,
} from '../../mocks/session'
import { layoutCssVars } from '../../theme'
import { MenuBar } from './MenuBar'
import { NavTree } from './NavTree'
import { StatusBar } from './StatusBar'
import { focusListSearch } from '../../app/commands'
import { Toolbar } from './Toolbar'
import './AppShell.css'

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
 * Khung ứng dụng cố định cho mọi trang (ui-guide.md mục AppShell, design.md mục 17.1):
 * thanh menu 28px, thanh công cụ 32px, cây điều hướng trái 240px, vùng nội dung, thanh trạng thái 22px.
 * Phím tắt chung: F3 tìm kiếm, Insert đổi INS/OVR, Alt + chữ gạch chân mở menu (trong MenuBar).
 * Người dùng, công ty, số đếm hiện là dữ liệu giả (src/mocks/session.ts) cho đến lát phân quyền.
 */
export function AppShell() {
  const { pathname } = useLocation()
  const screen = findScreenByPath(pathname)
  const screenTitle = screen?.title ?? 'Không tìm thấy màn hình'
  const [company, setCompany] = useState<string>(demoWorkingCompanies[0].value)
  const [insertMode, setInsertMode] = useState(true)

  useEffect(() => {
    document.title = `${screenTitle} – ${APP_NAME}`
  }, [screenTitle])

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'F3') {
        // F3 mặc định của trình duyệt là "tìm trong trang"; ở đây dùng để tìm trong danh sách.
        if (focusListSearch()) e.preventDefault()
      } else if (e.key === 'Insert') {
        setInsertMode((v) => !v)
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])

  return (
    <div className="app-shell" style={layoutCssVars()}>
      <MenuBar
        activeGroup={screen?.group}
        companies={demoWorkingCompanies}
        company={company}
        onCompanyChange={setCompany}
        unreadCount={demoUnreadNotifications}
        userFullName={demoUser.fullName}
        userInitials={demoUser.initials}
      />
      <Toolbar />
      <div className="app-body">
        <NavTree activePath={pathname} pinnedCodes={demoPinnedScreens} counts={demoNavCounts} />
        <main className="app-content">
          <Outlet />
        </main>
      </div>
      <StatusBar
        message="Đang dùng dữ liệu mẫu – chưa kết nối dữ liệu thật."
        screenTitle={screenTitle}
        userFullName={demoUser.fullName}
        workingDate={todayInVietnam()}
        version={__APP_VERSION__}
        insertMode={insertMode}
      />
    </div>
  )
}
