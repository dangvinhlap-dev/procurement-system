import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, chooseOption, openByCommand, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Ủy quyền duyệt (UQD – lát O2, design.md mục 19.3): quản trị BHA ủy quyền cho một quản trị mới duyệt thay đề nghị mua hàng, danh sách hiện
 * "Đang hiệu lực"; mở lại rồi thu hồi thì thành "Đã thu hồi". Việc duyệt thay kiểm tra ở test tích hợp ApprovalDelegationTests.
 * Dữ liệu Testing không bị xóa giữa các lần chạy nên người được ủy quyền luôn mới.
 */
test('lập ủy quyền duyệt rồi thu hồi, chụp màn hình', async ({ page, request }) => {
  const bha = await api(request, 'BHA')
  const roles = await bha.get<{ id: string; code: string }[]>('/api/roles')
  const userName = unique('uq').toLowerCase()
  const fullName = `Phó phòng ${userName}`
  await bha.send('post', '/api/users', {
    userName, fullName, email: null, password: 'Demo@2026',
    access: { roleIds: [roles.find((r) => r.code === 'ADMIN')!.id], warehouseIds: [], itemCategoryIds: [] },
  })

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'UQD')
  await expect(page.getByRole('heading', { name: /Ủy quyền duyệt/ })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  const drawer = page.locator('.detail-drawer')
  await chooseOption(page, 'Người được ủy quyền', userName)
  await chooseOption(page, 'Loại phiếu', 'Đề nghị mua hàng')
  await drawer.getByLabel('Lý do').fill(`Đi công tác ${userName}`)
  await drawer.getByLabel('Ghi chú', { exact: true }).fill('Liên hệ qua điện thoại')
  await expect(drawer.getByRole('tab', { name: /Đính kèm/i })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/delegation-new.png', animations: 'disabled' })
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(`Đã ủy quyền cho ${fullName}`)

  const row = page.locator('.data-grid-row', { hasText: `Đi công tác ${userName}` })
  await expect(row).toContainText('Đang hiệu lực')
  await expect(row).toContainText('Đề nghị mua hàng')
  await page.waitForLoadState('networkidle')
  await page.screenshot({ path: 'e2e/screenshots/delegation-list.png' })

  await row.dblclick()
  await drawer.getByRole('button', { name: 'Thu hồi' }).click()
  await page.locator('.ant-modal-confirm').getByRole('button', { name: 'Thu hồi' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã thu hồi ủy quyền.')
  await expect(row).toContainText('Đã thu hồi')
})
