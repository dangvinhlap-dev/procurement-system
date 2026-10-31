import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { ADMIN, loginViaApi } from './helpers.ts'
import { openByCommand } from './inventoryHelpers.ts'

/**
 * Xếp loại nhà cung cấp (design.md mục 11.3): tổng công ty tự cấu hình ngưỡng; hạng sau khắt khe hơn hạng trước thì bị chặn; báo cáo
 * Đánh giá NCC có cột Xếp loại. Cuối test xóa các hạng vừa thêm để CSDL test trở về chưa cấu hình.
 */

const drawer = (page: Page) => page.locator('.detail-drawer')

/** Xóa mọi hạng qua API (tổng công ty) – để test chạy lại được kể cả khi lần trước dừng giữa chừng. */
async function clearRules(request: APIRequestContext) {
  const login = await request.post('/api/auth/login', { data: { userName: ADMIN.userName, password: ADMIN.password } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { defaultCompanyId: string } }
  const headers = { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.defaultCompanyId }
  const rules = (await (await request.get('/api/supplier-rating-rules', { headers })).json()) as { id: string }[]
  for (const rule of rules) expect((await request.delete(`/api/supplier-rating-rules/${rule.id}`, { headers })).ok()).toBeTruthy()
}

/** Thêm một hạng qua ngăn kéo. */
async function addRule(page: Page, code: string, name: string, order: string, onTime: string, returned: string) {
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await drawer(page).getByLabel('Mã hạng').fill(code)
  await drawer(page).getByLabel('Tên hạng').fill(name)
  await drawer(page).getByLabel('Thứ tự xét').fill(order)
  await drawer(page).getByLabel('Đúng hạn tối thiểu (%)').fill(onTime)
  await drawer(page).getByLabel('Trả hàng tối đa (%)').fill(returned)
  await page.keyboard.press('Control+s')
}

test('cấu hình hạng xếp loại NCC, báo cáo đánh giá có cột xếp loại', async ({ page, request }) => {
  await clearRules(request)
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'RATE')
  await expect(page.getByRole('heading', { name: 'Xếp loại nhà cung cấp' })).toBeVisible()
  await expect(page.locator('.ant-alert')).toContainText('Chưa cấu hình hạng nào')

  await addRule(page, 'a', 'Tốt', '1', '95', '1')
  await expect(page.locator('.data-grid-row', { hasText: 'Tốt' })).toContainText('95')
  await addRule(page, 'B', 'Khá', '2', '98', '3')
  await expect(drawer(page).getByText('không được khắt khe hơn')).toBeVisible()
  await drawer(page).getByLabel('Đúng hạn tối thiểu (%)').fill('85')
  await page.keyboard.press('Control+s')
  await expect(page.locator('.data-grid-row', { hasText: 'Khá' })).toContainText('85')
  await expect(page.locator('.detail-drawer')).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/supplier-rating.png' })

  await openByCommand(page, 'SUPEVAL')
  await expect(page.getByRole('columnheader', { name: 'Xếp loại' })).toBeVisible()

  // Dọn: xóa hai hạng vừa thêm.
  await openByCommand(page, 'RATE')
  for (const name of ['Tốt', 'Khá']) {
    await page.locator('.data-grid-row', { hasText: name }).dblclick()
    await drawer(page).getByRole('button', { name: 'Xóa hạng' }).click()
    await page.locator('.ant-modal-confirm').getByRole('button', { name: 'Xóa' }).click()
    await expect(page.locator('.data-grid-row', { hasText: name })).toHaveCount(0)
  }
  await expect(page.locator('.ant-alert')).toContainText('Chưa cấu hình hạng nào')
})
