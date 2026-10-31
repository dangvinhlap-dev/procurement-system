import { expect, test, type Page } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, chooseOption, openByCommand, pickOption, seedPostedReceipt, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Luồng mua hàng ở BHA (design.md mục 6.2, 6.7): người lập (tài khoản mới) lập PR, gửi duyệt → quản trị duyệt ở Việc cần làm →
 * người lập lập PO lấy dòng từ PR, gửi duyệt → quản trị duyệt → phiếu nhập theo PO lấy dòng còn lại, ghi sổ → PO Đã nhận đủ.
 * Mặt hàng, NCC (áp dụng tại BHA) tạo sẵn qua API; giá tham khảo = giá nhập gần nhất 50.000 đ/hộp.
 */

/** Ghi sổ/gửi duyệt/duyệt qua hộp xác nhận rồi chờ thông báo. */
async function confirm(page: Page, button: string, messageText: string | RegExp) {
  // Tên nút có thể kèm tên icon ("send Gửi duyệt"); so khớp phần cuối, phân biệt hoa thường để "Duyệt" không trùng "Gửi duyệt".
  const name = new RegExp(`(^| )${button}$`)
  await page.getByRole('button', { name }).click()
  await page.getByRole('dialog').getByRole('button', { name }).click()
  await expect(page.locator('.ant-message')).toContainText(messageText)
  await expect(page.locator('.ant-modal-confirm')).toHaveCount(0)
}

