import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, openByCommand, seedPostedReceipt, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Ngân sách bộ phận × tháng (lát C3, design.md mục 18.24): khai ngân sách tháng hiện tại ở màn hình BUDG, PR vượt ngân sách hiện cảnh
 * báo ở đầu phiếu và gửi duyệt được (thêm chặng "Vượt ngân sách"), bảng ngân sách báo bộ phận vượt.
 */
test('khai ngân sách bộ phận, PR vượt ngân sách có cảnh báo và chặng duyệt riêng', async ({ page, request }) => {
  const seed = await seedPostedReceipt(request)
  const bha = await api(request, 'BHA')
  const code = unique('NS')
  const department = await bha.send<{ id: string }>('post', '/api/departments', { code, name: `Phân xưởng ${code}`, managerName: null })
  const now = new Date()
  const today = now.toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const [year, month] = today.split('-')

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'BUDG')
  await expect(page.getByRole('heading', { name: `Ngân sách bộ phận ${year} – BHA` })).toBeVisible()
  const row = page.locator('.data-grid-row', { hasText: code })
  await expect(row).toContainText('Chưa khai')

  // Khai 300.000 cho tháng hiện tại.
  await row.dblclick()
  const drawer = page.locator('.detail-drawer')
  await expect(drawer.getByText(`Ngân sách ${year}: ${code}`)).toBeVisible()
  await drawer.getByLabel(`Tháng ${month}/${year}`).fill('300.000')
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(`Đã lưu ngân sách ${year} của bộ phận ${code}.`)
  await expect(row).toContainText('300.000')
  await expect(row).toContainText('Trong ngân sách')

  // PR 10 hộp × 50.000 (giá nhập gần nhất) = 500.000 > 300.000: cảnh báo vượt, gửi duyệt vẫn được, có chặng vượt ngân sách.
  const warehouses = await bha.get<{ id: string; code: string }[]>('/api/warehouses')
  const uoms = await bha.get<{ id: string; code: string }[]>('/api/uoms')
  const pr = await bha.send<{ id: string; no: string }>('post', '/api/purchase-requests', {
    date: today, departmentId: department.id, purpose: 'Thử ngân sách', neededDate: null,
    warehouseId: warehouses.find((w) => w.code === 'KT')!.id, priority: 'Normal', note: null,
    lines: [{ id: null, itemId: seed.itemId, uomId: uoms.find((u) => u.code === 'HOP')!.id, qty: 10, suggestedSupplierId: null, neededDate: null, note: null }],
  })
  await page.goto(`/mua-hang/pr/${pr.id}`)
  await expect(page.getByText(`Vượt ngân sách bộ phận tháng ${month}/${year}: ngân sách 300.000, đã dùng 0, phiếu này 500.000 – vượt 200.000`)).toBeVisible()
  const submitted = await bha.send<{ approvals: { stage: number }[] }>('post', `/api/purchase-requests/${pr.id}/submit`, {})
  expect(submitted.approvals.some((s) => s.stage === 3)).toBeTruthy()
  await page.reload()
  await page.getByRole('tab', { name: /Lịch sử duyệt/i }).click()
  await expect(page.getByRole('tabpanel')).toContainText('Vượt ngân sách')

  // Bảng ngân sách: PR chờ duyệt đã tính vào "đã dùng" → bộ phận vượt 1 tháng.
  await openByCommand(page, 'BUDG')
  await expect(row).toContainText('Vượt 1 tháng')
  await page.getByRole('combobox', { name: 'Hiển thị' }).click()
  await page.locator('.ant-select-dropdown:visible').getByTitle('Còn lại').click()
  await expect(row).toContainText('-200.000')
  await page.waitForLoadState('networkidle')
  await page.screenshot({ path: 'e2e/screenshots/budget-list.png' })
})
