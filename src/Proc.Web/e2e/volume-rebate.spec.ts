import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, approveSupplier, chooseOption, openByCommand, pickOption, unique } from './inventoryHelpers.ts'

/**
 * Chiết khấu sản lượng gộp (CKB – design.md mục 18.25, lát G6a): quản trị ở tổng công ty lập thỏa thuận cho NCC × nhóm hàng, áp cho TCT và
 * BHA, dòng mặt hàng, tab Bậc chiết khấu hai bậc; lưu, xem tab Lũy kế (kỳ theo tháng), gửi duyệt (người lập không tự duyệt); mở màn hình
 * theo dõi bậc (CKBT). Lũy kế, chia chiết khấu, công ty áp dụng chỉ xem kiểm tra ở test tích hợp VolumeRebateTests. NCC, nhóm hàng, mặt hàng
 * tạo mới mỗi lần chạy nên không trùng thỏa thuận của lần chạy khác.
 */
test('lập thỏa thuận chiết khấu sản lượng gộp, xem lũy kế, gửi duyệt, chụp màn hình', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const tag = unique('ck')
  const supplier = await head.send<{ id: string; code: string }>('post', '/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: `NCC chiết khấu gộp ${tag}`, shortName: null,
    isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Approved', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })
  await approveSupplier(request, supplier.id)
  const category = await head.send<{ id: string; code: string }>('post', '/api/item-categories', {
    code: null, name: `Nhóm chiết khấu gộp ${tag}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: `Xi măng bao ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id,
    baseUomId: uoms.find((u) => u.code === 'CAI')!.id, specification: null, brand: null, origin: null, barcode: null, vatCode: '10',
    technicalSpecs: null, trackLot: false, trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })

  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'CKB')
  await expect(page.getByRole('heading', { name: 'Chiết khấu sản lượng gộp' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()

  await page.getByRole('textbox', { name: 'Tên thỏa thuận' }).fill(`Chiết khấu xi măng năm ${tag}`)
  await chooseOption(page, 'Nhà cung cấp', supplier.code)
  await chooseOption(page, 'Nhóm hàng', category.code)
  for (const code of ['TCT', 'BHA']) await chooseOption(page, 'Công ty áp dụng', code)
  await page.keyboard.press('Escape')
  await page.getByLabel('Bậc mục tiêu').fill('2')
  await page.getByLabel('Ghi chú', { exact: true }).fill('Theo thư chính sách chiết khấu của NCC')

  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(item.code.toLowerCase())
  await pickOption(page, item.code)

  // Tab Bậc chiết khấu: hai bậc 1.000 cái 1% – 2.000 cái 2%.
  await page.getByRole('tab', { name: 'Bậc chiết khấu' }).click()
  const tiers = page.getByRole('tabpanel', { name: 'Bậc chiết khấu' })
  for (const [i, [from, pct]] of [['1000', '1'], ['2000', '2']].entries()) {
    const n = i + 1
    await tiers.getByRole('button', { name: /Thêm dòng/ }).click()
    // Chờ dòng mới vẽ xong rồi mới gõ – đếm ngay sau khi bấm có thể còn ra số dòng cũ.
    await expect(tiers.getByLabel(/^Ngưỡng bậc \d+$/)).toHaveCount(n)
    await page.getByLabel(`Ngưỡng bậc ${n}`).fill(from)
    await page.getByLabel(`% chiết khấu bậc ${n}`).fill(pct)
  }
  await page.getByLabel('% chiết khấu bậc 2').press('Tab')

  await page.keyboard.press('Control+s')
  // Lần lưu đầu sau khi API khởi động có thể chậm (cấp số, nạp mô hình EF) – chờ lâu hơn mặc định.
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu thỏa thuận CKB-TCT-\d{4}-\d{4}\./, { timeout: 15_000 })
  await expect(page.locator('.document-form-number')).toHaveText(/^CKB-TCT-\d{4}-\d{4}$/)
  await expect(page.getByLabel('Ngưỡng bậc 2')).toHaveValue('2.000')

  // Tab Lũy kế: kỳ theo tháng trong năm, chưa có mua – chưa đạt bậc.
  await page.getByRole('tab', { name: 'Lũy kế' }).click()
  const progress = page.getByRole('table', { name: 'Lũy kế theo kỳ tính bậc' })
  await expect(progress.locator('tbody tr.ant-table-row')).toHaveCount(12)
  await expect(progress).toContainText('Chưa đạt')

  // Gửi duyệt có popup xem trước người duyệt; người lập không tự duyệt.
  await page.getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  const dialog = page.locator('.ant-modal-confirm')
  await expect(dialog).toContainText('Gửi Ban cung ứng duyệt')
  await dialog.getByRole('button', { name: /Gửi duyệt$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã gửi duyệt thỏa thuận CKB/)
  await expect(page.locator('.document-form-title')).toContainText('Chờ duyệt')
  await expect(page.getByRole('button', { name: /(^| )Duyệt$/ })).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/volume-rebate.png' })

  await page.getByRole('button', { name: /(^| )Đóng$/ }).click()
  await expect(page.locator('.data-grid-row', { hasText: supplier.code })).toContainText('Chờ duyệt')

  // Màn hình theo dõi bậc mở được (thỏa thuận chờ duyệt chưa theo dõi).
  await openByCommand(page, 'CKBT')
  await expect(page.getByRole('heading', { name: 'Theo dõi bậc chiết khấu sản lượng gộp' })).toBeVisible()
  await expect(page.locator('.data-grid-row', { hasText: supplier.code })).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/volume-rebate-tracking.png' })
})
