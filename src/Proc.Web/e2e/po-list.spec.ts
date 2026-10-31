import { expect, test, type Page } from '@playwright/test'
import { ADMIN, loginViaApi, appVersion } from './helpers.ts'

/**
 * Test giao diện khung (AppShell, ListPage, DataGrid) trên màn hình Danh sách đơn mua hàng (PO),
 * dùng 30 dòng dữ liệu giả. Đặc tả: docs/ui-guide.md, docs/design.md mục 17.
 */

/** Các dòng dữ liệu của lưới (bỏ dòng tiêu đề nhóm và dòng đo kích thước ẩn của antd). */
const dataRows = (page: Page) => page.locator('.data-grid tbody tr.data-grid-row')

test.beforeEach(async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/mua-hang/po/mau')
  await expect(page.getByRole('heading', { name: 'Danh sách đơn mua hàng (PO)' })).toBeVisible()
})

test('khung Shellbar + Dynamic Page đúng kích thước và thành phần theo ui-guide mục 4', async ({ page }) => {
  const shellBar = page.getByTestId('shell-bar')
  const nav = page.getByTestId('nav-tree')

  expect((await shellBar.boundingBox())?.height).toBe(48)
  expect((await nav.boundingBox())?.width).toBe(240)
  // Bỏ thanh công cụ và thanh trạng thái kiểu Windows (lát UI-b).
  await expect(page.getByTestId('toolbar')).toHaveCount(0)
  await expect(page.getByTestId('status-bar')).toHaveCount(0)

  for (const name of ['Hằng ngày', 'Mua hàng', 'Kho', 'Giá và NCC', 'Đấu thầu', 'Báo cáo', 'Hệ thống']) {
    await expect(shellBar.getByRole('menuitem', { name, exact: true })).toBeVisible()
  }
  await expect(shellBar.getByPlaceholder('Ô lệnh – tên màn hình')).toBeVisible()

  // Mục đang mở trên cây điều hướng được đánh dấu. Số đếm là số thật từ /api/dashboard – kiểm tra ở dashboard.spec.ts.
  const active = nav.locator('[aria-current="page"]').first()
  await expect(active).toContainText('Đơn mua hàng')

  // Menu người dùng thay thanh trạng thái cũ: họ tên, ngày làm việc, phiên bản.
  await shellBar.getByRole('button', { name: `Người dùng: ${ADMIN.fullName}` }).click()
  const userMenu = page.locator('.ant-dropdown:not(.ant-dropdown-hidden) .ant-dropdown-menu')
  await expect(userMenu).toContainText(ADMIN.fullName)
  await expect(userMenu).toContainText(/Ngày làm việc: \d{2}\/\d{2}\/\d{4}/)
  await expect(userMenu).toContainText(`Phiên bản ${appVersion()}`)
  await page.keyboard.press('Escape')

  // Dynamic Page: thu gọn phần đầu trang thì ẩn khu bộ lọc, bảng vẫn còn; mở rộng thì hiện lại.
  await page.getByRole('button', { name: 'Thu gọn phần đầu trang' }).click()
  await expect(page.locator('.list-filter-bar')).toHaveCount(0)
  await expect(dataRows(page).first()).toBeVisible()
  await page.getByRole('button', { name: 'Mở rộng phần đầu trang' }).click()
  await expect(page.locator('.list-filter-bar')).toBeVisible()
})

test('lưới hiện 30 dòng, định dạng ngày và tiền kiểu Việt Nam', async ({ page }) => {
  await expect(page.getByText('Tổng 30 bản ghi')).toBeVisible()
  await expect(dataRows(page)).toHaveCount(30)

  const firstRow = dataRows(page).first()
  await expect(firstRow).toContainText(/PO-[A-Z0-9]+-\d{4}-\d{4}/)
  await expect(firstRow).toContainText(/\d{2}\/\d{2}\/\d{4}/)
  // Tiền VND không số lẻ, phân cách hàng nghìn bằng dấu chấm.
  await expect(firstRow.locator('td.data-grid-col-money')).toHaveText(/^\d{1,3}(\.\d{3})+$/)

  // Dòng cao 32px theo ui-guide.md mục 3 (bảng Fiori, mật độ compact).
  expect((await firstRow.boundingBox())?.height).toBe(32)
})

