import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand, seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * Đánh giá NCC định kỳ theo quý (lát F4a, design.md mục 18.20): NCC có phiếu nhập mua đã ghi sổ trong quý hiện ra "Chưa đánh giá";
 * bấm đúp lập phiếu, chấm 6 tiêu chí, kết luận, "Hoàn thành" thì phiếu khóa và dòng chuyển "Hoàn thành".
 */
test('đánh giá NCC theo quý: lập phiếu, chấm điểm, hoàn thành', async ({ page, request }) => {
  const { supplierCode } = await seedPostedReceipt(request)
  const today = new Date(new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }))
  const year = today.getFullYear()
  const quarter = Math.floor(today.getMonth() / 3) + 1

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'SUPREV')
  await expect(page.getByRole('heading', { name: 'Đánh giá NCC định kỳ' })).toBeVisible()
  // Mặc định quý trước; NCC vừa nhập hàng thuộc quý hiện tại.
  await page.getByLabel('Năm').fill(String(year))
  await page.getByLabel('Quý').click()
  await page.locator('.ant-select-dropdown:visible').getByText(`Quý ${quarter}`, { exact: true }).click()
  const row = page.locator('.data-grid-row', { hasText: supplierCode })
  await expect(row).toContainText('Chưa đánh giá')
  await expect(page.getByRole('alert').first()).toContainText(/Đã hoàn thành \d+\/\d+ NCC thuộc diện/)

  await row.dblclick()
  const drawer = page.locator('.detail-drawer')
  await expect(drawer).toContainText(`Lập phiếu đánh giá NCC quý ${quarter}/${year}`)
  const criteria = ['Giá, tổng chi phí', 'Chất lượng', 'Giao hàng', 'Bảo hành, hỗ trợ', 'Công nợ, thanh toán', 'Năng lực tài chính, tuân thủ']
  const scores = ['4 – Tốt', '5 – Rất tốt', '4 – Tốt', '3 – Trung bình', '4 – Tốt', '4 – Tốt']
  for (const [i, label] of criteria.entries()) {
    await drawer.getByLabel(label, { exact: true }).click()
    await page.locator('.ant-select-dropdown:visible').getByText(scores[i], { exact: true }).click()
  }
  // (4 + 5 + 4 + 3 + 4 + 4) ÷ 6 = 4,00.
  await expect(drawer.getByLabel('Điểm bình quân (tạm tính)')).toHaveValue('4')
  await drawer.getByLabel('Kết luận', { exact: true }).click()
  await page.locator('.ant-select-dropdown:visible').getByText('Tiếp tục hợp tác', { exact: true }).click()
  await page.screenshot({ path: 'e2e/screenshots/supplier-review.png' })

  await drawer.getByRole('button', { name: 'Hoàn thành' }).click()
  await page.locator('.ant-modal-confirm').getByRole('button', { name: 'Hoàn thành' }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã hoàn thành phiếu đánh giá DGN-BHA-\d{4}-\d{4}/)
  await expect(drawer).toContainText('Hoàn thành')
  await drawer.getByRole('button', { name: 'Đóng' }).click()
  await expect(row).toContainText('Hoàn thành')
  await expect(row).toContainText('Tiếp tục hợp tác')
})
