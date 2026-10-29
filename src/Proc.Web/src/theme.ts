import { theme, type ThemeConfig } from 'antd'

/**
 * Theme token của toàn ứng dụng theo docs/ui-guide.md mục "Màu sắc và chữ".
 * Component không viết màu cứng; mọi màu, cỡ chữ, bo góc lấy từ đây (CLAUDE.md mục 9).
 * CSS của các component đọc lại các token này qua biến CSS của antd (ví dụ var(--ant-color-border)).
 */
export const appTheme: ThemeConfig = {
  // Thuật toán compact: mật độ thông tin cao kiểu phần mềm desktop.
  algorithm: theme.compactAlgorithm,
  token: {
    fontFamily: '"Segoe UI", Tahoma, sans-serif',
    fontSize: 12,
    fontSizeSM: 11,
    fontSizeLG: 13,
    fontSizeHeading4: 18,
    colorPrimary: '#1F4FA0',
    colorLink: '#1F4FA0',
    colorSuccess: '#2E8B3E',
    colorWarning: '#D9822B',
    colorError: '#C62828',
    // Màu trung tính của trạng thái (xám) – ui-guide.md.
    colorTextDisabled: '#8A8A8A',
    colorBgLayout: '#F3F3F3',
    colorBgContainer: '#FFFFFF',
    colorBorder: '#C8C8C8',
    colorBorderSecondary: '#DCDCDC',
    // Nền dòng chọn trong lưới / mục chọn trong danh sách.
    controlItemBgActive: '#DCE8F8',
    controlItemBgActiveHover: '#DCE8F8',
    // Nền xám rất nhạt: dòng xen kẽ của lưới, khung bộ lọc.
    colorFillAlter: '#F7F7F7',
    borderRadius: 2,
    borderRadiusSM: 2,
    borderRadiusLG: 2,
    controlHeight: 26,
    controlHeightSM: 22,
    boxShadow: 'none',
    boxShadowSecondary: '0 2px 6px rgba(0, 0, 0, 0.15)',
  },
  components: {
    Table: {
      headerBg: '#EFEFEF',
      headerSortActiveBg: '#E6E6E6',
      headerSortHoverBg: '#E6E6E6',
      headerSplitColor: '#C8C8C8',
      borderColor: '#DCDCDC',
      rowHoverBg: '#EEF3FB',
      rowSelectedBg: '#DCE8F8',
      rowSelectedHoverBg: '#DCE8F8',
      cellPaddingBlockSM: 0,
      cellPaddingInlineSM: 6,
      headerBorderRadius: 0,
    },
    Tabs: {
      cardBg: '#EFEFEF',
      cardHeight: 26,
      cardPadding: '2px 10px',
      horizontalMargin: '0',
    },
    Button: {
      primaryShadow: 'none',
      defaultShadow: 'none',
      dangerShadow: 'none',
    },
  },
}

/**
 * Kích thước cố định của khung ứng dụng theo ui-guide.md mục "Khung ứng dụng (AppShell)".
 * AppShell đưa chúng thành biến CSS (--app-*) để CSS các component dùng, không ghi số cứng.
 */
export const layoutTokens = {
  menuBarHeight: 28,
  toolbarHeight: 32,
  statusBarHeight: 22,
  navWidth: 240,
  navMinWidth: 180,
  navMaxWidth: 420,
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
  }
}
