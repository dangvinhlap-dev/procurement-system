import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, chooseOption, openByCommand, pickOption, unique } from './inventoryHelpers.ts'

/**
 * Khảo sát giá thị trường (KSG – design.md mục 18.25, lát G4a): quản trị ở tổng công ty lập phiếu theo nhóm hàng, một mặt hàng hai nguồn giá
 * (NCC trong danh mục và cửa hàng ngoài), lưu, chốt – phiếu chỉ đọc. Quy tắc hủy, quyền, tra giá kiểm tra ở test tích hợp MarketPriceSurveyTests.
 */
test('lập phiếu khảo sát giá thị trường, chốt, chụp màn hình', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const tag = unique('ks')
  const supplier = await head.send<{ id: string; code: string }>('post', '/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: `NCC khảo sát ${tag}`, shortName: null,
    isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Potential', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })
  const category = await head.send<{ id: string; code: string }>('post', '/api/item-categories', {
    code: null, name: `Nhóm khảo sát ${tag}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: `Que hàn ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id,
    baseUomId: uoms.find((u) => u.code === 'CAI')!.id, specification: null, brand: null, origin: null, barcode: null, vatCode: '10',
    technicalSpecs: null, trackLot: false, trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })

  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'KSG')
  await expect(page.getByRole('heading', { name: 'Khảo sát giá thị trường' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await chooseOption(page, 'Nhóm hàng', category.code)
  await page.getByLabel('Ghi chú', { exact: true }).fill('Khảo sát định kỳ tháng')

  // Dòng 1: NCC trong danh mục (NCC tiềm năng cũng chọn được).
  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(item.code.toLowerCase())
  await pickOption(page, item.code)
  await chooseOption(page, 'NCC dòng 1', supplier.code)
  await page.getByLabel('Đơn giá dòng 1').fill('15000')
  // Dòng 2: nguồn ngoài danh mục.
  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 2').fill(item.code.toLowerCase())
  await pickOption(page, item.code)
  await page.getByLabel('Nguồn giá dòng 2').fill('Cửa hàng Bình Tây')
  await page.getByLabel('Đơn giá dòng 2').fill('14500')
  await page.getByLabel('Đơn giá dòng 2').press('Tab')

  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu KSG-TCT-\d{4}-\d{4}\./)
  await expect(page.locator('.document-form-number')).toHaveText(/^KSG-TCT-\d{4}-\d{4}$/)
  await expect(page.getByLabel('Đơn giá dòng 2')).toHaveValue('14.500')

  await page.getByRole('button', { name: /(^| )Chốt$/ }).click()
  await page.locator('.ant-modal-confirm').getByRole('button', { name: /Chốt$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã chốt phiếu KSG/)
  await expect(page.locator('.document-form-title')).toContainText('Đã chốt')
  await expect(page.getByRole('alert').filter({ hasText: 'đã vào ngân hàng giá' })).toBeVisible()
  await expect(page.getByRole('button', { name: /(^| )Lưu nháp$/ })).toHaveCount(0)
  await expect(page.getByRole('button', { name: /Hủy phiếu$/ })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/market-survey.png' })

  await page.getByRole('button', { name: /(^| )Đóng$/ }).click()
  await expect(page.locator('.data-grid-row', { hasText: category.code })).toContainText('Đã chốt')
})
