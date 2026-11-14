import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { switchCompany } from './inventoryHelpers.ts'
import { receiveByApi, seedApprovedPo } from './receiptHelpers.ts'

/**
 * Trang chủ (design.md mục 11.4): ô số liệu theo công ty đang làm việc, bấm ô mở màn hình chi tiết; số đếm cây điều hướng là số thật.
 */
test('trang chủ: ô số liệu, mua hàng tháng này, bấm ô mở màn hình', async ({ page, request }) => {
  // PO đã duyệt ở BHA nhận 5/10 bằng XNH: có mua hàng tháng này, PO còn chờ giao (phân hệ kho gỡ ở lát K3).
  const { bha, po } = await seedApprovedPo(request)
  await receiveByApi(bha, po.id, po.lines[0].id, 5)
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await expect(page.getByRole('heading', { name: /Trang chủ – BHA/ })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Phiếu chờ tôi duyệt' })).toBeVisible()
  // Không còn ô số liệu kho (lát K3).
  await expect(page.getByRole('button', { name: 'Hàng dưới tồn tối thiểu' })).toHaveCount(0)
  // Phiếu XNH vừa xác nhận ở BHA (5 × 50.000) nằm trong mua hàng tháng này.
  const spend = page.locator('.home-spend')
  await expect(spend).toContainText('Tháng này')
  await expect(spend.locator('.ant-table-row').first()).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/dashboard.png' })

  await page.getByRole('button', { name: 'PO đang chờ giao' }).click()
  await expect(page).toHaveURL(/\/mua-hang\/theo-doi-giao-hang$/)
})
