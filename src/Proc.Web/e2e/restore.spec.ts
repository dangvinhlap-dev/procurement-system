import { expect, test, type Page } from '@playwright/test'
import { ADMIN, loginViaApi, openByCommand } from './helpers.ts'

/**
 * Lát QT6 – màn hình RST (Phục hồi, design.md mục 21.4, 21.15; ui-style.md mục 5o).
 * - Khôi phục pg_dump chạy thật (API Testing bật bộ chạy nền): tab Bản pg_dump → Khôi phục từ bản này… → tab Lịch sử "Chờ kiểm tra" → ngăn kéo
 *   có bảng so sánh số dòng → Bỏ bản khôi phục (CSDL khôi phục bị xóa). Chưa có bản dump thì đặt "Sao lưu ngay" pg_dump ở BAK trước.
 * - Hoán đổi: chỉ mở hộp thoại Q6 (gõ sai chữ "KHÔI PHỤC" bị chặn) rồi đóng – KHÔNG hoán đổi thật (sẽ đổi CSDL của API test đang chạy test
 *   khác). Bộ thực thi máy này được GIẢ LẬP là sẵn sàng (page.route sửa trường agentReady); phần máy chủ có test tích hợp RestoreTests.
 * - Về mốc thời gian: API test chưa khai thư mục WAL / chưa có bản Full → kế hoạch báo không khôi phục được, nút Khôi phục khóa.
 */

/** Có ít nhất một bản pg_dump dùng được: chưa có thì đặt "Sao lưu ngay" pg_dump ở BAK và chờ xong. */
async function ensureDump(page: Page) {
  await openByCommand(page, 'RST')
  await page.getByRole('tab', { name: 'Bản pg_dump' }).click()
  const rows = page.locator('.data-grid-row', { hasText: 'Còn file' })
  if (await rows.first().waitFor({ timeout: 5000 }).then(() => true, () => false)) return
  await openByCommand(page, 'BAK')
  await page.getByRole('button', { name: /Sao lưu ngay/ }).click()
  await page.getByRole('menuitem', { name: /pg_dump – bản logic/ }).click()
  await expect(page.locator('.data-grid-row', { hasText: 'pg_dump' }).first()).toContainText(/Xong/, { timeout: 90_000 })
  await openByCommand(page, 'RST')
  await page.getByRole('tab', { name: 'Bản pg_dump' }).click()
}

test('RST: khôi phục pg_dump thật, so sánh số dòng, hộp thoại hoán đổi Q6, bỏ bản khôi phục', async ({ page }) => {
  test.setTimeout(180_000)
  await page.route('**/api/ops/v1/restore/overview', async (route) => {
    const response = await route.fetch()
    const json = await response.json()
    await route.fulfill({ response, json: { ...json, agentReady: true, agentNote: null } })
  })
  await loginViaApi(page)
  await page.goto('/')
  await ensureDump(page)
  await expect(page.getByRole('heading', { name: /Phục hồi/ })).toBeVisible()
  await expect(page.getByRole('alert').first()).toContainText(/CSDL đang chạy: procurement\w*/)

  const dump = page.locator('.data-grid-row', { hasText: 'Còn file' }).first()
  await expect(page.getByRole('button', { name: 'Khôi phục từ bản này…' })).toBeDisabled()
  await dump.click()
  await page.getByRole('button', { name: 'Khôi phục từ bản này…' }).click()
  const dialog = page.getByRole('dialog', { name: 'Khôi phục từ pg_dump' })
  await dialog.getByLabel('Lý do').fill('Thử khôi phục bản đêm qua (e2e)')
  await dialog.getByRole('button', { name: /Khôi phục$/ }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã đặt khôi phục pg_dump vào hàng chờ')
  await expect(page.getByRole('tab', { name: 'Lịch sử', selected: true })).toBeVisible()

  const row = page.locator('.data-grid-row', { hasText: 'Thử khôi phục bản đêm qua (e2e)' }).first()
  await expect(row).toContainText('Chờ kiểm tra', { timeout: 120_000 })
  await expect(row).toContainText(/procurement\w*_restore_\d{8}_\d{4}/)
  await page.screenshot({ path: 'e2e/screenshots/restore-history.png' })

  // Ngăn kéo: bảng so sánh số dòng (bảng nghiệp vụ bằng nhau, bảng vận hành đánh dấu riêng).
  await row.dblclick()
  const drawer = page.locator('.detail-drawer')
  await expect(drawer).toContainText('Phục hồi: Từ pg_dump – Chờ kiểm tra')
  await drawer.getByRole('tab', { name: 'So sánh số dòng' }).click()
  // Lưới cuộn ảo, dòng khác số dòng (dữ liệu test khác ghi trong lúc khôi phục) xếp trước – chỉ kiểm tra có bảng so sánh, cùng phiên bản CSDL.
  await expect(drawer.getByRole('alert')).toContainText(/So sánh lúc .* phiên bản CSDL đang chạy (\S+) · bản khôi phục \1$/)
  await expect(drawer.locator('.data-grid-row').first()).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/restore-compare.png' })
  await page.keyboard.press('Escape')
  await expect(drawer).toBeHidden()

  // Hoán đổi: hộp thoại Q6 – chỉ kiểm tra chặn sai chữ, không hoán đổi thật.
  await row.click()
  await page.getByRole('button', { name: 'Hoán đổi…' }).click()
  const swap = page.getByRole('dialog', { name: 'Hoán đổi bản khôi phục' })
  await expect(swap).toContainText('thành CSDL đang chạy')
  await swap.getByLabel('Lý do').fill('Thử hộp thoại hoán đổi')
  await swap.getByLabel('Mật khẩu của bạn').fill(ADMIN.password)
  await swap.getByLabel(/Gõ "KHÔI PHỤC"/).fill('KHOI PHUC')
  await swap.getByRole('button', { name: /Hoán đổi$/ }).click()
  await expect(swap).toContainText('Gõ đúng "KHÔI PHỤC".')
  await page.screenshot({ path: 'e2e/screenshots/restore-swap-dialog.png' })
  await swap.getByRole('button', { name: 'Không hoán đổi' }).click()
  await expect(swap).toBeHidden()

  await page.getByRole('button', { name: 'Bỏ bản khôi phục' }).click()
  await page.locator('.ant-modal-confirm').getByRole('button', { name: 'Bỏ bản khôi phục' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã bỏ bản khôi phục.')
  await expect(row).toContainText('Đã bỏ')
})

test('RST: về mốc thời gian – kế hoạch báo chưa đủ chuỗi khi chưa khai thư mục WAL', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'RST')
  await page.getByRole('button', { name: 'Về mốc thời gian…' }).click()
  const dialog = page.getByRole('dialog', { name: 'Khôi phục về mốc thời gian' })
  await dialog.getByRole('button', { name: 'Lập kế hoạch' }).click()
  const plan = dialog.getByTestId('restore-plan')
  await expect(plan).toContainText('Không khôi phục được về mốc này')
  await expect(plan).toContainText('Backup:WalArchivePath')
  await expect(dialog.getByRole('button', { name: /Khôi phục$/ })).toBeDisabled()
  await page.screenshot({ path: 'e2e/screenshots/restore-pitr-plan.png' })
  await dialog.getByRole('button', { name: 'Không khôi phục' }).click()
})
