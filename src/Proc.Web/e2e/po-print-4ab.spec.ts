import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { switchCompany } from './inventoryHelpers.ts'
import { seedApprovedPo } from './receiptHelpers.ts'

/**
 * Lát Y7 (design.md mục 19.3, Q10): PO hiện "Loại mua – biểu mẫu"; "In phiếu" in bảng theo dõi mua hàng 4A/4B (biểu mẫu ký trước khi đặt
 * hàng), nút "In đơn gửi NCC" in đơn đặt hàng theo khuôn chung.
 */
test('PO: in bảng theo dõi 4B và đơn gửi NCC', async ({ page, request }) => {
  test.setTimeout(60_000)
  const { po } = await seedApprovedPo(request, 4)

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/po/${po.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(po.no)
  await expect(page.getByTestId('po-form-code')).toHaveValue('B – Hàng hóa, dịch vụ · 4B')

  const track = page.waitForResponse((r) => r.url().endsWith(`/api/purchase-orders/${po.id}/pdf`))
  const popup = page.waitForEvent('popup')
  await page.getByRole('button', { name: 'In phiếu' }).click()
  const trackResponse = await track
  expect(trackResponse.status()).toBe(200)
  expect(trackResponse.headers()['content-type']).toContain('application/pdf')
  await (await popup).close()

  const supplier = page.waitForResponse((r) => r.url().endsWith(`/api/purchase-orders/${po.id}/pdf?form=supplier`))
  const popup2 = page.waitForEvent('popup')
  await page.getByRole('button', { name: 'In đơn gửi NCC' }).click()
  expect((await supplier).status()).toBe(200)
  await (await popup2).close()
  await page.screenshot({ path: 'e2e/screenshots/po-print-4ab.png' })
})
