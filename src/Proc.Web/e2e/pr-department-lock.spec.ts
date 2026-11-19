import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, unique } from './inventoryHelpers.ts'

/**
 * Bộ phận đề nghị khóa theo bộ phận của người lập (design.md mục 19, lát O1 – chốt 19/11/2026): người lập không có quyền lập thay chỉ thấy
 * bộ phận của mình, ô bị khóa kèm dòng gợi ý lý do; Trưởng phòng ban duyệt đúng nhân viên của mình.
 */
test('PR: người lập thường – ô bộ phận khóa theo bộ phận của mình', async ({ browser, request }) => {
  const bha = await api(request, 'BHA')
  const code = unique('BP')
  const own = await bha.send<{ id: string }>('post', '/api/departments', { code, name: `Phân xưởng ${code}`, managerName: null })
  await bha.send('post', '/api/departments', { code: unique('BK'), name: 'Bộ phận khác', managerName: null })
  const roleCode = unique('LP')
  await bha.send('post', '/api/roles', {
    code: roleCode, name: `Lập đề nghị ${roleCode}`, permissionCodes: ['Purchase.PR.View', 'Purchase.PR.Create'],
  })
  const role = (await bha.get<{ id: string; code: string }[]>('/api/roles')).find((r) => r.code === roleCode)!
  const userName = unique('lp').toLowerCase()
  await bha.send('post', '/api/users', {
    userName, fullName: `Nhân viên ${userName}`, email: null, password: 'Demo@2026',
    access: { roleIds: [role.id], warehouseIds: [], itemCategoryIds: [], departmentId: own.id },
  })

  const context = await browser.newContext()
  const page = await context.newPage()
  await loginViaApi(page, { userName, password: 'Demo@2026' })
  await page.goto('/mua-hang/pr/moi')
  const department = page.getByRole('combobox', { name: 'Bộ phận đề nghị', exact: true })
  await expect(department).toBeDisabled()
  await expect(page.locator('.ant-form-item').filter({ has: department })).toContainText(code)
  await expect(page.getByText('Theo bộ phận của người lập – lập thay cần quyền riêng.')).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/pr-department-lock.png' })
  await context.close()
})
