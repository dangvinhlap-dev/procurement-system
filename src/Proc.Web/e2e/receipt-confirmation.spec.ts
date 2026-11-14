import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand, pickOption, switchCompany, unique } from './inventoryHelpers.ts'
import { seedApprovedPo } from './receiptHelpers.ts'

/**
 * Lát K2 (design.md mục 19.4): từ PO đã duyệt bấm "Lập phiếu nhận hàng" → phiếu XNH lấy sẵn dòng còn lại; nhập SL đạt / không đạt, kết quả
 * kiểm tra; xác nhận → PO có SL thực nhận; điều chỉnh SL (trả NCC) có lý do, lịch sử; đơn vị đặt hàng nghiệm thu; báo cáo OTIF thấy dòng thiếu.
 */
test('xác nhận nhận hàng theo PO, điều chỉnh SL, nghiệm thu, báo cáo OTIF, chụp màn hình', async ({ browser, page, request }) => {
  test.setTimeout(60_000)
  const { bha, po } = await seedApprovedPo(request, 10)

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/po/${po.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(po.no)
  await page.getByRole('button', { name: /Lập phiếu nhận hàng$/ }).click()

  // Phiếu mới: PO chọn sẵn, dòng còn lại 10 HOP.
  await expect(page).toHaveURL(/\/mua-hang\/xac-nhan-nhan-hang\/moi\?po=/)
  await expect(page.getByLabel('SL đạt dòng 1')).toHaveValue('10')
  await page.getByLabel('Số phiếu giao NCC').fill('PG-0001')
  await page.getByLabel('SL đạt dòng 1').fill('8')
  await page.getByLabel('SL không đạt dòng 1').fill('2')
  await page.getByLabel('Kết quả kiểm tra dòng 1').click()
  await pickOption(page, 'Không đạt')
  await page.getByLabel('Ghi chú kiểm tra dòng 1').fill('2 hộp móp, trả lại NCC')
  // Nút trên thanh lệnh (cây điều hướng cũng có mục cùng tên).
  await page.locator('button.ant-btn', { hasText: 'Xác nhận nhận hàng' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã xác nhận nhận hàng phiếu XNH')
  await expect(page).toHaveURL(/\/mua-hang\/xac-nhan-nhan-hang\/[0-9a-f-]{36}$/)
  await expect(page.locator('.document-form-title')).toContainText('Đã xác nhận')
  await expect(page.getByRole('alert').filter({ hasText: 'chưa xác nhận nghiệm thu' })).toBeVisible()
  const xnhNo = (await page.locator('.document-form-number').textContent())!.trim()
  await page.screenshot({ path: 'e2e/screenshots/receipt-confirmation.png' })

  // Trả NCC 1 hộp: điều chỉnh −1 có lý do → lịch sử điều chỉnh.
  await page.getByRole('button', { name: /Điều chỉnh SL thực nhận$/ }).click()
  const adjust = page.getByRole('dialog')
  await adjust.getByLabel('SL điều chỉnh').fill('-1')
  await adjust.getByLabel('Lý do điều chỉnh').fill('Trả NCC 1 hộp sai quy cách')
  await adjust.getByRole('button', { name: 'Lưu' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã điều chỉnh SL thực nhận phiếu ${xnhNo}.`)
  await page.getByRole('tab', { name: /Lịch sử điều chỉnh/i }).click()
  await expect(page.getByRole('tabpanel')).toContainText('Trả NCC 1 hộp sai quy cách')

  // Lát K2b: "In phiếu" mở biên bản giao nhận và kiểm tra hàng hóa (PDF) ở tab mới.
  const pdfResponse = page.waitForResponse((r) => r.url().endsWith('/pdf'))
  const popup = page.waitForEvent('popup')
  await page.getByRole('button', { name: 'In phiếu' }).click()
  const pdf = await pdfResponse
  expect(pdf.status()).toBe(200)
  expect(pdf.headers()['content-type']).toContain('application/pdf')
  await (await popup).close()

  // PO: SL thực nhận 7, còn 3.
  await page.goto(`/mua-hang/po/${po.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(po.no)
  await expect(page.locator('.line-grid-readonly').filter({ hasText: /^7$/ }).first()).toBeVisible()

  // Đơn vị đặt hàng nghiệm thu (người khác – người nhận hàng không tự nghiệm thu).
  const roleCode = unique('NT')
  await bha.send('post', '/api/roles', {
    code: roleCode, name: `Nghiệm thu ${roleCode}`, permissionCodes: ['Purchase.Receipt.View', 'Purchase.Receipt.Accept', 'Report.Purchase.View'],
  })
  const role = (await bha.get<{ id: string; code: string }[]>('/api/roles')).find((r) => r.code === roleCode)!
  const userName = unique('nt').toLowerCase()
  await bha.send('post', '/api/users', {
    userName, fullName: `Bếp trung tâm ${userName}`, email: null, password: 'Demo@2026',
    access: { roleIds: [role.id], warehouseIds: [], itemCategoryIds: [] },
  })
  const list = await bha.get<{ items: { id: string; no: string }[] }>(`/api/receipt-confirmations?purchaseOrderId=${po.id}`)
  const xnhId = list.items.find((x) => x.no === xnhNo)!.id
  // Phiên riêng cho người nghiệm thu (phiên quản trị của trang chính giữ nguyên).
  const unitContext = await browser.newContext()
  const unit = await unitContext.newPage()
  await loginViaApi(unit, { userName, password: 'Demo@2026' })
  await unit.goto(`/mua-hang/xac-nhan-nhan-hang/${xnhId}`)
  await expect(unit.locator('.document-form-number')).toHaveText(xnhNo)
  await expect(unit.getByRole('button', { name: /Điều chỉnh SL thực nhận$/ })).toHaveCount(0)
  await unit.getByRole('button', { name: /Xác nhận nghiệm thu$/ }).click()
  const acceptance = unit.getByRole('dialog')
  await acceptance.getByText('Không đạt – có ý kiến').click()
  await acceptance.getByRole('button', { name: 'Xác nhận' }).click()
  await expect(acceptance).toContainText('Không đạt phải ghi ý kiến')
  await acceptance.getByLabel('Ý kiến nghiệm thu').fill('Thiếu 3 hộp so với đơn')
  await acceptance.getByRole('button', { name: 'Xác nhận' }).click()
  await expect(unit.getByRole('alert').filter({ hasText: 'Nghiệm thu: Không đạt' })).toContainText('Thiếu 3 hộp so với đơn')

  // Báo cáo SL đặt – SL thực nhận: dòng PO thiếu 3.
  await openByCommand(unit, 'OTIF')
  const row = unit.locator('tr').filter({ hasText: po.no })
  await expect(row).toContainText('Thiếu')
  await unit.screenshot({ path: 'e2e/screenshots/order-fulfillment.png' })
  await unitContext.close()
})
