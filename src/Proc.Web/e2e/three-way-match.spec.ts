import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand, seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * Đối chiếu PO – nhận – hóa đơn (design.md mục 18.15): phiếu nhập đã ghi sổ chưa có hóa đơn → báo "Chưa có hóa đơn"; cập nhật hóa đơn
 * sau ghi sổ bằng nút "Cập nhật hóa đơn" → báo cáo thành "Khớp" (vẫn cảnh báo nhập không theo PO).
 */
test('cập nhật hóa đơn sau ghi sổ rồi đối chiếu trên báo cáo', async ({ page, request }) => {
  // Phiếu nhập thẳng 10 hộp × 50.000, VAT 10% → tổng thanh toán 550.000.
  const seed = await seedPostedReceipt(request)
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')

  await openByCommand(page, 'MATCH3')
  await expect(page.getByRole('heading', { name: 'Đối chiếu PO – nhận – hóa đơn' })).toBeVisible()
  const row = page.locator('.data-grid-row', { hasText: seed.receiptNo })
  await expect(row).toContainText('Chưa có hóa đơn')
  await expect(row).toContainText('Nhập không theo PO')

  // Bấm đúp mở phiếu nhập, cập nhật hóa đơn về sau.
  await row.dblclick()
  await expect(page).toHaveURL(new RegExp(`/kho/grn/${seed.receiptId}`))
  await page.getByRole('button', { name: 'Cập nhật hóa đơn' }).click()
  const dialog = page.getByRole('dialog', { name: 'Cập nhật hóa đơn NCC' })
  await dialog.getByLabel('Số hóa đơn').fill('0000777')
  await dialog.getByLabel('Ký hiệu hóa đơn').fill('1C26TAA')
  await dialog.getByLabel('Tiền hóa đơn').fill('550000')
  await page.screenshot({ path: 'e2e/screenshots/grn-update-invoice.png' })
  await dialog.getByRole('button', { name: 'Lưu' }).click()
  await expect(page.getByText('Đã cập nhật hóa đơn.')).toBeVisible()
  await expect(dialog).toBeHidden()

  await openByCommand(page, 'MATCH3')
  const updated = page.locator('.data-grid-row', { hasText: seed.receiptNo })
  await expect(updated).toContainText('Khớp')
  await expect(updated).toContainText('0000777')
  await expect(updated).toContainText('550.000')
  await expect(page.getByRole('alert').filter({ hasText: 'Chưa có hóa đơn' })).toContainText('Khớp')
  await page.getByRole('heading', { name: 'Đối chiếu PO – nhận – hóa đơn' }).click()
  await expect(page.locator('.ant-spin-spinning')).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/three-way-match.png' })
})
