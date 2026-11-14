import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { switchCompany } from './inventoryHelpers.ts'
import { receiveByApi, seedApprovedPo } from './receiptHelpers.ts'

/**
 * Phím F1 – lịch sử thay đổi của ô và của cả phiếu (design.md mục 9.3a): trên phiếu xác nhận nhận hàng XNH đã xác nhận (ô khóa – phiếu kho
 * cũ gỡ ở lát K3), rê chuột lên ô rồi bấm F1 mở "Trợ giúp ô" với tab LỊCH SỬ THAY ĐỔI (ai tạo/sửa, giá trị cũ → mới) và THÔNG TIN Ô; nút
 * "Lịch sử thay đổi" liệt kê các lần sửa của phiếu.
 */
test('F1 xem lịch sử ô đầu phiếu và lịch sử cả phiếu', async ({ page, request }) => {
  const { bha, po } = await seedApprovedPo(request)
  const xnh = await receiveByApi(bha, po.id, po.lines[0].id, 10)
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/xac-nhan-nhan-hang/${xnh.id}`)
  await expect(page.getByText(xnh.no).first()).toBeVisible()

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
  await expect(help).toContainText('InvoiceNo')
  await expect(help).toContainText('character varying(50)')
  await page.screenshot({ path: 'e2e/screenshots/field-help-info.png' })
  await page.keyboard.press('Escape')
  await expect(help).toBeHidden()

  // "Lịch sử thay đổi" cả phiếu: có lần cập nhật hóa đơn ở đầu phiếu.
  await page.getByRole('button', { name: 'Lịch sử thay đổi' }).click()
  const all = page.getByRole('dialog', { name: /Lịch sử thay đổi/ })
  await expect(all.locator('.ant-table-row', { hasText: '0000888' })).toContainText('Số hóa đơn')
  await page.screenshot({ path: 'e2e/screenshots/record-history.png' })
})
