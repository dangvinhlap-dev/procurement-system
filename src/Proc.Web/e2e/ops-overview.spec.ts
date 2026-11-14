import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand } from './inventoryHelpers.ts'

/**
 * Lát QT1 – module Quản trị vận hành (design.md mục 21): màn hình SYS hiện phiên bản và số liệu của môi trường đang chạy, Nhật ký vận hành;
 * dải nhận diện môi trường – cấu hình Testing là DEV (dải xám), máy TEST dải cam + tiêu đề tab "[TEST]", máy chưa khai môi trường dải đỏ
 * cảnh báo không gửi email (người dùng chốt 14/11/2026). Hai trường hợp sau giả lập bằng cách chặn API môi trường.
 */
test('SYS hiện phiên bản và số liệu, nhật ký vận hành mở được, dải DEV', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await expect(page.getByTestId('environment-banner')).toContainText('DEV')

  await openByCommand(page, 'SYS')
  await expect(page.getByRole('heading', { name: /Tổng quan hệ thống/ })).toBeVisible()
  await expect(page.getByRole('alert')).toContainText(/Môi trường DEV · phiên bản \d+\.\d+\.\d+/)
  await expect(page.locator('.data-grid-row', { hasText: 'Migration đang chờ' })).toContainText('Ổn')
  await expect(page.locator('.data-grid-row', { hasText: 'Dung lượng CSDL' })).toBeVisible()
  await expect(page.locator('.ant-spin-spinning')).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/ops-overview.png' })

  await openByCommand(page, 'OPLOG')
  await expect(page.getByRole('heading', { name: /Nhật ký vận hành/ })).toBeVisible()
  await expect(page.locator('.ant-spin-spinning')).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/operation-log.png' })
})

test('máy TEST: dải cam trên mọi màn hình, tiêu đề tab có [TEST]', async ({ page }) => {
  await page.route('**/api/ops/v1/environment', (route) => route.fulfill({ json: { environment: 'Test', warning: null } }))
  await loginViaApi(page)
  await page.goto('/')
  const banner = page.getByTestId('environment-banner')
  await expect(banner).toContainText('MÔI TRƯỜNG TEST')
  await expect(banner).toHaveClass(/is-test/)
  await expect.poll(() => page.title()).toMatch(/^\[TEST\] /)
  await openByCommand(page, 'PO')
  await expect(banner).toBeVisible()
  await expect.poll(() => page.title()).toMatch(/^\[TEST\] /)
  await page.screenshot({ path: 'e2e/screenshots/environment-test-banner.png' })
})

test('máy chưa khai môi trường: trang đăng nhập hiện dải đỏ cảnh báo không gửi email', async ({ page }) => {
  await page.route('**/api/ops/v1/environment', (route) =>
    route.fulfill({ json: { environment: 'None', warning: 'Máy chủ chưa khai App:Environment (Dev / Test / Prod) – email không được gửi.' } }),
  )
  await page.goto('/dang-nhap')
  const banner = page.getByRole('alert').filter({ hasText: 'email không được gửi' })
  await expect(banner).toBeVisible()
  await expect(banner).toHaveClass(/is-warning/)
  await page.screenshot({ path: 'e2e/screenshots/environment-not-configured.png' })
})

test('Portal NCC của máy TEST cũng hiện dải cam (API môi trường của site Portal)', async ({ page }) => {
  await page.route('**/api/portal/auth/environment', (route) => route.fulfill({ json: { environment: 'Test', warning: null } }))
  await page.goto('/ncc/dang-nhap')
  await expect(page.getByTestId('environment-banner')).toContainText('MÔI TRƯỜNG TEST')
})
