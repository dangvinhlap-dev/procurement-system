import { expect, test } from '@playwright/test'
import { loginViaApi, confirmCompanySwitch } from './helpers.ts'

/**
 * Màn hình Đánh số chứng từ (design.md mục 5.4, 5.5). Dùng công ty DTV và loại KSG (khảo sát giá) để không lẫn với test khác;
 * test tự trả về mẫu mặc định ở cuối nên chạy lại được nhiều lần.
 */
test('xem mẫu số mặc định, sửa mẫu có xem trước, rồi về mặc định', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await page.getByTestId('shell-bar').locator('.app-company-select').click()
  await page.locator('.ant-select-dropdown .ant-select-item-option', { hasText: 'DTV' }).click()
  await confirmCompanySwitch(page)
  await page.getByTestId('nav-tree').getByRole('button', { name: 'Đánh số chứng từ' }).click()

  await expect(page.getByRole('heading', { name: 'Đánh số chứng từ – DTV' })).toBeVisible()
  // 26 loại – thêm HSN hồ sơ đánh giá NCC mới (lát G8), XNH xác nhận nhận hàng (lát K2), BTH bảng tổng hợp đề xuất mua (lát Y5b),
  // BGH báo giao hàng NCC (lát P5), DNT đề nghị thanh toán (lát Y8), BKT bảng kê chi phí tạp vụ (lát Y11a); bỏ 6 loại phiếu kho (lát K3).
  await expect(page.locator('.data-grid-row')).toHaveCount(20)
  // Năm/tháng theo giờ máy (Việt Nam), giống server tính kỳ theo giờ Việt Nam.
  const now = new Date()
  const yymm = String(now.getFullYear()).slice(2) + String(now.getMonth() + 1).padStart(2, '0')
  const po = page.locator('.data-grid-row', { hasText: 'Đơn mua hàng' })
  await expect(po).toContainText('PO-{CTY}-{YYMM}-{0000}')
  await expect(po).toContainText(`PO-DTV-${yymm}-`)

  const ksg = page.locator('.data-grid-row', { hasText: 'Khảo sát giá thị trường' })
  await ksg.dblclick()
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
  await expect(page.locator('.ant-message')).toContainText('Đã lưu mẫu số KSG: TH/{YYYY}/{000}.')
  await expect(ksg).toContainText('Riêng')
  await expect(ksg).toContainText(`TH/${new Date().getFullYear()}/`)

  await ksg.dblclick()
  await drawer.getByRole('button', { name: 'Về mẫu mặc định' }).click()
  await expect(ksg).toContainText('Mặc định')
  await expect(ksg).toContainText('KSG-{CTY}-{YYMM}-{0000}')
})
