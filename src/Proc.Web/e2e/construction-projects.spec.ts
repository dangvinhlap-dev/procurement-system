import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, chooseOption, openByCommand, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Công trình – dự toán (CTDT – lát Y2, design.md mục 19.2): quản trị BHA tạo công trình với tổng dự toán 64.000.000, danh sách hiện
 * "Đang thực hiện"; mở lại, điều chỉnh dự toán lên 70.000.000 thì tab Dự toán có dòng "Điều chỉnh 1" chênh +6.000.000. Quy tắc chi tiết
 * (mã trùng, hoàn thành không điều chỉnh, phạm vi công ty, quyền) kiểm tra ở test tích hợp ConstructionProjectTests.
 */
test('tạo công trình, điều chỉnh dự toán, chụp màn hình', async ({ page, request }) => {
  const bha = await api(request, 'BHA')
  // Phòng ban riêng của test – dữ liệu Testing không bị xóa giữa các lần chạy.
  const deptCode = unique('QX').slice(0, 10)
  await bha.send('post', '/api/departments', { code: deptCode, name: `Phòng xây dựng ${deptCode}`, managerName: null })
  const code = unique('CT')
  const name = `Thi công nhà kho ${code}`

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'CTDT')
  await expect(page.getByRole('heading', { name: 'Công trình – dự toán' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  const drawer = page.locator('.detail-drawer')
  await drawer.getByLabel('Mã công trình').fill(code)
  await drawer.getByLabel('Tên công trình').fill(name)
  await chooseOption(page, 'Phòng ban đề xuất', deptCode)
  await drawer.getByLabel('Địa điểm').fill('Khu A')
  await drawer.getByLabel('Tổng dự toán (VND, chưa VAT)').fill('64000000')
  await drawer.getByLabel('Căn cứ dự toán').fill('QĐ 12/QĐ-CT')
  await drawer.getByLabel('Ghi chú', { exact: true }).fill('Thử e2e')
  await expect(drawer.getByRole('tab', { name: /Đính kèm/i })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/construction-project-new.png', animations: 'disabled' })
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(`Đã tạo công trình ${code}`)

  const row = page.locator('.data-grid-row', { hasText: code })
  await expect(row).toContainText('64.000.000')
  await expect(row).toContainText('Đang thực hiện')
  await page.waitForLoadState('networkidle')
  await page.screenshot({ path: 'e2e/screenshots/construction-project-list.png' })

  await row.dblclick()
  await drawer.getByRole('button', { name: 'Điều chỉnh dự toán' }).click()
  const dialog = page.locator('.ant-modal', { hasText: `Điều chỉnh dự toán – ${code}` })
  await dialog.getByLabel('Tổng dự toán mới (VND, chưa VAT)').fill('70000000')
  await dialog.getByLabel('Căn cứ').fill('QĐ 15/QĐ-CT')
  await dialog.getByLabel('Lý do').fill('Bổ sung hạng mục mái che')
  await dialog.getByRole('button', { name: 'Ghi điều chỉnh' }).click()
  await expect(page.locator('.ant-message')).toContainText('70.000.000')

  await expect(drawer.getByLabel('Tổng dự toán hiện hành')).toHaveValue('70.000.000')
  await drawer.getByRole('tab', { name: /Dự toán/i }).click()
  const history = drawer.getByRole('table', { name: 'Lịch sử dự toán công trình' })
  await expect(history).toContainText('Ban đầu')
  await expect(history.locator('tr', { hasText: 'Điều chỉnh 1' })).toContainText('+6.000.000')
  await page.screenshot({ path: 'e2e/screenshots/construction-project-estimate.png', animations: 'disabled' })
})
