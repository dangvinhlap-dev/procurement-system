import { expect, test, type APIRequestContext } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, switchCompany, unique } from './inventoryHelpers.ts'
import { seedApprovedPo } from './receiptHelpers.ts'

/** Mời một tài khoản Portal cho NCC, kích hoạt, đăng nhập 2 bước qua API; trả dữ liệu phiên Portal. */
async function portalSession(request: APIRequestContext, supplierId: string) {
  const head = await api(request)
  const email = `${unique('dh').toLowerCase()}@ncc-thu.vn`
  const invite = await head.send<{ inviteUrl: string }>('post', `/api/suppliers/${supplierId}/portal-users`, {
    email, fullName: 'Lê Bán Hàng', phone: null, position: 'Nhân viên kinh doanh',
  })
  const token = new URL(invite.inviteUrl).searchParams.get('token')!
  expect((await request.post('/api/portal/auth/activate', { data: { token, password: 'ncc12345' } })).status()).toBe(204)
  const challenge = await (await request.post('/api/portal/auth/login', { data: { email, password: 'ncc12345' } })).json()
  const session = await (await request.post('/api/portal/auth/verify-otp', { data: { challengeId: challenge.challengeId, code: challenge.devCode } })).json()
  return session as { accessToken: string }
}

/**
 * Lát P4a (design.md 19.5, P4-1…P4-5): NCC thấy PO đã duyệt trên Portal, gửi file, từ chối kèm lý do → nội bộ thấy trên ô "Xác nhận của NCC"
 * và tab Xác nhận NCC, mở lại phản hồi (lý do gửi NCC – yêu cầu người dùng 12/11/2026) → NCC thấy lý do, xác nhận kèm ngày giao cam kết.
 */
test('Portal: NCC từ chối đơn, Phòng CUVT mở lại, NCC xác nhận', async ({ page, request }) => {
  test.setTimeout(120_000)
  const { po, supplierId } = await seedApprovedPo(request, 12)
  const session = await portalSession(request, supplierId)

  // NCC mở đơn từ danh sách "Đơn đặt hàng".
  await page.goto('/ncc/dang-nhap')
  await page.evaluate((s) => sessionStorage.setItem('proc.portal.session', JSON.stringify(s)), session)
  await page.goto('/ncc')
  await page.getByRole('link', { name: 'Đơn đặt hàng' }).click()
  const row = page.locator('.data-grid-row', { hasText: po.no })
  await expect(row).toContainText('Chờ xác nhận')
  await row.dblclick()
  await expect(page.getByTestId('portal-order-info')).toContainText(po.no)
  await expect(page.getByTestId('portal-order-lines')).toContainText('Găng tay nhận hàng')
  // 12 × 50.000 = 600.000 + VAT 10% = 660.000.
  await expect(page.getByTestId('portal-order-total')).toContainText('660.000')

  // File gửi kèm trước khi phản hồi.
  await page.getByLabel('Ghi chú file').fill('Thư từ chối')
  await page.locator('.erp-portal-file-bar input[type=file]').setInputFiles({
    name: 'TuChoi.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4' + String.fromCharCode(10) + '%%EOF'),
  })
  await expect(page.locator('.ant-message')).toContainText('Đã tải lên "TuChoi.pdf"')
  await expect(page.getByTestId('portal-order-files')).toContainText('Thư từ chối')

  // Từ chối thiếu lý do → báo lỗi; có lý do → hỏi lại rồi gửi.
  await page.getByRole('button', { name: 'Từ chối đơn' }).click()
  await expect(page.getByText('Nhập lý do từ chối')).toBeVisible()
  await page.getByLabel('Ghi chú / lý do').fill('Hết hàng đến cuối tháng')
  await page.screenshot({ path: 'e2e/screenshots/portal-order-respond.png', fullPage: true })
  await page.getByRole('button', { name: 'Từ chối đơn' }).click()
  await page.locator('.ant-modal-confirm').getByRole('button', { name: 'Từ chối' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã từ chối đơn ${po.no}`)
  await expect(page.getByTestId('portal-order-responses')).toContainText('Hết hàng đến cuối tháng')
  await expect(page.getByRole('button', { name: 'Xác nhận đơn' })).toHaveCount(0)

  // Nội bộ: ô "Xác nhận của NCC" và tab Xác nhận NCC; mở lại phản hồi kèm lý do.
  const internal = await page.context().newPage()
  await loginViaApi(internal)
  await internal.goto('/')
  await switchCompany(internal, 'BHA')
  await internal.goto(`/mua-hang/po/${po.id}`)
  await expect(internal.getByTestId('po-supplier-response')).toHaveValue(/NCC từ chối.*Hết hàng đến cuối tháng/)
  await internal.getByRole('tab', { name: /Xác nhận NCC/i }).click()
  await expect(internal.getByLabel('Phản hồi của NCC')).toContainText('Hết hàng đến cuối tháng')
  await expect(internal.getByText('TuChoi.pdf')).toBeVisible()
  await internal.getByRole('button', { name: 'Mở lại phản hồi' }).click()
  await internal.locator('#action-reason').fill('Bên mua chấp nhận giao chậm 10 ngày')
  await internal.locator('.ant-modal-confirm').getByRole('button', { name: 'Mở lại' }).click()
  await expect(internal.locator('.ant-message')).toContainText('Đã mở lại phản hồi')
  await expect(internal.getByTestId('po-supplier-response-status')).toHaveText('Chờ NCC xác nhận')
  await expect(internal.getByLabel('Email NCC')).toContainText('Đề nghị phản hồi lại')
  await internal.screenshot({ path: 'e2e/screenshots/po-supplier-response.png', fullPage: true })
  await internal.close()

  // NCC thấy lý do mở lại, xác nhận kèm ngày giao cam kết (mặc định ngày giao dự kiến).
  await page.reload()
  await expect(page.getByText('Bên mua chấp nhận giao chậm 10 ngày').first()).toBeVisible()
  await page.getByRole('button', { name: 'Xác nhận đơn' }).click()
  await page.locator('.ant-modal-confirm').getByRole('button', { name: 'Xác nhận' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã xác nhận đơn ${po.no}`)
  await expect(page.locator('.ant-alert')).toContainText('Đã xác nhận')
  await page.screenshot({ path: 'e2e/screenshots/portal-order-confirmed.png', fullPage: true })
})
