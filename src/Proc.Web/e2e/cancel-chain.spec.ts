import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, seedItemSupplier, switchCompany, unique } from './inventoryHelpers.ts'
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

/**
 * Lát Z4c (design.md mục 20.2 E2): RFQ đã gửi NCC chỉ quản trị hủy – nút Hủy dùng hộp thoại trình tự (RFQ chưa có PO, hợp đồng, bảng giá
 * nên chuỗi chỉ có chính nó – không hiện bảng), lý do bắt buộc. Chuỗi RFQ → PO / hợp đồng / bảng giá nháp và chặn khi đã duyệt: test tích hợp `CancelChainRfqTests`.
 */
test('hủy RFQ đã gửi NCC: hộp thoại trình tự, lý do bắt buộc', async ({ page, request }) => {
  test.setTimeout(60_000)
  const { itemId, supplierId } = await seedItemSupplier(request)
  const bha = await api(request, 'BHA')
  const hop = (await bha.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'HOP')!.id
  const in3 = new Date(Date.now() + 3 * 86_400_000).toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const rfq = await bha.send<{ id: string; no: string }>('post', '/api/rfqs', {
    date: todayVn(), title: `RFQ hủy ${unique('H')}`, deadline: in3, sourcingId: null,
    weights: { price: 60, quality: 20, delivery: 10, payment: 5, warranty: 5 }, note: null,
    lines: [{ id: null, itemId, uomId: hop, qty: 5, sourcePrLineId: null, note: null }], supplierIds: [supplierId],
  })
  await bha.send('post', `/api/rfqs/${rfq.id}/send`, { reason: 'Mua gấp' })

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/dau-thau/rfq/${rfq.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(rfq.no)

  await page.getByRole('button', { name: 'Hủy RFQ' }).click()
  // Chuỗi chỉ có chính RFQ thì hộp thoại không hiện bảng trình tự (cancelChainDialog.tsx).
  const dialog = page.getByRole('dialog')
  await expect(dialog).toContainText(`Hủy phiếu ${rfq.no}?`)
  await expect(dialog.getByRole('table', { name: 'Trình tự hủy' })).toHaveCount(0)
  await dialog.getByRole('button', { name: 'Hủy phiếu' }).click()
  await expect(page.locator('.ant-message')).toContainText('Hãy nhập lý do hủy.')
  await dialog.getByRole('textbox').fill('Đổi phương án mua')
  await page.screenshot({ path: 'e2e/screenshots/cancel-chain-rfq.png' })
  await dialog.getByRole('button', { name: 'Hủy phiếu' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã hủy phiếu ${rfq.no}.`)
  await expect(page.locator('.document-form-title')).toContainText('Đã hủy')
})
