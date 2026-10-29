import type { ReactNode } from 'react'
import { createBrowserRouter } from 'react-router'
import { screens } from './app/screens'
import { LOGIN_PATH } from './auth/paths'
import { RequireAuth } from './auth/RequireAuth'
import { ScreenGuard } from './auth/ScreenGuard'
import { AppShell } from './components/AppShell/AppShell'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage'
import { PlaceholderPage } from './pages/PlaceholderPage'
import { PurchaseOrderListPage } from './pages/purchasing/PurchaseOrderListPage'
import { AccessLogListPage } from './pages/system/AccessLogListPage'
import { AuditLogListPage } from './pages/system/AuditLogListPage'
import { RoleListPage } from './pages/system/RoleListPage'
import { UserListPage } from './pages/system/UserListPage'

/** Màn hình đã làm, theo mã; màn hình khác trong danh mục mở trang "đang xây dựng". */
const implemented: Record<string, ReactNode> = {
  HOME: <HomePage />,
  PO: <PurchaseOrderListPage />,
  USER: <UserListPage />,
  ROLE: <RoleListPage />,
  LOG: <AuditLogListPage />,
  ACC: <AccessLogListPage />,
}

/**
 * Bộ định tuyến: mọi trang nằm trong AppShell (CLAUDE.md mục 9) và yêu cầu đăng nhập;
 * riêng màn hình đăng nhập đứng ngoài khung.
 */
export const router = createBrowserRouter([
  { path: LOGIN_PATH, element: <LoginPage /> },
  {
    element: (
      <RequireAuth>
        <AppShell />
      </RequireAuth>
    ),
    children: [
      ...screens.map((s) => ({
        path: s.path,
        element: <ScreenGuard screen={s}>{implemented[s.code] ?? <PlaceholderPage />}</ScreenGuard>,
      })),
      { path: '*', element: <PlaceholderPage /> },
    ],
  },
])
