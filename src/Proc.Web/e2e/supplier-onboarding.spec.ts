import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, chooseOption, openByCommand, unique } from './inventoryHelpers.ts'

/**
 * Hồ sơ đánh giá NCC mới (HSN – design.md mục 18.25, lát G8): Phòng cung ứng (quản trị ở tổng công ty) lập hồ sơ cho NCC Tiềm năng, khai giấy
 * phép kinh doanh, chấm 6 tiêu chí (điểm tạm tính), lưu, hoàn thành đánh giá – hồ sơ chờ Văn phòng Ban, người đánh giá không tự đề xuất.
 * Đề xuất, rút, Hội đồng duyệt/từ chối và quy tắc bắt buộc hồ sơ kiểm ở test tích hợp SupplierOnboardingTests.
 */
test('lập hồ sơ đánh giá NCC mới, chấm điểm, hoàn thành đánh giá, chụp màn hình', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const tag = unique('hs')
  const supplier = await head.send<{ id: string; code: string }>('post', '/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: `NCC hồ sơ mới ${tag}`, shortName: null,
    isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Potential', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })
  const category = await head.send<{ id: string; code: string }>('post', '/api/item-categories', {
    code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Rau củ hồ sơ ${tag}`, parentId: null, inventoryAccount: null,
  })

  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'HSN')
  await expect(page.getByRole('heading', { name: 'Hồ sơ đánh giá NCC mới' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await chooseOption(page, 'Nhà cung cấp', supplier.code)
  const categoryBox = page.getByRole('combobox', { name: 'Nhóm hàng đề xuất' })
  await categoryBox.click()
  await categoryBox.fill(category.code)
  await page.locator('.ant-select-dropdown:visible .ant-select-tree-title', { hasText: category.code }).click()
  await page.getByLabel('Nguồn tìm thấy NCC').fill('Hội chợ nông sản')
  await page.getByLabel('Ghi chú', { exact: true }).fill('Ưu tiên rau sạch VietGAP')

  // Lưới hồ sơ pháp lý: 5 giấy tờ cố định, dòng 1 là giấy phép kinh doanh (bắt buộc khi Đạt).
  await expect(page.getByText('Giấy phép kinh doanh, mã số thuế (bắt buộc khi Đạt)')).toBeVisible()
  await page.getByLabel('Đã nộp dòng 1').check()
  await page.getByLabel('Ghi chú giấy tờ dòng 1').fill('MST 0312345678')

  // Tab Đánh giá: 4, 5, 4, 3, 4, 3 → 23 ÷ 6 = 3,83 (tạm tính).
  await page.getByRole('tab', { name: 'Đánh giá' }).click()
  const scores = ['4', '5', '4', '3', '4', '3']
  const criteria = ['Giá chào, tổng chi phí', 'Chất lượng hàng mẫu', 'Năng lực cung ứng', 'Khả năng giao hàng', 'Công nợ, thanh toán', 'Pháp lý, năng lực tài chính']
  for (const [i, label] of criteria.entries()) await page.getByLabel(label, { exact: true }).fill(scores[i])
  await page.getByLabel('Pháp lý, năng lực tài chính', { exact: true }).press('Tab')
  await expect(page.locator('.document-form').getByText('3,83')).toBeVisible()
  await page.getByRole('combobox', { name: 'Kết luận' }).click()
  await page.locator('.ant-select-dropdown:visible').getByTitle('Đạt', { exact: true }).click()

  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu hồ sơ HSN-TCT-\d{4}-\d{4}\./)
  await expect(page.locator('.document-form-number')).toHaveText(/^HSN-TCT-\d{4}-\d{4}$/)

  await page.getByRole('button', { name: /Hoàn thành đánh giá$/ }).click()
  await page.locator('.ant-modal-confirm').getByRole('button', { name: /Hoàn thành$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã hoàn thành đánh giá hồ sơ HSN-TCT/)
  await expect(page.locator('.document-form-title')).toContainText('Đã đánh giá')
  await expect(page.getByRole('alert').filter({ hasText: 'chờ Văn phòng Ban đề xuất' })).toBeVisible()
  // Người đánh giá không tự đề xuất (tách Phòng cung ứng – Văn phòng Ban); còn mở lại đánh giá, hủy được.
  await expect(page.getByRole('button', { name: /Đề xuất Hội đồng$/ })).toHaveCount(0)
  await expect(page.getByRole('button', { name: /Mở lại đánh giá$/ })).toBeVisible()
  await expect(page.getByRole('button', { name: /(^| )Lưu nháp$/ })).toHaveCount(0)
  await page.getByRole('tab', { name: 'Đánh giá' }).click()
  await page.screenshot({ path: 'e2e/screenshots/supplier-onboarding.png' })
  await page.getByRole('tab', { name: 'Đề xuất' }).click()
  await expect(page.getByText('Chưa có ý kiến', { exact: true })).toBeVisible()

  await page.getByRole('button', { name: /(^| )Đóng$/ }).click()
  await expect(page.locator('.data-grid-row', { hasText: supplier.code })).toContainText('Đã đánh giá')
})
