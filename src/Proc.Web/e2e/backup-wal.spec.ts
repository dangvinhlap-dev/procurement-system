import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { openByCommand } from './inventoryHelpers.ts'

/**
 * Lát QT3a – màn hình BAK (Sao lưu): tình trạng lưu WAL của máy CSDL thật (máy dev chưa bật → "Chưa bật", đèn trung tính vì môi trường DEV),
 * các dòng tham số PostgreSQL; trạng thái "Trễ" giả lập bằng cách chặn API (máy dev không dựng được). SYS có dòng "Lưu WAL".
 */
test('BAK hiện tình trạng lưu WAL thật, SYS có dòng Lưu WAL', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'BAK')
  await expect(page.getByRole('heading', { name: /Sao lưu/ })).toBeVisible()
  await expect(page.getByRole('alert')).toContainText(/Lưu WAL: (Chưa bật|Chờ khởi động lại|Đang chạy|Trễ > 15 phút|Có lỗi)/)
  await expect(page.locator('[data-row-key="Tham số PostgreSQL|archive_mode"]')).toBeVisible()
  await expect(page.locator('[data-row-key="Tham số PostgreSQL|wal_level"]')).toBeVisible()
  await expect(page.locator('.data-grid-row', { hasText: 'Tài khoản bảo trì' })).toContainText('Chưa khai')
  await expect(page.locator('.ant-spin-spinning')).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/backup-wal.png' })

  await openByCommand(page, 'SYS')
  await expect(page.locator('.data-grid-row', { hasText: 'Lưu WAL' })).toBeVisible()
})

test('BAK: lưu WAL trễ hiện thông báo đỏ và dòng file chờ chép', async ({ page }) => {
  const now = new Date().toISOString()
  await page.route('**/api/ops/v1/backup/wal-status', (route) =>
    route.fulfill({
      json: {
        state: 'Lagging',
        stateLabel: 'Trễ > 15 phút',
        status: 'Error',
        summary: 'Trễ 20 phút: còn 3 file WAL đầy chưa chép – kiểm tra thư mục WAL, quyền ghi, chỗ trống ổ.',
        checkedAt: now,
        lastArchivedAt: now,
        lagSeconds: 1200,
        rows: [
          { group: 'Lưu WAL', item: 'Trạng thái', value: 'Trễ > 15 phút', status: 'Error', note: 'Trễ 20 phút' },
          { group: 'Thống kê chép', item: 'File WAL chờ chép', value: '3', status: 'Error', note: null },
        ],
      },
    }),
  )
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'BAK')
  const alert = page.getByRole('alert')
  await expect(alert).toContainText('Lưu WAL: Trễ > 15 phút')
  await expect(alert).toContainText('còn 3 file WAL')
  await expect(page.locator('.data-grid-row', { hasText: 'File WAL chờ chép' })).toContainText('Lỗi')
  await page.screenshot({ path: 'e2e/screenshots/backup-wal-lagging.png' })
})
