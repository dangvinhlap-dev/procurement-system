import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'

/** Màn hình của menu "Quản trị hệ thống" (người dùng yêu cầu 15/11/2026 – tách khỏi menu "Hệ thống"). */
const adminScreens = [
  'Người dùng',
  'Vai trò',
  'Nhật ký thao tác',
  'Nhật ký truy cập',
  'Tổng quan hệ thống',
  'Nhật ký vận hành',
  'Nhật ký lỗi',
  'Sao lưu',
  'Bảo trì CSDL',
]

/**
 * Menu "Quản trị hệ thống" (Alt+Q) gom người dùng, vai trò, nhật ký, module Quản trị vận hành; menu "Hệ thống" chỉ còn cấu hình tổ chức và
 * nghiệp vụ. Cây điều hướng có nhóm riêng cùng tên.
 */
test('menu Quản trị hệ thống tách khỏi menu Hệ thống', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  const shellBar = page.getByTestId('shell-bar')

  await shellBar.getByRole('menuitem', { name: 'Quản trị hệ thống', exact: true }).click()
  const adminMenu = page.locator('.ant-dropdown:visible')
  for (const name of adminScreens) await expect(adminMenu.getByText(new RegExp(`^${name}\\s+\\(`))).toBeVisible()
  await expect(adminMenu.getByText(/^Tham số hệ thống/)).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/admin-menu.png' })
  await page.keyboard.press('Escape')

  await shellBar.getByRole('menuitem', { name: 'Hệ thống', exact: true }).click()
  const systemMenu = page.locator('.ant-dropdown:visible')
  await expect(systemMenu.getByText(/^Tham số hệ thống/)).toBeVisible()
  await expect(systemMenu.getByText(/^Công ty\s+\(/)).toBeVisible()
  for (const name of adminScreens) await expect(systemMenu.getByText(new RegExp(`^${name}\\s+\\(`))).toHaveCount(0)
  await page.keyboard.press('Escape')

  // Mở màn hình từ menu mới: đường dẫn giữ /he-thong/… như trước.
  await shellBar.getByRole('menuitem', { name: 'Quản trị hệ thống', exact: true }).click()
  await page.locator('.ant-dropdown:visible').getByText(/^Bảo trì CSDL\s+\(/).click()
  await expect(page).toHaveURL(/\/he-thong\/bao-tri-csdl$/)
  await expect(page.getByTestId('nav-tree')).toContainText('Quản trị hệ thống')
})
