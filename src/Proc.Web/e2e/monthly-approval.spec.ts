import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, approveSupplier, chooseOption, openByCommand, pickOption, unique } from './inventoryHelpers.ts'

/**
 * Duyệt NCC và giá tháng (GTH – design.md mục 18.25, lát G2): quản trị ở tổng công ty lập phiếu tháng sau × nhóm hàng cho TCT và BHA, dòng
 * mặt hàng có NCC chính, giá, NCC dự phòng, giá dự phòng, hạn mức SL; lưu, gửi duyệt (người lập không tự duyệt). Luồng duyệt, điều chỉnh,
 * công ty áp dụng chỉ xem kiểm tra ở test tích hợp MonthlySupplierApprovalTests. Nhóm hàng, mặt hàng, NCC tạo mới mỗi lần chạy.
 */
test('lập phiếu duyệt NCC và giá tháng, gửi duyệt, chụp màn hình', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const tag = unique('gt')
  const supplier = async (name: string) => {
    const s = await head.send<{ id: string; code: string }>('post', '/api/suppliers', {
      code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name, shortName: null,
      isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
      rating: null, status: 'Approved', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
    })
    await approveSupplier(request, s.id)
    return s.code
  }
  const mainCode = await supplier(`NCC chính tháng ${tag}`)
  const backupCode = await supplier(`NCC dự phòng tháng ${tag}`)
  const category = await head.send<{ id: string; code: string }>('post', '/api/item-categories', {
    code: null, name: `Nhóm duyệt tháng ${tag}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: `Giấy in A4 ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id,
    baseUomId: uoms.find((u) => u.code === 'CAI')!.id, specification: null, brand: null, origin: null, barcode: null, vatCode: '10',
    technicalSpecs: null, trackLot: false, trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })

  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'GTH')
  await expect(page.getByRole('heading', { name: 'Duyệt NCC và giá tháng' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()

  // Tháng sau (gõ tay MM/yyyy) để không trùng phiếu tháng này của lần chạy khác.
  const next = new Date()
  next.setMonth(next.getMonth() + 1, 1)
  const month = `${String(next.getMonth() + 1).padStart(2, '0')}/${next.getFullYear()}`
  const monthBox = page.getByLabel('Tháng áp dụng')
  await monthBox.click()
  await monthBox.fill(month)
  await monthBox.press('Enter')
  await chooseOption(page, 'Nhóm hàng', category.code)
  for (const code of ['TCT', 'BHA']) await chooseOption(page, 'Công ty áp dụng', code)
  await page.keyboard.press('Escape')
  await page.getByLabel('Ghi chú', { exact: true }).fill('Theo biên bản họp Ban cung ứng')

  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(item.code.toLowerCase())
  await pickOption(page, item.code)
  await chooseOption(page, 'NCC chính dòng 1', mainCode)
  await page.getByLabel('Giá NCC chính dòng 1').fill('85000')
  await chooseOption(page, 'NCC dự phòng dòng 1', backupCode)
  await page.getByLabel('Giá dự phòng dòng 1').fill('87500')
  await page.getByLabel('Hạn mức dòng 1').fill('1200')
  // Ngân hàng giá (G4b): giá chuẩn, giá trần không bắt buộc; giá duyệt không vượt trần.
  await page.getByLabel('Giá chuẩn dòng 1').fill('86000')
  await page.getByLabel('Giá trần dòng 1').fill('90000')
  await page.getByLabel('Hạn mức dòng 1').press('Tab')
  await expect(page.getByLabel('Giá NCC chính dòng 1')).toHaveValue('85.000')

  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu GTH-TCT-\d{4}-\d{4}\./)
  await expect(page.locator('.document-form-number')).toHaveText(/^GTH-TCT-\d{4}-\d{4}$/)
  await expect(page.getByLabel('Hạn mức dòng 1')).toHaveValue('1.200')
  await expect(page.getByLabel('Giá trần dòng 1')).toHaveValue('90.000')

  // Gửi duyệt có popup xem trước người duyệt; người lập không tự duyệt.
  await page.getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  const dialog = page.locator('.ant-modal-confirm')
  await expect(dialog).toContainText('Gửi Ban cung ứng duyệt')
  await dialog.getByRole('button', { name: /Gửi duyệt$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã gửi duyệt phiếu GTH/)
  await expect(page.locator('.document-form-title')).toContainText('Chờ duyệt')
  await expect(page.getByRole('button', { name: /(^| )Duyệt$/ })).toHaveCount(0)
  await expect(page.getByRole('button', { name: /Rút về nháp$/ })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/monthly-approval.png' })

  await page.getByRole('button', { name: /(^| )Đóng$/ }).click()
  await expect(page.locator('.data-grid-row', { hasText: category.code })).toContainText('Chờ duyệt')
})
