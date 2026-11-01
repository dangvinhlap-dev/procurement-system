import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, chooseOption, openByCommand, pickOption, unique } from './inventoryHelpers.ts'

/**
 * Kê khai xung đột lợi ích (design.md mục 18.8): quản trị tự kê khai quan hệ với một NCC, thấy "Đang hiệu lực", rồi kết thúc kê khai
 * kèm lý do. NCC tạo mới mỗi lần chạy (dữ liệu Testing không bị xóa) – để Tiềm năng nên không phải qua duyệt NCC.
 */
test('kê khai quan hệ với NCC, kết thúc kê khai, chụp màn hình', async ({ page, request }) => {
  const head = await api(request)
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const name = `NCC kê khai ${unique('kk')}`
  await head.send('post', '/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name, shortName: null,
    isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Potential', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })

  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'COI')
  await expect(page.getByRole('heading', { name: 'Kê khai xung đột lợi ích – toàn tập đoàn' })).toBeVisible()

  await page.getByRole('button', { name: 'Tạo mới' }).click()
  const drawer = page.locator('.detail-drawer')
  await expect(drawer.getByText('Kê khai xung đột lợi ích')).toBeVisible()
  // Gõ không dấu vẫn tìm được NCC có dấu (CLAUDE.md mục 5).
  await chooseOption(page, 'Nhà cung cấp', name.replace('kê khai', 'ke khai'))
  await drawer.getByRole('combobox', { name: 'Loại quan hệ', exact: true }).click()
  await pickOption(page, 'Góp vốn, cổ phần')
  await drawer.getByLabel('Mô tả quan hệ').fill('Góp 10% vốn điều lệ')
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText('Đã ghi kê khai quan hệ với NCC')
  const row = page.locator('.data-grid-row', { hasText: name })
  await expect(row).toContainText('Góp vốn, cổ phần')
  await expect(row).toContainText('Đang hiệu lực')

  await row.dblclick()
  await expect(drawer.getByText('Đang hiệu lực – người này không duyệt')).toBeVisible()
  await drawer.getByRole('button', { name: 'Kết thúc kê khai' }).click()
  await page.getByLabel('Lý do kết thúc').fill('Đã chuyển nhượng phần vốn góp')
  await page.locator('.ant-modal-confirm').getByRole('button', { name: 'Kết thúc' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã kết thúc kê khai.')
  await expect(row).toContainText('Đã kết thúc')
  await page.evaluate('getSelection()?.removeAllRanges()')
  await page.waitForLoadState('networkidle')
  await page.screenshot({ path: 'e2e/screenshots/conflict-list.png' })
})
