import type { ReactNode } from 'react'
import { createBrowserRouter } from 'react-router'
import { screens } from './app/screens'
import { AppShell } from './components/AppShell/AppShell'
import { HomePage } from './pages/HomePage'
import { PlaceholderPage } from './pages/PlaceholderPage'
import { PurchaseOrderListPage } from './pages/purchasing/PurchaseOrderListPage'

/** Màn hình đã làm, theo mã; màn hình khác trong danh mục mở trang "đang xây dựng". */
const implemented: Record<string, ReactNode> = {
  HOME: <HomePage />,
  PO: <PurchaseOrderListPage />,
}

/**
 * Bộ định tuyến: mọi trang nằm trong AppShell (CLAUDE.md mục 9).
 */
export const router = createBrowserRouter([
  {
    element: <AppShell />,
    children: [
      ...screens.map((s) => ({ path: s.path, element: implemented[s.code] ?? <PlaceholderPage /> })),
      { path: '*', element: <PlaceholderPage /> },
    ],
  },
])
