import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand } from './inventoryHelpers.ts'

/**
 * NCC đủ điều kiện theo mặt hàng (lát G1, design.md mục 18.25): mở bằng mã SUPCOV, mặc định tối thiểu 3 NCC và chỉ hiện mặt hàng thiếu;
 * đổi mức tối thiểu thì thông báo đổi theo; bỏ "Chỉ mặt hàng thiếu" thì hiện cả mặt hàng đủ.
 */
test('NCC đủ điều kiện: lọc mức tối thiểu và chỉ mặt hàng thiếu', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'SUPCOV')
  await expect(page.getByRole('heading', { name: 'NCC đủ điều kiện theo mặt hàng' })).toBeVisible()
  await expect(page.getByRole('alert').first()).toContainText(/mặt hàng chưa đủ 3 NCC đã duyệt/)
  await expect(page.getByLabel('Chỉ mặt hàng thiếu')).toBeChecked()
  await page.screenshot({ path: 'e2e/screenshots/supplier-coverage.png' })

  await page.getByLabel('Số NCC tối thiểu').fill('1')
  await page.getByLabel('Số NCC tối thiểu').blur()
  await expect(page.getByRole('alert').first()).toContainText(/mặt hàng chưa đủ 1 NCC đã duyệt/)
  await page.getByLabel('Chỉ mặt hàng thiếu').uncheck()
  await expect(page.locator('.data-grid-row').first()).toBeVisible()
})
