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
  // Bộ phận đề nghị chọn từ danh mục bộ phận của BHA (design.md mục 18.5).
  const departmentCode = unique('BP')
  await bha.send('post', '/api/departments', { code: departmentCode, name: `Xưởng cán ${departmentCode}`, managerName: null })
  const buyerName = unique('mh').toLowerCase()
  await bha.send('post', '/api/users', {
    userName: buyerName, fullName: `Người lập mua hàng ${buyerName}`, email: null, password: 'Demo@2026',
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
  // Bộ phận đề nghị bắt buộc: lưu khi chưa chọn thì báo ngay trên ô.
  await expect(buyer.getByRole('combobox', { name: 'Bộ phận đề nghị', exact: true })).toBeVisible()
  await buyer.getByLabel('Mục đích sử dụng').click()
  await buyer.keyboard.press('Control+s')
  await expect(buyer.getByText('Chọn bộ phận đề nghị.')).toBeVisible()
  await chooseOption(buyer, 'Bộ phận đề nghị', departmentCode)
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
  // Người duyệt tiếp theo (TC-4): bước đang chờ nêu những người duyệt được – người lập không có trong đó.
  await buyer.getByRole('tab', { name: 'Lịch sử duyệt' }).click()
  const next = buyer.locator('.approval-next')
  await expect(next).toContainText(/^Chờ duyệt: \S/)
  await expect(next).not.toContainText(buyerName)
  await buyer.screenshot({ path: 'e2e/screenshots/next-approvers.png' })
  const firstApprover = (await next.innerText()).replace(/^Chờ duyệt: /, '').split(', ')[0]
  // Tra cứu chứng từ: cột "Người duyệt tiếp theo" cùng kết quả với tab Lịch sử duyệt.
  await openByCommand(buyer, 'DOCS')
  await buyer.getByRole('textbox', { name: 'Tìm kiếm' }).fill(prNo)
  const docRow = buyer.locator('.data-grid-row', { hasText: prNo })
  await expect(docRow).toContainText('Cấp 1')
  await expect(docRow).toContainText(firstApprover)

  // 2. Quản trị: Việc cần làm → mở PR → Duyệt.
  await openByCommand(page, 'TODO')
  const todo = page.locator('.data-grid-row', { hasText: prNo })
  await expect(todo).toContainText('Đề nghị mua hàng')
  await todo.dblclick()
  await confirm(page, 'Duyệt', `Đã duyệt xong phiếu ${prNo}.`)
  await expect(page.locator('.document-form-title')).toContainText('Đã duyệt')
  await page.getByRole('tab', { name: 'Lịch sử duyệt' }).click()
  // Cây lịch sử phê duyệt: người lập, lượt gửi, cấp đã duyệt kèm người và ngày giờ, lượt hoàn thành duyệt.
  const tree = page.getByLabel('Cây lịch sử phê duyệt')
  await expect(tree).toContainText(`Lập phiếu: Người lập mua hàng ${buyerName}`)
  await expect(tree).toContainText(/Đã duyệt: .+ lúc \d{2}\/\d{2}\/\d{4} \d{2}:\d{2}/)
  await expect(tree).toContainText('hoàn thành duyệt')

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
  // Mua khẩn cấp (design.md mục 18.11): bắt buộc lý do, hạn bổ sung hồ sơ tự điền +7 ngày.
  await buyer.getByRole('combobox', { name: 'Loại mua', exact: true }).click()
  await pickOption(buyer, 'Khẩn cấp')
  await expect(buyer.getByLabel('Hạn bổ sung hồ sơ')).not.toHaveValue('')
  await buyer.getByLabel('Lý do khẩn cấp').fill('Máy cán dừng vì gãy bulông')
  await buyer.keyboard.press('Control+s')
  await expect(buyer.locator('.ant-message')).toContainText(/Đã lưu phiếu PO-BHA-\d{4}-\d{4}\./)
  await expect(buyer.locator('.document-form-number')).toHaveText(/^PO-BHA-\d{4}-\d{4}$/)
  // Bộ phận đặt hàng tự lấy theo bộ phận của PR nguồn (design.md mục 18.10).
  await expect(buyer.locator('.ant-form-item', { hasText: 'Bộ phận đặt hàng' })).toContainText(departmentCode)
  const poNo = (await buyer.locator('.document-form-number').textContent())!
  await confirm(buyer, 'Gửi duyệt', `Đã gửi duyệt phiếu ${poNo}.`)
  // NCC chưa có giá đã duyệt: cảnh báo đầu phiếu, phải qua thêm chặng "Ngoài danh sách/giá" (POL – design.md mục 18.25).
  await expect(buyer.getByRole('alert').filter({ hasText: 'Ngoài danh sách/giá' })).toBeVisible()
  await buyer.screenshot({ path: 'e2e/screenshots/purchase-order.png' })

  // 4. Quản trị duyệt PO: cấp PO thường rồi chặng ngoài danh sách/giá.
  await openByCommand(page, 'TODO')
  await page.locator('.data-grid-row', { hasText: poNo }).dblclick()
  await confirm(page, 'Duyệt', `Đã duyệt cấp của bạn – phiếu ${poNo} chuyển cấp tiếp theo.`)
  await confirm(page, 'Duyệt', `Đã duyệt xong phiếu ${poNo}.`)

  // 4a. Báo cáo SLA mua hàng (design.md mục 18.13): dòng PR có PO duyệt trong hạn → Đúng hạn, có số PO.
  await openByCommand(page, 'SLA')
  await expect(page.getByRole('heading', { name: 'SLA mua hàng' })).toBeVisible()
  const slaRow = page.locator('.data-grid-row', { hasText: prNo })
  await expect(slaRow).toContainText(poNo)
  await expect(slaRow).toContainText('Đúng hạn')
  await expect(page.getByRole('alert').filter({ hasText: 'Đúng SLA' })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/purchase-sla.png' })

  // 4b. Người lập xác nhận đã bổ sung hồ sơ mua khẩn cấp.
  await buyer.reload()
  await expect(buyer.getByRole('alert').filter({ hasText: 'Mua khẩn cấp' })).toContainText('Hạn bổ sung hồ sơ')
  await buyer.getByRole('button', { name: /Đã bổ sung hồ sơ$/ }).click()
  await buyer.getByLabel('Hồ sơ đã bổ sung (bắt buộc)').fill('Biên bản sự cố máy cán, 3 báo giá')
  await buyer.getByRole('dialog').getByRole('button', { name: /Xác nhận$/ }).click()
  await expect(buyer.locator('.ant-message')).toContainText(`Đã xác nhận bổ sung hồ sơ đơn ${poNo}.`)
  await expect(buyer.getByRole('alert').filter({ hasText: 'đã bổ sung hồ sơ' })).toContainText('Biên bản sự cố máy cán')

  // 5. Phiếu nhập theo PO: chọn PO, lấy dòng còn lại (giá 48.000 theo PO), ghi sổ → PO Đã nhận đủ.
  await openByCommand(page, 'GRN')
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await chooseOption(page, 'Đơn mua hàng', poNo)
  await page.getByRole('button', { name: 'Lấy các dòng còn lại của PO' }).click()
  await expect(page.getByLabel('Đơn giá dòng 1')).toBeDisabled()
  await expect(page.locator('.line-grid-row').first()).toContainText('576.000')
  // Kiểm tra chất lượng dòng (design.md mục 18.14): Đạt.
  await page.getByRole('combobox', { name: 'Kết quả kiểm tra dòng 1' }).click()
  await pickOption(page, 'Đạt')
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

test('PR hàng mới chưa có mã → tìm nguồn → gắn mã → lên PO được', async ({ page, request }) => {
  test.setTimeout(60_000)
  const { itemCode, itemId } = await seedPostedReceipt(request)
  const bha = await api(request, 'BHA')
  const roles = await bha.get<{ id: string; code: string }[]>('/api/roles')
  const departmentCode = unique('BP')
  const department = await bha.send<{ id: string }>('post', '/api/departments', { code: departmentCode, name: `Xưởng ${departmentCode}`, managerName: null })
  const hop = (await bha.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'HOP')!.id
  // Người duyệt: tài khoản mới vai trò quản trị tại BHA (người lập – quản trị – không tự duyệt).
  const approverName = unique('dy').toLowerCase()
  await bha.send('post', '/api/users', {
    userName: approverName, fullName: 'Người duyệt PR', email: null, password: 'Demo@2026',
    access: { roleIds: [roles.find((r) => r.code === 'ADMIN')!.id], warehouseIds: [], itemCategoryIds: [] },
  })

  // PR chỉ có hàng mới chưa có mã, gửi duyệt và duyệt qua API.
  const pr = await bha.send<{ id: string; no: string }>('post', '/api/purchase-requests', {
    date: new Date().toISOString().slice(0, 10), departmentId: department.id, purpose: 'Linh kiện máy mới', neededDate: null, warehouseId: null,
    priority: 'Normal', note: null, lines: [],
    newItems: [{ id: null, description: 'Bulông inox chịu axit M12', specification: 'SUS316', uomId: hop, qty: 3, estPrice: 120000, neededDate: null, note: null }],
  })
  await bha.send('post', `/api/purchase-requests/${pr.id}/submit`, {})
  const login = await request.post('/api/auth/login', { data: { userName: approverName, password: 'Demo@2026' } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { companies: { id: string; code: string }[] } }
  const approved = await request.post(`/api/purchase-requests/${pr.id}/approve`, {
    headers: { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.companies.find((c) => c.code === 'BHA')!.id },
    data: { comment: null },
  })
  expect(approved.ok(), await approved.text()).toBeTruthy()

  // Quản trị mở PR: tab hàng mới → lập phiếu tìm nguồn → gắn mã hàng → dòng có mã xuất hiện.
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/pr/${pr.id}`)
  await page.getByRole('tab', { name: /Hàng mới chưa có mã \(1\)/i }).click()
  await expect(page.getByLabel('Mô tả hàng mới dòng 1')).toHaveValue('Bulông inox chịu axit M12')
  await page.getByRole('tabpanel').getByRole('button', { name: /Tìm nguồn$/ }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã lập phiếu tìm nguồn')
  await expect(page.getByRole('tabpanel').getByRole('link')).toBeVisible()
  await page.getByRole('tabpanel').getByRole('button', { name: /Gắn mã$/ }).click()
  await page.getByRole('combobox', { name: 'Mặt hàng gắn mã' }).fill(itemCode.toLowerCase())
  await pickOption(page, itemCode)
  await page.getByRole('dialog').getByRole('button', { name: /Gắn mã$/ }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã gắn mã hàng')
  await expect(page.getByRole('tabpanel')).toContainText(itemCode)
  await page.screenshot({ path: 'e2e/screenshots/purchase-request-new-item.png' })
  await page.getByRole('tab', { name: /Dòng hàng/i }).click()
  await expect(page.locator('.line-grid-row').first()).toContainText(itemCode)
  expect(itemId).toBeTruthy()
})
