import { expect, test, type APIRequestContext } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, approveSupplier, chooseOption, openByCommand, unique } from './inventoryHelpers.ts'

/**
 * Duyệt phiếu GTH bằng một quản trị mới ở tổng công ty (người lập không tự duyệt).
 * @param request Ngữ cảnh API của Playwright.
 * @param head API quản trị ở tổng công ty.
 * @param gthId Id phiếu GTH đang chờ duyệt.
 */
async function approveGthAsNewAdmin(request: APIRequestContext, head: Awaited<ReturnType<typeof api>>, gthId: string) {
  const roles = await head.get<{ id: string; code: string }[]>('/api/roles')
  const userName = unique('dg').toLowerCase()
  await head.send('post', '/api/users', {
    userName, fullName: `Người duyệt ${userName}`, email: null, password: 'Demo@2026',
    access: { roleIds: [roles.find((r) => r.code === 'ADMIN')!.id], warehouseIds: [], itemCategoryIds: [] },
  })
  const login = await request.post('/api/auth/login', { data: { userName, password: 'Demo@2026' } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { companies: { id: string; code: string }[] } }
  const approved = await request.post(`/api/monthly-supplier-approvals/${gthId}/approve`, {
    headers: { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.companies.find((c) => c.code === 'TCT')!.id },
    data: { comment: null },
  })
  expect(approved.ok(), await approved.text()).toBeTruthy()
}

/**
 * Đặt hàng gộp (DHG – design.md mục 18.25, lát G7): phiếu GTH tháng này cho TCT và BHA, kế hoạch nhu cầu TCT 60 / BHA 40; quản trị ở tổng công
 * ty lập phiếu đặt gộp, lấy mặt hàng của phiếu tháng, nhập tổng 100 → tự chia 60/40; sửa TCT 75 thì báo vượt biên độ; sửa lại trong biên độ,
 * lưu, Tạo PO → mỗi công ty một PO nháp. Quy tắc lỗi một công ty thì không PO nào kiểm tra ở test tích hợp PooledOrderTests.
 */
test('lập phiếu đặt hàng gộp, phân bổ theo kế hoạch, tạo PO cho hai công ty, chụp màn hình', async ({ page, request }) => {
  test.setTimeout(120_000)
  const head = await api(request)
  const bha = await api(request, 'BHA')
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const cai = uoms.find((u) => u.code === 'CAI')!.id
  const tag = unique('dg')
  const supplier = await head.send<{ id: string; code: string }>('post', '/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: `NCC đặt gộp ${tag}`, shortName: null,
    isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Approved', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })
  await approveSupplier(request, supplier.id)
  for (const c of [head, bha]) await c.send('put', `/api/suppliers/${supplier.id}/company`, { localCode: null, startDate: null, isActive: true })
  const category = await head.send<{ id: string; code: string }>('post', '/api/item-categories', {
    code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Nhóm đặt gộp ${tag}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: `Nước rửa chén ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id,
    baseUomId: cai, specification: null, brand: null, origin: null, barcode: null, vatCode: '10',
    technicalSpecs: null, trackLot: false, trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })

  // Kế hoạch nhu cầu tháng này: TCT 60, BHA 40 (đã chốt).
  const now = new Date()
  const plan = async (c: typeof head, qty: number) => {
    const months = Array.from({ length: 12 }, (_, m) => (m === now.getMonth() ? qty : 0))
    const p = await c.send<{ id: string }>('post', '/api/demand-plans', {
      year: now.getFullYear(), itemCategoryId: category.id, note: null, lines: [{ id: null, itemId: item.id, uomId: cai, months, note: null }],
    })
    await c.send('post', `/api/demand-plans/${p.id}/confirm`, null)
  }
  await plan(head, 60)
  await plan(bha, 40)

  // Phiếu GTH tháng này cho TCT, BHA – giá 20.000; người khác duyệt.
  const companies = await head.get<{ id: string; code: string }[]>('/api/companies')
  const month = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`
  const gth = await head.send<{ id: string; no: string }>('post', '/api/monthly-supplier-approvals', {
    period: month, itemCategoryId: category.id, companyIds: companies.filter((c) => ['TCT', 'BHA'].includes(c.code)).map((c) => c.id), note: null,
    lines: [{ id: null, itemId: item.id, uomId: cai, mainSupplierId: supplier.id, unitPrice: 20000, backupSupplierId: null, backupUnitPrice: null, quantityLimit: null, note: null }],
    // Mặt hàng mới một NCC – ngoại lệ (lát H2): giải trình, qua thêm chặng Chủ tịch/TGĐ do người thứ hai duyệt.
    exceptionNote: 'Mặt hàng mới, một NCC',
  })
  await head.send('post', `/api/monthly-supplier-approvals/${gth.id}/submit`, null)
  await approveGthAsNewAdmin(request, head, gth.id)
  await approveGthAsNewAdmin(request, head, gth.id)

  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'DHG')
  await expect(page.getByRole('heading', { name: 'Đặt hàng gộp' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await chooseOption(page, 'Phiếu duyệt tháng', gth.no)
  await chooseOption(page, 'Nhà cung cấp', supplier.code)
  await page.getByLabel('Ghi chú', { exact: true }).fill('Giao một lần đầu tháng')
  await page.getByRole('button', { name: /Lấy mặt hàng từ phiếu tháng$/ }).click()
  await expect(page.getByLabel('Mặt hàng dòng 1')).toBeVisible()

  // Tổng 100 → tự chia theo kế hoạch 60/40.
  await page.getByLabel('Tổng SL dòng 1').fill('100')
  await expect(page.getByLabel('SL TCT dòng 1')).toHaveValue('60')
  await expect(page.getByLabel('SL BHA dòng 1')).toHaveValue('40')
  // BHA 10 → tổng 70, đề xuất BHA 28 ± 7 → vượt biên độ; BHA 35 → tổng 95, đề xuất 38 ± 9,5 → trong biên độ.
  await page.getByLabel('SL BHA dòng 1').fill('10')
  await page.getByLabel('SL BHA dòng 1').press('Tab')
  await expect(page.getByLabel('Tổng SL dòng 1')).toHaveValue('70')
  await expect(page.getByLabel('Vượt biên độ BHA dòng 1')).toBeVisible()
  await page.getByLabel('SL BHA dòng 1').fill('35')
  await page.getByLabel('SL BHA dòng 1').press('Tab')
  await expect(page.getByLabel('Vượt biên độ BHA dòng 1')).toHaveCount(0)

  await page.getByRole('tab', { name: /Công ty nhận/i }).click()
  await expect(page.getByLabel('Kho nhận dòng 1')).toBeVisible()
  await page.getByRole('tab', { name: /Dòng hàng/i }).click()

  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu DHG-TCT-\d{4}-\d{4}\./)
  await expect(page.locator('.document-form-number')).toHaveText(/^DHG-TCT-\d{4}-\d{4}$/)
  await expect(page.getByLabel('SL BHA dòng 1')).toHaveValue('35')

  await page.getByRole('button', { name: /(^| )Tạo PO$/ }).click()
  await page.locator('.ant-modal-confirm').getByRole('button', { name: /Tạo PO$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã tạo 2 PO từ phiếu DHG/)
  await expect(page.locator('.document-form-title')).toContainText('Đã tạo PO')
  await expect(page.getByRole('alert').filter({ hasText: /TCT – PO-TCT-\d{4}-\d{4}/ })).toBeVisible()
  await expect(page.getByRole('alert').filter({ hasText: /BHA – PO-BHA-\d{4}-\d{4}/ })).toBeVisible()
  await expect(page.getByRole('button', { name: /(^| )Lưu nháp$/ })).toHaveCount(0)
  await page.waitForFunction("document.getAnimations().every((a) => a.playState !== 'running')")
  await page.screenshot({ path: 'e2e/screenshots/pooled-order.png' })

  await page.getByRole('tab', { name: /Công ty nhận/i }).click()
  await expect(page.getByRole('tabpanel')).toContainText(/PO-BHA-\d{4}-\d{4}/)
  await expect(page.getByRole('tabpanel')).toContainText('BHA –')
  await page.screenshot({ path: 'e2e/screenshots/pooled-order-companies.png' })
  await page.getByRole('button', { name: /(^| )Đóng$/ }).click()
  await expect(page.locator('.data-grid-row', { hasText: supplier.code })).toContainText('Đã tạo PO')
})
