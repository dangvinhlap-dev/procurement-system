import { expect, test, type Page } from '@playwright/test'
import { loginViaApi } from './helpers.ts'

/**
 * Thẻ màn hình (tối đa 4 màn hình mở cùng lúc – docs/ui-style.md mục 8a): bấm cây điều hướng mở thẻ mới, đã mở thì chuyển sang, đủ 4 thẻ
 * thì báo; chuyển thẻ giữ nguyên dữ liệu đang nhập; Alt+số chuyển thẻ; đóng thẻ; ô lệnh /n mở thay ở thẻ đang xem.
 */

/** Các thẻ màn hình; thẻ đang xem có nút chính aria-current="page". */
const tabs = (page: Page) => page.getByTestId('screen-tabs').locator('.app-screen-tab')
/** Mục cây điều hướng (màn hình đã ghim hiện hai lần – lấy mục đầu). */
const nav = (page: Page, label: string) => page.getByTestId('nav-tree').getByRole('button', { name: new RegExp(label) }).first()
/** Ô Tìm kiếm của danh sách ở thẻ đang xem (thẻ ẩn có `inert`, nằm ngoài cây trợ năng). */
const visibleSearch = (page: Page) => page.locator('.app-tab-panel.is-active input[data-list-search]')

test('mở tối đa 4 màn hình, chuyển thẻ giữ nguyên dữ liệu đang nhập', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/')
  await expect(tabs(page)).toHaveCount(1)
  await expect(page.getByTestId('screen-tabs')).toContainText('1/4 màn hình')

  await nav(page, 'Đơn mua hàng').click()
  await expect(page).toHaveURL(/\/mua-hang\/po$/)
  await expect(tabs(page)).toHaveCount(2)
  await visibleSearch(page).fill('thu-the-man-hinh')

  await nav(page, 'Đề nghị mua hàng').click()
  await expect(page).toHaveURL(/\/mua-hang\/pr$/)
  await nav(page, 'Nhà cung cấp').click()
  await expect(tabs(page)).toHaveCount(4)
  await expect(page.getByTestId('screen-tabs')).toContainText('4/4 màn hình')

  // Thẻ thứ 5: báo, không mở.
  await nav(page, 'Thông báo').click()
  await expect(page.getByText('Đã mở đủ 4 màn hình')).toBeVisible()
  await expect(tabs(page)).toHaveCount(4)
  await expect(page).toHaveURL(/\/gia-ncc\/nha-cung-cap$/)

  // Bấm lại màn hình đã mở: chuyển sang thẻ đó, chữ đang gõ ở ô tìm vẫn còn.
  await nav(page, 'Đơn mua hàng').click()
  await expect(page).toHaveURL(/\/mua-hang\/po$/)
  await expect(tabs(page).nth(1)).toHaveClass(/is-active/)
  await expect(visibleSearch(page)).toHaveValue('thu-the-man-hinh')

  // Alt+3 sang thẻ thứ ba.
  await page.keyboard.press('Alt+3')
  await expect(page).toHaveURL(/\/mua-hang\/pr$/)

  await page.screenshot({ path: test.info().outputPath('the-man-hinh.png') })

  // Đóng thẻ đang xem: hiện thẻ bên phải.
  await tabs(page).nth(2).getByRole('button', { name: /Đóng màn hình/ }).click()
  await expect(tabs(page)).toHaveCount(3)
  await expect(page).toHaveURL(/\/gia-ncc\/nha-cung-cap$/)

  // Ô lệnh /nPR: mở thay ở thẻ đang xem, không thêm thẻ.
  const command = page.getByRole('combobox', { name: 'Ô lệnh – tên màn hình' })
  await command.fill('/nPR')
  await command.press('Enter')
  await expect(page).toHaveURL(/\/mua-hang\/pr$/)
  await expect(tabs(page)).toHaveCount(3)
  await expect(tabs(page).nth(2)).toContainText(/Đề nghị mua hàng/i)

  // Nút Quay lại dùng lịch sử riêng của thẻ: về Nhà cung cấp.
  await page.getByRole('button', { name: 'Quay lại' }).click()
  await expect(page).toHaveURL(/\/gia-ncc\/nha-cung-cap$/)

  // F5: mở lại đủ thẻ.
  await page.reload()
  await expect(tabs(page)).toHaveCount(3)
  await expect(tabs(page).nth(2)).toHaveClass(/is-active/)
})

test('mọi thẻ thuộc công ty đang làm việc – đổi công ty hỏi lại rồi chỉ giữ một màn hình', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/mua-hang/po')
  await nav(page, 'Đề nghị mua hàng').click()
  await expect(tabs(page)).toHaveCount(2)
  const before = await page.getByTestId('screen-tabs').locator('.app-screen-tabs-count').textContent()

  // Chọn công ty khác công ty đang làm việc.
  await page.getByTestId('shell-bar').locator('.app-company-select').click()
  await page.locator('.ant-select-dropdown .ant-select-item-option:not(.ant-select-item-option-selected)').first().click()
  const dialog = page.getByRole('dialog', { name: 'Đổi công ty đang làm việc' })
  await expect(dialog).toContainText('sẽ đóng các màn hình này')

  // Không đổi: giữ nguyên 2 thẻ.
  await dialog.getByRole('button', { name: 'Không đổi' }).click()
  await expect(tabs(page)).toHaveCount(2)

  await page.getByTestId('shell-bar').locator('.app-company-select').click()
  await page.locator('.ant-select-dropdown .ant-select-item-option:not(.ant-select-item-option-selected)').first().click()
  await page.getByRole('dialog', { name: 'Đổi công ty đang làm việc' }).getByRole('button', { name: 'Đổi công ty' }).click()
  await expect(tabs(page)).toHaveCount(1)
  await expect(page).toHaveURL(/\/mua-hang\/pr$/)
  await expect(page.getByTestId('screen-tabs').locator('.app-screen-tabs-count')).not.toHaveText(before ?? '')
})
