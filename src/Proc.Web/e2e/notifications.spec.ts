import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, seedPostedReceipt, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Thông báo (design.md mục 11.6): nhân viên mới ở BHA gửi duyệt PR → quản trị thấy số trên chuông Shellbar, bấm chuông mở màn hình
 * Thông báo, bấm đúp mở thẳng phiếu, thông báo chuyển sang Đã đọc.
 */
test('PR gửi duyệt → chuông có số → mở thông báo vào phiếu → đã đọc', async ({ page, request }) => {
  test.setTimeout(60_000)
  const { itemId } = await seedPostedReceipt(request)
  const bha = await api(request, 'BHA')
  const roles = await bha.get<{ id: string; code: string }[]>('/api/roles')
  const uoms = await bha.get<{ id: string; code: string }[]>('/api/uoms')
  const buyerName = unique('tb').toLowerCase()
  await bha.send('post', '/api/users', {
    userName: buyerName, fullName: 'Người lập thông báo', email: null, password: 'Demo@2026',
    access: { roleIds: [roles.find((r) => r.code === 'ADMIN')!.id], warehouseIds: [], itemCategoryIds: [] },
  })

  // Người lập (qua API): lập PR rồi gửi duyệt.
  const login = await request.post('/api/auth/login', { data: { userName: buyerName, password: 'Demo@2026' } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { companies: { id: string; code: string }[] } }
  const headers = { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.companies.find((c) => c.code === 'BHA')!.id }
  const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date())
  const created = await request.post('/api/purchase-requests', {
    headers,
    data: {
      date: today, department: 'Xưởng thông báo', purpose: 'Thử thông báo', neededDate: null, warehouseId: null, priority: 'Normal', note: null,
      lines: [{ id: null, itemId, uomId: uoms.find((u) => u.code === 'HOP')!.id, qty: 2, suggestedSupplierId: null, neededDate: null, note: null }],
    },
  })
  expect(created.ok(), await created.text()).toBeTruthy()
  const pr = (await created.json()) as { id: string; no: string }
  const submitted = await request.post(`/api/purchase-requests/${pr.id}/submit`, { headers })
  expect(submitted.ok(), await submitted.text()).toBeTruthy()

  // Quản trị: chuông có số chưa đọc; bấm chuông mở màn hình Thông báo.
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  const bell = page.getByTestId('shell-bar').getByRole('button', { name: /^Thông báo \(\d+ chưa đọc\)$/ })
  await expect(bell).not.toHaveAccessibleName('Thông báo (0 chưa đọc)')
  await bell.click()
  await expect(page).toHaveURL(/\/hang-ngay\/thong-bao$/)
  await expect(page.getByRole('heading', { name: 'Thông báo' })).toBeVisible()
  const row = page.locator('.data-grid-row', { hasText: pr.no })
  await expect(row).toContainText('Chưa đọc')
  await expect(row).toContainText('Chờ duyệt')
  await expect(page.getByRole('button', { name: 'Đánh dấu tất cả đã đọc' })).toBeEnabled()
  // Chờ hiệu ứng mờ dần của bảng/thông báo chạy xong rồi mới chụp.
  await page.waitForFunction("document.getAnimations().every((a) => a.playState !== 'running')")
  await page.screenshot({ path: 'e2e/screenshots/notifications.png' })

  // Bấm đúp: mở thẳng phiếu PR; quay lại thì thông báo đã đọc.
  await row.dblclick()
  await expect(page).toHaveURL(new RegExp(`/mua-hang/pr/${pr.id}$`))
  await expect(page.locator('.document-form-number')).toHaveText(pr.no)
  await page.goBack()
  await expect(page.locator('.data-grid-row', { hasText: pr.no })).toContainText('Đã đọc')
})
