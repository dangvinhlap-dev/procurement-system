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

/** Người duyệt bảng giá NCC tại Tổng công ty (dữ liệu mẫu môi trường Testing). */
const PRICE_LIST_APPROVER = { userName: 'duyetgia.tct', password: 'Demo@2026' }

/**
 * Duyệt bảng giá NCC đang chờ duyệt ở Tổng công ty (lát B1 – người lập không tự duyệt) bằng tài khoản duyệt giá.
 * @param priceListId Id bảng giá đã gửi duyệt.
 */
export async function approvePriceList(request: APIRequestContext, priceListId: string) {
  const login = await request.post('/api/auth/login', { data: PRICE_LIST_APPROVER })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { defaultCompanyId: string } }
  const headers = { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.defaultCompanyId }
  const response = await request.post(`/api/price-lists/${priceListId}/approve`, { headers, data: { comment: null } })
  expect(response.ok(), await response.text()).toBeTruthy()
}

/** Người duyệt kết quả chọn thầu tại BHA (dữ liệu mẫu môi trường Testing). */
const RFQ_AWARD_APPROVER = { userName: 'duyetthau.bha', password: 'Demo@2026' }

/**
 * Hội đồng duyệt kết quả chọn thầu RFQ ở BHA (lát B3 – người lập, người chọn thầu không tự duyệt) bằng tài khoản duyệt thầu.
 * @param rfqId Id RFQ đang chờ duyệt kết quả.
 */
export async function approveRfqAward(request: APIRequestContext, rfqId: string) {
  const login = await request.post('/api/auth/login', { data: RFQ_AWARD_APPROVER })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { defaultCompanyId: string } }
  const headers = { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.defaultCompanyId }
  const response = await request.post(`/api/rfqs/${rfqId}/approve`, { headers, data: { comment: null } })
  expect(response.ok(), await response.text()).toBeTruthy()
}

/** Người duyệt hợp đồng nguyên tắc tại BHA (dữ liệu mẫu môi trường Testing). */
const CONTRACT_APPROVER = { userName: 'duyethd.bha', password: 'Demo@2026' }

/**
 * Duyệt hợp đồng nguyên tắc đang chờ duyệt ở BHA (lát B4a – người lập không tự duyệt) bằng tài khoản duyệt hợp đồng.
 * @param contractId Id hợp đồng đã gửi duyệt.
 */
export async function approveContract(request: APIRequestContext, contractId: string) {
  const login = await request.post('/api/auth/login', { data: CONTRACT_APPROVER })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { defaultCompanyId: string } }
  const headers = { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.defaultCompanyId }
  const response = await request.post(`/api/contracts/${contractId}/approve`, { headers, data: { comment: null } })
  expect(response.ok(), await response.text()).toBeTruthy()
}

/**
 * Duyệt phụ lục hợp đồng đang chờ duyệt ở BHA (lát B4b – cùng quyền duyệt hợp đồng) bằng tài khoản duyệt hợp đồng.
 * @param amendmentId Id phụ lục đã gửi duyệt.
 */
export async function approveContractAmendment(request: APIRequestContext, amendmentId: string) {
  const login = await request.post('/api/auth/login', { data: CONTRACT_APPROVER })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { defaultCompanyId: string } }
  const headers = { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.defaultCompanyId }
  const response = await request.post(`/api/contract-amendments/${amendmentId}/approve`, { headers, data: { comment: null } })
  expect(response.ok(), await response.text()).toBeTruthy()
}

/** Người duyệt NCC tại Tổng công ty (dữ liệu mẫu môi trường Testing). */
const SUPPLIER_APPROVER = { userName: 'duyetncc.tct', password: 'Demo@2026' }

/**
 * Duyệt xong các yêu cầu duyệt NCC đang chờ (design.md mục 18.6) bằng tài khoản duyệt NCC ở Tổng công ty – dùng khi dựng NCC "Đã duyệt"
 * cho test nghiệp vụ khác. Gọi ngay sau khi tạo NCC, trước khi áp dụng NCC cho công ty, nên yêu cầu chỉ có chặng Tổng công ty.
 * @param supplierId Id NCC.
 */
export async function approveSupplier(request: APIRequestContext, supplierId: string) {
  const login = await request.post('/api/auth/login', { data: SUPPLIER_APPROVER })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { defaultCompanyId: string } }
  const headers = { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.defaultCompanyId }
  const pending = (await (await request.get(`/api/supplier-change-requests?supplierId=${supplierId}`, { headers })).json()) as
    { id: string; status: string }[]
  for (const r of pending.filter((x) => x.status === 'Submitted')) {
    const response = await request.post(`/api/supplier-change-requests/${r.id}/approve`, { headers, data: { comment: null } })
    expect(response.ok(), await response.text()).toBeTruthy()
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
  await approveSupplier(request, supplier.id)
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
  return { itemCode, itemId: item.id, receiptId: receipt.id, receiptNo: receipt.no, supplierCode, supplierId: supplier.id }
}
