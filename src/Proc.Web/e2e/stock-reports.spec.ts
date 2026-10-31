import { readFileSync } from 'node:fs'
import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { chooseOption, openByCommand, seedPostedReceipt } from './inventoryHelpers.ts'

/**
 * Báo cáo kho còn lại (design.md mục 11.2): tồn tập đoàn (BHA vừa nhập 10 hộp → tổng công ty thấy BHA có thể bán nội bộ), xuất Excel
 * lưới đang xem, hàng chậm luân chuyển, hàng sắp hết hạn.
 */

test('tồn tập đoàn: thấy tồn của công ty khác, xuất Excel', async ({ page, request }) => {
  const { itemCode } = await seedPostedReceipt(request)
  await loginViaApi(page)
  await page.goto('/')

  await openByCommand(page, 'GSTK')
  await expect(page.getByRole('heading', { name: 'Tồn tập đoàn' })).toBeVisible()
  await expect(page.getByRole('alert')).toContainText('Chọn mặt hàng')
  await chooseOption(page, 'Mặt hàng', itemCode)
  const row = page.locator('.data-grid-row', { hasText: 'BHA' })
  await expect(row).toContainText('10')
  await expect(row).toContainText('Có thể mua nội bộ')
  await expect(page.getByRole('alert')).toContainText('công ty khác còn 10 ở kho Main')
  // Bấm ra ngoài để đóng danh sách thả xuống của ô Mặt hàng trước khi chụp.
  await page.getByRole('heading', { name: 'Tồn tập đoàn' }).click()
  await expect(page.locator('.ant-select-dropdown:visible')).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/group-stock.png' })

  const downloading = page.waitForEvent('download')
  await page.getByRole('button', { name: 'Xuất Excel' }).click()
  const download = await downloading
  expect(download.suggestedFilename()).toBe('ton-tap-doan.xlsx')
  // File .xlsx là gói zip: bắt đầu bằng "PK".
  expect(readFileSync(await download.path()).subarray(0, 2).toString()).toBe('PK')
})

test('hàng chậm luân chuyển và sắp hết hạn: mở được, đổi số ngày', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')

  await openByCommand(page, 'SLOW')
  await expect(page.getByRole('heading', { name: 'Hàng chậm luân chuyển' })).toBeVisible()
  await expect(page.getByRole('alert')).toContainText('Tổng giá trị tồn chậm luân chuyển')
  await expect(page.getByLabel('Không xuất từ (ngày)')).toHaveValue('90')
  await page.getByLabel('Không xuất từ (ngày)').fill('30')
  await page.getByLabel('Không xuất từ (ngày)').press('Tab')
  await expect(page.getByLabel('Không xuất từ (ngày)')).toHaveValue('30')
  await page.screenshot({ path: 'e2e/screenshots/slow-moving.png' })

  await openByCommand(page, 'EXP')
  await expect(page.getByRole('heading', { name: 'Hàng sắp hết hạn' })).toBeVisible()
  await expect(page.getByRole('alert')).toContainText('lô đã hết hạn')
  await expect(page.getByLabel('Trong (ngày tới)')).toHaveValue('30')
  await page.screenshot({ path: 'e2e/screenshots/expiring.png' })
})
