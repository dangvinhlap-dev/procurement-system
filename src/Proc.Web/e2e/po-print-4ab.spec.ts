import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { switchCompany } from './inventoryHelpers.ts'
import { seedApprovedPo } from './receiptHelpers.ts'

/**
 * Lát Y7 (design.md mục 19.3, Q10): PO hiện "Loại mua – biểu mẫu"; "Xem bảng theo dõi" (lát IN2) xem / in bảng theo dõi mua hàng 4A/4B giữ
 * nguyên sheet (biểu mẫu ký trước khi đặt hàng), nút "In đơn gửi NCC" in đơn đặt hàng theo khuôn chung.
 */
test('PO: in bảng theo dõi 4B và đơn gửi NCC', async ({ page, request }) => {
  test.setTimeout(60_000)
  const { po } = await seedApprovedPo(request, 4)

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/po/${po.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(po.no)
  await expect(page.getByTestId('po-form-code')).toHaveValue('B – Hàng hóa, dịch vụ · 4B')

  // Lát IN2: "Xem bảng theo dõi" mở hộp thoại xem trang in giữ nguyên sheet 4B (xem không ghi lần in; Tải PDF tính một lần in).
  const track = page.waitForResponse((r) => r.url().endsWith(`/api/purchase-orders/${po.id}/pdf`))
  await page.getByRole('button', { name: 'Xem bảng theo dõi' }).click()
  const trackResponse = await track
  expect(trackResponse.status()).toBe(200)
  expect(trackResponse.headers()['content-type']).toContain('application/pdf')
  const dialog = page.getByRole('dialog', { name: `Xem bảng theo dõi mua hàng ${po.no} – mẫu 4B` })
  await expect(page.getByTestId('print-preview-frame')).toBeVisible()
  await expect(page.getByTestId('print-count')).toHaveText('Chưa in lần nào')
  await page.screenshot({ path: 'e2e/screenshots/po-print-preview.png' })
  // Chromium chạy ẩn không có trình xem PDF: khung xem trong hộp thoại cũng sinh sự kiện tải (tên mã blob) – chờ đúng file tải có tên phiếu.
  const download = page.waitForEvent('download', { predicate: (d) => d.suggestedFilename() === `${po.no}.pdf` })
  await dialog.getByRole('button', { name: 'Tải PDF' }).click()
  expect((await download).suggestedFilename()).toBe(`${po.no}.pdf`)
  await expect(page.getByTestId('print-count')).toContainText('Đã in 1 lần')
  await dialog.getByRole('button', { name: 'Đóng', exact: true }).click()
  await expect(dialog).toHaveCount(0)

  const supplier = page.waitForResponse((r) => r.url().endsWith(`/api/purchase-orders/${po.id}/pdf?form=supplier`))
  const popup2 = page.waitForEvent('popup')
  await page.getByRole('button', { name: 'In đơn gửi NCC' }).click()
  expect((await supplier).status()).toBe(200)
  await (await popup2).close()
  await page.screenshot({ path: 'e2e/screenshots/po-print-4ab.png' })
})
