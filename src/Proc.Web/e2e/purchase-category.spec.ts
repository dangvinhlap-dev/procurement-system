import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, chooseOption, switchCompany, unique, createLeafCategory } from './inventoryHelpers.ts'

/**
 * Loại mua trên đề nghị mua (lát Y3a, design.md mục 19.3): PR của BHA lập loại B qua API; trên form đổi sang "A – Công trình", chọn công
 * trình thì bộ phận tự điền phòng ban đề xuất của công trình; lưu xong đầu phiếu hiện khung dự toán – lũy kế – còn lại (biểu mẫu 1A). Cách tính
 * lũy kế theo PO đã duyệt kiểm tra ở test tích hợp ConstructionPurchaseTests.
 */
test('đề nghị mua loại A chọn công trình, hiện dự toán – lũy kế – còn lại', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const bha = await api(request, 'BHA')
  const cai = (await bha.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'CAI')!.id
  const tag = unique('LM')
  const category = await createLeafCategory<{ id: string }>(head, {
    code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Nhóm vật tư công trình ${tag}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: `Xi măng ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: cai, specification: null,
    brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, trackLot: false, trackExpiry: false, minStock: null,
    maxStock: null, conversions: [],
  })
  const other = await bha.send<{ id: string }>('post', '/api/departments', { code: unique('PK').slice(0, 10), name: 'Phòng khác', managerName: null })
  const deptCode = unique('XD').slice(0, 10)
  const dept = await bha.send<{ id: string }>('post', '/api/departments', { code: deptCode, name: `Ban xây dựng ${deptCode}`, managerName: null })
  const today = new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const projectCode = unique('CT')
  await bha.send('post', '/api/construction-projects', {
    code: projectCode, name: `Nhà kho ${projectCode}`, departmentId: dept.id, location: null, startDate: today, expectedEndDate: null,
    estimateAmount: 10_000_000, basis: 'QĐ thử', note: null,
  })
  const pr = await bha.send<{ id: string; no: string }>('post', '/api/purchase-requests', {
    date: today, departmentId: other.id, purpose: 'Thi công móng', neededDate: null, warehouseId: null, priority: 'Normal', note: null,
    lines: [{ id: null, itemId: item.id, uomId: cai, qty: 5, suggestedSupplierId: null, neededDate: null, note: null }],
  })

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/pr/${pr.id}`)
  const categoryItem = page.locator('.ant-form-item', { has: page.getByRole('combobox', { name: 'Loại mua', exact: true }) })
  await expect(categoryItem).toContainText('B – Hàng hóa')
  await expect(page.getByRole('combobox', { name: 'Công trình', exact: true })).toHaveCount(0)

  await chooseOption(page, 'Loại mua', 'Công trình')
  await chooseOption(page, 'Công trình', projectCode)
  // Bộ phận tự điền phòng ban đề xuất của công trình.
  await expect(page.locator('.ant-form-item', { has: page.getByRole('combobox', { name: 'Bộ phận đề nghị', exact: true }) })).toContainText(deptCode)
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText('Đã lưu')
  await expect(page.locator('.ant-alert', { hasText: `Công trình ${projectCode}` })).toContainText('dự toán 10.000.000')
  await expect(page.locator('.ant-alert', { hasText: `Công trình ${projectCode}` })).toContainText('còn lại 10.000.000')
  await page.waitForLoadState('networkidle')
  await page.screenshot({ path: 'e2e/screenshots/purchase-category-a.png' })

  // Danh sách PR có cột loại mua.
  await page.goto('/mua-hang/pr')
  await expect(page.locator('.data-grid-row', { hasText: pr.no })).toContainText('A – Công trình')
})
