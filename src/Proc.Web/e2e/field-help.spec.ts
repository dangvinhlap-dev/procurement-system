import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * Phím F1 – lịch sử thay đổi của ô và của cả phiếu (design.md mục 9.3a): trên phiếu nhập đã ghi sổ (ô khóa), rê chuột lên ô rồi bấm F1
 * mở "Trợ giúp ô" với tab LỊCH SỬ THAY ĐỔI (ai tạo/sửa, giá trị cũ → mới) và THÔNG TIN Ô; F1 ở ô của dòng hàng; nút "Lịch sử thay đổi"
 * liệt kê cả đầu phiếu và dòng.
 */
test('F1 xem lịch sử ô đầu phiếu, ô dòng hàng và lịch sử cả phiếu', async ({ page, request }) => {
  const seed = await seedPostedReceipt(request)
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/kho/grn/${seed.receiptId}`)
  await expect(page.getByText(seed.receiptNo).first()).toBeVisible()

  // Nhập hóa đơn về sau để ô "Số hóa đơn" có một lần sửa.
  await page.getByRole('button', { name: 'Cập nhật hóa đơn' }).click()
  const invoice = page.getByRole('dialog', { name: 'Cập nhật hóa đơn NCC' })
  await invoice.getByLabel('Số hóa đơn').fill('0000888')
  await invoice.getByRole('button', { name: 'Lưu' }).click()
  await expect(invoice).toBeHidden()

  // Ô khóa không nhận con trỏ: rê chuột lên ô rồi bấm F1.
  await page.locator('.document-form-header .ant-form-item', { hasText: 'Số hóa đơn' }).first().locator('input').hover()
  await page.keyboard.press('F1')
  const help = page.getByRole('dialog', { name: 'Trợ giúp ô: Số hóa đơn' })
  await expect(help).toBeVisible()
  const update = help.locator('.ant-table-row').first()
  await expect(update).toContainText('Nhập giá trị')
  await expect(update).toContainText('0000888')
  await expect(update).toContainText('admin')
  await expect(help).toContainText('Tạo:')
  await page.screenshot({ path: 'e2e/screenshots/field-help-history.png' })
  await help.getByRole('tab', { name: /Thông tin ô/i }).click()
  await expect(help).toContainText('InventoryDocument.InvoiceNo')
  await expect(help).toContainText('character varying(50)')
  await page.screenshot({ path: 'e2e/screenshots/field-help-info.png' })
  await page.keyboard.press('Escape')
  await expect(help).toBeHidden()

  // Ô tham chiếu: nhà cung cấp hiện mã – tên, không hiện mã nội bộ.
  await page.locator('.document-form-header .ant-form-item', { hasText: 'Nhà cung cấp' }).first().locator('.ant-select').hover()
  await page.keyboard.press('F1')
  const supplier = page.getByRole('dialog', { name: 'Trợ giúp ô: Nhà cung cấp' })
  await expect(supplier.locator('.ant-table-row').first()).toContainText(seed.supplierCode)
  await expect(supplier.locator('.ant-table-row').first()).toContainText('Tạo')
  await page.keyboard.press('Escape')

  // Ô của dòng hàng: số lượng 10.
  await page.locator('.line-grid tbody tr.line-grid-row').first().getByRole('spinbutton').first().hover()
  await page.keyboard.press('F1')
  const qty = page.getByRole('dialog', { name: 'Trợ giúp ô: Số lượng' })
  await expect(qty.locator('.ant-table-row').first()).toContainText('10')
  await page.keyboard.press('Escape')

  // "Lịch sử thay đổi" cả phiếu: có đầu phiếu và dòng hàng.
  await page.getByRole('button', { name: 'Lịch sử thay đổi' }).click()
  const all = page.getByRole('dialog', { name: /Lịch sử thay đổi/ })
  await expect(all.locator('.ant-table-row', { hasText: '0000888' })).toContainText('Số hóa đơn')
  await expect(all.locator('.ant-table-row', { hasText: 'dòng 1' }).first()).toBeVisible()
  // Người ghi sổ lưu mã tài khoản: hiện tên đăng nhập – họ tên.
  await expect(all.locator('.ant-table-row', { hasText: 'Người ghi sổ' })).toContainText('admin')
  await page.screenshot({ path: 'e2e/screenshots/record-history.png' })
})
