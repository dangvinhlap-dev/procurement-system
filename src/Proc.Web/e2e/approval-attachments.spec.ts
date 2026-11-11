import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { switchCompany } from './inventoryHelpers.ts'
import { createBhaAdmin, seedApprovedPo } from './receiptHelpers.ts'

/**
 * Lát Z1 (design.md mục 20): hộp thoại Duyệt có ô "Ý kiến người duyệt" và đính kèm file; duyệt xong file gắn vào bước vừa duyệt (tab Đính
 * kèm hiện "Duyệt cấp n", "Đã khóa" – không còn nút Xóa), ý kiến hiện ở lịch sử duyệt.
 */
test('duyệt PO kèm ý kiến và file: file thuộc bước duyệt, bị khóa', async ({ page, request }) => {
  test.setTimeout(60_000)
  // PO đã qua cấp 1, còn chờ chặng "Ngoài danh sách/giá" – người duyệt là một quản trị khác người lập.
  const { bha, po } = await seedApprovedPo(request, 10, 1)
  const approver = await createBhaAdmin(bha)

  await loginViaApi(page, approver)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/po/${po.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(po.no)

  await page.locator('button.ant-btn', { hasText: /^Duyệt$/ }).click()
  const dialog = page.getByRole('dialog')
  await expect(dialog.getByText('Ý kiến người duyệt')).toBeVisible()
  await dialog.getByLabel('Ý kiến người duyệt').fill('Đồng ý mua, giá hợp lý')
  await dialog.locator('input[type=file]').setInputFiles({ name: 'bien-ban-duyet.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4 thu') })
  await expect(dialog.getByText('bien-ban-duyet.pdf')).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/approval-dialog.png' })
  await dialog.getByRole('button', { name: 'Duyệt' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã duyệt xong phiếu ${po.no}.`)

  // Lịch sử duyệt có ý kiến; tab Đính kèm: file thuộc bước duyệt, đã khóa.
  await page.getByRole('tab', { name: /Lịch sử duyệt/i }).click()
  await expect(page.getByText('Đồng ý mua, giá hợp lý')).toBeVisible()
  await page.getByRole('tab', { name: /Đính kèm/i }).click()
  const row = page.locator('tr', { hasText: 'bien-ban-duyet.pdf' })
  await expect(row).toContainText(/Duyệt cấp \d/)
  await expect(row).toContainText('Đã khóa')
  await expect(row.getByRole('button', { name: 'Xóa' })).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/approval-attachments.png' })
})
