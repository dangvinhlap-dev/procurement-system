import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'

/**
 * Màn hình Đánh số chứng từ (design.md mục 5.4, 5.5). Dùng công ty DTV và loại RTV để không lẫn với test khác;
 * test tự trả về mẫu mặc định ở cuối nên chạy lại được nhiều lần.
 */
test('xem mẫu số mặc định, sửa mẫu có xem trước, rồi về mặc định', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await page.getByTestId('shell-bar').locator('.app-company-select').click()
  await page.locator('.ant-select-dropdown .ant-select-item-option', { hasText: 'DTV' }).click()
  await page.getByTestId('nav-tree').getByRole('button', { name: 'Đánh số chứng từ' }).click()

  await expect(page.getByRole('heading', { name: 'Đánh số chứng từ – DTV' })).toBeVisible()
  await expect(page.locator('.data-grid-row')).toHaveCount(12)
  // Năm/tháng theo giờ máy (Việt Nam), giống server tính kỳ theo giờ Việt Nam.
  const now = new Date()
  const yymm = String(now.getFullYear()).slice(2) + String(now.getMonth() + 1).padStart(2, '0')
  const po = page.locator('.data-grid-row', { hasText: 'Đơn mua hàng' })
  await expect(po).toContainText('PO-{CTY}-{YYMM}-{0000}')
  await expect(po).toContainText(`PO-DTV-${yymm}-`)

  const rtv = page.locator('.data-grid-row', { hasText: 'Phiếu xuất trả NCC' })
  await rtv.dblclick()
  const drawer = page.locator('.detail-drawer')
  const input = drawer.getByRole('textbox', { name: 'Mẫu số' })
  await input.fill('th/{yyyy}/{000}')
  await expect(drawer.getByText(`TH/${new Date().getFullYear()}/001`)).toBeVisible()

  // Mẫu sai: server báo lỗi tiếng Việt, ngăn kéo vẫn mở.
  await input.fill('TH-{MM}-{000}')
  await drawer.getByRole('button', { name: 'Lưu' }).click()
  await expect(drawer.getByText(/phải có năm/)).toBeVisible()

  await input.fill('th/{yyyy}/{000}')
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText('Đã lưu mẫu số RTV: TH/{YYYY}/{000}.')
  await expect(rtv).toContainText('Riêng')
  await expect(rtv).toContainText(`TH/${new Date().getFullYear()}/`)

  await rtv.dblclick()
  await drawer.getByRole('button', { name: 'Về mẫu mặc định' }).click()
  await expect(rtv).toContainText('Mặc định')
  await expect(rtv).toContainText('RTV-{CTY}-{YYMM}-{0000}')
})
