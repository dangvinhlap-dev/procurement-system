import { readFileSync } from 'node:fs'
import { expect, test, type Page } from '@playwright/test'
import { loginViaApi } from './helpers.ts'

/**
 * Nhập/xuất Excel (design.md mục 5.10): xuất danh mục ra file, nhập lại chính file đó (kiểm tra thử rồi nhập thật);
 * file không phải Excel thì báo lỗi tiếng Việt.
 */

async function openByCommand(page: Page, code: string) {
  const box = page.getByPlaceholder('Ô lệnh – tên màn hình')
  await box.fill(code)
  await box.press('Enter')
}

test.beforeEach(async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
})

test('xuất đơn vị tính ra Excel rồi nhập lại: kiểm tra thử, nhập thật', async ({ page }) => {
  await openByCommand(page, 'UOM')
  await expect(page.locator('.data-grid-row', { hasText: 'Cái' })).toBeVisible()

  const downloading = page.waitForEvent('download')
  await page.getByRole('button', { name: 'Xuất Excel' }).click()
  const download = await downloading
  expect(download.suggestedFilename()).toBe('don-vi-tinh.xlsx')
  const path = await download.path()

  await page.getByRole('button', { name: 'Nhập Excel' }).click()
  const dialog = page.getByRole('dialog', { name: 'Nhập đơn vị tính từ Excel' })
  await expect(dialog.getByRole('button', { name: 'Nhập thật' })).toBeDisabled()
  await dialog.locator('input[type=file]').setInputFiles({
    name: 'don-vi-tinh.xlsx',
    mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    buffer: readFileSync(path),
  })
  await dialog.getByRole('button', { name: 'Kiểm tra thử' }).click()
  // File vừa xuất: mọi mã đã có nên toàn bộ là cập nhật, không có dòng mới.
  await expect(dialog.getByText(/File hợp lệ: \d+ dòng \(0 dòng mới, \d+ dòng cập nhật\)/)).toBeVisible()
  await expect(dialog.getByRole('button', { name: 'Nhập thật' })).toBeEnabled()
  await page.screenshot({ path: 'e2e/screenshots/excel-import.png' })

  await dialog.getByRole('button', { name: 'Nhập thật' }).click()
  await expect(dialog.getByText(/Đã nhập xong \d+ dòng/)).toBeVisible()
  await dialog.locator('.ant-modal-footer').getByRole('button', { name: 'Đóng' }).click()
  await expect(dialog).toHaveCount(0)
})

test('nhập file không phải Excel thì báo lỗi', async ({ page }) => {
  await openByCommand(page, 'ITEM')
  await page.getByRole('button', { name: 'Nhập Excel' }).click()
  const dialog = page.getByRole('dialog', { name: 'Nhập hàng hóa từ Excel' })
  await dialog.locator('input[type=file]').setInputFiles({ name: 'sai.xlsx', mimeType: 'application/octet-stream', buffer: Buffer.from('không phải excel') })
  await dialog.getByRole('button', { name: 'Kiểm tra thử' }).click()
  await expect(dialog.getByText(/Có 1 lỗi – chưa lưu dòng nào/)).toBeVisible()
  await expect(dialog.locator('.data-grid-row', { hasText: 'Không đọc được file' })).toBeVisible()
})
