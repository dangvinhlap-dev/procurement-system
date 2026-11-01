import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * File đính kèm dùng chung (design.md mục 18.4) trên phiếu nhập đã ghi sổ: tải lên kèm ghi chú, tab hiện số file, tải xuống đúng tên,
 * xóa sau khi xác nhận. Đuôi bị chặn báo lỗi ngay trên trình duyệt.
 */
test('đính kèm file vào phiếu nhập đã ghi sổ: tải lên, tải xuống, xóa', async ({ page, request }) => {
  const { receiptId, receiptNo } = await seedPostedReceipt(request)

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/kho/grn/${receiptId}`)
  await expect(page.getByText(receiptNo).first()).toBeVisible()

  await page.getByRole('tab', { name: 'Đính kèm' }).click()
  await expect(page.getByText('Chưa có file đính kèm.')).toBeVisible()

  await page.getByLabel('Ghi chú cho file tải lên').fill('Hóa đơn GTGT bản scan')
  const chooser = page.locator('.ant-upload input[type="file"]')
  await chooser.setInputFiles({ name: 'Hóa đơn 0000456.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4 thử') })
  await expect(page.getByText('Đã tải lên Hóa đơn 0000456.pdf.')).toBeVisible()
  await expect(page.getByRole('tab', { name: 'Đính kèm (1)' })).toBeVisible()
  const row = page.locator('.ant-table-row', { hasText: 'Hóa đơn 0000456.pdf' })
  await expect(row).toContainText('Hóa đơn GTGT bản scan')
  await expect(row).toContainText('Quản trị hệ thống')
  await page.screenshot({ path: 'e2e/screenshots/attachments.png' })

  const download = page.waitForEvent('download')
  await row.getByText('Hóa đơn 0000456.pdf').click()
  expect((await download).suggestedFilename()).toBe('Hóa đơn 0000456.pdf')

  await chooser.setInputFiles({ name: 'cai-dat.exe', mimeType: 'application/octet-stream', buffer: Buffer.from('MZ') })
  await expect(page.getByText('Không nhận file "cai-dat.exe"')).toBeVisible()

  await row.getByRole('button', { name: 'Xóa' }).click()
  await page.getByRole('button', { name: 'Xóa', exact: true }).last().click()
  await expect(page.getByText('Chưa có file đính kèm.')).toBeVisible()
})
