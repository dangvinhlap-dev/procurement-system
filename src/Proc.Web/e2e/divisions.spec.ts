import { expect, test } from '@playwright/test'
import { ADMIN, THUKHO_BHA, loginViaApi } from './helpers.ts'
import { chooseOption, openByCommand, pickOption, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Khối – Giám đốc khối – Trưởng phòng ban (lát O1, design.md mục 19.3): thêm khối ở BHA có Giám đốc khối là tài khoản có quyền tại
 * công ty, gán bộ phận vào khối kèm Trưởng phòng ban; danh sách bộ phận hiện khối, Trưởng phòng ban, Giám đốc khối.
 * Dữ liệu Testing không bị xóa giữa các lần chạy nên mã luôn mới.
 */
test('thêm khối, gán bộ phận vào khối kèm Trưởng phòng ban, chụp màn hình', async ({ page }) => {
  const divisionCode = unique('kh')
  const departmentCode = unique('bp')
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')

  await openByCommand(page, 'DIV')
  await expect(page.getByRole('heading', { name: 'Danh mục khối – BHA' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  const drawer = page.locator('.detail-drawer')
  await drawer.getByLabel('Mã khối').fill(divisionCode.toLowerCase())
  await drawer.getByLabel('Tên khối').fill(`Khối thử ${divisionCode}`)
  await chooseOption(page, 'Giám đốc khối', ADMIN.fullName)
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(`Đã thêm khối ${divisionCode}.`)
  await expect(page.locator('.data-grid-row', { hasText: divisionCode })).toContainText(ADMIN.fullName)

  await openByCommand(page, 'DEPT')
  // Chờ màn hình bộ phận mở xong – bấm "Tạo mới" sớm sẽ trúng nút của màn hình khối đang đóng.
  await expect(page.getByRole('heading', { name: 'Danh mục bộ phận – BHA' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await drawer.getByLabel('Mã bộ phận').fill(departmentCode)
  await drawer.getByLabel('Tên bộ phận').fill(`Phòng ${departmentCode}`)
  await chooseOption(page, 'Khối', divisionCode)
  await chooseOption(page, 'Trưởng phòng ban (tài khoản)', THUKHO_BHA.fullName)
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(`Đã thêm bộ phận ${departmentCode}.`)
  const row = page.locator('.data-grid-row', { hasText: departmentCode })
  await expect(row).toContainText(`${divisionCode} – Khối thử ${divisionCode}`)
  await expect(row).toContainText(THUKHO_BHA.fullName)
  await expect(row).toContainText(ADMIN.fullName)

  await openByCommand(page, 'DIV')
  await expect(page.locator('.data-grid-row', { hasText: divisionCode })).toContainText('1')
  await page.waitForLoadState('networkidle')
  await page.screenshot({ path: 'e2e/screenshots/division-list.png' })
})

/**
 * Quy trình duyệt (APRL) chọn người duyệt động (lát O1): loại PR chọn được "Trưởng phòng ban của phiếu" (không cần vai trò), loại không có
 * bộ phận (bảng giá NCC) khóa ô người duyệt. Cấp 9 ngưỡng rất lớn để không ảnh hưởng phiếu của test khác; xóa ngay sau khi kiểm tra.
 */
test('quy trình duyệt: cấp do Trưởng phòng ban của phiếu duyệt', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/he-thong/quy-trinh-duyet')
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  const drawer = page.locator('.detail-drawer')
  await drawer.getByLabel('Cấp').fill('9')
  await drawer.getByLabel('Từ giá trị (đ)').fill('999999999999')
  // Ô người duyệt không có tìm kiếm (3 lựa chọn cố định) – bấm mở rồi chọn.
  await drawer.getByRole('combobox', { name: 'Người duyệt', exact: true }).click()
  await pickOption(page, 'Trưởng phòng ban của phiếu')
  await expect(drawer.getByLabel('Vai trò duyệt')).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/approval-rule-dynamic.png', animations: 'disabled' })
  await drawer.getByRole('button', { name: 'Lưu' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã lưu cấp duyệt.')
  const row = page.locator('.data-grid-row', { hasText: 'Trưởng phòng ban của phiếu' }).filter({ hasText: '(PR)' })
  await expect(row).toHaveCount(1)

  await row.dblclick()
  await expect(drawer.getByLabel('Người duyệt')).toBeEnabled()
  await drawer.getByRole('button', { name: 'Xóa cấp này' }).click()
  await page.getByRole('button', { name: 'Xóa', exact: true }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã xóa cấp duyệt.')

  // Loại không có bộ phận: chỉ theo vai trò.
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  // Gõ để lọc – danh sách loại phiếu dài (thêm DNT ở lát Y8), mục cần chọn có thể nằm ngoài vùng hiện của ô chọn.
  await drawer.getByLabel('Loại phiếu').fill('Bảng giá NCC')
  await page.locator('.ant-select-item-option', { hasText: 'Bảng giá NCC (PRL)' }).click()
  await expect(drawer.getByText('Loại phiếu này không có bộ phận – chỉ duyệt theo vai trò.')).toBeVisible()
})
