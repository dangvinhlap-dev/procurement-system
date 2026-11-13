import { expect, test } from '@playwright/test'
import { loginViaApi, activeTab } from './helpers.ts'
import { switchCompany } from './inventoryHelpers.ts'
import { seedApprovedPo } from './receiptHelpers.ts'

/**
 * Lát Z3 (design.md mục 20.1 R5): mọi số chứng từ nằm trong ô chữ – bấm để sao chép số, bấm đúp mở form chứng từ gốc; áp ở form, danh
 * sách và báo cáo.
 */
test('ô số chứng từ: bấm sao chép, bấm đúp mở phiếu gốc', async ({ page, request, context }) => {
  test.setTimeout(60_000)
  await context.grantPermissions(['clipboard-read', 'clipboard-write'])
  const { po } = await seedApprovedPo(request, 5)

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')

  // Danh sách PO: số phiếu là ô chữ – bấm sao chép.
  await page.goto('/mua-hang/po')
  await page.getByLabel('Tìm kiếm', { exact: true }).fill(po.no)
  const cell = page.locator('.data-grid .erp-doc-no', { hasText: po.no })
  await expect(cell).toBeVisible()
  await cell.click()
  await expect(page.locator('.ant-message')).toContainText(`Đã sao chép số ${po.no}.`)
  expect(await page.evaluate('navigator.clipboard.readText()')).toBe(po.no)
  await page.screenshot({ path: 'e2e/screenshots/doc-no-list.png' })

  // Bấm đúp ô mở phiếu gốc – ở thẻ màn hình mới, danh sách vẫn giữ ở thẻ cũ (docs/ui-style.md mục 8a).
  await cell.dblclick()
  await expect(page).toHaveURL(new RegExp(`/mua-hang/po/${po.id}$`))
  await expect(page.getByTestId('screen-tabs').locator('.app-screen-tab')).toHaveCount(2)

  // Số phiếu trên đầu form cũng là ô chữ sao chép được.
  const head = activeTab(page).locator('.document-form-number')
  await expect(head).toHaveText(po.no)
  await expect(head).toHaveClass(/erp-doc-no/)
  await page.evaluate("navigator.clipboard.writeText('')")
  await head.click()
  expect(await page.evaluate('navigator.clipboard.readText()')).toBe(po.no)

  // Tra cứu chứng từ (báo cáo dùng DataGrid): bấm đúp ô số mở phiếu.
  await page.goto('/hang-ngay/tra-cuu-chung-tu')
  await page.getByRole('textbox', { name: 'Tìm kiếm' }).fill(po.no)
  await activeTab(page).locator('.data-grid .erp-doc-no', { hasText: po.no }).dblclick()
  await expect(page).toHaveURL(new RegExp(`/mua-hang/po/${po.id}$`))
})
