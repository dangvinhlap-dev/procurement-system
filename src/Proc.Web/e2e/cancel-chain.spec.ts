import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { switchCompany } from './inventoryHelpers.ts'
import { seedApprovedPo, todayVn } from './receiptHelpers.ts'

/**
 * Lát Z4b (design.md mục 20.1 R2, 20.2 D3): hủy PO có phiếu nhận hàng nháp – hộp thoại liệt kê trình tự từ phiếu cuối về phiếu đầu (phiếu
 * xác nhận nhận hàng rồi PO), lý do bắt buộc; hủy xong cả chuỗi đã hủy. (Phiếu nhận hàng đã xác nhận thì chặn hủy – test tích hợp.)
 */
test('hủy dây chuyền PO có phiếu nhận hàng nháp: hộp thoại trình tự, hủy cả chuỗi', async ({ page, request }) => {
  test.setTimeout(60_000)
  const { bha, po } = await seedApprovedPo(request, 6)
  // Phiếu nhận hàng còn nháp (chưa xác nhận hàng về).
  const xnh = await bha.send<{ id: string; no: string }>('post', '/api/receipt-confirmations', {
    purchaseOrderId: po.id, date: todayVn(), deliveryNoteNo: null, deliverer: null, note: null, invoiceNo: null, invoiceSeries: null,
    invoiceDate: null, invoiceAmount: null,
    lines: [{ purchaseOrderLineId: po.lines[0].id, deliveredQty: 2, acceptedQty: 2, rejectedQty: 0, checkResult: 'Passed', checkNote: null, note: null }],
  })

  // Quản trị (quyền Hủy phiếu) mở PO đã duyệt.
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/po/${po.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(po.no)

  await page.getByRole('button', { name: 'Hủy phiếu' }).click()
  const dialog = page.getByRole('dialog')
  const rows = dialog.getByRole('table', { name: 'Trình tự hủy' }).locator('tbody tr')
  await expect(rows).toHaveCount(2)
  await expect(rows.nth(0)).toContainText(xnh.no)
  await expect(rows.nth(1)).toContainText(po.no)
  await dialog.getByRole('textbox').fill('NCC giao sai quy cách')
  await page.screenshot({ path: 'e2e/screenshots/cancel-chain.png' })
  await dialog.getByRole('button', { name: 'Hủy 2 phiếu' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã hủy 2 phiếu: ${xnh.no}, ${po.no}.`)
  await expect(page.locator('.document-form-title')).toContainText('Đã hủy')

  await page.goto(`/mua-hang/xac-nhan-nhan-hang/${xnh.id}`)
  await expect(page.locator('.document-form-title')).toContainText('Đã hủy')
})
