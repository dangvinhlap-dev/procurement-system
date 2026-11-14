import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, unique } from './inventoryHelpers.ts'
import { portalSession } from './portalHelpers.ts'

/**
 * Lát P2a (design.md 19.5, P2-1…P2-6): quản trị NCC gửi đề nghị cập nhật hồ sơ trên Portal (mã số thuế chỉ xem) kèm file; Phòng CUVT mở màn
 * hình "Đề nghị cập nhật hồ sơ NCC", xem so sánh và chấp nhận – thông tin chung áp ngay, tài khoản ngân hàng thành yêu cầu duyệt NCC.
 */
test('Portal: NCC đề nghị cập nhật hồ sơ, Phòng CUVT chấp nhận', async ({ page, request }) => {
  test.setTimeout(120_000)
  const head = await api(request)
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const name = `NCC hồ sơ ${unique('HS')}`
  const supplier = await head.send<{ id: string; code: string }>('post', '/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name, shortName: null, isForeign: false, taxCode: null,
    address: 'Hà Nội', phone: null, email: null, website: null, paymentTerms: '30 ngày', supplyScope: null, rating: null, status: 'Potential',
    statusNote: null, internalCompanyId: null, contacts: [{ id: null, name: 'Anh Kinh Doanh', position: null, phone: '0901', email: null, isPrimary: true }],
    bankAccounts: [],
  })
  const session = await portalSession(request, supplier.id)
  await page.goto('/ncc/dang-nhap')
  await page.evaluate((s) => sessionStorage.setItem('proc.portal.session', JSON.stringify(s)), session)

  await page.goto('/ncc/ho-so')
  await expect(page.getByRole('heading', { name: 'Hồ sơ công ty', exact: true })).toBeVisible()
  await expect(page.getByTestId('portal-profile')).toContainText(name)
  await expect(page.getByTestId('portal-profile-current-contacts')).toContainText('Anh Kinh Doanh')

  await page.getByRole('button', { name: 'Đề nghị cập nhật hồ sơ…' }).click()
  const form = page.locator('section', { hasText: 'Đề nghị cập nhật hồ sơ' }).filter({ has: page.getByTestId('portal-profile-form') })
  // Mã số thuế chỉ xem – Phòng CUVT sửa trong phần mềm.
  await expect(form.getByLabel('Mã số thuế')).toHaveAttribute('readonly', '')
  await form.getByLabel('Địa chỉ').fill('12 Lê Lợi, Hải Phòng')
  await form.getByLabel('Ghi chú đề nghị').fill('Đổi trụ sở từ 01/12')
  await form.getByTestId('portal-profile-banks').getByRole('button', { name: 'Thêm dòng' }).click()
  await form.getByLabel('Ngân hàng dòng 1').fill('BIDV')
  await form.getByLabel('Số tài khoản dòng 1').fill('12345678')
  await form.getByLabel('Mặc định dòng 1').check()
  await form.locator('input[type=file]').setInputFiles({
    name: 'GiayPhep.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4' + String.fromCharCode(10) + '%%EOF'),
  })
  await expect(form.getByText('GiayPhep.pdf')).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/portal-profile-form.png', fullPage: true })
  await form.getByRole('button', { name: 'Gửi đề nghị' }).click()
  await page.locator('.ant-modal-confirm').getByRole('button', { name: 'Gửi' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã gửi đề nghị ${supplier.code}-HS01`)

  const requests = page.getByTestId('portal-profile-requests')
  await expect(requests).toContainText('Chờ Phòng CUVT xử lý')
  await expect(page.getByRole('button', { name: 'Đề nghị cập nhật hồ sơ…' })).toBeDisabled()
  await requests.locator('.data-grid-row', { hasText: `${supplier.code}-HS01` }).click()
  await expect(page.getByTestId('profile-compare-fields')).toContainText('12 Lê Lợi, Hải Phòng')
  await expect(page.getByTestId('profile-compare-banks')).toContainText('BIDV')
  await expect(page.getByTestId('portal-profile-files')).toContainText('GiayPhep.pdf')
  await expect(page.getByRole('button', { name: 'Rút đề nghị…' })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/portal-profile.png', fullPage: true })

  // Nội bộ: Phòng CUVT tổng công ty mở màn hình HSC (mặc định lọc đề nghị đang chờ), xem so sánh, chấp nhận.
  const internal = await page.context().newPage()
  await loginViaApi(internal)
  await internal.goto('/gia-ncc/de-nghi-cap-nhat-ncc')
  await expect(internal.getByRole('heading', { name: 'Đề nghị cập nhật hồ sơ NCC' })).toBeVisible()
  const row = internal.locator('.data-grid-row', { hasText: `${supplier.code}-HS01` })
  await expect(row).toContainText('Địa chỉ; tài khoản ngân hàng')
  await row.dblclick()
  const drawer = internal.locator('.ant-drawer')
  await expect(drawer.getByTestId('profile-compare-fields')).toContainText('12 Lê Lợi, Hải Phòng')
  await expect(drawer.getByTestId('profile-compare-fields')).toContainText('= lúc gửi')
  await expect(drawer).toContainText('Đổi trụ sở từ 01/12')
  await internal.screenshot({ path: 'e2e/screenshots/supplier-profile-request.png', fullPage: true })
  await drawer.getByRole('button', { name: 'Chấp nhận' }).click()
  await internal.locator('.ant-modal-confirm').getByRole('button', { name: 'Chấp nhận' }).click()
  await expect(internal.locator('.ant-message')).toContainText(`Đã chấp nhận đề nghị ${supplier.code}-HS01`)
  await expect(drawer).toContainText('Đã chấp nhận')
  await expect(drawer).toContainText('Yêu cầu duyệt tài khoản')

  const detail = await head.get<{ address: string; bankAccounts: unknown[] }>(`/api/suppliers/${supplier.id}`)
  expect(detail.address).toBe('12 Lê Lợi, Hải Phòng')
  expect(detail.bankAccounts).toHaveLength(0)

  // Portal thấy kết quả, gửi được đề nghị mới; file đã khóa.
  await page.reload()
  await expect(requests).toContainText('Đã chấp nhận')
  await expect(page.getByRole('button', { name: 'Đề nghị cập nhật hồ sơ…' })).toBeEnabled()
})
