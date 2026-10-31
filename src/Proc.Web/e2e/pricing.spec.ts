import { expect, test, type Page } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { chooseOption, openByCommand, pickOption, seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * Bảng giá NCC và so sánh giá (design.md mục 8.1, 8.2): tổng công ty lập bảng giá toàn tập đoàn, đưa vào hiệu lực → màn hình so sánh
 * giá thấy giá thực mua của BHA (phiếu nhập 50.000 đ/hộp) và giá bảng giá → PO ở BHA gợi ý giá theo bảng giá.
 */

/** Bấm nút lệnh rồi nút cùng tên trong hộp xác nhận, chờ thông báo. */
async function confirm(page: Page, button: string, messageText: string | RegExp) {
  const name = new RegExp(`(^| )${button}$`)
  await page.getByRole('button', { name }).click()
  await page.getByRole('dialog').getByRole('button', { name }).click()
  await expect(page.locator('.ant-message')).toContainText(messageText)
  await expect(page.locator('.ant-modal-confirm')).toHaveCount(0)
}

test('bảng giá toàn tập đoàn → so sánh giá → PO gợi ý giá', async ({ page, request }) => {
  test.setTimeout(90_000)
  const { itemCode, supplierCode } = await seedPostedReceipt(request)
  await loginViaApi(page)
  await page.goto('/')

  // 1. Tổng công ty: bảng giá toàn tập đoàn 47.000 đ/hộp.
  await openByCommand(page, 'PRL')
  await expect(page.getByRole('heading', { name: 'Bảng giá nhà cung cấp' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await chooseOption(page, 'Nhà cung cấp', supplierCode)
  await page.getByLabel('Tên bảng giá').fill('Bảng giá bulông 2026')
  await page.getByLabel('Áp dụng toàn tập đoàn').check()
  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(itemCode.toLowerCase())
  await pickOption(page, itemCode)
  await page.getByLabel('Đơn giá dòng 1').fill('47000')
  await page.getByLabel('Đơn giá dòng 1').press('Tab')
  await expect(page.locator('.line-grid-row').first()).toContainText('47.000')
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu bảng giá PRL-TCT-\d{4}-\d{4}\./)
  await expect(page.locator('.document-form-number')).toHaveText(/^PRL-TCT-\d{4}-\d{4}$/)
  await confirm(page, 'Đưa vào hiệu lực', 'đã có hiệu lực')
  await expect(page.locator('.document-form-title')).toContainText('Hiệu lực')
  await page.screenshot({ path: 'e2e/screenshots/supplier-price-list.png' })

  // 2. So sánh giá toàn tập đoàn: BHA mua 50.000; bảng giá 47.000.
  await openByCommand(page, 'PCMP')
  await expect(page.getByRole('heading', { name: 'So sánh giá' })).toBeVisible()
  await chooseOption(page, 'Mặt hàng', itemCode)
  await expect(page.getByRole('alert').filter({ hasText: 'Toàn tập đoàn' })).toContainText('thấp nhất 50.000')
  const row = page.locator('.data-grid-row', { hasText: 'BHA' })
  await expect(row).toContainText(supplierCode)
  await expect(row).toContainText('50.000')
  await expect(row).toContainText('Thấp nhất')
  await page.keyboard.press('Escape')
  await page.screenshot({ path: 'e2e/screenshots/price-comparison.png' })
  await page.getByRole('combobox', { name: 'Xem', exact: true }).click()
  await pickOption(page, 'Bảng giá NCC đang hiệu lực')
  await expect(page.locator('.data-grid-row', { hasText: supplierCode })).toContainText('47.000')
  await expect(page.locator('.data-grid-row', { hasText: supplierCode })).toContainText('Toàn tập đoàn')

  // 3. PO ở BHA: cột Giá bảng giá gợi ý 47.000, bấm Áp giá bảng giá.
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'PO')
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await chooseOption(page, 'Nhà cung cấp', supplierCode)
  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(itemCode.toLowerCase())
  await pickOption(page, itemCode)
  await page.getByLabel('SL đặt dòng 1').fill('5')
  await page.getByLabel('SL đặt dòng 1').press('Tab')
  await expect(page.locator('.line-grid-row').first()).toContainText('47.000')
  await page.getByRole('button', { name: /Áp giá bảng giá$/ }).click()
  await expect(page.getByLabel('Đơn giá dòng 1')).toHaveValue('47.000')
  await expect(page.locator('.document-form-footer')).toContainText('258.500')
})
