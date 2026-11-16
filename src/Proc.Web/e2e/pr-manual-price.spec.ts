import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, chooseOption, createLeafCategory, pickOption, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Đơn giá dự kiến trên đề nghị mua (design.md mục 10.2, hướng B người dùng chốt 16/11/2026): mặt hàng mới tạo chưa có giá nhận hàng → ô
 * "Giá tham khảo" mở cho người lập nhập (tam giác vàng); gửi duyệt khi chưa nhập thì máy chủ chặn; nhập 150.000 → lưu, gửi duyệt được. Phiếu
 * chờ duyệt: đầu phiếu cảnh báo người duyệt "Giá tham khảo do người lập đề nghị tự nhập", ô giá có tam giác vàng; trang duyệt gọn `/duyet`
 * cũng cảnh báo. Trường hợp có giá nhận hàng (máy chủ bỏ qua giá gửi lên) kiểm tra ở test tích hợp PurchasingTests.
 */
test('PR: mặt hàng chưa có giá nhận hàng – người lập nhập đơn giá dự kiến, người duyệt thấy cảnh báo', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const bha = await api(request, 'BHA')
  const cai = (await bha.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'CAI')!.id
  const tag = unique('GD')
  const category = await createLeafCategory<{ id: string }>(head, {
    code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Nhóm giá dự kiến ${tag}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: `Đầu dò nhiệt ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: cai, specification: null,
    brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, conversions: [],
  })
  const deptCode = unique('GD').slice(0, 10)
  await bha.send('post', '/api/departments', { code: deptCode, name: `Phòng giá dự kiến ${deptCode}`, managerName: null })

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto('/mua-hang/pr/moi')
  await chooseOption(page, 'Bộ phận đề nghị', deptCode)
  await page.getByLabel('Mục đích sử dụng').fill('Thay đầu dò lò sấy')
  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(item.code.toLowerCase())
  await pickOption(page, item.code)
  await page.getByLabel('SL cần sử dụng dòng 1').fill('10')

  // Chưa có giá nhận hàng: ô nhập đơn giá dự kiến hiện ra; lưu nháp được khi còn trống, gửi duyệt thì bị chặn.
  const price = page.getByLabel('Đơn giá dự kiến dòng 1')
  await expect(price).toBeVisible()
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu PR-BHA-\d{4}-\d{4}\./)
  await page.getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  await page.getByRole('dialog').getByRole('button', { name: /(^| )Gửi duyệt$/ }).click()
  await expect(page.getByText(/Chưa có đơn giá dự kiến ở dòng 1/).first()).toBeVisible()

  await price.fill('150000')
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu/)
  await expect(page.locator('.line-grid-row').first()).toContainText('1.500.000')
  await page.screenshot({ path: 'e2e/screenshots/pr-manual-price-draft.png' })
  const id = page.url().split('/').pop()!
  await bha.send('post', `/api/purchase-requests/${id}/submit`, {})

  // Người duyệt mở phiếu chờ duyệt: cảnh báo đầu phiếu + tam giác vàng ở ô giá.
  await page.reload()
  await expect(page.getByTestId('manual-price-notice')).toContainText('Giá tham khảo do người lập đề nghị tự nhập')
  await expect(page.getByTestId('manual-price-notice')).toContainText(`dòng 1 ${item.code}`)
  await expect(page.getByTestId('price-manual-1')).toContainText('150.000')
  await page.screenshot({ path: 'e2e/screenshots/pr-manual-price-approver.png' })

  // Trang duyệt gọn trên điện thoại cũng cảnh báo.
  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto(`/duyet/PR/${id}`)
  await expect(page.getByTestId('manual-price-notice')).toContainText('Giá tham khảo do người lập đề nghị tự nhập')
  await expect(page.locator('.erp-mobile-lines')).toContainText('Giá do người lập tự nhập')
  await page.screenshot({ path: 'e2e/screenshots/pr-manual-price-mobile.png', fullPage: true })
})
