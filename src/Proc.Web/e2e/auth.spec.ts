import { expect, test } from '@playwright/test'
import { ADMIN, loginViaApi, appVersion } from './helpers.ts'

/**
 * Test đăng nhập, đổi công ty làm việc, đăng xuất – chạy với API thật (database procurement_test).
 */

test('chưa đăng nhập thì chuyển sang màn hình đăng nhập, đăng nhập xong quay lại trang đang mở', async ({ page }) => {
  await page.goto('/mua-hang/po')
  await expect(page).toHaveURL(/\/dang-nhap$/)
  await expect(page.getByText('Đăng nhập – Quản lý mua hàng và kho')).toBeVisible()
  await expect(page.getByText(`Phiên bản ${appVersion()}`)).toBeVisible()

  await page.getByLabel('Tên đăng nhập').fill(ADMIN.userName)
  await page.getByLabel('Mật khẩu').fill(ADMIN.password)
  await page.getByLabel('Mật khẩu').press('Enter')

  await expect(page).toHaveURL(/\/mua-hang\/po$/)
  await expect(page.getByTestId('shell-bar').getByRole('button', { name: `Người dùng: ${ADMIN.fullName}` })).toBeVisible()
})

test('sai mật khẩu thì hiện thông báo tiếng Việt và ở lại màn hình đăng nhập', async ({ page }) => {
  await page.goto('/dang-nhap')
  await page.getByLabel('Tên đăng nhập').fill(ADMIN.userName)
  await page.getByLabel('Mật khẩu').fill('sai-mat-khau-1')
  await page.getByRole('button', { name: 'Đăng nhập' }).click()

  await expect(page.getByText('Tên đăng nhập hoặc mật khẩu không đúng.')).toBeVisible()
  await expect(page).toHaveURL(/\/dang-nhap$/)

  // Đăng nhập đúng ngay sau đó để đặt lại bộ đếm sai, tránh khóa tài khoản dùng chung của các test khác.
  await page.getByLabel('Mật khẩu').fill(ADMIN.password)
  await page.getByRole('button', { name: 'Đăng nhập' }).click()
  await expect(page.getByTestId('shell-bar')).toBeVisible()
})

test('bỏ trống thì báo ngay trên form, không gọi API', async ({ page }) => {
  await page.goto('/dang-nhap')
  await page.getByRole('button', { name: 'Đăng nhập' }).click()
  await expect(page.getByText('Hãy nhập tên đăng nhập.')).toBeVisible()
  await expect(page.getByText('Hãy nhập mật khẩu.')).toBeVisible()
})

test('đổi công ty làm việc trên Shellbar', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')

  const companySelect = page.getByTestId('shell-bar').locator('.app-company-select')
  await expect(companySelect).toContainText('TCT – Tổng công ty')

  await companySelect.click()
  await page.locator('.ant-select-dropdown').getByText('BHA – Công ty Thép Bắc Hà').click()

  await expect(companySelect).toContainText('BHA – Công ty Thép Bắc Hà')
  await expect(page.locator('.ant-message')).toContainText('Đang làm việc tại BHA')

  // Tải lại trang vẫn giữ công ty đang chọn.
  await page.reload()
  await expect(companySelect).toContainText('BHA – Công ty Thép Bắc Hà')
})

test('đăng xuất thì về màn hình đăng nhập và không vào lại được bằng nút Quay lại', async ({ page }) => {
  await loginViaApi(page)
  // Mở Trang chủ rồi sang PO bằng cây điều hướng để lịch sử trình duyệt có trang trước đó.
  await page.goto('/')
  await page.getByTestId('nav-tree').getByRole('button', { name: /Đơn mua hàng/ }).first().click()
  await expect(page).toHaveURL(/\/mua-hang\/po$/)

  await page.getByRole('button', { name: `Người dùng: ${ADMIN.fullName}` }).click()
  await page.getByRole('menuitem', { name: 'Đăng xuất' }).click()

  await expect(page).toHaveURL(/\/dang-nhap$/)
  await page.goBack()
  await expect(page).toHaveURL(/\/dang-nhap$/)
})

test('chụp màn hình đăng nhập', async ({ page }) => {
  await page.goto('/dang-nhap')
  await expect(page.getByRole('button', { name: 'Đăng nhập' })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/login.png' })
})
