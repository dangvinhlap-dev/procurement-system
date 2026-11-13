import { expect, type Page } from '@playwright/test'
import { readFileSync } from 'node:fs'

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

/**
 * Số phiên bản đang phát hành, đọc từ Directory.Build.props (nơi duy nhất ghi phiên bản – CLAUDE.md mục 8), để test không phải
 * sửa mỗi lần phát hành.
 * @returns Ví dụ "0.5.0".
 */
export function appVersion(): string {
  const props = readFileSync(new URL('../../../Directory.Build.props', import.meta.url), 'utf8')
  return /<Version>\s*([^<\s]+)\s*<\/Version>/.exec(props)![1]
}

/**
 * Sau khi chọn công ty ở ô công ty: đang mở nhiều thẻ màn hình thì phần mềm hỏi "Đổi công ty đang làm việc" (docs/ui-style.md mục 8a),
 * còn phiếu chưa lưu thì hỏi "Còn phiếu chưa lưu" – bấm đồng ý nếu có. Không có hộp thoại thì đi tiếp (chờ tối đa 1 giây).
 * @param page Trang đang test.
 */
export async function confirmCompanySwitch(page: Page): Promise<void> {
  const ask = page.getByRole('dialog', { name: 'Đổi công ty đang làm việc' })
  if (await ask.waitFor({ state: 'visible', timeout: 1000 }).then(() => true, () => false)) {
    await ask.getByRole('button', { name: 'Đổi công ty' }).click()
  }
  const unsaved = page.getByRole('dialog', { name: 'Còn phiếu chưa lưu' })
  if (await unsaved.waitFor({ state: 'visible', timeout: 500 }).then(() => true, () => false)) {
    await unsaved.getByRole('button', { name: 'Bỏ thay đổi' }).click()
  }
}

/**
 * Mở màn hình ở **thẻ đang xem** bằng ô lệnh `/n<mã>` (docs/ui-style.md mục 8a) – giữ cách các test viết trước khi có thẻ màn hình: mỗi lúc
 * chỉ một màn hình, bộ chọn trên cả trang không gặp bản sao ở thẻ ẩn. Phiếu đang sửa dở thì phần mềm hỏi "Rời trang" – test đồng ý.
 * @param page Trang đang test.
 * @param code Mã màn hình, ví dụ "PO".
 */
export async function openInCurrentTab(page: Page, code: string): Promise<void> {
  const box = page.getByPlaceholder('Ô lệnh – tên màn hình')
  await box.fill(`/n${code}`)
  await box.press('Enter')
  const leave = page.getByRole('dialog', { name: 'Rời trang khi phiếu chưa lưu' })
  if (await leave.waitFor({ state: 'visible', timeout: 500 }).then(() => true, () => false)) {
    await leave.getByRole('button', { name: 'Rời trang' }).click()
  }
}

/**
 * Vùng của thẻ màn hình đang xem – dùng khi thẻ ẩn có cùng nội dung (ví dụ bấm đúp ô số chứng từ mở phiếu gốc ở thẻ mới, danh sách vẫn
 * nằm trong thẻ cũ).
 * @param page Trang đang test.
 */
export function activeTab(page: Page) {
  return page.locator('.app-tab-panel.is-active')
}
