import { expect, test, type APIRequestContext } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, seedPostedReceipt, switchCompany, unique } from './inventoryHelpers.ts'

/** Mời một tài khoản Portal cho NCC, kích hoạt, đăng nhập 2 bước qua API; trả dữ liệu phiên Portal (như màn hình đăng nhập lưu). */
async function portalSession(request: APIRequestContext, supplierId: string) {
  const head = await api(request)
  const email = `${unique('bg').toLowerCase()}@ncc-thu.vn`
  const invite = await head.send<{ inviteUrl: string }>('post', `/api/suppliers/${supplierId}/portal-users`, {
    email, fullName: 'Trần Báo Giá', phone: null, position: 'Nhân viên kinh doanh',
  })
  const token = new URL(invite.inviteUrl).searchParams.get('token')!
  expect((await request.post('/api/portal/auth/activate', { data: { token, password: 'ncc12345' } })).status()).toBe(204)
  const challenge = await (await request.post('/api/portal/auth/login', { data: { email, password: 'ncc12345' } })).json()
  const session = await (await request.post('/api/portal/auth/verify-otp', { data: { challengeId: challenge.challengeId, code: challenge.devCode } })).json()
  return session as { accessToken: string }
}

/**
 * Lát P3a (design.md 19.5, Q15; người dùng yêu cầu 12/11/2026 "phải kèm giờ, trước giờ mở chỉ người có quyền mới được xem"): RFQ hạn nộp
 * kèm giờ → NCC thấy thư mời trên Portal, nộp báo giá (biên nhận mã băm) → nội bộ chỉ thấy "Niêm phong – nộp lúc…", không nhập thay; người
 * có quyền xem trước giờ mở (hỏi lại, ghi nhật ký). Mở báo giá sau giờ hạn đã có test tích hợp (cần dời giờ).
 */
test('Portal: NCC nộp báo giá niêm phong, nội bộ xem trước giờ mở có ghi nhật ký', async ({ page, request }) => {
  test.setTimeout(90_000)
  const { itemId, supplierId } = await seedPostedReceipt(request)
  const bha = await api(request, 'BHA')
  const hop = (await bha.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'HOP')!.id
  const today = new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const in3 = new Date(Date.now() + 3 * 86_400_000).toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const title = `Báo giá online ${unique('Q')}`
  const rfq = await bha.send<{ id: string; no: string }>('post', '/api/rfqs', {
    date: today, title, deadline: in3, deadlineTime: '16:30', sourcingId: null,
    weights: { price: 60, quality: 20, delivery: 10, payment: 5, warranty: 5 }, note: 'Giao tại kho KT',
    lines: [{ id: null, itemId, uomId: hop, qty: 20, sourcePrLineId: null, note: null }], supplierIds: [supplierId],
  })
  await bha.send('post', `/api/rfqs/${rfq.id}/send`, { reason: 'Chỉ một NCC cung cấp' })

  // NCC vào Portal (phiên đặt sẵn như sau khi đăng nhập OTP), mở thư mời từ danh sách.
  const session = await portalSession(request, supplierId)
  await page.goto('/ncc/dang-nhap')
  await page.evaluate((s) => sessionStorage.setItem('proc.portal.session', JSON.stringify(s)), session)
  await page.goto('/ncc')
  await page.getByRole('link', { name: 'Thư mời báo giá' }).click()
  const row = page.locator('.data-grid-row', { hasText: rfq.no })
  await expect(row).toContainText('Đang nhận báo giá')
  await expect(row).toContainText('16:30')
  await page.screenshot({ path: 'e2e/screenshots/portal-rfq-list.png' })
  await row.dblclick()
  await expect(page.getByTestId('portal-rfq-info')).toContainText(title)
  await expect(page.getByTestId('portal-rfq-info')).toContainText('16:30')

  // Lát P3b-1: tải file báo giá lên ngay (không cần nộp giá trước); file gắn với thư mời, thêm/xóa đến giờ hạn.
  await page.getByLabel('Ghi chú file').fill('Bảng giá chi tiết')
  await page.locator('.erp-portal-file-bar input[type=file]').setInputFiles({
    name: 'BangGia.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4' + String.fromCharCode(10) + '%%EOF'),
  })
  await expect(page.locator('.ant-message')).toContainText('Đã tải lên "BangGia.pdf"')
  await expect(page.getByTestId('portal-quote-files')).toContainText('Bảng giá chi tiết')

  // Nhập giá, nộp; hộp xác nhận nói rõ niêm phong đến hạn nộp.
  await page.getByLabel('Đơn giá dòng 1').fill('52000')
  await page.getByLabel('Chiết khấu dòng 1').fill('2')
  await page.getByLabel('Thời gian giao dòng 1').fill('5')
  await page.getByLabel('Điều kiện thanh toán').fill('Chuyển khoản 30 ngày')
  await expect(page.locator('.line-grid, .erp-portal-section').getByText('1.019.200').first()).toBeVisible() // 20 × 52.000 × 98%.
  await page.getByRole('button', { name: 'Nộp báo giá' }).click()
  const confirm = page.locator('.ant-modal-confirm')
  await expect(confirm).toContainText('niêm phong đến hết hạn nộp')
  await confirm.getByRole('button', { name: 'Nộp báo giá' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã nộp báo giá lần 1')
  await expect(page.getByText('Báo giá của bạn – đang có hiệu lực: lần 1')).toBeVisible()
  await expect(page.locator('.data-grid-row').filter({ hasText: '1.019.200' })).toHaveCount(1)
  await page.screenshot({ path: 'e2e/screenshots/portal-rfq-quote.png', fullPage: true })

  // Nội bộ: chỉ thấy giờ nộp (niêm phong), không có nút nhập thay; quản trị có quyền xem trước giờ mở – hỏi lại, rồi thấy giá.
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/dau-thau/rfq/${rfq.id}?tab=compare`)
  await expect(page.locator('.document-form-number')).toHaveText(rfq.no)
  await expect(page.getByText(/Niêm phong – nộp .* \(1 lần\)/)).toBeVisible()
  await expect(page.getByRole('button', { name: /^Nhập báo giá/ })).toHaveCount(0)
  await expect(page.getByRole('button', { name: /Mở báo giá Portal$/ })).toHaveCount(0) // Chưa đến giờ hạn.
  await page.getByRole('button', { name: /Xem báo giá niêm phong$/ }).click()
  await expect(page.locator('.ant-modal-confirm')).toContainText('ghi vào nhật ký truy cập')
  await page.locator('.ant-modal-confirm').getByRole('button', { name: /Xem$/ }).click()
  const sealedDialog = page.getByRole('dialog', { name: `Báo giá Portal niêm phong – RFQ ${rfq.no}` })
  await expect(sealedDialog).toContainText('52.000')
  await expect(sealedDialog).toContainText('1.019.200')
  await page.screenshot({ path: 'e2e/screenshots/rfq-sealed-quotes.png' })
  await page.keyboard.press('Escape')
  await expect(sealedDialog).toBeHidden()

  // File báo giá Portal: người có quyền xem niêm phong mở được danh sách file (tải xuống ghi nhật ký).
  await page.getByRole('button', { name: /File báo giá NCC/ }).click()
  const filesDialog = page.getByRole('dialog', { name: /File báo giá Portal/ })
  await expect(filesDialog).toContainText('BangGia.pdf')
  await expect(filesDialog).toContainText('niêm phong')
  await page.screenshot({ path: 'e2e/screenshots/rfq-portal-files.png' })
})
