import { expect, test, type APIRequestContext } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Tạo vai trò mẫu và một công ty mới (không đụng cấu hình duyệt của công ty mẫu mà các test khác dùng).
 * @param request Ngữ cảnh API của Playwright.
 * @param prefix Tiền tố mã công ty.
 * @returns Mã công ty mới.
 */
async function newCompany(request: APIRequestContext, prefix: string) {
  const head = await api(request)
  // Vai trò mẫu phải có trước (CSDL Testing dùng chung – có thể đã tạo).
  await head.send('post', '/api/roles/templates', {})
  const code = unique(prefix).slice(0, 8)
  await head.send('post', '/api/companies', {
    code, name: `Công ty thử duyệt mẫu ${code}`, shortName: null, taxCode: null, address: null, representative: null,
    startDate: new Date().toISOString().slice(0, 8) + '01', mainWarehouseCode: 'KT', mainWarehouseName: 'Kho tổng',
  })
  return code
}

/**
 * Cấu hình duyệt mẫu theo sơ đồ phê duyệt mua sắm (lát G10 – design.md mục 18.25): mở "Cấu hình duyệt mẫu…" trên màn hình Quy trình duyệt,
 * xem trước rồi tạo; PO có 3 cấp Phó ban – Phó ban – Chủ tịch/TGĐ; mở lại thì không còn cấp cần tạo.
 */
test('áp cấu hình duyệt mẫu theo sơ đồ cho công ty mới', async ({ page, request }) => {
  const code = await newCompany(request, 'G')
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, code)
  await page.goto('/he-thong/quy-trinh-duyet')
  await expect(page.getByRole('heading', { name: `Quy trình duyệt – ${code}` })).toBeVisible()
  await page.getByRole('button', { name: 'Cấu hình duyệt mẫu…' }).click()
  const dialog = page.getByRole('dialog', { name: `Cấu hình duyệt mẫu – ${code}` })
  // Bảng có cuộn dọc (antd tách tiêu đề và thân thành hai bảng) – đọc theo nội dung hộp thoại.
  const table = dialog.locator('.ant-table')
  await expect(table).toContainText('PO – Đơn mua hàng')
  await expect(table).toContainText('PHOBAN – Phó ban cung ứng (Hội đồng)')
  await expect(table).toContainText('Chỉ ở tổng công ty')
  await page.screenshot({ path: 'e2e/screenshots/approval-rule-template.png' })

  await dialog.getByRole('button', { name: /^Tạo \d+ cấp duyệt$/ }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã tạo \d+ cấp duyệt theo sơ đồ\./)
  await expect(page.locator('.data-grid-row', { hasText: 'CHUTICH' }).first()).toBeVisible()

  await page.getByRole('button', { name: 'Cấu hình duyệt mẫu…' }).click()
  await expect(dialog.getByRole('button', { name: 'Không có cấp cần tạo' })).toBeDisabled()
  await expect(table).toContainText('Đã cấu hình – giữ nguyên')
})

/**
 * Cấu hình duyệt mẫu theo biểu mẫu CUVT (lát O3 – design.md mục 19.3): tạo bộ vai trò mẫu "Vị trí ký biểu mẫu CUVT" trên màn hình Vai trò, rồi
 * chọn bộ "Theo biểu mẫu CUVT" ở Quy trình duyệt – đề nghị mua 6 cấp, cấp 1 là Trưởng phòng ban của phiếu, cấp 4 GĐ khối của phiếu.
 */
test('áp cấu hình duyệt mẫu theo biểu mẫu CUVT cho công ty mới', async ({ page, request }) => {
  const code = await newCompany(request, 'U')
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, code)

  await page.goto('/he-thong/vai-tro')
  await page.getByRole('button', { name: 'Bộ vai trò mẫu…' }).click()
  const roleDialog = page.getByRole('dialog', { name: 'Bộ vai trò mẫu' })
  await roleDialog.locator('.ant-select').click()
  await page.locator('.ant-select-item-option', { hasText: 'Vị trí ký biểu mẫu CUVT' }).click()
  await expect(roleDialog.getByText('TPCUVT – Trưởng phòng Cung ứng vật tư')).toBeVisible()
  await expect(roleDialog.getByText('PHOCTUBCL – Phó Chủ tịch UBCL&PT')).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/role-templates-cuvt.png', animations: 'disabled' })
  // CSDL Testing dùng chung: bộ CUVT có thể đã tạo từ lần chạy trước.
  const create = roleDialog.getByRole('button', { name: /Tạo \d+ vai trò còn thiếu/ })
  if (await create.isVisible()) {
    await create.click()
    await expect(page.locator('.ant-message')).toContainText('Đã tạo')
  } else {
    await roleDialog.getByRole('button', { name: 'Đóng', exact: true }).last().click()
  }

  await page.goto('/he-thong/quy-trinh-duyet')
  await page.getByRole('button', { name: 'Cấu hình duyệt mẫu…' }).click()
  const dialog = page.getByRole('dialog', { name: `Cấu hình duyệt mẫu – ${code}` })
  await dialog.locator('.ant-select').click()
  await page.locator('.ant-select-item-option', { hasText: 'Theo biểu mẫu CUVT' }).click()
  const table = dialog.locator('.ant-table')
  await expect(table).toContainText('Trưởng phòng ban của phiếu')
  await expect(table).toContainText('GĐ khối của phiếu')
  await expect(table).toContainText('GDTCKT – Giám đốc Tài chính – Nguồn quỹ – Kế toán')
  await page.screenshot({ path: 'e2e/screenshots/approval-rule-template-cuvt.png' })

  // Lát Y3c: thêm cấp Thủ kho nơi nhận, bộ PRA (1A), PRS (DV), PRE (vượt dự toán) – 32 cấp; lát Y5b thêm bảng tổng hợp BTH 4 cấp → 36; lát Y8 thêm đề nghị thanh toán DNT 4 cấp → 40; Trưởng phòng ban có ở PR, PRA, PRS.
  await expect(table).toContainText('Thủ kho nơi nhận')
  await dialog.getByRole('button', { name: 'Tạo 40 cấp duyệt' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã tạo 40 cấp duyệt theo biểu mẫu CUVT.')
  await expect(page.locator('.data-grid-row', { hasText: 'PHOCTUBCL' }).first()).toBeVisible()
  await expect(page.locator('.data-grid-row', { hasText: 'Trưởng phòng ban của phiếu' })).toHaveCount(3)
  await expect(page.locator('.data-grid-row', { hasText: 'Thủ kho nơi nhận' })).toHaveCount(2)
})
