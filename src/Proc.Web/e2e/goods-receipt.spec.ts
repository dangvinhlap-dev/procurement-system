import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, chooseOption, openByCommand, pickOption, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Phiếu nhập mua từ NCC (design.md mục 10.1, 10.4) trên khuôn DocumentForm: lập phiếu nháp ở BHA, F2 thêm dòng, chọn hàng
 * bằng ô tìm kiếm, đổi ĐVT, thành tiền tính ngay, Ctrl+S lưu và cấp số; hủy phiếu nháp.
 * Mặt hàng và NCC tạo sẵn qua API (ở tổng công ty), NCC được áp dụng tại BHA.
 */

test('lập phiếu nhập mua nháp, tính tiền, lưu cấp số, hủy phiếu', async ({ page, request }) => {
  const head = await api(request)
  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const uom = (code: string) => uoms.find((u) => u.code === code)!.id
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const category = await head.send<{ id: string; code: string }>('post', '/api/item-categories', { code: unique('G'), name: 'Nhóm phiếu nhập', parentId: null, inventoryAccount: null })
  const itemCode = `${category.code}-01`
  await head.send('post', '/api/items', {
    code: itemCode, name: 'Ốc vít thử phiếu nhập', englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: uom('HOP'),
    specification: null, brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, trackLot: false,
    trackExpiry: false, minStock: null, maxStock: null, conversions: [{ uomId: uom('THUNG'), factor: 24 }],
  })
  const supplierCode = unique('NCC')
  const supplier = await head.send<{ id: string }>('post', '/api/suppliers', {
    code: supplierCode, groupId: groups.find((g) => g.code === 'TN')!.id, name: `NCC phiếu nhập ${supplierCode}`, shortName: null,
    isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Approved', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })
  const bha = await api(request, 'BHA')
  await bha.send('put', `/api/suppliers/${supplier.id}/company`, { localCode: null, startDate: null, isActive: true })

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'GRN')
  await expect(page.getByRole('heading', { name: 'Phiếu nhập mua từ NCC (GRN)' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await expect(page).toHaveURL(/\/kho\/grn\/moi$/)
  await expect(page.getByText('(số phiếu tự cấp khi lưu)')).toBeVisible()

  await chooseOption(page, 'Nhà cung cấp', supplierCode)
  await page.getByLabel('Số hóa đơn').fill('0000456')

  // F2 thêm dòng; gõ mã để tìm hàng ở server.
  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(itemCode.toLowerCase())
  await pickOption(page, itemCode)
  await page.getByLabel('ĐVT dòng 1').click()
  await pickOption(page, 'THUNG')
  await page.getByLabel('Số lượng dòng 1').fill('2')
  await page.getByLabel('Đơn giá dòng 1').fill('480000')
  await page.getByLabel('Đơn giá dòng 1').press('Tab')
  const row = page.locator('.line-grid-row').first()
  await expect(row).toContainText('48 HOP')
  await expect(row).toContainText('960.000')
  await expect(page.locator('.document-form-footer')).toContainText('1.056.000')

  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu GRN-BHA-\d{4}-\d{4}\./)
  await expect(page).toHaveURL(/\/kho\/grn\/[0-9a-f-]{36}$/)
  await expect(page.locator('.document-form-number')).toHaveText(/^GRN-BHA-\d{4}-\d{4}$/)
  const number = await page.locator('.document-form-number').textContent()
  await page.screenshot({ path: 'e2e/screenshots/goods-receipt.png' })

  // Ghi sổ: phiếu thành chỉ xem, có nút Bỏ ghi sổ; bỏ ghi sổ thì về nháp.
  await page.getByRole('button', { name: 'Ghi sổ' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Ghi sổ' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã ghi sổ phiếu ${number}.`)
  await expect(page.locator('.document-form-title')).toContainText('Đã ghi sổ')
  await expect(page.getByRole('button', { name: 'Lưu nháp' })).toHaveCount(0)
  await expect(page.getByRole('dialog')).toHaveCount(0)
  // Phiếu đã ghi sổ chỉ xem: ô nhập bị khóa.
  await expect(page.getByLabel('Số hóa đơn')).toBeDisabled()
  await page.screenshot({ path: 'e2e/screenshots/goods-receipt-posted.png' })
  await page.getByRole('button', { name: 'Bỏ ghi sổ' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Bỏ ghi sổ' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã bỏ ghi sổ phiếu ${number}.`)
  await expect(page.locator('.document-form-title')).toContainText('Nháp')
  await expect(page.getByRole('button', { name: 'Lưu nháp' })).toBeVisible()

  await page.getByRole('button', { name: 'Hủy phiếu' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Hủy phiếu' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã hủy phiếu ${number}.`)
  await expect(page.locator('.document-form-title')).toContainText('Đã hủy')
  await expect(page.getByRole('button', { name: 'Lưu nháp' })).toHaveCount(0)

  await page.getByRole('button', { name: 'Đóng' }).click()
  await expect(page.locator('.data-grid-row', { hasText: number! })).toContainText('Đã hủy')
})
