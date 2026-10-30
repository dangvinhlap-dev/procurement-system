import { expect, test, type Page } from '@playwright/test'
import { loginViaApi } from './helpers.ts'

/**
 * Danh mục công ty và kho (design.md mục 4, 5.1, 5.6): thêm công ty kèm kho tổng ở tổng công ty, vào làm việc ở công ty mới
 * ngay, thêm kho phụ; công ty thành viên chỉ xem công ty mình.
 * Dữ liệu Testing không bị xóa giữa các lần chạy Playwright nên mã công ty luôn mới.
 */

/** Mã công ty mới: chữ hoa + số, tối đa 10 ký tự. */
const uniqueCode = () => `E${Date.now().toString(36).slice(-6)}${Math.floor(Math.random() * 100)}`.toUpperCase()

async function openByCommand(page: Page, code: string) {
  const box = page.getByPlaceholder('Ô lệnh – tên màn hình')
  await box.fill(code)
  await box.press('Enter')
}

async function switchCompany(page: Page, label: string) {
  await page.getByTestId('menu-bar').locator('.app-company-select').click()
  await page.locator('.ant-select-dropdown .ant-select-item-option', { hasText: label }).click()
}

function gridRow(page: Page, text: string) {
  return page.locator('.data-grid-row', { hasText: text })
}

test.describe('danh mục công ty và kho', () => {
  test.beforeEach(async ({ page }) => {
    await loginViaApi(page)
    await page.goto('/')
  })

  test('thêm công ty ở tổng công ty, chuyển sang công ty mới, thêm kho phụ', async ({ page }) => {
    const code = uniqueCode()
    await openByCommand(page, 'COMP')
    await expect(page.getByRole('heading', { name: 'Danh mục công ty' })).toBeVisible()
    await expect(gridRow(page, 'Tổng công ty').first()).toBeVisible()

    await page.getByRole('button', { name: 'Tạo mới' }).click()
    const drawer = page.locator('.detail-drawer')
    await drawer.getByLabel('Mã công ty').fill(code)
    await drawer.getByLabel('Tên công ty').fill(`Công ty thử ${code}`)
    // Mã số thuế sai định dạng thì báo ngay, chưa gửi lên server.
    await drawer.getByLabel('Mã số thuế').fill('12345')
    await page.keyboard.press('Control+s')
    await expect(drawer.getByText('10 chữ số, hoặc 10 chữ số + "-" + 3 chữ số.')).toBeVisible()
    await drawer.getByLabel('Mã số thuế').fill('')
    await drawer.getByLabel('Tên kho tổng').fill(`Kho tổng ${code}`)
    await page.keyboard.press('Control+s')

    await expect(page.locator('.ant-message')).toContainText(`Đã thêm công ty ${code}.`)
    await expect(gridRow(page, code)).toContainText('Thành viên')

    // Người thêm được gán vào công ty mới nên chọn được ngay trên thanh menu.
    await switchCompany(page, code)
    await openByCommand(page, 'WH')
    await expect(page.getByRole('heading', { name: `Danh mục kho – ${code}` })).toBeVisible()
    await expect(gridRow(page, `Kho tổng ${code}`)).toContainText('Kho tổng')

    await page.getByRole('button', { name: 'Tạo mới' }).click()
    await drawer.getByLabel('Mã kho').fill('kp1')
    await drawer.getByLabel('Tên kho').fill('Kho phụ xưởng 1')
    await drawer.getByLabel('Thủ kho').fill('Trần Bình')
    await drawer.getByRole('button', { name: 'Lưu' }).click()
    await expect(page.locator('.ant-message')).toContainText('Đã thêm kho KP1.')
    const sub = gridRow(page, 'Kho phụ xưởng 1')
    await expect(sub).toContainText('Kho phụ')
    await expect(sub).toContainText('KT')
    await expect(sub).toContainText('Trần Bình')

    // Kho tổng không ngừng dùng được.
    await gridRow(page, `Kho tổng ${code}`).dblclick()
    await expect(drawer.getByText(`Kho: KT`)).toBeVisible()
    await expect(drawer.getByRole('checkbox', { name: 'Còn sử dụng' })).toBeDisabled()
    await page.keyboard.press('Escape')
    await expect(drawer).toHaveCount(0)
    await page.waitForLoadState('networkidle')
    await page.screenshot({ path: 'e2e/screenshots/warehouse-list.png' })
  })

  test('ở công ty thành viên chỉ xem được công ty mình', async ({ page }) => {
    await switchCompany(page, 'BHA')
    await openByCommand(page, 'COMP')
    await expect(page.getByText('Đang làm việc ở công ty thành viên nên chỉ xem được công ty mình.', { exact: false })).toBeVisible()
    await expect(page.locator('.data-grid-row')).toHaveCount(1)
    await expect(gridRow(page, 'BHA')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Tạo mới' })).toHaveCount(0)

    await gridRow(page, 'BHA').dblclick()
    const drawer = page.locator('.detail-drawer')
    await expect(drawer.getByText('Công ty: BHA')).toBeVisible()
    await expect(drawer.getByRole('button', { name: 'Lưu' })).toHaveCount(0)
  })

  test('chụp danh mục công ty ở tổng công ty', async ({ page }) => {
    await openByCommand(page, 'COMP')
    await expect(gridRow(page, 'BHA')).toBeVisible()
    await page.waitForLoadState('networkidle')
    await page.screenshot({ path: 'e2e/screenshots/company-list.png' })
  })
})
