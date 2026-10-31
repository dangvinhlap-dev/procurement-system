import { readFileSync } from 'node:fs'
import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand, seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * Xuất chứng từ cho kế toán (design.md mục 11.5): BHA vừa ghi sổ phiếu nhập → màn hình xem trước có phiếu đó; tải file Excel cho kế toán.
 */
test('xuất chứng từ đã ghi sổ ra Excel cho kế toán', async ({ page, request }) => {
  const { receiptNo } = await seedPostedReceipt(request)
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'ACCX')
  await expect(page.getByRole('heading', { name: 'Xuất chứng từ cho kế toán' })).toBeVisible()
  const row = page.locator('.data-grid-row', { hasText: receiptNo })
  await expect(row).toContainText('Nhập mua NCC')
  await expect(row).toContainText('500.000')
  await expect(page.locator('.ant-spin-spinning')).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Tải file Excel cho kế toán' })).toBeEnabled()
  await page.mouse.move(0, 0)
  // Chờ hết hiệu ứng mờ dần của khung đang tải để ảnh chụp không bị nhạt.
  await page.waitForFunction("document.getAnimations().every((a) => a.playState !== 'running')")
  await page.screenshot({ path: 'e2e/screenshots/accounting-export.png' })

  const downloading = page.waitForEvent('download')
  await page.getByRole('button', { name: 'Tải file Excel cho kế toán' }).click()
  const download = await downloading
  expect(download.suggestedFilename()).toMatch(/^chung-tu-ke-toan-bha-\d{8}-\d{8}\.xlsx$/)
  // File .xlsx là gói zip: bắt đầu bằng "PK".
  expect(readFileSync(await download.path()).subarray(0, 2).toString()).toBe('PK')
})
