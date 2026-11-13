import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, chooseOption, openByCommand, switchCompany, unique } from './inventoryHelpers.ts'
import { createBhaAdmin } from './receiptHelpers.ts'

/**
 * Lát Y11a (design.md 19.2 loại C): bảng kê chi phí tạp vụ 2C – mở bằng ô lệnh BKT, chọn bộ phận, nhập hai khoản chi trong lưới (ngày chi, nội
 * dung, nơi mua, số tiền), lưu, gửi duyệt; người duyệt khác duyệt (API) → trạng thái Đã duyệt, phiếu khóa. Lát Y11b: từ bảng kê đã duyệt bấm
 * "Lập đề nghị thanh toán 1C" → phiếu 1C chọn sẵn bộ phận và bảng kê, nhập người nhận tiền, lưu.
 */
test('bảng kê chi phí tạp vụ: nhập khoản chi, lưu, gửi duyệt, duyệt, lập đề nghị 1C', async ({ page, request }) => {
  test.setTimeout(120_000)
  const bha = await api(request, 'BHA')
  const code = unique('TV')
  await bha.send('post', '/api/departments', { code, name: `Phòng tạp vụ ${code}`, managerName: null })

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'BKT')
  await expect(page.getByRole('heading', { name: 'Bảng kê chi phí tạp vụ (BKT)' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await expect(page).toHaveURL(/bang-ke-tap-vu\/moi/)

  await chooseOption(page, 'Bộ phận', code)
  await page.getByLabel('Nội dung chi dòng 1').fill('Nước uống văn phòng')
  await page.getByLabel('Nơi mua dòng 1').fill('Cửa hàng tiện lợi')
  await page.getByLabel('Số tiền dòng 1').fill('150000')
  await page.getByRole('button', { name: 'Thêm dòng' }).click()
  await expect(page.locator('.line-grid-row')).toHaveCount(2)
  await page.getByLabel('Nội dung chi dòng 2').fill('Gửi chuyển phát hồ sơ')
  await page.getByLabel('Số tiền dòng 2').fill('45500')
  await expect(page.locator('.document-form-content')).toContainText('195.500')
  await page.screenshot({ path: 'e2e/screenshots/sundry-expense-new.png', fullPage: true })

  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu BKT-BHA-\d{4}-\d{4}\./)
  await expect(page.locator('.document-form-number')).toHaveText(/^BKT-BHA-\d{4}-\d{4}$/)
  const no = (await page.locator('.document-form-number').textContent())!
  const id = /([0-9a-f-]{36})/.exec(page.url())![1]
  await page.getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  await page.getByRole('dialog').getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã gửi duyệt phiếu ${no}.`)

  // Người duyệt khác (quản trị BHA mới) duyệt qua API.
  const approver = await createBhaAdmin(bha)
  const login = await request.post('/api/auth/login', { data: { userName: approver.userName, password: approver.password } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { companies: { id: string; code: string }[] } }
  const approved = await request.post(`/api/sundry-expense-sheets/${id}/approve`, {
    headers: { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.companies.find((c) => c.code === 'BHA')!.id },
    data: { comment: 'Đồng ý' },
  })
  expect(approved.ok(), await approved.text()).toBeTruthy()

  await page.reload()
  await expect(page.locator('.document-form')).toContainText('Đã duyệt')
  await expect(page.getByLabel('Nội dung chi dòng 1')).toBeDisabled()
  await page.screenshot({ path: 'e2e/screenshots/sundry-expense-approved.png', fullPage: true })

  // Lát Y11b: lập đề nghị thanh toán tổng hợp 1C từ bảng kê.
  await page.getByRole('button', { name: 'Lập đề nghị thanh toán 1C' }).click()
  await expect(page).toHaveURL(/de-nghi-thanh-toan\/moi\?form=1C/)
  await expect(page.getByRole('heading', { name: 'Đề nghị thanh toán tổng hợp tạp vụ (1C)' })).toBeVisible()
  await expect(page.locator('.line-grid-row')).toHaveCount(1)
  await expect(page.locator('.document-form-content')).toContainText(no)
  await page.getByLabel('Người nhận tiền').fill('Nguyễn Văn An')
  await page.getByLabel('Số tiền giảm trừ').fill('5500')
  await page.getByLabel('Lý do giảm trừ').fill('Trừ tạm ứng')
  await expect(page.locator('.document-form-content')).toContainText('190.000')
  await page.screenshot({ path: 'e2e/screenshots/sundry-payment-1c-new.png', fullPage: true })
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu DNT-BHA-\d{4}-\d{4}\./)
  await expect(page.locator('.document-form-number')).toHaveText(/^DNT-BHA-\d{4}-\d{4}$/)
  const dntNo = (await page.locator('.document-form-number').textContent())!

  // Danh sách: phiếu đã duyệt, tổng 195.500.
  await page.goto('/mua-hang/bang-ke-tap-vu')
  const row = page.locator('.data-grid-row', { hasText: no })
  await expect(row).toContainText('195.500')
  await expect(row).toContainText('Đã duyệt')
  await expect(row).toContainText(dntNo)
})
