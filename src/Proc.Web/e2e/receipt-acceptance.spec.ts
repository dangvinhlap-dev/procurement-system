import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, openByCommand, seedPostedReceipt, unique } from './inventoryHelpers.ts'

/**
 * Lát H4 (design.md mục 18.25 – sơ đồ mục 3): thủ kho (quản trị) nhập và ghi sổ phiếu nhập; người của đơn vị đặt hàng mở phiếu, bấm "Xác nhận
 * nghiệm thu": chọn Không đạt mà chưa ghi ý kiến thì bị chặn; ghi ý kiến thì phiếu hiện "Nghiệm thu: Không đạt"; báo cáo MATCH3 có cột
 * Nghiệm thu và cảnh báo. Quy tắc thủ kho không tự xác nhận, bỏ ghi sổ thì xóa xác nhận kiểm tra ở test tích hợp.
 */
test('đơn vị đặt hàng xác nhận nghiệm thu phiếu nhập, hiện trên đối chiếu MATCH3, chụp màn hình', async ({ page, request }) => {
  test.setTimeout(90_000)
  const seed = await seedPostedReceipt(request)
  const bha = await api(request, 'BHA')

  // Người của đơn vị đặt hàng: vai trò chỉ xem chứng từ kho, xác nhận nghiệm thu, xem báo cáo mua hàng.
  const roleCode = unique('NT')
  await bha.send('post', '/api/roles', {
    code: roleCode, name: `Nghiệm thu ${roleCode}`,
    permissionCodes: ['Inventory.Document.View', 'Inventory.Receipt.Accept', 'Report.Purchase.View'],
  })
  const role = (await bha.get<{ id: string; code: string }[]>('/api/roles')).find((r) => r.code === roleCode)!
  const userName = unique('nt').toLowerCase()
  await bha.send('post', '/api/users', {
    userName, fullName: `Bộ phận bếp ${userName}`, email: null, password: 'Demo@2026',
    access: { roleIds: [role.id], warehouseIds: [], itemCategoryIds: [] },
  })

  await loginViaApi(page, { userName, password: 'Demo@2026' })
  await page.goto(`/kho/grn/${seed.receiptId}`)
  await expect(page.locator('.document-form-number')).toHaveText(seed.receiptNo)
  await expect(page.getByRole('alert').filter({ hasText: 'chưa xác nhận nghiệm thu' })).toBeVisible()

  await page.getByRole('button', { name: /Xác nhận nghiệm thu$/ }).click()
  const dialog = page.getByRole('dialog')
  await dialog.getByText('Không đạt – có ý kiến').click()
  await dialog.getByRole('button', { name: 'Xác nhận' }).click()
  await expect(dialog).toContainText('Không đạt phải ghi ý kiến')
  await dialog.getByLabel('Ý kiến nghiệm thu').fill('Thiếu 1 hộp, 2 hộp móp')
  await page.screenshot({ path: 'e2e/screenshots/receipt-acceptance-dialog.png' })
  await dialog.getByRole('button', { name: 'Xác nhận' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã xác nhận nghiệm thu phiếu ${seed.receiptNo}: Không đạt.`)
  await expect(page.getByRole('alert').filter({ hasText: 'Nghiệm thu: Không đạt' })).toContainText('Thiếu 1 hộp, 2 hộp móp')
  await page.screenshot({ path: 'e2e/screenshots/receipt-acceptance.png' })

  // Kế toán thấy trên báo cáo đối chiếu PO – nhận – hóa đơn.
  await openByCommand(page, 'MATCH3')
  const row = page.locator('tr').filter({ hasText: seed.receiptNo })
  await expect(row).toContainText('Không đạt')
  await expect(row).toContainText('Đơn vị đặt hàng không đồng ý nghiệm thu')
})
