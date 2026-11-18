import { writeFileSync } from 'node:fs'
import { expect, test } from '@playwright/test'
import { ADMIN, loginViaApi } from './helpers.ts'
import { seedDraftPo, switchCompany } from './inventoryHelpers.ts'

/**
 * In phiếu PDF (design.md mục 10.10): mở đơn mua hàng nháp ở BHA, bấm "Xem bảng theo dõi" (lát IN2) → hộp thoại xem trang in 4B; Ctrl+P cũng
 * mở. File PDF lưu vào e2e/screenshots để so mẫu in. (Phiếu nhập kho cũ gỡ ở lát K3; biên bản giao nhận in từ XNH – receipt-confirmation.spec.ts.)
 */
test('xem bảng theo dõi đơn mua hàng (PDF)', async ({ page, request }) => {
  const { poId, poNo } = await seedDraftPo(request)
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/po/${poId}`)
  await expect(page.locator('.document-form-number')).toHaveText(poNo)

  const response = page.waitForResponse((r) => r.url().endsWith('/pdf'))
  await page.getByRole('button', { name: 'Xem bảng theo dõi' }).click()
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
  writeFileSync('e2e/screenshots/don-mua-hang.pdf', body)
  const dialog = page.getByRole('dialog', { name: new RegExp(`Xem bảng theo dõi mua hàng ${poNo}`) })
  await expect(page.getByTestId('print-preview-frame')).toBeVisible()
  await dialog.getByRole('button', { name: 'Đóng', exact: true }).click()
  await expect(dialog).toHaveCount(0)

  // Ctrl+P: mở bản xem thay cho in trang web.
  const reprint = page.waitForResponse((r) => r.url().endsWith('/pdf'))
  await page.keyboard.press('Control+p')
  expect((await reprint).status()).toBe(200)
  await expect(dialog).toBeVisible()
})
