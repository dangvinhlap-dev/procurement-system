import { readFileSync } from 'node:fs'
import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand } from './inventoryHelpers.ts'

/**
 * Dữ liệu mua lịch sử (design.md mục 18.1, lát E1): nhập file Excel (kiểm tra thử → nhập thật), xem theo lô, chụp màn hình,
 * xóa lô nhập nhầm. File mẫu e2e/fixtures/du-lieu-mua-lich-su.xlsx có 3 dòng: số kiểu Việt Nam ("18.500.000", "20,5"), ô ngày Excel.
 */
test('nhập dữ liệu mua lịch sử từ Excel, xem theo lô, xóa lô', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'PHIS')
  await expect(page.getByRole('heading', { name: 'Dữ liệu mua lịch sử' })).toBeVisible()

  await page.getByRole('button', { name: 'Nhập Excel' }).click()
  const dialog = page.getByRole('dialog', { name: 'Nhập dữ liệu mua lịch sử từ Excel' })
  const downloading = page.waitForEvent('download')
  await dialog.getByRole('button', { name: 'Tải file mẫu' }).click()
  expect((await downloading).suggestedFilename()).toBe('mau-du-lieu-mua-lich-su.xlsx')

  await dialog.locator('input[type=file]').setInputFiles({
    name: 'du-lieu-mua-lich-su.xlsx',
    mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    buffer: readFileSync('e2e/fixtures/du-lieu-mua-lich-su.xlsx'),
  })
  await dialog.getByRole('button', { name: 'Kiểm tra thử' }).click()
  await expect(dialog.getByText(/File hợp lệ: 3 dòng/)).toBeVisible()
  await dialog.getByRole('button', { name: 'Nhập thật' }).click()
  await expect(dialog.getByText(/Đã nhập xong 3 dòng/)).toBeVisible()
  await dialog.locator('.ant-modal-footer').getByRole('button', { name: /(^|\s)Đóng$/ }).click()

  // Lô mới nhất đứng đầu ô "Lô nhập".
  await page.getByRole('combobox', { name: 'Lô nhập', exact: true }).click()
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option').first().click()
  await expect(page.locator('.data-grid-row')).toHaveCount(3)
  // Bấm ra ngoài để đóng danh sách thả xuống trước khi chụp.
  await page.getByRole('heading', { name: 'Dữ liệu mua lịch sử' }).click()
  await expect(page.locator('.ant-select-dropdown:visible')).toHaveCount(0)
  const oil = page.locator('.data-grid-row', { hasText: 'DAU-TL68' })
  await expect(oil).toContainText('12/06/2025')
  await expect(oil).toContainText('74.000.000')
  await expect(page.locator('.data-grid-row', { hasText: 'VB-6205' })).toContainText('20,5')
  await page.screenshot({ path: 'e2e/screenshots/purchase-history.png' })

  await page.getByRole('button', { name: 'Xóa lô' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Xóa lô' }).click()
  await expect(page.getByText(/Đã xóa lô/)).toBeVisible()
  await expect(page.getByRole('button', { name: 'Xóa lô' })).toHaveCount(0)
})
