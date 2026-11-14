import { expect, test, type Page } from '@playwright/test'
import { THUKHO_BHA, loginViaApi, confirmCompanySwitch } from './helpers.ts'

/**
 * Màn hình Người dùng và Vai trò (design.md mục 9.1, 9.2, 9.5, 10.9): danh sách + ngăn kéo chi tiết có tab.
 * Dữ liệu Testing không bị xóa giữa các lần chạy Playwright nên tên tạo mới luôn kèm số ngẫu nhiên.
 */

const unique = () => Date.now().toString(36) + Math.floor(Math.random() * 1000)

/** Mở màn hình bằng cách bấm mục trên cây điều hướng. */
async function openScreen(page: Page, navLabel: string) {
  await page
    .getByTestId('nav-tree')
    .getByRole('button', { name: new RegExp(navLabel) })
    .click()
}

/** Đổi công ty đang làm việc bằng ô chọn công ty trên thanh trên cùng. */
async function switchCompany(page: Page, label: string) {
  await page.getByTestId('shell-bar').locator('.app-company-select').click()
  await page.locator('.ant-select-dropdown .ant-select-item-option', { hasText: label }).click()
  await confirmCompanySwitch(page)
}

/** Dòng lưới danh sách có chứa chữ cho trước. */
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
    // Lọc theo tên đăng nhập: lưới cuộn ảo không vẽ dòng ngoài khung nhìn khi CSDL test đã nhiều tài khoản.
    await page.getByLabel('Tìm kiếm', { exact: true }).fill(THUKHO_BHA.userName)
    await expect(gridRow(page, THUKHO_BHA.userName)).toBeVisible()
    await page.getByLabel('Tìm kiếm', { exact: true }).fill('')

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
    // CSDL test tích lũy hàng trăm tài khoản qua các lượt e2e – lưới cuộn ảo không vẽ dòng ngoài khung nhìn, nên lọc theo tên đăng nhập.
    await page.getByLabel('Tìm kiếm', { exact: true }).fill(THUKHO_BHA.userName)
    await expect(gridRow(page, THUKHO_BHA.userName)).toContainText('BHA')
  })

  test('tạo vai trò, tick quyền theo nhóm; vai trò ADMIN chỉ xem', async ({ page }) => {
    await openScreen(page, 'Vai trò')
    await expect(page.getByRole('heading', { name: 'Vai trò và phân quyền' })).toBeVisible()

    await gridRow(page, 'ADMIN').dblclick()
    const drawer = page.locator('.detail-drawer')
    await expect(drawer.getByText(/Vai trò hệ thống: luôn có đủ mọi quyền/)).toBeVisible()
    await expect(drawer.getByRole('button', { name: 'Lưu' })).toHaveCount(0)
    await drawer.getByRole('button', { name: /(^|\s)Đóng$/ }).click()

    const code = `E2E${unique()}`.toUpperCase().slice(0, 20)
    await page.getByRole('button', { name: 'Tạo mới' }).click()
    await drawer.getByLabel('Mã vai trò').fill(code)
    await drawer.getByLabel('Tên vai trò').fill('Kiểm soát nội bộ thử')
    await drawer.getByRole('tab', { name: 'Quyền' }).click()
    // Tick cả nhóm Kiểm soát nội bộ (3 quyền) rồi bỏ một quyền (nhóm Kho gỡ ở lát K3).
    await drawer.getByRole('group', { name: 'Kiểm soát nội bộ' }).getByRole('checkbox', { name: 'Kiểm soát nội bộ', exact: true }).check()
    await drawer.getByRole('checkbox', { name: 'Ghi nhận, kết luận vi phạm (KSNB)' }).uncheck()
    await expect(drawer.getByText('Đã chọn 2/')).toBeVisible()
    await drawer.getByRole('button', { name: 'Lưu' }).click()

    await expect(page.locator('.ant-message')).toContainText(`Đã tạo vai trò ${code}.`)
    await expect(gridRow(page, code)).toContainText('Kiểm soát nội bộ thử')
    await expect(gridRow(page, code)).toContainText('2')
  })

  test('bộ vai trò mẫu Ban Cung ứng: tạo vai trò còn thiếu, mở lại thì đã đủ', async ({ page }) => {
    await openScreen(page, 'Vai trò')
    await page.getByRole('button', { name: 'Bộ vai trò mẫu…' }).click()
    const dialog = page.getByRole('dialog', { name: 'Bộ vai trò mẫu' })
    await expect(dialog.getByText('HOIDONG – Hội đồng xét chọn & duyệt giá')).toBeVisible()
    await page.screenshot({ path: 'e2e/screenshots/role-templates.png', animations: 'disabled' })
    // CSDL Testing dùng chung giữa các lần chạy: có thể đã tạo từ trước.
    const create = dialog.getByRole('button', { name: /Tạo \d+ vai trò còn thiếu/ })
    if (await create.isVisible()) {
      await create.click()
      await expect(page.locator('.ant-message')).toContainText('Đã tạo')
    } else {
      await dialog.getByRole('button', { name: /(^|\s)Đóng$/ }).last().click()
    }
    // Vai trò LANHDAO cũng nhắc tới KSNB trong mô tả – lọc đúng dòng vai trò KSNB.
    await expect(gridRow(page, 'KSNB').filter({ hasText: 'Kiểm soát nội bộ' })).toHaveCount(1)
    await expect(gridRow(page, 'LANHDAO')).toContainText('Lãnh đạo')

    await page.getByRole('button', { name: 'Bộ vai trò mẫu…' }).click()
    await expect(dialog.getByRole('button', { name: 'Đã có đủ' })).toBeDisabled()
  })

  test('quy trình duyệt: chọn được loại bảng giá NCC, loại không xét giá trị khóa ngưỡng', async ({ page }) => {
    await page.goto('/he-thong/quy-trinh-duyet')
    await page.getByRole('button', { name: 'Tạo mới' }).click()
    const drawer = page.locator('.detail-drawer')
    // Danh sách loại dài (thêm PRA, PRS, PRE – lát Y3c) cuộn ảo: gõ tìm từng loại để chắc có trong danh sách.
    const type = drawer.getByLabel('Loại phiếu')
    for (const name of ['Bảng giá NCC (PRL)', 'Kết quả chọn thầu (RFQ)', 'Hợp đồng nguyên tắc (CTR)', 'Đề nghị mua công trình (loại A) (PRA)', 'Hồ sơ nhà cung cấp (NCC)']) {
      await type.fill(name)
      await expect(page.locator('.ant-select-item-option', { hasText: name })).toBeVisible()
    }
    await page.locator('.ant-select-item-option', { hasText: 'Hồ sơ nhà cung cấp (NCC)' }).click()
    await expect(drawer.getByLabel('Từ giá trị (đ)')).toBeDisabled()
    await expect(drawer.getByText('Loại này không xét giá trị')).toBeVisible()
    await page.screenshot({ path: 'e2e/screenshots/approval-rule-types.png', animations: 'disabled' })
  })
})

test('thủ kho không thấy màn hình Người dùng và Vai trò', async ({ page }) => {
  await loginViaApi(page, THUKHO_BHA)
  await page.goto('/he-thong/nguoi-dung')
  await expect(page.getByText('Bạn không có quyền mở màn hình này')).toBeVisible()
  await expect(page.getByTestId('nav-tree').getByRole('button', { name: /Vai trò/ })).toHaveCount(0)
})