test('tìm kiếm không dấu vẫn ra kết quả có dấu', async ({ page }) => {
  const search = page.getByPlaceholder('Gõ từ khóa — có dấu hoặc không dấu đều được')
  await search.fill('cho duyet')
  const count = await dataRows(page).count()
  expect(count).toBeGreaterThan(0)
  expect(count).toBeLessThan(30)
  for (const row of await dataRows(page).all()) {
    await expect(row).toContainText('Chờ duyệt')
  }

  await page.getByRole('button', { name: 'Xóa bộ lọc' }).click()
  await expect(dataRows(page)).toHaveCount(30)
})

test('bấm tiêu đề cột để sắp xếp tăng rồi giảm', async ({ page }) => {
  const header = page.locator('.data-grid thead th', { hasText: 'Tổng tiền' })
  const amounts = async () =>
    (await page.locator('.data-grid tbody td.data-grid-col-money').allTextContents()).map((t) =>
      Number(t.replaceAll('.', '')),
    )

  await header.click()
  const asc = await amounts()
  expect(asc).toEqual([...asc].sort((a, b) => a - b))

  await header.click()
  const desc = await amounts()
  expect(desc).toEqual([...desc].sort((a, b) => b - a))
})

test('nhóm theo công ty bằng ô Nhóm theo và bằng kéo tiêu đề cột', async ({ page }) => {
  await page.getByLabel('Nhóm theo').click()
  await page.locator('.ant-select-dropdown').getByText('Công ty', { exact: true }).click()
  await expect(page.locator('.data-grid-group-row').first()).toBeVisible()
  await expect(dataRows(page)).toHaveCount(30)

  await page.getByRole('button', { name: 'Bỏ nhóm' }).click()
  await expect(page.locator('.data-grid-group-row')).toHaveCount(0)

  await page.locator('.data-grid thead th', { hasText: 'Trạng thái' }).dragTo(page.getByTestId('group-zone'))
  await expect(page.getByTestId('group-zone')).toContainText('Trạng thái')
  await expect(page.locator('.data-grid-group-row').first()).toBeVisible()
})

test('chọn cột hiển thị: ẩn cột thì số cột ẩn tăng', async ({ page }) => {
  await page.getByRole('button', { name: /Cột/ }).click()
  await page.getByRole('checkbox', { name: 'Người tạo' }).uncheck()
  await expect(page.locator('.data-grid thead th', { hasText: 'Người tạo' })).toHaveCount(0)
  await expect(page.getByTestId('hidden-column-count')).toHaveText('1')
})

test('bàn phím: mũi tên di chuyển dòng chọn, Enter mở chi tiết', async ({ page }) => {
  await dataRows(page).first().click()
  await expect(dataRows(page).nth(0)).toHaveClass(/is-selected/)
  await page.keyboard.press('ArrowDown')
  await expect(dataRows(page).nth(1)).toHaveClass(/is-selected/)
  const poNo = (await dataRows(page).nth(1).locator('td').nth(1).textContent()) ?? ''
  await page.keyboard.press('Enter')
  await expect(page.locator('.ant-message')).toContainText(poNo)

  // Mũi tên ">" cuối dòng cũng mở chi tiết (ui-guide.md mục 5).
  const thirdNo = (await dataRows(page).nth(2).locator('td').nth(1).textContent()) ?? ''
  await dataRows(page).nth(2).getByRole('button', { name: 'Mở chi tiết' }).click()
  await expect(page.locator('.ant-message')).toContainText(thirdNo)
})

test('ô lệnh: gõ mã màn hình rồi Enter để mở', async ({ page }) => {
  const box = page.getByPlaceholder('Ô lệnh – tên màn hình')
  await box.fill('PR')
  await box.press('Enter')
  await expect(page).toHaveURL(/\/mua-hang\/pr$/)
  await box.fill('PO')
  await box.press('Enter')
  await expect(page).toHaveURL(/\/mua-hang\/po$/)
})

test('chụp màn hình để so với ảnh mẫu docs/mau-man-hinh-danh-sach.png', async ({ page }) => {
  await page.screenshot({ path: 'e2e/screenshots/po-list.png' })
})
