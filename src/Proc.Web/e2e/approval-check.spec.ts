import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { switchCompany } from './inventoryHelpers.ts'
import { seedApprovedPo } from './receiptHelpers.ts'

/**
 * Lát Z2 (design.md mục 20.1 R3, R4): form chứng từ có dải ổ khóa vàng từng bước duyệt (đóng = đã duyệt, mở = chưa) và nút "Kiểm tra phê
 * duyệt" liệt kê Bước 0 (gửi phê duyệt) → bước cuối với tên đăng nhập, họ tên, chức vụ, giờ ngày; người dùng có trường Chức vụ.
 */
test('ổ khóa từng bước và Kiểm tra phê duyệt trên PO', async ({ page, request }) => {
  test.setTimeout(60_000)
  // PO đã qua cấp 1, còn chờ một chặng – người duyệt là quản trị mới có chức vụ.
  const { bha, po } = await seedApprovedPo(request, 10, 1)
  const roles = await bha.get<{ id: string; code: string }[]>('/api/roles')
  const userName = `ktpd${crypto.randomUUID().slice(0, 8)}`
  await bha.send('post', '/api/users', {
    userName, fullName: `Người duyệt ${userName}`, email: null, password: 'Demo@2026', position: 'Phó Tổng giám đốc',
    access: { roleIds: [roles.find((r) => r.code === 'ADMIN')!.id], warehouseIds: [], itemCategoryIds: [] },
  })

  await loginViaApi(page, { userName, password: 'Demo@2026' })
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/po/${po.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(po.no)

  // Trước khi duyệt: còn ít nhất một ổ khóa mở (bước chưa duyệt), có ổ khóa đóng (cấp 1 đã duyệt).
  const locks = page.locator('.erp-approval-locks')
  await expect(locks.locator('.erp-approval-lock.is-closed').first()).toBeVisible()
  await expect(locks.locator('.erp-approval-lock.is-open')).toHaveCount(1)
  await expect(locks.locator('.erp-approval-lock.is-open')).toHaveAttribute('title', /Chờ duyệt/)

  await page.locator('button.ant-btn', { hasText: /^Duyệt$/ }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Duyệt' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã duyệt xong phiếu ${po.no}.`)

  // Duyệt xong: mọi ổ khóa đóng.
  await expect(locks.locator('.erp-approval-lock.is-open')).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/approval-locks.png' })

  // Kiểm tra phê duyệt: Bước 0 gửi phê duyệt, bước cuối là mình với tên đăng nhập + chức vụ.
  await page.getByRole('button', { name: 'Kiểm tra phê duyệt' }).click()
  const dialog = page.getByRole('dialog', { name: 'Kiểm tra phê duyệt' })
  await expect(dialog.locator('tr', { hasText: 'Bước 0' })).toContainText('Gửi phê duyệt')
  const mine = dialog.locator('tr', { hasText: userName })
  await expect(mine).toContainText('Phó Tổng giám đốc')
  await expect(mine).toContainText('Đã duyệt')
  await expect(mine).toContainText(/\d{2}\/\d{2}\/\d{4} \d{2}:\d{2}/)
  await page.screenshot({ path: 'e2e/screenshots/approval-check.png' })
})
