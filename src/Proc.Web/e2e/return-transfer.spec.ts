import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { chooseOption, openByCommand, pickOption, seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * Phiếu xuất trả NCC (design.md mục 10.5) và phiếu chuyển kho nội bộ (mục 10.6) trên khuôn DocumentForm, ở công ty BHA.
 * Dữ liệu chuẩn bị qua API: mặt hàng, NCC và một phiếu nhập 10 hộp vào kho tổng KT đã ghi sổ.
 */

test('trả NCC theo phiếu nhập gốc, rồi chuyển phần còn lại sang kho phụ', async ({ page, request }) => {
  const { itemCode, receiptNo } = await seedPostedReceipt(request)

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')

  // Phiếu trả NCC: chọn phiếu nhập gốc, lấy mọi dòng còn trả được (10), sửa còn 4.
  await openByCommand(page, 'RTV')
  await expect(page.getByRole('heading', { name: 'Phiếu xuất trả hàng NCC (RTV)' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await expect(page).toHaveURL(/\/kho\/rtv\/moi$/)
  await chooseOption(page, 'Phiếu nhập gốc', receiptNo)
  await expect(page.getByLabel('Kho xuất / NCC')).toHaveValue(/^KT \/ NCC/)
  await page.getByRole('button', { name: 'Lấy mọi dòng còn trả được' }).click()
  const returnRow = page.locator('.line-grid-row').first()
  await expect(returnRow).toContainText(itemCode)
  await expect(returnRow).toContainText('50.000')
  await page.getByLabel('SL trả dòng 1').fill('4')
  await page.getByLabel('SL trả dòng 1').press('Tab')
  await expect(returnRow).toContainText('200.000')
  await expect(page.locator('.document-form-footer')).toContainText('220.000')

  await page.getByRole('button', { name: 'Ghi sổ' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Ghi sổ' }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã ghi sổ phiếu RTV-BHA-\d{4}-\d{4}\./)
  await expect(page).toHaveURL(/\/kho\/rtv\/[0-9a-f-]{36}$/)
  await expect(page.locator('.document-form-title')).toContainText('Đã ghi sổ')
  await page.screenshot({ path: 'e2e/screenshots/goods-return.png' })
  const returnNo = await page.locator('.document-form-number').textContent()
  await page.getByRole('button', { name: 'Đóng' }).click()
  await expect(page.locator('.data-grid-row', { hasText: returnNo! })).toContainText('Đã ghi sổ')

  // Phiếu chuyển kho KT → KP1: tồn kho xuất còn 6; gõ 7 thì cảnh báo vượt tồn, sửa 6 rồi ghi sổ.
  await openByCommand(page, 'TRF')
  await expect(page.getByRole('heading', { name: 'Phiếu chuyển kho nội bộ (TRF)' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await chooseOption(page, 'Kho xuất', 'KT – ')
  await chooseOption(page, 'Kho nhập', 'KP1 – ')
  await page.getByLabel('Người nhận').fill('Tổ trưởng xưởng cán 1')
  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(itemCode.toLowerCase())
  await pickOption(page, itemCode)
  await page.getByLabel('Số lượng dòng 1').fill('7')
  await page.getByLabel('Số lượng dòng 1').press('Tab')
  const transferRow = page.locator('.line-grid-row').first()
  await expect(transferRow.locator('.line-grid-warning')).toHaveText('6')
  await page.getByLabel('Số lượng dòng 1').fill('6')
  await page.getByLabel('Số lượng dòng 1').press('Tab')
  await expect(transferRow.locator('.line-grid-warning')).toHaveCount(0)

  await page.getByRole('button', { name: 'Ghi sổ' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Ghi sổ' }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã ghi sổ phiếu TRF-BHA-\d{4}-\d{4}\./)
  await expect(page.locator('.document-form-title')).toContainText('Đã ghi sổ')
  await page.screenshot({ path: 'e2e/screenshots/stock-transfer.png' })
  const transferNo = await page.locator('.document-form-number').textContent()
  await page.getByRole('button', { name: 'Đóng' }).click()
  const listRow = page.locator('.data-grid-row', { hasText: transferNo! })
  await expect(listRow).toContainText('KP1')
  await page.screenshot({ path: 'e2e/screenshots/stock-transfer-list.png' })
})
