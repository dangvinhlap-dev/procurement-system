import { readFileSync } from 'node:fs'
import { expect, test, type Page } from '@playwright/test'
import { loginViaApi, openInCurrentTab } from './helpers.ts'

/**
 * Nhập/xuất Excel (design.md mục 5.10): xuất danh mục ra file, nhập lại chính file đó (kiểm tra thử rồi nhập thật);
 * file không phải Excel thì báo lỗi tiếng Việt.
 */

async function openByCommand(page: Page, code: string) {
  await openInCurrentTab(page, code)
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
  await dialog.locator('.ant-modal-footer').getByRole('button', { name: /(^|\s)Đóng$/ }).click()
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

test('xuất lưới ra Excel: cột số chứng từ và màu đèn trạng thái gửi kèm để file giống lưới (ui-style.md mục 5i)', async ({ page }) => {
  await openByCommand(page, 'DOCS')
  await expect(page.getByRole('columnheader', { name: 'Số chứng từ' })).toBeVisible()
  const exporting = page.waitForRequest((r) => r.url().includes('/api/excel/grid') && r.method() === 'POST')
  const downloading = page.waitForEvent('download')
  await page.getByRole('button', { name: 'Xuất Excel' }).click()
  const body = (await exporting).postDataJSON() as {
    columns: { title: string; type: string; tones: (string | null)[] | null }[]
    rows: unknown[]
  }
  const column = (title: string) => body.columns.find((c) => c.title === title)!
  expect(column('Số chứng từ').type).toBe('docno')
  expect(column('Giá trị').type).toBe('money')
  expect(column('Loại chứng từ').tones).toBeNull()
  // Cột trạng thái: mỗi dòng một màu đèn, cùng số dòng với dữ liệu.
  const tones = column('Tình trạng').tones!
  expect(column('Tình trạng').type).toBe('status')
  expect(tones).toHaveLength(body.rows.length)
  for (const tone of tones) expect(['success', 'warning', 'error', 'neutral', null]).toContain(tone)
  const download = await downloading
  expect(download.suggestedFilename()).toBe('tra-cuu-chung-tu.xlsx')
  expect(readFileSync(await download.path()).subarray(0, 2).toString()).toBe('PK')
})
