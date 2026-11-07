import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, openByCommand, unique } from './inventoryHelpers.ts'

/**
 * Tổng hợp nhu cầu (KHNTH – design.md mục 18.25, lát G5b): kế hoạch đã chốt của TCT (theo thùng) và BHA gộp toàn tập đoàn, một cột mỗi
 * công ty + tổng theo ĐVT cơ sở; đổi kỳ sang Quý I; mở ngăn kéo theo tháng. Quy tắc nháp/đã thay/quyền kiểm tra ở test tích hợp.
 */
test('tổng hợp nhu cầu toàn tập đoàn theo công ty, đổi kỳ, xem theo tháng, chụp màn hình', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const bha = await api(request, 'BHA')
  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const cai = uoms.find((u) => u.code === 'CAI')!.id
  const thung = uoms.find((u) => u.code === 'THUNG')!.id
  const tag = unique('th')
  const category = await head.send<{ id: string; code: string }>('post', '/api/item-categories', {
    code: null, name: `Nhóm tổng hợp ${tag}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: `Bóng đèn ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: cai,
    specification: null, brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, trackLot: false, trackExpiry: false,
    minStock: null, maxStock: null, conversions: [{ uomId: thung, factor: 12 }],
  })
  const year = new Date().getFullYear() + 1
  const months = (...first: number[]) => [...first, ...Array(12 - first.length).fill(0)]
  const plan = async (client: typeof head, uomId: string, qty: number[]) => {
    const created = await client.send<{ id: string }>('post', '/api/demand-plans', {
      year, itemCategoryId: category.id, note: null, lines: [{ id: null, itemId: item.id, uomId, months: months(...qty), note: null }],
    })
    await client.send('post', `/api/demand-plans/${created.id}/confirm`, null)
  }
  // TCT: 2 thùng tháng 1 = 24 cái, 1 thùng tháng 5 = 12 cái; BHA: 100 cái tháng 2.
  await plan(head, thung, [2, 0, 0, 0, 1])
  await plan(bha, cai, [0, 100])

  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'KHNTH')
  await expect(page.getByRole('heading', { name: 'Tổng hợp nhu cầu' })).toBeVisible()
  await expect(page.getByLabel('Toàn tập đoàn')).toBeChecked()
  const filter = page.getByRole('combobox', { name: 'Nhóm hàng (gồm nhóm con)' })
  await filter.click()
  await filter.fill(category.code)
  await page.locator('.ant-select-dropdown:visible .ant-select-tree-title', { hasText: category.code }).click()

  const row = page.locator('.data-grid-row', { hasText: item.code })
  await expect(row).toContainText('36')
  await expect(row).toContainText('136')
  await expect(page.getByRole('columnheader', { name: 'BHA', exact: true })).toBeVisible()

  // Quý I: TCT 24, BHA 100, tổng 124 (tháng 5 không tính).
  const period = page.getByRole('combobox', { name: 'Kỳ', exact: true })
  await period.click()
  await period.fill('Quý I')
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option', { hasText: /^Quý I$/ }).click()
  await expect(row).toContainText('124')
  await page.screenshot({ path: 'e2e/screenshots/demand-summary.png' })

  await row.dblclick()
  const drawer = page.locator('.detail-drawer')
  await expect(drawer).toContainText(`Nhu cầu: ${item.code}`)
  await expect(drawer).toContainText(/KHN-TCT-/)
  await expect(drawer.locator('.data-grid-row', { hasText: 'Tháng 5' })).toContainText('12')
  await expect(drawer.locator('.data-grid-row', { hasText: 'Cả năm' })).toContainText('136')
  await page.screenshot({ path: 'e2e/screenshots/demand-summary-drawer.png' })
})
