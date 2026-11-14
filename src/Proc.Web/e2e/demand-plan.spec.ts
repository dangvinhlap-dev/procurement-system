import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, chooseOption, openByCommand, pickOption, unique } from './inventoryHelpers.ts'

/**
 * Kế hoạch nhu cầu (KHN – design.md mục 18.25, lát G5a): lập phiếu năm sau theo nhóm hàng, nhập SL vài tháng – tổng quý, cả năm tự tính;
 * lưu, chốt, điều chỉnh (phiếu mới ghi rõ điều chỉnh phiếu nào). Quy tắc trùng mặt hàng, hủy, quyền kiểm tra ở test tích hợp DemandPlanTests.
 */
test('lập kế hoạch nhu cầu 12 tháng, chốt, điều chỉnh, chụp màn hình', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const tag = unique('kh')
  const category = await head.send<{ id: string; code: string }>('post', '/api/item-categories', {
    code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Nhóm kế hoạch ${tag}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: `Găng tay ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id,
    baseUomId: uoms.find((u) => u.code === 'CAI')!.id, specification: null, brand: null, origin: null, barcode: null, vatCode: '10',
    technicalSpecs: null, trackLot: false, trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })

  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'KHN')
  await expect(page.getByRole('heading', { name: 'Kế hoạch nhu cầu' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await chooseOption(page, 'Nhóm hàng', category.code)
  await page.getByLabel('Ghi chú', { exact: true }).fill('Theo định mức năm sau')

  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(item.code.toLowerCase())
  await pickOption(page, item.code)
  await page.getByLabel('Tháng 1 dòng 1').fill('100')
  await page.getByLabel('Tháng 2 dòng 1').fill('1.500')
  await page.getByLabel('Tháng 4 dòng 1').fill('20')
  await page.getByLabel('Tháng 4 dòng 1').press('Tab')
  // Quý I = 100 + 1.500; cả năm = 1.620 – tự tính, chỉ đọc.
  const row = page.locator('.line-grid-row').first()
  await expect(row).toContainText('1.600')
  await expect(row).toContainText('1.620')

  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu KHN-[A-Z]+-\d{4}-\d{4}\./)
  await expect(page.locator('.document-form-number')).toHaveText(/^KHN-[A-Z]+-\d{4}-\d{4}$/)
  await expect(page.getByLabel('Tháng 2 dòng 1')).toHaveValue('1.500')
  const firstNo = (await page.locator('.document-form-number').textContent())!

  await page.getByRole('button', { name: /(^| )Chốt$/ }).click()
  await page.locator('.ant-modal-confirm').getByRole('button', { name: /Chốt$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã chốt phiếu KHN/)
  await expect(page.locator('.document-form-title')).toContainText('Đã chốt')
  await expect(page.getByRole('button', { name: /(^| )Lưu nháp$/ })).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/demand-plan.png' })

  // Điều chỉnh: phiếu nháp mới chép nội dung, ghi rõ điều chỉnh phiếu nào.
  await page.getByRole('button', { name: /Điều chỉnh$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã lập phiếu điều chỉnh KHN/)
  await expect(page.getByRole('alert').filter({ hasText: `Phiếu điều chỉnh của ${firstNo}` })).toBeVisible()
  await expect(page.getByLabel('Tháng 2 dòng 1')).toHaveValue('1.500')

  await page.getByRole('button', { name: /(^| )Đóng$/ }).click()
  // Hai dòng có số phiếu cũ: chính phiếu đó (Đã chốt) và phiếu điều chỉnh (Nháp, cột "Điều chỉnh phiếu").
  const rows = page.locator('.data-grid-row', { hasText: firstNo })
  await expect(rows).toHaveCount(2)
  await expect(rows.filter({ hasText: 'Đã chốt' })).toHaveCount(1)
  await expect(rows.filter({ hasText: 'Nháp' })).toHaveCount(1)
})
