import { expect, test, type Locator } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand } from './inventoryHelpers.ts'

/** Nhãn ô nhập (Form.Item) theo chữ hiển thị (dấu * vẽ bằng CSS nên không nằm trong chữ). */
function formLabel(scope: Locator, text: string) {
  return scope.locator('.ant-form-item-label label').filter({ hasText: new RegExp(`^${text}$`) })
}

/**
 * Ô bắt buộc có dấu * đỏ sau nhãn (docs/ui-guide.md mục 6): trên form chứng từ, tiêu đề cột lưới dòng và ngăn kéo danh mục;
 * ô không bắt buộc thì không có.
 */
test('ô bắt buộc có dấu * đỏ: phiếu PO mới, lưới dòng, ngăn kéo NCC', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/mua-hang/po/moi')

  const form = page.locator('.document-form-content')
  await expect(formLabel(form, 'Nhà cung cấp').locator('.required-mark')).toBeVisible()
  await expect(formLabel(form, 'Kho nhận').locator('.required-mark')).toBeVisible()
  await expect(formLabel(form, 'Ghi chú').locator('.required-mark')).toHaveCount(0)
  // Màu dấu * là màu lỗi của theme Horizon (#b00e12). Đọc kiểu của ::after trong trình duyệt (chuỗi vì tsconfig e2e không có lib DOM).
  const mark = (await page.evaluate(
    "(() => { const a = getComputedStyle(document.querySelector('.document-form-content .required-mark'), '::after'); return { content: a.content, color: a.color } })()",
  )) as { content: string; color: string }
  expect(mark.color).toBe('rgb(176, 14, 18)')
  expect(mark.content).toContain('*')

  const header = page.locator('.line-grid thead')
  await expect(header.locator('th', { hasText: 'Mặt hàng' }).locator('.required-mark')).toBeVisible()
  await expect(header.locator('th', { hasText: 'SL đặt' }).locator('.required-mark')).toBeVisible()
  await expect(header.locator('th', { hasText: 'Thành tiền' }).locator('.required-mark')).toHaveCount(0)
  await page.waitForFunction("document.getAnimations().every((a) => a.playState !== 'running')")
  await page.screenshot({ path: 'e2e/screenshots/required-marks-po.png' })

  await page.goto('/')
  await openByCommand(page, 'SUP')
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  const drawer = page.locator('.detail-drawer')
  await expect(formLabel(drawer, 'Tên nhà cung cấp').locator('.required-mark')).toBeVisible()
  await expect(formLabel(drawer, 'Trạng thái').locator('.required-mark')).toBeVisible()
  await expect(formLabel(drawer, 'Điện thoại').first().locator('.required-mark')).toHaveCount(0)
  await page.waitForFunction("document.getAnimations().every((a) => a.playState !== 'running')")
  await page.screenshot({ path: 'e2e/screenshots/required-marks-drawer.png' })
})
