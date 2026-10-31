import { expect, test, type Page } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, chooseOption, openByCommand, pickOption, seedPostedReceipt, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Tìm nguồn → RFQ → báo giá 3 NCC → so sánh → chọn thầu → PO nháp (design.md mục 8.3, 8.4), ở BHA.
 * Mặt hàng và NCC thứ nhất lấy từ seedPostedReceipt; thêm 2 NCC đã duyệt, áp dụng tại BHA qua API.
 */

/** Bấm nút lệnh rồi nút cùng tên trong hộp xác nhận, chờ thông báo. */
async function confirm(page: Page, button: string, okText: string, messageText: string | RegExp) {
  await page.getByRole('button', { name: new RegExp(`(^| )${button}$`) }).click()
  await page.getByRole('dialog').getByRole('button', { name: new RegExp(`(^| )${okText}$`) }).click()
  await expect(page.locator('.ant-message')).toContainText(messageText)
  await expect(page.locator('.ant-modal-confirm')).toHaveCount(0)
}

/** Nhập đơn giá dòng 1 trong hộp báo giá của một NCC. */
async function quote(page: Page, supplierCode: string, price: string) {
  await page.getByRole('button', { name: `Nhập báo giá ${supplierCode}` }).click()
  const dialog = page.getByRole('dialog', { name: new RegExp(`Báo giá của ${supplierCode}`) })
  await dialog.getByLabel('Đơn giá báo giá dòng 1').fill(price)
  await dialog.getByRole('button', { name: 'Lưu báo giá' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã lưu báo giá của ${supplierCode}.`)
  await expect(dialog).toHaveCount(0)
}

test('tìm nguồn → RFQ → báo giá → chọn thầu → tạo PO', async ({ page, request }) => {
  test.setTimeout(120_000)
  const { itemCode, supplierCode: s1 } = await seedPostedReceipt(request)
  const head = await api(request)
  const bha = await api(request, 'BHA')
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const others: string[] = []
  for (const name of ['B', 'C']) {
    const supplier = await head.send<{ id: string; code: string }>('post', '/api/suppliers', {
      code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: `NCC thầu ${name} ${unique('T')}`, shortName: null, isForeign: false, taxCode: null,
      address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null, rating: null, status: 'Approved',
      statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
    })
    await bha.send('put', `/api/suppliers/${supplier.id}/company`, { localCode: null, startDate: null, isActive: true })
    others.push(supplier.code)
  }
  const [s2, s3] = others
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')

  // 1. Phiếu tìm nguồn: mặt hàng, một NCC ứng viên → Lập RFQ.
  await openByCommand(page, 'SRC')
  await expect(page.getByRole('heading', { name: 'Danh sách tìm nguồn' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await chooseOption(page, 'Mặt hàng', itemCode)
  await page.getByLabel('Số lượng dự kiến').fill('40')
  await page.getByLabel('Người phụ trách').fill('Anh Nam')
  await page.keyboard.press('F2')
  await page.getByLabel('NCC dòng 1').fill(s1)
  await pickOption(page, s1)
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu SRC-BHA-\d{4}-\d{4}\./)
  await page.getByRole('button', { name: /Lập RFQ$/ }).click()

  // 2. RFQ: dòng hàng mang từ phiếu tìm nguồn; mời thêm 2 NCC; gửi.
  await expect(page.getByLabel('Tiêu đề')).toHaveValue(/Báo giá Bulông/)
  await expect(page.locator('.line-grid-row').first()).toContainText(itemCode)
  await page.getByRole('tab', { name: 'NCC mời' }).click()
  for (const code of [s2, s3]) await chooseOption(page, 'NCC mời', code)
  await page.keyboard.press('Escape')
  await confirm(page, 'Gửi NCC', 'Gửi NCC', /Đã gửi RFQ RFQ-BHA-\d{4}-\d{4}\./)
  await expect(page.locator('.document-form-title')).toContainText('Đang nhận báo giá')

  // 3. Nhập báo giá 3 NCC: B rẻ nhất.
  await page.getByRole('tab', { name: 'So sánh & chọn thầu' }).click()
  await quote(page, s1, '50000')
  await quote(page, s2, '45000')
  await quote(page, s3, '47000')
  await expect(page.getByTestId(`quote-${s2}-1`)).toContainText('Thấp nhất')
  await expect(page.getByLabel('NCC trúng dòng 1')).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/rfq-comparison.png' })

  // 4. Chọn thầu (mặc định NCC giá thấp nhất) → Tạo PO nháp cho B.
  await confirm(page, 'Chọn thầu', 'Chọn thầu', /Đã chọn thầu RFQ/)
  await expect(page.locator('.document-form-title')).toContainText('Đã chọn thầu')
  await confirm(page, 'Tạo PO', 'Tạo PO', new RegExp(`Đã tạo PO-BHA-\\d{4}-\\d{4} \\(${s2}\\)\\.`))
  await expect(page.getByRole('table', { name: 'Điểm nhà cung cấp' })).toContainText(/PO-BHA-\d{4}-\d{4}/)
})
