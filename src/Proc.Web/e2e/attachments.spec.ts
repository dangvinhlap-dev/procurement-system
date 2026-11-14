import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { seedDraftPo, switchCompany } from './inventoryHelpers.ts'

/**
 * File đính kèm dùng chung (design.md mục 18.4) trên đơn mua hàng nháp (phiếu kho cũ gỡ ở lát K3): tải lên kèm ghi chú, tab hiện số file, tải xuống đúng tên,
 * xóa sau khi xác nhận. Đuôi bị chặn báo lỗi ngay trên trình duyệt.
 */
test('đính kèm file vào đơn mua hàng: tải lên, tải xuống, xóa', async ({ page, request }) => {
  const { poId, poNo } = await seedDraftPo(request)

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/po/${poId}`)
  await expect(page.getByText(poNo).first()).toBeVisible()

  await page.getByRole('tab', { name: 'Đính kèm' }).click()
  // Form PO còn bảng file khác ở tab ẩn (file NCC) – chỉ xét ô đang hiện.
  const empty = page.getByText('Chưa có file đính kèm.').locator('visible=true')
  await expect(empty).toBeVisible()

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
  await expect(empty).toBeVisible()
})

/**
 * Đính kèm ngay khi tạo phiếu (quy tắc người dùng 03/11/2026, design.md mục 18.4): phiếu chưa lưu vẫn chọn được file vào hàng chờ, có ô
 * Ghi chú; lưu phiếu xong file tự tải lên. File quá giới hạn (mặc định 2 MB) báo lỗi ngay trên trình duyệt.
 */
test('đính kèm khi tạo phiếu tìm nguồn: chọn file trước khi lưu, lưu xong tự tải lên', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto('/dau-thau/tim-nguon/moi')

  const subject = `Van bi thử đính kèm ${Date.now()}`
  await page.getByLabel('Mô tả hàng cần tìm').fill(subject)
  await page.getByLabel('Ghi chú', { exact: true }).fill('Cần gấp cho bảo trì tháng 11')

  await page.getByRole('tab', { name: 'Đính kèm' }).click()
  await expect(page.getByText('Tối đa 2 MB/file')).toBeVisible()
  await expect(page.getByText('File sẽ tải lên khi lưu phiếu.')).toBeVisible()
  const chooser = page.locator('.ant-upload input[type="file"]')
  await chooser.setInputFiles({ name: 'qua-lon.pdf', mimeType: 'application/pdf', buffer: Buffer.alloc(2 * 1024 * 1024 + 1) })
  await expect(page.getByText('File "qua-lon.pdf" quá 2 MB')).toBeVisible()

  await page.getByLabel('Ghi chú cho file tải lên').fill('Catalogue NCC')
  await chooser.setInputFiles({ name: 'catalogue.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4 thử') })
  await expect(page.getByRole('tab', { name: 'Đính kèm (1)' })).toBeVisible()
  await expect(page.locator('.ant-table-row', { hasText: 'catalogue.pdf' })).toContainText('Catalogue NCC')

  await page.getByRole('button', { name: 'Lưu' }).first().click()
  await expect(page.getByText('Đã tải lên 1 file đính kèm.')).toBeVisible()
  await expect(page).toHaveURL(/\/dau-thau\/tim-nguon\/[0-9a-f-]{36}$/)

  // Mở lại phiếu: file đã nằm trên máy chủ (không còn ở hàng chờ trình duyệt).
  await page.reload()
  await page.getByRole('tab', { name: 'Đính kèm (1)' }).click()
  const row = page.locator('.ant-table-row', { hasText: 'catalogue.pdf' })
  await expect(row).toContainText('Quản trị hệ thống')
  await expect(page.getByLabel('Ghi chú', { exact: true })).toHaveValue('Cần gấp cho bảo trì tháng 11')
  await page.screenshot({ path: 'e2e/screenshots/attachments-on-create.png' })
})