test('PR → duyệt → PO từ PR → duyệt → nhập kho theo PO', async ({ browser, page, request }) => {
  // Luồng dài (hai người dùng, 5 phiếu): chạy song song với các test khác có thể quá 30 giây mặc định.
  test.setTimeout(90_000)
  const { itemCode, supplierCode } = await seedPostedReceipt(request)
  // Người lập: tài khoản mới, vai trò quản trị tại BHA (người lập không tự duyệt được phiếu của mình).
  const bha = await api(request, 'BHA')
  const roles = await bha.get<{ id: string; code: string }[]>('/api/roles')
  const buyerName = unique('mh').toLowerCase()
  await bha.send('post', '/api/users', {
    userName: buyerName, fullName: 'Người lập mua hàng', email: null, password: 'Demo@2026',
    access: { roleIds: [roles.find((r) => r.code === 'ADMIN')!.id], warehouseIds: [], itemCategoryIds: [] },
  })
  const buyerContext = await browser.newContext()
  const buyer = await buyerContext.newPage()
  await loginViaApi(buyer, { userName: buyerName, password: 'Demo@2026' })
  await buyer.goto('/')
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')

  // 1. Người lập: PR 12 hộp, gửi duyệt.
  await openByCommand(buyer, 'PR')
  await expect(buyer.getByRole('heading', { name: 'Danh sách đề nghị mua hàng (PR)' })).toBeVisible()
  await buyer.getByRole('button', { name: 'Tạo mới' }).click()
  await buyer.getByLabel('Bộ phận đề nghị').fill('Xưởng cán 1')
  await buyer.getByLabel('Mục đích sử dụng').fill('Thay thế định kỳ')
  await buyer.keyboard.press('F2')
  await buyer.getByLabel('Mặt hàng dòng 1').fill(itemCode.toLowerCase())
  await pickOption(buyer, itemCode)
  await buyer.getByLabel('SL đề nghị dòng 1').fill('12')
  await buyer.keyboard.press('Control+s')
  await expect(buyer.locator('.ant-message')).toContainText(/Đã lưu phiếu PR-BHA-\d{4}-\d{4}\./)
  await expect(buyer.locator('.line-grid-row').first()).toContainText('600.000')
  await expect(buyer.locator('.document-form-number')).toHaveText(/^PR-BHA-\d{4}-\d{4}$/)
  const prNo = (await buyer.locator('.document-form-number').textContent())!
  // Phiếu đang sửa: nút lệnh ở thanh chân (ui-guide.md mục 4, ý 3); chờ duyệt thì chỉ xem, thanh chân ẩn.
  await expect(buyer.getByTestId('document-form-bar')).toContainText('Gửi duyệt')
  await confirm(buyer, 'Gửi duyệt', `Đã gửi duyệt phiếu ${prNo}.`)
  await expect(buyer.locator('.document-form-title')).toContainText('Chờ duyệt')
  await expect(buyer.getByTestId('document-form-bar')).toHaveCount(0)
  await expect(buyer.getByRole('button', { name: /(^|\s)Duyệt$/ })).toHaveCount(0)
  await buyer.screenshot({ path: 'e2e/screenshots/purchase-request.png' })

  // 2. Quản trị: Việc cần làm → mở PR → Duyệt.
  await openByCommand(page, 'TODO')
  const todo = page.locator('.data-grid-row', { hasText: prNo })
  await expect(todo).toContainText('Đề nghị mua hàng')
  await todo.dblclick()
  await confirm(page, 'Duyệt', `Đã duyệt xong phiếu ${prNo}.`)
  await expect(page.locator('.document-form-title')).toContainText('Đã duyệt')
  await page.getByRole('tab', { name: 'Lịch sử duyệt' }).click()
  await expect(page.getByRole('cell', { name: 'Đã duyệt', exact: true })).toBeVisible()

  // 3. Người lập: PO lấy dòng từ PR, NCC, CK 0; gửi duyệt.
  await openByCommand(buyer, 'PO')
  await expect(buyer.getByRole('heading', { name: 'Danh sách đơn mua hàng (PO)' })).toBeVisible()
  await buyer.getByRole('button', { name: 'Tạo mới' }).click()
  await buyer.getByRole('button', { name: 'Lấy dòng từ PR đã duyệt' }).click()
  const picker = buyer.getByRole('dialog')
  await picker.getByRole('row', { name: new RegExp(prNo) }).getByRole('checkbox').check()
  await picker.getByRole('button', { name: 'Thêm 1 dòng' }).click()
  await chooseOption(buyer, 'Nhà cung cấp', supplierCode)
  await expect(buyer.getByLabel('SL đặt dòng 1')).toHaveValue('12')
  await buyer.getByLabel('Đơn giá dòng 1').fill('48000')
  await buyer.getByLabel('Đơn giá dòng 1').press('Tab')
  await expect(buyer.locator('.document-form-footer')).toContainText('633.600')
  await buyer.keyboard.press('Control+s')
  await expect(buyer.locator('.ant-message')).toContainText(/Đã lưu phiếu PO-BHA-\d{4}-\d{4}\./)
  await expect(buyer.locator('.document-form-number')).toHaveText(/^PO-BHA-\d{4}-\d{4}$/)
  const poNo = (await buyer.locator('.document-form-number').textContent())!
  await confirm(buyer, 'Gửi duyệt', `Đã gửi duyệt phiếu ${poNo}.`)
  await buyer.screenshot({ path: 'e2e/screenshots/purchase-order.png' })

  // 4. Quản trị duyệt PO.
  await openByCommand(page, 'TODO')
  await page.locator('.data-grid-row', { hasText: poNo }).dblclick()
  await confirm(page, 'Duyệt', `Đã duyệt xong phiếu ${poNo}.`)

  // 5. Phiếu nhập theo PO: chọn PO, lấy dòng còn lại (giá 48.000 theo PO), ghi sổ → PO Đã nhận đủ.
  await openByCommand(page, 'GRN')
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await chooseOption(page, 'Đơn mua hàng', poNo)
  await page.getByRole('button', { name: 'Lấy các dòng còn lại của PO' }).click()
  await expect(page.getByLabel('Đơn giá dòng 1')).toBeDisabled()
  await expect(page.locator('.line-grid-row').first()).toContainText('576.000')
  await confirm(page, 'Ghi sổ', /Đã ghi sổ phiếu GRN-BHA-\d{4}-\d{4}\./)
  await openByCommand(page, 'PO')
  await expect(page.locator('.data-grid-row', { hasText: poNo })).toContainText('Đã nhận đủ')
  await buyerContext.close()
})

test('PO: chưa có NCC chọn được thì báo rõ lý do và chỗ xử lý', async ({ page }) => {
  // Giả lập công ty chưa có NCC đã duyệt và đang áp dụng (design.md mục 5.9).
  await page.route('**/api/suppliers/lookup**', (route) => route.fulfill({ json: [] }))
  await loginViaApi(page)
  await page.goto('/mua-hang/po/moi')
  const hint = page.locator('.document-form-content .ant-alert-warning')
  await expect(hint).toContainText('Chưa có nhà cung cấp chọn được')
  await expect(hint.getByRole('link', { name: 'Danh mục nhà cung cấp' })).toHaveAttribute('href', '/gia-ncc/nha-cung-cap')
})
