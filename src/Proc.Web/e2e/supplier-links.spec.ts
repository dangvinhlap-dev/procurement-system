import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { ADMIN, loginViaApi } from './helpers.ts'

/**
 * NCC – sản phẩm và NCC áp dụng tại công ty (design.md mục 5.3, 5.9). NCC và mặt hàng tạo sẵn qua API cho nhanh,
 * thao tác cần kiểm tra làm trên giao diện.
 */

const unique = (prefix: string) => `${prefix}${Date.now().toString(36).slice(-5)}${Math.floor(Math.random() * 100)}`.toUpperCase()

/** Gọi API bằng tài khoản quản trị ở tổng công ty (công ty mặc định của admin). */
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

async function openByCommand(page: Page, code: string) {
  const box = page.getByPlaceholder('Ô lệnh – tên màn hình')
  await box.fill(code)
  await box.press('Enter')
}

test('NCC – sản phẩm: thêm mặt hàng NCC bán, áp dụng tại công ty, xem NCC từ ngăn kéo hàng hóa', async ({ page, request }) => {
  const api = await adminApi(request)
  const groups = await api.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const uoms = await api.get<{ id: string; code: string }[]>('/api/uoms')
  const uom = (code: string) => uoms.find((u) => u.code === code)!.id
  const supplierTag = unique('NCC')
  const supplier = await api.post<{ id: string; code: string }>('/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: `NCC liên kết ${supplierTag}`, shortName: null,
    isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Approved', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })
  const supplierCode = supplier.code
  const category = await api.post<{ id: string; code: string }>('/api/item-categories', { code: null, name: 'Nhóm thử liên kết', parentId: null, inventoryAccount: null })
  // Mã hàng do hệ thống cấp theo dải đánh số (design.md mục 5.11).
  const item = await api.post<{ id: string; code: string }>('/api/items', {
    code: null, name: 'Dây cáp điện thử', englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: uom('M'),
    specification: null, brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, trackLot: false,
    trackExpiry: false, minStock: null, maxStock: null, conversions: [{ uomId: uom('CUON'), factor: 100 }],
  })
  const itemCode = item.code

  await loginViaApi(page)
  await page.goto('/')
  const drawer = page.locator('.detail-drawer')
  await openByCommand(page, 'SUP')
  await page.getByPlaceholder(/Gõ từ khóa/).fill(supplierCode)
  await page.locator('.data-grid-row', { hasText: supplierCode }).dblclick()
  await expect(drawer.getByText(`Nhà cung cấp: ${supplierCode}`)).toBeVisible()

  // Thêm mặt hàng NCC bán: tìm hàng ở server, ĐVT mua chỉ có Mét (cơ sở) và Cuộn (quy đổi).
  await drawer.getByRole('tab', { name: 'Sản phẩm/dịch vụ' }).click()
  await drawer.getByRole('button', { name: 'Thêm mặt hàng' }).click()
  const dialog = page.getByRole('dialog', { name: 'Thêm mặt hàng NCC bán' })
  await dialog.getByLabel('Mặt hàng', { exact: true }).fill(itemCode.toLowerCase())
  await page.locator('.ant-select-dropdown:visible').getByTitle(itemCode, { exact: false }).click()
  await dialog.getByLabel('ĐVT mua').click()
  await expect(page.locator('.ant-select-dropdown:visible .ant-select-item-option')).toHaveCount(2)
  await page.locator('.ant-select-dropdown:visible').getByTitle('CUON', { exact: false }).click()
  await dialog.getByLabel('Số lượng đặt tối thiểu (MOQ)').fill('5')
  await dialog.getByRole('checkbox', { name: 'NCC ưu tiên của mặt hàng này' }).check()
  await dialog.getByRole('button', { name: 'Lưu' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã lưu mặt hàng của NCC.')
  const itemRow = drawer.locator('.data-grid-row', { hasText: itemCode })
  await expect(itemRow).toContainText('CUON')
  await expect(itemRow).toContainText('✓')
  await page.screenshot({ path: 'e2e/screenshots/supplier-items-tab.png' })

  // Áp dụng tại công ty đang làm việc (TCT), lưu riêng trong tab.
  await drawer.getByRole('tab', { name: 'Áp dụng tại TCT' }).click()
  await drawer.getByLabel('Mã NCC tại công ty').fill(`331${supplierCode}`.slice(0, 20))
  await drawer.getByRole('button', { name: 'Lưu áp dụng' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã lưu áp dụng tại TCT.')
  await drawer.getByRole('button', { name: 'Đóng' }).click()

  await page.getByLabel('Phạm vi').click()
  await page.locator('.ant-select-dropdown:visible').getByTitle('NCC TCT đang mua').click()
  await expect(page.locator('.data-grid-row', { hasText: supplierCode })).toContainText('Đang mua')

  // Ngăn kéo hàng hóa: tab Nhà cung cấp thấy NCC ưu tiên.
  await openByCommand(page, 'ITEM')
  await page.getByPlaceholder(/Gõ từ khóa/).fill(itemCode)
  await page.locator('.data-grid-row', { hasText: itemCode }).dblclick()
  await drawer.getByRole('tab', { name: 'Nhà cung cấp' }).click()
  await expect(drawer.getByRole('tabpanel').locator('.data-grid-row', { hasText: supplierCode })).toContainText('✓')
})
