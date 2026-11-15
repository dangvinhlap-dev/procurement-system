import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand } from './inventoryHelpers.ts'

/**
 * Lát QT2 – Nhật ký lỗi ERR (design.md mục 21.4): gây lỗi thử ở máy chủ và giao diện đều thấy trong ERR; mã theo dõi trên câu báo lỗi người dùng
 * thấy tìm ra đúng nhóm lỗi; lỗi khi vẽ một màn hình chỉ làm hỏng vùng nội dung, hiện khối báo lỗi kèm mã theo dõi.
 */
test('lỗi máy chủ thử: câu báo lỗi có mã theo dõi, tìm theo mã ra nhóm lỗi, xem chi tiết kỹ thuật', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'ERR')
  await expect(page.getByRole('heading', { name: /Nhật ký lỗi/ })).toBeVisible()

  await page.getByRole('button', { name: 'Thử lỗi máy chủ' }).click()
  const toast = page.locator('.ant-message-notice').filter({ hasText: 'mã theo dõi' })
  await expect(toast).toBeVisible()
  const code = /ERR-[0-9A-Z]{8}/.exec((await toast.textContent()) ?? '')?.[0]
  expect(code).toBeTruthy()

  const search = page.getByPlaceholder('ERR-…')
  await search.fill(code!.toLowerCase())
  await search.press('Enter')
  const row = page.locator('.data-grid-row', { hasText: code! })
  await expect(row).toHaveCount(1)
  await expect(row).toContainText('System.InvalidOperationException')
  await expect(page.locator('.ant-spin-spinning')).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/error-log.png' })

  await row.dblclick()
  const drawer = page.locator('.ant-drawer')
  await expect(drawer).toContainText('Nhóm lỗi: System.InvalidOperationException')
  await page.screenshot({ path: 'e2e/screenshots/error-log-drawer.png' })
  await drawer.getByRole('tab', { name: /Chi tiết kỹ thuật/i }).click()
  await expect(drawer.getByTestId('error-detail')).toContainText('InvalidOperationException')
})

test('lỗi giao diện thử (window.onerror) vào nhật ký lỗi, nguồn Giao diện', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'ERR')
  await page.getByRole('button', { name: 'Thử lỗi giao diện' }).click()
  const row = page.locator('.data-grid-row', { hasText: 'Lỗi giao diện thử' }).first()
  await expect(row).toBeVisible({ timeout: 10_000 })
  await expect(row).toContainText('Giao diện')
})

test('màn hình vẽ lỗi: vùng nội dung hiện khối báo lỗi kèm mã theo dõi, màn hình khác vẫn mở được', async ({ page }) => {
  // Dữ liệu tổng quan hỏng (thiếu danh sách dòng) làm màn hình SYS lỗi khi vẽ.
  await page.route('**/api/ops/v1/overview', (route) =>
    route.fulfill({ json: { environment: 'Dev', version: '0.0.0', checkedAt: new Date().toISOString(), appliedMigrations: 0, pendingMigrations: [], rows: null } }),
  )
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'SYS')
  const fallback = page.getByTestId('error-fallback')
  await expect(fallback).toContainText('Màn hình gặp lỗi giao diện')
  await expect(fallback).toContainText(/Mã theo dõi ERR-[0-9A-Z]{8}/)
  await page.screenshot({ path: 'e2e/screenshots/error-fallback.png' })

  await openByCommand(page, 'OPLOG')
  await expect(page.getByRole('heading', { name: /Nhật ký vận hành/ })).toBeVisible()
})
