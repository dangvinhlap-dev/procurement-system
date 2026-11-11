import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Danh mục đơn vị mua (DVM – lát Y5a, design.md mục 19.3 – Q3): quản trị BHA thêm đơn vị mua, danh sách hiện "Đang dùng"; mở lại, bỏ
 * "Còn sử dụng" thì danh sách hiện "Ngừng dùng" và mã không sửa được. Quy tắc chi tiết (mã trùng, phạm vi công ty, quyền, danh sách chọn) kiểm
 * tra ở test tích hợp PurchasingUnitTests.
 */
test('thêm đơn vị mua, ngừng dùng, chụp màn hình', async ({ page }) => {
  // Mã riêng của test – dữ liệu Testing không bị xóa giữa các lần chạy.
  const code = unique('DV').replace(/[^A-Z0-9]/gi, '').slice(0, 12).toUpperCase()

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'DVM')
  await expect(page.getByRole('heading', { name: /Danh mục đơn vị mua/ })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  const drawer = page.locator('.detail-drawer')
  await drawer.getByLabel('Mã đơn vị').fill(code.toLowerCase())
  await drawer.getByLabel('Tên đơn vị mua').fill(`Văn phòng người đại diện ${code}`)
  await drawer.getByLabel('Ghi chú').fill('Mua hàng ăn uống tiếp khách')
  await page.screenshot({ path: 'e2e/screenshots/purchasing-unit-new.png', animations: 'disabled' })
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(`Đã thêm đơn vị mua ${code}`)

  const row = page.locator('.data-grid-row', { hasText: code })
  await expect(row).toContainText('Đang dùng')
  await expect(row).toContainText('Mua hàng ăn uống tiếp khách')
  await page.waitForLoadState('networkidle')
  await page.screenshot({ path: 'e2e/screenshots/purchasing-unit-list.png' })

  await row.dblclick()
  await expect(drawer.getByLabel('Mã đơn vị')).toBeDisabled()
  await drawer.getByLabel('Còn sử dụng').uncheck()
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(`Đã lưu đơn vị mua ${code}`)
  await expect(page.locator('.data-grid-row', { hasText: code })).toContainText('Ngừng dùng')
})
