import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { switchCompany } from './inventoryHelpers.ts'
import { seedApprovedPo } from './receiptHelpers.ts'

/**
 * Lát Z4a (design.md mục 20.2a) + yêu cầu người dùng 11/11/2026: người lập rút PO đang chờ duyệt về Nháp, rồi tự hủy – hộp thoại Hủy bắt
 * buộc lý do.
 */
test('rút PO chờ duyệt về nháp rồi hủy có lý do', async ({ page, request }) => {
  test.setTimeout(60_000)
  // PO đã gửi duyệt, chưa ai duyệt (người lập là quản trị dùng để chạy e2e).
  const { po } = await seedApprovedPo(request, 3, 0)

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/po/${po.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(po.no)

  await page.getByRole('button', { name: 'Rút về nháp' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Rút về nháp' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã rút phiếu ${po.no} về nháp.`)
  await expect(page.locator('.document-form-title')).toContainText('Nháp')

  // Hủy: thiếu lý do thì báo lỗi, không hủy.
  await page.getByRole('button', { name: 'Hủy phiếu' }).click()
  const dialog = page.getByRole('dialog')
  await dialog.getByRole('button', { name: 'Hủy phiếu' }).click()
  await expect(page.locator('.ant-message')).toContainText('Hãy nhập lý do hủy.')
  await dialog.getByRole('textbox').fill('Đổi nhà cung cấp')
  await page.screenshot({ path: 'e2e/screenshots/cancel-reason.png' })
  await dialog.getByRole('button', { name: 'Hủy phiếu' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã hủy phiếu ${po.no}.`)
  await expect(page.locator('.document-form-title')).toContainText('Đã hủy')
})
