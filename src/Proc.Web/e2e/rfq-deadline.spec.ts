import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, seedPostedReceipt, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Lát R7 (design.md mục 18.26) – RFQ: gia hạn nhận báo giá phải có lý do (hiện trên phiếu); mời 3 NCC mà chỉ 1 chào giá thì chọn thầu hỏi
 * giải trình ít báo giá trước khi xem trước chuỗi duyệt, giải trình hiện trên phiếu.
 */
test('RFQ: gia hạn có lý do, chọn thầu khi ít báo giá phải giải trình', async ({ page, request }) => {
  test.setTimeout(60_000)
  const { itemId, supplierId } = await seedPostedReceipt(request)
  const head = await api(request)
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const others: string[] = []
  for (let i = 0; i < 2; i++) {
    const s = await head.send<{ id: string }>('post', '/api/suppliers', {
      code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: `NCC mời thầu ${unique('M')}`, shortName: null,
      isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
      rating: null, status: 'Potential', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
    })
    others.push(s.id)
  }
  const bha = await api(request, 'BHA')
  const hop = (await bha.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'HOP')!.id
  const today = new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const in3 = new Date(Date.now() + 3 * 86_400_000).toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const rfq = await bha.send<{ id: string; no: string; lines: { id: string }[] }>('post', '/api/rfqs', {
    date: today, title: `RFQ hạn nộp ${unique('R')}`, deadline: in3, sourcingId: null,
    weights: { price: 60, quality: 20, delivery: 10, payment: 5, warranty: 5 }, note: null,
    lines: [{ id: null, itemId, uomId: hop, qty: 20, sourcePrLineId: null, note: null }], supplierIds: [supplierId, ...others],
  })
  await bha.send('post', `/api/rfqs/${rfq.id}/send`, { reason: null })
  await bha.send('put', `/api/rfqs/${rfq.id}/quotations/${supplierId}`, {
    quoteDate: today, validUntil: null, paymentTerms: null, qualityScore: 8, deliveryScore: 8, paymentScore: 8, warrantyScore: 8, note: null,
    lines: [{ rfqLineId: rfq.lines[0].id, unitPrice: 50000, discountPct: 0, vatCode: '10', leadTimeDays: 5, note: null }],
  })

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/dau-thau/rfq/${rfq.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(rfq.no)

  // Gia hạn: thiếu lý do báo lỗi; đủ thì hiện dòng báo kèm lý do.
  await page.getByRole('button', { name: /Gia hạn nhận báo giá$/ }).click()
  const dialog = page.getByRole('dialog')
  await dialog.getByRole('button', { name: /Gia hạn$/ }).click()
  await expect(page.locator('.ant-message')).toContainText('Hãy chọn hạn mới và nhập lý do gia hạn.')
  await dialog.getByLabel('Lý do gia hạn').fill('NCC xin thêm thời gian chuẩn bị')
  await dialog.getByRole('button', { name: /Gia hạn$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã gia hạn nhận báo giá RFQ ${rfq.no}`)
  await expect(page.getByRole('alert').filter({ hasText: 'NCC xin thêm thời gian chuẩn bị' })).toBeVisible()

  // Chọn thầu khi chỉ 1/3 NCC chào giá: hỏi giải trình trước.
  await page.getByRole('button', { name: /Chọn thầu, gửi duyệt$/ }).click()
  const reasonDialog = page.locator('.ant-modal-confirm', { hasText: 'Chỉ có 1 NCC chào giá (tối thiểu 3)' })
  await expect(reasonDialog).toBeVisible()
  await reasonDialog.getByLabel('Giải trình ít báo giá').fill('Hai NCC không phản hồi trong hạn')
  await reasonDialog.getByRole('button', { name: /Tiếp tục$/ }).click()
  await page.locator('.ant-modal-confirm').getByRole('button', { name: /Chọn thầu, gửi duyệt$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(`RFQ ${rfq.no}`)
  await expect(page.getByRole('alert').filter({ hasText: 'Hai NCC không phản hồi trong hạn' })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/rfq-deadline.png' })
})
