import { type ThemeConfig } from 'antd'

/**
 * Theme token của toàn ứng dụng theo phong cách ERP desktop cổ điển (docs/ui-style.md, chính thức từ 01/11/2026).
 * Component không viết màu cứng; mọi màu, cỡ chữ, bo góc lấy từ đây (CLAUDE.md mục 9).
 * CSS của các component đọc lại các token này qua biến CSS của antd (ví dụ var(--ant-color-border));
 * phần hình khối riêng của phong cách cổ điển (thanh dải màu, tab ô vuông, nút nền be) ở src/classic/classic-app.css.
 */

/**
 * Không đổ bóng (docs/ui-style.md mục 4). Lớp nổi (menu thả xuống, lịch, hộp thoại) dùng "bóng" 1px làm viền rõ ràng
 * cộng vệt tối lệch 2px như cửa sổ desktop, để tách khỏi nền mà không có bóng mờ hiện đại.
 */
const popupFrame = '0 0 0 1px #7f9db9, 2px 2px 0 rgba(27, 42, 58, 0.18)'

/** Token giao diện Ant Design theo phong cách ERP cổ điển (chữ 12px Tahoma, bo góc 2px, không hiệu ứng) – docs/ui-style.md. */
export const appTheme: ThemeConfig = {
  token: {
    fontFamily: 'Tahoma, "Segoe UI", Arial, sans-serif',
    fontSize: 12,
    fontSizeSM: 11,
    fontSizeLG: 13,
    // Tiêu đề màn hình 18px/700 xanh đậm; tiêu đề khối 13px/700 (docs/ui-style.md mục 3).
    fontSizeHeading4: 18,
    fontSizeHeading5: 13,
    fontWeightStrong: 700,
    colorText: '#1b2a3a',
    colorTextHeading: '#1f3d6b',
    colorTextSecondary: '#5a6b7d',
    colorTextDescription: '#5a6b7d',
    colorTextTertiary: '#7a8a9a',
    colorTextDisabled: '#8a97a5',
    // Màu nhấn dùng cho liên kết, viền ô đang nhập, mục chọn; nút lệnh nền be vẽ ở classic-app.css.
    colorPrimary: '#1f5fbf',
    colorLink: '#1f5fbf',
    colorLinkHover: '#1f3d6b',
    colorLinkActive: '#1f3d6b',
    // Màu ngữ nghĩa = màu đèn trạng thái / icon theo mức quan trọng (đỏ, xanh lá, vàng).
    colorSuccess: '#1e8f2e',
    colorSuccessBg: '#eef8ef',
    colorSuccessBorder: '#9fd3a6',
    colorWarning: '#a86f00',
    colorWarningBg: '#fff6c8',
    colorWarningBorder: '#e8c860',
    colorError: '#d0281e',
    colorErrorBg: '#fff0ee',
    colorErrorBorder: '#eea59f',
    colorInfo: '#2a5db0',
    colorInfoBg: '#eef3fb',
    colorInfoBorder: '#a9bbd1',
    colorBgLayout: '#e3eaf2',
    colorBgContainer: '#ffffff',
    colorBgContainerDisabled: '#eef2f6',
    colorBorder: '#7f9db9',
    colorBorderSecondary: '#b8c5d6',
    // Mục chọn trong danh sách/cây: xanh nhạt; dòng chọn của lưới tô vàng nhạt ở classic-app.css.
    controlItemBgActive: '#d6e3f5',
    controlItemBgActiveHover: '#c7d8f0',
    controlItemBgHover: '#eef4fb',
    colorFillAlter: '#f4f7fb',
    // Góc vuông hoặc bo rất nhẹ 2px (docs/ui-style.md mục 4).
    borderRadius: 2,
    borderRadiusSM: 2,
    borderRadiusLG: 2,
    borderRadiusXS: 1,
    // Dày đặc: ô nhập, nút 22px; khoảng cách 2/4/8px.
    controlHeight: 22,
    controlHeightSM: 20,
    controlHeightLG: 26,
    padding: 8,
    paddingSM: 4,
    paddingXS: 2,
    paddingLG: 12,
    margin: 8,
    marginSM: 4,
    marginXS: 2,
    marginLG: 12,
    boxShadow: 'none',
    boxShadowSecondary: popupFrame,
    boxShadowTertiary: 'none',
    // Không hiệu ứng chuyển động.
    motion: false,
  },
  components: {
    Table: {
      headerBg: '#d3ddea',
      headerColor: '#1b2a3a',
      headerSortActiveBg: '#c5d2e3',
      headerSortHoverBg: '#c5d2e3',
      headerSplitColor: '#b8c5d6',
      borderColor: '#b8c5d6',
      rowHoverBg: '#eef4fb',
      rowSelectedBg: '#fff6c8',
      rowSelectedHoverBg: '#fdeea8',
      cellPaddingBlockSM: 0,
      cellPaddingInlineSM: 4,
      headerBorderRadius: 0,
    },
    Tabs: {
      horizontalMargin: '0',
      horizontalItemGutter: 1,
      titleFontSize: 12,
      itemColor: '#1f3d6b',
      itemSelectedColor: '#ffffff',
      itemHoverColor: '#1f3d6b',
      inkBarColor: 'transparent',
    },
    Button: {
      primaryShadow: 'none',
      defaultShadow: 'none',
      dangerShadow: 'none',
      fontWeight: 400,
      defaultBg: '#f3efdf',
      defaultBorderColor: '#b5a97f',
      defaultColor: '#1b2a3a',
      defaultHoverBg: '#fbf8ec',
      defaultHoverBorderColor: '#8f845e',
      defaultHoverColor: '#1b2a3a',
      defaultActiveBg: '#e4dcc0',
      defaultActiveBorderColor: '#8f845e',
      defaultActiveColor: '#1b2a3a',
      paddingInline: 8,
    },
    Card: {
      headerFontSize: 13,
      headerBg: '#d3ddea',
    },
    Modal: {
      headerBg: '#d3ddea',
      titleColor: '#1f3d6b',
      titleFontSize: 13,
    },
    Drawer: {
      footerPaddingBlock: 4,
      footerPaddingInline: 8,
    },
    Form: {
      itemMarginBottom: 6,
      verticalLabelPadding: '0 0 2px',
    },
  },
}

