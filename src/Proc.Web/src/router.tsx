import type { ReactNode } from 'react'
import { createBrowserRouter } from 'react-router'
import { screens } from './app/screens'
import { LOGIN_PATH } from './auth/paths'
import { RequireAuth } from './auth/RequireAuth'
import { ScreenGuard } from './auth/ScreenGuard'
import { CreatePurchaseOrderPreview } from './classic/preview/CreatePurchaseOrderPreview'
import { AppShell } from './components/AppShell/AppShell'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage'
import { NotificationPage } from './pages/NotificationPage'
import { DocumentRegistryPage } from './pages/DocumentRegistryPage'
import { ItemCategoryListPage } from './pages/masterData/ItemCategoryListPage'
import { ItemListPage } from './pages/masterData/ItemListPage'
import { SupplierGroupListPage } from './pages/masterData/SupplierGroupListPage'
import { SupplierApprovalPage } from './pages/masterData/SupplierApprovalPage'
import { ConflictListPage } from './pages/pricing/ConflictListPage'
import { SoleSourceListPage } from './pages/pricing/SoleSourceListPage'
import { SupplierReviewPage } from './pages/pricing/SupplierReviewPage'
import { SupplierListPage } from './pages/masterData/SupplierListPage'
import { UomListPage } from './pages/masterData/UomListPage'
import { GoodsReceiptPage } from './pages/inventory/GoodsReceiptPage'
import { GoodsReturnPage } from './pages/inventory/GoodsReturnPage'
import { IntercompanyReceiptPage } from './pages/inventory/IntercompanyReceiptPage'
import { IntercompanySalePage } from './pages/inventory/IntercompanySalePage'
import { InventoryDocumentListPage } from './pages/inventory/InventoryDocumentListPage'
import { StockCardPage } from './pages/inventory/StockCardPage'
import { StockOnHandPage } from './pages/inventory/StockOnHandPage'
import { StockTransferPage } from './pages/inventory/StockTransferPage'
import { InventoryPeriodPage } from './pages/inventory/InventoryPeriodPage'
import { PlaceholderPage } from './pages/PlaceholderPage'
import { ContractAmendmentRedirect } from './pages/purchasing/ContractAmendmentDrawer'
import { ContractListPage, ContractPage } from './pages/purchasing/ContractPages'
import { InternalPriceListPage } from './pages/pricing/InternalPriceListPage'
import { PriceComparisonPage } from './pages/pricing/PriceComparisonPage'
import { PriceListListPage, SupplierPriceListPage } from './pages/pricing/SupplierPriceListPages'
import { RfqListPage, RfqPage } from './pages/sourcing/RfqPages'
import { SourcingListPage, SourcingPage } from './pages/sourcing/SourcingPages'
import { GoodsInTransitPage, IntercompanyReconciliationPage } from './pages/reports/IntercompanyReportPages'
import {
  PendingRequestsReportPage,
  PurchaseSlaReportPage,
  ReceiptQualityReportPage,
  ThreeWayMatchReportPage,
  PurchaseSpendReportPage,
  SourcingSavingsReportPage,
  ContractComplianceReportPage,
  PriceVarianceReportPage,
  KpiReportPage,
  SupplierEvaluationReportPage,
} from './pages/reports/PurchaseReportPages'
import { AccountingExportPage } from './pages/reports/AccountingExportPage'
import { PurchaseHistoryMappingPage } from './pages/reports/PurchaseHistoryMappingPage'
import { PurchaseAnalysisPage } from './pages/reports/PurchaseAnalysisPage'
import { PurchaseHistoryPage } from './pages/reports/PurchaseHistoryPage'
import { StockMovementReportPage } from './pages/reports/StockMovementReportPage'
import { ExpiringReportPage, GroupStockReportPage, ReorderSuggestionReportPage, SlowMovingReportPage } from './pages/reports/StockReportPages'
import { PurchaseOrderListPage as DemoPurchaseOrderListPage } from './pages/purchasing/PurchaseOrderListPage'
import { PurchaseOrderPage } from './pages/purchasing/PurchaseOrderPage'
import { PurchaseRequestPage } from './pages/purchasing/PurchaseRequestPage'
import { ApprovalRuleListPage, DeliveryTrackingPage, PendingApprovalsPage, PurchaseListPage } from './pages/purchasing/PurchasingListPages'
import { AccessLogListPage } from './pages/system/AccessLogListPage'
import { AuditLogListPage } from './pages/system/AuditLogListPage'
import { CompanyListPage } from './pages/system/CompanyListPage'
import { DocumentNumberingPage } from './pages/system/DocumentNumberingPage'
import { CatalogNumberingPage } from './pages/system/CatalogNumberingPage'
import { RoleListPage } from './pages/system/RoleListPage'
import { SupplierRatingPage } from './pages/system/SupplierRatingPage'
import { SystemParameterPage } from './pages/system/SystemParameterPage'
import { UserListPage } from './pages/system/UserListPage'
import { WarehouseListPage } from './pages/system/WarehouseListPage'
import { AnomalyReportPage, ControlFindingListPage } from './pages/reports/ControlPages'
import { DepartmentListPage } from './pages/system/DepartmentListPage'

