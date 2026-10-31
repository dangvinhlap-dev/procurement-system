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

test('khung ứng dụng đúng kích thước và thành phần theo ui-guide', async ({ page }) => {
  const menuBar = page.getByTestId('menu-bar')
  const toolbar = page.getByTestId('toolbar')
  const nav = page.getByTestId('nav-tree')
  const statusBar = page.getByTestId('status-bar')

  expect((await menuBar.boundingBox())?.height).toBe(28)
  expect((await toolbar.boundingBox())?.height).toBe(32)
  expect((await statusBar.boundingBox())?.height).toBe(22)
  expect((await nav.boundingBox())?.width).toBe(240)

  for (const name of ['Hằng ngày', 'Mua hàng', 'Kho', 'Giá và NCC', 'Đấu thầu', 'Báo cáo', 'Hệ thống']) {
    await expect(menuBar.getByRole('button', { name, exact: true })).toBeVisible()
  }
  await expect(toolbar.getByPlaceholder('Ô lệnh – tên màn hình')).toBeVisible()

  // Mục đang mở trên cây điều hướng được đánh dấu và có số đếm.
  const active = nav.locator('[aria-current="page"]').first()
  await expect(active).toContainText('Đơn mua hàng')
  await expect(active).toContainText('30')

  // Thanh trạng thái: tên màn hình, người dùng, ngày làm việc, phiên bản, INS.
  await expect(statusBar).toContainText('Danh sách đơn mua hàng (PO)')
  await expect(statusBar).toContainText(ADMIN.fullName)
  await expect(statusBar).toContainText(appVersion())
  await expect(statusBar).toContainText('INS')
  await expect(statusBar).toContainText(/\d{2}\/\d{2}\/\d{4}/)
})

test('lưới hiện 30 dòng, định dạng ngày và tiền kiểu Việt Nam', async ({ page }) => {
  await expect(page.getByText('Tổng 30 bản ghi')).toBeVisible()
  await expect(dataRows(page)).toHaveCount(30)

  const firstRow = dataRows(page).first()
  await expect(firstRow).toContainText(/PO-[A-Z0-9]+-\d{4}-\d{4}/)
  await expect(firstRow).toContainText(/\d{2}\/\d{2}\/\d{4}/)
  // Tiền VND không số lẻ, phân cách hàng nghìn bằng dấu chấm.
  await expect(firstRow.locator('td.data-grid-col-money')).toHaveText(/^\d{1,3}(\.\d{3})+$/)

  // Dòng cao 24px theo ui-guide.
  expect((await firstRow.boundingBox())?.height).toBe(24)
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