/**
 * Kích thước cố định của khung ứng dụng và bảng (docs/ui-style.md mục 1, 4).
 * AppShell đưa chúng thành biến CSS (--app-*) để CSS các component dùng, không ghi số cứng.
 * Khung cổ điển: thanh menu chữ 24px + thanh công cụ 30px; điều hướng trái 240px (kéo rộng/hẹp được).
 */
export const layoutTokens = {
  /** Tổng chiều cao thanh menu chữ + thanh công cụ trên cùng. */
  shellBarHeight: 54,
  /** Dòng thanh menu chữ (hàng trên của khung). */
  menuBarHeight: 24,
  /** Dòng mục của điều hướng trái. */
  navItemHeight: 22,
  navWidth: 240,
  navMinWidth: 180,
  navMaxWidth: 420,
  /** Dòng dữ liệu của bảng (dày đặc kiểu desktop). */
  gridRowHeight: 22,
  /** Dòng tiêu đề cột của bảng. */
  gridHeaderHeight: 22,
  /** Dòng nhập của lưới dòng chứng từ. */
  lineRowHeight: 22,
  /**
   * Hệ số nhân độ rộng cột khai báo trong các màn hình. Độ rộng cột khai báo theo chữ 12px; phong cách cổ điển dùng lại chữ 12px
   * nên hệ số là 1 (cột ngày khai báo 90px → 90px).
   */
  columnWidthScale: 1,
} as const

/**
 * Chuyển layoutTokens thành biến CSS gắn vào phần tử gốc của AppShell.
 * @returns Đối tượng style, ví dụ { '--app-shell-bar-height': '54px', ... }.
 */
export function layoutCssVars(): Record<string, string> {
  return {
    '--app-shell-bar-height': `${layoutTokens.shellBarHeight}px`,
    '--app-menu-bar-height': `${layoutTokens.menuBarHeight}px`,
    '--app-nav-item-height': `${layoutTokens.navItemHeight}px`,
    '--app-grid-row-height': `${layoutTokens.gridRowHeight}px`,
    '--app-grid-header-height': `${layoutTokens.gridHeaderHeight}px`,
    '--app-line-row-height': `${layoutTokens.lineRowHeight}px`,
  }
}
