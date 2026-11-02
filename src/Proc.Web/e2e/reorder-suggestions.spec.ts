import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, openByCommand, seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * Gợi ý đặt hàng (lát D1, design.md mục 18.16): mặt hàng trọng yếu có tồn BHA 10 < tồn an toàn 20, điểm đặt lại 50, tối đa 200 →
 * khẩn, gợi ý 190; lập đề nghị mua điền sẵn dòng; ngăn kéo mặt hàng có các ô đặt hàng lại.
 */
test('gợi ý đặt hàng: hàng trọng yếu dưới tồn an toàn, lập đề nghị mua điền sẵn', async ({ page, request }) => {
  const { itemCode, itemId } = await seedPostedReceipt(request)
  const head = await api(request)
  const detail = await head.get<Record<string, unknown>>(`/api/items/${itemId}`)
  await head.send('put', `/api/items/${itemId}`, { ...detail, maxStock: 200, safetyStock: 20, reorderPoint: 50, isCritical: true })

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'REORD')
  await expect(page.getByRole('heading', { name: 'Gợi ý đặt hàng' })).toBeVisible()
  await page.getByRole('checkbox', { name: 'Chỉ trọng yếu' }).check()
  await page.locator('[data-list-search]').fill(itemCode)
  const row = page.locator('.data-grid-row', { hasText: itemCode })
  await expect(row).toContainText('Khẩn – dưới tồn an toàn')
  await expect(row).toContainText('190')
  await page.screenshot({ path: 'e2e/screenshots/reorder-suggestions.png' })

  await page.getByRole('button', { name: 'Lập đề nghị mua' }).click()
  await expect(page).toHaveURL(/\/mua-hang\/pr\/moi$/)
  const line = page.locator('.line-grid-row', { hasText: itemCode })
  await expect(line).toBeVisible()
  await expect(line.getByRole('spinbutton').first()).toHaveValue('190')

  // Ngăn kéo mặt hàng (tab Quản lý kho) hiện các ô đặt hàng lại.
  await switchCompany(page, 'TCT')
  await openByCommand(page, 'ITEM')
  await page.locator('[data-list-search]').fill(itemCode)
  await page.locator('.data-grid-row', { hasText: itemCode }).dblclick()
  await page.getByRole('tab', { name: 'Quản lý kho' }).click()
  await expect(page.getByLabel('Điểm đặt hàng lại (HOP)')).toHaveValue('50,0000')
  await expect(page.getByRole('checkbox', { name: /Vật tư trọng yếu/ })).toBeChecked()
})
