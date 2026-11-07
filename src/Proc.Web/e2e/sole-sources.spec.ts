import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, openByCommand, pickOption, unique } from './inventoryHelpers.ts'

/** Người duyệt NCC độc quyền tại Tổng công ty (dữ liệu mẫu môi trường Testing). */
const SOLE_SOURCE_APPROVER = { userName: 'duyetdq.tct', password: 'Demo@2026' }

/**
 * NCC độc quyền (design.md mục 18.9): quản trị lập phiếu NCC × mặt hàng kèm giải trình trên màn hình NDQ, gửi duyệt; người duyệt riêng
 * duyệt (người lập không tự duyệt); phiếu hiện "Đang hiệu lực" và lập được phiếu đánh giá lại. NCC, mặt hàng tạo mới mỗi lần chạy.
 */
test('lập phiếu NCC độc quyền, gửi duyệt, duyệt, đánh giá lại, chụp màn hình', async ({ page, request }) => {
  const head = await api(request)
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const supplierName = `NCC độc quyền ${unique('dq')}`
  await head.send('post', '/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: supplierName, shortName: null,
    isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Potential', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })
  const category = await head.send<{ id: string }>('post', '/api/item-categories', { code: null, name: `Nhóm độc quyền ${crypto.randomUUID().slice(0, 8)}`, parentId: null, inventoryAccount: null })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: `Phụ tùng máy nén ${unique('pt')}`, englishName: null, type: 'Inventory', categoryId: category.id,
    baseUomId: uoms.find((u) => u.code === 'CAI')!.id, specification: null, brand: null, origin: null, barcode: null, vatCode: '10',
    technicalSpecs: null, trackLot: false, trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })

  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'NDQ')
  await expect(page.getByRole('heading', { name: 'NCC độc quyền' })).toBeVisible()

  await page.getByRole('button', { name: 'Tạo mới' }).click()
  const drawer = page.locator('.detail-drawer')
  await expect(drawer.getByText('Lập phiếu NCC độc quyền')).toBeVisible()
  // Gõ không dấu vẫn tìm được NCC có dấu (CLAUDE.md mục 5).
  const supplierBox = drawer.getByRole('combobox', { name: 'Nhà cung cấp', exact: true })
  await supplierBox.click()
  await supplierBox.fill(supplierName.replace('độc quyền', 'doc quyen'))
  await pickOption(page, supplierName)
  const itemBox = drawer.getByRole('combobox', { name: 'Mặt hàng', exact: true })
  await itemBox.click()
  await itemBox.fill(item.code)
  await pickOption(page, item.code)
  await drawer.getByLabel('Giải trình').fill('Hãng là nhà sản xuất duy nhất phụ tùng cho máy nén hiện có')
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText('Đã lập phiếu NCC độc quyền')
  await expect(drawer.locator('.detail-drawer-title')).toContainText('Nháp')
  const no = ((await drawer.locator('.detail-drawer-title').innerText()).match(/NDQ\S+/) ?? [''])[0]
  expect(no).not.toBe('')

  await drawer.getByRole('button', { name: 'Gửi duyệt' }).click()
  // Popup xem trước người duyệt (yêu cầu người dùng 03/11/2026) rồi mới gửi.
  const sendDialog = page.locator('.ant-modal-confirm')
  await expect(sendDialog).toContainText('Người duyệt lần lượt')
  await sendDialog.getByRole('button', { name: /Gửi duyệt$/ }).click()
  await expect(drawer.locator('.detail-drawer-title')).toContainText('Chờ duyệt')
  // Người lập không tự duyệt: không có nút Duyệt.
  await expect(drawer.getByRole('button', { name: 'Duyệt', exact: true })).toHaveCount(0)
  await drawer.getByRole('button', { name: 'Đóng' }).click()
  const row = page.locator('.data-grid-row', { hasText: no })
  await expect(row).toContainText('Chờ duyệt')

  // Người duyệt riêng duyệt qua API (tài khoản mẫu duyetdq.tct).
  const login = await request.post('/api/auth/login', { data: SOLE_SOURCE_APPROVER })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { defaultCompanyId: string } }
  const headers = { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.defaultCompanyId }
  const list = (await (await request.get('/api/sole-sources', { headers })).json()) as { id: string; no: string }[]
  const id = list.find((x) => x.no === no)!.id
  const approved = await request.post(`/api/sole-sources/${id}/approve`, { headers, data: { comment: null } })
  expect(approved.ok(), await approved.text()).toBeTruthy()

  // Trang đầy đủ của phiếu (mở từ thông báo, Việc cần làm, Tra cứu chứng từ – TC-3).
  await page.goto(`/gia-ncc/ncc-doc-quyen/${id}`)
  await expect(page.locator('.detail-page')).toContainText('Đang hiệu lực đến')
  await expect(page.locator('.detail-drawer')).toHaveCount(0)
  await page.locator('.detail-page').getByRole('button', { name: 'Quay lại' }).click()
  await expect(page).toHaveURL(/\/gia-ncc\/ncc-doc-quyen$/)
  await expect(row).toContainText('Đang hiệu lực')
  await row.dblclick()
  await expect(drawer.getByText('Đang hiệu lực đến')).toBeVisible()
  await drawer.getByRole('button', { name: 'Đánh giá lại' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã lập phiếu đánh giá lại')
  await expect(drawer.locator('.detail-drawer-title')).toContainText('Nháp')
  await expect(drawer.getByRole('button', { name: no })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/sole-source-drawer.png' })
  await drawer.getByRole('button', { name: 'Đóng' }).click()
  await expect(drawer).toHaveCount(0)
  await page.evaluate('getSelection()?.removeAllRanges()')
  await page.waitForLoadState('networkidle')
  await page.screenshot({ path: 'e2e/screenshots/sole-source-list.png' })
})
