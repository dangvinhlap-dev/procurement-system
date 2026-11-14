import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand, switchCompany } from './inventoryHelpers.ts'

/**
 * Bảng KPI cung ứng (lát F2, design.md mục 18.18): đủ 8 chỉ số đều đã đo (KPI tồn chậm luân chuyển bỏ cùng phân hệ kho – lát K3) (đánh giá NCC từ lát F4a), bấm đúp mở báo cáo chi tiết,
 * nút "Mục tiêu KPI" mở Tham số hệ thống có nhóm KPI.
 */
test('bảng KPI: 8 chỉ số, mở báo cáo chi tiết và mục tiêu', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'KPI')
  await expect(page.getByRole('heading', { name: 'Bảng KPI cung ứng' })).toBeVisible()
  await expect(page.getByText(/Đạt \d+\/\d+ chỉ số có kết quả; so với kỳ trước từ \d{2}\/\d{2}\/\d{4}/)).toBeVisible()
  await expect(page.locator('.data-grid-row')).toHaveCount(8)
  const review = page.locator('.data-grid-row', { hasText: 'Hoàn thành đánh giá NCC' })
  await expect(review).toContainText('≥ 100%')
  await expect(review).not.toContainText('Chưa đo')
  await expect(page.locator('.data-grid-row', { hasText: 'Vi phạm nghiêm trọng' })).toContainText('≤ 0 vụ')
  await expect(page.locator('.data-grid-row', { hasText: 'Hàng đạt chất lượng' })).toContainText('≥ 98%')
  await page.screenshot({ path: 'e2e/screenshots/kpi.png' })

  await page.locator('.data-grid-row', { hasText: 'Mua theo giá/hợp đồng đã duyệt' }).dblclick()
  await expect(page.getByRole('heading', { name: 'Giá mua & mua ngoài hợp đồng' })).toBeVisible()

  await openByCommand(page, 'KPI')
  await page.getByRole('button', { name: 'Mục tiêu KPI' }).click()
  await expect(page.getByRole('heading', { name: 'Tham số hệ thống' })).toBeVisible()
  await expect(page.getByText('Mục tiêu KPI – Mua theo giá/hợp đồng đã duyệt (≥)')).toBeVisible()
})
