import { theme, type ThemeConfig } from 'antd'

/**
 * Theme token của toàn ứng dụng theo docs/ui-guide.md mục "Màu sắc và chữ".
 * Component không viết màu cứng; mọi màu, cỡ chữ, bo góc lấy từ đây (CLAUDE.md mục 9).
 */
export const appTheme: ThemeConfig = {
  // Thuật toán compact: mật độ thông tin cao kiểu phần mềm desktop.
  algorithm: theme.compactAlgorithm,
  token: {
    fontFamily: '"Segoe UI", Tahoma, sans-serif',
    fontSize: 12,
    fontSizeHeading4: 18,
    colorPrimary: '#1F4FA0',
    colorLink: '#1F4FA0',
    colorSuccess: '#2E8B3E',
    colorWarning: '#D9822B',
    colorError: '#C62828',
    colorTextDisabled: '#8A8A8A',
    colorBgLayout: '#F3F3F3',
    colorBgContainer: '#FFFFFF',
    colorBorder: '#C8C8C8',
    controlItemBgActive: '#DCE8F8',
    borderRadius: 2,
    controlHeight: 26,
    boxShadow: 'none',
    boxShadowSecondary: 'none',
  },
}
