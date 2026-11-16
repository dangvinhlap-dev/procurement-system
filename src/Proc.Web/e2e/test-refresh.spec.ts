import { expect, test } from '@playwright/test'
import { loginViaApi, openByCommand } from './helpers.ts'

/**
 * Lát QT9 – màn hình TST (làm mới TEST từ PROD) và nút "Báo lỗi / góp ý" trên máy TEST (design.md 21.5, 21.18; ui-style.md mục 5r).
 * API test chạy môi trường Dev, không có máy TEST thật nên tổng quan TST và API môi trường được GIẢ LẬP; không giao lệnh làm mới thật (phần máy chủ có
 * test tích hợp TestRefreshTests, bộ thực thi đã thử trên CSDL nháp).
 */

/** PNG 1×1 hợp lệ. */
const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==', 'base64')

test('TST: tình trạng đủ điều kiện, hộp thoại làm mới (Q6 – gõ sai bị chặn), tab Lịch sử', async ({ page }) => {
  await page.route('**/api/ops/v1/test-refresh/overview', (route) =>
    route.fulfill({
      json: {
        machineEnvironment: 'Prod',
        version: '0.37.0',
        readOnlyReason: null,
        test: {
          code: 'Test', name: 'Máy TEST', contactLight: 'Ok', contactLabel: 'Đang liên lạc', appVersion: '0.37.0', agentVersion: '1.3.0',
          lastSeenAt: new Date().toISOString(), hasServiceKey: false,
        },
        dump: {
          runId: '00000000-0000-0000-0000-000000000001', finishedAt: new Date().toISOString(), relativePath: 'dump\\procurement_20261116.dump',
          sizeBytes: 52428800, sha256: 'ab'.repeat(32), ageHours: 1.5, stale: false,
        },
        blockers: [],
        warnings: ['TEST chưa có khóa dịch vụ – nút "Báo lỗi / góp ý" trên TEST chưa gửi về đây được (AGT → Tạo khóa dịch vụ…).'],
        keepTables: ['AgentEnvironment', 'AgentCommand', 'BackupRun', 'OperationLog', 'ErrorGroup', 'ErrorOccurrence'],
        openCommandId: null,
      },
    }),
  )
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'TST')
  await expect(page.getByRole('heading', { name: 'Làm mới TEST' })).toBeVisible()
  await expect(page.getByTestId('tst-notice')).toContainText('Đủ điều kiện làm mới')
  await expect(page.getByTestId('tst-notice')).toContainText('khóa dịch vụ')
  await expect(page.locator('.data-grid-row', { hasText: 'Phiên bản phần mềm' })).toContainText('0.37.0')
  await expect(page.locator('.data-grid-row', { hasText: 'Khóa dịch vụ' })).toContainText('Cần xem')
  await page.screenshot({ path: 'e2e/screenshots/test-refresh.png' })

  await page.getByRole('button', { name: 'Làm mới TEST…' }).click()
  const dialog = page.getByRole('dialog', { name: 'Làm mới TEST từ PROD' })
  await expect(dialog).toContainText('thay toàn bộ dữ liệu TEST bằng dữ liệu PROD')
  await dialog.getByLabel('Lý do').fill('UAT bản 0.37.1')
  await dialog.getByLabel('Mật khẩu của bạn').fill('sai-mat-khau')
  await dialog.getByLabel(/Gõ "LÀM MỚI TEST"/).fill('LAM MOI')
  await dialog.getByRole('button', { name: 'Làm mới TEST' }).click()
  await expect(dialog).toContainText('Gõ đúng "LÀM MỚI TEST".')
  await page.screenshot({ path: 'e2e/screenshots/test-refresh-dialog.png' })
  await dialog.getByRole('button', { name: 'Không làm mới' }).click()

  await page.getByRole('tab', { name: /Lịch sử/ }).click()
  await expect(page.getByText('Từ ngày – Đến ngày')).toBeVisible()
})

test('Máy TEST: nút "Báo lỗi / góp ý" – tiêu đề, màn hình điền sẵn, ảnh chọn file, gửi về PROD', async ({ page }) => {
  await page.route('**/api/ops/v1/environment', (route) =>
    route.fulfill({ json: { environment: 'Test', warning: null, maintenance: null, feedbackEnabled: true } }),
  )
  let sent: Record<string, unknown> | null = null
  await page.route('**/api/ops/v1/feedback', async (route) => {
    sent = route.request().postDataJSON() as Record<string, unknown>
    await route.fulfill({
      json: { version: '0.37.0', ticketStatus: 'Đang UAT', openDefects: 3, message: 'Đã ghi lỗi vào phiếu phát hành bản 0.37.0 (Đang UAT) – phiếu có 3 lỗi chưa xử lý.' },
    })
  })
  await loginViaApi(page)
  await page.goto('/hang-ngay/thong-bao')
  await page.getByRole('button', { name: /Báo lỗi \/ góp ý/ }).click()
  const dialog = page.getByRole('dialog', { name: 'Báo lỗi / góp ý' })
  await expect(dialog.getByLabel('Màn hình', { exact: true })).toHaveValue('/hang-ngay/thong-bao')
  await dialog.getByRole('button', { name: 'Gửi' }).click()
  await expect(dialog).toContainText('Nhập tiêu đề lỗi.')
  await dialog.getByLabel('Tiêu đề').fill('Thông báo không đánh dấu đã đọc')
  await dialog.getByLabel('Mô tả').fill('Bấm thông báo nhưng vẫn in đậm')
  await dialog.locator('input[type=file]').setInputFiles({ name: 'loi.png', mimeType: 'image/png', buffer: png })
  await expect(dialog.getByTestId('feedback-paste')).toContainText('loi.png')
  await page.screenshot({ path: 'e2e/screenshots/feedback-dialog.png' })
  await dialog.getByRole('button', { name: 'Gửi' }).click()
  await expect(page.getByText('Đã ghi lỗi vào phiếu phát hành bản 0.37.0')).toBeVisible()
  expect(sent).toMatchObject({ title: 'Thông báo không đánh dấu đã đọc', screen: '/hang-ngay/thong-bao', imageFileName: 'loi.png' })
  expect((sent as unknown as { imageBase64: string }).imageBase64).toBe(png.toString('base64'))
})
