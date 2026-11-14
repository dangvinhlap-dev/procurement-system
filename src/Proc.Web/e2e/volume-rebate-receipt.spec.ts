import { expect, test, type APIRequestContext } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, approveSupplier, unique } from './inventoryHelpers.ts'

type Api = Awaited<ReturnType<typeof api>>

/**
 * Duyệt một chứng từ ở TCT bằng các tài khoản quản trị mới (người lập không tự duyệt; mỗi cấp một người – lát R3) cho đến khi hết chờ duyệt.
 * @param request Ngữ cảnh API của Playwright.
 * @param tct API quản trị tại TCT.
 * @param url Đường dẫn chứng từ, ví dụ /api/volume-rebates/{id}.
 */
async function approveUntilDone(request: APIRequestContext, tct: Api, url: string) {
  const roles = await tct.get<{ id: string; code: string }[]>('/api/roles')
  for (let i = 0; i < 4 && (await tct.get<{ status: string }>(url)).status === 'Submitted'; i++) {
    const userName = unique('dk').toLowerCase()
    await tct.send('post', '/api/users', {
      userName, fullName: `Người duyệt ${userName}`, email: null, password: 'Demo@2026',
      access: { roleIds: [roles.find((r) => r.code === 'ADMIN')!.id], warehouseIds: [], itemCategoryIds: [] },
    })
    const login = await request.post('/api/auth/login', { data: { userName, password: 'Demo@2026' } })
    const { accessToken, user } = (await login.json()) as { accessToken: string; user: { companies: { id: string; code: string }[] } }
    const approved = await request.post(`${url}/approve`, {
      headers: { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.companies.find((c) => c.code === 'TCT')!.id },
      data: { comment: null },
    })
    expect(approved.ok(), await approved.text()).toBeTruthy()
  }
}

/**
 * Thực nhận chiết khấu sản lượng gộp (CKB, tab THỰC NHẬN – design.md mục 18.25, lát G6b): thỏa thuận theo tháng từ tháng trước, PO đã duyệt
 * tháng trước 20 cái × 50.000 đạt bậc 1% → phải nhận 10.000. Chỉ kỳ đã kết thúc có nút Ghi nhận; ghi nhận kèm Ghi chú và file đính kèm,
 * kỳ chuyển "Đủ", phần chia hiện theo công ty. Chia nhiều công ty, KSNB, hủy kiểm tra ở test tích hợp VolumeRebateReceiptTests.
 */
