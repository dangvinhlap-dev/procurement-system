import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { switchCompany } from './inventoryHelpers.ts'
import { receiveByApi, seedApprovedPo } from './receiptHelpers.ts'

/**
 * Sửa lỗi test 0.22.0 (11/11/2026): PO đã nhận hàng nhưng người dùng không thấy SL thực nhận vì cột nằm cuối lưới 17 cột, bị đẩy khỏi
 * khung nhìn. Cột "SL thực nhận" / "Còn lại" phải đứng ngay sau "SL đặt" và nằm trong khung lưới ngay khi mở phiếu (màn hình 1366px),
 * lưới cuộn ngang thay vì ép cột hẹp.
 */
test('PO đã nhận hàng: cột SL thực nhận, Còn lại thấy ngay sau SL đặt', async ({ page, request }) => {
  test.setTimeout(60_000)
  const { bha, po } = await seedApprovedPo(request, 10)
  await receiveByApi(bha, po.id, po.lines[0].id, 6)

  await page.setViewportSize({ width: 1366, height: 768 })
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/po/${po.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(po.no)

  const grid = page.locator('.line-grid').first()
  const heads = (await grid.locator('thead th').allInnerTexts()).map((t) => t.trim())
  const qtyAt = heads.indexOf('SL đặt')
  expect(heads.slice(qtyAt, qtyAt + 3)).toEqual(['SL đặt', 'SL thực nhận', 'Còn lại'])

  // Cột nằm trong khung lưới (không phải cuộn mới thấy) và hiện đúng số.
  const gridBox = (await grid.boundingBox())!
  const receivedHead = grid.locator('thead th').nth(qtyAt + 1)
  const headBox = (await receivedHead.boundingBox())!
  expect(headBox.x + headBox.width).toBeLessThanOrEqual(gridBox.x + gridBox.width)
  const row = grid.locator('tbody tr').first()
  await expect(row.locator('td').nth(qtyAt + 1)).toHaveText('6')
  await expect(row.locator('td').nth(qtyAt + 2)).toHaveText('4')

  // Lưới rộng hơn khung thì cuộn ngang, cột giữ độ rộng mặc định (Đơn giá ≥ 100px, không bị ép).
  const priceBox = (await grid.locator('thead th').filter({ hasText: /^Đơn giá$/ }).boundingBox())!
  expect(priceBox.width).toBeGreaterThanOrEqual(100)
  await page.screenshot({ path: 'e2e/screenshots/po-received-column.png' })
})
