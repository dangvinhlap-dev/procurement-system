import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, chooseOption, createLeafCategory, pickOption, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Ô chọn mặt hàng trên PR, PO tìm được mặt hàng loại **Dịch vụ** (lỗi người dùng báo 18/11/2026: mã dịch vụ MAIN-00001 không chọn được).
 * Trước đây ô chọn mặc định chỉ hàng tồn kho – lọc của phiếu kho, đã gỡ ở lát K3 (design.md mục 19.4). Kiểm tra cả cách gõ một phần mã
 * và gõ `*` kiểu SAP ECC.
 */
test('PR, PO: chọn được mặt hàng dịch vụ trong ô Mặt hàng', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const cai = (await head.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'CAI')!.id
  const tag = unique('DV')
  const category = await createLeafCategory<{ id: string }>(head, {
    code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Nhóm dịch vụ ${tag}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: `Sửa chữa máy phát ${tag}`, englishName: null, type: 'Service', categoryId: category.id, baseUomId: cai, specification: null,
    brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, conversions: [],
  })

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')

  // PO trước (phiếu PR nháp chưa lưu sẽ hỏi lại khi rời trang): gõ `*` + một phần tên vẫn liệt kê dịch vụ.
  await page.goto('/mua-hang/po/moi')
  await page.getByRole('button', { name: 'Thêm dòng' }).first().click()
  const poItem = page.getByLabel('Mặt hàng dòng 1')
  await poItem.click()
  await poItem.fill(`*${tag.toLowerCase()}`)
  await poItem.press('Enter')
  await pickOption(page, item.code)
  await expect(page.locator('.line-grid-row').first()).toContainText(`Sửa chữa máy phát ${tag}`)
  await page.screenshot({ path: 'e2e/screenshots/service-item-po.png' })

  // PR loại DV: gõ mã dịch vụ, chọn được.
  await page.goto('/mua-hang/pr/moi')
  await chooseOption(page, 'Loại mua', 'DV – Dịch vụ')
  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(item.code.toLowerCase())
  await pickOption(page, item.code)
  await expect(page.locator('.line-grid-row').first()).toContainText(`Sửa chữa máy phát ${tag}`)
  await page.screenshot({ path: 'e2e/screenshots/service-item-pr.png' })
})
