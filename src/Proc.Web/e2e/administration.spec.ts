import { expect, test, type Page } from '@playwright/test'
import { THUKHO_BHA, loginViaApi } from './helpers.ts'

/**
 * Màn hình Người dùng và Vai trò (design.md mục 9.1, 9.2, 9.5, 10.9): danh sách + ngăn kéo chi tiết có tab.
 * Dữ liệu Testing không bị xóa giữa các lần chạy Playwright nên tên tạo mới luôn kèm số ngẫu nhiên.
 */

const unique = () => Date.now().toString(36) + Math.floor(Math.random() * 1000)

async function openScreen(page: Page, navLabel: string) {
  await page
    .getByTestId('nav-tree')
    .getByRole('button', { name: new RegExp(navLabel) })
    .click()
}

async function switchCompany(page: Page, label: string) {
  await page.getByTestId('menu-bar').locator('.app-company-select').click()
  await page.locator('.ant-select-dropdown .ant-select-item-option', { hasText: label }).click()
}

function gridRow(page: Page, text: string) {
  return page.locator('.data-grid-row', { hasText: text })
}

test.describe('quản trị hệ thống', () => {
  test.beforeEach(async ({ page }) => {
    await loginViaApi(page)
    await page.goto('/')
  })

  test('tạo người dùng tại BHA với vai trò Thủ kho và một kho, rồi mở lại thấy đúng', async ({ page }) => {
    await switchCompany(page, 'BHA')
    await openScreen(page, 'Người dùng')
    await expect(page.getByRole('heading', { name: 'Người dùng' })).toBeVisible()
    await expect(gridRow(page, THUKHO_BHA.userName)).toBeVisible()

    const userName = `e2e.${unique()}`
    await page.getByRole('button', { name: 'Tạo mới' }).click()
    const drawer = page.locator('.detail-drawer')
    await expect(drawer.getByText('Thêm người dùng')).toBeVisible()
    await drawer.getByLabel('Tên đăng nhập').fill(userName)
    await drawer.getByLabel('Họ tên').fill('Người thử Playwright')
    await drawer.getByLabel('Mật khẩu ban đầu').fill('matkhau123')

    await drawer.getByRole('tab', { name: 'Phân quyền tại BHA' }).click()
    await drawer.getByRole('checkbox', { name: /Thủ kho/ }).check()
    await drawer.getByRole('radio', { name: 'Chỉ các kho chọn dưới đây' }).check()
    // Chọn "chỉ một số kho" mà không tick kho nào thì báo ngay, chưa gửi lên server.
    await drawer.getByRole('button', { name: 'Lưu' }).click()
    await expect(drawer.getByText('Hãy chọn ít nhất một kho.')).toBeVisible()
    await drawer.getByRole('checkbox', { name: /KP2 – Kho phụ xưởng cán 2/ }).check()
    await page.keyboard.press('Control+s')

    await expect(page.locator('.ant-message')).toContainText(`Đã tạo người dùng ${userName}.`)
    const row = gridRow(page, userName)
    await expect(row).toContainText('Thủ kho')
    await expect(row).toContainText('Đang dùng')

    await row.dblclick()
    await expect(drawer.getByText(`Người dùng: ${userName}`)).toBeVisible()
    await expect(drawer.getByRole('button', { name: 'Đặt lại mật khẩu' })).toBeVisible()
    await drawer.getByRole('tab', { name: 'Phân quyền tại BHA' }).click()
    await expect(drawer.getByRole('checkbox', { name: /KP2 – Kho phụ xưởng cán 2/ })).toBeChecked()
    await expect(drawer.getByRole('checkbox', { name: /KP1 – Kho phụ xưởng cán 1/ })).not.toBeChecked()
  })

  test('mở tài khoản của chính mình thì không sửa được phân quyền', async ({ page }) => {
    await openScreen(page, 'Người dùng')
    // Tìm theo đúng ô tên đăng nhập: test tích hợp tạo thêm nhiều quản trị cùng vai trò "Quản trị hệ thống" ở TCT.
    await page.locator('.data-grid-row').filter({ has: page.getByRole('cell', { name: 'admin', exact: true }) }).dblclick()
    const drawer = page.locator('.detail-drawer')
    await drawer.getByRole('tab', { name: /Phân quyền tại/ }).click()
    await expect(drawer.getByText('Không được tự sửa quyền của chính mình.')).toBeVisible()
    await expect(drawer.getByRole('checkbox', { name: /Quản trị hệ thống/ })).toBeDisabled()
    await page.keyboard.press('Escape')
    await expect(drawer).toHaveCount(0)
  })

  test('lọc "Tất cả tài khoản" hiện cả người không làm việc ở công ty đang chọn', async ({ page }) => {
    await openScreen(page, 'Người dùng')
    // Admin mặc định làm việc ở tổng công ty, nơi thủ kho BHA không được gán.
    await expect(gridRow(page, THUKHO_BHA.userName)).toHaveCount(0)
    await page.getByLabel('Phạm vi').click()
    await page.locator('.ant-select-dropdown .ant-select-item-option', { hasText: 'Tất cả tài khoản' }).click()
    await expect(gridRow(page, THUKHO_BHA.userName)).toContainText('BHA')
  })

  test('tạo vai trò, tick quyền theo nhóm; vai trò ADMIN chỉ xem', async ({ page }) => {
    await openScreen(page, 'Vai trò')
    await expect(page.getByRole('heading', { name: 'Vai trò và phân quyền' })).toBeVisible()

    await gridRow(page, 'ADMIN').dblclick()
    const drawer = page.locator('.detail-drawer')
    await expect(drawer.getByText(/Vai trò hệ thống: luôn có đủ mọi quyền/)).toBeVisible()
    await expect(drawer.getByRole('button', { name: 'Lưu' })).toHaveCount(0)
    await drawer.getByRole('button', { name: 'Đóng' }).click()

    const code = `E2E${unique()}`.toUpperCase().slice(0, 20)
    await page.getByRole('button', { name: 'Tạo mới' }).click()
    await drawer.getByLabel('Mã vai trò').fill(code)
    await drawer.getByLabel('Tên vai trò').fill('Kế toán kho thử')
    await drawer.getByRole('tab', { name: 'Quyền' }).click()
    // Tick cả nhóm Kho (9 quyền) rồi bỏ một quyền.
    await drawer.getByRole('group', { name: 'Kho' }).getByRole('checkbox', { name: 'Kho', exact: true }).check()
    await drawer.getByRole('checkbox', { name: 'Bỏ ghi sổ chứng từ kho' }).uncheck()
    await expect(drawer.getByText('Đã chọn 8/')).toBeVisible()
    await drawer.getByRole('button', { name: 'Lưu' }).click()

    await expect(page.locator('.ant-message')).toContainText(`Đã tạo vai trò ${code}.`)
    await expect(gridRow(page, code)).toContainText('Kế toán kho thử')
    await expect(gridRow(page, code)).toContainText('8')
  })
})

test('thủ kho không thấy màn hình Người dùng và Vai trò', async ({ page }) => {
  await loginViaApi(page, THUKHO_BHA)
  await page.goto('/he-thong/nguoi-dung')
  await expect(page.getByText('Bạn không có quyền mở màn hình này')).toBeVisible()
  await expect(page.getByTestId('nav-tree').getByRole('button', { name: /Vai trò/ })).toHaveCount(0)
})
