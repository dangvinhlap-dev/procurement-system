import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, unique, createLeafCategory } from './inventoryHelpers.ts'

/**
 * Ý kiến thẩm định của thành viên chuyên môn (lát H1 – design.md mục 18.25): quản trị lập RFQ ở tổng công ty; một người khác (không phải người
 * lập) mở tab THẨM ĐỊNH, ghi ý kiến Không đạt kèm file (chờ, ghi xong tự tải lên), tab hiện số ý kiến không đạt; mở đính kèm của ý kiến; rút
 * ý kiến có lý do thì tổng hợp về 0 nhưng dòng vẫn hiện. Quy tắc máy chủ (người lập không tự thẩm định, chủ đã quyết thì khóa) ở test tích hợp.
 */
test('ghi ý kiến thẩm định Không đạt kèm file trên RFQ, rút ý kiến có lý do, chụp màn hình', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const tag = unique('td')
  const cai = (await head.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'CAI')!.id
  const category = await createLeafCategory<{ id: string }>(head, {
    code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Nhóm thẩm định ${tag}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string }>('post', '/api/items', {
    code: null, name: `Van an toàn ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id,
    baseUomId: cai, specification: null, brand: null, origin: null, barcode: null, vatCode: '10',
    technicalSpecs: null, trackLot: false, trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })
  const today = new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const in7 = new Date(Date.now() + 7 * 86_400_000).toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const rfq = await head.send<{ id: string; no: string }>('post', '/api/rfqs', {
    date: today, title: `RFQ thẩm định ${tag}`, deadline: in7, sourcingId: null,
    weights: { price: 60, quality: 20, delivery: 10, payment: 5, warranty: 5 }, note: null,
    lines: [{ id: null, itemId: item.id, uomId: cai, qty: 10, sourcePrLineId: null, note: null }], supplierIds: [],
  })

  // Người thẩm định: tài khoản khác người lập RFQ (người lập không tự thẩm định).
  const roles = await head.get<{ id: string; code: string }[]>('/api/roles')
  const userName = unique('td').toLowerCase()
  await head.send('post', '/api/users', {
    userName, fullName: `Thẩm định viên ${tag}`, email: null, password: 'Demo@2026',
    access: { roleIds: [roles.find((r) => r.code === 'ADMIN')!.id], warehouseIds: [], itemCategoryIds: [] },
  })
  await loginViaApi(page, { userName, password: 'Demo@2026' })
  await page.goto(`/dau-thau/rfq/${rfq.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(rfq.no)
  await page.getByRole('tab', { name: 'Thẩm định' }).click()
  await expect(page.getByRole('table', { name: 'Ý kiến thẩm định' })).toContainText('Chưa có ý kiến thẩm định.')

  await page.getByRole('button', { name: 'Ghi ý kiến thẩm định' }).click()
  const dialog = page.getByRole('dialog', { name: 'Ghi ý kiến thẩm định' })
  await dialog.getByRole('button', { name: 'Ghi ý kiến' }).click()
  await expect(dialog).toContainText('Chọn Đạt hoặc Không đạt.')
  await dialog.getByLabel('Khía cạnh thẩm định').click()
  await page.locator('.ant-select-dropdown:visible').getByText('Pháp lý', { exact: true }).click()
  await dialog.getByLabel('Không đạt').check()
  await dialog.getByLabel('Nội dung ý kiến').fill('Hồ sơ thầu thiếu điều khoản phạt chậm giao')
  await dialog.locator('input[type=file]').setInputFiles({ name: 'bien-ban-tham-dinh.txt', mimeType: 'text/plain', buffer: Buffer.from('bien ban') })
  await expect(dialog).toContainText('bien-ban-tham-dinh.txt')
  await page.screenshot({ path: 'e2e/screenshots/appraisal-dialog.png' })
  await dialog.getByRole('button', { name: 'Ghi ý kiến' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã ghi ý kiến thẩm định.')

  const table = page.getByRole('table', { name: 'Ý kiến thẩm định' })
  await expect(page.getByRole('tab', { name: 'Thẩm định (1, 1 không đạt)' })).toBeVisible()
  await expect(table).toContainText('Không đạt')
  await expect(table).toContainText('Pháp lý')
  await expect(table).toContainText(`Thẩm định viên ${tag}`)
  await expect(page.getByLabel('Tổng hợp ý kiến thẩm định')).toContainText('Không đạt: 1')
  await page.screenshot({ path: 'e2e/screenshots/appraisal.png' })

  await table.getByRole('button', { name: 'Đính kèm của ý kiến' }).click()
  const files = page.getByRole('dialog', { name: /Đính kèm – ý kiến của/ })
  await expect(files).toContainText('bien-ban-tham-dinh.txt')
  await page.keyboard.press('Escape')
  await expect(files).toBeHidden()

  // Rút: thiếu lý do báo lỗi; đủ lý do thì dòng hiện "Đã rút", tổng hợp về 0.
  await table.getByRole('button', { name: 'Rút' }).click()
  const confirm = page.locator('.ant-modal-confirm', { hasText: 'Rút ý kiến thẩm định?' })
  await confirm.getByRole('button', { name: 'Rút ý kiến' }).click()
  await expect(page.locator('.ant-message')).toContainText('Hãy nhập lý do rút.')
  await confirm.getByLabel('Lý do rút').fill('Đã bổ sung điều khoản')
  await confirm.getByRole('button', { name: 'Rút ý kiến' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã rút ý kiến thẩm định.')
  await expect(table).toContainText('Đã bổ sung điều khoản')
  await expect(page.getByLabel('Tổng hợp ý kiến thẩm định')).toContainText('Không đạt: 0')
  await expect(page.getByRole('tab', { name: 'Thẩm định', exact: true })).toBeVisible()
})
