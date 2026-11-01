import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'

/**
 * BẢN THỬ giao diện ERP desktop cổ điển (docs/ui-style.md): màn hình Tạo đơn mua hàng dữ liệu mẫu.
 * Chụp ảnh để người dùng duyệt và kiểm tra các thao tác bàn phím chính.
 */
test.describe('Bản thử giao diện ERP cổ điển – Tạo đơn mua hàng', () => {
  test.beforeEach(async ({ page }) => {
    await loginViaApi(page)
    await page.goto('/thu-giao-dien/tao-po')
    await expect(page.getByRole('heading', { name: 'Tạo đơn mua hàng' })).toBeVisible()
  })

  test('đủ các vùng bố cục và chụp ảnh', async ({ page }) => {
    for (const label of ['Phiếu', 'Sửa', 'Hệ thống']) {
      await expect(page.getByRole('navigation', { name: 'Menu chính' }).getByRole('button', { name: label })).toBeVisible()
    }
    await expect(page.getByRole('button', { name: 'Lưu (Ctrl+S)' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Tổng quan chứng từ' })).toBeVisible()
    await expect(page.getByLabel('Nhà cung cấp')).toBeVisible()
    await expect(page.getByRole('tab', { name: 'Điều kiện giá' })).toHaveAttribute('aria-selected', 'true')
    // Dòng 30 thiếu đơn giá → tam giác cảnh báo.
    await expect(page.getByRole('img', { name: 'Chưa có đơn giá' })).toBeVisible()
    // Ngày hiển thị dd/MM/yyyy (không theo ngôn ngữ hệ điều hành); gõ nhanh "ddMMyyyy" vẫn hiểu.
    const due = page.getByLabel('Ngày giao dòng 1')
    await expect(due).toHaveValue(/^\d{2}\/\d{2}\/\d{4}$/)
    await due.fill('05122026')
    await due.press('Tab')
    await expect(due).toHaveValue('05/12/2026')
    await page.screenshot({ path: 'e2e/screenshots/classic-create-po.png', animations: 'disabled' })
  })

  test('Ctrl+S báo dòng lỗi; nhập đơn giá xong thì hợp lệ', async ({ page }) => {
    await page.keyboard.press('Control+s')
    await expect(page.getByRole('status')).toContainText('Dòng 30: Chưa có đơn giá.')

    const price = page.getByLabel('Đơn giá dòng 3')
    await price.fill('45.200')
    await price.press('Enter')
    // Enter chuyển xuống ô Đơn giá dòng dưới.
    await expect(page.getByLabel('Đơn giá dòng 4')).toBeFocused()
    await expect(price).toHaveValue('45.200')

    await page.keyboard.press('Control+s')
    await expect(page.getByRole('status')).toContainText('Đơn mua hàng hợp lệ')
  })

  test('chọn tab bằng mũi tên và thêm dòng bằng F2', async ({ page }) => {
    await page.getByRole('tab', { name: 'Điều kiện giá' }).focus()
    await page.keyboard.press('ArrowRight')
    await expect(page.getByRole('tab', { name: 'Ghi chú' })).toHaveAttribute('aria-selected', 'true')

    await expect(page.getByLabel(/^Mã hàng dòng/)).toHaveCount(6)
    await page.keyboard.press('F2')
    await expect(page.getByLabel(/^Mã hàng dòng/)).toHaveCount(7)
  })
})
