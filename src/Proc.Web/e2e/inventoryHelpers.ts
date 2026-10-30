import { expect, type APIRequestContext, type Page } from '@playwright/test'
import { ADMIN } from './helpers.ts'

/** Hàm dùng chung cho test chứng từ kho (phiếu nhập, trả NCC, chuyển kho). */

/** Mã ngẫu nhiên viết hoa để dữ liệu test không trùng giữa các lần chạy. */
export const unique = (prefix: string) => `${prefix}${Date.now().toString(36).slice(-5)}${Math.floor(Math.random() * 100)}`.toUpperCase()

/**
 * Gọi API bằng tài khoản quản trị trong một công ty.
 * @param companyCode Mã công ty; bỏ trống là công ty mặc định (tổng công ty).
 */
export async function api(request: APIRequestContext, companyCode?: string) {
  const login = await request.post('/api/auth/login', { data: { userName: ADMIN.userName, password: ADMIN.password } })
  const { accessToken, user } = (await login.json()) as {
    accessToken: string
    user: { defaultCompanyId: string; companies: { id: string; code: string }[] }
  }
  const companyId = companyCode ? user.companies.find((c) => c.code === companyCode)!.id : user.defaultCompanyId
  const headers = { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': companyId }
  return {
    get: async <T,>(url: string) => (await (await request.get(url, { headers })).json()) as T,
    send: async <T,>(method: 'post' | 'put', url: string, data: unknown) => {
      const response = await request[method](url, { headers, data })
      expect(response.ok(), await response.text()).toBeTruthy()
      return (await response.json()) as T
    },
  }
}

/** Mở màn hình bằng ô lệnh. */
export async function openByCommand(page: Page, code: string) {
  const box = page.getByPlaceholder('Ô lệnh – tên màn hình')
  await box.fill(code)
  await box.press('Enter')
}

/** Đổi công ty làm việc trên thanh menu. */
export async function switchCompany(page: Page, label: string) {
  await page.getByTestId('menu-bar').locator('.app-company-select').click()
  await page.locator('.ant-select-dropdown .ant-select-item-option', { hasText: label }).click()
}

/** Chọn mục trong danh sách thả xuống đang mở. */
export async function pickOption(page: Page, text: string) {
  await page.locator('.ant-select-dropdown:visible').getByTitle(text, { exact: false }).first().click()
}

/**
 * Chọn trong ô Select có tìm kiếm: gõ để lọc (danh sách thả xuống là danh sách ảo, mục chưa cuộn tới thì chưa có trong DOM) rồi chọn.
 * @param label Nhãn của ô.
 * @param text Chữ gõ để lọc, cũng là chữ của mục cần chọn.
 */
export async function chooseOption(page: Page, label: string, text: string) {
  const box = page.getByRole('combobox', { name: label, exact: true })
  await box.click()
  await box.fill(text)
  await pickOption(page, text)
}
