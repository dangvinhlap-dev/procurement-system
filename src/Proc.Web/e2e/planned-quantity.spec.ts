import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, unique, createLeafCategory } from './inventoryHelpers.ts'

/**
 * Lấy SL kế hoạch (design.md mục 18.25, lát G5c): RFQ nháp ở tổng công ty, dòng tính theo thùng; kế hoạch đã chốt của TCT (30 cái) và BHA
 * (2 thùng) năm sau → hộp thoại toàn tập đoàn ra 4,5 thùng (lẻ, cảnh báo), chi tiết từng công ty; mặc định làm tròn lên 5 thùng, chọn "Đổi dòng
 * sang ĐVT cơ sở" thì áp dụng 54 cái. Hợp đồng nguyên tắc dùng chung hộp thoại; chia theo ngày, quyền kiểm tra ở test tích hợp DemandPlanTests.
 */
test('RFQ lấy SL kế hoạch toàn tập đoàn, quy về ĐVT dòng, chụp màn hình', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const bha = await api(request, 'BHA')
  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const cai = uoms.find((u) => u.code === 'CAI')!.id
  const thung = uoms.find((u) => u.code === 'THUNG')!.id
  const tag = unique('sl')
  const category = await createLeafCategory<{ id: string }>(head, {
    code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Nhóm SL kế hoạch ${tag}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: `Ốc vít ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: cai,
    specification: null, brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, trackLot: false, trackExpiry: false,
    minStock: null, maxStock: null, conversions: [{ uomId: thung, factor: 12 }],
  })
  const year = new Date().getFullYear() + 1
  const plan = async (client: typeof head, uomId: string, months: number[]) => {
    const created = await client.send<{ id: string }>('post', '/api/demand-plans', {
      year, itemCategoryId: category.id, note: null, lines: [{ id: null, itemId: item.id, uomId, months, note: null }],
    })
    await client.send('post', `/api/demand-plans/${created.id}/confirm`, null)
  }
  await plan(head, cai, [30, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0])
  await plan(bha, thung, [0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0])

  const today = new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const in7 = new Date(Date.now() + 7 * 86_400_000).toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const rfq = await head.send<{ id: string; no: string }>('post', '/api/rfqs', {
    date: today, title: `RFQ theo kế hoạch ${tag}`, deadline: in7, sourcingId: null,
    weights: { price: 60, quality: 20, delivery: 10, payment: 5, warranty: 5 }, note: null,
    lines: [{ id: null, itemId: item.id, uomId: thung, qty: 1, sourcePrLineId: null, note: null }], supplierIds: [],
  })

  await loginViaApi(page)
  await page.goto(`/dau-thau/rfq/${rfq.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(rfq.no)
  await page.getByRole('button', { name: /Lấy SL kế hoạch$/ }).click()
  const dialog = page.getByRole('dialog', { name: 'Lấy SL kế hoạch làm SL mời thầu' })
  // Ở tổng công ty mặc định toàn tập đoàn; chọn khoảng cả năm sau (trọn tháng).
  await expect(dialog.getByRole('checkbox', { name: 'Toàn tập đoàn' })).toBeChecked()
  for (const [label, value] of [['Từ ngày', `01/01/${year}`], ['Đến ngày', `31/12/${year}`]]) {
    const box = dialog.getByLabel(label)
    await box.click()
    await box.fill(value)
    await box.press('Enter')
  }
  const row = dialog.locator('.ant-table-row', { hasText: item.code })
  // 30 cái + 2 thùng × 12 = 54 cái = 4,5 thùng: lẻ → mặc định làm tròn lên 5 thùng.
  await expect(dialog).toContainText('Tính 12 tháng')
  await expect(row.getByLabel('SL lẻ dòng 1')).toBeVisible()
  await expect(row).toContainText('4,5')
  await expect(row).toContainText('54 CAI')
  await expect(row).toContainText('5 THUNG')
  await expect(row).toContainText(/BHA: 2 \(KHN-BHA-.+\); TCT: 2,5 \(KHN-TCT-.+\)/)
  await dialog.getByRole('radio', { name: 'Đổi dòng sang ĐVT cơ sở' }).check()
  await expect(row).toContainText('54 CAI')
  await page.screenshot({ path: 'e2e/screenshots/planned-quantity.png' })
  await dialog.getByRole('button', { name: 'Áp dụng cho 1 dòng' }).click()
  await expect(page.getByLabel('Số lượng dòng 1')).toHaveValue('54')
})
