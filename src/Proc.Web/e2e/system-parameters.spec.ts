import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'

/**
 * Màn hình Tham số hệ thống (SEC – design.md mục 5.4). Dùng công ty DTV (test đấu thầu chạy ở BHA nên không bị ảnh hưởng);
 * test tự trả tham số về mặc định ở cuối nên chạy lại được nhiều lần.
 */
test('xem tham số công ty, sửa số NCC tối thiểu khi gửi RFQ và % markup nội bộ, rồi về mặc định', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await page.getByTestId('shell-bar').locator('.app-company-select').click()
  await page.locator('.ant-select-dropdown .ant-select-item-option', { hasText: 'DTV' }).click()
  await page.getByTestId('nav-tree').getByRole('button', { name: 'Tham số hệ thống' }).click()

  await expect(page.getByRole('heading', { name: 'Tham số hệ thống – DTV' })).toBeVisible()
  // 3 tham số sửa được (thêm % cảnh báo giá cao – mục 8.5) + 1 bật/tắt hồ sơ trình (mục 8.6) + 2 bật/tắt nhận hàng (mục 18.14)
  // + 1 bật/tắt nhập mua theo PO (lát R4 – mục 18.26) + 3 SLA mua hàng (mục 18.13) + 2 chỉ xem + 10 tham số KPI (mục 18.18)
  // + 1 % biên độ phân bổ đặt hàng gộp (lát G7 – mục 18.25) + 1 bật/tắt duyệt NCC mới không qua hồ sơ HSN (lát G8 – mục 18.25)
  // + 1 ngưỡng so sánh 3 NCC loại A (lát Y6 – mục 19.3, mặc định 0).
  await expect(page.locator('.data-grid-row')).toHaveCount(25)
  await expect(page.locator('.data-grid-row', { hasText: 'Ngưỡng so sánh 3 NCC' })).toContainText('0')
  await expect(page.locator('.data-grid-row', { hasText: '% biên độ phân bổ đặt hàng gộp' })).toContainText('10')
  await expect(page.locator('.data-grid-row', { hasText: 'Nhập mua bắt buộc theo đơn mua hàng' })).toContainText('Tắt')
  await expect(page.locator('.data-grid-row', { hasText: 'Bắt buộc đủ hồ sơ trình khi chọn thầu' })).toContainText('Tắt')
  await expect(page.locator('.data-grid-row', { hasText: 'Mục tiêu KPI – Hàng đạt chất lượng (≥)' })).toContainText('98')
  await expect(page.locator('.data-grid-row', { hasText: 'Bắt buộc kiểm tra chất lượng khi nhận hàng' })).toContainText('Tắt')
  await expect(page.locator('.data-grid-row', { hasText: 'SLA – hàng chưa có NCC được duyệt' })).toContainText('5')
  await expect(page.locator('.data-grid-row', { hasText: 'Phương pháp giá vốn' })).toContainText('Chỉ xem')
  const rfq = page.locator('.data-grid-row', { hasText: 'Số NCC tối thiểu khi gửi RFQ' })
  await expect(rfq).toContainText('Mặc định')
  await page.waitForFunction("document.getAnimations().every((a) => a.playState !== 'running')")
  await page.screenshot({ path: 'e2e/screenshots/system-parameters.png' })

  // Tham số chỉ xem: ngăn kéo không có nút Lưu.
  await page.locator('.data-grid-row', { hasText: 'Phương pháp giá vốn' }).dblclick()
  const drawer = page.locator('.detail-drawer')
  await expect(drawer.getByText('Bình quân gia quyền cuối kỳ tháng')).toBeVisible()
  await expect(drawer.getByRole('button', { name: 'Lưu' })).toHaveCount(0)
  await page.keyboard.press('Escape')

  await rfq.dblclick()
  const input = drawer.getByRole('spinbutton')
  await input.fill('2')
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText('Đã lưu "Số NCC tối thiểu khi gửi RFQ": 2.')
  await expect(rfq).toContainText('Riêng')

  await rfq.dblclick()
  await drawer.getByRole('button', { name: 'Về mặc định' }).click()
  await expect(rfq).toContainText('Mặc định')
  await expect(rfq).toContainText('3')

  // % markup nội bộ mặc định (SEC-2): gõ số lẻ kiểu Việt Nam, rồi về mặc định 0.
  const markup = page.locator('.data-grid-row', { hasText: '% markup nội bộ mặc định' })
  await expect(markup).toContainText('Mặc định')
  await markup.dblclick()
  await input.fill('12,5')
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText('Đã lưu "% markup nội bộ mặc định": 12,5.')
  await expect(markup).toContainText('Riêng')
  await markup.dblclick()
  await drawer.getByRole('button', { name: 'Về mặc định' }).click()
  await expect(markup).toContainText('Mặc định')
})
