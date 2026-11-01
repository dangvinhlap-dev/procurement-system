import { type ThemeConfig } from 'antd'

/**
 * Bảng màu phong cách ERP desktop cổ điển (BẢN THỬ – docs/ui-style.md mục 2).
 * Giá trị trùng với biến --erp-* trong classic.css; giữ ở đây để dùng cho token antd (hộp thoại, ô chọn ngày)
 * khi các component antd được đặt bên trong ClassicScreen.
 */
export const classicColors = {
  bg: '#e3eaf2',
  text: '#1b2a3a',
  title: '#1f3d6b',
  tabBg: '#b9cde8',
  tabActive: '#7fa2d1',
  btnBg: '#f3efdf',
  btnBorder: '#b5a97f',
  gridHead: '#d3ddea',
  gridLine: '#b8c5d6',
  inputBorder: '#7f9db9',
  inputFocus: '#1f5fbf',
  readonlyBg: '#eef2f6',
  subtotal: '#2a5db0',
  lightGreen: '#2e9e3a',
  lightYellow: '#e8b400',
  lightRed: '#d0281e',
  required: '#b00e12',
} as const

/** Phông chung 12px và phông đơn cách cho cột số (docs/ui-style.md mục 3). */
export const classicFonts = {
  body: 'Tahoma, "Segoe UI", Arial, sans-serif',
  mono: 'Consolas, "Courier New", monospace',
} as const

/**
 * Theme antd cho vùng ClassicScreen: chữ 12px, bo góc 2px, không đổ bóng, chiều cao điều khiển 20px.
 * Lồng trong ConfigProvider của trang nên không ảnh hưởng theme Fiori của các màn hình khác.
 */
export const classicTheme: ThemeConfig = {
  token: {
    fontFamily: classicFonts.body,
    fontSize: 12,
    fontSizeSM: 11,
    colorText: classicColors.text,
    colorPrimary: classicColors.inputFocus,
    colorBorder: classicColors.inputBorder,
    colorBgLayout: classicColors.bg,
    colorBgContainerDisabled: classicColors.readonlyBg,
    colorError: classicColors.lightRed,
    borderRadius: 2,
    borderRadiusSM: 2,
    borderRadiusLG: 2,
    controlHeight: 20,
    controlHeightSM: 18,
    boxShadow: 'none',
    boxShadowSecondary: 'none',
    motion: false,
  },
}
