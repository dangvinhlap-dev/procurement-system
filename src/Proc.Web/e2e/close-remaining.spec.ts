import { expect, test, type APIRequestContext } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, seedPostedReceipt, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Lát R6 (design.md mục 18.26) – đóng phần còn lại của PO đã nhận một phần: nút "Đóng phần còn lại" hỏi lý do (bắt buộc), xong thì phiếu
 * hiện "Đã đóng phần còn lại" kèm người đóng và lý do, không còn nút đóng.
 */
test('đóng phần còn lại của PO nhận thiếu: bắt buộc lý do, hiện lý do trên phiếu', async ({ page, request }) => {
  test.setTimeout(60_000)
  const { itemId, supplierId } = await seedPostedReceipt(request)
  const bha = await api(request, 'BHA')
  const uoms = await bha.get<{ id: string; code: string }[]>('/api/uoms')
  const hop = uoms.find((u) => u.code === 'HOP')!.id
  const kt = (await bha.get<{ id: string; code: string }[]>('/api/warehouses')).find((w) => w.code === 'KT')!.id
  const today = new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const po = await bha.send<{ id: string; no: string; lines: { id: string }[] }>('post', '/api/purchase-orders', {
    date: today, supplierId, warehouseId: kt, expectedDate: null, paymentTerms: null, deliveryTerms: null, deliveryAddress: null,
    receiveTolerancePct: 0, note: null,
    lines: [{ id: null, itemId, uomId: hop, qty: 10, unitPrice: 50000, discountPct: 0, vatCode: '10', deliveryDate: null, note: null, sourcePrLineId: null }],
  })
  await bha.send('post', `/api/purchase-orders/${po.id}/submit`, {})
  // Cấp PO thường và chặng "Ngoài danh sách/giá" (POL) – mỗi chặng một người duyệt (lát R3).
  for (let i = 0; i < 2; i++) await approveAsNewAdmin(request, bha, po.id)

  const grn = await bha.send<{ id: string }>('post', '/api/goods-receipts', {
    date: today, warehouseId: kt, supplierId, invoiceNo: null, invoiceSeries: null, invoiceDate: null, deliveryNoteNo: null, deliverer: null,
    note: null, purchaseOrderId: po.id,
    lines: [{ id: null, itemId, uomId: hop, qty: 4, unitPrice: 0, vatCode: null, lotNo: null, expiryDate: null, note: null, purchaseOrderLineId: po.lines[0].id }],
  })
  await bha.send('post', `/api/inventory-documents/${grn.id}/post`, {})

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/po/${po.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(po.no)
  await page.getByRole('button', { name: /Đóng phần còn lại$/ }).click()
  const dialog = page.getByRole('dialog')
  await dialog.getByRole('button', { name: /Đóng phần còn lại$/ }).click()
  await expect(page.locator('.ant-message')).toContainText('Hãy nhập lý do đóng.')
  await dialog.getByLabel('Lý do đóng').fill('NCC hết hàng, không giao tiếp')
  await dialog.getByRole('button', { name: /Đóng phần còn lại$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã đóng phần còn lại của phiếu ${po.no}.`)
  await expect(page.getByRole('alert').filter({ hasText: 'NCC hết hàng, không giao tiếp' })).toBeVisible()
  await expect(page.locator('.document-form-title')).toContainText('Đã đóng phần còn lại')
  await expect(page.getByRole('button', { name: /Đóng phần còn lại$/ })).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/close-remaining.png' })
})

/**
 * Tạo một quản trị mới tại BHA rồi duyệt PO bằng tài khoản đó (người lập không tự duyệt; mỗi cấp một người).
 * @param request Ngữ cảnh API của Playwright.
 * @param bha API quản trị tại BHA.
 * @param poId Id PO đang chờ duyệt.
 */
async function approveAsNewAdmin(request: APIRequestContext, bha: Awaited<ReturnType<typeof api>>, poId: string) {
  const roles = await bha.get<{ id: string; code: string }[]>('/api/roles')
  const userName = unique('dy').toLowerCase()
  await bha.send('post', '/api/users', {
    userName, fullName: `Người duyệt ${userName}`, email: null, password: 'Demo@2026',
    access: { roleIds: [roles.find((r) => r.code === 'ADMIN')!.id], warehouseIds: [], itemCategoryIds: [] },
  })
  const login = await request.post('/api/auth/login', { data: { userName, password: 'Demo@2026' } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { companies: { id: string; code: string }[] } }
  const approved = await request.post(`/api/purchase-orders/${poId}/approve`, {
    headers: { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.companies.find((c) => c.code === 'BHA')!.id },
    data: { comment: null },
  })
  expect(approved.ok(), await approved.text()).toBeTruthy()
}
