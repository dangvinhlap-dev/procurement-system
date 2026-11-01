import { defineConfig, devices } from '@playwright/test'

/**
 * Cấu hình Playwright cho test giao diện (CLAUDE.md mục 2, 9).
 * Tự chạy API (môi trường Testing → database procurement_test, có sẵn tài khoản admin) và Vite nếu chưa chạy.
 * Nếu API đang chạy sẵn ở cổng 5000 (môi trường Development) thì dùng luôn – dữ liệu khởi tạo hai môi trường giống nhau.
 * Màn hình 1366×768 là kích thước tối thiểu trong ui-guide.md.
 * Địa chỉ đổi được bằng biến môi trường E2E_WEB_URL, E2E_API_URL (mặc định cổng 5173, 5000 như máy dev).
 */
const webUrl = process.env.E2E_WEB_URL ?? 'http://localhost:5173'
const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5000'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  reporter: [['list']],
  use: {
    ...devices['Desktop Chrome'],
    baseURL: webUrl,
    viewport: { width: 1366, height: 768 },
    locale: 'vi-VN',
    timezoneId: 'Asia/Ho_Chi_Minh',
  },
  webServer: [
    {
      command: `dotnet run --project ../Proc.Api --no-launch-profile --urls ${apiUrl}`,
      url: `${apiUrl}/health`,
      env: { ASPNETCORE_ENVIRONMENT: 'Testing' },
      reuseExistingServer: true,
      timeout: 180_000,
    },
    {
      command: 'npm run dev',
      url: webUrl,
      reuseExistingServer: true,
      timeout: 60_000,
    },
  ],
})
