import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * Tìm kiểu SAP ECC trong ô chọn danh mục (yêu cầu người dùng 02/11/2026, docs/ui-style.md mục 5b): gõ `*` rồi Enter liệt kê toàn bộ
 * danh mục, Enter không tự chọn dòng đầu; mũi tên xuống + Enter mới chọn. Kiểm tra cả ô lọc trên trình duyệt (NCC đề xuất trên PR) và
 * ô tìm trên máy chủ (Mặt hàng trên PR, Mặt hàng ở Thẻ kho).
 */
test('gõ * rồi Enter liệt kê toàn bộ danh mục trong ô chọn', async ({ page, request }) => {
  const { itemCode } = await seedPostedReceipt(request)
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')

  // Ô tìm trên máy chủ: Thẻ kho → Mặt hàng.
  await page.goto('/kho/the-kho')
  const item = page.getByRole('combobox', { name: 'Mặt hàng' })
  const itemBox = page.locator('.ant-select').filter({ has: item })
  await item.click()
  await item.fill('*')
  await item.press('Enter')
  const dropdown = page.locator('.ant-select-dropdown:visible')
  await expect(dropdown).toBeVisible()
  await expect(dropdown.locator('.ant-select-item-option').first()).toBeVisible()
  // Enter không chọn: chưa có mặt hàng, danh sách còn mở.
  await expect(page.locator('.stock-card-summary')).toHaveCount(0)
  // "*" giữa từ khóa là ký tự đại diện: "bul*chuy" khớp "Bulông thử trả chuyển".
  await item.fill(`${itemCode} bul*chuy`)
  await expect(dropdown.getByTitle(`${itemCode} – Bulông thử trả chuyển`)).toBeVisible()
  // Mẫu có * thì Enter chỉ liệt kê; mũi tên xuống rồi Enter mới chọn.
  await item.press('Enter')
  await expect(dropdown).toBeVisible()
  await item.press('ArrowDown')
  await item.press('Enter')
  await expect(itemBox).toContainText(itemCode)
  await expect(page.locator('.stock-card-summary')).toContainText(itemCode)

  // Ô lọc trên trình duyệt: PR mới → NCC đề xuất dòng 1.
  await page.goto('/mua-hang/pr/moi')
  await page.getByLabel('Mục đích sử dụng').click()
  await page.keyboard.press('F2')
  const supplier = page.getByRole('combobox', { name: 'NCC đề xuất dòng 1' })
  await supplier.click()
  await supplier.fill('*')
  await supplier.press('Enter')
  const options = page.locator('.ant-select-dropdown:visible .ant-select-item-option')
  await expect(options.first()).toBeVisible()
  expect(await options.count()).toBeGreaterThan(1)
  await page.screenshot({ path: 'e2e/screenshots/lookup-wildcard.png' })
  // Mũi tên xuống rồi Enter: chọn dòng đầu.
  await supplier.press('ArrowDown')
  await supplier.press('Enter')
  await expect(page.locator('.ant-select-dropdown:visible')).toHaveCount(0)
})
