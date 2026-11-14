import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, switchCompany, unique, createLeafCategory } from './inventoryHelpers.ts'

/**
 * Thủ kho nhập tồn trên đề nghị mua (lát Y3b, design.md mục 19.3): PR loại B của BHA (nơi nhận KT) lập và gửi duyệt qua API; trên form bấm
 * "Nhập tồn", nhập tồn 2 cho dòng cần sử dụng 5 → hộp thoại hiện Cần mua 3; xác nhận xong lưới có cột Tồn 2, Cần mua 3 và đầu phiếu báo thủ kho
 * đã xác nhận tồn. Phạm vi kho, khóa sau cấp duyệt, gợi ý tồn gần nhất kiểm tra ở test tích hợp PurchaseRequestStockTests.
 */
test('thủ kho nhập tồn trên đề nghị mua chờ duyệt, tính cần mua', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const bha = await api(request, 'BHA')
  const cai = (await bha.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'CAI')!.id
  const kt = (await bha.get<{ id: string; code: string }[]>('/api/warehouses')).find((w) => w.code === 'KT')!.id
  const tag = unique('NT')
  const category = await createLeafCategory<{ id: string }>(head, {
    code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Nhóm vật tư nhập tồn ${tag}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: `Găng tay ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: cai, specification: null,
    brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, trackLot: false, trackExpiry: false, minStock: null,
    maxStock: null, conversions: [],
  })
  const deptCode = unique('NT').slice(0, 10)
  const dept = await bha.send<{ id: string }>('post', '/api/departments', { code: deptCode, name: `Phòng nhập tồn ${deptCode}`, managerName: null })
  const today = new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const pr = await bha.send<{ id: string; no: string }>('post', '/api/purchase-requests', {
    date: today, departmentId: dept.id, purpose: 'Bảo hộ lao động', neededDate: null, warehouseId: kt, priority: 'Normal', note: null,
    lines: [{ id: null, itemId: item.id, uomId: cai, qty: 5, suggestedSupplierId: null, neededDate: null, note: null }],
  })
  await bha.send('post', `/api/purchase-requests/${pr.id}/submit`, {})

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/pr/${pr.id}`)
  await expect(page.locator('.ant-alert', { hasText: 'Chưa có tồn của thủ kho nơi nhận' })).toBeVisible()
  const row = page.locator('.line-grid-row').first()
  await expect(row).toContainText('Chưa nhập')

  await page.getByRole('button', { name: 'Nhập tồn' }).click()
  const dialog = page.getByRole('dialog', { name: /Nhập tồn kho/ })
  await expect(dialog).toContainText('Cần mua')
  await dialog.getByLabel('Tồn dòng 1').fill('2')
  await expect(dialog.locator('.ant-table-tbody tr.ant-table-row').first().locator('td').last()).toHaveText('3')
  await page.waitForLoadState('networkidle')
  await page.screenshot({ path: 'e2e/screenshots/pr-stock-entry-dialog.png' })
  await dialog.getByRole('button', { name: 'Xác nhận tồn' }).click()
  await expect(dialog).toBeHidden()

  await expect(page.locator('.ant-alert', { hasText: 'đã xác nhận tồn' })).toBeVisible()
  await expect(row.locator('td').filter({ hasText: /^2$/ })).toHaveCount(1)
  await expect(row.locator('td').filter({ hasText: /^3$/ })).toHaveCount(1)
  await page.screenshot({ path: 'e2e/screenshots/pr-stock-entry.png' })
})
