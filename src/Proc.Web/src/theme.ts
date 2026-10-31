import { type ThemeConfig } from 'antd'

/**
 * Theme token của toàn ứng dụng theo quy chuẩn SAP Fiori, theme Horizon (docs/ui-guide.md mục 1–3).
 * Component không viết màu cứng; mọi màu, cỡ chữ, bo góc, đổ bóng lấy từ đây (CLAUDE.md mục 9).
 * CSS của các component đọc lại các token này qua biến CSS của antd (ví dụ var(--ant-color-border)).
 */

/** Đổ bóng cực nhẹ của khối nội dung (thẻ, bảng, khối form) – ui-guide.md mục 3. */
const shadowBlock = '0 0 2px rgba(34, 53, 72, 0.15), 0 2px 4px rgba(34, 53, 72, 0.15)'

/** Đổ bóng của lớp nổi (menu thả xuống, hộp thoại, ngăn kéo). */
const shadowPopup = '0 0 4px rgba(34, 53, 72, 0.15), 0 8px 16px rgba(34, 53, 72, 0.15)'

export const appTheme: ThemeConfig = {
  token: {
    // Phông "72" của SAP chỉ khai báo, không kèm file (giấy phép riêng) – máy chưa cài dùng Arial/Helvetica.
    fontFamily: '"72", "72full", Arial, Helvetica, sans-serif',
    fontSize: 14,
    fontSizeSM: 12,
    fontSizeLG: 16,
    // Tiêu đề trang (H1) 24px/700; tiêu đề khối 16px/600 (ui-guide.md mục 1).
    fontSizeHeading4: 24,
    fontSizeHeading5: 16,
    fontWeightStrong: 600,
    colorText: '#1d2d3d',
    colorTextHeading: '#1d2d3d',
    colorTextSecondary: '#647987',
    colorTextDescription: '#647987',
    colorTextTertiary: '#8396a8',
    // Trạng thái trung tính (xám) và chữ bị mờ.
    colorTextDisabled: '#8396a8',
    colorPrimary: '#0a6ed1',
    colorLink: '#0a6ed1',
    colorLinkHover: '#005a9e',
    colorLinkActive: '#005a9e',
    colorSuccess: '#1a7a24',
    colorSuccessBg: '#f0f9f1',
    colorSuccessBorder: '#a6d6ac',
    colorWarning: '#b25900',
    colorWarningBg: '#fff8eb',
    colorWarningBorder: '#f0c98a',
    colorError: '#b00e12',
    colorErrorBg: '#fff1f1',
    colorErrorBorder: '#f1a8aa',
    colorInfo: '#02629b',
    colorInfoBg: '#f2f9ff',
    colorInfoBorder: '#9cc8ea',
    colorBgLayout: '#f5f6f7',
    colorBgContainer: '#ffffff',
    colorBorder: '#bcc3ca',
    colorBorderSecondary: '#e5e8eb',
    // Nền dòng chọn trong bảng / mục chọn trong danh sách.
    controlItemBgActive: '#e8f3ff',
    controlItemBgActiveHover: '#dbecff',
    controlItemBgHover: '#f2f9ff',
    // Nền rất nhạt: dòng xen kẽ của bảng, khu bộ lọc.
    colorFillAlter: '#f7f8f9',
    // Bo góc: 4px ô nhập, nút; 8px khối lớn (ui-guide.md mục 3).
    borderRadius: 4,
    borderRadiusSM: 4,
    borderRadiusLG: 8,
    // Chiều cao, khoảng cách theo bội số của 4.
    controlHeight: 28,
    controlHeightSM: 24,
    controlHeightLG: 36,
    padding: 16,
    paddingSM: 8,
    paddingXS: 4,
    margin: 16,
    marginSM: 8,
    marginXS: 4,
    boxShadow: shadowBlock,
    boxShadowSecondary: shadowPopup,
    boxShadowTertiary: shadowBlock,
  },
  components: {
    Table: {
      headerBg: '#ffffff',
      headerColor: '#647987',
      headerSortActiveBg: '#f5f6f7',
      headerSortHoverBg: '#f5f6f7',
      headerSplitColor: '#e5e8eb',
      borderColor: '#e5e8eb',
      rowHoverBg: '#f2f9ff',
      rowSelectedBg: '#e8f3ff',
      rowSelectedHoverBg: '#dbecff',
      cellPaddingBlockSM: 0,
      cellPaddingInlineSM: 8,
      headerBorderRadius: 0,
    },
    Tabs: {
      horizontalMargin: '0',
      titleFontSize: 14,
      itemColor: '#647987',
    },
    Button: {
      primaryShadow: 'none',
      defaultShadow: 'none',
      dangerShadow: 'none',
      fontWeight: 600,
    },
    Card: {
      borderRadiusLG: 8,
      headerFontSize: 16,
    },
    Alert: {
      borderRadiusLG: 8,
    },
  },
}

/**
 * Kích thước cố định của khung ứng dụng và bảng (ui-guide.md mục 3, 9).
 * AppShell đưa chúng thành biến CSS (--app-*) để CSS các component dùng, không ghi số cứng.
 * Lát UI-a giữ bố cục cũ (thanh menu, thanh công cụ, thanh trạng thái); lát UI-b đổi sang Shellbar + Dynamic Page.
 */
export const layoutTokens = {
  menuBarHeight: 28,
  toolbarHeight: 32,
  statusBarHeight: 22,
  navWidth: 240,
  navMinWidth: 180,
  navMaxWidth: 420,
  /** Dòng dữ liệu của bảng (mật độ compact của Fiori). */
  gridRowHeight: 32,
  /** Dòng tiêu đề cột của bảng. */
  gridHeaderHeight: 36,
  /** Dòng nhập của lưới dòng chứng từ. */
  lineRowHeight: 32,
  /**
   * Hệ số nhân độ rộng cột khai báo trong các màn hình (khai báo theo chữ 12px trước đây): chữ 14px cần rộng hơn ~1,2 lần để ngày
   * dd/MM/yyyy, số tiền không bị cắt. Ví dụ cột ngày khai báo 90px → 108px.
   */
  columnWidthScale: 1.2,
} as const

/**
 * Chuyển layoutTokens thành biến CSS gắn vào phần tử gốc của AppShell.
 * @returns Đối tượng style, ví dụ { '--app-menu-bar-height': '28px', ... }.
 */
export function layoutCssVars(): Record<string, string> {
  return {
    '--app-menu-bar-height': `${layoutTokens.menuBarHeight}px`,
    '--app-toolbar-height': `${layoutTokens.toolbarHeight}px`,
    '--app-status-bar-height': `${layoutTokens.statusBarHeight}px`,
    '--app-grid-row-height': `${layoutTokens.gridRowHeight}px`,
    '--app-grid-header-height': `${layoutTokens.gridHeaderHeight}px`,
    '--app-line-row-height': `${layoutTokens.lineRowHeight}px`,
  }
}
