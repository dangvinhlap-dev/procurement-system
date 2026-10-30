import { expect, test, type Page } from '@playwright/test'
import { loginViaApi } from './helpers.ts'

/**
 * Danh mục đơn vị tính, nhóm hàng, hàng hóa (design.md mục 5.2, 5.7): thêm ở tổng công ty, tìm không dấu,
 * quy đổi ĐVT; công ty thành viên chỉ xem.
 * Dữ liệu Testing không bị xóa giữa các lần chạy Playwright nên mã luôn mới.
 */

/** Mã mới: chữ hoa + số, 8 ký tự. */
const uniqueCode = (prefix: string) => `${prefix}${Date.now().toString(36).slice(-5)}${Math.floor(Math.random() * 100)}`.toUpperCase()

async function openByCommand(page: Page, code: string) {
  const box = page.getByPlaceholder('Ô lệnh – tên màn hình')
  await box.fill(code)
  await box.press('Enter')
}

async function switchCompany(page: Page, label: string) {
  await page.getByTestId('menu-bar').locator('.app-company-select').click()
  await page.locator('.ant-select-dropdown .ant-select-item-option', { hasText: label }).click()
}

/** Chọn một mục trong Select/TreeSelect của antd đang mở trong ngăn kéo. */
async function pick(page: Page, label: string, option: string) {
  await page.locator('.detail-drawer').getByLabel(label, { exact: true }).click()
  await page.locator('.ant-select-dropdown:visible').getByTitle(option, { exact: false }).first().click()
}

function gridRow(page: Page, text: string) {
  return page.locator('.data-grid-row', { hasText: text })
}

test.describe('danh mục hàng hóa', () => {
  test.beforeEach(async ({ page }) => {
    await loginViaApi(page)
    await page.goto('/')
  })

  test('thêm nhóm hàng và hàng hóa có quy đổi ĐVT, tìm không dấu', async ({ page }) => {
    const drawer = page.locator('.detail-drawer')
    const categoryCode = uniqueCode('N')
    await openByCommand(page, 'CAT')
    await expect(page.getByRole('heading', { name: 'Nhóm hàng' })).toBeVisible()
    await page.getByRole('button', { name: 'Tạo mới' }).click()
    await drawer.getByLabel('Mã nhóm').fill(categoryCode)
    await drawer.getByLabel('Tên nhóm hàng').fill('Ống nhựa thử')
    await drawer.getByLabel('Tài khoản kho mặc định').fill('152')
    await page.keyboard.press('Control+s')
    await expect(page.locator('.ant-message')).toContainText(`Đã lưu nhóm hàng ${categoryCode}.`)

    const itemCode = `${categoryCode}-01`
    await openByCommand(page, 'ITEM')
    await expect(page.getByRole('heading', { name: 'Danh mục hàng hóa' })).toBeVisible()
    await page.getByRole('button', { name: 'Tạo mới' }).click()
    await drawer.getByLabel('Mã hàng').fill(itemCode.toLowerCase())
    await drawer.getByLabel('Tên hàng', { exact: true }).fill('Ống nhựa PVC phi 21')
    await pick(page, 'Nhóm hàng', categoryCode)
    await pick(page, 'ĐVT cơ sở', 'M – Mét')
    await drawer.getByRole('tab', { name: 'Quy đổi ĐVT' }).click()
    await drawer.getByRole('button', { name: 'Thêm ĐVT quy đổi' }).click()
    await pick(page, 'ĐVT quy đổi', 'CUON – Cuộn')
    await drawer.getByLabel(/Hệ số/).fill('100')
    await page.keyboard.press('Control+s')
    await expect(page.locator('.ant-message')).toContainText(`Đã thêm hàng ${itemCode}.`)

    // Gõ không dấu vẫn tìm được "Ống nhựa".
    await page.getByPlaceholder(/Gõ từ khóa/).fill('ong nhua phi 21')
    const row = gridRow(page, itemCode)
    await expect(row).toContainText('Ống nhựa PVC phi 21')
    await expect(row).toContainText('Hàng tồn kho')
    await expect(row).toContainText('10%')

    await row.dblclick()
    await expect(drawer.getByText(`Hàng hóa: ${itemCode}`)).toBeVisible()
    await drawer.getByRole('tab', { name: 'Quy đổi ĐVT' }).click()
    await expect(drawer.getByLabel(/Hệ số/)).toHaveValue('100,000000')
    // Đổi sang dịch vụ thì khóa các ô quản lý kho.
    await drawer.getByRole('tab', { name: 'Thông tin chung' }).click()
    await pick(page, 'Loại hàng', 'Dịch vụ')
    await drawer.getByRole('tab', { name: 'Quản lý kho' }).click()
    await expect(drawer.getByRole('checkbox', { name: 'Quản lý theo lô' })).toBeDisabled()
    await drawer.getByRole('button', { name: 'Đóng' }).click()
    await expect(drawer).toHaveCount(0)
    await page.waitForLoadState('networkidle')
    await page.screenshot({ path: 'e2e/screenshots/item-list.png' })
  })

  test('đơn vị tính: thêm mới, trùng mã thì báo lỗi', async ({ page }) => {
    const drawer = page.locator('.detail-drawer')
    const code = uniqueCode('U')
    await openByCommand(page, 'UOM')
    await expect(gridRow(page, 'Cái')).toBeVisible()
    await page.getByRole('button', { name: 'Tạo mới' }).click()
    await drawer.getByLabel('Mã ĐVT').fill(code)
    await drawer.getByLabel('Tên đơn vị tính').fill('Ram')
    await page.keyboard.press('Control+s')
    await expect(gridRow(page, code)).toContainText('Ram')

    await page.getByRole('button', { name: 'Tạo mới' }).click()
    await drawer.getByLabel('Mã ĐVT').fill('cai')
    await drawer.getByLabel('Tên đơn vị tính').fill('Trùng')
    await page.keyboard.press('Control+s')
    await expect(drawer.getByText('Mã đơn vị tính CAI đã có.')).toBeVisible()
  })

  test('ở công ty thành viên chỉ xem, không thêm được', async ({ page }) => {
    await switchCompany(page, 'BHA')
    await openByCommand(page, 'ITEM')
    await expect(page.getByText('Danh mục dùng chung toàn tập đoàn: chỉ thêm, sửa khi làm việc ở tổng công ty.')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Tạo mới' })).toHaveCount(0)
  })
})
