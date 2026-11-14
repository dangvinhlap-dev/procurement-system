import { expect, test, type APIRequestContext } from '@playwright/test'
import { ADMIN, loginViaApi, openInCurrentTab } from './helpers.ts'

/**
 * Màn hình Nhóm hàng CAT – cây tối đa 4 cấp (lát N4b; design.md mục 5.7 N4-1…N4-9, bản vẽ duyệt 14/11/2026): cột Cấp có ô số màu, nút
 * "Thêm nhóm con" trên dòng đang chọn tự khóa ở nhóm cấp 4, ngăn kéo có ô "Cấp n/4", đường dẫn bấm được, tab NHÓM CON; thu/mở nhánh.
 */

/** Mã nhóm ngẫu nhiên 2–20 chữ/số (mã do người dùng gõ – N4-7). */
const newCode = (prefix: string) => `${prefix}${crypto.randomUUID().replace(/-/g, '').slice(0, 8).toUpperCase()}`

/** Tạo nhóm hàng qua API bằng tài khoản quản trị ở tổng công ty. */
async function createCategory(request: APIRequestContext, input: { code: string; name: string; parentId: string | null }) {
  const login = await request.post('/api/auth/login', { data: { userName: ADMIN.userName, password: ADMIN.password } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { defaultCompanyId: string } }
  const response = await request.post('/api/item-categories', {
    headers: { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.defaultCompanyId },
    data: { ...input, inventoryAccount: null },
  })
  expect(response.ok(), await response.text()).toBeTruthy()
  return (await response.json()) as { id: string; code: string; level: number }
}

test('CAT: thêm nhóm con đến cấp 4, cấp 4 khóa thêm nhóm con, đường dẫn và thu/mở nhánh', async ({ page, request }, testInfo) => {
  const tag = crypto.randomUUID().slice(0, 8)
  const l1 = await createCategory(request, { code: newCode('NA'), name: `Ngành thử ${tag}`, parentId: null })
  const l2 = await createCategory(request, { code: newCode('NB'), name: `Nhóm thử ${tag}`, parentId: l1.id })
  const l3 = await createCategory(request, { code: newCode('NC'), name: `Loại thử ${tag}`, parentId: l2.id })
  expect(l3.level).toBe(3)

  await loginViaApi(page)
  await page.goto('/')
  await openInCurrentTab(page, 'CAT')
  await expect(page.getByRole('heading', { name: 'Nhóm hàng' })).toBeVisible()
  await page.getByLabel('Tìm kiếm', { exact: true }).fill(tag)
  const row = (code: string) => page.locator('.data-grid-row', { hasText: code })
  await expect(row(l3.code)).toContainText('Loại hàng')

  // Chọn nhóm cấp 3 → thêm nhóm con cấp 4 (Chủng loại), nhóm cha điền sẵn.
  await row(l3.code).click()
  await expect(page.getByTestId('category-selection-hint')).toContainText('Nhóm con thêm vào sẽ ở cấp 4 – Chủng loại')
  await page.getByRole('button', { name: 'Thêm nhóm con' }).click()
  const drawer = page.locator('.detail-drawer')
  await expect(drawer).toContainText(`Thêm nhóm con của ${l3.code}`)
  await expect(drawer.getByRole('img', { name: 'Cấp 4 – Chủng loại' }).first()).toBeVisible()
  await expect(drawer.getByLabel('Cấp', { exact: true })).toHaveValue('4 – Chủng loại')
  await expect(drawer.getByRole('navigation', { name: 'Đường dẫn nhóm hàng' })).toContainText(l1.code)
  const l4Code = newCode('ND')
  await drawer.getByLabel('Mã nhóm').fill(l4Code)
  await drawer.getByLabel('Tên nhóm hàng').fill(`Chủng loại thử ${tag}`)
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(`Đã lưu nhóm hàng ${l4Code}.`)
  await expect(drawer).toBeHidden()

  // Nhóm cấp 4 là cấp cuối (N4-3): nút "Thêm nhóm con" khóa, dòng gợi ý nêu lý do.
  await row(l4Code).click()
  await expect(page.getByRole('button', { name: 'Thêm nhóm con' })).toBeDisabled()
  await expect(page.getByTestId('category-selection-hint')).toContainText('đã ở cấp 4 (cấp cuối), không thêm nhóm con được')
  await page.screenshot({ path: testInfo.outputPath('cat-4-cap.png') })

  // Ngăn kéo nhóm cấp 4: tab NHÓM CON chỉ có dòng giải thích; bấm đường dẫn mở nhóm cấp 1.
  await row(l4Code).dblclick()
  await expect(drawer).toContainText(`Nhóm hàng: ${l4Code}`)
  await drawer.getByRole('tab', { name: /Nhóm con/ }).click()
  await expect(drawer).toContainText('không thêm nhóm con được')
  await expect(drawer.getByRole('button', { name: 'Thêm nhóm con' })).toHaveCount(0)
  await drawer.getByRole('tab', { name: 'Thông tin chung' }).click()
  await page.screenshot({ path: testInfo.outputPath('cat-ngan-keo.png') })
  await drawer.getByRole('button', { name: `${l1.code} – Ngành thử ${tag}` }).click()
  await expect(drawer).toContainText(`Nhóm hàng: ${l1.code}`)
  await expect(drawer.getByLabel('Cấp', { exact: true })).toHaveValue('1 – Ngành hàng')
  await drawer.getByRole('tab', { name: 'Nhóm con (1)' }).click()
  await expect(drawer.locator('.data-grid-row', { hasText: l2.code })).toBeVisible()
  await drawer.getByRole('button', { name: 'Đóng' }).click()

  // Thu nhánh cấp 1 thì ẩn cả nhánh con, mở lại thì hiện.
  await page.getByRole('button', { name: `Thu nhánh ${l1.code}` }).click()
  await expect(row(l2.code)).toHaveCount(0)
  await expect(row(l4Code)).toHaveCount(0)
  await page.getByRole('button', { name: `Mở nhánh ${l1.code}` }).click()
  await expect(row(l4Code)).toBeVisible()
})

test('Hàng hóa: ô Nhóm hàng chỉ chọn được nhóm cấp 4 (N4-5)', async ({ page, request }) => {
  const tag = crypto.randomUUID().slice(0, 8)
  const l1 = await createCategory(request, { code: newCode('HA'), name: `Ngành chọn ${tag}`, parentId: null })
  const l2 = await createCategory(request, { code: newCode('HB'), name: `Nhóm chọn ${tag}`, parentId: l1.id })
  const l3 = await createCategory(request, { code: newCode('HC'), name: `Loại chọn ${tag}`, parentId: l2.id })
  const l4 = await createCategory(request, { code: newCode('HD'), name: `Chủng loại chọn ${tag}`, parentId: l3.id })

  await loginViaApi(page)
  await page.goto('/')
  await openInCurrentTab(page, 'ITEM')
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  const drawer = page.locator('.detail-drawer')
  await expect(drawer).toContainText('Chỉ chọn nhóm cấp 4 – Chủng loại.')
  const box = drawer.getByLabel('Nhóm hàng', { exact: true })
  await box.click()
  await box.pressSequentially(tag)
  const node = (code: string) => page.locator('.ant-select-dropdown:visible .ant-select-tree-treenode', { hasText: code })
  // Nhóm cấp 3 hiện để mở nhánh nhưng bị khóa, ghi lý do; nhóm cấp 4 chọn được.
  await expect(node(l3.code)).toHaveClass(/ant-select-tree-treenode-disabled/)
  await expect(node(l3.code)).toContainText('chọn nhóm cấp 4')
  await expect(node(l4.code)).not.toHaveClass(/ant-select-tree-treenode-disabled/)
  await node(l4.code).locator('.ant-select-tree-title').click()
  await expect(drawer.locator('.ant-form-item', { has: page.getByLabel('Nhóm hàng', { exact: true }) })).toContainText(l4.code)
})
