import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, approveContract, approvePriceList, chooseOption, openByCommand, pickOption, seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

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
