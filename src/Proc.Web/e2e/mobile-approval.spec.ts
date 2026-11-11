import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, unique } from './inventoryHelpers.ts'

/**
 * Trang duyệt gọn trên điện thoại (lát O2d, design.md mục 19.3): người duyệt mở link email trên điện thoại → trang `/duyet/PR/{id}`; danh sách
 * `/duyet` có thẻ phiếu chờ duyệt; từ chối thiếu lý do bị chặn; duyệt kèm ý kiến xong thẻ biến mất. Màn hình cỡ điện thoại 390×844.
 */
test.use({ viewport: { width: 390, height: 844 }, hasTouch: true })

test('duyệt đề nghị mua trên điện thoại, chụp màn hình', async ({ page, request }) => {
  const bha = await api(request, 'BHA')
  const roles = await bha.get<{ id: string; code: string }[]>('/api/roles')
  const departmentCode = unique('DT')
  const department = await bha.send<{ id: string }>('post', '/api/departments', { code: departmentCode, name: `Phòng ${departmentCode}`, managerName: null })
  const hop = (await bha.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'HOP')!.id
  // Người duyệt: tài khoản mới vai trò quản trị tại BHA (người lập – admin – không tự duyệt).
  const approverName = unique('dt').toLowerCase()
  await bha.send('post', '/api/users', {
    userName: approverName, fullName: 'Người duyệt điện thoại', email: null, password: 'Demo@2026',
    access: { roleIds: [roles.find((r) => r.code === 'ADMIN')!.id], warehouseIds: [], itemCategoryIds: [] },
  })
  const purpose = `Thay bóng đèn xưởng ${departmentCode}`
  const pr = await bha.send<{ id: string; no: string }>('post', '/api/purchase-requests', {
    date: new Date().toISOString().slice(0, 10), departmentId: department.id, purpose, neededDate: null, warehouseId: null,
    priority: 'Normal', note: 'Cần trước đợt kiểm tra', lines: [],
    newItems: [{ id: null, description: 'Bóng LED 40W', specification: null, uomId: hop, qty: 4, estPrice: 250000, neededDate: null, note: null }],
  })
  await bha.send('post', `/api/purchase-requests/${pr.id}/submit`, {})

  await loginViaApi(page, { userName: approverName, password: 'Demo@2026' })
  // Link "Mở trên phần mềm" trong email mở trên màn hình hẹp → trang duyệt gọn.
  await page.goto(`/mo/PR/${pr.id}`)
  await expect(page).toHaveURL(new RegExp(`/duyet/PR/${pr.id}$`))
  await expect(page.locator('.erp-mobile-section').first()).toContainText(pr.no)

  await page.goto('/duyet')
  const card = page.locator('.erp-mobile-card', { hasText: pr.no })
  await expect(card).toContainText(`Phòng ${departmentCode}`)
  await expect(card).toContainText('1.000.000')
  await page.screenshot({ path: 'e2e/screenshots/mobile-approval-list.png' })

  await card.click()
  await expect(page).toHaveURL(new RegExp(`/duyet/PR/${pr.id}$`))
  await expect(page.getByText(purpose)).toBeVisible()
  await expect(page.getByText('Hàng mới: Bóng LED 40W')).toBeVisible()
  await expect(page.locator('.erp-mobile-steps')).toContainText('Chờ duyệt')
  await page.screenshot({ path: 'e2e/screenshots/mobile-approval-detail.png', fullPage: true })

  // Từ chối bắt buộc lý do (design.md mục 18.6).
  await page.getByRole('button', { name: 'Từ chối' }).click()
  await expect(page.locator('.ant-message')).toContainText('Hãy nhập lý do từ chối')
  await expect(page.locator('.ant-modal-confirm')).toHaveCount(0)

  await page.getByLabel('Ý kiến / lý do (bắt buộc khi từ chối)').fill('Đồng ý, mua đúng số lượng')
  await page.getByRole('button', { name: 'Duyệt' }).click()
  await page.locator('.ant-modal-confirm').getByRole('button', { name: 'Duyệt' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã duyệt ${pr.no}.`)
  await expect(page).toHaveURL(/\/duyet$/)
  await expect(page.locator('.erp-mobile-card', { hasText: pr.no })).toHaveCount(0)

  // Mở lại phiếu: không còn chờ người này duyệt, lịch sử ghi ý kiến.
  await page.goto(`/duyet/PR/${pr.id}`)
  await expect(page.getByText(/Phiếu không còn chờ bạn duyệt/)).toBeVisible()
  await expect(page.locator('.erp-mobile-steps')).toContainText('Đồng ý, mua đúng số lượng')
  await expect(page.getByRole('button', { name: 'Duyệt' })).toHaveCount(0)
})
