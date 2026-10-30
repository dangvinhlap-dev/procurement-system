import { expect, test, type APIRequestContext } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, chooseOption, openByCommand, pickOption, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Phiếu xuất trả NCC (design.md mục 10.5) và phiếu chuyển kho nội bộ (mục 10.6) trên khuôn DocumentForm, ở công ty BHA.
 * Dữ liệu chuẩn bị qua API: mặt hàng, NCC và một phiếu nhập 10 hộp vào kho tổng KT đã ghi sổ.
 */

/** Tạo mặt hàng + NCC áp dụng tại BHA, nhập 10 HOP giá 50.000 (VAT 10%) vào kho KT và ghi sổ. */
async function seedPostedReceipt(request: APIRequestContext) {
  const head = await api(request)
  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const hop = uoms.find((u) => u.code === 'HOP')!.id
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const category = await head.send<{ id: string; code: string }>('post', '/api/item-categories', { code: unique('R'), name: 'Nhóm trả/chuyển', parentId: null, inventoryAccount: null })
  const itemCode = `${category.code}-01`
  const item = await head.send<{ id: string }>('post', '/api/items', {
    code: itemCode, name: 'Bulông thử trả chuyển', englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: hop,
    specification: null, brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, trackLot: false,
    trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })
  const supplierCode = unique('NCC')
  const supplier = await head.send<{ id: string }>('post', '/api/suppliers', {
    code: supplierCode, groupId: groups.find((g) => g.code === 'TN')!.id, name: `NCC trả hàng ${supplierCode}`, shortName: null,
    isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Approved', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })
  const bha = await api(request, 'BHA')
  await bha.send('put', `/api/suppliers/${supplier.id}/company`, { localCode: null, startDate: null, isActive: true })
  const warehouses = await bha.get<{ id: string; code: string }[]>('/api/warehouses')
  const receipt = await bha.send<{ id: string; no: string }>('post', '/api/goods-receipts', {
    date: new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }),
    warehouseId: warehouses.find((w) => w.code === 'KT')!.id, supplierId: supplier.id, invoiceNo: null, invoiceSeries: null,
    invoiceDate: null, deliveryNoteNo: null, deliverer: null, note: null,
    lines: [{ id: null, itemId: item.id, uomId: hop, qty: 10, unitPrice: 50000, vatCode: '10', lotNo: null, expiryDate: null, note: null }],
  })
  await bha.send('post', `/api/inventory-documents/${receipt.id}/post`, {})
  return { itemCode, receiptNo: receipt.no }
}

test('trả NCC theo phiếu nhập gốc, rồi chuyển phần còn lại sang kho phụ', async ({ page, request }) => {
  const { itemCode, receiptNo } = await seedPostedReceipt(request)

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')

  // Phiếu trả NCC: chọn phiếu nhập gốc, lấy mọi dòng còn trả được (10), sửa còn 4.
  await openByCommand(page, 'RTV')
  await expect(page.getByRole('heading', { name: 'Phiếu xuất trả hàng NCC (RTV)' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await expect(page).toHaveURL(/\/kho\/rtv\/moi$/)
  await chooseOption(page, 'Phiếu nhập gốc', receiptNo)
  await expect(page.getByLabel('Kho xuất / NCC')).toHaveValue(/^KT \/ NCC/)
  await page.getByRole('button', { name: 'Lấy mọi dòng còn trả được' }).click()
  const returnRow = page.locator('.line-grid-row').first()
  await expect(returnRow).toContainText(itemCode)
  await expect(returnRow).toContainText('50.000')
  await page.getByLabel('SL trả dòng 1').fill('4')
  await page.getByLabel('SL trả dòng 1').press('Tab')
  await expect(returnRow).toContainText('200.000')
  await expect(page.locator('.document-form-footer')).toContainText('220.000')

  await page.getByRole('button', { name: 'Ghi sổ' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Ghi sổ' }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã ghi sổ phiếu RTV-BHA-\d{4}-\d{4}\./)
  await expect(page).toHaveURL(/\/kho\/rtv\/[0-9a-f-]{36}$/)
  await expect(page.locator('.document-form-title')).toContainText('Đã ghi sổ')
  await page.screenshot({ path: 'e2e/screenshots/goods-return.png' })
  const returnNo = await page.locator('.document-form-number').textContent()
  await page.getByRole('button', { name: 'Đóng' }).click()
  await expect(page.locator('.data-grid-row', { hasText: returnNo! })).toContainText('Đã ghi sổ')

  // Phiếu chuyển kho KT → KP1: tồn kho xuất còn 6; gõ 7 thì cảnh báo vượt tồn, sửa 6 rồi ghi sổ.
  await openByCommand(page, 'TRF')
  await expect(page.getByRole('heading', { name: 'Phiếu chuyển kho nội bộ (TRF)' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await chooseOption(page, 'Kho xuất', 'KT – ')
  await chooseOption(page, 'Kho nhập', 'KP1 – ')
  await page.getByLabel('Người nhận').fill('Tổ trưởng xưởng cán 1')
  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(itemCode.toLowerCase())
  await pickOption(page, itemCode)
  await page.getByLabel('Số lượng dòng 1').fill('7')
  await page.getByLabel('Số lượng dòng 1').press('Tab')
  const transferRow = page.locator('.line-grid-row').first()
  await expect(transferRow.locator('.line-grid-warning')).toHaveText('6')
  await page.getByLabel('Số lượng dòng 1').fill('6')
  await page.getByLabel('Số lượng dòng 1').press('Tab')
  await expect(transferRow.locator('.line-grid-warning')).toHaveCount(0)

  await page.getByRole('button', { name: 'Ghi sổ' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Ghi sổ' }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã ghi sổ phiếu TRF-BHA-\d{4}-\d{4}\./)
  await expect(page.locator('.document-form-title')).toContainText('Đã ghi sổ')
  await page.screenshot({ path: 'e2e/screenshots/stock-transfer.png' })
  const transferNo = await page.locator('.document-form-number').textContent()
  await page.getByRole('button', { name: 'Đóng' }).click()
  const listRow = page.locator('.data-grid-row', { hasText: transferNo! })
  await expect(listRow).toContainText('KP1')
  await page.screenshot({ path: 'e2e/screenshots/stock-transfer-list.png' })
})
