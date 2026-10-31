import { writeFileSync } from 'node:fs'
import { expect, test } from '@playwright/test'
import { ADMIN, loginViaApi } from './helpers.ts'
import { openByCommand, seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * In phiếu PDF (design.md mục 10.10): mở phiếu nhập đã ghi sổ ở BHA, bấm "In phiếu" → tab mới hiện file PDF; Ctrl+P cũng in.
 * File PDF lưu vào e2e/screenshots để so mẫu in.
 */
test('in phiếu nhập kho ra PDF ở tab mới', async ({ page, request }) => {
  const { receiptNo } = await seedPostedReceipt(request)
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'GRN')
  await page.locator('.data-grid-row', { hasText: receiptNo }).dblclick()
  await expect(page.locator('.document-form-number')).toHaveText(receiptNo)

  const response = page.waitForResponse((r) => r.url().endsWith('/pdf'))
  const popup = page.waitForEvent('popup')
  await page.getByRole('button', { name: 'In phiếu' }).click()
  const pdf = await response
  expect(pdf.status()).toBe(200)
  expect(pdf.headers()['content-type']).toContain('application/pdf')
  // Nội dung file tải lại bằng API (Playwright không giữ thân của phản hồi fetch đã được trang đọc thành blob).
  const login = await request.post('/api/auth/login', { data: { userName: ADMIN.userName, password: ADMIN.password } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { companies: { id: string; code: string }[] } }
  const bha = user.companies.find((c) => c.code === 'BHA')!.id
  const file = await request.get(new URL(pdf.url()).pathname, { headers: { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': bha } })
  const body = await file.body()
  expect(body.subarray(0, 4).toString()).toBe('%PDF')
  writeFileSync('e2e/screenshots/phieu-nhap-kho.pdf', body)
  // Chromium chạy ẩn không có trình xem PDF (coi như tải file) nên chỉ kiểm tra tab mới được mở và không bị đóng vì lỗi.
  const tab = await popup
  expect(tab.isClosed()).toBe(false)
  await tab.close()

  // Ctrl+P: in phiếu thay cho in trang web.
  const again = page.waitForEvent('popup')
  const reprint = page.waitForResponse((r) => r.url().endsWith('/pdf'))
  await page.keyboard.press('Control+p')
  expect((await reprint).status()).toBe(200)
  expect((await again).isClosed()).toBe(false)
})
