import { readFileSync } from 'node:fs'
import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand } from './inventoryHelpers.ts'

/**
 * Báo cáo tháng cho Ban (BCBAN – lát G12, design.md mục 18.25): quản trị mở màn hình, chọn tháng hiện tại bằng cách gõ MM/yyyy; lưới chỉ tiêu
 * có đủ các mục (quản trị có quyền KSNB nên thấy cả mục kiểm soát nội bộ); bấm đúp mở chi tiết mục KPI (cùng cột màn hình KPI); tải gói Excel.
 * Số liệu từng mục khớp báo cáo chi tiết kiểm tra ở test tích hợp BoardMonthlyReportTests.
 */
test('báo cáo tháng cho Ban: chọn tháng, xem chi tiết mục, xuất gói Excel, chụp màn hình', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'BCBAN')
  await expect(page.getByRole('heading', { name: 'Báo cáo tháng cho Ban' })).toBeVisible()

  const now = new Date()
  const month = `${String(now.getMonth() + 1).padStart(2, '0')}/${now.getFullYear()}`
  const monthInput = page.getByLabel('Tháng', { exact: true })
  await monthInput.click()
  await monthInput.fill(month)
  await monthInput.press('Enter')
  await expect(page.locator('.ant-alert')).toContainText(`Tháng ${month}`)
  await expect(page.locator('.ant-alert')).toContainText('công ty TCT')

  const rows = page.locator('.data-grid-row')
  for (const label of [
    'Tỷ lệ mua theo giá đã duyệt',
    'Số KPI đạt mục tiêu',
    'Tiết kiệm so giá chuẩn',
    'Số dòng mua ngoài giá đã duyệt',
    'Ảnh hưởng biến động giá',
    'Thỏa thuận chiết khấu đang theo dõi',
    'NCC đã duyệt',
    'Hồ sơ đánh giá NCC mới',
    'Vi phạm ghi nhận trong tháng',
  ])
    await expect(rows.filter({ hasText: label })).toHaveCount(1)

  // Bấm đúp mục KPI: ngăn kéo có 9 chỉ số, nút mở báo cáo KPI.
  await rows.filter({ hasText: 'Số KPI đạt mục tiêu' }).dblclick()
  const drawer = page.locator('.detail-drawer')
  await expect(drawer.locator('.detail-drawer-title')).toContainText(`KPI cung ứng – tháng ${month}`)
  await expect(drawer.locator('.data-grid-row')).toHaveCount(9)
  await expect(drawer.getByRole('button', { name: 'Mở báo cáo KPI' })).toBeVisible()
  await page.waitForFunction("document.getAnimations().every((a) => a.playState !== 'running')")
  await page.screenshot({ path: 'e2e/screenshots/board-monthly-kpi.png' })
  await page.keyboard.press('Escape')
  await expect(drawer).toHaveCount(0)

  await page.mouse.move(0, 0)
  await page.waitForFunction("document.getAnimations().every((a) => a.playState !== 'running')")
  await page.screenshot({ path: 'e2e/screenshots/board-monthly.png' })

  const downloading = page.waitForEvent('download')
  await page.getByRole('button', { name: 'Xuất gói Excel' }).click()
  const download = await downloading
  expect(download.suggestedFilename()).toBe(`BaoCaoThangBan_${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}_TCT.xlsx`)
  // File .xlsx là gói zip: bắt đầu bằng "PK".
  expect(readFileSync(await download.path()).subarray(0, 2).toString()).toBe('PK')
})
