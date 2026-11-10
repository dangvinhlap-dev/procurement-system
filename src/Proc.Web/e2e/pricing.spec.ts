import { readFileSync } from 'node:fs'
import { expect, test, type Page } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { approvePriceList, chooseOption, openByCommand, pickOption, switchCompany } from './inventoryHelpers.ts'
import { receiveByApi, seedApprovedPo } from './receiptHelpers.ts'

/**
 * Bảng giá NCC và so sánh giá (design.md mục 8.1, 8.2): tổng công ty lập bảng giá toàn tập đoàn, gửi duyệt, người duyệt giá duyệt → so sánh
 * giá thấy giá thực mua của BHA (phiếu nhập 50.000 đ/hộp) và giá bảng giá → PO ở BHA lấy giá đã duyệt theo bảng giá; sửa giá thì phải
 * ghi lý do lệch giá và PO qua thêm chặng duyệt ngoại lệ giá (design.md mục 18.10).
 */

/** Bấm nút lệnh rồi nút cùng tên trong hộp xác nhận, chờ thông báo. */
async function confirm(page: Page, button: string, messageText: string | RegExp) {
  const name = new RegExp(`(^| )${button}$`)
  await page.getByRole('button', { name }).click()
  await page.getByRole('dialog').getByRole('button', { name }).click()
  await expect(page.locator('.ant-message')).toContainText(messageText)
  await expect(page.locator('.ant-modal-confirm')).toHaveCount(0)
}

