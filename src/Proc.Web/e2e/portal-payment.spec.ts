import { expect, test } from '@playwright/test'
import { portalSession } from './portalHelpers.ts'
import { createBhaAdmin, receiveByApi, seedApprovedPo, todayVn } from './receiptHelpers.ts'

/**
 * Lát P6b (design.md 19.5, P6b-1…P6b-4): NCC xem trạng thái thanh toán trên Portal – trang Thanh toán (tổng hợp theo đơn, còn phải trả), chi tiết
 * đơn (hóa đơn – phiếu nhận hàng, đợt thanh toán đã chi kèm giảm trừ) và khối Thanh toán trên trang đơn.
 */
test('Portal: NCC xem hóa đơn, đợt thanh toán đã chi, còn phải trả', async ({ page, request }) => {
  test.setTimeout(120_000)
  const { bha, po, supplierId } = await seedApprovedPo(request, 10)
  // Hai lần nhận: 4 hộp (220.000 gồm VAT) có hóa đơn, đã đề nghị + chi (giảm trừ 20.000); 6 hộp (330.000) có hóa đơn chưa đề nghị.
  const first = await receiveByApi(bha, po.id, po.lines[0].id, 4)
  const second = await receiveByApi(bha, po.id, po.lines[0].id, 6)
  await bha.send('put', `/api/receipt-confirmations/${first.id}/invoice`, { invoiceNo: '0000201', invoiceSeries: '1C26TAA', invoiceDate: todayVn(), invoiceAmount: 220000 })
  await bha.send('put', `/api/receipt-confirmations/${second.id}/invoice`, { invoiceNo: '0000202', invoiceSeries: '1C26TAA', invoiceDate: todayVn(), invoiceAmount: 330000 })
  const dnt = await bha.send<{ id: string; no: string }>('post', '/api/payment-requests', {
    date: todayVn(), purchaseOrderId: po.id, method: 'Cash', bankAccountId: null, deductionAmount: 20000, deductionReason: 'Phạt giao trễ',
    note: null, lines: [{ receiptConfirmationId: first.id, description: null }],
  })
  await bha.send('post', `/api/payment-requests/${dnt.id}/submit`, {})
  const approver = await createBhaAdmin(bha)
  const login = await request.post('/api/auth/login', { data: { userName: approver.userName, password: approver.password } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { companies: { id: string; code: string }[] } }
  const approved = await request.post(`/api/payment-requests/${dnt.id}/approve`, {
    headers: { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.companies.find((c) => c.code === 'BHA')!.id },
    data: { comment: null },
  })
  expect(approved.ok(), await approved.text()).toBeTruthy()
  await bha.send('post', `/api/payment-requests/${dnt.id}/pay`, { paidDate: todayVn(), note: 'UNC nội bộ' })

  const session = await portalSession(request, supplierId)
  await page.goto('/ncc/dang-nhap')
  await page.evaluate((s) => sessionStorage.setItem('proc.portal.session', JSON.stringify(s)), session)

  // Trang Thanh toán: đơn thanh toán một phần, còn phải trả 330.000 (hóa đơn 550.000 − đã chi 200.000 − giảm trừ 20.000).
  await page.goto('/ncc/thanh-toan')
  await expect(page.getByRole('heading', { name: 'Thanh toán', exact: true })).toBeVisible()
  const row = page.locator('.data-grid-row', { hasText: po.no })
  await expect(row).toContainText('Thanh toán một phần')
  await expect(row).toContainText('550.000')
  await expect(row).toContainText('330.000')
  await expect(page.getByTestId('portal-payments-outstanding')).toContainText('330.000')
  await page.screenshot({ path: 'e2e/screenshots/portal-payments.png', fullPage: true })

  // Chi tiết đơn: hóa đơn đã thanh toán / đã nhận hóa đơn; đợt thanh toán đã chi có giảm trừ; không có ghi chú chi nội bộ.
  await row.dblclick()
  await expect(page).toHaveURL(new RegExp(`/ncc/thanh-toan/${po.id}$`))
  await expect(page.getByRole('heading', { name: `Thanh toán đơn ${po.no}` })).toBeVisible()
  const invoices = page.getByTestId('portal-payment-invoices')
  await expect(invoices.locator('.data-grid-row', { hasText: '0000201' })).toContainText('Đã thanh toán')
  await expect(invoices.locator('.data-grid-row', { hasText: '0000202' })).toContainText('Đã nhận hóa đơn')
  const rounds = page.getByTestId('portal-payment-rounds')
  await expect(rounds).toContainText(dnt.no)
  await expect(rounds).toContainText('Phạt giao trễ')
  await expect(rounds).toContainText('200.000')
  await expect(page.getByTestId('portal-payment-outstanding')).toContainText('330.000')
  await expect(page.locator('body')).not.toContainText('UNC nội bộ')
  await page.screenshot({ path: 'e2e/screenshots/portal-payment-detail.png', fullPage: true })

  // Trang đơn có khối Thanh toán.
  await page.goto(`/ncc/don-hang/${po.id}`)
  await expect(page.getByTestId('portal-payment-summary')).toContainText('330.000')
  await expect(page.getByTestId('portal-payment-rounds')).toContainText(dnt.no)
})
