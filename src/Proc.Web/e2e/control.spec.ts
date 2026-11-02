import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, openByCommand, seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * Kiểm soát nội bộ (lát F3, design.md mục 18.19): PO gửi duyệt của NCC mà người lập có kê khai quan hệ lợi ích → dấu hiệu "Xung đột lợi
 * ích" trên màn hình ANOM; bấm đúp, "Ghi nhận vi phạm" mở sổ vi phạm với phiếu điền sẵn; lưu, kết luận thì phiếu khóa.
 */
test('dấu hiệu bất thường → ghi nhận vi phạm → kết luận', async ({ page, request }) => {
  const { itemId, supplierId } = await seedPostedReceipt(request)
  const bha = await api(request, 'BHA')
  const hop = (await bha.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'HOP')!.id
  const warehouses = await bha.get<{ id: string; code: string }[]>('/api/warehouses')
  const today = new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const po = await bha.send<{ id: string; no: string }>('post', '/api/purchase-orders', {
    date: today, supplierId, warehouseId: warehouses.find((w) => w.code === 'KT')!.id,
    expectedDate: null, paymentTerms: null, deliveryTerms: null, deliveryAddress: null, receiveTolerancePct: 0, note: null,
    lines: [{ id: null, itemId, uomId: hop, qty: 2, unitPrice: 60000, discountPct: 0, vatCode: '10', deliveryDate: null, note: null, sourcePrLineId: null }],
  })
  await bha.send('post', `/api/purchase-orders/${po.id}/submit`, {})
  // Người lập (quản trị) kê khai quan hệ lợi ích với NCC từ hôm nay – NCC mới mỗi lần chạy nên không ảnh hưởng test khác.
  await bha.send('post', '/api/conflict-declarations', {
    userId: null, supplierId, relation: 'Family', description: 'Em trai là giám đốc kinh doanh', validFrom: today,
  })

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'ANOM')
  await expect(page.getByRole('heading', { name: 'Dấu hiệu bất thường (KSNB)' })).toBeVisible()
  const row = page.locator('.data-grid-row', { hasText: po.no }).filter({ hasText: 'Xung đột lợi ích' })
  await expect(row).toContainText('Cao')
  await expect(page.getByRole('alert').first()).toContainText(/Chiết khấu\/tài trợ chưa ghi nhận: \d+/)
  await page.screenshot({ path: 'e2e/screenshots/anomalies.png' })

  await row.dblclick()
  const dialog = page.getByRole('dialog', { name: /Xung đột lợi ích/ })
  await expect(dialog).toContainText('Em trai là giám đốc kinh doanh')
  await dialog.getByRole('button', { name: 'Ghi nhận vi phạm' }).click()

  await expect(page.getByRole('heading', { name: 'Sổ vi phạm (KSNB)' })).toBeVisible()
  await expect(page.getByRole('textbox', { name: 'Tiêu đề' })).toHaveValue(`Xung đột lợi ích – ${po.no}`)
  await expect(page.getByRole('textbox', { name: 'Chứng từ liên quan' })).toHaveValue(po.no)
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã ghi nhận vi phạm VP-BHA-\d{4}-\d{4}/)
  const drawer = page.locator('.detail-drawer')
  await expect(drawer).toContainText('Đang xác minh')

  await drawer.getByRole('button', { name: 'Kết luận' }).click()
  await page.getByLabel('Kết luận, hướng xử lý').fill('Xác minh: người lập có quan hệ với NCC, chưa kê khai trước khi lập PO')
  await page.locator('.ant-modal-confirm').getByRole('button', { name: 'Kết luận' }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã kết luận vi phạm VP-/)
  await expect(drawer).toContainText('Đã kết luận')
  await page.screenshot({ path: 'e2e/screenshots/control-finding.png' })

  // Trang đầy đủ của vi phạm (mở từ thông báo, Tra cứu chứng từ – TC-3).
  const findingId = new URL(page.url()).searchParams.get('mo')!
  await page.goto(`/bao-cao/so-vi-pham/${findingId}`)
  await expect(page.locator('.detail-page')).toContainText('Đã kết luận')
  await expect(page.locator('.detail-drawer')).toHaveCount(0)
  await page.locator('.detail-page').getByRole('button', { name: 'Quay lại' }).click()
  await expect(page).toHaveURL(/\/bao-cao\/so-vi-pham$/)
  await expect(page.locator('.data-grid-row', { hasText: `Xung đột lợi ích – ${po.no}` })).toContainText('Đã kết luận')
})
