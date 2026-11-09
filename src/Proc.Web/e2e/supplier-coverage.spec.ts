import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand } from './inventoryHelpers.ts'

/**
 * NCC đủ điều kiện theo nhóm hàng (lát G1 → G1b, design.md mục 18.25): mở bằng mã SUPCOV, mặc định tối thiểu 3 NCC và chỉ hiện nhóm hàng
 * thiếu; đổi mức tối thiểu thì thông báo đổi theo; bỏ "Chỉ nhóm hàng thiếu" thì hiện cả nhóm hàng đủ. Cách đếm kiểm tra ở test tích hợp
 * SupplierCoverageTests.
 */
test('NCC đủ điều kiện: lọc mức tối thiểu và chỉ nhóm hàng thiếu', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'SUPCOV')
  await expect(page.getByRole('heading', { name: 'NCC đủ điều kiện theo nhóm hàng' })).toBeVisible()
  await expect(page.getByRole('alert').first()).toContainText(/nhóm hàng chưa đủ 3 NCC đã duyệt/)
  await expect(page.getByLabel('Chỉ nhóm hàng thiếu')).toBeChecked()
  await page.screenshot({ path: 'e2e/screenshots/supplier-coverage.png' })

  await page.getByLabel('Số NCC tối thiểu').fill('1')
  await page.getByLabel('Số NCC tối thiểu').blur()
  await expect(page.getByRole('alert').first()).toContainText(/nhóm hàng chưa đủ 1 NCC đã duyệt/)
  await page.getByLabel('Chỉ nhóm hàng thiếu').uncheck()
  await expect(page.locator('.data-grid-row').first()).toBeVisible()
})
