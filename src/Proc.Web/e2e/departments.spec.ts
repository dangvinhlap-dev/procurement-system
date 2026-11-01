import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Danh mục bộ phận theo công ty (design.md mục 5.1, 18.5): thêm bộ phận ở BHA, mã tự viết hoa, sửa ngừng dùng.
 * Dữ liệu Testing không bị xóa giữa các lần chạy nên mã bộ phận luôn mới.
 */
test('thêm bộ phận, ngừng dùng, chụp màn hình danh mục', async ({ page }) => {
  const code = unique('bp')
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'DEPT')
  await expect(page.getByRole('heading', { name: 'Danh mục bộ phận – BHA' })).toBeVisible()

  await page.getByRole('button', { name: 'Tạo mới' }).click()
  const drawer = page.locator('.detail-drawer')
  await drawer.getByLabel('Mã bộ phận').fill(code.toLowerCase())
  await drawer.getByLabel('Tên bộ phận').fill(`Phân xưởng ${code}`)
  await drawer.getByLabel('Trưởng bộ phận').fill('Nguyễn Văn An')
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(`Đã thêm bộ phận ${code}.`)
  const row = page.locator('.data-grid-row', { hasText: code })
  await expect(row).toContainText('Nguyễn Văn An')
  await expect(row).toContainText('Đang dùng')

  await row.dblclick()
  await expect(drawer.getByText(`Bộ phận: ${code}`)).toBeVisible()
  await expect(drawer.getByLabel('Mã bộ phận')).toBeDisabled()
  await drawer.getByRole('checkbox', { name: 'Còn sử dụng' }).uncheck()
  await drawer.getByRole('button', { name: 'Lưu' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã lưu bộ phận ${code}.`)
  await expect(row).toContainText('Ngừng dùng')
  // Bấm đúp để mở dòng làm bôi đen chữ trong ô – bỏ chọn cho ảnh chụp sạch (chuỗi vì tsconfig e2e không có lib DOM).
  await page.evaluate('getSelection()?.removeAllRanges()')
  await page.waitForLoadState('networkidle')
  await page.screenshot({ path: 'e2e/screenshots/department-list.png' })
})
