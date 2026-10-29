import { defineConfig, devices } from '@playwright/test'

/**
 * Cấu hình Playwright cho test giao diện (CLAUDE.md mục 2, 9).
 * Tự chạy Vite ở cổng 5173 nếu chưa chạy; màn hình 1366×768 là kích thước tối thiểu trong ui-guide.md.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  reporter: [['list']],
  use: {
    ...devices['Desktop Chrome'],
    baseURL: 'http://localhost:5173',
    viewport: { width: 1366, height: 768 },
    locale: 'vi-VN',
    timezoneId: 'Asia/Ho_Chi_Minh',
  },
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: true,
    timeout: 60_000,
  },
})
