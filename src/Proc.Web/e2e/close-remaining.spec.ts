import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { switchCompany } from './inventoryHelpers.ts'
import { receiveByApi, seedApprovedPo } from './receiptHelpers.ts'

/**
 * Lát R6 (design.md mục 18.26) – đóng phần còn lại của PO đã nhận một phần: nút "Đóng phần còn lại" hỏi lý do (bắt buộc), xong thì phiếu
 * hiện "Đã đóng phần còn lại" kèm người đóng và lý do, không còn nút đóng.
 */
test('đóng phần còn lại của PO nhận thiếu: bắt buộc lý do, hiện lý do trên phiếu', async ({ page, request }) => {
  test.setTimeout(60_000)
  // PO 10 hộp đã duyệt, nhận 4 bằng phiếu XNH (design.md mục 19.4).
  const { bha, po } = await seedApprovedPo(request, 10)
  await receiveByApi(bha, po.id, po.lines[0].id, 4)

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/po/${po.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(po.no)
  await page.getByRole('button', { name: /Đóng phần còn lại$/ }).click()
  const dialog = page.getByRole('dialog')
  await dialog.getByRole('button', { name: /Đóng phần còn lại$/ }).click()
  await expect(page.locator('.ant-message')).toContainText('Hãy nhập lý do đóng.')
  await dialog.getByLabel('Lý do đóng').fill('NCC hết hàng, không giao tiếp')
  await dialog.getByRole('button', { name: /Đóng phần còn lại$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã đóng phần còn lại của phiếu ${po.no}.`)
  await expect(page.getByRole('alert').filter({ hasText: 'NCC hết hàng, không giao tiếp' })).toBeVisible()
  await expect(page.locator('.document-form-title')).toContainText('Đã đóng phần còn lại')
  await expect(page.getByRole('button', { name: /Đóng phần còn lại$/ })).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/close-remaining.png' })
})
