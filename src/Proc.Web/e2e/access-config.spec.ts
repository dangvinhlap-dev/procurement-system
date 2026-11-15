import { expect, test } from '@playwright/test'
import { loginViaApi, openByCommand } from './helpers.ts'

/**
 * Lát QT7 – màn hình CFG (Cấu hình truy cập, design.md mục 21.4, 21.16; ui-style.md mục 5p).
 * - Tab Chính sách: các dòng mật khẩu / đăng nhập / dải IP / giới hạn; "Sửa cấu hình…" – thiếu lý do bị chặn, dải IP sai báo lỗi máy chủ trong hộp
 *   thoại (không lưu), lưu lại đúng giá trị cũ (không đổi chính sách của các test đang chạy song song).
 * - "Đăng xuất tất cả…": gõ sai chữ xác nhận bị chặn; API được GIẢ LẬP (page.route) – đăng xuất thật sẽ làm mọi test song song mất phiên. Phần máy
 *   chủ có test tích hợp AccessConfigTests.
 * - Tab Bảo mật: header bảo mật (CSP…). Tab Bảo trì: bật / tắt chế độ bảo trì (chuyển từ SYS) – API GIẢ LẬP vì bật thật chặn test khác.
 */

test('CFG: tab Chính sách, sửa cấu hình (lỗi trong hộp thoại, lưu lại giá trị cũ), đăng xuất tất cả (giả lập), tab Bảo mật', async ({ page }) => {
  let signOutBody: Record<string, string> | null = null
  await page.route('**/api/ops/v1/access-config/sign-out-all', async (route) => {
    signOutBody = route.request().postDataJSON() as Record<string, string>
    const token = (route.request().headers()['authorization'] ?? '').replace(/^Bearer /, '')
    await route.fulfill({
      json: { revokedAt: new Date().toISOString(), accessToken: token, expiresAt: new Date(Date.now() + 3_600_000).toISOString() },
    })
  })
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'CFG')
  await expect(page.getByRole('heading', { name: /Cấu hình truy cập/ })).toBeVisible()
  const grid = page.locator('.data-grid-row')
  await expect(grid.filter({ hasText: 'Độ dài tối thiểu' })).toContainText('ký tự')
  await expect(grid.filter({ hasText: 'Dải IP được dùng' })).toBeVisible()
  await expect(grid.filter({ hasText: 'IP của bạn' })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/access-config-policy.png' })

  await page.getByRole('button', { name: 'Sửa cấu hình…' }).click()
  const dialog = page.getByRole('dialog', { name: 'Sửa cấu hình truy cập' })
  await expect(dialog.getByLabel('Độ dài mật khẩu tối thiểu')).toBeVisible()
  const ranges = dialog.getByLabel('Dải IP được dùng chức năng quản trị vận hành')
  const oldRanges = await ranges.inputValue()
  await dialog.getByRole('button', { name: 'Lưu' }).click()
  await expect(dialog).toContainText('Nhập lý do.')
  await ranges.fill('10.0.0.300')
  await dialog.getByLabel('Lý do').fill('Thử khai dải IP sai (e2e)')
  await dialog.getByRole('button', { name: 'Lưu' }).click()
  await expect(dialog.getByRole('alert')).toContainText('Địa chỉ IP "10.0.0.300" không đúng dạng.')
  await page.screenshot({ path: 'e2e/screenshots/access-config-edit.png' })
  await ranges.fill(oldRanges)
  await dialog.getByLabel('Lý do').fill('Lưu lại giá trị cũ (e2e)')
  await dialog.getByRole('button', { name: 'Lưu' }).click()
  await expect(dialog).toBeHidden()
  await expect(page.getByRole('alert').first()).toContainText(/Lưu lần cuối .* – admin/)

  await page.getByRole('button', { name: 'Đăng xuất tất cả…' }).click()
  const signOut = page.getByRole('dialog', { name: 'Đăng xuất tất cả' })
  await signOut.getByLabel('Lý do').fill('Thử đăng xuất tất cả (e2e)')
  await signOut.getByLabel('Mật khẩu của bạn').fill('Admin@2026')
  await signOut.getByLabel(/Gõ "ĐĂNG XUẤT TẤT CẢ"/).fill('ĐĂNG XUẤT')
  await signOut.getByRole('button', { name: 'Đăng xuất tất cả' }).click()
  await expect(signOut).toContainText('Gõ đúng "ĐĂNG XUẤT TẤT CẢ".')
  await page.screenshot({ path: 'e2e/screenshots/access-config-sign-out-all.png' })
  await signOut.getByLabel(/Gõ "ĐĂNG XUẤT TẤT CẢ"/).fill('đăng xuất tất cả')
  await signOut.getByRole('button', { name: 'Đăng xuất tất cả' }).click()
  await expect(signOut).toBeHidden()
  await expect(page.getByText('Đã đăng xuất mọi phiên đăng nhập khác.')).toBeVisible()
  expect(signOutBody).toMatchObject({ reason: 'Thử đăng xuất tất cả (e2e)', confirmation: 'đăng xuất tất cả' })
  // Phiên vẫn dùng được sau khi thay token.
  await expect(page.getByRole('heading', { name: /Cấu hình truy cập/ })).toBeVisible()

  await page.getByRole('tab', { name: 'Bảo mật' }).click()
  await expect(grid.filter({ hasText: 'Content-Security-Policy' })).toContainText("default-src 'self'")
  await expect(grid.filter({ hasText: 'Đăng nhập nội bộ' })).toContainText('lần / phút / IP')
  await page.screenshot({ path: 'e2e/screenshots/access-config-security.png' })
})

