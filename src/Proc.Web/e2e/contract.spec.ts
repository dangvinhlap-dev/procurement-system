import { readFileSync } from 'node:fs'
import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, approveContract, approveContractAmendment, approvePriceList, chooseOption, openByCommand, pickOption, seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * Hợp đồng nguyên tắc phương án 2 (lát B4a – design.md mục 18.7) ở BHA: gắn bảng giá toàn tập đoàn của NCC, sản lượng dự kiến lấy giá
 * theo bảng giá, điều khoản, gửi duyệt → người duyệt hợp đồng duyệt → Hiệu lực.
 */
test('hợp đồng nguyên tắc: lập, sản lượng theo bảng giá, gửi duyệt, hiệu lực', async ({ page, request }) => {
  test.setTimeout(120_000)
  const { itemCode, itemId, supplierCode } = await seedPostedReceipt(request)
  const head = await api(request)
  const suppliers = await head.get<{ items: { id: string; code: string }[] }>(`/api/suppliers?search=${supplierCode}`)
  const supplierId = suppliers.items.find((s) => s.code === supplierCode)!.id
  const hop = (await head.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'HOP')!.id
  const today = new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  // Bảng giá toàn tập đoàn do tổng công ty lập và duyệt – các pháp nhân dùng chung (phương án 2).
  const list = await head.send<{ id: string; no: string }>('post', '/api/price-lists', {
    supplierId, title: 'Bảng giá bulông tập đoàn', isGroupWide: true, validFrom: today, validTo: null, deliveryTerms: null, note: null,
    lines: [{ id: null, itemId, uomId: hop, minQty: 0, unitPrice: 50000, discountPct: 4, vatCode: null, note: null }],
  })
  await head.send('post', `/api/price-lists/${list.id}/submit`, {})
  await approvePriceList(request, list.id)

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'CTR')
  await expect(page.getByRole('heading', { name: 'Hợp đồng nguyên tắc' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()

  await chooseOption(page, 'Nhà cung cấp', supplierCode)
  await chooseOption(page, 'Bảng giá dùng chung', list.no)
  await page.getByLabel('Tên hợp đồng').fill('Hợp đồng nguyên tắc bulông 2026')
  await page.getByLabel('Số HĐ giấy').fill('HĐNT-01/2026')
  const validTo = new Date(Date.now() + 180 * 86_400_000).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric' })
  await page.getByLabel('Hiệu lực đến').fill(validTo)
  await page.getByLabel('Hiệu lực đến').press('Enter')

  // Sản lượng dự kiến: 1.000 hộp × 50.000 × 96% = 48.000.000.
  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(itemCode)
  await pickOption(page, itemCode)
  await page.getByLabel('SL dự kiến dòng 1').fill('1000')
  await page.getByLabel('SL dự kiến dòng 1').press('Tab')
  await expect(page.locator('.line-grid-row').first()).toContainText('48.000.000')

  await page.getByRole('tab', { name: 'Điều khoản' }).click()
  await page.getByLabel('Chế tài chậm giao, giao sai, hàng lỗi').fill('Phạt 0,1%/ngày chậm giao')
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu hợp đồng CTR-BHA-\d{4}-\d{4}\./)
  await expect(page.locator('.document-form-title')).toContainText('Nháp')
  await expect(page.getByRole('button', { name: /Gửi duyệt$/ })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/contract-terms.png' })

  await page.getByRole('button', { name: /Gửi duyệt$/ }).click()
  await page.getByRole('dialog').getByRole('button', { name: /Gửi duyệt$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã gửi duyệt hợp đồng CTR-BHA-/)
  await expect(page.locator('.document-form-title')).toContainText('Chờ duyệt')
  await expect(page.getByRole('button', { name: /Duyệt$/ })).toHaveCount(0)

  await approveContract(request, page.url().split('/').pop()!)
  await page.reload()
  await expect(page.locator('.document-form-title')).toContainText('Hiệu lực')
  await page.getByRole('tab', { name: 'Lịch sử duyệt' }).click()
  await page.screenshot({ path: 'e2e/screenshots/contract-active.png' })
  await expect(page.getByRole('button', { name: /Chấm dứt$/ })).toBeVisible()
})

/**
 * Phụ lục hợp đồng (lát B4b – design.md mục 18.7): hợp đồng hiệu lực → Lập phụ lục (điền sẵn giá trị hiện hành) → gia hạn, đổi điều khoản
 * thanh toán → gửi duyệt → người duyệt hợp đồng duyệt → hợp đồng đổi ngày hết hiệu lực, tab Phụ lục ghi "Đã duyệt – đã áp".
 */
test('phụ lục hợp đồng: gia hạn, đổi điều khoản, duyệt thì áp vào hợp đồng', async ({ page, request }) => {
  test.setTimeout(120_000)
  const { itemId, supplierCode } = await seedPostedReceipt(request)
  const head = await api(request)
  const bha = await api(request, 'BHA')
  const suppliers = await head.get<{ items: { id: string; code: string }[] }>(`/api/suppliers?search=${supplierCode}`)
  const supplierId = suppliers.items.find((s) => s.code === supplierCode)!.id
  const hop = (await head.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'HOP')!.id
  const day = (offset: number) => new Date(Date.now() + offset * 86_400_000).toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const list = await head.send<{ id: string; no: string }>('post', '/api/price-lists', {
    supplierId, title: 'Bảng giá bulông tập đoàn', isGroupWide: true, validFrom: day(0), validTo: null, deliveryTerms: null, note: null,
    lines: [{ id: null, itemId, uomId: hop, minQty: 0, unitPrice: 50000, discountPct: 4, vatCode: null, note: null }],
  })
  await head.send('post', `/api/price-lists/${list.id}/submit`, {})
  await approvePriceList(request, list.id)
  const contract = await bha.send<{ id: string; no: string }>('post', '/api/contracts', {
    supplierId, priceListId: list.id, contractNo: 'HĐNT-02/2026', title: 'Hợp đồng nguyên tắc bulông', signDate: day(0), validFrom: day(0),
    validTo: day(90), priceAdjustmentTerms: null, paymentTerms: '30 ngày', creditLimit: null, deliveryTerms: null, discountTerms: null,
    sponsorshipTerms: null, penaltyTerms: null, otherTerms: null, note: null, departmentIds: [],
    lines: [{ id: null, itemId, uomId: hop, expectedQty: 1000, minCommitQty: null, note: null }],
  })
  await bha.send('post', `/api/contracts/${contract.id}/submit`, {})
  await approveContract(request, contract.id)

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/hop-dong-khung/${contract.id}`)
  await expect(page.locator('.document-form-title')).toContainText('Hiệu lực')
  await page.getByRole('button', { name: /Lập phụ lục$/ }).click()

  const drawer = page.locator('.detail-drawer')
  await expect(drawer).toContainText(`Lập phụ lục – hợp đồng ${contract.no}`)
  await drawer.getByLabel('Lý do lập phụ lục').fill('Gia hạn thêm 6 tháng theo biên bản đàm phán')
  const newTo = new Date(Date.now() + 270 * 86_400_000).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric' })
  await drawer.getByLabel('Hiệu lực đến (sau phụ lục)').fill(newTo)
  await drawer.getByLabel('Hiệu lực đến (sau phụ lục)').press('Enter')
  await drawer.getByRole('tab', { name: 'Điều khoản' }).click()
  // Ô điều khoản điền sẵn giá trị hiện hành; đổi thì hiện "Hiện hành: …" để so sánh.
  await expect(drawer.getByLabel('Thanh toán, công nợ')).toHaveValue('30 ngày')
  await drawer.getByLabel('Thanh toán, công nợ').fill('45 ngày')
  await expect(drawer).toContainText('Hiện hành: 30 ngày')
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(`Đã lưu phụ lục ${contract.no}-PL01.`)
  await drawer.getByRole('tab', { name: 'Phụ lục' }).click()
  await expect(drawer).toContainText(`Gia hạn đến ${newTo}`)
  await expect(drawer).toContainText('Đổi thanh toán')
  await page.screenshot({ path: 'e2e/screenshots/contract-amendment.png' })

  await drawer.getByRole('button', { name: /Gửi duyệt$/ }).click()
  await page.getByRole('dialog').last().getByRole('button', { name: /Gửi duyệt$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã gửi duyệt phụ lục ${contract.no}-PL01.`)
  const amendmentId = new URL(page.url()).searchParams.get('phu-luc')!
  await approveContractAmendment(request, amendmentId)

  // Mở từ đường dẫn của thông báo (chỉ có Id phụ lục) → chuyển sang hợp đồng, mở sẵn ngăn kéo.
  await page.goto(`/mua-hang/hop-dong-khung/phu-luc/${amendmentId}`)
  await expect(drawer).toContainText('Đã duyệt – đã áp')
  await drawer.getByRole('button', { name: 'Đóng' }).click()
  await expect(page.locator('.document-form')).toBeVisible()
  await expect(page.getByLabel('Hiệu lực đến', { exact: true })).toHaveValue(newTo)
  await page.getByRole('tab', { name: 'Phụ lục (1)' }).click()
  await expect(page.getByRole('tabpanel')).toContainText('Đã duyệt – đã áp')
  await page.screenshot({ path: 'e2e/screenshots/contract-amendment-applied.png' })
})

/**
 * Nhập Excel hợp đồng hiện hữu (lát B4c, design.md mục 18.7): nút Nhập Excel trên danh sách CTR, tải file mẫu, file sai mẫu báo thiếu cột
 * bắt buộc (nhập đúng file đã có test tích hợp ContractTests.NhapExcel_*).
 */
test('hợp đồng nguyên tắc: hộp thoại nhập Excel hợp đồng hiện hữu', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'CTR')
  await page.getByRole('button', { name: 'Nhập Excel' }).click()
  const dialog = page.getByRole('dialog', { name: 'Nhập hợp đồng nguyên tắc từ Excel' })
  await expect(dialog.getByText(/các dòng cùng Số HĐ giấy là một hợp đồng/)).toBeVisible()
  const downloading = page.waitForEvent('download')
  await dialog.getByRole('button', { name: 'Tải file mẫu' }).click()
  expect((await downloading).suggestedFilename()).toBe('mau-hop-dong-nguyen-tac.xlsx')

  // File của màn hình khác (dữ liệu mua lịch sử) thiếu các cột của hợp đồng – báo lỗi, không lưu gì.
  await dialog.locator('input[type=file]').setInputFiles({
    name: 'sai-mau.xlsx',
    mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    buffer: readFileSync('e2e/fixtures/du-lieu-mua-lich-su.xlsx'),
  })
  await dialog.getByRole('button', { name: 'Kiểm tra thử' }).click()
  await expect(dialog.getByText(/File thiếu cột bắt buộc: Số HĐ giấy/)).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/contract-import.png' })
})

