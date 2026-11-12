import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { chooseOption, openByCommand, switchCompany, unique } from './inventoryHelpers.ts'
import { createBhaAdmin, receiveByApi, seedApprovedPo, todayVn } from './receiptHelpers.ts'

/**
 * Màn hình bảng tổng hợp đề xuất mua BTH (lát Y5c-2, design.md mục 19.3): quản trị lập bảng 2B ở BHA – chọn dòng đề nghị đã duyệt qua hộp
 * "Chọn dòng đề nghị" (gán sẵn đơn vị mua), lưu, gửi duyệt → người duyệt (quản trị khác) duyệt trên trang duyệt gọn điện thoại `/duyet/BTH/{id}`
 * → bảng Đã duyệt; hủy bảng bằng hộp hủy dây chuyền (chuỗi chỉ có chính bảng vì chưa lên PO). Đề nghị, đơn vị mua tạo sẵn qua API.
 */
test('lập bảng tổng hợp 2B, duyệt trên điện thoại, hủy', async ({ browser, page, request }) => {
  test.setTimeout(90_000)
  // Mặt hàng có giá nhận gần nhất 50.000 đ/hộp (giá tham khảo của đề nghị): một PO đã nhận đủ bằng XNH.
  const seed = await seedApprovedPo(request, 10)
  await receiveByApi(seed.bha, seed.po.id, seed.po.lines[0].id, 10)
  const { bha, itemCode, itemId, hop } = seed
  const departmentCode = unique('TH')
  const department = await bha.send<{ id: string }>('post', '/api/departments', { code: departmentCode, name: `Phòng ${departmentCode}`, managerName: null })
  const unitCode = unique('DV')
  await bha.send('post', '/api/purchasing-units', { code: unitCode, name: `Phòng CUVT ${unitCode}`, note: null })
  const approver = await createBhaAdmin(bha)
  const approverLogin = await request.post('/api/auth/login', { data: { userName: approver.userName, password: approver.password } })
  const { accessToken, user } = (await approverLogin.json()) as { accessToken: string; user: { companies: { id: string; code: string }[] } }
  const approverHeaders = { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.companies.find((c) => c.code === 'BHA')!.id }

  // Đề nghị mua 6 hộp, gửi duyệt và duyệt qua API (người lập – quản trị – không tự duyệt).
  const pr = await bha.send<{ id: string; no: string }>('post', '/api/purchase-requests', {
    date: todayVn(), departmentId: department.id, purpose: 'Bảo hộ lao động quý IV', neededDate: null, warehouseId: null, priority: 'Normal',
    category: 'Goods', note: null, lines: [{ id: null, itemId, uomId: hop, qty: 6, suggestedSupplierId: null, neededDate: null, note: null }],
    newItems: [],
  })
  await bha.send('post', `/api/purchase-requests/${pr.id}/submit`, {})
  const prApproved = await request.post(`/api/purchase-requests/${pr.id}/approve`, { headers: approverHeaders, data: { comment: null } })
  expect(prApproved.ok(), await prApproved.text()).toBeTruthy()

  // 1. Quản trị lập bảng tổng hợp 2B: chọn bộ phận, chọn dòng đề nghị kèm đơn vị mua, lưu, gửi duyệt.
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'BTH')
  await expect(page.getByRole('heading', { name: 'Bảng tổng hợp đề xuất mua (BTH)' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  // Chưa chọn bộ phận thì chưa chọn dòng được.
  await page.getByRole('button', { name: 'Chọn dòng đề nghị' }).click()
  await expect(page.locator('.ant-message')).toContainText('Hãy chọn biểu mẫu và bộ phận đề xuất')
  await chooseOption(page, 'Bộ phận đề xuất', departmentCode)
  await page.getByRole('button', { name: 'Chọn dòng đề nghị' }).click()
  const picker = page.getByRole('dialog', { name: 'Chọn dòng đề nghị mua đã duyệt còn cần mua' })
  const candidate = picker.locator('.ant-table-row', { hasText: pr.no })
  await expect(candidate).toContainText(itemCode)
  await expect(candidate).toContainText('300.000')
  await chooseOption(page, 'Đơn vị mua cho các dòng chọn', unitCode)
  await candidate.getByRole('checkbox').check()
  await picker.getByRole('button', { name: 'Thêm 1 dòng' }).click()
  const line = page.locator('.line-grid-row').first()
  await expect(line).toContainText(pr.no)
  await expect(line).toContainText(itemCode)
  await expect(line).toContainText(unitCode)
  await expect(page.locator('.document-form-content')).toContainText('300.000')

  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu BTH-BHA-\d{4}-\d{4}\./)
  await expect(page.locator('.document-form-number')).toHaveText(/^BTH-BHA-\d{4}-\d{4}$/)
  const no = (await page.locator('.document-form-number').textContent())!
  const summaryId = /([0-9a-f-]{36})/.exec(page.url())![1]
  // Khung chi phí 2B: ngân sách tháng của bộ phận (bộ phận mới chưa có ngân sách).
  await expect(page.locator('.document-form-content .ant-alert').filter({ hasText: 'chưa có ngân sách tháng' })).toBeVisible()
  await page.getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  await page.getByRole('dialog').getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã gửi duyệt phiếu ${no}.`)
  await expect(page.locator('.document-form-title')).toContainText('Chờ duyệt')
  await page.screenshot({ path: 'e2e/screenshots/purchase-summary.png', fullPage: true })

  // 2. Người duyệt mở trang duyệt gọn trên điện thoại: thấy biểu mẫu, dòng đề nghị kèm đơn vị mua; duyệt.
  const phoneContext = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true })
  const phone = await phoneContext.newPage()
  await loginViaApi(phone, { userName: approver.userName, password: approver.password })
  await phone.goto(`/duyet/BTH/${summaryId}`)
  const detail = phone.locator('.erp-mobile-section').filter({ has: phone.getByRole('heading', { name: 'Bảng tổng hợp', exact: true }) })
  await expect(detail).toContainText('2B – Hàng hóa, dịch vụ')
  await expect(detail).toContainText(`Phòng ${departmentCode}`)
  await expect(phone.locator('.erp-mobile-lines')).toContainText(`Đơn vị mua: ${unitCode}`)
  await phone.screenshot({ path: 'e2e/screenshots/purchase-summary-mobile.png', fullPage: true })
  await phone.getByRole('button', { name: 'Duyệt' }).click()
  await phone.locator('.ant-modal-confirm').getByRole('button', { name: 'Duyệt' }).click()
  await expect(phone.locator('.ant-message')).toContainText(`Đã duyệt ${no}.`)
  await phoneContext.close()

  // 3. Bảng đã duyệt; hủy bằng hộp hủy dây chuyền (chưa lên PO – chuỗi chỉ có chính bảng), lý do bắt buộc.
  await page.reload()
  await expect(page.locator('.document-form-title')).toContainText('Đã duyệt')
  await page.getByRole('button', { name: 'Hủy phiếu' }).click()
  const dialog = page.getByRole('dialog')
  await expect(dialog).toContainText(`Hủy phiếu ${no}?`)
  await dialog.getByRole('textbox').fill('Gộp vào bảng tổng hợp khác')
  await dialog.getByRole('button', { name: 'Hủy phiếu' }).click()
  await expect(page.locator('.document-form-title')).toContainText('Đã hủy')
  await expect(page.getByText('Đã hủy: Gộp vào bảng tổng hợp khác')).toBeVisible()
})
