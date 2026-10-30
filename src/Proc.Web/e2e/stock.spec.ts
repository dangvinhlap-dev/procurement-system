import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand, seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * Tồn kho hiện tại và thẻ kho (design.md mục 11) ở công ty BHA: mặt hàng mới nhập 10 hộp × 50.000 vào kho KT (qua API);
 * tìm không dấu trên danh sách tồn, bấm đúp mở thẻ kho của đúng mặt hàng, kho; bấm đúp phát sinh mở phiếu nhập gốc.
 */
test('xem tồn kho hiện tại, mở thẻ kho, mở phiếu gốc', async ({ page, request }) => {
  const { itemCode, receiptNo } = await seedPostedReceipt(request)

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'STK')
  await expect(page.getByRole('heading', { name: 'Tồn kho hiện tại' })).toBeVisible()
  await page.getByRole('textbox', { name: 'Tìm kiếm' }).fill(`${itemCode.toLowerCase()} bulong`)
  const row = page.locator('.data-grid-row', { hasText: itemCode })
  await expect(row).toHaveCount(1)
  await expect(row).toContainText('KT – Kho tổng Bắc Hà')
  await expect(row).toContainText('500.000')
  await expect(row).toContainText('50.000')
  await page.screenshot({ path: 'e2e/screenshots/stock-on-hand.png' })

  await row.dblclick()
  await expect(page).toHaveURL(/\/kho\/the-kho\?itemId=/)
  await expect(page.getByRole('heading', { name: 'Thẻ kho' })).toBeVisible()
  const summary = page.locator('.stock-card-summary')
  await expect(summary).toContainText(`${itemCode} – Bulông thử trả chuyển (HOP)`)
  await expect(summary).toContainText('Tồn đầu 0')
  await expect(summary).toContainText('Tồn cuối 10 (500.000)')
  const entry = page.locator('.data-grid-row', { hasText: receiptNo })
  await expect(entry).toContainText('GRN')
  await expect(entry).toContainText('Đã chốt')
  await page.screenshot({ path: 'e2e/screenshots/stock-card.png' })

  await entry.dblclick()
  await expect(page).toHaveURL(/\/kho\/grn\/[0-9a-f-]{36}$/)
  await expect(page.locator('.document-form-number')).toHaveText(receiptNo)
})
