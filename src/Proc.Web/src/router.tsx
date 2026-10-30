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
import { SupplierGroupListPage } from './pages/masterData/SupplierGroupListPage'
import { SupplierListPage } from './pages/masterData/SupplierListPage'
import { UomListPage } from './pages/masterData/UomListPage'
import { GoodsReceiptPage } from './pages/inventory/GoodsReceiptPage'
import { GoodsReturnPage } from './pages/inventory/GoodsReturnPage'
import { InventoryDocumentListPage } from './pages/inventory/InventoryDocumentListPage'
import { StockCardPage } from './pages/inventory/StockCardPage'
import { StockOnHandPage } from './pages/inventory/StockOnHandPage'
import { StockTransferPage } from './pages/inventory/StockTransferPage'
import { InventoryPeriodPage } from './pages/inventory/InventoryPeriodPage'
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
  SUP: <SupplierListPage />,
  SGRP: <SupplierGroupListPage />,
  ITEM: <ItemListPage />,
  CAT: <ItemCategoryListPage />,
  UOM: <UomListPage />,
  COMP: <CompanyListPage />,
  PER: <InventoryPeriodPage />,
  GRN: <InventoryDocumentListPage docType="GRN" title="Phiếu nhập mua từ NCC (GRN)" path="/kho/grn" exportFileName="phieu-nhap-mua" />,
  RTV: <InventoryDocumentListPage docType="RTV" title="Phiếu xuất trả hàng NCC (RTV)" path="/kho/rtv" exportFileName="phieu-tra-ncc" />,
  TRF: <InventoryDocumentListPage docType="TRF" title="Phiếu chuyển kho nội bộ (TRF)" path="/kho/trf" exportFileName="phieu-chuyen-kho" />,
  STK: <StockOnHandPage />,
  CARD: <StockCardPage />,
  WH: <WarehouseListPage />,
  USER: <UserListPage />,
  ROLE: <RoleListPage />,
  LOG: <AuditLogListPage />,
  ACC: <AccessLogListPage />,
  DSCT: <DocumentNumberingPage />,
}

/** Trang chứng từ mở từ màn hình danh sách: mã màn hình → trang phiếu. */
const documentPages = [
  { code: 'GRN', element: <GoodsReceiptPage /> },
  { code: 'RTV', element: <GoodsReturnPage /> },
  { code: 'TRF', element: <StockTransferPage /> },
].map((d) => ({
  screen: screens.find((s) => s.code === d.code)!,
  element: d.element,
}))

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
      // Trang chứng từ (lập mới: /kho/grn/moi) – cùng quyền với màn hình danh sách của nó.
      ...documentPages.map((d) => ({
        path: `${d.screen.path}/:id`,
        element: <ScreenGuard screen={d.screen}>{d.element}</ScreenGuard>,
      })),
      { path: '*', element: <PlaceholderPage /> },
    ],
  },
])
