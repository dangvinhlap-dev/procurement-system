import { expect, test } from '@playwright/test'
import { loginViaApi, confirmCompanySwitch } from './helpers.ts'

/**
 * Màn hình Tham số hệ thống (SEC – design.md mục 5.4). Dùng công ty DTV (test đấu thầu chạy ở BHA nên không bị ảnh hưởng);
 * test tự trả tham số về mặc định ở cuối nên chạy lại được nhiều lần.
 */
test('xem tham số công ty, sửa số NCC tối thiểu khi gửi RFQ và % cảnh báo giá cao, rồi về mặc định', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await page.getByTestId('shell-bar').locator('.app-company-select').click()
  await page.locator('.ant-select-dropdown .ant-select-item-option', { hasText: 'DTV' }).click()
  await confirmCompanySwitch(page)
  await page.getByTestId('nav-tree').getByRole('button', { name: 'Tham số hệ thống' }).click()

  await expect(page.getByRole('heading', { name: 'Tham số hệ thống – DTV' })).toBeVisible()
  // 2 tham số sửa được (số NCC tối thiểu, % cảnh báo giá cao – mục 8.5) + 1 bật/tắt hồ sơ trình (mục 8.6) + 2 bật/tắt nhận hàng (mục 18.14)
  // + 3 SLA mua hàng (mục 18.13) + 8 mục tiêu KPI (mục 18.18) + 1 % biên độ phân bổ đặt hàng gộp (lát G7 – mục 18.25) + 1 bật/tắt duyệt NCC
  // mới không qua hồ sơ HSN (lát G8) + 1 ngưỡng so sánh 3 NCC loại A (lát Y6, mặc định 0). Lát K3 bỏ % markup nội bộ, nhập mua theo PO,
  // 2 tham số kho chỉ xem, 2 tham số KPI tồn chậm.
  await expect(page.locator('.data-grid-row')).toHaveCount(19)
  await expect(page.locator('.data-grid-row', { hasText: 'Ngưỡng so sánh 3 NCC' })).toContainText('0')
  await expect(page.locator('.data-grid-row', { hasText: '% biên độ phân bổ đặt hàng gộp' })).toContainText('10')
  await expect(page.locator('.data-grid-row', { hasText: 'Nhập mua bắt buộc theo đơn mua hàng' })).toHaveCount(0)
  await expect(page.locator('.data-grid-row', { hasText: 'Bắt buộc đủ hồ sơ trình khi chọn thầu' })).toContainText('Tắt')
  await expect(page.locator('.data-grid-row', { hasText: 'Mục tiêu KPI – Hàng đạt chất lượng (≥)' })).toContainText('98')
  await expect(page.locator('.data-grid-row', { hasText: 'Bắt buộc kiểm tra chất lượng khi nhận hàng' })).toContainText('Tắt')
  await expect(page.locator('.data-grid-row', { hasText: 'SLA – hàng chưa có NCC được duyệt' })).toContainText('5')
  // Tham số kho (giá vốn, tồn âm) gỡ ở lát K3.
  await expect(page.locator('.data-grid-row', { hasText: 'Phương pháp giá vốn' })).toHaveCount(0)
  const rfq = page.locator('.data-grid-row', { hasText: 'Số NCC tối thiểu khi gửi RFQ' })
  await expect(rfq).toContainText('Mặc định')
  await page.waitForFunction("document.getAnimations().every((a) => a.playState !== 'running')")
  await page.screenshot({ path: 'e2e/screenshots/system-parameters.png' })

  const drawer = page.locator('.detail-drawer')

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

  // % cảnh báo giá cao trên PO (mục 8.5): gõ số lẻ kiểu Việt Nam, rồi về mặc định.
  const markup = page.locator('.data-grid-row', { hasText: '% cảnh báo giá cao trên PO' })
  await expect(markup).toContainText('Mặc định')
  await markup.dblclick()
  await input.fill('12,5')
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText('Đã lưu "% cảnh báo giá cao trên PO": 12,5.')
  await expect(markup).toContainText('Riêng')
  await markup.dblclick()
  await drawer.getByRole('button', { name: 'Về mặc định' }).click()
  await expect(markup).toContainText('Mặc định')
})
