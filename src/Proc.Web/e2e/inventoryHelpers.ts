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
  await page.getByTestId('shell-bar').locator('.app-company-select').click()
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option', { hasText: label }).click()
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

/** Tạo mặt hàng + NCC áp dụng tại BHA, nhập 10 HOP giá 50.000 (VAT 10%) vào kho KT và ghi sổ. */
export async function seedPostedReceipt(request: APIRequestContext) {
  const head = await api(request)
  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const hop = uoms.find((u) => u.code === 'HOP')!.id
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const category = await head.send<{ id: string; code: string }>('post', '/api/item-categories', { code: null, name: 'Nhóm trả/chuyển', parentId: null, inventoryAccount: null })
  // Mã hàng do hệ thống cấp theo dải đánh số (design.md mục 5.11).
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: 'Bulông thử trả chuyển', englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: hop,
    specification: null, brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, trackLot: false,
    trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })
  const itemCode = item.code
  const supplierTag = unique('NCC')
  const supplier = await head.send<{ id: string; code: string }>('post', '/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: `NCC trả hàng ${supplierTag}`, shortName: null,
    isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Approved', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })
  const supplierCode = supplier.code
  const bha = await api(request, 'BHA')
  await bha.send('put', `/api/suppliers/${supplier.id}/company`, { localCode: null, startDate: null, isActive: true })
  const warehouses = await bha.get<{ id: string; code: string }[]>('/api/warehouses')
  const receipt = await bha.send<{ id: string; no: string }>('post', '/api/goods-receipts', {
    date: new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }),
    warehouseId: warehouses.find((w) => w.code === 'KT')!.id, supplierId: supplier.id, invoiceNo: null, invoiceSeries: null,
    invoiceDate: null, deliveryNoteNo: null, deliverer: null, note: null,
    lines: [{ id: null, itemId: item.id, uomId: hop, qty: 10, unitPrice: 50000, vatCode: '10', lotNo: null, expiryDate: null, note: null }],
  })
  await bha.send('post', `/api/inventory-documents/${receipt.id}/post`, {})
  return { itemCode, itemId: item.id, receiptNo: receipt.no, supplierCode }
}
