import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, openByCommand, seedPostedReceipt, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Báo cáo tuần giá mua & mua ngoài hợp đồng, biến động giá (lát F1, design.md mục 18.17): PO đã duyệt ngày thứ Hai tuần trước của NCC
 * chưa có bảng giá → dòng "Không có giá đã duyệt" trên báo cáo mặc định tuần trước; màn hình biến động giá mở được với kỳ mặc định.
 */
test('giá mua & mua ngoài hợp đồng: PO tuần trước không có giá đã duyệt', async ({ page, request }) => {
  // NCC của phiếu nhập mẫu chưa có bảng giá, hợp đồng → dòng PO không có giá đã duyệt.
  const { itemCode, itemId, supplierId } = await seedPostedReceipt(request)
  const bha = await api(request, 'BHA')
  const roles = await bha.get<{ id: string; code: string }[]>('/api/roles')
  const hop = (await bha.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'HOP')!.id
  const warehouses = await bha.get<{ id: string; code: string }[]>('/api/warehouses')

  // Thứ Hai tuần trước theo giờ Việt Nam (kỳ mặc định của báo cáo tuần).
  const today = new Date(new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }))
  const monday = new Date(today)
  monday.setUTCDate(today.getUTCDate() - ((today.getUTCDay() + 6) % 7) - 7)
  const po = await bha.send<{ id: string; no: string }>('post', '/api/purchase-orders', {
    date: monday.toISOString().slice(0, 10), supplierId, warehouseId: warehouses.find((w) => w.code === 'KT')!.id,
    expectedDate: null, paymentTerms: null, deliveryTerms: null, deliveryAddress: null, receiveTolerancePct: 0, note: null,
    lines: [{ id: null, itemId, uomId: hop, qty: 4, unitPrice: 75000, discountPct: 0, vatCode: '10', deliveryDate: null, note: null, sourcePrLineId: null }],
  })
  await bha.send('post', `/api/purchase-orders/${po.id}/submit`, {})
  // Người duyệt: tài khoản quản trị mới tại BHA (người lập không tự duyệt).
  const approverName = unique('dy').toLowerCase()
  await bha.send('post', '/api/users', {
    userName: approverName, fullName: 'Người duyệt PO', email: null, password: 'Demo@2026',
    access: { roleIds: [roles.find((r) => r.code === 'ADMIN')!.id], warehouseIds: [], itemCategoryIds: [] },
  })
  const login = await request.post('/api/auth/login', { data: { userName: approverName, password: 'Demo@2026' } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { companies: { id: string; code: string }[] } }
  // Hai lượt duyệt: cấp PO thường và chặng "Ngoài danh sách/giá" (POL) – dòng không có giá đã duyệt bắt buộc duyệt (design.md mục 18.25).
  for (let i = 0; i < 2; i++) {
    const approved = await request.post(`/api/purchase-orders/${po.id}/approve`, {
      headers: { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.companies.find((c) => c.code === 'BHA')!.id },
      data: { comment: null },
    })
    expect(approved.ok(), await approved.text()).toBeTruthy()
  }

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'PRCW')
  await expect(page.getByRole('heading', { name: 'Giá mua & mua ngoài hợp đồng' })).toBeVisible()
  await expect(page.getByText(/Mua theo giá đã duyệt: .*mục tiêu ≥ 95%/)).toBeVisible()
  await page.locator('[data-list-search]').fill(itemCode)
  const row = page.locator('.data-grid-row', { hasText: po.no })
  await expect(row).toContainText('Không có giá đã duyệt')
  await expect(row).toContainText('300.000')
  await page.screenshot({ path: 'e2e/screenshots/contract-compliance.png' })
  await row.dblclick()
  await expect(page).toHaveURL(new RegExp(`/mua-hang/po/${po.id}$`))

  await openByCommand(page, 'PRVAR')
  await expect(page.getByRole('heading', { name: 'Biến động giá mua' })).toBeVisible()
  await expect(page.getByText(/So với kỳ trước từ \d{2}\/\d{2}\/\d{4}/)).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/price-variance.png' })
})
