import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand } from './inventoryHelpers.ts'

/**
 * Lát QT4 – màn hình DBM (Bảo trì CSDL, design.md mục 21.4; ui-style.md mục 5m): tab Bảng có số liệu thật của CSDL test; VACUUM ANALYZE một
 * bảng chạy thật qua hàng chờ (API Testing bật bộ chạy nền) → tab Lịch sử "Xong". Tab Phiên và khóa: hộp thoại ngắt phiên xác nhận Q6 – gõ sai
 * chữ xác nhận bị chặn ngay, sai mật khẩu máy chủ báo lỗi trong hộp thoại; không ngắt phiên thật (phiên của API đang chạy test khác).
 */
test('DBM: tab Bảng, VACUUM ANALYZE một bảng chạy thật, Lịch sử', async ({ page }) => {
  test.setTimeout(120_000)
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'DBM')
  await expect(page.getByRole('heading', { name: /Bảo trì CSDL/ })).toBeVisible()
  await expect(page.getByRole('tab', { name: 'Bảng', selected: true })).toBeVisible()
  await expect(page.getByRole('alert')).toContainText(/CSDL procurement\w*: .* bảng · số liệu lúc/)

  const item = page.locator('[data-row-key="public.Item"]')
  await expect(item).toBeVisible()
  await expect(page.getByRole('button', { name: /REINDEX$/ })).toBeDisabled()
  await item.click()
  await page.screenshot({ path: 'e2e/screenshots/db-maintenance-tables.png' })

  await page.getByRole('button', { name: /VACUUM ANALYZE$/ }).click()
  await expect(page.locator('.ant-modal-confirm')).toContainText('Chạy VACUUM ANALYZE bảng Item?')
  await page.locator('.ant-modal-confirm').getByRole('button', { name: 'Chạy', exact: true }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã đặt VACUUM ANALYZE bảng Item vào hàng chờ')
  await expect(page.getByRole('tab', { name: 'Lịch sử', selected: true })).toBeVisible()

  const row = page.locator('.data-grid-row', { hasText: 'public.Item' }).first()
  await expect(row).toContainText('Xong', { timeout: 60_000 })
  await expect(row).toContainText('VACUUM ANALYZE')
  await expect(page.getByRole('alert').filter({ hasText: 'Lịch hằng tuần' })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/db-maintenance-history.png' })
})

test('DBM: Phiên và khóa – hộp thoại ngắt phiên đòi lý do, mật khẩu, chữ "NGẮT PHIÊN"', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'DBM')
  await page.getByRole('tab', { name: 'Phiên và khóa' }).click()
  await expect(page.getByRole('alert')).toContainText(/phiên · \d+ phiên chờ khóa/)
  const terminate = page.getByRole('button', { name: 'Ngắt phiên…' })
  await expect(terminate).toBeDisabled()

  await page.locator('.data-grid-row').filter({ hasNotText: 'phiên đang đọc' }).first().click()
  await expect(terminate).toBeEnabled()
  await terminate.click()
  const dialog = page.getByRole('dialog')
  await expect(dialog).toContainText(/Ngắt phiên \d+/)

  await dialog.getByLabel('Lý do').fill('Thử hộp thoại')
  await dialog.getByLabel('Mật khẩu của bạn').fill('sai-mat-khau')
  await dialog.getByLabel(/Gõ "NGẮT PHIÊN"/).fill('NGAT PHIEN')
  await dialog.getByRole('button', { name: 'Ngắt phiên' }).click()
  await expect(dialog).toContainText('Gõ đúng "NGẮT PHIÊN".')

  await dialog.getByLabel(/Gõ "NGẮT PHIÊN"/).fill('ngắt phiên')
  await dialog.getByRole('button', { name: 'Ngắt phiên' }).click()
  await expect(dialog.getByRole('alert').filter({ hasText: 'Mật khẩu xác nhận không đúng.' })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/db-maintenance-terminate.png' })

  await dialog.getByRole('button', { name: 'Không ngắt' }).click()
  await expect(dialog).toBeHidden()
  await page.screenshot({ path: 'e2e/screenshots/db-maintenance-sessions.png' })
})
