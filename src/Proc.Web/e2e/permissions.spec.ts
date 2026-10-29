import { expect, test } from '@playwright/test'
import { THUKHO_BHA, loginViaApi } from './helpers.ts'

/**
 * Giao diện theo quyền (design.md mục 9.1): thủ kho BHA không có quyền mua hàng nên không thấy màn hình mua hàng,
 * và mở thẳng đường dẫn cũng bị chặn. Đổi mật khẩu báo lỗi tiếng Việt.
 */

test.beforeEach(async ({ page }) => {
  await loginViaApi(page, THUKHO_BHA)
  await page.goto('/')
  await expect(page.getByTestId('menu-bar')).toContainText(THUKHO_BHA.fullName)
})

test('cây điều hướng chỉ hiện màn hình có quyền', async ({ page }) => {
  const nav = page.getByTestId('nav-tree')
  await expect(nav.getByRole('button', { name: /Nhập mua NCC/ })).toBeVisible()
  await expect(nav.getByRole('button', { name: /Nhập – xuất – tồn/ }).first()).toBeVisible()
  await expect(nav.getByRole('button', { name: /Đơn mua hàng/ })).toHaveCount(0)
  await expect(nav.getByRole('button', { name: /Nhà cung cấp/ })).toHaveCount(0)
  // Nhóm không còn màn hình nào thì ẩn hẳn.
  await expect(nav.getByRole('button', { name: 'Mua hàng', exact: true })).toHaveCount(0)
})

test('menu nhóm không có quyền báo rõ, chỉ được chọn công ty được gán', async ({ page }) => {
  const menuBar = page.getByTestId('menu-bar')
  await menuBar.getByRole('button', { name: 'Mua hàng', exact: true }).click()
  await expect(page.getByRole('menuitem', { name: 'Không có màn hình được phép' })).toBeVisible()
  await page.keyboard.press('Escape')

  await menuBar.locator('.app-company-select').click()
  await expect(page.locator('.ant-select-dropdown .ant-select-item-option')).toHaveCount(1)
  await expect(page.locator('.ant-select-dropdown')).toContainText('BHA – Công ty Thép Bắc Hà')
})

test('mở thẳng đường dẫn không có quyền thì bị chặn', async ({ page }) => {
  await page.goto('/mua-hang/po')
  await expect(page.getByText('Bạn không có quyền mở màn hình này')).toBeVisible()
  await expect(page.locator('.data-grid')).toHaveCount(0)
})

test('ô lệnh không mở được màn hình không có quyền', async ({ page }) => {
  const box = page.getByPlaceholder('Ô lệnh – tên màn hình')
  await box.fill('PO')
  await box.press('Enter')
  await expect(page.locator('.ant-message')).toContainText('Không có màn hình "PO"')
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