test('ghi thực nhận chiết khấu sản lượng gộp cho kỳ đã kết thúc, kèm đính kèm', async ({ page, request }) => {
  test.setTimeout(120_000)
  const tct = await api(request, 'TCT')
  const groups = await tct.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const uoms = await tct.get<{ id: string; code: string }[]>('/api/uoms')
  const companies = await tct.get<{ id: string; code: string }[]>('/api/companies')
  const warehouses = await tct.get<{ id: string; code: string }[]>('/api/warehouses')
  const cai = uoms.find((u) => u.code === 'CAI')!.id
  const tag = unique('tn')
  const supplier = await tct.send<{ id: string; code: string }>('post', '/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: `NCC thực nhận gộp ${tag}`, shortName: null,
    isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Approved', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })
  await approveSupplier(request, supplier.id)
  await tct.send('put', `/api/suppliers/${supplier.id}/company`, { localCode: null, startDate: null, isActive: true })
  const category = await tct.send<{ id: string }>('post', '/api/item-categories', {
    code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Nhóm thực nhận gộp ${tag}`, parentId: null, inventoryAccount: null,
  })
  const item = await tct.send<{ id: string }>('post', '/api/items', {
    code: null, name: `Gạch thực nhận ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: cai,
    specification: null, brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, trackLot: false, trackExpiry: false,
    minStock: null, maxStock: null, conversions: [],
  })

  // Thỏa thuận theo tháng: từ đầu tháng trước đến cuối tháng này, áp cho TCT; bậc 10 cái 1%.
  const now = new Date()
  const iso = (d: Date) => d.toISOString().slice(0, 10)
  const lastMonth = new Date(Date.UTC(now.getFullYear(), now.getMonth() - 1, 1))
  const endThisMonth = new Date(Date.UTC(now.getFullYear(), now.getMonth() + 1, 0))
  const agreement = await tct.send<{ id: string; no: string }>('post', '/api/volume-rebates', {
    title: `Chiết khấu gạch ${tag}`, supplierId: supplier.id, itemCategoryId: category.id, validFrom: iso(lastMonth), validTo: iso(endThisMonth),
    period: 'Month', basis: 'Quantity', targetTierNo: null, companyIds: [companies.find((c) => c.code === 'TCT')!.id], note: null,
    lines: [{ id: null, itemId: item.id, uomId: cai, note: null }], tiers: [{ fromValue: 10, percent: 1 }],
  })
  await tct.send('post', `/api/volume-rebates/${agreement.id}/submit`, {})
  await approveUntilDone(request, tct, `/api/volume-rebates/${agreement.id}`)

  // PO tháng trước 20 cái × 50.000 = 1.000.000 → bậc 1 (1%) → phải nhận 10.000.
  const po = await tct.send<{ id: string }>('post', '/api/purchase-orders', {
    date: iso(new Date(Date.UTC(now.getFullYear(), now.getMonth() - 1, 3))), supplierId: supplier.id,
    warehouseId: warehouses.find((w) => w.code === 'KT')!.id, expectedDate: null, paymentTerms: null, deliveryTerms: null, deliveryAddress: null,
    receiveTolerancePct: 0, note: null,
    lines: [{ id: null, itemId: item.id, uomId: cai, qty: 20, unitPrice: 50000, discountPct: 0, vatCode: '10', deliveryDate: null, note: null, sourcePrLineId: null }],
  })
  await tct.send('post', `/api/purchase-orders/${po.id}/submit`, {})
  await approveUntilDone(request, tct, `/api/purchase-orders/${po.id}`)

  await loginViaApi(page)
  await page.goto(`/gia-ncc/chiet-khau-san-luong/${agreement.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(agreement.no)
  await page.getByRole('tab', { name: 'Thực nhận' }).click()
  const periods = page.getByRole('table', { name: 'Thực nhận theo kỳ' })
  await expect(periods.locator('tbody tr.ant-table-row')).toHaveCount(2)
  const first = periods.locator('tbody tr.ant-table-row').first()
  await expect(first).toContainText('10.000')
  // Kỳ tháng này chưa kết thúc: không có nút Ghi nhận (chỉ ghi sau khi kỳ kết thúc).
  await expect(periods.getByRole('button', { name: /^Ghi nhận kỳ/ })).toHaveCount(1)

  await first.getByRole('button', { name: /^Ghi nhận kỳ/ }).click()
  const dialog = page.getByRole('dialog', { name: /Ghi nhận thực nhận/ })
  await expect(dialog.getByLabel('Số tiền thực nhận (VND)')).toHaveValue('10.000')
  await dialog.getByLabel('Số chứng từ đối chiếu').fill(`BB-${tag}`)
  await dialog.getByLabel('Ghi chú', { exact: true }).fill('Giảm trừ công nợ tháng trước')
  await dialog.locator('input[type=file]').setInputFiles({ name: 'bien-ban.txt', mimeType: 'text/plain', buffer: Buffer.from('bien ban doi chieu') })
  await expect(dialog).toContainText('bien-ban.txt')
  await page.screenshot({ path: 'e2e/screenshots/volume-rebate-receipt-dialog.png' })
  await dialog.getByRole('button', { name: 'Ghi nhận' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã ghi nhận thực nhận.')

  await expect(first).toContainText('Đủ')
  const receipts = page.getByRole('table', { name: 'Các lần ghi nhận thực nhận chiết khấu gộp' })
  await expect(receipts).toContainText(`BB-${tag}`)
  await expect(receipts).toContainText('TCT: 10.000')
  await expect(receipts).toContainText('Giảm trừ công nợ tháng trước')
  await receipts.getByRole('button', { name: /^Đính kèm lần nhận/ }).click()
  await expect(page.getByRole('dialog', { name: /Đính kèm – lần nhận/ })).toContainText('bien-ban.txt')
  await page.screenshot({ path: 'e2e/screenshots/volume-rebate-receipt.png' })
})
