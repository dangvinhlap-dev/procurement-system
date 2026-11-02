import { expect, test, type Page } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { approvePriceList, chooseOption, openByCommand, pickOption, seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * Bảng giá NCC và so sánh giá (design.md mục 8.1, 8.2): tổng công ty lập bảng giá toàn tập đoàn, gửi duyệt, người duyệt giá duyệt → so sánh
 * giá thấy giá thực mua của BHA (phiếu nhập 50.000 đ/hộp) và giá bảng giá → PO ở BHA lấy giá đã duyệt theo bảng giá; sửa giá thì phải
 * ghi lý do lệch giá và PO qua thêm chặng duyệt ngoại lệ giá (design.md mục 18.10).
 */

/** Bấm nút lệnh rồi nút cùng tên trong hộp xác nhận, chờ thông báo. */
async function confirm(page: Page, button: string, messageText: string | RegExp) {
  const name = new RegExp(`(^| )${button}$`)
  await page.getByRole('button', { name }).click()
  await page.getByRole('dialog').getByRole('button', { name }).click()
  await expect(page.locator('.ant-message')).toContainText(messageText)
  await expect(page.locator('.ant-modal-confirm')).toHaveCount(0)
}

test('bảng giá toàn tập đoàn → so sánh giá → PO theo giá đã duyệt, lệch giá qua duyệt ngoại lệ', async ({ page, request }) => {
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
  // Bảng giá phải duyệt mới hiệu lực, người lập không tự duyệt (lát B1): gửi duyệt → Chờ duyệt, không có nút Duyệt.
  await confirm(page, 'Gửi duyệt', /Đã gửi duyệt bảng giá/)
  await expect(page.locator('.document-form-title')).toContainText('Chờ duyệt')
  await expect(page.getByRole('button', { name: /(^| )Duyệt$/ })).toHaveCount(0)
  await expect(page.getByRole('button', { name: /Rút về nháp$/ })).toBeVisible()
  await approvePriceList(request, page.url().split('/').pop()!)
  await page.reload()
  await expect(page.locator('.document-form-title')).toContainText('Hiệu lực')
  await page.getByRole('tab', { name: 'Lịch sử duyệt' }).click()
  await expect(page.getByRole('tabpanel')).toContainText('Phan Thị Giá')
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

  // 3. PO ở BHA: cột Giá đã duyệt 47.000 nguồn Bảng giá; đơn giá 0 là lệch giá (tam giác vàng), bấm Áp giá đã duyệt thì hết lệch.
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'PO')
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await chooseOption(page, 'Nhà cung cấp', supplierCode)
  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(itemCode.toLowerCase())
  await pickOption(page, itemCode)
  await page.getByLabel('SL đặt dòng 1').fill('5')
  await page.getByLabel('SL đặt dòng 1').press('Tab')
  const line = page.locator('.line-grid-row').first()
  await expect(line).toContainText('47.000')
  await expect(line).toContainText(/Bảng giá PRL-TCT-\d{4}-\d{4}/)
  await expect(page.getByLabel('Lệch giá đã duyệt dòng 1')).toBeVisible()
  await page.getByRole('button', { name: /Áp giá đã duyệt$/ }).click()
  await expect(page.getByLabel('Đơn giá dòng 1')).toHaveValue('47.000')
  await expect(page.locator('.document-form-footer')).toContainText('258.500')
  await expect(page.getByLabel('Lệch giá đã duyệt dòng 1')).toHaveCount(0)

  // 4. Sửa giá 48.000: lệch giá → cảnh báo, gửi duyệt khi chưa có lý do bị chặn; ghi lý do thì gửi được, có chặng "Ngoại lệ giá".
  await page.getByLabel('Đơn giá dòng 1').fill('48000')
  await page.getByLabel('Đơn giá dòng 1').press('Tab')
  await expect(page.getByLabel('Lệch giá đã duyệt dòng 1')).toBeVisible()
  await expect(page.getByRole('alert').filter({ hasText: 'lệch giá đã duyệt' })).toBeVisible()
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu PO-BHA-\d{4}-\d{4}\./)
  // Chờ chuyển từ /moi sang trang phiếu đã lưu – bấm Gửi duyệt sớm hơn thì thông báo lỗi mất khi trang dựng lại (máy bận khi chạy song song).
  await expect(page).not.toHaveURL(/\/moi$/)
  await page.getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  await page.getByRole('dialog').getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  await expect(page.getByRole('alert').filter({ hasText: 'lý do lệch giá' })).toBeVisible()
  await page.getByLabel('Lý do lệch giá dòng 1').fill('NCC báo tăng giá nguyên liệu')
  await page.screenshot({ path: 'e2e/screenshots/po-price-exception.png' })
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu/)
  // Gửi duyệt = lưu rồi gửi (hai lượt gọi API) – khi chạy song song có thể quá 5 giây, chờ trạng thái thay vì thông báo thoáng qua.
  await page.getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  await page.getByRole('dialog').getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  await expect(page.locator('.document-form-title')).toContainText('Chờ duyệt', { timeout: 20_000 })
  await page.getByRole('tab', { name: 'Lịch sử duyệt' }).click()
  await expect(page.getByRole('tabpanel')).toContainText('Ngoại lệ giá')
})
