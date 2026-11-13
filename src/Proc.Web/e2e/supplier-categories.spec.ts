import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { ADMIN, loginViaApi, openInCurrentTab } from './helpers.ts'

/**
 * Ngành hàng NCC chọn từ danh mục Nhóm hàng (design.md mục 5.3, 5.8; người dùng chốt 07/11/2026): chọn nhiều nhóm, thiếu thì thêm ngay
 * trong ngăn kéo NCC (cảnh báo trùng tên, không cho tạo trùng trong cùng nhóm cha), lưu không cần duyệt, lọc danh sách NCC theo ngành hàng.
 */

const tag = () => crypto.randomUUID().slice(0, 8)

/** Gọi API bằng tài khoản quản trị ở tổng công ty. */
async function adminApi(request: APIRequestContext) {
  const login = await request.post('/api/auth/login', { data: { userName: ADMIN.userName, password: ADMIN.password } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { defaultCompanyId: string } }
  const headers = { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.defaultCompanyId }
  return {
    get: async <T,>(url: string) => (await (await request.get(url, { headers })).json()) as T,
    post: async <T,>(url: string, data: unknown) => {
      const response = await request.post(url, { headers, data })
      expect(response.ok(), await response.text()).toBeTruthy()
      return (await response.json()) as T
    },
  }
}

/** Mở màn hình bằng cách gõ mã vào ô lệnh rồi Enter. */
async function openByCommand(page: Page, code: string) {
  await openInCurrentTab(page, code)
}

test('ngành hàng NCC: chọn nhóm hàng, thêm nhóm thiếu (chặn trùng), lưu, lọc danh sách', async ({ page, request }) => {
  test.setTimeout(90_000)
  const api = await adminApi(request)
  const t = tag()
  const root = await api.post<{ id: string; code: string; name: string }>('/api/item-categories', {
    code: null, name: `Ngành cơ khí ${t}`, parentId: null, inventoryAccount: null,
  })
  const child = await api.post<{ id: string; code: string; name: string }>('/api/item-categories', {
    code: null, name: `Bu lông ${t}`, parentId: root.id, inventoryAccount: null,
  })
  const groups = await api.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const supplier = await api.post<{ id: string; code: string }>('/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: `NCC ngành hàng ${t}`, shortName: null, isForeign: false,
    taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: `Cơ khí chế tạo ${t}`,
    rating: null, status: 'Potential', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })

  await loginViaApi(page)
  await page.goto('/')
  const drawer = page.locator('.detail-drawer')
  await openByCommand(page, 'SUP')
  await page.getByPlaceholder(/Gõ từ khóa/).fill(supplier.code)
  await page.locator('.data-grid-row', { hasText: supplier.code }).dblclick()
  await expect(drawer.getByText(`Nhà cung cấp: ${supplier.code}`)).toBeVisible()
  // Chữ tự do cũ chỉ hiện để tham khảo.
  await expect(drawer).toContainText(`Cơ khí chế tạo ${t}`)

  // Chọn nhóm có sẵn: gõ không dấu.
  const field = drawer.getByRole('combobox', { name: 'Ngành hàng cung cấp' })
  await field.click()
  await field.fill(`bu long ${t}`)
  await page.locator('.ant-select-dropdown:visible .ant-select-tree-title', { hasText: child.code }).click()
  await field.press('Escape')

  // Thêm nhóm thiếu: gõ trùng tên nhóm đã có trong cùng nhóm cha → báo lỗi, không lưu được; đổi tên → lưu và tự chọn.
  await drawer.getByRole('button', { name: 'Thêm nhóm hàng' }).click()
  const dialog = page.getByRole('dialog', { name: 'Thêm nhóm hàng' })
  const parent = dialog.getByRole('combobox', { name: 'Nhóm cha' })
  await parent.click()
  await parent.fill(root.code)
  await page.locator('.ant-select-dropdown:visible .ant-select-tree-title', { hasText: root.code }).click()
  await dialog.getByLabel('Tên nhóm hàng').fill(`BU LONG ${t}`)
  await expect(dialog.getByRole('alert')).toContainText(`Đã có nhóm ${child.code}`)
  await expect(dialog.getByRole('button', { name: 'Lưu' })).toBeDisabled()
  await dialog.getByLabel('Tên nhóm hàng').fill(`Đai ốc ${t}`)
  await expect(dialog.getByRole('button', { name: 'Lưu' })).toBeEnabled()
  await dialog.screenshot({ path: 'e2e/screenshots/supplier-category-dialog.png' })
  await dialog.getByRole('button', { name: 'Lưu' }).click()
  await expect(page.locator('.ant-message')).toContainText('chọn làm ngành hàng của NCC')
  await expect(drawer.locator('.ant-select-selection-item', { hasText: `Đai ốc ${t}` })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/supplier-categories.png' })

  // Lưu NCC: ngành hàng không cần duyệt – lưu thẳng, không hỏi gửi duyệt.
  await drawer.getByRole('button', { name: 'Lưu' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã lưu NCC ${supplier.code}.`)

  // Lọc danh sách theo nhóm cha: gồm nhóm con; cột Ngành hàng.
  const filter = page.getByRole('combobox', { name: 'Ngành hàng (gồm nhóm con)' })
  await filter.click()
  await filter.fill(root.code)
  await page.locator('.ant-select-dropdown:visible .ant-select-tree-title', { hasText: root.code }).click()
  const row = page.locator('.data-grid-row', { hasText: supplier.code })
  await expect(row).toContainText(child.name)
  await expect(row).toContainText(`Đai ốc ${t}`)
})
