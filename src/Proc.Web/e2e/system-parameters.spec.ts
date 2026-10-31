import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'

/**
 * Màn hình Tham số hệ thống (SEC – design.md mục 5.4). Dùng công ty DTV (test đấu thầu chạy ở BHA nên không bị ảnh hưởng);
 * test tự trả tham số về mặc định ở cuối nên chạy lại được nhiều lần.
 */
test('xem tham số công ty, sửa số NCC tối thiểu khi gửi RFQ, rồi về mặc định', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await page.getByTestId('shell-bar').locator('.app-company-select').click()
  await page.locator('.ant-select-dropdown .ant-select-item-option', { hasText: 'DTV' }).click()
  await page.getByTestId('nav-tree').getByRole('button', { name: 'Tham số hệ thống' }).click()

  await expect(page.getByRole('heading', { name: 'Tham số hệ thống – DTV' })).toBeVisible()
  await expect(page.locator('.data-grid-row')).toHaveCount(3)
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
})
