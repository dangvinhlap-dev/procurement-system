import { expect, test, type Page } from '@playwright/test'
import { ADMIN, loginViaApi } from './helpers.ts'

/**
 * Màn hình Nhật ký thao tác và Nhật ký truy cập (design.md mục 9.3).
 */

async function switchCompany(page: Page, label: string) {
  await page.getByTestId('menu-bar').locator('.app-company-select').click()
  await page.locator('.ant-select-dropdown .ant-select-item-option', { hasText: label }).click()
}

async function openScreen(page: Page, navLabel: string) {
  await page.getByTestId('nav-tree').getByRole('button', { name: navLabel }).click()
}

/** Tạo vai trò qua API tại công ty BHA để có dòng nhật ký; trả mã vai trò. */
async function createRoleAtBha(page: Page): Promise<string> {
  const login = await page.request.post('/api/auth/login', { data: { userName: ADMIN.userName, password: ADMIN.password } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { companies: { id: string; code: string }[] } }
  const bha = user.companies.find((c) => c.code === 'BHA')!
  const code = `NK${Date.now().toString(36)}`.toUpperCase()
  const response = await page.request.post('/api/roles', {
    headers: { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': bha.id },
    data: { code, name: 'Vai trò xem nhật ký', permissionCodes: ['Inventory.Stock.View'] },
  })
  expect(response.status()).toBe(201)
  return code
}

test.beforeEach(async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
})

test('nhật ký thao tác hiện việc tạo vai trò và so sánh trước/sau', async ({ page }) => {
  const code = await createRoleAtBha(page)
  await switchCompany(page, 'BHA')
  await openScreen(page, 'Nhật ký thao tác')
  await expect(page.getByRole('heading', { name: 'Nhật ký thao tác – BHA' })).toBeVisible()

  await page.getByLabel('Tìm kiếm', { exact: true }).fill(code)
  const row = page.locator('.data-grid-row', { hasText: 'Vai trò' }).filter({ hasText: code })
  await expect(row).toContainText('Tạo')
  await expect(row).toContainText(ADMIN.userName)

  await row.dblclick()
  const drawer = page.locator('.detail-drawer')
  await expect(drawer.getByText(`Tạo vai trò: ${code}`)).toBeVisible()
  const codeRow = drawer.locator('.ant-table-row', { hasText: 'Code' })
  await expect(codeRow).toContainText(code)
  await expect(codeRow).toContainText('–')
  await expect(drawer.getByRole('button', { name: 'Lưu' })).toHaveCount(0)
})

test('nhật ký truy cập ghi đăng nhập và đổi công ty', async ({ page }) => {
  await switchCompany(page, 'BHA')
  await openScreen(page, 'Nhật ký truy cập')
  await expect(page.getByRole('heading', { name: 'Nhật ký truy cập' })).toBeVisible()

  await page.getByLabel('Tìm kiếm', { exact: true }).fill(ADMIN.userName)
  await expect(page.locator('.data-grid-row', { hasText: 'Đăng nhập' }).first()).toBeVisible()
  await expect(page.locator('.data-grid-row', { hasText: 'Đổi công ty' }).filter({ hasText: 'BHA' }).first()).toBeVisible()
})
