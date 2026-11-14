import { expect, test, type APIRequestContext } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, approveSupplier, unique, createLeafCategory } from './inventoryHelpers.ts'

/**
 * Duyệt phiếu GTH bằng một quản trị mới ở tổng công ty (người lập không tự duyệt; mỗi cấp một người – lát R3).
 * @param request Ngữ cảnh API của Playwright.
 * @param head API quản trị ở tổng công ty.
 * @param gthId Id phiếu GTH đang chờ duyệt.
 */
async function approveGthAsNewAdmin(request: APIRequestContext, head: Awaited<ReturnType<typeof api>>, gthId: string) {
  const roles = await head.get<{ id: string; code: string }[]>('/api/roles')
  const userName = unique('dt').toLowerCase()
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
 * Lát H3 (design.md mục 18.25 – quyết định Q3): PO có dòng giá ròng vượt giá trần của phiếu duyệt NCC và giá tháng vẫn gửi duyệt được nhưng
 * phải qua thêm chặng "Vượt giá trần" (Chủ tịch/TGĐ). Phiếu hiện cảnh báo vàng nêu rõ chặng này; tab Lịch sử duyệt ghi tên chặng. Quy tắc bỏ
 * cấp trùng vai trò (Chủ tịch đã duyệt PO) kiểm tra ở test tích hợp PurchaseOrderMonthlyApprovalTests.
 */
test('PO vượt giá trần: cảnh báo và chặng duyệt "Vượt giá trần", chụp màn hình', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const cai = (await head.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'CAI')!.id
  const tag = unique('vt')
  const supplier = async (name: string) => {
    const s = await head.send<{ id: string }>('post', '/api/suppliers', {
      code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: `${name} ${tag}`, shortName: null,
      isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
      rating: null, status: 'Approved', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
    })
    await approveSupplier(request, s.id)
    await head.send('put', `/api/suppliers/${s.id}/company`, { localCode: null, startDate: null, isActive: true })
    return s.id
  }
  const main = await supplier('NCC chính vượt trần')
  const outsider = await supplier('NCC ngoài danh sách vượt trần')
  const category = await createLeafCategory<{ id: string }>(head, {
    code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Nhóm vượt trần ${tag}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string }>('post', '/api/items', {
    code: null, name: `Van khóa ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id,
    baseUomId: cai, specification: null, brand: null, origin: null, barcode: null, vatCode: '10',
    technicalSpecs: null, trackLot: false, trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })

  // Phiếu GTH tháng này cho TCT: giá NCC chính 10.000, trần 10.500; một NCC – ngoại lệ (lát H2) nên hai người duyệt.
  const companies = await head.get<{ id: string; code: string }[]>('/api/companies')
  const now = new Date()
  const gth = await head.send<{ id: string }>('post', '/api/monthly-supplier-approvals', {
    period: `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`, itemCategoryId: category.id,
    companyIds: companies.filter((c) => c.code === 'TCT').map((c) => c.id), note: null,
    lines: [{
      id: null, itemId: item.id, uomId: cai, mainSupplierId: main, unitPrice: 10000, backupSupplierId: null, backupUnitPrice: null,
      quantityLimit: null, note: null, standardPrice: null, ceilingPrice: 10500, targetPrice: null,
    }],
    exceptionNote: 'Mặt hàng mới, một NCC',
  })
  await head.send('post', `/api/monthly-supplier-approvals/${gth.id}/submit`, null)
  await approveGthAsNewAdmin(request, head, gth.id)
  await approveGthAsNewAdmin(request, head, gth.id)

  // PO NCC ngoài danh sách giá 11.000 > trần 10.500: chặng ngoài danh sách/giá và chặng vượt giá trần.
  const warehouse = (await head.get<{ id: string; code: string }[]>('/api/warehouses'))[0].id
  const today = now.toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const po = await head.send<{ id: string; no: string }>('post', '/api/purchase-orders', {
    date: today, supplierId: outsider, warehouseId: warehouse, expectedDate: null, paymentTerms: null, deliveryTerms: null, deliveryAddress: null,
    receiveTolerancePct: 0, note: null,
    lines: [{ id: null, itemId: item.id, uomId: cai, qty: 10, unitPrice: 11000, discountPct: 0, vatCode: '10', deliveryDate: null, note: null, sourcePrLineId: null }],
  })

  await loginViaApi(page)
  await page.goto(`/mua-hang/po/${po.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(po.no)
  await expect(page.getByRole('alert').filter({ hasText: 'vượt giá trần' })).toContainText('Vượt giá trần" (Chủ tịch/TGĐ)')
  // Popup gửi duyệt ghi tên từng chặng – thấy trước chặng Chủ tịch/TGĐ.
  await page.getByRole('button', { name: /^.*Gửi duyệt$/ }).first().click()
  const dialog = page.getByRole('dialog')
  await expect(dialog).toContainText('Vượt giá trần · Cấp 1')
  await page.screenshot({ path: 'e2e/screenshots/po-over-ceiling-submit.png' })
  await dialog.getByRole('button', { name: /Gửi duyệt$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(po.no)

  await page.getByRole('tab', { name: /Lịch sử duyệt/i }).click()
  const history = page.getByLabel('Cây lịch sử phê duyệt')
  await expect(history.getByText(/Ngoài danh sách\/giá · Cấp 1/)).toBeVisible()
  await expect(history.getByText(/Vượt giá trần · Cấp 1/)).toBeVisible()

  await page.screenshot({ path: 'e2e/screenshots/po-over-ceiling.png' })
})
