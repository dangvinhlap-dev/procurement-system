import { expect, test } from '@playwright/test'
import { THUKHO_BHA, loginViaApi } from './helpers.ts'

/**
 * Giao diện theo quyền (design.md mục 9.1): thủ kho BHA chỉ có quyền xem PO và lập/xem phiếu xác nhận nhận hàng (từ lát K3 – phân hệ kho
 * đã gỡ), không có quyền NCC, đấu thầu nên không thấy các màn hình đó, mở thẳng đường dẫn cũng bị chặn. Đổi mật khẩu báo lỗi tiếng Việt.
 */

test.beforeEach(async ({ page }) => {
  await loginViaApi(page, THUKHO_BHA)
  await page.goto('/')
  await expect(page.getByTestId('shell-bar').getByRole('button', { name: `Người dùng: ${THUKHO_BHA.fullName}` })).toBeVisible()
})

test('cây điều hướng chỉ hiện màn hình có quyền', async ({ page }) => {
  const nav = page.getByTestId('nav-tree')
  await expect(nav.getByRole('button', { name: /Xác nhận nhận hàng/ })).toBeVisible()
  await expect(nav.getByRole('button', { name: /Yêu cầu báo giá/ })).toHaveCount(0)
  await expect(nav.getByRole('button', { name: /Nhà cung cấp/ })).toHaveCount(0)
  // Nhóm không còn màn hình nào thì ẩn hẳn.
  await expect(nav.getByRole('button', { name: 'Đấu thầu', exact: true })).toHaveCount(0)
})

test('thanh menu ẩn nhóm không có màn hình được phép, chỉ được chọn công ty được gán', async ({ page }) => {
  const menuBar = page.getByTestId('shell-bar')
  // Thủ kho BHA không có quyền đấu thầu → nhóm Đấu thầu không hiện trên thanh menu chữ (như cây điều hướng).
  await expect(menuBar.getByRole('menuitem', { name: 'Mua hàng', exact: true })).toBeVisible()
  await expect(menuBar.getByRole('menuitem', { name: 'Đấu thầu', exact: true })).toHaveCount(0)
  await expect(page.getByText('Không có màn hình được phép')).toHaveCount(0)

  await menuBar.locator('.app-company-select').click()
  await expect(page.locator('.ant-select-dropdown .ant-select-item-option')).toHaveCount(1)
  await expect(page.locator('.ant-select-dropdown')).toContainText('BHA – Công ty Thép Bắc Hà')
})

test('mở thẳng đường dẫn không có quyền thì bị chặn', async ({ page }) => {
  await page.goto('/dau-thau/rfq')
  await expect(page.getByText('Bạn không có quyền mở màn hình này')).toBeVisible()
  await expect(page.locator('.data-grid')).toHaveCount(0)
})

test('ô lệnh không mở được màn hình không có quyền', async ({ page }) => {
  const box = page.getByPlaceholder('Ô lệnh – tên màn hình')
  await box.fill('RFQ')
  await box.press('Enter')
  await expect(page.locator('.ant-message')).toContainText('Không có màn hình "RFQ"')
  await expect(page).toHaveURL(/\/$/)
})

test('đổi mật khẩu: sai mật khẩu hiện tại và nhập lại không khớp đều báo tiếng Việt', async ({ page }) => {
  await page.getByRole('button', { name: `Người dùng: ${THUKHO_BHA.fullName}` }).click()
  await page.getByRole('menuitem', { name: 'Đổi mật khẩu' }).click()
  const dialog = page.getByRole('dialog', { name: 'Đổi mật khẩu' })
  await expect(dialog).toBeVisible()

  await dialog.getByLabel('Mật khẩu hiện tại').fill('sai-mat-khau-1')
  await dialog.getByLabel('Mật khẩu mới', { exact: true }).fill('moimoi2026')
  await dialog.getByLabel('Nhập lại mật khẩu mới').fill('khac2026')
  await dialog.getByRole('button', { name: 'Đổi mật khẩu' }).click()
  await expect(dialog.getByText('Mật khẩu nhập lại không khớp.')).toBeVisible()

  await dialog.getByLabel('Nhập lại mật khẩu mới').fill('moimoi2026')
  await dialog.getByRole('button', { name: 'Đổi mật khẩu' }).click()
  await expect(dialog.getByText('Mật khẩu hiện tại không đúng.')).toBeVisible()

  await page.keyboard.press('Escape')
  await expect(dialog).toBeHidden()
})
