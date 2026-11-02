import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand, seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * Tra cứu chứng từ (DOCS – design.md mục 18.22): mọi loại chứng từ, kể cả phiếu kho, đã hoàn thành hay chưa; lọc tình trạng;
 * bấm đúp mở trang đầy đủ của chứng từ.
 */
test('tra cứu mọi loại chứng từ, bấm đúp mở trang phiếu', async ({ page, request }) => {
  const { receiptNo } = await seedPostedReceipt(request)
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'DOCS')
  await expect(page.getByRole('heading', { name: 'Tra cứu chứng từ' })).toBeVisible()

  await page.getByRole('textbox', { name: 'Tìm kiếm' }).fill(receiptNo)
  const row = page.locator('.data-grid-row', { hasText: receiptNo })
  await expect(row).toHaveCount(1)
  await expect(row).toContainText('Nhập mua NCC')
  await expect(row).toContainText('Hoàn thành')
  await expect(row).toContainText('Đã ghi sổ')

  // Lọc "Chưa hoàn thành": phiếu đã ghi sổ không còn.
  const progress = page.getByRole('combobox', { name: 'Tình trạng' })
  await progress.click()
  await page.locator('.ant-select-dropdown:visible').getByTitle('Chưa hoàn thành').click()
  await expect(page.locator('.data-grid-row', { hasText: receiptNo })).toHaveCount(0)
  await progress.click()
  await page.locator('.ant-select-dropdown:visible').getByTitle('Tất cả').click()
  await expect(row).toHaveCount(1)
  await page.screenshot({ path: 'e2e/screenshots/document-registry.png' })

  await row.dblclick()
  await expect(page).toHaveURL(/\/kho\/grn\/[0-9a-f-]{36}$/)
  await expect(page.locator('.document-form-number')).toHaveText(receiptNo)
})
