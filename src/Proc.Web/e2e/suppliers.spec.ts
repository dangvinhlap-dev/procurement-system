import { expect, test, type Page } from '@playwright/test'
import { loginViaApi } from './helpers.ts'

/**
 * Danh mục nhà cung cấp (design.md mục 5.3, 5.8): thêm NCC kèm người liên hệ và tài khoản ngân hàng, tìm không dấu,
 * trạng thái danh sách đen phải có lý do.
 * Dữ liệu Testing không bị xóa giữa các lần chạy Playwright nên mã và mã số thuế luôn mới.
 */

const uniqueTaxCode = () => String(1_000_000_000 + Math.floor(Math.random() * 8_999_999_999))

async function openByCommand(page: Page, code: string) {
  const box = page.getByPlaceholder('Ô lệnh – tên màn hình')
  await box.fill(code)
  await box.press('Enter')
}

async function pick(page: Page, label: string, option: string) {
  await page.locator('.detail-drawer').getByLabel(label, { exact: true }).click()
  await page.locator('.ant-select-dropdown:visible').getByTitle(option, { exact: false }).first().click()
}

function gridRow(page: Page, text: string) {
  return page.locator('.data-grid-row', { hasText: text })
}

test.describe('danh mục nhà cung cấp', () => {
  test.beforeEach(async ({ page }) => {
    await loginViaApi(page)
    await page.goto('/')
  })

  test('thêm NCC có người liên hệ, tài khoản; tìm không dấu; danh sách đen phải có lý do', async ({ page }) => {
    const drawer = page.locator('.detail-drawer')
    await openByCommand(page, 'SUP')
    await expect(page.getByRole('heading', { name: 'Danh mục nhà cung cấp' })).toBeVisible()

    await page.getByRole('button', { name: 'Tạo mới' }).click()
    // Mã do hệ thống cấp theo dải đánh số (design.md mục 5.11).
    await expect(drawer.getByLabel('Mã NCC')).toBeDisabled()
    await pick(page, 'Nhóm NCC', 'TN – Trong nước')
    await drawer.getByLabel('Tên nhà cung cấp').fill('Công ty TNHH Điện Quang Minh')
    await drawer.getByLabel('Mã số thuế', { exact: true }).fill(uniqueTaxCode())
    await drawer.getByLabel('Điện thoại', { exact: true }).fill('024 3999 8888')

    await drawer.getByRole('tab', { name: 'Người liên hệ' }).click()
    await drawer.getByRole('button', { name: 'Thêm người liên hệ' }).click()
    await drawer.getByLabel('Họ tên').fill('Phạm Thu Hà')
    await drawer.getByRole('tabpanel').getByLabel('Điện thoại').fill('0987 654 321')

    await drawer.getByRole('tab', { name: 'Ngân hàng' }).click()
    await drawer.getByRole('button', { name: 'Thêm tài khoản ngân hàng' }).click()
    await drawer.getByRole('tabpanel').getByLabel('Ngân hàng', { exact: true }).fill('BIDV')
    await drawer.getByLabel('Số tài khoản').fill('1234 5678 90')
    await page.keyboard.press('Control+s')
    const saved = await page.locator('.ant-message').getByText(/Đã thêm NCC /).textContent()
    const code = /Đã thêm NCC (\S+)\./.exec(saved ?? '')![1]
    expect(code).toMatch(/^NCC\d{6}$/)

    await page.getByPlaceholder(/Gõ từ khóa/).fill('dien quang minh')
    const row = gridRow(page, code)
    await expect(row).toContainText('Công ty TNHH Điện Quang Minh')
    await expect(row).toContainText('Phạm Thu Hà – 0987 654 321')
    await expect(row).toContainText('Tiềm năng')

    // Đưa vào danh sách đen mà chưa ghi lý do thì báo ngay.
    await row.dblclick()
    await expect(drawer.getByText(`Nhà cung cấp: ${code}`)).toBeVisible()
    await pick(page, 'Trạng thái', 'Danh sách đen')
    await drawer.getByRole('button', { name: 'Lưu' }).click()
    await expect(drawer.getByText('Hãy ghi lý do.')).toBeVisible()
    await drawer.getByLabel('Lý do').fill('Giao hàng không đúng xuất xứ')
    await drawer.getByRole('button', { name: 'Lưu' }).click()
    await expect(page.locator('.ant-message')).toContainText(`Đã lưu NCC ${code}.`)
    await expect(gridRow(page, code)).toContainText('Danh sách đen')

    await page.waitForLoadState('networkidle')
    await page.screenshot({ path: 'e2e/screenshots/supplier-list.png' })
  })

  test('nhóm NCC có sẵn nhóm mẫu', async ({ page }) => {
    await openByCommand(page, 'SGRP')
    await expect(page.getByRole('heading', { name: 'Nhóm nhà cung cấp' })).toBeVisible()
    await expect(gridRow(page, 'Trong nước')).toBeVisible()
    await expect(gridRow(page, 'Nhập khẩu')).toBeVisible()
    await expect(gridRow(page, 'Nội bộ')).toBeVisible()
  })
})