/** Màn hình đã làm, theo mã; màn hình khác trong danh mục mở trang "đang xây dựng". */
const implemented: Record<string, ReactNode> = {
  HOME: <HomePage />,
  NOTI: <NotificationPage />,
  DOCS: <DocumentRegistryPage />,
  PR: <PurchaseListPage kind="purchase-requests" title="Danh sách đề nghị mua hàng (PR)" path="/mua-hang/pr" exportFileName="de-nghi-mua-hang" />,
  PO: <PurchaseListPage kind="purchase-orders" title="Danh sách đơn mua hàng (PO)" path="/mua-hang/po" exportFileName="don-mua-hang" />,
  POTRK: <DeliveryTrackingPage />,
  TODO: <PendingApprovalsPage />,
  APRL: <ApprovalRuleListPage />,
  SUP: <SupplierListPage />,
  SUPAPR: <SupplierApprovalPage />,
  COI: <ConflictListPage />,
  NDQ: <SoleSourceListPage />,
  SUPREV: <SupplierReviewPage />,
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
  NXT: <StockMovementReportPage />,
  SLOW: <SlowMovingReportPage />,
  REORD: <ReorderSuggestionReportPage />,
  EXP: <ExpiringReportPage />,
  GSTK: <GroupStockReportPage />,
  PRPEND: <PendingRequestsReportPage />,
  SLA: <PurchaseSlaReportPage />,
  QCDEV: <ReceiptQualityReportPage />,
  MATCH3: <ThreeWayMatchReportPage />,
  RPSUP: <PurchaseSpendReportPage />,
  SUPEVAL: <SupplierEvaluationReportPage />,
  SAVE: <SourcingSavingsReportPage />,
  PRCW: <ContractComplianceReportPage />,
  PRVAR: <PriceVarianceReportPage />,
  KPI: <KpiReportPage />,
  ANOM: <AnomalyReportPage />,
  VIOL: <ControlFindingListPage />,
  ACCX: <AccountingExportPage />,
  PHIS: <PurchaseHistoryPage />,
  PHMAP: <PurchaseHistoryMappingPage />,
  PHAN: <PurchaseAnalysisPage />,
  ICS: <InventoryDocumentListPage docType="ICS" title="Phiếu xuất bán nội bộ (ICS)" path="/kho/ics" exportFileName="phieu-xuat-ban-noi-bo" />,
  ICR: <InventoryDocumentListPage docType="ICR" title="Phiếu nhập mua nội bộ (ICR)" path="/kho/icr" exportFileName="phieu-nhap-mua-noi-bo" canCreate={false} />,
  IPR: <InternalPriceListPage />,
  PRL: <PriceListListPage />,
  PCMP: <PriceComparisonPage />,
  SRC: <SourcingListPage />,
  RFQ: <RfqListPage />,
  CTR: <ContractListPage />,
  QCMP: <RfqListPage compare />,
  TRANSIT: <GoodsInTransitPage />,
  ICREC: <IntercompanyReconciliationPage />,
  WH: <WarehouseListPage />,
  DEPT: <DepartmentListPage />,
  USER: <UserListPage />,
  ROLE: <RoleListPage />,
  LOG: <AuditLogListPage />,
  ACC: <AccessLogListPage />,
  DSCT: <DocumentNumberingPage />,
  RATE: <SupplierRatingPage />,
  DSDM: <CatalogNumberingPage />,
  SEC: <SystemParameterPage />,
}

/** Trang chứng từ mở từ màn hình danh sách: mã màn hình → trang phiếu. */
const documentPages = [
  { code: 'GRN', element: <GoodsReceiptPage /> },
  { code: 'RTV', element: <GoodsReturnPage /> },
  { code: 'TRF', element: <StockTransferPage /> },
  { code: 'ICS', element: <IntercompanySalePage /> },
  { code: 'ICR', element: <IntercompanyReceiptPage /> },
  { code: 'PR', element: <PurchaseRequestPage /> },
  { code: 'PO', element: <PurchaseOrderPage /> },
  { code: 'PRL', element: <SupplierPriceListPage /> },
  { code: 'SRC', element: <SourcingPage /> },
  { code: 'RFQ', element: <RfqPage /> },
  { code: 'CTR', element: <ContractPage /> },
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
  // Màn hình mẫu chuẩn của giao diện ERP desktop cổ điển (docs/ui-style.md): có khung riêng nên đứng ngoài AppShell.
  {
    path: '/thu-giao-dien/tao-po',
    element: (
      <RequireAuth>
        <CreatePurchaseOrderPreview />
      </RequireAuth>
    ),
  },
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
      // Phụ lục hợp đồng mở từ thông báo / Việc cần làm (chỉ có Id phụ lục) – cùng quyền màn hình CTR.
      {
        path: '/mua-hang/hop-dong-khung/phu-luc/:id',
        element: <ScreenGuard screen={screens.find((s) => s.code === 'CTR')!}><ContractAmendmentRedirect /></ScreenGuard>,
      },
      // Danh sách PO dữ liệu mẫu (30 dòng) giữ cho test giao diện chung AppShell/ListPage/DataGrid so với ảnh mẫu (e2e/po-list.spec.ts).
      {
        path: '/mua-hang/po/mau',
        element: <ScreenGuard screen={screens.find((s) => s.code === 'PO')!}><DemoPurchaseOrderListPage /></ScreenGuard>,
      },
      { path: '*', element: <PlaceholderPage /> },
    ],
  },
])
