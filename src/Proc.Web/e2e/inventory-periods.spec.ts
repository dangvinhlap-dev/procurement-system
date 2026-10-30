import { expect, test, type Page } from '@playwright/test'
import { ADMIN, loginViaApi } from './helpers.ts'

/**
 * Kỳ kho (design.md mục 7, 7.6): tạo lịch năm, mở kỳ tuần tự theo tháng. Mỗi lần chạy tạo công ty mới qua API
 * (quản trị được gán vào công ty mới) để lịch kỳ luôn bắt đầu từ trống.
 */

async function openByCommand(page: Page, code: string) {
  const box = page.getByPlaceholder('Ô lệnh – tên màn hình')
  await box.fill(code)
  await box.press('Enter')
}

async function switchCompany(page: Page, label: string) {
  await page.getByTestId('menu-bar').locator('.app-company-select').click()
  await page.locator('.ant-select-dropdown .ant-select-item-option', { hasText: label }).click()
}

test('tạo lịch kỳ năm rồi mở kỳ tháng theo thứ tự', async ({ page, request }) => {
  const login = await request.post('/api/auth/login', { data: { userName: ADMIN.userName, password: ADMIN.password } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { defaultCompanyId: string } }
  const code = `K${Date.now().toString(36).slice(-6)}`.toUpperCase()
  const created = await request.post('/api/companies', {
    headers: { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.defaultCompanyId },
    data: { code, name: `Công ty thử kỳ ${code}`, shortName: null, taxCode: null, address: null, representative: null, startDate: null, mainWarehouseCode: 'KT', mainWarehouseName: 'Kho tổng' },
  })
  expect(created.ok(), await created.text()).toBeTruthy()

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, code)
  await openByCommand(page, 'PER')
  await expect(page.getByRole('heading', { name: `Kỳ kho – ${code}` })).toBeVisible()
  await expect(page.getByText('Công ty chưa có lịch kỳ kho.', { exact: false })).toBeVisible()

  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Tạo lịch' }).click()
  const year = new Date().getFullYear()
  await expect(page.locator('.data-grid-row', { hasText: `Năm ${year}` })).toContainText('Đang mở')
  await expect(page.locator('.data-grid-row', { hasText: `03/${year}` })).toContainText('Chưa mở')

  // Kỳ đầu tiên tự chọn: mở tháng 3; sau đó tháng 5 chưa mở được (phải mở tháng 4 trước).
  const drawer = page.locator('.detail-drawer')
  await page.locator('.data-grid-row', { hasText: `03/${year}` }).dblclick()
  await drawer.getByRole('button', { name: 'Mở kỳ' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã mở kỳ 03/${year}.`)
  await expect(page.locator('.data-grid-row', { hasText: `03/${year}` })).toContainText('Đang mở')
  await expect(page.locator('.data-grid-row', { hasText: `04/${year}` })).toContainText('Mở kỳ')

  await page.locator('.data-grid-row', { hasText: `05/${year}` }).dblclick()
  await expect(drawer.getByRole('button', { name: 'Mở kỳ' })).toBeDisabled()
  await expect(drawer.getByText('Mở kỳ tuần tự theo tháng', { exact: false })).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(drawer).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/inventory-periods.png' })
})
