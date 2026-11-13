import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { chooseOption, openByCommand, pickOption, seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * Phiếu điều chỉnh / kiểm kê kho – ADJ (design.md mục 6.1, 18.23) ở công ty BHA: kho KT có 10 hộp (phiếu nhập chuẩn bị qua API).
 * Dòng giảm (thiếu khi kiểm kê) không nhập giá, cảnh báo khi vượt tồn; dòng tăng (thừa) nhập giá tay. Ghi sổ, rồi mở lại từ Tra cứu chứng từ.
 */
test('điều chỉnh kho theo biên bản kiểm kê: dòng giảm, dòng tăng, ghi sổ, tra cứu', async ({ page, request }) => {
  const { itemCode } = await seedPostedReceipt(request)
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')

  await openByCommand(page, 'ADJ')
  await expect(page.getByRole('heading', { name: 'Phiếu điều chỉnh, kiểm kê kho (ADJ)' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await chooseOption(page, 'Kho', 'KT – ')
  await page.getByLabel('Số biên bản kiểm kê').fill('BBKK-E2E-01')
  await page.getByLabel('Lý do điều chỉnh').fill('Kiểm kê cuối tháng – chênh lệch thực tế')

  // Dòng 1 (mặc định Giảm): gõ 12 > tồn 10 thì cảnh báo; sửa còn 3. Ô đơn giá khóa – xuất theo giá vốn.
  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(itemCode.toLowerCase())
  await pickOption(page, itemCode)
  await expect(page.getByLabel('Đơn giá dòng 1')).toHaveText('Giá vốn')
  await page.getByLabel('Số lượng dòng 1').fill('12')
  await page.getByLabel('Số lượng dòng 1').press('Tab')
  const first = page.locator('.line-grid-row').first()
  await expect(first.locator('.line-grid-warning')).toHaveText('10')
  await page.getByLabel('Số lượng dòng 1').fill('3')
  await page.getByLabel('Số lượng dòng 1').press('Tab')
  await expect(first.locator('.line-grid-warning')).toHaveCount(0)

  // Dòng 2: Tăng 2 hộp × 55.000 = 110.000.
  await page.keyboard.press('F2')
  await page.getByLabel('Tăng/Giảm dòng 2').click()
  await pickOption(page, 'Tăng')
  await page.getByLabel('Mặt hàng dòng 2').fill(itemCode.toLowerCase())
  await pickOption(page, itemCode)
  await page.getByLabel('Số lượng dòng 2').fill('2')
  await page.getByLabel('Đơn giá dòng 2').fill('55000')
  await page.getByLabel('Đơn giá dòng 2').press('Tab')
  await expect(page.locator('.document-form-footer')).toContainText('110.000')

  await page.getByRole('button', { name: 'Ghi sổ' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Ghi sổ' }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã ghi sổ phiếu ADJ-BHA-\d{4}-\d{4}\./)
  await expect(page.locator('.document-form-title')).toContainText('Đã ghi sổ')
  await page.screenshot({ path: 'e2e/screenshots/stock-adjustment.png' })
  const adjustmentNo = (await page.locator('.document-form-number').textContent())!
  await page.getByRole('button', { name: /(^|\s)Đóng$/ }).click()
  const listRow = page.locator('.data-grid-row', { hasText: adjustmentNo })
  await expect(listRow).toContainText('BBKK-E2E-01')
  await expect(listRow).toContainText('Đã ghi sổ')
  await page.screenshot({ path: 'e2e/screenshots/stock-adjustment-list.png' })

  // Tra cứu chứng từ mở được trang phiếu điều chỉnh (trước đây ADJ chưa có trang).
  await openByCommand(page, 'DOCS')
  await page.getByRole('textbox', { name: 'Tìm kiếm' }).fill(adjustmentNo)
  const docRow = page.locator('.data-grid-row', { hasText: adjustmentNo })
  await expect(docRow).toContainText('Hoàn thành')
  await docRow.dblclick()
  await expect(page).toHaveURL(/\/kho\/dieu-chinh\/[0-9a-f-]{36}$/)
  await expect(page.locator('.document-form-number')).toHaveText(adjustmentNo)
})

/**
 * Kiểm kê (ADJ-3): mở bảng kiểm kê của kho KT, gõ SL thực đếm 8 cho mặt hàng tồn sổ 10 → phiếu tự lập dòng Giảm 2; lưu rồi in biên bản.
 */
test('bảng kiểm kê: nhập thực đếm, tự lập dòng chênh lệch, in biên bản', async ({ page, request }) => {
  const { itemCode } = await seedPostedReceipt(request)
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')

  await openByCommand(page, 'ADJ')
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await expect(page.getByRole('button', { name: 'Bảng kiểm kê' })).toBeDisabled()
  await chooseOption(page, 'Kho', 'KT – ')
  await page.getByLabel('Số biên bản kiểm kê').fill('BBKK-E2E-02')
  await page.getByLabel('Lý do điều chỉnh').fill('Kiểm kê định kỳ')
  await page.getByRole('button', { name: 'Bảng kiểm kê' }).click()

  const dialog = page.getByRole('dialog', { name: 'Bảng kiểm kê – nhập SL thực đếm' })
  const row = dialog.locator('.ant-table-row', { hasText: itemCode })
  await expect(row).toContainText('10')
  await dialog.getByLabel(`Thực đếm ${itemCode}`).fill('8')
  await expect(row).toContainText('-2')
  await page.screenshot({ path: 'e2e/screenshots/stock-count-sheet.png' })
  await dialog.getByRole('button', { name: 'Lập dòng chênh lệch (1)' }).click()
  await expect(dialog).toBeHidden()

  await expect(page.getByLabel('Tăng/Giảm dòng 1')).toBeDisabled()
  await expect(page.locator('.line-grid-row').first()).toContainText('Giảm')
  await expect(page.getByLabel('Số lượng dòng 1')).toHaveValue('2')
  await expect(page.getByLabel('Thực đếm dòng 1')).toHaveValue('8')

  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu ADJ-BHA-\d{4}-\d{4}\./)
  await expect(page.getByRole('button', { name: 'In phiếu' })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/stock-count-adjustment.png' })
})