test('CFG tab Bảo trì: bật chế độ bảo trì – thông báo đỏ, dải ĐANG BẢO TRÌ, SYS báo; tắt lại', async ({ page }) => {
  let mode = { isOn: false, message: null as string | null, reason: null as string | null, startedAt: null as string | null, changedByName: null as string | null, changedAt: null as string | null }
  const sent: { isOn: boolean; message: string | null; reason: string }[] = []
  await page.route('**/api/ops/v1/maintenance-mode', async (route) => {
    if (route.request().method() === 'PUT') {
      const body = route.request().postDataJSON() as { isOn: boolean; message: string | null; reason: string }
      sent.push(body)
      const now = new Date().toISOString()
      mode = { isOn: body.isOn, message: body.isOn ? body.message : null, reason: body.reason, startedAt: body.isOn ? now : null, changedByName: 'admin', changedAt: now }
    }
    await route.fulfill({ json: mode })
  })
  await page.route('**/api/ops/v1/environment', async (route) => {
    const real = (await (await route.fetch()).json()) as Record<string, unknown>
    await route.fulfill({ json: { ...real, maintenance: mode.isOn ? { message: mode.message, startedAt: mode.startedAt } : null } })
  })
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'CFG')
  await page.getByRole('tab', { name: 'Bảo trì' }).click()
  await expect(page.locator('.data-grid-row', { hasText: 'Trạng thái' })).toContainText('Đang tắt')
  const toggleOn = page.getByRole('button', { name: 'Bật chế độ bảo trì…' })
  await toggleOn.click()
  const dialog = page.getByRole('dialog')
  await dialog.getByLabel('Câu thông báo người dùng thấy').fill('Đang cài bản mới (e2e), quay lại sau 5 phút.')
  await dialog.getByRole('button', { name: 'Bật bảo trì' }).click()
  await expect(dialog).toContainText('Nhập lý do.')
  await dialog.getByLabel('Lý do').fill('Thử chế độ bảo trì e2e')
  await dialog.getByRole('button', { name: 'Bật bảo trì' }).click()
  try {
    await expect(page.getByTestId('cfg-maintenance-notice')).toContainText('Đang cài bản mới (e2e)')
    await expect(page.getByTestId('environment-banner')).toContainText('ĐANG BẢO TRÌ: Đang cài bản mới (e2e), quay lại sau 5 phút.')
    await page.screenshot({ path: 'e2e/screenshots/maintenance-on.png' })
    await openByCommand(page, 'SYS')
    await expect(page.getByTestId('maintenance-notice')).toContainText('Đang cài bản mới (e2e)')
    await expect(page.getByRole('button', { name: /chế độ bảo trì/ })).toHaveCount(0)
    await openByCommand(page, 'CFG')
    await page.getByRole('tab', { name: 'Bảo trì' }).click()
  } finally {
    await page.getByRole('button', { name: 'Tắt chế độ bảo trì…' }).click()
    const off = page.getByRole('dialog')
    await off.getByLabel('Lý do').fill('Xong thử chế độ bảo trì')
    await off.getByRole('button', { name: 'Tắt bảo trì' }).click()
  }
  await expect(page.getByTestId('cfg-maintenance-notice')).toBeHidden()
  await expect(page.getByRole('button', { name: 'Bật chế độ bảo trì…' })).toBeVisible()
  await expect(page.getByTestId('environment-banner')).not.toContainText('ĐANG BẢO TRÌ')
  expect(sent.map((s) => [s.isOn, s.reason])).toEqual([
    [true, 'Thử chế độ bảo trì e2e'],
    [false, 'Xong thử chế độ bảo trì'],
  ])
})