test('bảng giá toàn tập đoàn → so sánh giá → PO theo giá đã duyệt, lệch giá qua duyệt ngoại lệ', async ({ page, request }) => {
  test.setTimeout(90_000)
  // Nhận 10 hộp × 50.000 bằng phiếu XNH theo PO đã duyệt (từ 0.22.0 – design.md mục 19.4).
  const seed = await seedApprovedPo(request, 10)
  await receiveByApi(seed.bha, seed.po.id, seed.po.lines[0].id, 10)
  const { itemCode, supplierCode } = seed
  await loginViaApi(page)
  await page.goto('/')

  // 1. Tổng công ty: bảng giá toàn tập đoàn 47.000 đ/hộp.
  await openByCommand(page, 'PRL')
  await expect(page.getByRole('heading', { name: 'Bảng giá nhà cung cấp' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await chooseOption(page, 'Nhà cung cấp', supplierCode)
  await page.getByLabel('Tên bảng giá').fill('Bảng giá bulông 2026')
  await page.getByLabel('Áp dụng toàn tập đoàn').check()
  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(itemCode.toLowerCase())
  await pickOption(page, itemCode)
  await page.getByLabel('Đơn giá dòng 1').fill('47000')
  await page.getByLabel('Đơn giá dòng 1').press('Tab')
  await expect(page.locator('.line-grid-row').first()).toContainText('47.000')
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu bảng giá PRL-TCT-\d{4}-\d{4}\./)
  await expect(page.locator('.document-form-number')).toHaveText(/^PRL-TCT-\d{4}-\d{4}$/)
  // Bảng giá phải duyệt mới hiệu lực, người lập không tự duyệt (lát B1): gửi duyệt → Chờ duyệt, không có nút Duyệt.
  await confirm(page, 'Gửi duyệt', /Đã gửi duyệt bảng giá/)
  await expect(page.locator('.document-form-title')).toContainText('Chờ duyệt')
  await expect(page.getByRole('button', { name: /(^| )Duyệt$/ })).toHaveCount(0)
  await expect(page.getByRole('button', { name: /Rút về nháp$/ })).toBeVisible()
  await approvePriceList(request, page.url().split('/').pop()!)
  await page.reload()
  await expect(page.locator('.document-form-title')).toContainText('Hiệu lực')
  await page.getByRole('tab', { name: 'Lịch sử duyệt' }).click()
  await expect(page.getByRole('tabpanel')).toContainText('Phan Thị Giá')
  await page.screenshot({ path: 'e2e/screenshots/supplier-price-list.png' })

  // 2. So sánh giá toàn tập đoàn: BHA mua 50.000; bảng giá 47.000.
  await openByCommand(page, 'PCMP')
  await expect(page.getByRole('heading', { name: 'So sánh giá' })).toBeVisible()
  await chooseOption(page, 'Mặt hàng', itemCode)
  await expect(page.getByRole('alert').filter({ hasText: 'Toàn tập đoàn' })).toContainText('thấp nhất 50.000')
  const row = page.locator('.data-grid-row', { hasText: 'BHA' })
  await expect(row).toContainText(supplierCode)
  await expect(row).toContainText('50.000')
  await expect(row).toContainText('Thấp nhất')
  await page.keyboard.press('Escape')
  await page.screenshot({ path: 'e2e/screenshots/price-comparison.png' })
  await page.getByRole('combobox', { name: 'Xem', exact: true }).click()
  await pickOption(page, 'Bảng giá NCC đang hiệu lực')
  await expect(page.locator('.data-grid-row', { hasText: supplierCode })).toContainText('47.000')
  await expect(page.locator('.data-grid-row', { hasText: supplierCode })).toContainText('Toàn tập đoàn')

  // 3. PO ở BHA: cột Giá đã duyệt 47.000 nguồn Bảng giá; đơn giá 0 là lệch giá (tam giác vàng), bấm Áp giá đã duyệt thì hết lệch.
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'PO')
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await chooseOption(page, 'Nhà cung cấp', supplierCode)
  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(itemCode.toLowerCase())
  await pickOption(page, itemCode)
  await page.getByLabel('SL đặt dòng 1').fill('5')
  await page.getByLabel('SL đặt dòng 1').press('Tab')
  const line = page.locator('.line-grid-row').first()
  await expect(line).toContainText('47.000')
  await expect(line).toContainText(/Bảng giá PRL-TCT-\d{4}-\d{4}/)
  await expect(page.getByLabel('Lệch giá đã duyệt dòng 1')).toBeVisible()
  await page.getByRole('button', { name: /Áp giá đã duyệt$/ }).click()
  await expect(page.getByLabel('Đơn giá dòng 1')).toHaveValue('47.000')
  await expect(page.locator('.document-form-footer')).toContainText('258.500')
  await expect(page.getByLabel('Lệch giá đã duyệt dòng 1')).toHaveCount(0)

  // 3b. Cảnh báo giá cao (design.md mục 8.5): giá thấp nhất tập đoàn từ phiếu nhập; đơn giá gấp 10 lần thì tam giác vàng, giá thường thì không.
  await expect(page.getByLabel('Giá cao dòng 1')).toHaveCount(0)
  await page.getByLabel('Đơn giá dòng 1').fill('500000')
  await page.getByLabel('Đơn giá dòng 1').press('Tab')
  await expect(page.getByLabel('Giá cao dòng 1')).toBeVisible()
  await page.getByLabel('Giá cao dòng 1').hover()
  await expect(page.getByRole('tooltip').filter({ hasText: 'cao hơn giá thấp nhất tập đoàn' })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/po-high-price.png' })
  await page.getByRole('button', { name: /Áp giá đã duyệt$/ }).click()
  await expect(page.getByLabel('Giá cao dòng 1')).toHaveCount(0)

  // 4. Sửa giá 48.000: lệch giá → cảnh báo, gửi duyệt khi chưa có lý do bị chặn; ghi lý do thì gửi được, có chặng "Ngoại lệ giá".
  await page.getByLabel('Đơn giá dòng 1').fill('48000')
  await page.getByLabel('Đơn giá dòng 1').press('Tab')
  await expect(page.getByLabel('Lệch giá đã duyệt dòng 1')).toBeVisible()
  await expect(page.getByRole('alert').filter({ hasText: 'lệch giá đã duyệt' })).toBeVisible()
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu PO-BHA-\d{4}-\d{4}\./)
  // Chờ chuyển từ /moi sang trang phiếu đã lưu – bấm Gửi duyệt sớm hơn thì thông báo lỗi mất khi trang dựng lại (máy bận khi chạy song song).
  await expect(page).not.toHaveURL(/\/moi$/)
  // Làm chậm lượt tải lại phiếu (sau lưu/gửi) để chắc chắn người dùng gõ lý do TRƯỚC khi phiếu tải về: phiếu tải về trùng nội dung
  // đã lưu thì không được nạp đè chữ đang gõ (useDocumentFormSync).
  const poDetail = /\/api\/purchase-orders\/[0-9a-f-]{36}$/
  await page.route(poDetail, async (route) => {
    if (route.request().method() !== 'GET') return route.continue()
    await new Promise((r) => setTimeout(r, 1500))
    await route.continue()
  })
  // Đổi giá sau lần lưu đầu: "Gửi duyệt" lưu thay đổi này nên phiếu tải về khác bản đang có – trước đây chính lượt này ghi đè form.
  await page.getByLabel('Đơn giá dòng 1').fill('48500')
  await page.getByLabel('Đơn giá dòng 1').press('Tab')
  // Gửi duyệt lưu phiếu rồi xem trước chuỗi duyệt – thiếu lý do lệch giá thì báo lỗi ngay, không mở popup.
  await page.getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  // Lưu rồi xem trước (hai lượt gọi API) – khi máy bận có thể quá 5 giây mới báo lỗi.
  await expect(page.getByRole('alert').filter({ hasText: 'lý do lệch giá' })).toBeVisible({ timeout: 20_000 })
  await expect(page.locator('.ant-modal-confirm')).toHaveCount(0)
  const reloaded = page.waitForResponse((r) => poDetail.test(new URL(r.url()).pathname) && r.request().method() === 'GET')
  await page.getByLabel('Lý do lệch giá dòng 1').fill('NCC báo tăng giá nguyên liệu')
  await reloaded
  await page.unroute(poDetail)
  await expect(page.getByLabel('Đơn giá dòng 1')).toHaveValue('48.500')
  await expect(page.getByLabel('Lý do lệch giá dòng 1')).toHaveValue('NCC báo tăng giá nguyên liệu')
  await page.screenshot({ path: 'e2e/screenshots/po-price-exception.png' })
  // Gửi duyệt = lưu rồi gửi (hai lượt gọi API) – khi chạy song song có thể quá 5 giây, chờ trạng thái thay vì thông báo thoáng qua.
  await page.getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  // Popup liệt kê người duyệt từng cấp (yêu cầu người dùng 03/11/2026).
  const sendDialog = page.locator('.ant-modal-confirm')
  await expect(sendDialog).toContainText('Người duyệt lần lượt', { timeout: 20_000 })
  await page.screenshot({ path: 'e2e/screenshots/po-submit-preview.png' })
  await sendDialog.getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  await expect(page.locator('.document-form-title')).toContainText('Chờ duyệt', { timeout: 20_000 })
  await page.getByRole('tab', { name: 'Lịch sử duyệt' }).click()
  await expect(page.getByRole('tabpanel')).toContainText('Ngoại lệ giá')
  // Cây lịch sử phê duyệt: lập phiếu, lượt gửi (người, giờ), cấp đang chờ nêu tên người duyệt.
  const tree = page.getByLabel('Cây lịch sử phê duyệt')
  await expect(tree).toContainText('Lập phiếu:')
  await expect(tree).toContainText(/Lượt 1 – gửi duyệt bởi .+ lúc \d{2}\/\d{2}\/\d{4} \d{2}:\d{2}/)
  await expect(tree).toContainText('Chờ duyệt:')
  await page.screenshot({ path: 'e2e/screenshots/approval-tree.png' })
})

/**
 * Nhập Excel bảng giá NCC (design.md mục 8.5): nút Nhập Excel trên danh sách PRL, tải file mẫu, file sai mẫu báo thiếu cột bắt buộc
 * (nhập đúng file đã có test tích hợp SupplierPricingTests.NhapExcel_BangGia_*).
 */
test('bảng giá NCC: hộp thoại nhập Excel', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'PRL')
  await page.getByRole('button', { name: 'Nhập Excel' }).click()
  const dialog = page.getByRole('dialog', { name: 'Nhập bảng giá NCC từ Excel' })
  await expect(dialog.getByText(/các dòng cùng Mã NCC \+ Tên bảng giá là một bảng giá/)).toBeVisible()
  const downloading = page.waitForEvent('download')
  await dialog.getByRole('button', { name: 'Tải file mẫu' }).click()
  expect((await downloading).suggestedFilename()).toBe('mau-bang-gia-ncc.xlsx')

  // File của màn hình khác (dữ liệu mua lịch sử) thiếu các cột của bảng giá – báo lỗi, không lưu gì.
  await dialog.locator('input[type=file]').setInputFiles({
    name: 'sai-mau.xlsx',
    mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    buffer: readFileSync('e2e/fixtures/du-lieu-mua-lich-su.xlsx'),
  })
  await dialog.getByRole('button', { name: 'Kiểm tra thử' }).click()
  await expect(dialog.getByText(/File thiếu cột bắt buộc: .*Tên bảng giá/)).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/price-list-import.png' })
})
