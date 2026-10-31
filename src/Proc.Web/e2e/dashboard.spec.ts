import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * Trang chủ (design.md mục 11.4): ô số liệu theo công ty đang làm việc, bấm ô mở màn hình chi tiết; số đếm cây điều hướng là số thật.
 */
test('trang chủ: ô số liệu, mua hàng tháng này, bấm ô mở màn hình', async ({ page, request }) => {
  await seedPostedReceipt(request)
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await expect(page.getByRole('heading', { name: /Trang chủ – BHA/ })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Phiếu chờ tôi duyệt' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Hàng dưới tồn tối thiểu' })).toBeVisible()
  // Phiếu nhập vừa ghi sổ ở BHA (10 × 50.000) nằm trong mua hàng tháng này.
  const spend = page.locator('.home-spend')
  await expect(spend).toContainText('Tháng này')
  await expect(spend.locator('.ant-table-row').first()).toBeVisible()
  await expect(page.locator('.app-status-message')).toHaveText(/Sẵn sàng/)
  await page.screenshot({ path: 'e2e/screenshots/dashboard.png' })

  await page.getByRole('button', { name: 'PO đang chờ giao' }).click()
  await expect(page).toHaveURL(/\/mua-hang\/theo-doi-giao-hang$/)
})
