import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand } from './inventoryHelpers.ts'

/**
 * Đánh số danh mục (design.md mục 5.11): màn hình cấu hình dải mã của nhà cung cấp, nhóm NCC, nhóm hàng, hàng hóa – dữ liệu mẫu có sẵn dải
 * mặc định; mở dải mã hàng thấy tiền tố {NHOM}-.
 */
test('màn hình đánh số danh mục: xem dải mặc định, mở chi tiết', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'DSDM')
  await expect(page.getByRole('heading', { name: 'Đánh số danh mục' })).toBeVisible()
  for (const name of ['Nhà cung cấp', 'Nhóm nhà cung cấp', 'Nhóm hàng']) {
    await expect(page.locator('.data-grid-row', { hasText: name }).first()).toBeVisible()
  }
  const itemRow = page.locator('.data-grid-row', { hasText: 'Mặc định mọi nhóm' })
  await expect(itemRow).toContainText('{NHOM}-')
  await expect(itemRow).toContainText('Theo từng nhóm hàng')
  await page.screenshot({ path: 'e2e/screenshots/catalog-numbering.png' })

  await itemRow.dblclick()
  const drawer = page.locator('.detail-drawer')
  await expect(drawer.getByLabel('Tiền tố')).toHaveValue('{NHOM}-')
  await expect(drawer.getByRole('button', { name: 'Lưu' })).toBeVisible()
})
