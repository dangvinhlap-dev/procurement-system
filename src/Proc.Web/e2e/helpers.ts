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
 * Sau khi chọn công ty ở ô công ty: còn phiếu chưa lưu thì phần mềm hỏi "Còn phiếu chưa lưu" – bấm "Bỏ thay đổi" nếu có. Không có hộp thoại
 * thì đi tiếp (chờ tối đa 0,5 giây).
 * @param page Trang đang test.
 */
export async function confirmCompanySwitch(page: Page): Promise<void> {
  const unsaved = page.getByRole('dialog', { name: 'Còn phiếu chưa lưu' })
  if (await unsaved.waitFor({ state: 'visible', timeout: 500 }).then(() => true, () => false)) {
    await unsaved.getByRole('button', { name: 'Bỏ thay đổi' }).click()
  }
}

/**
 * Mở màn hình bằng ô lệnh: gõ mã rồi chọn đúng dòng "<mã> – <tên>" (không lấy dòng đầu – mã ngắn như "PO" khớp nhiều màn hình). Phiếu đang
 * sửa dở thì phần mềm hỏi "Rời trang" – test đồng ý.
 * @param page Trang đang test.
 * @param code Mã màn hình, ví dụ "PO".
 */
export async function openByCommand(page: Page, code: string): Promise<void> {
  const box = page.getByPlaceholder('Ô lệnh – tên màn hình')
  await box.fill(code)
  await page.locator('.ant-select-item-option').filter({ hasText: new RegExp(`^${code} – `) }).first().click()
  const leave = page.getByRole('dialog', { name: 'Rời trang khi phiếu chưa lưu' })
  if (await leave.waitFor({ state: 'visible', timeout: 500 }).then(() => true, () => false)) {
    await leave.getByRole('button', { name: 'Rời trang' }).click()
  }
}

/**
 * Vùng nội dung của khung (trang đang mở) – để bộ chọn không gặp chữ trùng trên cây điều hướng, thanh menu.
 * @param page Trang đang test.
 */
export function mainContent(page: Page) {
  return page.locator('main.app-content')
}
