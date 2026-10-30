import type { ReactNode } from 'react'
import { createBrowserRouter } from 'react-router'
import { screens } from './app/screens'
import { LOGIN_PATH } from './auth/paths'
import { RequireAuth } from './auth/RequireAuth'
import { ScreenGuard } from './auth/ScreenGuard'
import { AppShell } from './components/AppShell/AppShell'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage'
import { ItemCategoryListPage } from './pages/masterData/ItemCategoryListPage'
import { ItemListPage } from './pages/masterData/ItemListPage'
import { UomListPage } from './pages/masterData/UomListPage'
import { PlaceholderPage } from './pages/PlaceholderPage'
import { PurchaseOrderListPage } from './pages/purchasing/PurchaseOrderListPage'
import { AccessLogListPage } from './pages/system/AccessLogListPage'
import { AuditLogListPage } from './pages/system/AuditLogListPage'
import { CompanyListPage } from './pages/system/CompanyListPage'
import { DocumentNumberingPage } from './pages/system/DocumentNumberingPage'
import { RoleListPage } from './pages/system/RoleListPage'
import { UserListPage } from './pages/system/UserListPage'
import { WarehouseListPage } from './pages/system/WarehouseListPage'

/** Màn hình đã làm, theo mã; màn hình khác trong danh mục mở trang "đang xây dựng". */
const implemented: Record<string, ReactNode> = {
  HOME: <HomePage />,
  PO: <PurchaseOrderListPage />,
  ITEM: <ItemListPage />,
  CAT: <ItemCategoryListPage />,
  UOM: <UomListPage />,
  COMP: <CompanyListPage />,
  WH: <WarehouseListPage />,
  USER: <UserListPage />,
  ROLE: <RoleListPage />,
  LOG: <AuditLogListPage />,
  ACC: <AccessLogListPage />,
  DSCT: <DocumentNumberingPage />,
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
