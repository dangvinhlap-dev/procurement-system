import { expect, test } from '@playwright/test'
import { ADMIN, THUKHO_BHA, loginViaApi } from './helpers.ts'
import { chooseOption, openByCommand, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Khối – Giám đốc khối – Trưởng phòng ban (lát O1, design.md mục 19.3): thêm khối ở BHA có Giám đốc khối là tài khoản có quyền tại
 * công ty, gán bộ phận vào khối kèm Trưởng phòng ban; danh sách bộ phận hiện khối, Trưởng phòng ban, Giám đốc khối.
 * Dữ liệu Testing không bị xóa giữa các lần chạy nên mã luôn mới.
 */
test('thêm khối, gán bộ phận vào khối kèm Trưởng phòng ban, chụp màn hình', async ({ page }) => {
  const divisionCode = unique('kh')
  const departmentCode = unique('bp')
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')

  await openByCommand(page, 'DIV')
  await expect(page.getByRole('heading', { name: 'Danh mục khối – BHA' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  const drawer = page.locator('.detail-drawer')
  await drawer.getByLabel('Mã khối').fill(divisionCode.toLowerCase())
  await drawer.getByLabel('Tên khối').fill(`Khối thử ${divisionCode}`)
  await chooseOption(page, 'Giám đốc khối', ADMIN.fullName)
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(`Đã thêm khối ${divisionCode}.`)
  await expect(page.locator('.data-grid-row', { hasText: divisionCode })).toContainText(ADMIN.fullName)

  await openByCommand(page, 'DEPT')
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await drawer.getByLabel('Mã bộ phận').fill(departmentCode)
  await drawer.getByLabel('Tên bộ phận').fill(`Phòng ${departmentCode}`)
  await chooseOption(page, 'Khối', divisionCode)
  await chooseOption(page, 'Trưởng phòng ban (tài khoản)', THUKHO_BHA.fullName)
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(`Đã thêm bộ phận ${departmentCode}.`)
  const row = page.locator('.data-grid-row', { hasText: departmentCode })
  await expect(row).toContainText(`${divisionCode} – Khối thử ${divisionCode}`)
  await expect(row).toContainText(THUKHO_BHA.fullName)
  await expect(row).toContainText(ADMIN.fullName)

  await openByCommand(page, 'DIV')
  await expect(page.locator('.data-grid-row', { hasText: divisionCode })).toContainText('1')
  await page.waitForLoadState('networkidle')
  await page.screenshot({ path: 'e2e/screenshots/division-list.png' })
})
