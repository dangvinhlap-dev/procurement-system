import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand, pickOption, seedPostedReceipt } from './inventoryHelpers.ts'

/**
 * Báo cáo mua hàng (design.md mục 11.3): mua hàng theo NCC toàn tập đoàn nhóm theo công ty (BHA vừa nhập 10 hộp × 50.000), theo nhóm hàng
 * gộp cấp 1 (N4-10), PR chưa xử lý,
 * đánh giá NCC, tiết kiệm từ đấu thầu.
 */

test('mua hàng toàn tập đoàn theo công ty thấy BHA', async ({ page, request }) => {
  await seedPostedReceipt(request)
  await loginViaApi(page)
  await page.goto('/')

  await openByCommand(page, 'RPSUP')
  await expect(page.getByRole('heading', { name: 'Mua hàng theo NCC' })).toBeVisible()
  await expect(page.getByRole('alert')).toContainText('Tổng mua thuần')
  await page.getByLabel('Toàn tập đoàn').check()
  // Ô chọn không gõ tìm được: bấm mở rồi chọn.
  await page.getByRole('combobox', { name: 'Tổng hợp theo', exact: true }).click()
  await pickOption(page, 'Công ty')
  const row = page.locator('.data-grid-row', { hasText: 'BHA' })
  await expect(row).toBeVisible()
  await expect(page.getByRole('columnheader', { name: 'Mã công ty' })).toBeVisible()
  await page.getByRole('heading', { name: 'Mua hàng theo NCC' }).click()
  await expect(page.locator('.ant-spin-spinning')).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/purchase-spend.png' })

  // Nhóm hàng gộp theo cấp (design.md mục 5.7, N4-10): chọn cấp 1 thì mọi dòng là ngành hàng, có cột Cấp và Đường dẫn.
  await page.getByRole('combobox', { name: 'Tổng hợp theo', exact: true }).click()
  await pickOption(page, 'Nhóm hàng')
  await expect(page.getByRole('columnheader', { name: 'Đường dẫn' })).toBeVisible()
  await page.getByRole('combobox', { name: 'Cấp nhóm', exact: true }).click()
  await pickOption(page, '1 – Ngành hàng')
  await expect(page.locator('.ant-spin-spinning')).toHaveCount(0)
  const rows = page.locator('.data-grid-row')
  await expect(rows.first()).toContainText('Ngành hàng')
  await expect(rows.filter({ hasText: /Chủng loại|Loại hàng/ })).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/purchase-spend-category-level.png' })
})

test('PR chưa xử lý, đánh giá NCC, tiết kiệm đấu thầu: mở được', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')

  await openByCommand(page, 'PRPEND')
  await expect(page.getByRole('heading', { name: 'PR chưa xử lý' })).toBeVisible()
  await expect(page.getByRole('alert')).toContainText('quá ngày cần')
  await expect(page.locator('.ant-spin-spinning')).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/pending-requests.png' })

  await openByCommand(page, 'SUPEVAL')
  await expect(page.getByRole('heading', { name: 'Đánh giá nhà cung cấp' })).toBeVisible()
  await expect(page.getByRole('alert')).toContainText('Đúng hạn')
  await expect(page.locator('.ant-spin-spinning')).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/supplier-evaluation.png' })

  await openByCommand(page, 'SAVE')
  await expect(page.getByRole('heading', { name: 'Tiết kiệm từ đấu thầu' })).toBeVisible()
  await expect(page.getByRole('alert')).toContainText('tiết kiệm so với giá chào bình quân')
  await expect(page.locator('.ant-spin-spinning')).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/sourcing-savings.png' })
})
