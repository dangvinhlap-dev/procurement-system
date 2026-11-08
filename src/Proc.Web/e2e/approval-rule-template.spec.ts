import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Cấu hình duyệt mẫu theo sơ đồ phê duyệt mua sắm (lát G10 – design.md mục 18.25): ở một công ty mới (không đụng cấu hình duyệt của công ty
 * mẫu mà các test khác dùng), mở "Cấu hình duyệt mẫu theo sơ đồ…" trên màn hình Quy trình duyệt, xem trước rồi tạo; PO có 3 cấp
 * Phó ban – Phó ban – Chủ tịch/TGĐ; mở lại thì không còn cấp cần tạo.
 */
test('áp cấu hình duyệt mẫu theo sơ đồ cho công ty mới', async ({ page, request }) => {
  const head = await api(request)
  // Vai trò mẫu phải có trước (CSDL Testing dùng chung – có thể đã tạo).
  await head.send('post', '/api/roles/templates', {})
  const code = unique('G').slice(0, 8)
  await head.send('post', '/api/companies', {
    code, name: `Công ty thử duyệt mẫu ${code}`, shortName: null, taxCode: null, address: null, representative: null,
    startDate: new Date().toISOString().slice(0, 8) + '01', mainWarehouseCode: 'KT', mainWarehouseName: 'Kho tổng',
  })

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, code)
  await page.goto('/he-thong/quy-trinh-duyet')
  await expect(page.getByRole('heading', { name: `Quy trình duyệt – ${code}` })).toBeVisible()
  await page.getByRole('button', { name: /Cấu hình duyệt mẫu theo sơ đồ/ }).click()
  const dialog = page.getByRole('dialog', { name: `Cấu hình duyệt mẫu theo sơ đồ – ${code}` })
  const table = dialog.getByRole('table', { name: 'Cấu hình duyệt mẫu' })
  await expect(table).toContainText('PO – Đơn mua hàng')
  await expect(table).toContainText('PHOBAN – Phó ban cung ứng (Hội đồng)')
  await expect(table).toContainText('Chỉ ở tổng công ty')
  await page.screenshot({ path: 'e2e/screenshots/approval-rule-template.png' })

  await dialog.getByRole('button', { name: /^Tạo \d+ cấp duyệt$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã tạo \d+ cấp duyệt theo sơ đồ\./)
  await expect(page.locator('.data-grid-row', { hasText: 'CHUTICH' }).first()).toBeVisible()

  await page.getByRole('button', { name: /Cấu hình duyệt mẫu theo sơ đồ/ }).click()
  await expect(dialog.getByRole('button', { name: 'Không có cấp cần tạo' })).toBeDisabled()
  await expect(table).toContainText('Đã cấu hình – giữ nguyên')
})
