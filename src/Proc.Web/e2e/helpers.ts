import { expect, type Page } from '@playwright/test'

/** Tài khoản quản trị tạo sẵn ở môi trường Testing/Development (appsettings.*.json, mục Seed). */
export const ADMIN = { userName: 'admin', password: 'Admin@2026', fullName: 'Quản trị hệ thống' }

/** Thủ kho công ty BHA: chỉ quyền kho/báo cáo tồn, chỉ kho KP1, nhóm hàng VT (dữ liệu mẫu trong appsettings). */
export const THUKHO_BHA = { userName: 'thukho.bha', password: 'Demo@2026', fullName: 'Trần Thị Bình' }

/** Một tài khoản đăng nhập dùng trong test. */
export type TestUser = { userName: string; password: string }

/** Khóa lưu phiên – phải khớp src/auth/sessionStorage.ts. */
const SESSION_KEY = 'proc.session'

/**
 * Đăng nhập nhanh qua API rồi đặt phiên vào sessionStorage trước khi trang tải,
 * để các test không chuyên về đăng nhập khỏi phải điền form mỗi lần.
 * @param page Trang Playwright.
 * @param user Tài khoản; mặc định admin.
 */
export async function loginViaApi(page: Page, user: TestUser = ADMIN): Promise<void> {
  const response = await page.request.post('/api/auth/login', {
    data: { userName: user.userName, password: user.password },
  })
  expect(response.ok()).toBeTruthy()
  const result = (await response.json()) as {
    accessToken: string
    expiresAt: string
    user: { defaultCompanyId: string }
  }
  const session = JSON.stringify({ ...result, workingCompanyId: result.user.defaultCompanyId })
  await page.addInitScript(
    ([key, value]) => {
      // Chỉ đặt khi chưa có, để test đăng xuất không bị đăng nhập lại khi trang tải lại.
      if (!sessionStorage.getItem(key)) sessionStorage.setItem(key, value)
    },
    [SESSION_KEY, session],
  )
}
