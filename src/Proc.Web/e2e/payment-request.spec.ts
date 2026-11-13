import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { switchCompany } from './inventoryHelpers.ts'
import { createBhaAdmin, receiveByApi, seedApprovedPo, todayVn } from './receiptHelpers.ts'

/**
 * Lát Y8 (design.md 19.3 bước 7): đề nghị thanh toán 5B – từ PO bấm "Lập đề nghị thanh toán" → phiếu điền sẵn đơn và các phiếu nhận hàng có hóa
 * đơn (nhiều dòng hóa đơn) → lưu, gửi duyệt → người duyệt khác duyệt (API) → kế toán (quản trị) ghi Đã chi.
 */
test('đề nghị thanh toán từ PO: nhiều dòng hóa đơn, duyệt, ghi Đã chi', async ({ page, request }) => {
  test.setTimeout(120_000)
  const { bha, po } = await seedApprovedPo(request, 10)
  // Hai lần nhận hàng, mỗi lần một hóa đơn: 4 hộp × 50.000 + VAT 10% = 220.000; 6 hộp = 330.000.
  const first = await receiveByApi(bha, po.id, po.lines[0].id, 4)
  const second = await receiveByApi(bha, po.id, po.lines[0].id, 6)
  await bha.send('put', `/api/receipt-confirmations/${first.id}/invoice`, { invoiceNo: '0000101', invoiceSeries: '1C26TAA', invoiceDate: todayVn(), invoiceAmount: 220000 })
  await bha.send('put', `/api/receipt-confirmations/${second.id}/invoice`, { invoiceNo: '0000102', invoiceSeries: '1C26TAA', invoiceDate: todayVn(), invoiceAmount: 330000 })

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/po/${po.id}`)
  await page.getByRole('button', { name: 'Lập đề nghị thanh toán' }).click()
  await expect(page).toHaveURL(/de-nghi-thanh-toan\/moi\?po=/)
  // NCC thử chưa khai tài khoản ngân hàng → tự chọn tiền mặt; hai dòng hóa đơn điền sẵn.
  await expect(page.locator('.line-grid-row')).toHaveCount(2)
  await expect(page.locator('.document-form-content')).toContainText('0000101')
  await expect(page.locator('.document-form-content')).toContainText('0000102')
  await expect(page.locator('.document-form-content')).toContainText('550.000')
  await page.getByLabel('Số tiền giảm trừ').fill('50000')
  await page.getByLabel('Lý do giảm trừ').fill('Trừ tạm ứng')
  await expect(page.locator('.document-form-content')).toContainText('500.000')
  await page.screenshot({ path: 'e2e/screenshots/payment-request-new.png', fullPage: true })

  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu DNT-BHA-\d{4}-\d{4}\./)
  await expect(page.locator('.document-form-number')).toHaveText(/^DNT-BHA-\d{4}-\d{4}$/)
  const no = (await page.locator('.document-form-number').textContent())!
  const id = /([0-9a-f-]{36})/.exec(page.url())![1]
  await page.getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  await page.getByRole('dialog').getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã gửi duyệt phiếu ${no}.`)

  // Người duyệt khác (quản trị BHA mới) duyệt qua API.
  const approver = await createBhaAdmin(bha)
  const login = await request.post('/api/auth/login', { data: { userName: approver.userName, password: approver.password } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { companies: { id: string; code: string }[] } }
  const approved = await request.post(`/api/payment-requests/${id}/approve`, {
    headers: { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.companies.find((c) => c.code === 'BHA')!.id },
    data: { comment: 'Đồng ý' },
  })
  expect(approved.ok(), await approved.text()).toBeTruthy()

  // Kế toán ghi Đã chi.
  await page.reload()
  await page.getByRole('button', { name: 'Ghi Đã chi' }).click()
  const dialog = page.getByRole('dialog', { name: `Ghi Đã chi – ${no}` })
  await dialog.getByLabel('Ghi chú (số ủy nhiệm chi, phiếu chi…)').fill('PC 0456')
  await dialog.getByRole('button', { name: 'Ghi Đã chi' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã ghi Đã chi đề nghị ${no}.`)
  await expect(page.locator('.document-form-title')).toContainText('Đã chi')
  await expect(page.locator('.document-form-content')).toContainText('PC 0456')
  await page.screenshot({ path: 'e2e/screenshots/payment-request-paid.png', fullPage: true })

  // Danh sách DNT.
  await page.goto('/mua-hang/de-nghi-thanh-toan')
  await expect(page.getByRole('heading', { name: 'Đề nghị thanh toán (DNT)' })).toBeVisible()
  await expect(page.locator('.data-grid-row', { hasText: no })).toContainText('Đã chi')
})