/**
 * Chiết khấu doanh số/tài trợ (lát F4b – design.md mục 18.21): nhập cam kết trên hợp đồng nháp (tab Chiết khấu – tài trợ), duyệt → hợp
 * đồng hiệu lực hiện bảng theo dõi theo kỳ; ghi nhận thực nhận rồi hủy (bắt buộc lý do).
 */
test('hợp đồng nguyên tắc: cam kết chiết khấu, theo dõi và ghi nhận thực nhận', async ({ page, request }) => {
  test.setTimeout(120_000)
  const { itemId, supplierCode } = await seedPostedReceipt(request)
  const head = await api(request)
  const bha = await api(request, 'BHA')
  const suppliers = await head.get<{ items: { id: string; code: string }[] }>(`/api/suppliers?search=${supplierCode}`)
  const supplierId = suppliers.items.find((s) => s.code === supplierCode)!.id
  const hop = (await head.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'HOP')!.id
  const day = (offset: number) => new Date(Date.now() + offset * 86_400_000).toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const list = await head.send<{ id: string; no: string }>('post', '/api/price-lists', {
    supplierId, title: 'Bảng giá bulông chiết khấu', isGroupWide: true, validFrom: day(0), validTo: null, deliveryTerms: null, note: null,
    lines: [{ id: null, itemId, uomId: hop, minQty: 0, unitPrice: 50000, discountPct: 0, vatCode: null, note: null }],
  })
  await head.send('post', `/api/price-lists/${list.id}/submit`, {})
  await approvePriceList(request, list.id)
  // Hiệu lực từ 200 ngày trước để có kỳ quý đã kết thúc.
  const contract = await bha.send<{ id: string; no: string }>('post', '/api/contracts', {
    supplierId, priceListId: list.id, contractNo: 'HĐNT-03/2026', title: 'Hợp đồng bulông có chiết khấu', signDate: day(-200), validFrom: day(-200),
    validTo: day(160), priceAdjustmentTerms: null, paymentTerms: null, creditLimit: null, deliveryTerms: null, discountTerms: '2% doanh số quý',
    sponsorshipTerms: null, penaltyTerms: null, otherTerms: null, note: null, departmentIds: [],
    lines: [{ id: null, itemId, uomId: hop, expectedQty: 1000, minCommitQty: null, note: null }],
  })

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/hop-dong-khung/${contract.id}`)
  await expect(page.locator('.document-form-title')).toContainText('Nháp')
  await page.getByRole('tab', { name: 'Chiết khấu – tài trợ (0)' }).click()
  const panel = page.getByRole('tabpanel', { name: 'Chiết khấu – tài trợ (0)' })
  await panel.getByRole('button', { name: 'Thêm dòng' }).click()
  await page.getByLabel('Mức cam kết 1').fill('2')
  await page.getByLabel('Nội dung cam kết 1').fill('Chiết khấu 2% doanh số mỗi quý, trừ vào công nợ')
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(`Đã lưu hợp đồng ${contract.no}.`)
  await expect(page.getByRole('tab', { name: 'Chiết khấu – tài trợ (1)' })).toBeVisible()

  await bha.send('post', `/api/contracts/${contract.id}/submit`, {})
  await approveContract(request, contract.id)
  await page.reload()
  await expect(page.locator('.document-form-title')).toContainText('Hiệu lực')
  await page.getByRole('tab', { name: 'Chiết khấu – tài trợ (1)' }).click()
  const periods = page.getByRole('table', { name: 'Theo dõi chiết khấu tài trợ theo kỳ' })
  // Kỳ đầu (từ ngày hiệu lực) không có mua → phải nhận 0, "Không phải nhận".
  await expect(periods.locator('tr.ant-table-row').first()).toContainText('Không phải nhận')
  await periods.getByRole('button', { name: /^Ghi nhận kỳ/ }).first().click()
  const dialog = page.getByRole('dialog', { name: /Ghi nhận thực nhận/ })
  await dialog.getByLabel('Số tiền thực nhận (VND)').fill('1500000')
  await dialog.getByLabel('Số chứng từ đối chiếu').fill('BB-CK-01')
  await dialog.getByRole('button', { name: 'Ghi nhận' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã ghi nhận thực nhận.')
  const receipts = page.getByRole('table', { name: 'Các lần ghi nhận thực nhận' })
  await expect(receipts).toContainText('1.500.000')
  await expect(receipts).toContainText('Giảm trừ công nợ')
  await expect(periods.locator('tr.ant-table-row').first()).toContainText('1.500.000')
  await page.screenshot({ path: 'e2e/screenshots/contract-rebates.png' })

  await receipts.getByRole('button', { name: 'Hủy' }).click()
  const confirm = page.getByRole('dialog', { name: /Hủy ghi nhận/ })
  await confirm.getByLabel('Lý do hủy').fill('Ghi nhầm kỳ')
  await confirm.getByRole('button', { name: 'Hủy ghi nhận' }).click()
  await expect(receipts).toContainText('Đã hủy: Ghi nhầm kỳ')
  await expect(receipts.getByRole('button', { name: 'Hủy' })).toHaveCount(0)
})
